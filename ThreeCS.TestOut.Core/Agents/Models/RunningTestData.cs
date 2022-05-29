using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Agents.Models
{
    public class RunningTestData
    {
        public TestInvocationSpec Invocation { get; set; }
        public List<TestSpec> Tests { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public CancellationTokenSource CancellationSource { get; set; }
        public LocalWorkspace Workspace { get; set; }
        public string RequestId { get; set; }
    }
}
