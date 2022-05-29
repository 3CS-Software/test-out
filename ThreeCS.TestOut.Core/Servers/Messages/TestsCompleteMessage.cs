using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Communication.Messages
{
    public class TestsCompleteMessage
    {
        /// <summary>
        /// The reason why the tests couldn't be completed.  If this is not empty, then the results will not contain
        /// anything.
        /// </summary>
        /// <remarks>
        /// TODO: at least return what we can in results if an error occurs.
        /// </remarks>
        public string Error { get; set; }
        public TestInvocationExecutionInfo Results { get; set; }
    }
}
