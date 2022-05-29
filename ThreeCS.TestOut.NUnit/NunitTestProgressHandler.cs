using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents;
using ThreeCS.TestOut.Core.Agents.Models;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.NUnit
{
    internal class NunitTestProgressHandler
    {
        readonly RunningTestData _runData;
        readonly AgentTestProgressNotifier _progressNotifier;
        readonly Dictionary<string, TestSpec> _testByName;
        readonly ConcurrentDictionary<string, bool> _testHasStartedByName;

        private TestSpec _curTest;

        public NunitTestProgressHandler(RunningTestData runData, AgentTestProgressNotifier progressNotifier)
        {
            _runData = runData;
            _progressNotifier = progressNotifier;
            _testByName = runData.Tests.ToDictionary(n => n.FullTestName);
            _testHasStartedByName = new ConcurrentDictionary<string, bool>(runData.Tests.Select(n => KeyValuePair.Create(n.FullTestName, false)));
            _curTest = null;
        }

        public async Task ParseOutput(string outputLine)
        {
            bool wasInfoLine = false;
            if (outputLine.StartsWith("=> "))
            {
                if (UpdateCurTest(outputLine.Substring("=> ".Length)))
                {
                    wasInfoLine = true;
                    if (!_testHasStartedByName[_curTest.FullTestName])
                    {
                        _testHasStartedByName[_curTest.FullTestName] = true;
                        //Send a 'started' message'
                        await _progressNotifier.NotifyStatusUpdate(_runData, _curTest, Core.Agents.Messages.StatusUpdateType.Started);
                    }
                }
            }
            else if (outputLine.StartsWith("Failed => "))
            {
                if (UpdateCurTest(outputLine.Substring("Failed => ".Length)))
                {
                    wasInfoLine = true;
                    //TODO: send test stdout along.
                    await _progressNotifier.NotifyStatusUpdate(_runData, _curTest, Core.Agents.Messages.StatusUpdateType.Failed);
                }
            }
            else if (outputLine.StartsWith("Passed => "))
            {
                if (UpdateCurTest(outputLine.Substring("Passed => ".Length)))
                {
                    wasInfoLine = true;
                    //TODO: send test stdout along.
                    await _progressNotifier.NotifyStatusUpdate(_runData, _curTest, Core.Agents.Messages.StatusUpdateType.Finished);
                }
            }

            if (!wasInfoLine)
            {
                //Just add this std out lin to test output.
                //TODO: do we send this as a progress message?.  Might get a bit noisy.
            }
        }

        private bool UpdateCurTest(string potentialTestName)
        {
            if (_testByName.TryGetValue(potentialTestName, out var newTest))
            {
                _curTest = newTest;
                return true;
            }
            return false;
        }
    }
}
