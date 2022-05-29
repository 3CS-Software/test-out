using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleTests
{
    [TestFixture]
    public class TestResultFixture
    {
        [Test]
        public void RandomlySucceeding()
        {
            TestHelpers.BlockRandomLength();
            var rand = new Random();
            if (rand.Next(2) != 1)
            {
                Assert.Fail("Randomly Failed");
            }
        }

        [Test]
        public void NoisyTest()
        {
            Debug.WriteLine("Testing Debug Output");
            Console.WriteLine("Testing Console Output");

            Console.Out.WriteLine("Output written to output stream");
            Console.Out.Flush();
            
            Console.Error.WriteLine("Error written to error stream");
            Console.Error.Flush();

            Trace.WriteLine("Trace Output");
            Trace.Flush();

            TestContext.Error.WriteLine("Error Written to Test Context");

            TestContext.Progress.WriteLine("Progress Written to Test Context");

            TestContext.Out.WriteLine("Output Written to Test Context");

            TestContext.Error.Flush();
            TestContext.Progress.Flush();
            TestContext.Out.Flush();
        }

        [Test]
        public void FailingTest()
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Bad stuff happened");
        }

        [TestCase("1", TestName = "SometimesExplicit_Show")]
        [TestCase("2", TestName = "SometimesExplicit_DontShow")]
        public void SometimesExplicit(string someVar)
        {
            TestHelpers.BlockRandomLength();
        }

        [Test, Explicit]
        public void TestIsExplicit()
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Test should not run, explicit");
        }

        [Test]
        public void StackOverflow()
        {
            CallBadThing();
        }

        private void CallBadThing()
        {
            CallBadThing();
        }

        [Test, Ignore("Ignore reason")]
        public void TestIsIgnored()
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Test should not run, ignored");
        }

        [Ignore("Both Should Be Ignored")]
        [TestCase(1)]
        [TestCase(2)]
        public void TestWithMultipleCasesIsIgnored(int someValue)
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Test should not run, ignored");
        }
    }
}
