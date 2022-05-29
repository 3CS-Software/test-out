using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Communication.Messages
{
    public class InvokeTestsMessage
    {
        public string InvocationId { get; set; }
        public InvokerInfo InvokerInfo { get; set; }
        public RemotePathInfo SourcePath { get; set; }
        public string TestAssemblyPath { get; set; }
        public int MaxRetryCount { get; set; }
        public int TestInactivityTimeoutSeconds { get; set; }
    }
}
