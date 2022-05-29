using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents.Messages;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Servers
{
    /// <summary>
    /// Adds new tests to the repo, and notifies agents of pending work.
    /// </summary>
    public class TestStarter
    {
        readonly IMessageBusClient _messageBus;
        readonly ILogger<TestStarter> _logger;
        readonly TestRetriever _testRetriever;
        readonly ServerTestState _state;

        public TestStarter(
            IMessageBusClient messageBus,
            ILogger<TestStarter> logger,
            TestRetriever testRetriever,
            ServerTestState state)
        {
            _messageBus = messageBus;
            _logger = logger;
            _testRetriever = testRetriever;
            _state = state;
        }

        public void Init()
        {
            _messageBus.OnMessageReceived<InvokeTestsMessage>(HandleInvokeTestsMessage);
            _messageBus.OnMessageReceived<AgentReadyMessage>(HandleAgentReadyMessage);
        }

        private async Task HandleInvokeTestsMessage(string senderId, InvokeTestsMessage message)
        {
            var testRunSpec = new TestInvocationSpec
            {
                Id = message.InvocationId,
                InvokerId = senderId,
                SourcePath = message.SourcePath,
                TestAssemblyPath = message.TestAssemblyPath,
                MaxRetryCount = message.MaxRetryCount,
                TestInactivityTimeoutSeconds = message.TestInactivityTimeoutSeconds,
                RequestedAt = DateTime.Now
            };

            _logger.LogInformation("Running Tests in {@assemblyPath} from {@invoker}: id {@testRunId}", testRunSpec.TestAssemblyPath, testRunSpec.InvokerId, testRunSpec.Id);

            //Enumerate the tests.  This should return them in the approximate order we want to 
            //process them in.
            var testsToRun = await _testRetriever.RetrieveTestsToExecute(testRunSpec);

            //Put this run into the repo.
            try
            {
                _state.AddTestRun(testRunSpec, testsToRun);

                //Trigger a request for free agents to start the processing.
                await RequestFreeAgents(testRunSpec);
            }
            catch (Exception ex)
            {
                //We couldn't add the test run.  Let the invoker know immediately, so it can cancel the tests, and show the error.
                _logger.LogError(ex, "Test run could not start.");
                _state.RemoveTestRun(message.InvocationId);
                await _messageBus.SendMessage(senderId, new TestsCompleteMessage { Error = "An error occurred starting the tests: " + ex.Message });
            }
        }

        private async Task HandleAgentReadyMessage(string senderId, AgentReadyMessage message)
        {
            //Send some tests to this agent.  TODO: make the batch size come down as the run progresses.
            var nextTests = _state.GetNextTests();
            if (nextTests != null)
            {
                _logger.LogDebug($"Sending Agent {senderId} tests to run: {string.Join(",", nextTests.TestsToRun.Select(n => n.FullTestName))}");
                AgentDelegatedTestRun agentDelegatedTestRun = _state.AddAgentDelegatedRun(senderId, nextTests);
                await _messageBus.SendMessage(senderId, new RunAgentTestsMessage
                {
                    RequestId = agentDelegatedTestRun.RequestId,
                    InvocationSpec = agentDelegatedTestRun.PartialTests.InvocationSpec,
                    TestsToRun = agentDelegatedTestRun.PartialTests.TestsToRun
                });
            }
            else
            {
                _logger.LogDebug($"Agent {senderId} ready, but no tests to queue.");
            }
        }

        /// <summary>
        /// Message all the agents, asking if they can process the given test spec
        /// </summary>
        private async Task RequestFreeAgents(TestInvocationSpec testInvocationSpec)
        {
            await _messageBus.BroadcastMessage(new RequestAgentsMessage
            {
                InvocationSpec = testInvocationSpec
            });
        }
    }
}
