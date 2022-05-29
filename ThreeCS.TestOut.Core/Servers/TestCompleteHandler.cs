using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Servers
{
    /// <summary>
    /// Handles tests being completed on agents.
    /// </summary>
    public class TestCompleteHandler
    {
        readonly ILogger<TestCompleteHandler> _logger;
        readonly IMessageBusClient _messageBus;
        readonly ServerTestState _state;

        public TestCompleteHandler(
            ILogger<TestCompleteHandler> logger,
            IMessageBusClient messageBus,
            ServerTestState state)
        {
            _logger = logger;
            _messageBus = messageBus;
            _state = state;
        }

        public virtual void Init()
        {
            _messageBus.OnMessageReceived<AgentTestsCompleteMessage>(AgentTestsCompleteMessageHandler);
        }

        private async Task AgentTestsCompleteMessageHandler(string senderId, AgentTestsCompleteMessage message)
        {
            var agentDelegatedTestRun = _state.RemoveAgentDelegatedRun(message.InvocationId, message.RequestId);

            if (agentDelegatedTestRun != null)
            {
                var invocationSpec = _state.GetInvocationSpec(message.InvocationId);

                //Get the tests and update them as required.
                if (message.Error != null)
                {
                    //All the tests on the agent failed, mark them as such from the invoked ones.
                    foreach (var test in agentDelegatedTestRun.PartialTests.TestsToRun)
                    {
                        var repoTestResult = _state.GetTestResult(message.InvocationId, test.FullTestName);
                        if (repoTestResult.Outcome == null)
                        {
                            repoTestResult.Outcome = TestExecutionOutcome.Failed;
                            repoTestResult.ErrorMessage = message.Error;
                            repoTestResult.StartDateTime = DateTime.Now;
                            repoTestResult.EndDateTime = DateTime.Now;
                            repoTestResult.AgentId = agentDelegatedTestRun.AgentId;
                            repoTestResult.AttemptCount++;

                            await CompleteOrRequeue(invocationSpec, repoTestResult);
                        }
                    }
                }
                else
                {
                    //Update from the results.
                    foreach (var agentTestResult in message.TestResults)
                    {
                        var repoTestResult = _state.GetTestResult(message.InvocationId, agentTestResult.Spec.FullTestName);

                        //Set it back to the repo.
                        repoTestResult.Outcome = agentTestResult.Outcome;
                        repoTestResult.SerialisedResult = agentTestResult.SerialisedResult;
                        repoTestResult.StandardOutput = agentTestResult.StandardOutput;
                        repoTestResult.TraceOutput = agentTestResult.TraceOutput;
                        repoTestResult.ErrorOutput = agentTestResult.ErrorOutput;
                        repoTestResult.StackTrace = agentTestResult.StackTrace;
                        repoTestResult.ErrorMessage = agentTestResult.ErrorMessage;
                        repoTestResult.StartDateTime = agentTestResult.StartDateTime;
                        repoTestResult.EndDateTime = agentTestResult.EndDateTime;
                        repoTestResult.AgentId = agentTestResult.AgentId;
                        repoTestResult.AttemptCount++;

                        await CompleteOrRequeue(invocationSpec, repoTestResult);
                    }
                }
            }
            else
            {
                _logger.LogDebug("Received a complete message from {@senderId} for a run that no longer exists, Ingoring.", senderId);
            }
        }

        public async Task CompleteOrRequeue(TestInvocationSpec invocationSpec, TestExecutionInfo testInfo)
        {
            var completed = _state.GetCompletedTestCount(invocationSpec.Id);
            if (testInfo.Outcome == TestExecutionOutcome.Failed && testInfo.AttemptCount <= invocationSpec.MaxRetryCount)
            {
                //Push this test again.
                testInfo.Outcome = null;
                await _messageBus.SendMessage(invocationSpec.InvokerId, new TestsProgressMessage
                {
                    Message = $"Test '{testInfo.Spec.FullTestName}' failed on agent '{testInfo.AgentId}', and is being retried (current attempt count {testInfo.AttemptCount})",
                    CompletedTestsCount = completed
                });
            }
            else
            {
                await _messageBus.SendMessage(invocationSpec.InvokerId, new TestsProgressMessage
                {
                    Message = $"Test '{testInfo.Spec.FullTestName}' finished on agent '{testInfo.AgentId}' with outcome '{testInfo.Outcome}'.",
                    CompletedTestsCount = completed
                });
            }

            _state.SetTestResult(invocationSpec.Id, testInfo);

            if (_state.HasCompleted(invocationSpec.Id))
            {
                //Get the completed result from the state repo.
                var result = _state.GetCompletedTestRun(invocationSpec.Id);

                //Notify the invoker that we're done.
                await _messageBus.SendMessage(result.InvocationSpec.InvokerId, new TestsCompleteMessage
                {
                    Results = new TestInvocationExecutionInfo
                    {
                        Spec = result.InvocationSpec,
                        Tests = new List<TestExecutionInfo>(result.TestsByFullName.Values)
                    }
                });
                _state.RemoveTestRun(invocationSpec.Id);
            }
        }
    }
}
