using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents.Messages;
using ThreeCS.TestOut.Core.Agents.Models;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers;

namespace ThreeCS.TestOut.Core.Agents
{
    /// <summary>
    /// Sends individual test progress to the server.
    /// </summary>
    public class AgentTestProgressNotifier
    {
        readonly IMessageBusClient _messageBus;
        readonly ServerConnectionConfig _serverConfig;

        public AgentTestProgressNotifier(
            IMessageBusClient messageBus,
            ServerConnectionConfig serverConfig)
        {
            _messageBus = messageBus;
            _serverConfig = serverConfig;
        }

        /// <summary>
        /// Notifies the invoker and the server of test progress.
        /// </summary>
        public async Task NotifyStatusUpdate(RunningTestData runningTestData, TestSpec test, StatusUpdateType updateType, string additionalMessage = null) 
        {
            //Prepare an update message.
            var updateMessage = new AgentTestStatusUpdateMessage
            {
                InvocationId = runningTestData.Invocation.Id,
                AgentRunRequestId = runningTestData.RequestId,
                Test = test,
                UpdateType = updateType,
                MessageText = additionalMessage
            };

            //Let the invoker know of the progress.
            await _messageBus.SendMessage(runningTestData.Invocation.InvokerId, updateMessage);

            //Let the server know, so it can update the 'last activity' to keep an eye on these tests.
            await _messageBus.SendMessage(_serverConfig.ServerId, updateMessage);
        }
    }
}
