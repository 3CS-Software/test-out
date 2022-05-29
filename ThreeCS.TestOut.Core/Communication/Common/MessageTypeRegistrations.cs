using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication.Common
{
    internal class MessageTypeRegistrations
    {
        public Type MessageType { get; set; }
        public ConcurrentBag<MessageRegistration> Registrations;
    }
}
