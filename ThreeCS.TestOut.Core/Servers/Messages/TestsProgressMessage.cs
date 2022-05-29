using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Servers.Messages
{
    public class TestsProgressMessage
    {
        public string Message { get; set; }
        public int CompletedTestsCount { get; set; }
    }
}
