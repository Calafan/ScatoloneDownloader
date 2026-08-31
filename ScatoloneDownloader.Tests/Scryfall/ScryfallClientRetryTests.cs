using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Scryfall;

using Xunit;

namespace ScatoloneDownloader.Tests.Scryfall;

/// <summary>
/// Verifies that <see cref="ScryfallClient"/> retries a transport-level failure
/// (HttpRequestException / TaskCanceledException from the socket or HttpClient
/// timeout), not just a 429/5xx status — so a single network blip during a
/// multi-minute bulk download does not abort the whole run. Uses the
/// HttpMessageHandler test seam.
/// </summary>
public sealed class ScryfallClientRetryTests
{
    [Fact]
    public async Task GetStreamAsync_RetriesHttpRequestException_ThenSucceeds()
    {
        FlakyHandler handler = new(failTimes: 1, failure: new HttpRequestException("simulated TCP reset"));
        using ScryfallClient client = new(handler);

        using Stream stream = await client.GetStreamAsync("https://test/x");

        Assert.Equal(2, handler.Calls); // 1 failure + 1 success
        Assert.Equal(3, stream.Length); // the success body was returned
    }

    [Fact]
    public async Task GetStreamAsync_RetriesTaskCanceled_ThenSucceeds()
    {
        // HttpClient surfaces its request timeout as TaskCanceledException.
        FlakyHandler handler = new(failTimes: 1, failure: new TaskCanceledException("simulated timeout"));
        using ScryfallClient client = new(handler);

        using Stream stream = await client.GetStreamAsync("https://test/x");

        Assert.Equal(2, handler.Calls);
    }

    private sealed class FlakyHandler : HttpMessageHandler
    {
        private readonly int failTimes;
        private readonly Exception failure;
        private int calls;

        public int Calls => calls;

        public FlakyHandler(int failTimes, Exception failure)
        {
            this.failTimes = failTimes;
            this.failure = failure;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls++;
            if (calls <= failTimes)
            {
                return Task.FromException<HttpResponseMessage>(failure);
            }

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            };
            return Task.FromResult(response);
        }
    }
}
