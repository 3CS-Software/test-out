using NUnit.Framework;
using System.Threading;
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
    }
}