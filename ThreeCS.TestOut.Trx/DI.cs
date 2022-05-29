using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Invokers;

namespace ThreeCS.TestOut.Trx
{
    public static class NUnitTestOutDIHelper
    {
        public static IServiceCollection AddTrxTestOut(this IServiceCollection services)
        {
            services.AddSingleton<IResultSerializer, TrxResultSerializer>();

            return services;
        }
    }
}
