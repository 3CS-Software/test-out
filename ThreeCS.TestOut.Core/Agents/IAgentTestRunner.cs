using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents.Models;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Agents
{
    public interface IAgentTestRunner
    {
        Task<TestRunnerResult> RunTests(RunningTestData runData);
    }
}
