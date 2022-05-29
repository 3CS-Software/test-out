using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Server;

namespace ThreeCS.TestOut.Console
{
    internal static class LogHelper
    {
        public static IHostBuilder ConfigureHostLogging(this IHostBuilder builder)
        {
            return builder.UseSerilog();
        }


        public static ILogger ConfigureLogging(string logFolder, string dataFolder, bool verbose, ExecutionMode mode, bool asService)
        {
            //Basic Config for Serilog.
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .MinimumLevel.Override($"{nameof(ThreeCS)}.{nameof(ThreeCS.TestOut)}", Serilog.Events.LogEventLevel.Information)
                .Enrich.FromLogContext();

            //This is basically the same as the default output, with the logger name included.
            string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

            //Setup console logging, if this isn't running as a service.
            if (!asService)
            {
                ConsoleTheme theme = null;
                var useAsyncConsole = false;// mode.HasFlag(ExecutionMode.Agent) || mode.HasFlag(ExecutionMode.Server);
                if (useAsyncConsole)
                {
                    //We'll do some fancy footwork to ensure the console.writeline doesn't block.  This is required because 
                    //other components in testout (eg TrxLogger) write to Console rather than using ILogger (booooo).
                    System.Console.SetOut(new AsyncTextWriter(System.Console.Out));
                    //Theme needs to be none, as colouring will be messed up otherwise, as the console.setcolor will not happen at the same time the delayed writer
                    //actually writes.
                    theme = ConsoleTheme.None;
                }

                //Console output is async.
                logConfig.WriteTo.Async(ac => ac.Console(outputTemplate: outputTemplate, theme: theme));
            }

            //Setup file logging.
            string effectiveLogFolder = logFolder;
            if (string.IsNullOrEmpty(logFolder))
            {
                //Use default appdata folder.
                effectiveLogFolder = Path.Combine(dataFolder, "Logs");
            }
            else if (logFolder == "NONE")
            {
                effectiveLogFolder = null;
            }

            if (!string.IsNullOrEmpty(effectiveLogFolder))
            {
                Directory.CreateDirectory(effectiveLogFolder);
                //Would be nice to store in subfolders for yyyy-MM, but serilog doesn't support that :(
                //Alternatively, we can switch back to log4net :/
                string logPathTemplate = Path.Combine(effectiveLogFolder, $"testout-{mode.ToString().Replace(", ", "-")}-.log");
                logConfig.WriteTo.File(path: logPathTemplate, outputTemplate: outputTemplate, rollingInterval: RollingInterval.Day, shared: true);
            }

            //Up the logging level if we're doing verbose.  TODO: should we also up all the other non Testout things?
            if (verbose)
            {
                logConfig.MinimumLevel.Override($"{nameof(ThreeCS)}.{nameof(TestOut)}", Serilog.Events.LogEventLevel.Debug);

                //Add the trace logger.
                logConfig.WriteTo.Async(c => c.Debug(outputTemplate: outputTemplate));
            }

            //Set the global shared logger.
            Log.Logger = logConfig.CreateLogger();

            //Return a logger for the program.
            return Log.Logger.ForContext(typeof(Program));
        }
    }
}
