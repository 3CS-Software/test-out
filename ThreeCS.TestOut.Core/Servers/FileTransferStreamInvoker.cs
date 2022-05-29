using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Servers
{
    /// <summary>
    /// Shim class to handle calling hosted ServerHostedService quasi singleton from scoped services.
    /// </summary>
    public class FileTransferStreamInvoker
    {
        public Func<Guid, Stream, Task> DownloadToFunc { get; set; }
        public Func<Guid, Stream, Task> UploadFromFunc { get; set; }
    }
}
