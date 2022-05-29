using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers;

namespace ThreeCS.TestOut.Core.Hosting
{
    public class Server
    {
        readonly IMessageBusClient _messageBus;
        readonly ServerConnectionConfig _serverConfig;
        readonly ILogger<Server> _logger;

        readonly TestStarter _testStarter;
        readonly TestHealthMonitor _healthMonitor;
        readonly TestCompleteHandler _testCompleteHandler;
        readonly FileTransferStreamServer _fileTransferServer;

        public Server(
            IMessageBusClient messageBus,
            ServerConnectionConfig serverConfig,
            ILogger<Server> logger,
            TestStarter testStarter,
            TestHealthMonitor healthMonitor,
            TestCompleteHandler testCompleteHandler, 
            FileTransferStreamServer fileTransferServer)
        {
            _messageBus = messageBus;
            _serverConfig = serverConfig;
            _logger = logger;
            _testStarter = testStarter;
            _healthMonitor = healthMonitor;
            _testCompleteHandler = testCompleteHandler;
            _fileTransferServer = fileTransferServer;
        }

        public async Task StartServer()
        {
            //Start all the supporting services.
            await _fileTransferServer.StartAsync(CancellationToken.None);

            //Register the server onto the message bus.
            await _messageBus.Register(_serverConfig.ServerId);
            await _messageBus.BroadcastMessage(CreateServerRegisteredMessage());

            _testStarter.Init();
            _healthMonitor.Init();
            _testCompleteHandler.Init();
        }

        private ServerRegisteredMessage CreateServerRegisteredMessage()
        {
            return new ServerRegisteredMessage
            {
                ServerInfo = new ServerInfo { Id = _serverConfig.ServerId }
            };
        }

        public async Task StopServer()
        {
            //TODO: send out cancel to all tests, etc.
            await _fileTransferServer.StopAsync(CancellationToken.None);
            await _messageBus.DisposeAsync();
        }
    }
}
