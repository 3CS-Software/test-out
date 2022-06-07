using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication
{
    public class WorkspaceConfig
    {
        /// <summary>
        /// Defines whether this will request compressed files or not.
        /// </summary>
        public bool TransferUsingCompression { get; set; }
        public string WorkingFolder { get; set; }
    }
}
