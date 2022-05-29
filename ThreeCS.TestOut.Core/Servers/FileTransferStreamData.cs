using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeCS.TestOut.Core.Servers
{
    public class FileTransferStreamData
    {
        public class StreamData
        {
            public Stream SourceStream;
            public Stream DestStream;
            public AsyncCountdownEvent ReadyCountdown;
            public AsyncManualResetEvent Completed;
        }

        private ConcurrentDictionary<Guid, StreamData> _streamsById;

        public FileTransferStreamData()
        {
            _streamsById = new ConcurrentDictionary<Guid, StreamData>();
        }

        public StreamData CreateNew(Guid streamId)
        {
            var sd = new StreamData
            {
                ReadyCountdown = new AsyncCountdownEvent(2),
                Completed = new AsyncManualResetEvent()
            };
            _streamsById.TryAdd(streamId, sd);
            return sd;
        }

        public StreamData Get(Guid streamId)
        {
            return _streamsById[streamId];
        }
    }
}
