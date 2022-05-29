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
        static Random _rand = new Random();

        public static void BlockRandomLength()
        {
            var timeSpent = _rand.Next(2 * 1000);
            Thread.Sleep(timeSpent);
        }
    }
}
