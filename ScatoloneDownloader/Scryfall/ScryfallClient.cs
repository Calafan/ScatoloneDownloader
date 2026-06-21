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
	/// </summary>
	internal sealed class ScryfallClient : IDisposable
	{
		private static readonly TimeSpan MinRequestInterval = TimeSpan.FromMilliseconds(100);

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
			await ThrottleAsync();

			using HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new HttpRequestException(string.Format("Unable to contact: {0}. Status code: {1}", url, response.StatusCode));
			}

			using Stream stream = await response.Content.ReadAsStreamAsync();

			return await JsonSerializer.DeserializeAsync<T>(stream, options);
		}

		/// <summary>
		/// Fetches a (small) binary resource — card images — and buffers it into a
		/// seekable <see cref="MemoryStream"/>, so the caller owns the bytes and the
		/// underlying response can be released.
		/// </summary>
		internal async Task<Stream> GetStreamAsync(string url)
		{
			await ThrottleAsync();

			using HttpResponseMessage response = await httpClient.GetAsync(url);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new HttpRequestException(string.Format("Unable to contact: {0}. Status code: {1}", url, response.StatusCode));
			}

			byte[] bytes = await response.Content.ReadAsByteArrayAsync();

			return new MemoryStream(bytes);
		}

		public void Dispose()
		{
			httpClient.Dispose();
		}
	}
}
