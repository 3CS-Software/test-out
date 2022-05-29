using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication.Common
{
    internal class MessageRegistration : IDisposable
    {
        public Delegate Callback;

        public void Dispose()
        {
            Callback = null;
        }
    }
}
