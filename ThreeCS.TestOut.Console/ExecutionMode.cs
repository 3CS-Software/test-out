using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Server
{
    /// <summary>
    /// The execution mode.
    /// </summary>
    [Flags]
    public enum ExecutionMode
    {
        /// <summary>
        /// This will run tests.
        /// </summary>
        Run = 1,

        /// <summary>
        /// This will run an agent.
        /// </summary>
        Agent = 2,

        /// <summary>
        /// This will run a server.
        /// </summary>
        Server = 4
    }
}
