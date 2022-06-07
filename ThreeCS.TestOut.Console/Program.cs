using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ThreeCS.TestOut.Console;
using ThreeCS.TestOut.Core.Agents;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Execution;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.PersistedState;
using ThreeCS.TestOut.Core.Servers;
using ThreeCS.TestOut.NUnit;
using ThreeCS.TestOut.Trx;

namespace ThreeCS.TestOut.Server
{
    public class Program
    {
        /// <summary>
        /// TestOut: It's like a campout for your tests!
        /// </summary>
        /// <param name="mode">The mode to run in.  This is required.  For console running, this can be a comma seperated list of modes.  If running as a service, 
        /// this must be either 'Agent' or 'Server'</param>
        /// <param name="serverUrl">The server URL that this will either host at for server mode, or connect to for agent or runner.  
        /// This should be of the form https://SomeHost. </param>
        /// <param name="basePath">Only used for Runner.  The base path for the tests to run in.  Everything in this path will be 
        /// distributed to all agents.</param>
        /// <param name="testAssembly">Only used for Runner.  The assembly to search for tests.</param>
        /// <param name="resultFilename">Only used for Runner.  The filename to aggregate the test results into.</param>
        /// <param name="maxRetryCount">Only used for Runner.  The maximum number of times to re-run a failed test.</param>
        /// <param name="compressFileTransfers">Specifies that any transfers into this console should be compressed.  Defaults to false.</param>
        /// <param name="testInactivityTimeout">Only used for Runner.  The maximum amount of seconds that a test can process without giving any 
        /// feedback before being considered dead.</param>
        /// <param name="batchSize">Only used for Server.  The number of tests per thread to include in a batch.  For the default 
        /// batch size of 10, this means if an agent has 8 threads, then it will be passed 80 tests (10 tests per thread, 8 threads) 
        /// to process.  This is a very rough way to level the tests passed to each 
        /// agent based on their processing power.</param>
        /// <param name="verbose">Sets internal testout logging level to debug. Note this will get quite noisy and is only recommended 
        /// for diagnostics, not day to day running.</param>
        /// <param name="logFolder">The folder to output logs.  Logs will be in the folder format testout-yyyyMMdd.log.  
        /// Setting this to 'NONE' will disable logging.  Leaving this empty will log under the system appdata folder.</param>
        /// <param name="maxWorkers">Only used for the Agent.  The maximum number of test workers.  Defaults to the machine core count if null.</param>
        /// <param name="asService">Used when installed using sc.exe on windows.  Modifies hosting slightly to work as a windows service.</param>
        /// <returns></returns>
        static async Task<int> Main(
            ExecutionMode mode,
            string serverUrl = "http://localhost:34872/",
            string basePath = ".",
            string testAssembly = "",
            string resultFilename = "test_results.trx",
            int batchSize = 10,
            int maxRetryCount = 2,
            bool compressFileTransfers = false,
            int testInactivityTimeout = 1200,
            bool verbose = false,
            string logFolder = "",
            int? maxWorkers = null,
            bool asService = false
            )
        {
            //Sanity check.
            if (asService && (mode != ExecutionMode.Server && mode != ExecutionMode.Agent))
            {
                throw new Exception($"Cannot start service in given mode.  Use either '{nameof(ExecutionMode.Agent)}' or '{nameof(ExecutionMode.Server)}'");
            }

            //TODO: can dragonfruit propogate args?...
            string[] args = new string[0];

            var dataFolder = asService
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TestOut")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TestOut");

            Directory.CreateDirectory(dataFolder);

            var globalLog = LogHelper.ConfigureLogging(logFolder, dataFolder, verbose, mode, asService);

            globalLog.Debug("Started Logging");
            var version = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(typeof(Program).Assembly, typeof(AssemblyFileVersionAttribute), false)).Version;
            globalLog.Information("Started Testout Console version {@version}", version);

            try
            {
                List<Task> hostTasks = new List<Task>();

                //Run the server URI through the uri builder, which will add the trailing slash if it's missing, and will also cause an error if it's invalid.
                var serverUrlBuilder = new UriBuilder(serverUrl);
                serverUrl = serverUrlBuilder.ToString();

                //The file transfer web comms work over a diff port.  TODO: refac so file transfer uses websockets rather than
                //just this way.  Fly in the ointment atm is that websocket client in .net doesn't allow streaming easily atm.
                var fileTransferServerUrl = new UriBuilder(serverUrl);
                fileTransferServerUrl.Port++;

                //Some global configs.
                var serverConnectionConfig = new ServerConnectionConfig { ServerUrl = serverUrl, FileServerUrl = fileTransferServerUrl.ToString() };
                var workspaceConfig = new WorkspaceConfig { WorkingFolder = Path.Combine(dataFolder, "Data") , TransferUsingCompression = compressFileTransfers };
                var storageConfig = new StorageConfig { StateFolder = Path.Combine(dataFolder, "State") };

                void AddCommonConfigs(IServiceCollection svc)
                {
                    svc.AddSingleton(serverConnectionConfig);
                    svc.AddSingleton(workspaceConfig);
                    svc.AddSingleton(storageConfig);
                }

                if (mode.HasFlag(ExecutionMode.Server))
                {
                    //All the server init stuff is handled in the web host.
                    //TODO: look at using MinimalAPIs for this stuff, rather than a full mvc host.
                    //https://gist.github.com/davidfowl/ff1addd02d239d2d26f4648a06158727
                    var builder = Host.CreateDefaultBuilder(args)
                        .UseWindowsService(c => c.ServiceName = "TestOut Server")
                        .ConfigureHostLogging()
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                        //We need to ship around large files, so don't limit request body size.  TODO: make a controller for the methods and
                        //limit just those.
                        webBuilder.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = null);
                            webBuilder.UseUrls(serverConnectionConfig.FileServerUrl);
                            webBuilder.UseStartup<Startup>();
                        });


                    var serverConfig = new ServerConfig
                    {
                        BatchSize = batchSize
                    };

                    builder.ConfigureServices(svc =>
                    {
                        AddCommonConfigs(svc);
                        svc.AddSingleton(serverConfig);
                        svc.AddSingleton(new HostInfo { HostId = serverConnectionConfig.ServerId });
                        svc.AddHostedService<ServerHostedService>();
                    });
                    var host = builder.Build();

                    hostTasks.Add(host.RunAsync());
                }

                if (mode.HasFlag(ExecutionMode.Agent))
                {
                    var builder = CreateConsoleHostBuilder(args, "TestOut Agent");
                    builder.ConfigureServices(svc =>
                    {
                        AddCommonConfigs(svc);
                        //If this is running as a service, use the machine name as the agent, otherwise go with a random name.
                        svc.AddScoped(_ => new HostInfo { HostId = "Agent_" + (asService ? Environment.MachineName : Guid.NewGuid()) });
                        svc.AddHostedService<AgentHostedService>();
                    });
                    var host = builder.Build();

                    var agentConfig = host.Services.GetService<AgentConfig>();
                    agentConfig.MaxWorkers = maxWorkers;

                    //await host.StartService<Agent>(a => a.StartAgent(), serverUrl);
                    hostTasks.Add(host.RunAsync());
                }

                if (mode.HasFlag(ExecutionMode.Run))
                {

                    var builder = CreateConsoleHostBuilder(args, null)
                        .ConfigureServices(svc =>
                        {
                            AddCommonConfigs(svc);
                            svc.AddSingleton(new HostInfo { HostId = "Invoker_" + Guid.NewGuid() });
                        });
                    var host = builder.Build();

                    var config = host.Services.GetService<InvokerConfig>();
                    config.BasePath = basePath;
                    config.TestAssemblyPath = testAssembly;
                    config.ResultFilename = resultFilename;
                    config.MaxRetryCount = maxRetryCount;
                    config.TestInactivityTimeoutSeconds = testInactivityTimeout;

                    //Start the invocation.
                    await host.StartService<Invoker>(async r =>
                    {
                    //Perform the test run.
                    await r.RunTests();
                        await host.StopAsync();
                    }, serverUrl);


                    hostTasks.Add(host.WaitForShutdownAsync());
                }

                await Task.WhenAll(hostTasks);
                globalLog.Information("Testout Closed", version);

                return 1;
            }
            catch (Exception ex)
            {
                globalLog.Fatal(ex, "A fatal exception occurred :( ");
                return -1;
            }
        }

        private static IHostBuilder CreateConsoleHostBuilder(string[] args, string serviceName)
        {
            var builder = Host.CreateDefaultBuilder(args);

            if (serviceName != null)
            {
                builder.UseWindowsService(c => c.ServiceName = serviceName);
            }
            else
            {
                builder.UseConsoleLifetime();
            }

            builder.ConfigureHostLogging()
                .ConfigureServices(coll => coll
                    .AddTestOutAgentOrRunner()
                    .AddNUnitTestOutAgent()
                    .AddTrxTestOut()
                );

            return builder;
        }
    }

    internal static class ServiceInvoker
    {
        public static async Task StartService<TService>(this IHost host, Func<TService, Task> startAction, string serverUrl)
        {
            //TODO: better async way of this.  The fluent mixed makes it a bit more challenging but.. 
            var svc = host.Services.GetService<TService>();
            await startAction(svc);
        }
    }
}
