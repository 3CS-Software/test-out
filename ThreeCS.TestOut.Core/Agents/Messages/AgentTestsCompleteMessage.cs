using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Communication.Messages
{
    public class AgentTestsCompleteMessage
    {
        public string RequestId { get; set; }
        public string InvocationId { get; set; }
        public List<TestExecutionInfo> TestResults { get; set; }
        /// <summary>
        /// If this is set, all tests in this request failed.
        /// </summary>
        public string Error { get; set; }
    }
}
