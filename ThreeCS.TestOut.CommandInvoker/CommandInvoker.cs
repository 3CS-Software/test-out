using Asmichi.ProcessManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Utilities
{
    /// <summary>
    /// Library to invoke a process and get back response outputs
    /// </summary>
    public class CommandInvoker
    {
        public async Task<(string output, string error)> InvokeCommand(string filename, string[] commandLineParams, CancellationToken cancelToken, Func<string, Task> outputLineCallback = null)
        {
            //Using redirect to file, using stream doesn't work for some reason under .net 6.
            //could be related to this? https://github.com/dotnet/runtime/issues/62851

            //As a workaround, we'll redirect to file, and open that file and stream it's contents.
            string stdOutFile = Path.GetTempFileName();
            string stdErrFile = Path.GetTempFileName();

            var si = new ChildProcessStartInfo(filename, commandLineParams)
            {
                StdOutputFile = stdOutFile,
                StdOutputRedirection = OutputRedirection.File,
                StdErrorFile = stdErrFile,
                StdErrorRedirection = OutputRedirection.File
            };

            using var p = ChildProcess.Start(si);

            var exitTask = p.WaitForExitAsync(cancelToken);

            if (outputLineCallback != null)
            {
                //Open the std out file using a shared read mode, so nunit can still write to it.
                using var stdOutFileStream = File.Open(stdOutFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                using var stdOutReader = new StreamReader(stdOutFileStream);
                Task<string> outputTask;

                //Interactively read the std out, and callback with each line.
                StringBuilder sb = new StringBuilder();
                while (!exitTask.IsCompleted)
                {
                    while (!stdOutReader.EndOfStream)
                    {
                        var stdOutLine = await stdOutReader.ReadLineAsync();
                        sb.AppendLine(stdOutLine);
                        await outputLineCallback(stdOutLine);
                    }

                    //Only check for new log file content every 100 milliseconds.  Polling isn't great, but this 
                    //whole method is a workaround.
                    Thread.Sleep(100);
                }

                outputTask = Task.FromResult(sb.ToString());
            }

            await exitTask;

            var output = File.ReadAllText(stdOutFile);
            var error = File.ReadAllText(stdErrFile);

            File.Delete(stdOutFile);
            File.Delete(stdErrFile);

            return (output, error);
        }
    }
}
