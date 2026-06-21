using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ScatoloneDownloader.Scryfall
{
	/// <summary>
	/// Wraps a stream and aborts a read that stalls: every individual read must make
	/// progress within <see cref="idleTimeout"/>, otherwise it is cancelled. The timer
	/// resets per read, so a long-but-healthy download (e.g. a multi-hundred-MB
	/// Scryfall bulk-data file) is never cut short — only a connection that goes
	/// silent mid-body is. Owns the inner stream and disposes it.
	/// </summary>
	internal sealed class IdleTimeoutStream : Stream
	{
		private readonly Stream inner;
		private readonly TimeSpan idleTimeout;

		internal IdleTimeoutStream(Stream inner, TimeSpan idleTimeout)
		{
			this.inner = inner;
			this.idleTimeout = idleTimeout;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			using CancellationTokenSource timeoutCts = new(idleTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				return await inner.ReadAsync(buffer, linkedCts.Token);
			}
			catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
			{
				throw new IOException(string.Format("Read stalled: no data for {0:N0} ms.", idleTimeout.TotalMilliseconds));
			}
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			// Synchronous path has no idle guard; deserialization uses the async path above.
			return inner.Read(buffer, offset, count);
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
