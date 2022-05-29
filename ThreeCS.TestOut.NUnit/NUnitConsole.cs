using Asmichi.ProcessManagement;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Utilities;

namespace ThreeCS.TestOut.NUnit
{
    public class NUnitConsole
    {
        readonly ILogger<NUnitConsole> _logger;
        readonly CommandInvoker _commandInvoker;

        public NUnitConsole(
            CommandInvoker commandInvoker,
            ILogger<NUnitConsole> logger)
        {
            _commandInvoker = commandInvoker;
            _logger = logger;
        }

        public async Task<(string output, string error)> RunCommand(string[] commandLineParams, CancellationToken cancelToken, Func<string, Task> outputLineCallback = null)
        {
            Guid processKey = Guid.NewGuid();

            var baseFolder = AppContext.BaseDirectory;
            var nUnitTools = Path.Combine(baseFolder, "NunitTools");
            string filename = Path.Combine(nUnitTools, "nunit3-console.exe");

            _logger.LogDebug("Invoking key {@key}: {@command} {@args}", processKey, filename, commandLineParams);

            (var output, var error) = await _commandInvoker.InvokeCommand(filename, commandLineParams, cancelToken, outputLineCallback);

            _logger.LogDebug("Completed key {@key}: output-{@output}, error-{@error}", processKey, output, error);

            return (output, error);
        }

    }
}
