using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Agents.Models
{
    public class TestRunnerResult
    {
        //TODO: info about tests that couldn't run, or failures etc.
        public List<TestExecutionInfo> TestResults { get; set; }
    }
}
