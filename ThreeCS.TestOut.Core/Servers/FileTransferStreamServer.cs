using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Servers;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Communication
{
    /// <summary>
    /// Hosted by the server, this service co-ordinates file transfers.
    /// </summary>
    public class FileTransferStreamServer
    {
        readonly IMessageBusClient _messageBus;
        readonly ILogger<FileTransferStreamServer> _logger;
        readonly FileTransferStreamData _streamData;
        readonly FileTransferStreamInvoker _invoker;

        public FileTransferStreamServer(
            IMessageBusClient messageBus,
            ILogger<FileTransferStreamServer> logger,
            FileTransferStreamData streamData, 
            FileTransferStreamInvoker invoker)
        {
            _messageBus = messageBus;

            _logger = logger;
            _streamData = streamData;
            _invoker = invoker;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _messageBus.OnMessageReceived<HostFileTransferMessage>(OnHostFileTransfer);

            _invoker.DownloadToFunc = DownloadTo;
            _invoker.UploadFromFunc = UploadFrom;
        }

        private async Task OnHostFileTransfer(string senderId, HostFileTransferMessage message)
        {
            _logger.LogDebug("Creating stream {@streamId}", message.StreamId);

            var streamData = _streamData.CreateNew(message.StreamId);

            //Let the sender know they can now write to the file.
            await _messageBus.SendMessage(message.SenderId, new StartFileTransferWriteMessage
            {
                StreamId = message.StreamId,
                RequestedPath = message.SourcePath
            });

            //Let the receiver know they should read from the file.
            await _messageBus.SendMessage(message.ReceiverId, new StartFileTransferReadMessage
            {
                StreamId = message.StreamId
            });

            //Wait for the upload and download queues to be ready.
            await streamData.ReadyCountdown.WaitAsync();

            //Copy it.  TODO: check how this handles long streams...
            await streamData.SourceStream.CopyToAsync(streamData.DestStream);

            //Notify the upload and download that this is all done.
            streamData.Completed.Set();
        }

        public async Task DownloadTo(Guid streamId, Stream destStream)
        {
            var content = _streamData.Get(streamId);
            content.DestStream = destStream;
            content.ReadyCountdown.Signal();
            await content.Completed.WaitAsync();
        }

        public async Task UploadFrom(Guid streamId, Stream sourceStream)
        {
            var content = _streamData.Get(streamId);
            content.SourceStream = sourceStream;
            content.ReadyCountdown.Signal();
            await content.Completed.WaitAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            //TODO: flush/close any open streams?  They're going to get cleaned up anyway...
        }
    }
}
