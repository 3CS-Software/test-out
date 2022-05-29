using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.PersistedState.Models;

namespace ThreeCS.TestOut.Core.PersistedState
{
    public class StatRepoJsonModel
    {
        public List<TestInvocationStats> InvocationStats { get; set; } = new List<TestInvocationStats>();
        public List<TestRunStats> TestStats { get; set; } = new List<TestRunStats>();
    }

    /// <summary>
    /// TODO: actually write this out or write this out to a simple db in real time.  Right now it's just in mem.
    /// </summary>
    public class StatRepo
    {
        readonly StorageConfig _storageConfig;
        readonly ILogger<StatRepo> _logger;

        public StatRepo(
            StorageConfig storageConfig,
            ILogger<StatRepo> logger)
        {
            _storageConfig = storageConfig;
            _logger = logger;
        }

        private ConcurrentDictionary<string, TestInvocationStats> _invocationsById;
        private ConcurrentDictionary<string, TestRunStats> _testsByFullName;

        private static object _lock = new object();

        internal TestInvocationStats GetInvocation(string invocationId)
        {
            InitRepo();
            _invocationsById.TryGetValue(invocationId, out var result);
            return result;
        }

        internal List<TestRunStats> GetTestRunStats(IEnumerable<string> fullNames)
        {
            InitRepo();
            HashSet<string> fullNamesHash = new HashSet<string>(fullNames);
            return _testsByFullName.Values
                .Where(n => fullNamesHash.Contains(n.TestFullName))
                .ToList();
        }

        internal List<TestRunResult> GetInvocationTestResults(string invocationId)
        {
            InitRepo();
            return _testsByFullName
                    .Values
                    .SelectMany(n => n.RecentResults)
                    .Where(n => n.InvocationId == invocationId)
                    .ToList();
        }

        internal void SaveInvocation(TestInvocationStats invocation)
        {
            InitRepo();
            _invocationsById.AddOrUpdate(invocation.InvocationId, invocation, (k, v) => invocation);
            if (invocation.FinishedAt != null)
            {
                SaveRepo();
            }
        }

        internal TestRunStats GetTestRunStats(string fullName)
        {
            InitRepo();
            _testsByFullName.TryGetValue(fullName, out var retVal);
            return retVal;
        }

        internal void SaveTest(TestRunStats testStats)
        {
            _testsByFullName.AddOrUpdate(testStats.TestFullName, testStats, (k, v) => testStats);
        }

        private void InitRepo()
        {
            if (_invocationsById == null)
            {
                lock (_lock)
                {
                    try
                    {
                        string jsonPath = GetStateFilePath();

                        if (File.Exists(jsonPath))
                        {
                            StatRepoJsonModel jsonData;
                            using (var fs = File.OpenRead(jsonPath))
                            {
                                jsonData = JsonSerializer.Deserialize<StatRepoJsonModel>(fs);
                            }
                            _invocationsById = new ConcurrentDictionary<string, TestInvocationStats>(jsonData.InvocationStats.ToDictionary(n => n.InvocationId));
                            _testsByFullName = new ConcurrentDictionary<string, TestRunStats>(jsonData.TestStats.ToDictionary(n => n.TestFullName));

                            _logger.LogDebug("Read {@testCount} tests and {@invocationCount} invocations from stat file {@jsonPath}.", jsonData.TestStats.Count, jsonData.InvocationStats.Count, jsonPath);
                        }
                        else
                        {
                            _logger.LogDebug("stat file {@jsonPath} not found, skipping load.", jsonPath);
                            _invocationsById = new();
                            _testsByFullName = new();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while reading stats json file.  Empty stats will be used, and updated stats will be written out after this test run.");
                        _invocationsById = new();
                        _testsByFullName = new();
                    }
                }
            }
        }

        private string GetStateFilePath()
        {
            Directory.CreateDirectory(_storageConfig.StateFolder);
            return Path.Combine(_storageConfig.StateFolder, "stats.json");
        }

        private void SaveRepo()
        {
            lock (_lock)
            {
                string jsonPath = GetStateFilePath();
                var jsonData = new StatRepoJsonModel
                {
                    TestStats = _testsByFullName.Values.OrderBy(n => n.TestFullName).ToList(),
                    InvocationStats = _invocationsById.Values.OrderBy(n => n.InvocationId).ToList()
                };
                using (var fs = File.Open(jsonPath, FileMode.Create)) 
                {
                    JsonSerializer.Serialize(fs, jsonData, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    });
                }
                _logger.LogDebug("Wrote {@testCount} tests and {@invocationCount} invocations to stat file {@filePath}.", jsonData.TestStats.Count, jsonData.InvocationStats.Count, jsonPath);
            }
        }
    }
}
