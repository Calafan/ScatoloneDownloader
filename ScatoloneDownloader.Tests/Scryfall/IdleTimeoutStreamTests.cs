using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Scryfall;

using Xunit;

namespace ScatoloneDownloader.Tests.Scryfall;

/// <summary>
/// Covers <see cref="IdleTimeoutStream"/>, the guard between a bulk download and
/// an unbounded wait. Every test here exists because of one observed failure: an
/// import that sat for thirteen minutes with no CPU, no open socket, no error and
/// no exit. A stalled body must become a fast, named exception on every read path,
/// including the synchronous one that used to bypass the guard entirely.
/// Timeouts are constructor parameters precisely so these can run in milliseconds.
/// </summary>
public sealed class IdleTimeoutStreamTests
{
    private static readonly TimeSpan Brief = TimeSpan.FromMilliseconds(150);

    /// <summary>A stream that never produces a byte, and honours cancellation —
    /// what a connection that has gone silent mid-body looks like from here.</summary>
    private sealed class SilentStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A stream that never ends but keeps dribbling one byte per read:
    /// healthy by the per-read measure, never finishing by any useful one. This is
    /// the case only the total ceiling can catch.</summary>
    private sealed class DribblingStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            buffer.Span[0] = (byte)'x';
            return 1;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ReadAsync_SilentConnection_ThrowsInsteadOfHanging()
    {
        using IdleTimeoutStream stream = new(new SilentStream(), Brief);

        IOException error = await Assert.ThrowsAsync<IOException>(
            async () => Assert.Equal(0, await stream.ReadAsync(new byte[16])));

        Assert.Contains("stalled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Synchronous_IsGuardedToo()
    {
        // The hole this whole change exists to close: the sync override used to
        // call the inner stream directly, so anything reaching it could wait
        // forever with no timer running.
        using IdleTimeoutStream stream = new(new SilentStream(), Brief);

        IOException error = Assert.Throws<IOException>(() => stream.Read(new byte[16], 0, 16));

        Assert.Contains("stalled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_TransferThatNeverEnds_TripsTheTotalCeiling()
    {
        // Each individual read completes in 10 ms, so the idle guard is happy
        // forever; only the lifetime ceiling ends this.
        using IdleTimeoutStream stream = new(new DribblingStream(), TimeSpan.FromSeconds(30), Brief);

        IOException error = await Assert.ThrowsAsync<IOException>(async () =>
        {
            byte[] buffer = new byte[1];
            for (int i = 0; i < 10000; i++)
            {
                Assert.Equal(1, await stream.ReadAsync(buffer));
            }
        });

        Assert.Contains("ceiling", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_HealthyStream_ReadsToCompletion()
    {
        byte[] payload = Encoding.UTF8.GetBytes("the quick brown fox");
        using IdleTimeoutStream stream = new(new MemoryStream(payload), Brief, TimeSpan.FromSeconds(30));

        using MemoryStream copy = new();
        await stream.CopyToAsync(copy);

        Assert.Equal(payload, copy.ToArray());
    }

    [Fact]
    public void Read_HealthyStream_SyncPathStillReturnsTheBytes()
    {
        byte[] payload = Encoding.UTF8.GetBytes("jumps over the lazy dog");
        using IdleTimeoutStream stream = new(new MemoryStream(payload), Brief);

        byte[] buffer = new byte[payload.Length];
        int read = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(payload.Length, read);
        Assert.Equal(payload, buffer);
    }

    [Fact]
    public async Task ReadAsync_CallerCancellation_SurfacesAsCancellationNotAsAStall()
    {
        // A caller-driven cancel is not a network fault and must not be reported
        // as one, or a Ctrl+C would read like a broken connection.
        using CancellationTokenSource cts = new();
        using IdleTimeoutStream stream = new(new SilentStream(), TimeSpan.FromSeconds(30));

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => Assert.Equal(0, await stream.ReadAsync(new byte[16], cts.Token)));
    }

    [Fact]
    public void Dispose_DisposesTheInnerStream()
    {
        MemoryStream inner = new([1, 2, 3]);

        using (IdleTimeoutStream stream = new(inner, Brief))
        {
            Assert.True(stream.CanRead);
        }

        Assert.False(inner.CanRead); // disposed along with the wrapper
    }
}
