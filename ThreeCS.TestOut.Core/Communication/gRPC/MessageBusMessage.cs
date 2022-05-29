using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication.gRPC
{
    /// <summary>
    /// A wrapper for a generic message that gets sent by the gRPC message bus.
    /// </summary>
    public class MessageBusMessage
    {
        public string SenderId { get; internal set; }
        public string RecipientId { get; internal set; }
        public string FullTypeName { get; internal set; }
        public string MessageJson { get; internal set; }
    }
}
