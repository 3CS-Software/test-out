using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.PersistedState;

namespace ThreeCS.TestOut.Core.Hosting
{
    public class TestRetriever
    {
        readonly DistributedWorkspaceHandler _distributedWorkspaceHandler;
        readonly ITestEnumerator _testEnumerator;
        readonly StatsRetrieveService _statsRetrieveService;

        public TestRetriever(
            DistributedWorkspaceHandler distributedWorkspaceHandler,
            ITestEnumerator testEnumerator,
            StatsRetrieveService statsRetrieveService)
        {
            _distributedWorkspaceHandler = distributedWorkspaceHandler;
            _testEnumerator = testEnumerator;
            _statsRetrieveService = statsRetrieveService;
        }

        public async Task<List<TestExecutionInfo>> RetrieveTestsToExecute(TestInvocationSpec runSpec)
        {
            //Copy the test resources locally.
            var testWorkspace = await _distributedWorkspaceHandler.CreateLocalWorkspace(runSpec);

            //Find available tests.
            var tests = new List<TestExecutionInfo>();
            await foreach (var test in _testEnumerator.EnumerateTests(testWorkspace))
            {
                tests.Add(new TestExecutionInfo
                {
                    Spec = test
                });
            }

            //Get test stats.
            var stats = _statsRetrieveService.GetTestSummariesByFullName(tests.Select(n => n.Spec.FullTestName));

            if (stats.Count > 0)
            {
                var totalAvgDurationSeconds = stats.Values.Sum(n => n.AverageSuccessfulDuration?.TotalSeconds ?? n.AverageDuration?.TotalSeconds ?? 0d) / stats.Count;

                //Sort them by average duration.  If there's no time availble on the test, default to the average of the whole tests.
                var comparer = new Comparison<TestExecutionInfo>((te1, te2) =>
                {
                    var te1AvgDurationSeconds = totalAvgDurationSeconds;
                    var te2AvgDurationSeconds = totalAvgDurationSeconds;

                    if (stats.TryGetValue(te1.Spec.FullTestName, out var te1Stats))
                    {
                        te1AvgDurationSeconds = te1Stats.AverageSuccessfulDuration?.TotalSeconds ?? te1Stats.AverageDuration?.TotalSeconds ?? totalAvgDurationSeconds;
                    }
                    if (stats.TryGetValue(te2.Spec.FullTestName, out var te2Stats))
                    {
                        te2AvgDurationSeconds = te2Stats.AverageSuccessfulDuration?.TotalSeconds ?? te2Stats.AverageDuration?.TotalSeconds ?? totalAvgDurationSeconds;
                    }

                    return te1AvgDurationSeconds.CompareTo(te2AvgDurationSeconds);
                });

                tests.Sort(comparer);
            }

            return tests;
        }
    }
}