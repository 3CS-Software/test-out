using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Models
{
    /// <summary>
    /// The spec from a remote test runner.  Contains info about the tests to run, and where the resources are.
    /// </summary>
    public class TestInvocationSpec
    {
        /// <summary>
        /// A unique once per run Id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The Id of the remote test runner who started this.
        /// </summary>
        public string InvokerId { get; set; }

        /// <summary>
        /// The test working folder, on the server.
        /// </summary>
        public RemotePathInfo SourcePath { get; set; }

        /// <summary>
        /// The relative path to the test assembly within the root of the zipped test binaries.
        /// </summary>
        public string TestAssemblyPath { get; set; }

        /// <summary>
        /// The effective maximum times each test will be re-run after failure.  Defaults to 3.
        /// </summary>
        public int MaxRetryCount { get; set; }

        /// <summary>
        /// The amount of time an agent can report no test progress before the test considered dead.
        /// </summary>
        public int TestInactivityTimeoutSeconds { get; set; }

        public bool UseFileCompression { get; set; }

        public DateTime RequestedAt { get; set; }
    }
}
