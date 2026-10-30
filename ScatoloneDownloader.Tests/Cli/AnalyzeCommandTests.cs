using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The pieces of <c>analyze</c> that run without Scryfall: its option rules, the list
/// payload (every card counts, no bench) and the store-or-classifier sync.
/// </summary>
public sealed class AnalyzeCommandTests
{
    [Fact]
    public void Validate_OutputWithTwoFiles_IsAnError()
    {
        AnalyzeCommand.Settings settings = new()
        {
            MetadataDirectory = Path.Combine(Path.GetTempPath(), "meta"),
            Files = ["a.txt", "b.txt"],
            OutputFile = "x",
        };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void Validate_OutputWithOneFile_AndManyFilesWithoutOutput_AreFine()
    {
        string meta = Path.Combine(Path.GetTempPath(), "meta");

        Assert.True(new AnalyzeCommand.Settings { MetadataDirectory = meta, Files = ["a.txt"], OutputFile = "x" }.Validate().Successful);
        Assert.True(new AnalyzeCommand.Settings { MetadataDirectory = meta, Files = ["a.txt", "b.txt"] }.Validate().Successful);
    }

    [Fact]
    public void BuildForList_CountsEveryCard_AndHasNoBench()
    {
        List<Card> cards =
        [
            StatsMarkdownTests.Make("Unrated", ["W"], "Instant", rating: 0),
            StatsMarkdownTests.Make("Banned", ["W"], "Instant", rating: 4, status: CardStatus.Banned),
            StatsMarkdownTests.Make("Rejected", ["W"], "Instant", rating: 1),
            StatsMarkdownTests.Make("Bench", ["W"], "Instant", rating: 2),
            StatsMarkdownTests.Make("Pool", ["W"], "Instant", rating: 4),
        ];

        StatsPayload payload = StatsPayload.BuildForList(cards, "mylist");

        Assert.Null(payload.Bench);
        Assert.Null(payload.BenchOverview);
        Assert.Equal("mylist", payload.Pool.Label);
        Assert.Equal(5, payload.Pool.Mono.TotalCards);
        Assert.Equal(5, payload.Overview.NonLand);
    }

    [Fact]
    public void Build_Cube_KeepsThePoolLabel_AndHasABenchOverview()
    {
        StatsPayload payload = StatsPayload.Build([StatsMarkdownTests.Make("Bench", ["W"], "Instant", rating: 2)]);

        Assert.Equal("Pool", payload.Pool.Label);
        Assert.NotNull(payload.Bench);
        Assert.Equal(1, payload.BenchOverview!.NonLand);
    }

    [Fact]
    public void BuildForList_OrdersTheEffectRowsByTheListsOwnTotals()
    {
        List<Card> cards =
        [
            StatsMarkdownTests.Make("A", ["W"], "Instant", CardEffect.Ramp, rating: 0),
            StatsMarkdownTests.Make("B", ["W"], "Instant", CardEffect.Ramp, rating: 0),
            StatsMarkdownTests.Make("C", ["W"], "Instant", CardEffect.Burn, rating: 0),
        ];

        List<string> rows = StatsPayload.BuildForList(cards, "l").Pool.Mono.Rows.Select(r => r.Effect).ToList();

        Assert.Equal("Ramp", rows[0]);
        Assert.Equal("Burn", rows[1]);
    }

    [Fact]
    public void SyncFromJsonOrClassify_AnEntryWins_EvenWithNoEffects()
    {
        Card inStore = StatsMarkdownTests.Make("Reviewed Nothing", ["W"], "Instant", oracleId: "o-1", oracleText: "Destroy target creature.");
        CubeMetadata data = new();
        data.Cards["o-1"] = new CardMetadataEntry { Rating = 3, Effects = [], ReviewedAt = DateTimeOffset.UtcNow };

        int found = MetadataJsonSynchronizer.SyncFromJsonOrClassify([inStore], data);

        Assert.Equal(1, found);
        Assert.Equal(CardEffect.None, inStore.Effects);
        Assert.Equal(3, inStore.Rating);
        Assert.True(inStore.Reviewed);
    }

    [Fact]
    public void SyncFromJsonOrClassify_AMissingCardGetsTheClassifiersFlags_AndTheCountIsTheEntries()
    {
        Card known = StatsMarkdownTests.Make("Known", ["W"], "Instant", oracleId: "o-known");
        Card unknown = StatsMarkdownTests.Make("Unknown", ["B"], "Instant", rating: 5, status: CardStatus.Banned, reviewed: true,
            oracleId: "o-unknown", oracleText: "Destroy target creature.");
        CubeMetadata data = new();
        data.Cards["o-known"] = new CardMetadataEntry { Rating = 4, Effects = ["Ramp"] };

        int found = MetadataJsonSynchronizer.SyncFromJsonOrClassify([known, unknown], data);

        Assert.Equal(1, found);
        Assert.Equal(CardEffect.Ramp, known.Effects);
        Assert.True(unknown.Effects.HasFlag(CardEffect.Removal));
        Assert.Equal(0, unknown.Rating);
        Assert.Equal(CardStatus.None, unknown.Status);
        Assert.False(unknown.Reviewed);
    }
}
