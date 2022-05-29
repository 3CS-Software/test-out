using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.NUnit
{
    public class NUnitTestEnumerator : ITestEnumerator
    {
        readonly NUnitConsole _nunitConsole;
        readonly ILogger<NUnitTestEnumerator> _logger;

        public NUnitTestEnumerator(
            NUnitConsole nunitConsole,
            ILogger<NUnitTestEnumerator> logger)
        {
            _nunitConsole = nunitConsole;
            _logger = logger;
        }

        public async IAsyncEnumerable<TestSpec> EnumerateTests(LocalWorkspace localWorkspace)
        {            
            var testLibraryPath = Path.GetFullPath(Path.Combine(localWorkspace.BasePath, localWorkspace.RunSpec.TestAssemblyPath));

            var tempBasePath = Path.Combine(Path.GetTempPath(), "testout");
            Directory.CreateDirectory(tempBasePath);

            var testListPath =  Path.Combine(tempBasePath, Guid.NewGuid() + ".exploreresult.xml");

            var runResults = await _nunitConsole.RunCommand(new[] { testLibraryPath, $"--explore={testListPath}" }, CancellationToken.None);
            var testListXml = new XmlDocument();
            testListXml.Load(testListPath);
            File.Delete(testListPath);

            int testCount = 0;
            var testCases = testListXml.SelectNodes("//test-case");
            foreach (XmlNode test in testCases)
            {
                
                // Dont include anything that has test state of explicit/ignored.
                // TODO: Could this be done using a param to nunit console --explore?
                var runstate = test.Attributes["runstate"];
                if (runstate?.Value == "Explicit")
                    continue;

                testCount++;
                yield return new TestSpec
                {
                    TestName = test.Attributes["name"].Value,
                    FullTestName = test.Attributes["fullname"].Value,
                    ClassName = test.Attributes["classname"]?.Value ?? "",
                    MethodName = test.Attributes["methodname"]?.Value ?? "",
                };
            }

            _logger.LogInformation("Parsed {@testCount} tests for execution.", testCount);
        }
    }
}
