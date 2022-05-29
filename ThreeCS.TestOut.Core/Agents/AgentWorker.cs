using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ThreeCS.TestOut.Core.Agents.Messages;
using ThreeCS.TestOut.Core.Agents.Models;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Agents
{
    public class AgentWorker : IAsyncDisposable
    {
        readonly AgentConfig _config;
        readonly IMessageBusClient _messageBus;
        readonly AgentTestRunner _testRunner;
        readonly ILogger<AgentWorker> _logger;
        readonly ServerConnectionConfig _serverConfig;
        readonly HostInfo _hostInfo;
        readonly HeartbeatSender _heartbeatSender;

        private AgentInfo _info;
        private RunningTestData _currentTest;

        public AgentWorker(
            AgentConfig config,
            IMessageBusClient messageBus,
            AgentTestRunner testRunner,
            ILogger<AgentWorker> logger,
            ServerConnectionConfig serverConfig,
            HostInfo hostInfo, 
            HeartbeatSender heartbeatSender)
        {
            _config = config;
            _messageBus = messageBus;
            _testRunner = testRunner;
            _logger = logger;
            _serverConfig = serverConfig;
            _hostInfo = hostInfo;
            _heartbeatSender = heartbeatSender;
        }

        public async Task Init(int ix) 
        {
            //Suffix the host id with this agents index.
            _hostInfo.HostId = _hostInfo.HostId + "_" + ix;
            _info = new AgentInfo
            {
                Id = _hostInfo.HostId,
            };

            //Setup message handlers.
            _messageBus.OnMessageReceived<RunAgentTestsMessage>(HandleRunTests);
            _messageBus.OnMessageReceived<RequestAgentsMessage>(HandleRequestAgentsMessage);
            _messageBus.OnMessageReceived<CancelAgentTestsMessage>(HandleCancelAgentTests);

            //Register with message bus.
            await _messageBus.Register(_info.Id);
        }

        private async Task HandleCancelAgentTests(string senderId, CancelAgentTestsMessage message)
        {
            var curTest = _currentTest;
            if (message.RequestId == curTest?.RequestId)
            {
                _logger.LogDebug("Received cancel request for running request {@requestId}, cancelling current run.", message.RequestId);
                curTest.CancellationSource.Cancel();
            }
        }

        private async Task HandleRequestAgentsMessage(string senderId, RequestAgentsMessage message)
        {
            //Check if we're running tests, and if not, respond with 'ready'.
            //TODO: should be using a threadsafe method, but worst case we just get 2 lots of tests running for one agent if the request agents message happens just as this finishes
            //a process, which should be pretty unlikely.
            if (_currentTest == null)
            {
                await _messageBus.SendMessage(_serverConfig.ServerId, new AgentReadyMessage { AgentId = _info.Id });
            }
        }

        /// <summary>
        /// TODO: thread safety
        /// </summary>
        private async Task HandleRunTests(string senderId, RunAgentTestsMessage message)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation("Running {@testCount} tests: {@tests}.", message.TestsToRun.Count, message.TestsToRun.Select(n => n.FullTestName).ToList());
            }
            else
            {
                _logger.LogInformation("Running {@testCount} tests.", message.TestsToRun.Count);
            }

            var curTest = _currentTest;
            if (curTest != null)
            {
                //Fail immediately, agents can't queue tests yet.
                _logger.LogError("Run Tests Requested for {@agentId} but this agent is already processing tests.", _hostInfo.HostId);
                await _messageBus.SendMessage(_serverConfig.ServerId, new AgentTestsCompleteMessage
                {
                    InvocationId = message.InvocationSpec.Id,
                    RequestId = message.RequestId,
                    Error = "Agent " + _hostInfo.HostId + " can't accept this run request when another run (" + curTest.RequestId + ") is in progress."
                });
            }
            else
            {
                var finished = new CancellationTokenSource();
                var finishedToken = finished.Token;

                var testRunData = new RunningTestData
                {
                    Invocation = message.InvocationSpec,
                    RequestId = message.RequestId,
                    Tests = message.TestsToRun,
                    CancellationToken = finishedToken,
                    CancellationSource = finished
                };
                _currentTest = testRunData;

                //Run the tests.
                var msg = new AgentTestsCompleteMessage
                {
                    InvocationId = message.InvocationSpec.Id,
                    RequestId = message.RequestId,
                };

                try
                {
                    await using var heartbeat = _heartbeatSender.StartHeartbeat(new AgentTestRunHeartbeatMessage { RequestId = message.RequestId });

                    //Start the tests and await their result.
                    var result = await _testRunner.Run(testRunData);// message.InvocationSpec, message.TestsToRun);

                    await heartbeat.Stop();

                    //Record the results.
                    msg.TestResults = result.TestResults;

                    //Add the agent id to the test results.
                    msg.TestResults.ForEach(n => n.AgentId = _info.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running tests {@tests}", message.TestsToRun.Select(n => n.FullTestName).ToList());
                    msg.Error = ex.Message;
                }

                //Send the test results.
                await _messageBus.SendMessage(_serverConfig.ServerId, msg);

                //Broadcast the ready message.
                _currentTest = null;
                await _messageBus.SendMessage(_serverConfig.ServerId, new AgentReadyMessage { AgentId = _info.Id });
            }
        }

        public async ValueTask DisposeAsync()
        {
            //TODO: Cancel any tests.

            //Notify that we're disconnecting.
            await _messageBus.BroadcastMessage(new DeregisterAgentMessage());
            await _messageBus.DisposeAsync();
        }
    }
}
