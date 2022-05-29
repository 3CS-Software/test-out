using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Execution
{
    public class InvokerConfig
    {
        public string BasePath { get; set; }
        public string TestAssemblyPath { get; set; }
        public string ResultFilename { get; set; }
        public int MaxRetryCount { get; set; }
        public int TestInactivityTimeoutSeconds { get; set; }
    }
}
