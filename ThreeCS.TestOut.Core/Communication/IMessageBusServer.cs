using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication
{
    public interface IMessageBusServer : IAsyncDisposable
    {
        public Task Init();
    }
}
