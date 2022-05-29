using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Communication
{
    public class FileTransferHandler
    {
        private class StreamDownloadRegistrations
        {
            public AsyncManualResetEvent CompleteEvent;
            public string DestLocalFolder;
        }

        readonly IMessageBusClient _messageBus;
        readonly ServerConnectionConfig _serverConfig;
        readonly HostInfo _hostInfo;
        readonly ILogger<FileTransferHandler> _logger;
        readonly WorkspaceConfig _fileConfig;

        private Dictionary<Guid, StreamDownloadRegistrations> _streamDownloadRegistrationsByStreamId;

        public FileTransferHandler(
            IMessageBusClient messageBus,
            ServerConnectionConfig serverConfig,
            ILogger<FileTransferHandler> logger,
            HostInfo hostInfo,
            WorkspaceConfig fileConfig)
        {
            _messageBus = messageBus;
            _serverConfig = serverConfig;
            _logger = logger;
            _hostInfo = hostInfo;
            _fileConfig = fileConfig;

            _streamDownloadRegistrationsByStreamId = new Dictionary<Guid, StreamDownloadRegistrations>();

            _messageBus.OnMessageReceived<StartFileTransferReadMessage>(OnStartFileTransferRead);
            _messageBus.OnMessageReceived<StartFileTransferWriteMessage>(OnStartFileTransferWrite);
            
        }

        /// <summary>
        /// Another handler has requested files, start streaming them to the requested stream.
        /// </summary>
        private async Task OnStartFileTransferWrite(string senderId, StartFileTransferWriteMessage message)
        {
            //Pipe the zip stream into the request stream.  Saves writing a stream forwarder.
            var pipeServerStream = new AnonymousPipeServerStream();
            var pipeClientStream = new AnonymousPipeClientStream(PipeDirection.In, pipeServerStream.GetClientHandleAsString());

            //Zip into the pipe.
            using var memStream = new MemoryStream(64 * 1024);
            using var zipArchive = new ZipArchive(pipeServerStream, ZipArchiveMode.Create);

            //Upload out of the pipe.
            using HttpClient hc = GetClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "Stream/" + message.StreamId);
            using var reqContent = new StreamContent(pipeClientStream);
            request.Content = reqContent;

            //Begin the request async.
            _logger.LogDebug("Starting Upload Request");
            var responseTask = hc.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            //Now we have a zip archive stream wired up to the upload stream post.  We'll pump all the 
            //directory contents into it, and close it.
            _logger.LogDebug("Zipping Files For Upload");
            string basePath = Path.GetFullPath(message.RequestedPath);
            var allFiles = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories);

            int sentCount = 1;
            int totalCount = allFiles.Count();
            foreach (var file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
            {
                _logger.LogDebug("Sending file {@curItem} ({@curCount} of {@total})", file, sentCount++, totalCount);
                var zipEntryPath = file.Replace(basePath + Path.DirectorySeparatorChar, "");
                var zipEntry = zipArchive.CreateEntry(zipEntryPath);
                using var fileStream = File.OpenRead(file);
                using var zipEntryStream = zipEntry.Open();

                await fileStream.CopyToAsync(zipEntryStream);
                //await pipeServerStream.Flush();
            }

            //Explicitly close the zip archive, so the underlying streams are closed (I hope..)
            zipArchive.Dispose();

            _logger.LogDebug("Completed File Upload Request");
            var response = await responseTask;
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Completed File Upload");
        }

        /// <summary>
        /// The server has given the all clear to start downloading files.  Grab them from the server stream and
        /// copy them locally, then signal that we're done.
        /// </summary>
        /// <param name="senderId"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task OnStartFileTransferRead(string senderId, StartFileTransferReadMessage message)
        {
            //First check if there's work to do.
            if (!_streamDownloadRegistrationsByStreamId.TryGetValue(message.StreamId, out var streamDownloadRegistration))
            {
                return;
            }

            // Copy the zip over into this agent
            using HttpClient hc = GetClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "Stream/" + message.StreamId);
            var response = await hc.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Reading from file stream");
            var responseStream = await response.Content.ReadAsStreamAsync();
            _logger.LogDebug("Creating Zip Archive from file stream");
            var zipArchive = new ZipArchive(responseStream, ZipArchiveMode.Read);
            _logger.LogDebug("Extracting Zip stream into folder {@folder}", streamDownloadRegistration.DestLocalFolder);


            var progress = new Progress<ZipProgress>();
            progress.ProgressChanged += (o, e) =>
            {
                _logger.LogDebug("Receiving file {@curItem} ({@curCount} of {@total})", e.CurrentItem, e.Processed, e.Total);
            };

            zipArchive.ExtractToDirectory(streamDownloadRegistration.DestLocalFolder, progress);
            _logger.LogDebug($"Zip extraction complete.");

            //Finally, signal to the waiting items that we're done.
            streamDownloadRegistration.CompleteEvent.Set();
        }

        private void Progress_ProgressChanged(object sender, ZipProgress e)
        {
            throw new NotImplementedException();
        }

        private HttpClient GetClient()
        {
            //TODO: single http client?  how much does this cost to spin up?...
            var hc = new HttpClient();
            hc.BaseAddress = new Uri(_serverConfig.FileServerUrl);
            return hc;
        }

        public virtual async Task CopyRemoteFiles(RemotePathInfo sourcePath, string localDestPath)
        {
            //Register to handle server events.
            var streamId = Guid.NewGuid();
            AsyncManualResetEvent mre = new AsyncManualResetEvent();
            _streamDownloadRegistrationsByStreamId[streamId] = new StreamDownloadRegistrations
            {
                CompleteEvent = mre,
                DestLocalFolder = localDestPath
            };

            //Send a message to the server to facilitate the transfer.
            await _messageBus.SendMessage(_serverConfig.ServerId, new HostFileTransferMessage
            {
                SenderId = sourcePath.HostId,
                ReceiverId = _hostInfo.HostId,
                StreamId = streamId,
                SourcePath = sourcePath.SourcePath
            });

            //Wait for the contents to be copied locally.
            await mre.WaitAsync();
        }
    }
}
