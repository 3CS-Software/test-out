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
    public class ReconnectTests
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
        public async Task AgentReconnects()
        {
            var mocker = SetupMocking();

            //Simplehandler.
            AsyncManualResetEvent mre = new AsyncManualResetEvent(false);
            SimpleMessage? receivedMessage = null;
            async Task HandleSimpleMessage(string senderId, SimpleMessage message)
            {
                receivedMessage = message;
                mre.Set();
            }

            //Client 1 will connect before the server is running.  It should just stay disconnected.
            var client1 = GetClient(mocker);
            client1.OnMessageReceived<SimpleMessage>(HandleSimpleMessage);
            await client1.Register("Client1");

            Assert.IsFalse(client1.IsConnected);

            //Start the server.
            var server = GetServer(mocker);
            await server.Init();

            //Client 1 should be reconnected after a bit.
            while (!client1.IsConnected)
            {
                await Task.Delay(1000);
            }

            //Start client 2.
            var client2 = GetClient(mocker);
            client2.OnMessageReceived<SimpleMessage>(HandleSimpleMessage);
            await client2.Register("Client2");

            Assert.IsTrue(client2.IsConnected);

            //All setup.  Start doing some communication.
            await client1.SendMessage("Client2", new SimpleMessage { Message = "For 2 From 1" });
            await mre.WaitAsync();

            Assert.IsNotNull(receivedMessage);
            Assert.AreEqual("For 2 From 1", receivedMessage.Message);

            receivedMessage = null;

            //Ok, so basic comms has worked.  Now kill the server, so both clients have their connections severed..
            await server.DisposeAsync();

            //TODO: check for logged errors.

            //Now send the message again.  This time, it will go nowhere.
            await client1.SendMessage("Client2", new SimpleMessage { Message = "This will go nowhere" });

            //TODO: check logs for discarded message.
            Assert.IsNull(receivedMessage);

            //Restart the server.
            server = GetServer(mocker);
            await server.Init();


            //Server is back up, wait for the clients to reconnect.
            while (!client1.IsConnected || !client2.IsConnected)
            {
                await Task.Delay(1000);
            }

            //Clients are connected, send the message.
            receivedMessage = null;
            mre.Reset();
            await client1.SendMessage("Client2", new SimpleMessage { Message = "For 2 From 1, once again" });
            await mre.WaitAsync();

            //Check it got through.
            Assert.IsNotNull(receivedMessage);
            Assert.AreEqual("For 2 From 1, once again", receivedMessage.Message);


            //Now, let's do the opposite, and kill/recreate client 2.
            await client2.DisposeAsync();

            await Task.Delay(10);

            //Send a message from client 1 to 2, it should go nowhere as client 2 is disconnected.
            receivedMessage = null;
            await client1.SendMessage("Client 2", new SimpleMessage { Message = "This should go nowhere still" });
            Task.Delay(10);
            Assert.IsNull(receivedMessage);

            client2 = GetClient(mocker);
            client2.OnMessageReceived<SimpleMessage>(HandleSimpleMessage);
            await client2.Register("Client2");

            //Clients are connected, send the message a final time..
            mre.Reset();
            await client1.SendMessage("Client2", new SimpleMessage { Message = "For 2 From 1, after client recreated." });
            await mre.WaitAsync();

            Assert.IsNotNull(receivedMessage);
            Assert.AreEqual("For 2 From 1, after client recreated.", receivedMessage.Message);

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
