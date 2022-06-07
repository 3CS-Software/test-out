using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Servers.Messages
{
    public class HostFileTransferMessage
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public Guid StreamId { get; set; }
        public string SourcePath { get; set; }
        public bool UseCompression { get; set; }
    }
}
