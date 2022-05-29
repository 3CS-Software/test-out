using NUnit.Framework;
using System.Collections.Generic;

namespace SampleTests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        public static IEnumerable<string> DynamicTestCases()
        {
            for (int ix = 0; ix < 20; ix++)
            {
                yield return "Data_" + ix;
            }
        }

        [TestCase(2020, 2, 2, 14, 45), Ignore("Functionality removed for now")]
        [TestCase(2020, 9, 1, 14, 45)]
        public void VerifyTerritoryTimes(int year, int month, int day, int hour, int minute)
        {
            TestHelpers.BlockRandomLength();
        }

        [TestCase(1), Ignore("asdf")] //Both should be ignored.
        [TestCase(2)]
        public void DoAThing(int someValue)
        {
            TestHelpers.BlockRandomLength();
            Assert.Fail("Test should not run, ignored");
        }

        [TestCaseSource(nameof(DynamicTestCases))]
        public void DoDynamicStuff(string arg)
        {
            TestHelpers.BlockRandomLength();
            if (new System.Random().Next(4) != 1)
            {
                Assert.Fail("Randomly Failed");
            }
        }
    }
}