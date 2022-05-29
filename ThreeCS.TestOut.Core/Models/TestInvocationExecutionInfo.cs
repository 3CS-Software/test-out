using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Models
{
    /// <summary>
    /// Captures information about the execution of a test suite spec.
    /// </summary>
    public class TestInvocationExecutionInfo
    {
        public TestInvocationSpec Spec { get; set; }
        public List<TestExecutionInfo> Tests { get; set; }
    }
}
