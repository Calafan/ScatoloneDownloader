using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Download;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Pins the pure steps of <c>sync</c>: the plan against the files on disk, the
/// register-and-propose step (new entries only, never reviewed, never an existing
/// entry), the count of cards a dry run would register, the entries stamped with
/// another printing, and the store entries with no image. No network and no real
/// library.
/// </summary>
public sealed class LibrarySyncTests
{
    private static readonly string Lib = Path.Combine(Path.GetTempPath(), "Quintet", "Source");

    private static string PathFor(string file) => Path.Combine(Lib, "1993", "Alpha", file);

    private static Card MakeCard(string name, string oracleId = "", string oracleText = "", string typeLine = "Instant", string id = "")
    {
        JsonCard json = new()
        {
            Id = id,
            Name = name,
            OracleId = oracleId,
            OracleText = oracleText,
            Language = "en",
            ReleasedAt = "1993-08-05",
            Layout = "normal",
            TypeLine = typeLine,
            Games = ["paper"],
            FrameEffects = [],
            Reprint = false,
            Variation = false,
            Textless = false,
            Set = "LEA",
            SetName = "Alpha",
            SetType = "core",
            BorderColor = "black",
            Cmc = 1,
            ImageUris = new JsonImageUris { Png = "https://test/img.png" },
        };

        return Card.CreateCard(json);
    }

    // --- Plan --------------------------------------------------------------

    [Fact]
    public void Plan_SplitsPresentMissingAndExtra()
    {
        Card here = MakeCard("Here");
        Card gone = MakeCard("Gone");

        SyncPlan plan = LibrarySync.Plan(
            [(here, PathFor("Here.png")), (gone, PathFor("Gone.png"))],
            [PathFor("Here.png"), PathFor("Stray.png")]);

        Assert.Equal(1, plan.PresentCount);
        Assert.Equal([gone], plan.Missing.Select(m => m.Card));
        Assert.Equal([PathFor("Stray.png")], plan.Extra);
    }

    [Fact]
    public void Plan_MatchesPathsCaseInsensitively()
    {
        SyncPlan plan = LibrarySync.Plan([(MakeCard("Here"), PathFor("Here.png"))], [PathFor("HERE.PNG")]);

        Assert.Equal(1, plan.PresentCount);
        Assert.Empty(plan.Missing);
        Assert.Empty(plan.Extra);
    }

    [Fact]
    public void Plan_ExtraFilesAreSortedOrdinally()
    {
        SyncPlan plan = LibrarySync.Plan([], [PathFor("b.png"), PathFor("C.png"), PathFor("a.png")]);

        Assert.Equal([PathFor("C.png"), PathFor("a.png"), PathFor("b.png")], plan.Extra);
    }

    [Fact]
    public void Plan_TwoCardsOnOnePath_AreOneMissing()
    {
        SyncPlan plan = LibrarySync.Plan(
            [(MakeCard("A"), PathFor("Same.png")), (MakeCard("B"), PathFor("Same.png"))], []);

        Assert.Single(plan.Missing);
        Assert.Equal(0, plan.PresentCount);
    }

    [Fact]
    public void Plan_TwoCardsOnOnePresentPath_CountOnce()
    {
        SyncPlan plan = LibrarySync.Plan(
            [(MakeCard("A"), PathFor("Same.png")), (MakeCard("B"), PathFor("Same.png"))], [PathFor("Same.png")]);

        Assert.Equal(1, plan.PresentCount);
        Assert.Empty(plan.Missing);
    }

    [Fact]
    public void ExpectedPath_FollowsTheDownloadNaming_ForSplitCardsAndColons()
    {
        // The sync expects exactly what `years --library` writes: Sanitize(name) + ".png".
        Assert.Equal("Fire_Ice.png", OutputPaths.Sanitize(MakeCard("Fire // Ice").Name) + ".png");
        Assert.Equal("Summon Choco.png", OutputPaths.Sanitize(MakeCard("Summon: Choco").Name) + ".png");
    }

    // --- RegisterAndPropose ------------------------------------------------

    [Fact]
    public void RegisterAndPropose_NewCard_GetsAnUnratedUnreviewedEntryWithTheProposal()
    {
        Card kill = MakeCard("Doom Blade", "oid-kill", "Destroy target creature.");
        CubeMetadata metadata = new();

        SyncRegistration result = LibrarySync.RegisterAndPropose(
            metadata, [(kill, PathFor("Doom Blade.png"))], new Dictionary<string, Card> { ["oid-kill"] = kill });

        CardMetadataEntry entry = metadata.Cards["oid-kill"];
        CardEffect expected = EffectClassifier.Classify(kill);
        Assert.NotEqual(CardEffect.None, expected);
        Assert.Equal(expected, entry.EffectFlags);
        Assert.Equal(0, entry.Rating);
        Assert.Null(entry.ReviewedAt);
        Assert.Equal(["Doom Blade"], result.AddedNames);
        Assert.Equal(["oid-kill"], result.AddedOracleIds);
        Assert.Equal((1, 0), (result.Proposed, result.NoProposal));
    }

    [Fact]
    public void RegisterAndPropose_CardWithNoProposal_GetsAnEntryWithNoEffects()
    {
        Card bear = MakeCard("Vanilla", "oid-bear", "", "Creature — Bear");
        CubeMetadata metadata = new();

        SyncRegistration result = LibrarySync.RegisterAndPropose(
            metadata, [(bear, PathFor("Vanilla.png"))], new Dictionary<string, Card> { ["oid-bear"] = bear });

        Assert.Equal(CardEffect.None, metadata.Cards["oid-bear"].EffectFlags);
        Assert.Equal((0, 1), (result.Proposed, result.NoProposal));
    }

    [Fact]
    public void RegisterAndPropose_ExistingEntries_AreKeptByteForByte()
    {
        Card reviewedCard = MakeCard("Reviewed", "oid-r", "Destroy target creature.");
        Card unreviewedCard = MakeCard("Unreviewed", "oid-u", "Destroy target creature.");
        DateTimeOffset stamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        CubeMetadata metadata = new();
        metadata.Cards["oid-r"] = new CardMetadataEntry { Name = "Reviewed", Rating = 4, ReviewedAt = stamp, EffectFlags = CardEffect.Burn };
        metadata.Cards["oid-u"] = new CardMetadataEntry { Name = "Unreviewed", Rating = 2, EffectFlags = CardEffect.None };

        Dictionary<string, Card> index = new() { ["oid-r"] = reviewedCard, ["oid-u"] = unreviewedCard };
        SyncRegistration result = LibrarySync.RegisterAndPropose(
            metadata, [(reviewedCard, PathFor("Reviewed.png")), (unreviewedCard, PathFor("Unreviewed.png"))], index);

        Assert.Empty(result.AddedNames);
        Assert.Equal((0, 0), (result.Proposed, result.NoProposal));

        CardMetadataEntry reviewed = metadata.Cards["oid-r"];
        Assert.Equal((4, stamp, CardEffect.Burn), (reviewed.Rating, reviewed.ReviewedAt, reviewed.EffectFlags));

        // An unreviewed entry that EXISTED before is not proposed over either.
        CardMetadataEntry unreviewed = metadata.Cards["oid-u"];
        Assert.Equal((2, (DateTimeOffset?)null, CardEffect.None), (unreviewed.Rating, unreviewed.ReviewedAt, unreviewed.EffectFlags));
    }

    // --- NewOracleIds ------------------------------------------------------

    [Fact]
    public void NewOracleIds_SkipsKnownDuplicatesAndBlanks()
    {
        CubeMetadata metadata = new();
        metadata.Cards["known"] = new CardMetadataEntry { Name = "Known" };

        IReadOnlyList<string> fresh = LibrarySync.NewOracleIds(metadata, ["known", "a", "a", "", "  ", "b"]);

        Assert.Equal(["a", "b"], fresh);
        Assert.Single(metadata.Cards);
    }

    // --- StaleIds ----------------------------------------------------------

    [Fact]
    public void StaleIds_ListsOnlyExistingEntriesStampedWithAnotherPrinting()
    {
        CubeMetadata metadata = new();
        metadata.Cards["moved"] = new CardMetadataEntry { Name = "Moved", ScryfallId = "old" };
        metadata.Cards["kept"] = new CardMetadataEntry { Name = "Kept", ScryfallId = "same" };

        IReadOnlyList<SyncRestamp> stale = LibrarySync.StaleIds(metadata,
        [
            MakeCard("Moved", oracleId: "moved", id: "new"),
            MakeCard("Kept", oracleId: "kept", id: "same"),
            MakeCard("Fresh", oracleId: "fresh", id: "x"),
        ]);

        Assert.Equal([new SyncRestamp("moved", "Moved", "new")], stale);
    }

    [Fact]
    public void StaleIds_FirstPickPerOracleIdWins()
    {
        CubeMetadata metadata = new();
        metadata.Cards["oid"] = new CardMetadataEntry { Name = "Card", ScryfallId = "first" };

        IReadOnlyList<SyncRestamp> stale = LibrarySync.StaleIds(metadata,
        [
            MakeCard("Card", oracleId: "oid", id: "first"),
            MakeCard("Card", oracleId: "oid", id: "second"),
        ]);

        Assert.Empty(stale);
    }

    // --- Orphans -----------------------------------------------------------

    [Fact]
    public void Orphans_ListsOnlyEntriesWithoutAnImage_SortedByName()
    {
        CubeMetadata metadata = new();
        metadata.Cards["here"] = new CardMetadataEntry { Name = "Here", Rating = 3 };
        metadata.Cards["z"] = new CardMetadataEntry { Name = "Zed", Rating = 4, ReviewedAt = DateTimeOffset.UnixEpoch };
        metadata.Cards["a"] = new CardMetadataEntry { Name = "Ay", Rating = 1 };

        IReadOnlyList<SyncOrphan> orphans = LibrarySync.Orphans(metadata, new HashSet<string> { "here" });

        Assert.Equal([new SyncOrphan("Ay", 1, false), new SyncOrphan("Zed", 4, true)], orphans);
    }
}
