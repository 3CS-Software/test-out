using NUnit.Framework;
using System.Collections.Generic;

namespace SampleTests
{
    public class Test
    {
        [SetUp]
        public void Setup()
        {
        }

        public static IEnumerable<string> DynamicTestCases()
        {
            for (int ix = 0; ix < 100; ix++)
            {
                yield return "Data_" + ix;
            }
        }

        [TestCaseSource(nameof(DynamicTestCases))]
        public void DoDynamicStuff(string arg)
        {
            TestHelpers.BlockRandomLength();
        }
    }
}