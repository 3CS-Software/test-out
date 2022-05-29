using System;
using System.IO;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Execution
{
    public class InvocationResourcePackager
    {
        readonly InvokerConfig _config;

        public InvocationResourcePackager(InvokerConfig config)
        {
            _config = config;
        }

        public async Task<RemotePathInfo> Prepare(string invokerId)
        {
            //TODO: copying stuff around.
            return new RemotePathInfo
            {
                HostId = invokerId,
                SourcePath = _config.BasePath ?? Path.GetDirectoryName(_config.TestAssemblyPath)
            };
        }
    }
}