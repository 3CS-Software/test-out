using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.PersistedState;
using ThreeCS.TestOut.Core.Servers.Models;

namespace ThreeCS.TestOut.Core.Servers
{
    /// <summary>
    /// Contains state retrieval/manipulation for any running tests.
    /// </summary>
    /// <remarks>
    /// TODO: 
    /// -distribute among agents and remove fixed servers.
    /// -use record rather than class for TestExecutionInfo type.
    /// </remarks>
    public class ServerTestState
    {
        readonly ServerConfig _serverConfig;
        readonly StatsUpdateService _statsUpdateService;

        private ConcurrentDictionary<string, ServerTestRun> _testRunByInvocationId = new();

        public ServerTestState(
            ServerConfig serverConfig,
            StatsUpdateService statsUpdateService)
        {
            _serverConfig = serverConfig;
            _statsUpdateService = statsUpdateService;
        }

        public void Init()
        {
        }

        public virtual void AddTestRun(TestInvocationSpec testRunSpec, List<TestExecutionInfo> testsToRun)
        {
            var dupTestGrouped = testsToRun
                .GroupBy(n => n.Spec.FullTestName)
                .Where(g => g.Count() > 1);

            var allDups = dupTestGrouped.SelectMany(n => n).ToList();

            var firstDups = dupTestGrouped.Select(n => n.First());

            foreach (var test in allDups)
            {
                test.Outcome = TestExecutionOutcome.Failed;
                test.ErrorMessage = $"Test full name of {test.Spec.FullTestName} was detected more than once during test enumeration.  All tests with this name have been failed.";
            }

            var effectiveTestsToRun = new ConcurrentStack<TestExecutionInfo>(testsToRun.Except(allDups));

            var testsByFullName = new ConcurrentDictionary<string, TestExecutionInfo>(
                effectiveTestsToRun.Union(firstDups)
                .ToDictionary(t => t.Spec.FullTestName, v => v));

            //Ensure any dups are already marked processed, and are included in the results.
            var processedTestsByFullName = new ConcurrentDictionary<string, TestExecutionInfo>(
                firstDups.ToDictionary(t => t.Spec.FullTestName, v => v));

            var serverTestRun = new ServerTestRun
            {
                InvocationSpec = testRunSpec,
                RequestedAt = DateTime.Now,
                TestsToProcess = effectiveTestsToRun,
                TestsByFullName = testsByFullName,
                AgentRunsByRequestId = new(),
                ProcessedTestsByFullName = processedTestsByFullName,
                LastInvokerHeartbeatAt = DateTime.Now,
            };
            _testRunByInvocationId.TryAdd(testRunSpec.Id, serverTestRun);
        }

        public virtual TestInvocationSpec GetInvocationSpec(string invocationId)
        {
            return _testRunByInvocationId.GetValueOrDefault(invocationId)?.InvocationSpec;
        }

        /// <summary>
        /// Returns the test execution info for the given test, or null if not found.
        /// </summary>
        public virtual TestExecutionInfo GetTestResult(string invocationId, string fullTestName)
        {
            var testRun = _testRunByInvocationId.GetValueOrDefault(invocationId);
            return testRun?.TestsByFullName.GetValueOrDefault(fullTestName);
        }

        /// <summary>
        /// Checks if the given invocation is pending complete, and if so, completes it and returns true.
        /// </summary>
        public virtual bool HasCompleted(string invocationId)
        {
            var testRun = _testRunByInvocationId.GetValueOrDefault(invocationId);
            if (testRun != null && testRun.FinishedAt == null && testRun.TestsByFullName.Count == testRun.ProcessedTestsByFullName.Count)
            {
                //TODO: should be able to do this with a different thread safety structure than this coarse lock.
                lock (testRun)
                {
                    if (testRun.FinishedAt == null)
                    {
                        testRun.FinishedAt = DateTime.Now;
                        _statsUpdateService.RecordInvocationFinish(testRun.InvocationSpec.Id);
                        return true;
                    }
                }
            }
            return false;
        }

        internal int GetCompletedTestCount(string invocationId)
        {
            var testRun = _testRunByInvocationId.GetValueOrDefault(invocationId);
            if (testRun == null)
                return 0;

            return testRun.ProcessedTestsByFullName.Count;
        }

        public virtual TestPartSpec GetNextTests()
        {
            int batchSize = _serverConfig.BatchSize;

            var testRun = _testRunByInvocationId.Values.FirstOrDefault();
            if (testRun != null)
            {
                //Mark the run as started, if it's not.
                if (testRun.StartedAt == null)
                {
                    testRun.StartedAt = DateTime.Now;
                    _statsUpdateService.RecordInvocationStart(testRun.InvocationSpec.Id, testRun.InvocationSpec.InvokerId, testRun.InvocationSpec.TestAssemblyPath);
                }

                //Calculate the batch size, based on how many tests are left.
                var testsLeft = testRun.TestsToProcess.Count;
                //This is a very rough thing.  Basically we're assuming about 32 agents, really we should be looking at the test run stats.
                var totalAgents = 32;
                if ((testsLeft / batchSize) < totalAgents)
                {
                    var newBatchSize = Math.Max(1, testsLeft / totalAgents);
                    if (newBatchSize != batchSize)
                    {
                        batchSize = newBatchSize;
                    }
                }

                TestExecutionInfo[] poppedTests = new TestExecutionInfo[batchSize];
                int numPopped = testRun.TestsToProcess.TryPopRange(poppedTests);
                if (numPopped > 0)
                {
                    return new TestPartSpec
                    {
                        InvocationSpec = testRun.InvocationSpec,
                        TestsToRun = poppedTests
                            .Take(numPopped)
                            .Select(n => n.Spec)
                            .ToList()
                    };
                }
            }
            return null;
        }

        internal void RemoveTestRun(string invocationId)
        {
            _testRunByInvocationId.TryRemove(invocationId, out _);
        }

        public virtual List<AgentDelegatedTestRun> GetActiveAgentDelegatedRuns()
        {
            return _testRunByInvocationId.Values
                .SelectMany(n => n.AgentRunsByRequestId.Values)
                .ToList();
        }

        public virtual AgentDelegatedTestRun GetAgentDelegatedRun(string requestId)
        {
            //TODO: index agent delegated runs by agent as well.
            return _testRunByInvocationId.Values
                .Select(n => n.AgentRunsByRequestId.GetValueOrDefault(requestId))
                .Where(n => n != null)
                .SingleOrDefault();
        }

        public virtual List<AgentDelegatedTestRun> GetActiveAgentDelegatedRunsForAgent(string agentId)
        {
            //TODO: index agent delegated runs by agent as well.
            return _testRunByInvocationId.Values
                .SelectMany(n => n.AgentRunsByRequestId.Values.Where(n => n.AgentId == agentId))
                .ToList();
        }

        public virtual List<ServerTestRun> GetActiveServerRuns()
        {
            return _testRunByInvocationId.Values.ToList();
        }

        public virtual ServerTestRun GetServerRun(string invocationId)
        {
            _testRunByInvocationId.TryGetValue(invocationId, out var serverRun);
            return serverRun;
        }

        /// <summary>
        /// Adds a record of an agent delegated run, which will be used in heartbeat functionality.
        /// </summary>
        public AgentDelegatedTestRun AddAgentDelegatedRun(string senderId, TestPartSpec nextTests)
        {
            var testRun = _testRunByInvocationId[nextTests.InvocationSpec.Id];
            
            var agentRun = new AgentDelegatedTestRun
            {
                AgentId = senderId,
                LastHeartbeat = DateTime.Now,
                LastTestActivity = DateTime.Now,
                RequestId = Guid.NewGuid().ToString(),
                PartialTests = nextTests
            };

            testRun.AgentRunsByRequestId.TryAdd(agentRun.RequestId, agentRun);
            return agentRun;
        }

        public virtual AgentDelegatedTestRun RemoveAgentDelegatedRun(string invocationId, string requestId)
        {
            AgentDelegatedTestRun agentDelegatedTestRun = null;
            if (_testRunByInvocationId.TryGetValue(invocationId, out var serverRun))
            {
                serverRun.AgentRunsByRequestId.TryRemove(requestId, out agentDelegatedTestRun);
            }
            return agentDelegatedTestRun;
        }

        /// <summary>
        /// Gets the completed test run.  If not available, returns null.
        /// </summary>
        /// <param name="invocationId"></param>
        /// <returns></returns>
        public virtual ServerTestRun GetCompletedTestRun(string invocationId)
        {
            return _testRunByInvocationId.GetValueOrDefault(invocationId);
        }

        /// <summary>
        /// Sets the given test result.
        /// </summary>
        public virtual void SetTestResult(string invocationId, TestExecutionInfo newResult)
        {
            var testRun = _testRunByInvocationId.GetValueOrDefault(invocationId);
            var testFullName = newResult.Spec.FullTestName;
            testRun.TestsByFullName.AddOrUpdate(testFullName, (key) => newResult, (key, existingValue) => newResult);

            if (newResult.Outcome != null)
            {
                //This test was completed, for better or worse.
                testRun.ProcessedTestsByFullName.TryAdd(testFullName, newResult);

                //Record stats, if it has a duration.
                var duration = newResult.EndDateTime - newResult.StartDateTime;
                _statsUpdateService.RecordTestResult(invocationId, newResult.AgentId, testFullName, newResult.Outcome.Value, duration);
            }
            else
            {
                //Push this back onto the processing queue, it hasn't been completed yet.
                testRun.TestsToProcess.Push(newResult);
            }
        }
    }
}
