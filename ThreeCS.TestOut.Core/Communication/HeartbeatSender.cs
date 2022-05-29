using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Servers;
using ThreeCS.TestOut.Core.Utility;

namespace ThreeCS.TestOut.Core.Communication
{
    /// <summary>
    /// Class that handles sending a heartbeat on a cancellable timer.
    /// </summary>
    public class HeartbeatSender
    {
        readonly IMessageBusClient _messageBus;
        readonly ServerConnectionConfig _serverConfig;

        public record RunningHeartbeat(
            CancellationTokenSource tokenSource, 
            Task heartbeatTask
            ) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                if (!tokenSource.IsCancellationRequested)
                {
                    await Stop();
                }
            }

            public Task Stop()
            {
                tokenSource.Cancel();
                return heartbeatTask;
            }
        }

        public HeartbeatSender(
            IMessageBusClient messageBus,
            ServerConnectionConfig serverConfig)
        {
            _messageBus = messageBus;
            _serverConfig = serverConfig;
        }

        public RunningHeartbeat StartHeartbeat<TMessage>(TMessage heartbeatMessage)
        {
            var finishedTokenSource = new CancellationTokenSource();
            var finishedToken = finishedTokenSource.Token;

            var heartbeatTask = TaskHelpers.StartLongRunning(async () =>
            {
                while (!finishedToken.IsCancellationRequested)
                {
                    //Delay, but if cancelled by token, ignore the error (ie, the continue with being empty)
                    await Task.Delay(30000, finishedToken).ContinueWith(_ => { });

                    //Send a heartbeat.
                    if (!finishedToken.IsCancellationRequested && _messageBus.IsConnected)
                    {
                        await _messageBus.SendMessage(_serverConfig.ServerId, heartbeatMessage);
                    }
                }
            });

            return new RunningHeartbeat(finishedTokenSource, heartbeatTask);
        }
    }
}
