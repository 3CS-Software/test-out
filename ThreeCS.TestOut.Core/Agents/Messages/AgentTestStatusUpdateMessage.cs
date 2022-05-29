using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Agents.Messages
{
    public enum StatusUpdateType
    {
        /// <summary>
        /// The test has started.
        /// </summary>
        Started,
        
        /// <summary>
        /// The test has progressed.  This status update will generally have a message.
        /// </summary>
        Progress,
        
        /// <summary>
        /// The test has finished successfully.
        /// </summary>
        Finished,

        /// <summary>
        /// The test has failed.
        /// </summary>
        Failed,
    }

    /// <summary>
    /// An informational message sent from the agent running the test to the server.
    /// </summary>
    public class AgentTestStatusUpdateMessage
    {
        public string InvocationId { get; set; }
        public string AgentRunRequestId { get; set; }
        public TestSpec Test { get; set; }
        public StatusUpdateType UpdateType { get; set; }
        /// <summary>
        /// A string with human readable additional info.
        /// </summary>
        public string MessageText { get; set; }
    }
}
