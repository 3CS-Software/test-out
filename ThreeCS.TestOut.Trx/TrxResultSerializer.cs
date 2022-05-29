using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.Extensions.TrxLogger;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using ThreeCS.TestOut.Core.Invokers;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Trx
{
    /// <summary>
    /// Serializes test results from the visual studio test result format.
    /// See
    /// https://github.com/microsoft/vstest/blob/6b8b30f0fc6d597f1c89d0cc0c1113b888c9fe14/src/Microsoft.TestPlatform.Extensions.TrxLogger/TrxLogger.cs
    /// </summary>
    public class TrxResultSerializer : IResultSerializer
    {
        readonly ILogger<TrxResultSerializer> _logger;

        public TrxResultSerializer(ILogger<TrxResultSerializer> logger)
        {
            _logger = logger;
        }

        public async Task SerializeAsync(string resultPath, TestInvocationExecutionInfo results)
        {
            _logger.LogDebug("Serializing result for {@testCount} tests", results.Tests.Count);

            TrxLogger logger = new TrxLogger();
            MockResultLoggerEventSupplier tle = new MockResultLoggerEventSupplier();
            var fullResultPath = Path.GetFullPath(resultPath);
            var loggerParameters = new Dictionary<string, string>
            {
                ["TestRunDirectory"] = Path.GetDirectoryName(fullResultPath),
                ["LogFileName"] = Path.GetFileName(fullResultPath)
            };
            logger.Initialize(tle, loggerParameters);

            var statsDict = new Dictionary<TestOutcome, long>();
            foreach (TestOutcome trxOutcomeEnumVal in Enum.GetValues<TestOutcome>())
            {
                statsDict[trxOutcomeEnumVal] = 0;
            }

            TimeSpan totalTestTime = TimeSpan.Zero;

            List<TestCase> trxTests = new List<TestCase>();
            List<TestResult> trxResults = new List<TestResult>();

            DateTime minDate = results.Tests.Min(n => n.StartDateTime ?? DateTime.Now);
            DateTime maxDate = results.Tests.Max(n => n.EndDateTime ?? DateTime.Now);

            foreach (var test in results.Tests)
            {
                var testId = Guid.NewGuid();
                var testExecId = Guid.NewGuid().ToString();

                var tc = new TestCase
                {
                    Id = testId,
                    DisplayName = test.Spec.TestName,
                    FullyQualifiedName = test.Spec.FullTestName,
                    //TODO: get this from the actual executor..
                    ExecutorUri = new Uri(@"executor://testOutTestExecutor"), //nunit3testexecutor
                    Source = Path.GetFileName(results.Spec.TestAssemblyPath),
                };

                trxTests.Add(tc);

                TestOutcome trxOutcome = TestOutcome.None;
                switch (test.Outcome)
                {
                    case TestExecutionOutcome.Passed:
                        trxOutcome = TestOutcome.Passed;
                        break;
                    case TestExecutionOutcome.Failed:
                        trxOutcome = TestOutcome.Failed;
                        break;
                    case TestExecutionOutcome.Skipped:
                        trxOutcome = TestOutcome.Skipped;
                        break;
                    default:
                        break;
                }

                var tr = new TestResult(tc)
                {
                    StartTime = test.StartDateTime ?? minDate,
                    EndTime = test.EndDateTime ?? test.StartDateTime ?? minDate,
                    Duration = (test.EndDateTime - test.StartDateTime) ?? TimeSpan.Zero,
                    ComputerName = test.AgentId, //TODO: fix issue where this isn't actually used. https://github.com/microsoft/vstest/blob/6b8b30f0fc6d597f1c89d0cc0c1113b888c9fe14/src/Microsoft.TestPlatform.Extensions.TrxLogger/TrxLogger.cs#L267
                    DisplayName = test.Spec.TestName,
                    Outcome = trxOutcome,
                    ErrorMessage = test.ErrorMessage,
                    ErrorStackTrace = test.StackTrace,
                };

                totalTestTime += tr.Duration;

                if (!string.IsNullOrEmpty(test.StandardOutput))
                {
                    tr.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, test.StandardOutput));
                }
                if (!string.IsNullOrEmpty(test.TraceOutput))
                {
                    tr.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, test.TraceOutput));
                }
                if (!string.IsNullOrEmpty(test.ErrorOutput))
                {
                    tr.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, test.ErrorOutput));
                }

                trxResults.Add(tr);

                statsDict[trxOutcome] = statsDict[trxOutcome] + 1;
            }

            tle.OnTestRunStart(new TestRunStartEventArgs(new TestRunCriteria(trxTests, new BaseTestRunCriteria(1))));

            foreach (var tr in trxResults)
            {
                tle.OnTestResult(new TestResultEventArgs(tr));
            }

            var testRunStats = new TestRunStatistics(statsDict);
            tle.OnTestRunComplete(new TestRunCompleteEventArgs(
                stats: testRunStats,
                isCanceled: false,
                isAborted: false,
                error: null,
                attachmentSets: null,
                elapsedTime: totalTestTime
            ));

            _logger.LogDebug("Wrote results to @{resultPath}", fullResultPath);
        }

        private string Serialize<TEntity>(TEntity entityToBeSerialized)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UnicodeEncoding(false, false),
                Indent = false,
                OmitXmlDeclaration = false,
            };
            var xmlSerializer = new XmlSerializer(typeof(TEntity));
            string result;

            using (var stringWriter = new StringWriter())
            {
                using var writer = XmlWriter.Create(stringWriter, settings);
                xmlSerializer.Serialize(writer, entityToBeSerialized);
                result = stringWriter.ToString();
            }

            result = result.Replace("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", string.Empty);
            result = result.Replace("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", string.Empty);

            return result;
        }
    }
}
