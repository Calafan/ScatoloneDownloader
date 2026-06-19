using System;
using System.IO;
using System.Net;
using System.Net.Http;
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
		private DateTime minNextRequestTime = DateTime.MinValue;

		public ScryfallClient()
		{
			httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
			httpClient.DefaultRequestHeaders.Add("User-Agent", "ScatoloneDownloader");
		}

		private async Task ThrottleAsync()
		{
			TimeSpan wait = minNextRequestTime - DateTime.Now;

			if (wait > TimeSpan.Zero)
			{
				await Task.Delay(wait);
			}

			minNextRequestTime = DateTime.Now.Add(MinRequestInterval);
		}

		public async Task<string> GetJsonAsync(string url)
		{
			using Stream stream = await GetStreamAsync(url);
			using StreamReader reader = new(stream);

			return await reader.ReadToEndAsync();
		}

		/// <summary>
		/// Fetches a resource and buffers it into a seekable <see cref="MemoryStream"/>,
		/// so the caller owns the bytes and the underlying response can be released.
		/// </summary>
		public async Task<Stream> GetStreamAsync(string url)
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
