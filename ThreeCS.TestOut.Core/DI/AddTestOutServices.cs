using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.gPRC;
using ThreeCS.TestOut.Core.Execution;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.PersistedState;
using ThreeCS.TestOut.Core.Servers;

namespace ThreeCS.TestOut
{
    //TODO: better registration, either through using a diff ioc container, using reflection based utility, or using a code-gen'd utility.
    //Also, Don't be so single(ton) minded.
    public static class DependencyHelpers
    {
        private static void AddTestOutCommon(this IServiceCollection services)
        {
            //Common stuff
            services.AddScoped<FileTransferHandler>();
            services.AddScoped<DistributedWorkspaceHandler>();
            services.AddScoped<HeartbeatSender>();

            //services.AddScoped<IMessageBusClient, WsMessageBusClient>();
            services.AddScoped<IMessageBusClient, RpcMessageBusClient>();
        }

        public static IServiceCollection AddTestOutServer(this IServiceCollection services)
        {
            AddTestOutCommon(services);

            //Servers.
            services.AddScoped<Server>();

            services.AddScoped<TestRetriever>();

            services.AddScoped<ServerTestState>();
            services.AddScoped<TestStarter>();
            services.AddScoped<TestHealthMonitor>();
            services.AddScoped<TestCompleteHandler>();
            services.AddScoped<FileTransferStreamServer>();

            services.AddScoped<IMessageBusServer, RpcMessageBusServer>();

            services.AddSingleton<FileTransferStreamData>();
            services.AddSingleton<FileTransferStreamInvoker>();

            services.AddScoped<StatRepo>();
            services.AddScoped<StatsRetrieveService>();
            services.AddScoped<StatsUpdateService>();

            return services;
        }

        public static IServiceCollection AddTestOutAgentOrRunner(this IServiceCollection services)
        {
            AddTestOutCommon(services);

            //Agents.
            services.AddScoped<AgentConfig>();
            services.AddScoped<AgentTestRunner>();
            services.AddScoped<AgentTestProgressNotifier>();
            services.AddScoped<AgentWorker>();

            //Runner.
            services.AddScoped<Invoker>();
            services.AddScoped<InvokerConfig>();
            services.AddScoped<InvocationResourcePackager>();

            return services;
        }


    }
}
