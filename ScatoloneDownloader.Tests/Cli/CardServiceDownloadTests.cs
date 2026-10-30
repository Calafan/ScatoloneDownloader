using System.Net;

using ScatoloneDownloader.Cli.Download;
using ScatoloneDownloader.Download;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Scryfall;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Pins the download loop's circuit breaker: a failed card is recorded and the run
/// goes on, but a 429 that outlived every retry stops the run at that card, and the
/// report rethrows it so the command ends in an error.
/// </summary>
public sealed class CardServiceDownloadTests
{
    [Fact]
    public async Task DownloadEachAsync_RateLimit_StopsAtThatCard()
    {
        Card a = MakeCard("A");
        Card b = MakeCard("B");
        Card c = MakeCard("C");
        List<string> attempted = [];
        int afterEach = 0;

        CardService.DownloadTally tally = await CardService.DownloadEachAsync(
            [a, b, c],
            card =>
            {
                attempted.Add(card.Name);
                return card == b
                    ? Task.FromException<DownloadOutcome>(new ScryfallRateLimitException("https://test/b.png"))
                    : Task.FromResult(DownloadOutcome.Downloaded);
            },
            () => afterEach++);

        Assert.Equal(["A", "B"], attempted);
        Assert.Equal(1, tally.Downloaded);
        Assert.Empty(tally.Failures);
        Assert.Same(b, tally.StoppedAt);
        Assert.NotNull(tally.RateLimit);
        Assert.Equal(1, afterEach);
    }

    [Fact]
    public async Task DownloadEachAsync_OtherFailure_IsRecordedAndTheRunGoesOn()
    {
        Card a = MakeCard("A");
        Card b = MakeCard("B");
        Card c = MakeCard("C");
        List<string> attempted = [];

        CardService.DownloadTally tally = await CardService.DownloadEachAsync(
            [a, b, c],
            card =>
            {
                attempted.Add(card.Name);
                return card == b
                    ? Task.FromException<DownloadOutcome>(new HttpRequestException("Status code: NotFound", null, HttpStatusCode.NotFound))
                    : Task.FromResult(DownloadOutcome.Skipped);
            },
            () => { });

        Assert.Equal(["A", "B", "C"], attempted);
        Assert.Equal(2, tally.Skipped);
        Assert.Equal("B", Assert.Single(tally.Failures).Name);
        Assert.Null(tally.StoppedAt);
        Assert.Null(tally.RateLimit);
    }

    [Fact]
    public void ReportTally_RateLimit_Rethrows()
    {
        CardService.DownloadTally tally = new()
        {
            Downloaded = 1,
            StoppedAt = MakeCard("B"),
            RateLimit = new ScryfallRateLimitException("https://test/b.png"),
        };

        Assert.Throws<ScryfallRateLimitException>(() => CardService.ReportTally(tally, 3, skipExisting: true, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ReportTally_FailuresAlone_DoNotThrow()
    {
        CardService.DownloadTally tally = new() { Downloaded = 2 };
        tally.Failures.Add(("B", "TMP", "Status code: NotFound"));

        CardService.ReportTally(tally, 3, skipExisting: true, TimeSpan.FromSeconds(1));
    }

    private static Card MakeCard(string name) =>
        TestCards.Make(name, id: "id-" + name, oracleId: "oracle-" + name);
}
