using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Hosting;

namespace ThreeCS.TestOut.Core.Servers
{
    public class ServerHostedService : IHostedService
    {
        readonly IServiceProvider _svcProvider;

        private Server _svr;
        private FileTransferStreamServer _fileSvr;
        private IMessageBusServer _busServer;
        private IServiceScope _scope;

        public ServerHostedService(IServiceProvider svcProvider)
        {
             _svcProvider = svcProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _scope = _svcProvider.CreateAsyncScope();
            _svr = _scope.ServiceProvider.GetRequiredService<Server>();
            _fileSvr = _scope.ServiceProvider.GetRequiredService<FileTransferStreamServer>();
            _busServer = _scope.ServiceProvider.GetRequiredService<IMessageBusServer>();

            await _busServer.Init();
            await _svr.StartServer();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _svr.StopServer();
            await ((IAsyncDisposable)_scope).DisposeAsync();
        }

        //Wrappers for the file stream service. TODO: better way of handling this.
        public Task DownloadTo(Guid streamId, Stream body)
            => _fileSvr.DownloadTo(streamId, body);
        public Task UploadFrom(Guid streamId, Stream sourceStream)
            => _fileSvr.UploadFrom(streamId, sourceStream);
    }
}
