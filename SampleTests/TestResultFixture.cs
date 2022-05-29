using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleTests
{
    [TestFixture]
    public class TestResultFixture
    {
        [Test]
        public void FailingTest()
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Bad stuff happened");
        }

        [Test, Explicit]
        public void TestIsExplicit()
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Test should not run, explicit");
        }

        [Test, Ignore("Ignore reason")]
        public void TestIsIgnored()
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Test should not run, ignored");
        }
    }
}
