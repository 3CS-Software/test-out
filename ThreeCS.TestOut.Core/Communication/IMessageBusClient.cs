using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication
{
    public delegate Task MessageReceivedDelegate<TMessage>(string senderId, TMessage message);

    /// <summary>
    /// Simple abstraction to keep the communication channels from getting tangled in the implementation.  Implemented in SignalR, using whatever server config is available.
    /// </summary>
    public interface IMessageBusClient : IAsyncDisposable
    {
        /// <summary>
        /// Registers the recipient, and broadcasts a registration message.
        /// </summary>
        Task Register(string recipientId);

        /// <summary>
        /// Sends a message to everyone.
        /// </summary>
        Task BroadcastMessage<TMessage>(TMessage message);

        /// <summary>
        /// Sends a message to the given recipient.
        /// </summary>
        Task SendMessage<TMessage>(string recipientId, TMessage message);

        /// <summary>
        /// Handles recieving messages.  The disposeable can be used to detach the handler.
        /// </summary>
        IDisposable OnMessageReceived<TMessage>(MessageReceivedDelegate<TMessage> handler);

        /// <summary>
        /// Indicates if the message bus client is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Fired when the message bus is re-connected.
        /// </summary>
        event Func<Task> Reconnected;

        /// <summary>
        /// Fired when the message bus becomes unavilable.  The underlying connection will attempt to re-connect, and will re-register once connected.  Check the IsConnected property for the state.
        /// </summary>
        event Func<Task> Disconnected;
    }
}
