using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.PersistedState.Models;

namespace ThreeCS.TestOut.Core.PersistedState
{
    /// <summary>
    /// Persists test run info.
    /// </summary>
    /// <remarks>
    /// TODO: first cut, just implement this as a json save/load thing.
    /// </remarks>
    public class StatsUpdateService
    {
        StatRepo _statRepo;

        public StatsUpdateService(StatRepo statRepo)
        {
            _statRepo = statRepo;
        }

        public void RecordInvocationStart(string invocationId, string invokerId, string testAssemblyPath)
        {
            var invocation = _statRepo.GetInvocation(invocationId);
            if (invocation == null)
            {
                invocation = new TestInvocationStats
                {
                    StartedAt = DateTime.Now,
                    InvocationId = invocationId,
                    InvokerId = invokerId,
                    TestAssemblyPath = testAssemblyPath
                };
            }
            _statRepo.SaveInvocation(invocation);
        }

        public void RecordInvocationFinish(string invocationId)
        {
            var invocation = _statRepo.GetInvocation(invocationId);
            if (invocation != null)
            {
                invocation.FinishedAt = DateTime.Now;

                //Update the invocation stats from the test stats linked to this invocation.
                var invocationTests = _statRepo.GetInvocationTestResults(invocationId);

                invocation.TotalTests = invocationTests.Count;
                invocation.WorkerCount = invocationTests.Select(n => n.AgentId).Distinct().Count();

                _statRepo.SaveInvocation(invocation);
            }
        }

        public void RecordTestResult(string invocationId, string agentId, string fullName, TestExecutionOutcome outcome, TimeSpan? duration)
        {
            var testStats = _statRepo.GetTestRunStats(fullName);
            if (testStats == null)
            {
                testStats = new TestRunStats
                {
                    TestFullName = fullName,
                };
            }

            testStats.RecentResults.Add(new TestRunResult
            {
                AgentId = agentId,
                InvocationId = invocationId,
                Duration = duration,
                Outcome = outcome
            });

            //TODO: trim recent results to some reasonable number, refac this a bit.
            if (testStats.RecentResults.Any(n => n.Duration != null))
            {
                testStats.AverageDuration = TimeSpan.FromSeconds(testStats.RecentResults
                    .Where(n => n.Duration != null)
                    .Average(n => n.Duration.Value.TotalSeconds));

                if (testStats.RecentResults.Any(n => n.Duration != null && n.Outcome == TestExecutionOutcome.Passed))
                {
                    testStats.AverageSuccessfulDuration = TimeSpan.FromSeconds(testStats.RecentResults
                        .Where(n => n.Duration != null && n.Outcome == TestExecutionOutcome.Passed)
                        .Average(n => n.Duration.Value.TotalSeconds));
                }
            }

            _statRepo.SaveTest(testStats);
        }
    }
}
