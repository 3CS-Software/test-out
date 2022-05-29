using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Servers.Messages
{
    /// <summary>
    /// Instructs an agent to cancel an ongoing test run.
    /// </summary>
    public class CancelAgentTestsMessage
    {
        /// <summary>
        /// The running request to cancel.
        /// </summary>
        public string RequestId { get; set; }
    }
}
