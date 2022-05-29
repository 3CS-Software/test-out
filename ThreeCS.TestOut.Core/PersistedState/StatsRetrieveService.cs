using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.PersistedState.Models;

namespace ThreeCS.TestOut.Core.PersistedState
{
    /// <summary>
    /// Persists test run info.
    /// </summary>
    /// <remarks>
    /// TODO: first cut, just implement this as a json save/load thing.
    /// </remarks>
    public class StatsRetrieveService
    {
        readonly StatRepo _statRepo;

        public StatsRetrieveService(StatRepo statRepo)
        {
            _statRepo = statRepo;
        }

        public Dictionary<string, TestRunStats> GetTestSummariesByFullName(IEnumerable<string> fullNames)
        {
            return _statRepo.GetTestRunStats(fullNames).ToDictionary(n => n.TestFullName);
        }
    }
}
