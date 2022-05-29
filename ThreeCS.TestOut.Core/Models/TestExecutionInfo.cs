using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Models
{
    public enum TestExecutionOutcome
    {
        Passed,
        Failed,
        Skipped,
    }

    public class TestExecutionInfo
    {
        public TestSpec Spec { get; set; }
        public string AgentId { get; set; }
        public TestExecutionOutcome? Outcome { get; set; }
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }

        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }

        // Detailed results from output streams.
        public string StandardOutput { get; set; }
        public string TraceOutput { get; set; }
        public string ErrorOutput { get; set; }
        

        /// <summary>
        /// A count of how many times the test was run.  Usually will be 1 for any test that succeeds first go.
        /// </summary>
        public int AttemptCount { get; set; }
        
        /// <summary>
        /// This property allows a stringified version of the test information from the underlying test hanlder to be 
        /// passed over the message bank, if desired.  this allows us to cater forward for different types of data
        /// that may not be meaningful to the process of running the test, but is useful in the output.
        /// It is up to the test runner and result serialiser to use this according to their implementation
        /// </summary>
        public string SerialisedResult { get; set; }
    }
}
