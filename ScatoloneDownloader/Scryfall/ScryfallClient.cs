using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScatoloneDownloader.Scryfall
{
	/// <summary>
	/// Async HTTP access to the Scryfall API. A single <see cref="HttpClient"/> is
	/// reused for every request, and an async gate keeps consecutive requests
	/// spaced by at least <see cref="MinRequestInterval"/> to honour Scryfall's
	/// rate limit. Downloads stay sequential; the gate is the single choke point.
	/// A 429 (or 5xx) is retried with backoff, honouring any <c>Retry-After</c>
	/// header, because sustained paging can still trip the limit.
	/// </summary>
	internal sealed class ScryfallClient : IDisposable
	{
		// Scryfall asks for 50-100 ms between requests (~10 req/s). 100 ms sits right
		// on the limit and still trips 429 under sustained paging, so leave a margin.
		private static readonly TimeSpan MinRequestInterval = TimeSpan.FromMilliseconds(150);

		private const int MaxRetries = 5;

		// Max time a single bulk-data read may stall before we give up. The whole
		// download can still take minutes — only an idle (silent) connection trips this.
		private static readonly TimeSpan ReadIdleTimeout = TimeSpan.FromSeconds(30);

		private readonly HttpClient httpClient;

		// Monotonic (ms since boot), so the rate-limit gate is immune to wall-clock
		// jumps (DST, NTP corrections) that a DateTime.Now-based gate would suffer.
		private long minNextRequestTickMs;

		internal ScryfallClient()
		{
			httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
			httpClient.DefaultRequestHeaders.Add("User-Agent", "ScatoloneDownloader");
		}

		private async Task ThrottleAsync()
		{
			long wait = minNextRequestTickMs - Environment.TickCount64;

			if (wait > 0)
			{
				await Task.Delay((int)wait);
			}

			minNextRequestTickMs = Environment.TickCount64 + (long)MinRequestInterval.TotalMilliseconds;
		}

		/// <summary>
		/// Streams a JSON resource straight into the deserializer without buffering the
		/// whole payload first. Scryfall bulk-data files run to hundreds of megabytes
		/// (sometimes &gt;2&#160;GB), so we read with <see cref="HttpCompletionOption.ResponseHeadersRead"/>
		/// and deserialize from the live response stream.
		/// </summary>
		internal async Task<T> GetFromJsonAsync<T>(string url, JsonSerializerOptions options = null)
		{
			using HttpResponseMessage response = await SendWithRetryAsync(url, HttpCompletionOption.ResponseHeadersRead);

			Stream stream = await response.Content.ReadAsStreamAsync();
			using IdleTimeoutStream guardedStream = new(stream, ReadIdleTimeout);

			return await JsonSerializer.DeserializeAsync<T>(guardedStream, options);
		}

		/// <summary>
		/// Fetches a (small) binary resource — card images — and buffers it into a
		/// seekable <see cref="MemoryStream"/>, so the caller owns the bytes and the
		/// underlying response can be released.
		/// </summary>
		internal async Task<Stream> GetStreamAsync(string url)
		{
			using HttpResponseMessage response = await SendWithRetryAsync(url, HttpCompletionOption.ResponseContentRead);

			byte[] bytes = await response.Content.ReadAsByteArrayAsync();

			return new MemoryStream(bytes);
		}

		/// <summary>
		/// Issues a throttled GET and retries a 429/5xx with backoff (honouring any
		/// <c>Retry-After</c> header) up to <see cref="MaxRetries"/> times. Returns the
		/// successful response — the caller owns and disposes it.
		/// </summary>
		private async Task<HttpResponseMessage> SendWithRetryAsync(string url, HttpCompletionOption completionOption)
		{
			for (int attempt = 1; ; attempt++)
			{
				await ThrottleAsync();

				HttpResponseMessage response = await httpClient.GetAsync(url, completionOption);

				if (response.StatusCode == HttpStatusCode.OK)
				{
					return response;
				}

				bool transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;

				if (transient && attempt <= MaxRetries)
				{
					TimeSpan delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt) * 0.5);
					response.Dispose();

					await Task.Delay(delay);
					continue;
				}

				HttpStatusCode status = response.StatusCode;
				response.Dispose();

				throw new HttpRequestException(string.Format("Unable to contact: {0}. Status code: {1}", url, status));
			}
		}

		public void Dispose()
		{
			httpClient.Dispose();
		}
	}
}
