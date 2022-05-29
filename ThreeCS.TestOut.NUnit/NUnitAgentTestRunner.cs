using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using ThreeCS.TestOut.Core.Agents;
using ThreeCS.TestOut.Core.Agents.Models;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.NUnit
{
    public class NUnitAgentTestRunner : IAgentTestRunner
    {
        readonly NUnitConsole _nunitConsole;
        readonly ILogger<NUnitAgentTestRunner> _logger;
        readonly AgentTestProgressNotifier _progressNotifier;

        public NUnitAgentTestRunner(
            NUnitConsole nunitConsole,
            ILogger<NUnitAgentTestRunner> logger,
            AgentTestProgressNotifier progressNotifier)
        {
            _nunitConsole = nunitConsole;
            _logger = logger;
            _progressNotifier = progressNotifier;
        }

        public async Task<TestRunnerResult> RunTests(RunningTestData runData)
        {
            _logger.LogDebug("NUnit Agent " + GetHashCode() + " running tests.");
            var testRunnerResult = new TestRunnerResult
            {
                TestResults = new List<TestExecutionInfo>()
            };

            var baseTempPath = Path.Combine(Path.GetTempPath(), "testout");
            Directory.CreateDirectory(baseTempPath);

            var tempTestFile = Path.Combine(baseTempPath, Guid.NewGuid() + ".testfilespec.txt");
            var tempOutputFile = Path.Combine(baseTempPath, Guid.NewGuid() + ".testresult.xml"); ;

            await PopulateTestFile(tempTestFile, runData.Tests);
            var testAssemblyPath = Path.Join(Path.GetFullPath(runData.Workspace.BasePath), runData.Workspace.RunSpec.TestAssemblyPath);

            //We're using the internal helper class here, not injecting it, as we need one per call.
            NunitTestProgressHandler progressHandler = new NunitTestProgressHandler(runData, _progressNotifier);

            //TODO: if log level is verbose, add --trace=Verbose to command args.
            (string stdOut, string stdErr) = await _nunitConsole.RunCommand(new string[] {
                testAssemblyPath,
                $"--testlist={tempTestFile}",
                "--workers=1", //We don't want to allow nunit to run in parallel, as we're handling it ourselves.
                $"--result={tempOutputFile}",
                "--labels=BeforeAndAfter"
            }, runData.CancellationToken, progressHandler.ParseOutput);

            if (!string.IsNullOrEmpty(stdErr))
            {
                _logger.LogWarning($"Console run reported errors: {stdErr}");
            }

            File.Delete(tempTestFile);
            if (!File.Exists(tempOutputFile))
            {
                throw new InvalidOperationException("Output file was not created, review logs");
            }

            //Parse the result.
            var result = new XmlDocument();
            result.Load(tempOutputFile);

            //Remove the temp output file, we don't need it once we've loaded the xml into memory.
            File.Delete(tempOutputFile);

            var testCases = result.SelectNodes("//test-case");
            var testsToRunByFullName = runData.Tests.ToDictionary(n => n.FullTestName);

            var testResultsMissing = new HashSet<TestSpec>(runData.Tests);

            foreach (XmlNode testCaseNode in testCases)
            {
                var fullTestName = testCaseNode.Attributes["fullname"].Value;
                var startDateTime = DateTime.Parse(testCaseNode.Attributes["start-time"].Value);
                var endDateTime = DateTime.Parse(testCaseNode.Attributes["end-time"].Value);
                TestExecutionOutcome? outcome;
                switch (testCaseNode.Attributes["result"].Value)
                {
                    case "Passed":
                        outcome = TestExecutionOutcome.Passed;
                        break;
                    case "Failed":
                        outcome = TestExecutionOutcome.Failed;
                        break;
                    case "Skipped":
                        outcome = TestExecutionOutcome.Skipped;
                        break;
                    default:
                        //Don't understand this yet.
                        _logger.LogWarning("Encountered unknown nunit outcome: {@outcome}", testCaseNode.Attributes["result"].Value);
                        outcome = null;
                        break;
                }

                var errorMessage = "";
                var stackTrace = "";
                var failure = testCaseNode.SelectSingleNode("failure");
                if (failure != null)
                {
                    errorMessage = failure.SelectSingleNode("message")?.InnerText;
                    stackTrace = failure.SelectSingleNode("stack-trace")?.InnerText;
                }

                if (!testsToRunByFullName.TryGetValue(fullTestName, out var testSpec))
                {
                    _logger.LogError($"Test run result was found for test that doesn't exist in requsted list: [{fullTestName}]");
                    continue;
                }

                testResultsMissing.Remove(testSpec);

                string testStdOut = testCaseNode.SelectSingleNode("output/text()")?.Value;

                //Nunit doesn't seem to propagate StdErr or Trace.
                //See https://docs.nunit.org/articles/vs-test-adapter/Trace-and-Debug.html, or https://github.com/nunit/nunit3-vs-adapter/issues/718
                string testErrOut = null;
                string testDebugOut = null;

                //TODO: any additional info, like output, errors etc.
                var testExec = new TestExecutionInfo
                {
                    Spec = testSpec,
                    StartDateTime = startDateTime,
                    EndDateTime = endDateTime,
                    Outcome = outcome,
                    ErrorMessage = errorMessage,
                    StackTrace = stackTrace,
                    SerialisedResult = testCaseNode.OuterXml,
                    StandardOutput = testStdOut,
                    ErrorOutput = testErrOut,
                    TraceOutput = testDebugOut
                    //TargetAgent =  //TODO: pass agent into this? this is currently set higher up once the result is sent back.
                };
                testRunnerResult.TestResults.Add(testExec);
            }

            //For each test that was passed into nunit, but had no result, add a failed result.  We don't want these to go unchecked.
            DateTime failedDateTime = DateTime.Now;
            foreach (var testSpec in testResultsMissing)
            {
                
                //TODO: any additional info, like output, errors etc.
                var testExec = new TestExecutionInfo
                {
                    Spec = testSpec,
                    StartDateTime = failedDateTime,
                    EndDateTime = failedDateTime,
                    Outcome = TestExecutionOutcome.Failed,
                    ErrorMessage = "Test Not Run.  Check for stack overflow or other errors.  All batch output is attached to this test result.",
                    StackTrace = null,
                    SerialisedResult = null, 
                    StandardOutput = stdOut,
                    ErrorOutput = stdErr,
                    TraceOutput = null
                    //TargetAgent =  //TODO: pass agent into this? this is currently set higher up once the result is sent back.
                };
                testRunnerResult.TestResults.Add(testExec);
            }

            return testRunnerResult;
        }

        private async Task PopulateTestFile(string tempTestFile, List<TestSpec> testsToRun)
        {
            using var fs = File.OpenWrite(tempTestFile);
            using var writer = new StreamWriter(fs);

            foreach (var line in testsToRun)
            {
                await writer.WriteLineAsync(line.FullTestName);
            }

            await fs.FlushAsync();
        }
    }
}
