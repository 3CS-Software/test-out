using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Utilities;

namespace ThreeCS.TestOut.Core.Tests.Integration
{
    [TestFixture]
    public class EndToEndTests
    {
        [Test]
        public void MultiAgentTests() 
        {
            var invoker = new CommandInvoker();

            //TODO: Run these in 4 separate threads to each invoke a process and await its return before thread ends.

            // Start server going.
            var serverResult = invoker.InvokeCommand
                ("ThreeCS.TestOut.Console.exe",
                new string[]{ "--mode Server --test-assembly \"Sample.FrameworkTests.dll\" --verbose --batch-size 5 --max-workers 5"}, 
                new CancellationToken());

            // Start n agent
            for (int ix = 0; ix < 5; ix++)
            {
                var agentResult = invoker.InvokeCommand
                    ("ThreeCS.TestOut.Console.exe",
                    new string[] { "--mode Agent --test-assembly \"Sample.FrameworkTests.dll\" --verbose --batch-size 5 --max-workers 5" },
                new CancellationToken());
            }

            // start invoker
            var runResult = invoker.InvokeCommand
                  ("ThreeCS.TestOut.Console.exe",
                  new string[] { "--mode Run --base-path \"..\\..\\..\\..\\Sample.FrameworkTests\\bin\\Debug\" --test-assembly \"Sample.FrameworkTests.dll\" --verbose --batch-size 5 --max-workers 5" },
                new CancellationToken());


            // invoke tests.            


        }

        string ConsoleExePath => Path.GetFullPath("ThreeCS.TestOut.Console.exe");
        string SampleTestsDllPath => Path.GetFullPath("..\\..\\..\\..\\Sample.FrameworkTests\\bin\\debug\\");
        string SampleTestsDllFile => "Sample.FrameworkTests.dll";


        [Test]
        public async Task AgentLifespanTests()
        {
            var invoker = new CommandInvoker();

            var cancelTokenSrc = new CancellationTokenSource();

            var args = new string[] { "--mode", "Run,Server,Agent", $"--base-path", SampleTestsDllPath, $"--test-assembly", SampleTestsDllFile, "--verbose", "--batch-size", "5", "--max-workers", "5" };
            var serverResult = invoker.InvokeCommand(
                ConsoleExePath,
                args,
                cancelTokenSrc.Token);

            var result = await serverResult;
            //TODO: validate results, also, tests may be freezing due to locks in stdout/err.
        }
    }
}