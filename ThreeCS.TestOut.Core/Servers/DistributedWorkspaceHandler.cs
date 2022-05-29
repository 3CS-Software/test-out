using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Hosting
{
    /// <summary>
    /// Handles creating local workspaces from remote ones.
    /// </summary>
    /// <remarks>
    /// This is thread safe, and will not re-create workspaces that have been already created.
    /// </remarks>
    public class DistributedWorkspaceHandler
    {
        readonly FileTransferHandler _fileTransfer;
        readonly ILogger<DistributedWorkspaceHandler> _logger;
        readonly HostInfo _hostInfo;
        readonly WorkspaceConfig _config;

        /// <summary>
        /// Note that we want to share this semaphore across all agents in the process.  TODO: lock file instead.
        /// </summary>
        private static AsyncSemaphore _workspaceCreateSemaphore = new AsyncSemaphore(1);

        public DistributedWorkspaceHandler(
            FileTransferHandler fileTransfer,
            ILogger<DistributedWorkspaceHandler> logger,
            HostInfo hostInfo,
            WorkspaceConfig config)
        {
            _fileTransfer = fileTransfer;
            _logger = logger;
            _hostInfo = hostInfo;
            _config = config;
        }

        /// <summary>
        /// Copies a workspace from a runner into the agent local folders.        
        /// </summary>
        /// <remarks>
        /// TODO: Lots of enhancements are possible here, this is a very simple first pass implementation to directly get a zipped copy, but we could
        /// compare folders using a hash to only copy what is needed as most of the time test runs rely on the same supporting dlls etc, and copying these
        /// on each iteration is expensive.
        /// </remarks>
        /// <param name="testRunSpec"></param>
        /// <returns></returns>
        public async Task<LocalWorkspace> CreateLocalWorkspace(TestInvocationSpec testRunSpec)
        {
            try
            {
                var remotePath = testRunSpec.SourcePath;

                //Get the working folder for this run/agent combo.
                var testPath = await GetWorkingFolder(_config.WorkingFolder, remotePath.HostId, testRunSpec.Id);

                //We only want one thread on the agent to access this at a time.
                using (var createLock = await _workspaceCreateSemaphore.LockAsync())
                {
                    if (!Directory.Exists(testPath))
                    {
                        _logger.LogDebug("Extracting remote workspace {@remoteSourcePath} to local directory {@testPath}", remotePath.SourcePath, testPath);

                        Directory.CreateDirectory(testPath);

                        await _fileTransfer.CopyRemoteFiles(remotePath, testPath);

                        _logger.LogDebug("Local workspace ready at {@testPath}", testPath);
                    }
                    else
                    {
                        _logger.LogDebug("Remote workspace already exists, skipping download to {@testPath}", testPath);
                    }
                }
                
                return new LocalWorkspace
                {
                    RunSpec = testRunSpec,
                    BasePath = testPath,
                };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed creating local workspace");
                throw;
            }
        }

        private async Task<string> GetWorkingFolder(string workingFolderBase, string hostId, string runId)
        {
            //We only want one thread on the agent to create the temp folder.
            using (var createLock = await _workspaceCreateSemaphore.LockAsync())
            {
                //Ensure the working folder exists.
                Directory.CreateDirectory(workingFolderBase);

                //Check if there's a folder for this host/runid.
                var specFilePath = $"Host_{hostId}_{runId}.txt";
                var fullSpecFilePath = Path.Combine(workingFolderBase, specFilePath);
                if (File.Exists(fullSpecFilePath))
                {
                    //Just use the path info in this one.
                    return await File.ReadAllTextAsync(fullSpecFilePath);
                }
                else
                {
                    var existingFilesOrFolders = Directory.GetDirectories(workingFolderBase).Union(Directory.GetFiles(workingFolderBase));
                    string newFolderName;
                    int ix = 0;
                    do
                    {
                        ix++;
                        newFolderName = Path.Combine(workingFolderBase, ix.ToString());
                    }
                    while (existingFilesOrFolders.Contains(newFolderName));
                    
                    //Found a new folder name that's not taken.
                    await File.WriteAllTextAsync(fullSpecFilePath, newFolderName);

                    return newFolderName;
                }
            }
        }
    }
}
