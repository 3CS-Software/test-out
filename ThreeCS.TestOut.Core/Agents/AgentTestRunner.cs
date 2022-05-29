using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents.Models;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Agents
{
    public class AgentTestRunner
    {
        readonly DistributedWorkspaceHandler _workspaceHandler;
        readonly IAgentTestRunner _runner;

        public AgentTestRunner(DistributedWorkspaceHandler workspaceHandler, IAgentTestRunner runner)
        {
            _workspaceHandler = workspaceHandler;
            _runner = runner;
        }

        public async Task<TestRunnerResult> Run(RunningTestData runData)
        {
            //Get the local workspace.
            runData.Workspace = await _workspaceHandler.CreateLocalWorkspace(runData.Invocation);

            //Invoke the tests in it.
            var result = await _runner.RunTests(runData);// localWorkspace, runData.RequestMessage.InvocationSpec, runData.RequestMessage.TestsToRun, runData.CancellationToken);

            //Return the updated test results
            return result;
        }
    }
}