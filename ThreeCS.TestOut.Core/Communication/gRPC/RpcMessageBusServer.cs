using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using MsgBusServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication.gRPC;
using ThreeCS.TestOut.Core.Hosting;
using GrpcServer = Grpc.Core.Server;

namespace ThreeCS.TestOut.Core.Communication.gPRC
{
    internal class MessageBusServerImpl : MessageBusServer.MessageBusServerBase
    {
        //We'll log all these under the RpcMessageBusServer banner.
        readonly ILogger<RpcMessageBusServer> _logger;

        public MessageBusServerImpl(ILogger<RpcMessageBusServer> logger)
        {
            _logger = logger;
        }

        ConcurrentDictionary<string, BlockingCollection<GrpcHubMessage>> messagePumpsByClientId = new();

        //Nothing to do for this except handle the ping.
        public override Task<Empty> Ping(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> Register(GrpcClientInfo request, ServerCallContext context)
        {
            var block = new BlockingCollection<GrpcHubMessage>();
            messagePumpsByClientId.TryAdd(request.HostId, block);

            _logger.LogInformation("{@host} registered", request.HostId);

            return new Empty();
        }

        /// <summary>
        /// This is used by clients to receive messages.  They call this, and this then uses a blocking collection to feed them messages as they 
        /// become available, which is the 'grpc' way of push notifications.
        /// </summary>
        public override async Task ReceiveMessages(GrpcClientInfo request, IServerStreamWriter<GrpcHubMessage> responseStream, ServerCallContext context)
        {
            try
            {
                if (messagePumpsByClientId.TryGetValue(request.HostId, out var block))
                {
                    foreach (var item in block.GetConsumingEnumerable(context.CancellationToken))
                    {
                        if (!context.CancellationToken.IsCancellationRequested)
                        {
                            _logger.LogTrace("Delivering message from {@senderId} to {@recipientId}.", item.SenderId, request.HostId);
                            await responseStream.WriteAsync(item);
                        }
                    }
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("{@host} stopped receiving messages.", request.HostId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{@host} stopped receiving messages with an expected error.", request.HostId);
            }

            //Unregister this client.
            if (messagePumpsByClientId.TryRemove(request.HostId, out var blockToFinish))
            {
                blockToFinish.CompleteAdding();
            }
        }

        public override async Task<Empty> SendMessage(GrpcHubMessage request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.RecipientId))
            {
                //Send this to all clients, except the sender.
                var allClients = messagePumpsByClientId.Keys.ToList();
                foreach (var client in allClients)
                {
                    if (client != request.SenderId)
                    {
                        _logger.LogTrace("Queueing broadcast message from {@senderId} to {@recipientId}.", request.SenderId, client);
                        SendIndividualMessage(request, client);
                    }
                }
            }
            else
            {
                _logger.LogTrace("Queueing individual message from {@senderId} to {@recipientId}.", request.SenderId, request.RecipientId);
                SendIndividualMessage(request, request.RecipientId);
            }

            //Hmm..
            return new Empty();
        }

        private void SendIndividualMessage(GrpcHubMessage request, string recipientId)
        {
            if (messagePumpsByClientId.TryGetValue(recipientId, out var blockingColl))
            {
                blockingColl.Add(request);
            }
        }

        public async Task Stop()
        {
            foreach (var item in messagePumpsByClientId)
            {
                item.Value.CompleteAdding();
            }
        }

    }

    public class RpcMessageBusServer : IMessageBusServer
    {
        readonly ILogger<RpcMessageBusServer> _logger;
        readonly ServerConnectionConfig _serverConfig;

        private MessageBusServerImpl _implementation;
        private GrpcServer _server;

        public RpcMessageBusServer(
            ILogger<RpcMessageBusServer> logger,
            ServerConnectionConfig serverConfig)
        {
            _logger = logger;
            _serverConfig = serverConfig;
        }

        public async Task Init()
        {
            _implementation = new MessageBusServerImpl(_logger);
            Uri uri = new Uri(_serverConfig.ServerUrl);

            _server = new GrpcServer(RpcChannelOptions.DefaultChannelOptions);

            _server.Services.Add(MessageBusServer.BindService(_implementation));
            _server.Ports.Add(new ServerPort(uri.Host, uri.Port, ServerCredentials.Insecure));

            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _implementation.Stop();
            await _server.ShutdownAsync();
        }
    }
}
