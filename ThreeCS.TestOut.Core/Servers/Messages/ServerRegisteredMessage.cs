using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Models;

namespace ThreeCS.TestOut.Core.Communication.Messages
{
    public class ServerRegisteredMessage
    {
        public ServerInfo ServerInfo { get; set; }

        //MOTD/Disclaimer etc could go here.
    }
}
