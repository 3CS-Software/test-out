using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using ThreeCS.TestOut.Core.Agents.Messages;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Invokers;
using ThreeCS.TestOut.Core.Invokers.Messages;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Execution
{
    public class Invoker : IAsyncDisposable
    {
        readonly InvokerConfig _config;
        readonly IMessageBusClient _messageBus;
        readonly InvocationResourcePackager _resourcePackager;
        readonly IResultSerializer _resultSerializer;
        readonly ILogger<Invoker> _logger;
        readonly HostInfo _hostInfo;
        readonly ServerConnectionConfig _serverConfig;
        readonly FileTransferHandler _fileTransferHandler;
        readonly HeartbeatSender _heartbeatSender;

        private InvokerInfo _invokerInfo;
        private AsyncManualResetEvent _completedEvent;
        private TestsCompleteMessage _resultMessage = null;
        private Timer _reconnectTimeoutTimer;

        public Invoker(
            InvokerConfig config,
            IMessageBusClient messageBus,
            InvocationResourcePackager resourcePackager,
            IResultSerializer resultSerializer,
            ILogger<Invoker> logger,
            HostInfo hostInfo,
            ServerConnectionConfig serverConfig,
            FileTransferHandler fileTransferHandler, 
            HeartbeatSender heartbeatSender)
        {
            _config = config;
            _messageBus = messageBus;
            _resourcePackager = resourcePackager;
            _resultSerializer = resultSerializer;
            _logger = logger;
            _hostInfo = hostInfo;
            _serverConfig = serverConfig;
            _fileTransferHandler = fileTransferHandler;
            _heartbeatSender = heartbeatSender;
        }

        /// <summary>
        /// TODO: Add StartTests() method which just returns the invocation id, and runs tests in the background.
        /// </summary>
        /// <returns></returns>
        public async Task RunTests()
        {
            _reconnectTimeoutTimer = new Timer();
            _reconnectTimeoutTimer.AutoReset = false;
            _reconnectTimeoutTimer.Interval = 120 * 1000;
            _reconnectTimeoutTimer.Elapsed += _reconnectTimeoutTimer_Elapsed;

            _completedEvent = new AsyncManualResetEvent();

            _invokerInfo = new InvokerInfo
            {
                Id = _hostInfo.HostId
            };

            _logger.LogDebug("Beginning Invocation: {@invocation}", _invokerInfo);

            _messageBus.OnMessageReceived<TestsCompleteMessage>(HandleTestsCompleted);
            _messageBus.OnMessageReceived<AgentTestStatusUpdateMessage>(OnAgentTestStatusUpdate);
            _messageBus.OnMessageReceived<TestsProgressMessage>(OnTestsProgress);
            _messageBus.Reconnected += _messageBus_Reconnected;
            _messageBus.Disconnected += _messageBus_Disconnected;

            await _messageBus.Register(_invokerInfo.Id);

            //Prepare a folder to transmit to the server.
            var testBasePath = await _resourcePackager.Prepare(_invokerInfo.Id);

            //Broadcast to the server with the details required to start the test.
            var invocationId = "Invocation_" + Guid.NewGuid().ToString();
            await _messageBus.SendMessage(_serverConfig.ServerId, new InvokeTestsMessage
            {
                InvocationId = invocationId,
                InvokerInfo = _invokerInfo,
                SourcePath = testBasePath,
                TestAssemblyPath = _config.TestAssemblyPath,
                MaxRetryCount = _config.MaxRetryCount,
                TestInactivityTimeoutSeconds = _config.TestInactivityTimeoutSeconds
            });

            await using var heartbeat = _heartbeatSender.StartHeartbeat(new InvokerHeartbeatMessage { InvocationId = invocationId });

            await _completedEvent.WaitAsync();

            await heartbeat.Stop();

            //Check if we have a result.  We may not if the network was terminally disconnected, or this is cancelling.
            if (_resultMessage != null)
            {
                if (!string.IsNullOrEmpty(_resultMessage.Error))
                {
                    _logger.LogError(_resultMessage.Error);
                }
                else
                {
                    await _resultSerializer.SerializeAsync(_config.ResultFilename, _resultMessage.Results);
                }
            }

            _logger.LogDebug("Invocation Complete: {@invocation}", _invokerInfo);
        }

        private void _reconnectTimeoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _logger.LogError("Connection retry to server timed out.");
            _completedEvent.Set();
        }

        private async Task OnTestsProgress(string senderId, TestsProgressMessage message)
        {
            _logger.LogInformation("Progress Update: Completed " + message.CompletedTestsCount + " tests.  " + message.Message);
        }

        private async Task _messageBus_Reconnected()
        {
            _reconnectTimeoutTimer.Stop();
        }

        private async Task _messageBus_Disconnected()
        {
            _logger.LogWarning("Lost connection to server.");

            _reconnectTimeoutTimer.Start();
        }

        private async Task OnAgentTestStatusUpdate(string senderId, AgentTestStatusUpdateMessage message)
        {
            //Just log this.
            string logMessage = message.Test.FullTestName + " " + message.UpdateType;
            if (!string.IsNullOrEmpty(message.MessageText))
            {
                logMessage += ": " + message.MessageText;
            }
            logMessage += " (from " + senderId + ")";
            _logger.LogInformation(logMessage);
        }

        private async Task HandleTestsCompleted(string senderId, TestsCompleteMessage message)
        {
            _resultMessage = message;
            _completedEvent.Set();
        }

        public async ValueTask DisposeAsync()
        {
            await _messageBus.DisposeAsync();
        }
    }
}
