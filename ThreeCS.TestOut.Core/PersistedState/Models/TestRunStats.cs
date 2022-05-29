using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.PersistedState.Models
{
    public class TestRunStats
    {
        public string TestFullName { get; set; }
        public List<TestRunResult> RecentResults { get; set; } = new List<TestRunResult>();

        //Some calculated stats.
        public TimeSpan? AverageDuration { get; set; }
        public TimeSpan? AverageSuccessfulDuration { get; set; }
    }

    public class TestRunResult
    {
        public string InvocationId { get; set; }
        public string AgentId { get; set; }
        public TestExecutionOutcome Outcome { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}
