using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Agents
{
    /// <summary>
    /// The network facing interface for a group of agent workers in a single process.
    /// </summary>
    /// <remarks>
    /// This class also instantiates the workers using an injected ServiceProvider.  We could configure the IoC but then we're basically
    /// putting the creation logic that is required in this case into the IoC Config, and that is even uglier than injecting the service provider.
    /// </remarks>
    public class AgentHostedService : IHostedService
    {
        readonly IServiceProvider _serviceProvider;
        readonly AgentConfig _config;

        private List<WorkerItem> _workers = new List<WorkerItem>();

        private class WorkerItem
        {
            public AgentWorker Worker;
            public AsyncServiceScope Scope;
        }

        public AgentHostedService(
            IServiceProvider serviceProvider,
            AgentConfig config)
        {
            _serviceProvider = serviceProvider;
            _config = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int maxWorkers = _config.MaxWorkers ?? Environment.ProcessorCount;

            //Setup the agent workers.
            List<Task> agentStartTasks = new List<Task>();
            for (int ix = 0; ix < maxWorkers; ix++)
            {
                var newScope = _serviceProvider.CreateAsyncScope();
                var worker = newScope.ServiceProvider.GetRequiredService<AgentWorker>();
                _workers.Add(new WorkerItem
                {
                    Worker = worker,
                    Scope = newScope
                });

                //Start this worker, with it's index.
                agentStartTasks.Add(worker.Init(ix));
            }
            await Task.WhenAll(agentStartTasks);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var worker in _workers.AsReadOnly())
            {
                await worker.Scope.DisposeAsync();
            }
        }
    }
}
