using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Communication.Messages
{
    public class RunAgentTestsMessage
    {
        /// <summary>
        /// A unique id per run request.
        /// </summary>
        public string RequestId { get; set; }
        public TestInvocationSpec InvocationSpec { get; set; }
        public List<TestSpec> TestsToRun { get; set; }
    }
}
