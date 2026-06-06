using System.IO;
using System.Text;
using System.Threading.Channels;

namespace Shittim.Utils
{
    /// <summary>
    /// A <see cref="TextWriter"/> wrapper that enqueues all writes onto a
    /// bounded channel and drains them on a single background thread.
    ///
    /// This ensures that <c>Console.WriteLine</c> from any ASP.NET
    /// request-handling thread returns almost immediately – even when the
    /// underlying console handle or pipe is slow or temporarily blocked
    /// (e.g. pipe buffer full, or a consumer that is lagging behind).
    ///
    /// If the channel reaches capacity the oldest entries are dropped so
    /// that the calling thread is **never** blocked.
    /// </summary>
    public sealed class AsyncConsoleWriter : TextWriter
    {
        private readonly TextWriter _inner;
        private readonly Channel<string> _channel;

        private const int MaxQueuedLines = 4096;

        public AsyncConsoleWriter(TextWriter inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxQueuedLines)
            {
                FullMode = BoundedChannelFullMode.DropOldest,   // never block the caller
                SingleReader = true,
                SingleWriter = false,
            });

            // Fire-and-forget background drain loop.
            var thread = new Thread(DrainLoop)
            {
                IsBackground = true,
                Name = "AsyncConsoleWriter",
            };
            thread.Start();
        }

        public override Encoding Encoding => _inner.Encoding;

        // ── TextWriter overrides ───────────────────────────────────────

        public override void Write(char value)
        {
            _channel.Writer.TryWrite(value.ToString());
        }

        public override void Write(string? value)
        {
            if (value is not null)
                _channel.Writer.TryWrite(value);
        }

        public override void WriteLine()
        {
            _channel.Writer.TryWrite(Environment.NewLine);
        }

        public override void WriteLine(string? value)
        {
            _channel.Writer.TryWrite((value ?? string.Empty) + Environment.NewLine);
        }

        public override void Flush()
        {
            // No-op – the background thread flushes continuously.
        }

        // ── Background drain ───────────────────────────────────────────

        private void DrainLoop()
        {
            try
            {
                var reader = _channel.Reader;
                // Synchronous blocking read – fine on a dedicated thread.
                while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
                {
                    while (reader.TryRead(out var text))
                    {
                        try
                        {
                            _inner.Write(text);
                        }
                        catch
                        {
                            // Swallow write errors (broken pipe, etc.)
                        }
                    }

                    try
                    {
                        _inner.Flush();
                    }
                    catch
                    {
                        // Swallow flush errors.
                    }
                }
            }
            catch
            {
                // Channel completed or unexpected error – stop draining.
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channel.Writer.TryComplete();
            }
            base.Dispose(disposing);
        }
    }
}
