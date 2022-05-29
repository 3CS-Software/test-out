using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleTests
{
    [TestFixture]
    public class NamingEdgeCasesFixture
    {
        [TestCase("A simple value")]
        [TestCase("A value with puncuation: ;/\\'`~!@#$%^&*()_+=-\"[]{}<>,.? should do it.")]
        [TestCase(int.MaxValue)]
        public void ArgumentsTest(object argumentValue)
        {
            TestHelpers.BlockRandomLength();
        }

        [TestCase("DuplicateNameTest")]
        [TestCase("DuplicateNameTest")]
        public void DuplicateNameTest()
        {

        }
    }
}
