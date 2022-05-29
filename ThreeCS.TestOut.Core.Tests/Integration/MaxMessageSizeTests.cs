using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Nito.AsyncEx;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.gPRC;
using ThreeCS.TestOut.Core.Hosting;

namespace ThreeCS.TestOut.Core.Tests.Integration
{
    public class MaxMessageSizeTests
    {
        public class SimpleMessage
        {
            public string Message { get; set; }
        }

        private AutoMocker SetupMocking()
        {
            var mocker = new AutoMocker();
            mocker.Use(new ServerConnectionConfig
            {
                ServerUrl = "http://localhost:33445/",
                ServerId = "server",
            });

            SetupLoggerFor<RpcMessageBusClient>(mocker);
            SetupLoggerFor<RpcMessageBusServer>(mocker);

            return mocker;
        }

        private IMessageBusClient GetClient(AutoMocker mocker)
            => new RpcMessageBusClient(mocker.Get<ILogger<RpcMessageBusClient>>(),
                mocker.Get<ServerConnectionConfig>());

        private IMessageBusServer GetServer(AutoMocker mocker)
            => mocker.CreateInstance<RpcMessageBusServer>();


        [Test]
        public async Task MaxMessageSize()
        {
            var mocker = SetupMocking();

            //Simplehandler.
            AsyncManualResetEvent mre = new AsyncManualResetEvent(false);
            SimpleMessage? receivedMessage = null;
            Task HandleSimpleMessage(string senderId, SimpleMessage message)
            {
                receivedMessage = message;
                mre.Set();
                return Task.CompletedTask;
            }

            var server = GetServer(mocker);
            await server.Init();

            var client1 = GetClient(mocker);
            client1.OnMessageReceived<SimpleMessage>(HandleSimpleMessage);
            await client1.Register("client1");

            var client2 = GetClient(mocker);
            client2.OnMessageReceived<SimpleMessage>(HandleSimpleMessage);
            await client2.Register("client2");


            //40 megs of 'A'.  Sounds good, Ay?
            int charCount = 1024 * 1024 * 40;
            string longString = new string('A', charCount);
            await client1.SendMessage("client2", new SimpleMessage { Message = longString });

            await mre.WaitAsync();
            Assert.IsNotNull(receivedMessage);
            Assert.AreEqual(charCount, receivedMessage!.Message.Length);

        }

        private void SetupLoggerFor<TLogger>(AutoMocker mocker)
        {
            mocker.GetMock<ILogger<TLogger>>()
                .Setup(n => n.Log<It.IsAnyType>(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction((ia) =>
                {
                    var logLevel = ia.Arguments[0];
                    var formatter = (Delegate)ia.Arguments[4];
                    var messageContents = ia.Arguments[2];
                    var formattedMessage = formatter.DynamicInvoke(ia.Arguments[2], ia.Arguments[3]);
                    Debug.WriteLine($"Logged at {logLevel}: {formattedMessage}");
                }));

        }
    }
}
