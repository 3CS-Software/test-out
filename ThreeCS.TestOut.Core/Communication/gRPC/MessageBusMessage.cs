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
        public string SenderId { get; set; }
        public string RecipientId { get; set; }
        public string FullTypeName { get; set; }
        public string MessageJson { get; set; }
    }
}
