using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Communication.gRPC
{
    /// <summary>
    /// Contants to encapsulate common setup across message bus client and server.
    /// </summary>
    internal static class RpcChannelOptions
    {
        public static ChannelOption[] DefaultChannelOptions = new[] {
                new ChannelOption("grpc.keepalive_permit_without_calls", 1),
                new ChannelOption("grpc.keepalive_time_ms", 60000),
                // 50MB instead of default 4MB
                new ChannelOption("grpc.max_send_message_length", 50*1024*1024),
                new ChannelOption("grpc.max_receive_message_length", 50*1024*1024)
            };
    }
}
