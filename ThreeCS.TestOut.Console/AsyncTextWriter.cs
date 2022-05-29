using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Utility;

namespace ThreeCS.TestOut.Console
{
    /// <summary>
    /// This class routes all text writes to an internal buffer, then flushes them to the wrapped writer in an async manner.  It
    /// can also optionally drop writes if the flush to the internal writer takes too long.  The purpose is to not block the
    /// writer, so it should only be used for cases where the output is not critical, eg, verbose debug log sampling.
    /// </summary>
    /// <remarks>
    /// Heavily inspired by https://github.com/serilog/serilog-sinks-async/blob/dev/src/Serilog.Sinks.Async/Sinks/Async/BackgroundWorkerSink.cs
    /// Basically wanted something like this, but that would apply to any console writes, so used it with Console.SetOut to handle other classes that didn't
    /// use ILogger, and instead did naughty logging straight to Console.  I'm looking at you, TrxLogger...
    /// Using this DOES mean that Console colors get messed up, so maybe we could make this run synced until a write timeout (of say, 10 seconds), and then it could
    /// switch over to async until it catches up again?  Not sure.  Right now, colours seem less important than things hanging :/
    /// </remarks>
    internal class AsyncTextWriter : TextWriter
    {
        readonly BlockingCollection<Char> _queue;
        readonly Task _worker;
        readonly bool _blockWhenFull;
        readonly TextWriter _innerWriter;

        /// <summary>
        /// Creates a new AsyncTextWriter
        /// </summary>
        /// <param name="innerWriter">The writer to wrap.</param>
        /// <param name="blockWhenFull">Whether to block when the buffer is full, or continue and lose extra characters.  Defaults to lossy (false).</param>
        /// <param name="maxBufferCharacters">Maximum character buffer size.  Defaults to about 4mb worth (2 ^ 22 characters).</param>
        public AsyncTextWriter(TextWriter innerWriter, bool blockWhenFull = false, int maxBufferCharacters = 4194304)
        {
            _innerWriter = innerWriter;
            _blockWhenFull = blockWhenFull;
            _queue = new BlockingCollection<char>(maxBufferCharacters);
            _worker = TaskHelpers.StartLongRunning(Pump);
        }

        /// <summary>
        /// Returns the encoding of the inner writer.
        /// </summary>
        public override Encoding Encoding => _innerWriter.Encoding;

        /// <summary>
        /// Writes out the given characters to the internal buffer.
        /// </summary>
        /// <remarks>
        /// As per https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter?f1url=%3FappId%3DDev16IDEF1%26l%3DEN-US%26k%3Dk(System.IO.TextWriter)%3Bk(DevLang-csharp)%26rd%3Dtrue&view=net-5.0#notes-to-implementers
        /// this is what we need to implement to capture all the various WriteXYZ permutations.
        /// </remarks>
        public override void Write(char value)
        {
            try
            {
                if (_blockWhenFull)
                {
                    _queue.Add(value);
                }
                else
                {
                    if (!_queue.TryAdd(value))
                    {
                        // Console.WriteLine("Queue was full");
                        // JK!
                        // Just dropping it.  We could log something here, but the point of this is for non essential stuff, so we don't really care.
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Thrown if CompleteAdding is called and then this is called (eg, race condition).  We're disposing in this case, so we'll
                // once again let this through to the keeper.
            }
        }

        private void Pump()
        {
            try
            {
                foreach (var next in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        _innerWriter.Write(next);
                    }
                    catch (Exception ex)
                    {
                        // Failed to write character.  We don't care!  Could log something, I guess.
                    }
                }
            }
            catch (InvalidOperationException)
            {
                //If this is being diposed, this might happen.  We'll let this one go.
            }
            catch
            {
                // Something unexpectedly bad happened.  Because our job is to delay the console and drop packets, and not just provide a
                // totally robust console, we'll explode in glittery magic.  Yes.. this whole code block could be skipped, but then where
                // would I describe why it wasn't present?... Thanks compiler for removing this ;)
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _queue.CompleteAdding();
                _worker.Wait();
                _innerWriter.Dispose();
            }
        }
    }
}
