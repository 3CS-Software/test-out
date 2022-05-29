using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication.Common
{
    internal enum MessageBusClientState
    {
        NotRegistered,
        Registering,
        Connected,
        Disconnected
    }
}
