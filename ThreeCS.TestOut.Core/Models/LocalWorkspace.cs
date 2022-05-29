using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Models
{
    /// <summary>
    /// The effective spec for running tests locally.  This is used by the server when evaluating tests, and the agents when executing distributed tests.
    /// </summary>
    public class LocalWorkspace
    {
        public TestInvocationSpec RunSpec { get; set; }
        public string BasePath { get; set; }
    }
}
