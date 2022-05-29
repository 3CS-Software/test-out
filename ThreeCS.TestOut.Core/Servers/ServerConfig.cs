using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Servers
{
    public class ServerConfig
    {
        public int HealthCheckIntervalSeconds { get; set; } = 30;
        public int BatchSize { get; set; }
    }
}
