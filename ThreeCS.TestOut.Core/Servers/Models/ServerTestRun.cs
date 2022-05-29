using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Models
{
    /// <summary>
    /// Contains the servers view of a running test suite.
    /// </summary>
    public class ServerTestRun
    {
        public TestInvocationSpec InvocationSpec { get; set; }
        public ConcurrentDictionary<string, TestExecutionInfo> TestsByFullName { get; set; }
        public ConcurrentStack<TestExecutionInfo> TestsToProcess { get; set; }
        public ConcurrentDictionary<string, TestExecutionInfo> ProcessedTestsByFullName { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public DateTime LastInvokerHeartbeatAt { get; set; }
        public ConcurrentDictionary<string, AgentDelegatedTestRun> AgentRunsByRequestId { get; set; }
    }
}
