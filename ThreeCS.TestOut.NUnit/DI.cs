using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Agents;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Utilities;

namespace ThreeCS.TestOut.NUnit
{
    public static class NUnitTestOutDIHelper
    {
        public static IServiceCollection AddNUnitTestOutServer(this IServiceCollection services)
        {
            services.AddScoped<ITestEnumerator, NUnitTestEnumerator>();
            services.AddScoped<NUnitConsole>();
            services.AddScoped<CommandInvoker>();

            return services;
        }

        public static IServiceCollection AddNUnitTestOutAgent(this IServiceCollection services)
        {
            services.AddScoped<IAgentTestRunner, NUnitAgentTestRunner>();
            services.AddScoped<NUnitConsole>();
            services.AddScoped<CommandInvoker>();

            return services;
        }
    }
}
