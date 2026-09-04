using System.Diagnostics;

namespace ScatoloneDownloader.Scryfall
{
    /// <summary>
    /// Wraps a stream and aborts a read that stalls: every individual read must make
    /// progress within <see cref="idleTimeout"/>, otherwise it is cancelled. The timer
    /// resets per read, so a long-but-healthy download (e.g. a multi-hundred-MB
    /// Scryfall bulk-data file) is never cut short — only a connection that goes
    /// silent mid-body is. Owns the inner stream and disposes it.
    /// <para>
    /// An optional <see cref="totalTimeout"/> covers the case the per-read guard
    /// cannot see: a transfer that keeps dribbling bytes just often enough to reset
    /// the idle timer, and so never finishes and never trips. It is measured from
    /// construction and is a hard ceiling on the whole body.
    /// </para>
    /// </summary>
    internal sealed class IdleTimeoutStream : Stream
    {
        private readonly Stream inner;
        private readonly TimeSpan idleTimeout;
        private readonly TimeSpan? totalTimeout;
        private readonly Stopwatch lifetime = Stopwatch.StartNew();

        internal IdleTimeoutStream(Stream inner, TimeSpan idleTimeout, TimeSpan? totalTimeout = null)
        {
            this.inner = inner;
            this.idleTimeout = idleTimeout;
            this.totalTimeout = totalTimeout;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            TimeSpan budget = idleTimeout;

            if (totalTimeout != null)
            {
                TimeSpan remaining = totalTimeout.Value - lifetime.Elapsed;

                if (remaining <= TimeSpan.Zero)
                {
                    throw new IOException(
                        string.Format("Download exceeded its {0:N0} s ceiling.", totalTimeout.Value.TotalSeconds));
                }

                if (remaining < budget)
                {
                    budget = remaining;
                }
            }

            using CancellationTokenSource timeoutCts = new(budget);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                return await inner.ReadAsync(buffer, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Which clock ran out decides the message: an idle connection and a
                // transfer that overstayed its welcome need different diagnoses.
                if (totalTimeout != null && lifetime.Elapsed >= totalTimeout.Value)
                {
                    throw new IOException(
                        string.Format("Download exceeded its {0:N0} s ceiling.", totalTimeout.Value.TotalSeconds));
                }

                throw new IOException(string.Format("Read stalled: no data for {0:N0} ms.", idleTimeout.TotalMilliseconds));
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <summary>
        /// Routed through the guarded async path rather than straight to the inner
        /// stream. This used to call <c>inner.Read</c> directly on the assumption
        /// that deserialization only ever takes the async path — an assumption
        /// nothing enforced, and the one hole through which a silent, unbounded
        /// hang could still reach the caller. There is no synchronization context
        /// in a console host, so blocking on the task here cannot deadlock.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
