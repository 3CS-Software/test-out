using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Utility
{
    public static class TaskHelpers
    {
        public static Task StartLongRunning(Func<Task> taskFunction)
        {
            return Task.Factory.StartNew(taskFunction, CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        public static Task StartLongRunning(Action taskFunction)
        {
            return Task.Factory.StartNew(taskFunction, CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
    }
}
