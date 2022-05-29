using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Agents
{
    public class AgentConfig
    {
        /// <summary>
        /// The maximum number of simultanous test workers to run with.
        /// </summary>
        public int? MaxWorkers { get; set; }
    }
}
