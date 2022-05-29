using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampleTests
{
    public static class TestHelpers
    {
        public static void BlockRandomLength()
        {
            Thread.Sleep(2000);
        }
    }
}
