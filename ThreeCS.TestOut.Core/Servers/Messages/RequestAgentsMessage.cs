using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Servers.Messages
{
    public class RequestAgentsMessage
    {
        public TestInvocationSpec InvocationSpec { get; set; }
    }
}
