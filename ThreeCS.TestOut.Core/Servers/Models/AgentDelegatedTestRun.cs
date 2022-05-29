using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Servers.Models;

namespace ThreeCS.TestOut.Core.Models
{
    /// <summary>
    /// Represents the server view of a test run that is in flight on an agent.
    /// </summary>
    public class AgentDelegatedTestRun
    {
        public string AgentId { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public DateTime LastTestActivity { get; set; }
        /// <summary>
        /// The unique id for this partial test run.
        /// </summary>
        public string RequestId { get; internal set; }
        public TestPartSpec PartialTests { get; internal set; }
    }
}
