using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Hosting
{
    public class ServerConnectionConfig
    {
        public string ServerUrl { get; set; } = "https://localhost:5001";
        public string FileServerUrl = "https://localhost:5002";
        public string ServerId = "Server";
    }
}
