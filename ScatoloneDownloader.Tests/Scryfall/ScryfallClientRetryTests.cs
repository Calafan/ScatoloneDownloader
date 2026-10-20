using System.Net;
using System.Net.Http.Headers;

using ScatoloneDownloader.Scryfall;

namespace ScatoloneDownloader.Tests.Scryfall;

/// <summary>
/// Verifies that <see cref="ScryfallClient"/> retries a transport-level failure
/// (HttpRequestException / TaskCanceledException from the socket or HttpClient
/// timeout), not just a 429/5xx status — so a single network blip during a
/// multi-minute bulk download does not abort the whole run, and pins the backoff
/// the client waits between attempts (exponential on 5xx, <c>Retry-After</c> on
/// 429). Uses the HttpMessageHandler and delay test seams, so no test sleeps.
/// </summary>
public sealed class ScryfallClientRetryTests
{
    [Fact]
    public async Task GetStreamAsync_RetriesHttpRequestException_ThenSucceeds()
    {
        FlakyHandler handler = new(failTimes: 1, failure: new HttpRequestException("simulated TCP reset"));
        using ScryfallClient client = new(handler, NoDelay);

        using Stream stream = await client.GetStreamAsync("https://test/x");

        Assert.Equal(2, handler.Calls); // 1 failure + 1 success
        Assert.Equal(3, stream.Length); // the success body was returned
    }

    [Fact]
    public async Task GetStreamAsync_RetriesTaskCanceled_ThenSucceeds()
    {
        // HttpClient surfaces its request timeout as TaskCanceledException.
        FlakyHandler handler = new(failTimes: 1, failure: new TaskCanceledException("simulated timeout"));
        using ScryfallClient client = new(handler, NoDelay);

        using Stream stream = await client.GetStreamAsync("https://test/x");

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetStreamAsync_On5xx_BacksOffExponentially_ThenThrows()
    {
        StatusHandler handler = new(HttpStatusCode.InternalServerError);
        List<TimeSpan> delays = [];
        using ScryfallClient client = new(handler, Record(delays));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStreamAsync("https://test/x"));

        Assert.Equal(6, handler.Calls); // first try + 5 retries
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16)],
            Backoffs(delays));
    }

    [Fact]
    public async Task GetStreamAsync_On429_WaitsExactlyRetryAfter()
    {
        StatusHandler handler = new(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(3), failTimes: 1);
        List<TimeSpan> delays = [];
        using ScryfallClient client = new(handler, Record(delays));

        using Stream stream = await client.GetStreamAsync("https://test/x");

        Assert.Equal(2, handler.Calls);
        Assert.Equal([TimeSpan.FromSeconds(3)], Backoffs(delays));
    }

    private static Task NoDelay(TimeSpan _) => Task.CompletedTask;

    private static Func<TimeSpan, Task> Record(List<TimeSpan> delays)
    {
        return delay =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        };
    }

    // Throttle waits are always shorter than the 500 ms gate; every backoff is at least a second.
    private static List<TimeSpan> Backoffs(List<TimeSpan> delays)
    {
        return delays.Where(d => d >= TimeSpan.FromSeconds(1)).ToList();
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly TimeSpan? retryAfter;
        private readonly int failTimes;
        private int calls;

        public int Calls => calls;

        public StatusHandler(HttpStatusCode status, TimeSpan? retryAfter = null, int failTimes = int.MaxValue)
        {
            this.status = status;
            this.retryAfter = retryAfter;
            this.failTimes = failTimes;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls++;
            if (calls > failTimes)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3]),
                });
            }

            HttpResponseMessage response = new(status);
            if (retryAfter is { } wait)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
            }

            return Task.FromResult(response);
        }
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
