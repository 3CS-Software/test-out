using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.PersistedState.Models
{
    public class TestInvocationStats
    {
        public string InvocationId { get; set; }
        public string InvokerId { get; set; }
        public string TestAssemblyPath { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int TotalTests { get; internal set; }
        public int WorkerCount { get; internal set; }
    }
}
