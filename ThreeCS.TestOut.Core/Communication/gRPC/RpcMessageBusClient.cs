using Grpc.Core;
using Microsoft.Extensions.Logging;
using MsgBusServer;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication.Common;
using ThreeCS.TestOut.Core.Communication.gRPC;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Utility;
using static MsgBusServer.MessageBusServer;

namespace ThreeCS.TestOut.Core.Communication.gPRC
{
    public class RpcMessageBusClient : IMessageBusClient
    {
        readonly ILogger<RpcMessageBusClient> _logger;
        readonly ServerConnectionConfig _serverConfig;

        private Channel _channel;
        private MessageBusServerClient _client;
        private AsyncServerStreamingCall<GrpcHubMessage> _stream;
        private Task _pumpTask;
        private Task _monitorTask;

        private string _sendAsId;
        private string _rpcUrl;
        private bool _disposing;

        private ConcurrentDictionary<string, MessageTypeRegistrations> _messageRegistrationsByFullTypeName;

        public RpcMessageBusClient(
            ILogger<RpcMessageBusClient> logger,
            ServerConnectionConfig serverConfig)
        {
            _logger = logger;
            _serverConfig = serverConfig;

            _messageRegistrationsByFullTypeName = new ConcurrentDictionary<string, MessageTypeRegistrations>();
        }

        
        
        public IDisposable OnMessageReceived<TMessage>(MessageReceivedDelegate<TMessage> handler)
        {
            string typefullName = typeof(TMessage).FullName;
            MessageRegistration registration = new MessageRegistration
            {
                Callback = handler
            };

            _messageRegistrationsByFullTypeName.AddOrUpdate(typefullName,
                addValueFactory: (typefullName) =>
                {
                    return new MessageTypeRegistrations
                    {
                        MessageType = typeof(TMessage),
                        Registrations = new(new[] { registration })
                    };
                },
                updateValueFactory: (typeFullName, existingRegistrations) =>
                {
                    existingRegistrations.Registrations.Add(registration);
                    return existingRegistrations;
                });

            return registration;
        }

        public async Task Register(string recipientId)
        {
            _logger.LogDebug("Registering client {hostId}", recipientId);

            _sendAsId = recipientId;
            Uri uri = new Uri(_serverConfig.ServerUrl);
            _rpcUrl = uri.Host + ":" + uri.Port;

            _channel = new Channel(_rpcUrl, ChannelCredentials.Insecure, RpcChannelOptions.DefaultChannelOptions);

            _client = new MessageBusServerClient(_channel);

            try
            {
                await _client.PingAsync(new());
                _logger.LogDebug("First connect successful for {hostId}", recipientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "First connect was unsuccessful");
                //In this case we don't care about waiting for the pump event, we'll just let the call return to caller as we need
                //to fix it in the background.
            }

            await CreateMonitorTask();
        }

        private async Task StartPump()
        {
            //Start the pumping.
            bool isInitialConnect = _stream == null;
            try
            {
                await _client.RegisterAsync(new GrpcClientInfo { HostId = _sendAsId });
                _stream = _client.ReceiveMessages(new GrpcClientInfo { HostId = _sendAsId });
                _pumpTask = TaskHelpers.StartLongRunning(PumpTask);
                _logger.LogDebug("{@sendAsId} pump setup.", _sendAsId);
                if (!isInitialConnect)
                {
                    Reconnected?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{@sendAsId} couldn't setup pump.", _sendAsId);
            }
        }

        private async Task CreateMonitorTask()
        {

            var prevState = _channel.State;

            //Check if this is already connected, and if so, start pumping immediately.
            if (prevState == ChannelState.Ready)
            {
                await StartPump();
            }

            _monitorTask = TaskHelpers.StartLongRunning(async () =>
            {
                while (true)
                {
                    await _channel.WaitForStateChangedAsync(prevState);
                    prevState = _channel.State;

                    if (_disposing)
                        break;

                    _logger.LogDebug("{@sendAsId} Grpc State Changed: {@state}", _sendAsId, _channel.State);

                    if (_channel.State == ChannelState.Shutdown)
                    {
                        //This has to end.
                        break;
                    }
                    else if (_stream != null && _channel.State == ChannelState.Idle)
                    {
                        _logger.LogDebug("{@sendAsId} Idle detected, pinging to reactivate", _sendAsId);
                        Disconnected?.Invoke();
                        try
                        {
                            await _client.PingAsync(new());
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "{@sendAsId} ping unsuccessful, but should have triggered more channel reconnect attempts.", _sendAsId);
                        }
                        //await _client.RegisterAsync(new MsgBusServer.ClientInfo { HostId = _clientName }, deadline: DateTime.Now.AddHours(4));
                    }
                    else if (_channel.State == ChannelState.Ready)
                    {
                        await StartPump();
                    }
                }

            });
        }

        public bool IsConnected
        {
            get
            {
                //TODO: better than this.
                return _channel.State == ChannelState.Ready;
            }
        }

        public event Func<Task> Reconnected;
        public event Func<Task> Disconnected;

        public Task SendMessage<TMessage>(string recipientId, TMessage message)
            => SendMessageImpl(recipientId, message);

        public Task BroadcastMessage<TMessage>(TMessage message)
            => SendMessageImpl("", message);

        private async Task SendMessageImpl<TMessage>(string recipientId, TMessage message)
        {
            _logger.LogDebug("{@sendAsId} sending message {@message} to {@recipientId}", _sendAsId, typeof(TMessage).FullName, recipientId);

            //Send this message.  TODO: disconnected message queueing.  For now, disconnected messages just dissappear into the ether.
            if (_channel?.State != ChannelState.Ready)
            {
                //Bam, and the message is gone.
                _logger.LogWarning("Message has gone into the abyss as current channel state is '{@channelState}'.", _channel?.State.ToString() ?? "<null>");
            }
            else
            {
                try
                {
                    //Todo: In future we can map out all these messages via grpc.
                    // For now, we're incurring json serialization.
                    var packet = JsonSerializer.Serialize(new MessageBusMessage
                    {
                        SenderId = _sendAsId,
                        RecipientId = recipientId,
                        FullTypeName = typeof(TMessage).FullName,
                        MessageJson = JsonSerializer.Serialize(message)
                    });

                    await _client.SendMessageAsync(new GrpcHubMessage { SenderId = _sendAsId, MessageData = packet, RecipientId = recipientId });
                }
                catch (Exception ex)
                {
                    //TODO: kill the connection?.. 
                    _logger.LogWarning(ex, "{@sendAsId}: Error while sending message, it will not be delivered.", _sendAsId);
                }
            }
        }

        private async Task PumpTask()
        {
            try
            {
                var responseStream = _stream.ResponseStream;
                while (await responseStream.MoveNext())
                {
                    //TODO: should pass in a cancellation token above instead, and check it here.
                    if (_disposing)
                        break;

                    var receivedMsg = responseStream.Current;
                    //Work out what to do with this message:
                    HandleMessage(receivedMsg);
                }
            }
            catch (RpcException ex)
            {
                if (ex.StatusCode == StatusCode.Cancelled)
                {
                    //All good.
                }
                else if (ex.Status.Detail == "Stream removed")
                {
                    //Server or connection has balked.  We'll just die.
                    _logger.LogDebug(ex, "{@sendAsId} Stream Removed, pump task closing.  Could be due to server disconnect.", _sendAsId);
                }
                else
                {
                    _logger.LogWarning(ex, "{@sendAsId} Pump task had an unexpected error.", _sendAsId);
                    throw;
                }
            }
        }

        private void HandleMessage(GrpcHubMessage receivedMsg)
        {
            _logger.LogTrace("{@sendAsId} handling client message", _sendAsId);
            var message = JsonSerializer.Deserialize<MessageBusMessage>(receivedMsg.MessageData);
            var messageTypeFullName = message.FullTypeName;

            var recipientId = message.RecipientId;
            var senderId = message.SenderId;
            if (recipientId == "" || recipientId == _sendAsId)
            {
                //This is for us.
                if (_messageRegistrationsByFullTypeName.TryGetValue(messageTypeFullName, out var messageRegistrations))
                {
                    _logger.LogDebug("{@sendAsId} handling message {@message} from {@senderId}", _sendAsId, messageTypeFullName, senderId);
                    //We have registrations.
                    var typedMessage = JsonSerializer.Deserialize(message.MessageJson, messageRegistrations.MessageType);
                    foreach (var registration in messageRegistrations.Registrations.ToArray())
                    {
                        //TODO: should we start this as long running?  I guess we can't guess how this will be used, but generally our message handlers have to do a fair bit.
                        TaskHelpers.StartLongRunning(async () =>
                        {
                            try
                            {
                                await (Task)registration.Callback.DynamicInvoke(senderId, typedMessage);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "{@sendAsId}: error occurred handling {@message} from {senderId}", _sendAsId, messageTypeFullName, senderId);
                            }
                        });

                    }
                }
                else
                {
                    //TODO: log for debug.
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disposing = true;
            _stream.Dispose();
            await _channel.ShutdownAsync();
        }
    }
}
