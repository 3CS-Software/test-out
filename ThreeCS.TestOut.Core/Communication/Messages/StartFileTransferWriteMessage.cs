using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication.Messages
{
    public class StartFileTransferWriteMessage
    {
        public Guid StreamId { get; set; }
        public string RequestedPath { get; set; }
    }
}
