using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication
{
    /// <summary>
    /// Contains information about the running agent or server.
    /// </summary>
    public class HostInfo
    {
        /// <summary>
        /// This is the same ID used to send and receive messages.
        /// </summary>
        /// <remarks>
        /// TODO: make sure this is only written once, and not read before it is written.  Could do this as a singleton that's injected
        /// by the console.
        /// </remarks>
        public string HostId { get; set; }
    }
}
