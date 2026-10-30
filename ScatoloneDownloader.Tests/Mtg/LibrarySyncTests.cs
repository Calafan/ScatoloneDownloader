using System.Text.Json;

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
        // The sync expects exactly what the downloader writes: Sanitize(name) + ".png".
        Assert.Equal("Fire_Ice.png", OutputPaths.Sanitize(MakeCard("Fire // Ice").Name) + ".png");
        Assert.Equal("Summon Choco.png", OutputPaths.Sanitize(MakeCard("Summon: Choco").Name) + ".png");
    }

    // --- RegisterNew -------------------------------------------------------

    [Fact]
    public void RegisterNew_NewCard_IsAddedUnratedWithNameAndScryfallId()
    {
        CubeMetadata metadata = new();
        Card bolt = MakeCard("Lightning Bolt", oracleId: "oracle-bolt", id: "print-bolt");

        RegisterResult result = LibrarySync.RegisterNew(metadata, [(bolt, @"C:\lib\Lightning Bolt.png")]);

        CardMetadataEntry entry = Assert.Single(metadata.Cards).Value;
        Assert.Equal("oracle-bolt", Assert.Single(metadata.Cards).Key);
        Assert.Equal("Lightning Bolt", entry.Name);
        Assert.Equal(0, entry.Rating);
        Assert.Equal("print-bolt", entry.ScryfallId);
        Assert.Null(entry.Status);
        Assert.Empty(entry.Effects);
        Assert.Null(entry.ReviewedAt);
        Assert.Equal(["Lightning Bolt"], result.AddedNames);
    }

    [Fact]
    public void RegisterNew_ExistingEntry_IsLeftUntouched_EvenWhenTheMatchedCardDiffers()
    {
        CubeMetadata metadata = new();
        metadata.Cards["oracle-bolt"] = new CardMetadataEntry
        {
            Name = "Lightning Bolt",
            Rating = 5,
            ScryfallId = "print-original",
            Status = "Banned",
            Effects = ["Burn", "Removal"],
            ReviewedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        string before = JsonSerializer.Serialize(metadata.Cards["oracle-bolt"]);

        // Same oracle, different printing id and a different name on the matched card.
        Card other = MakeCard("Bolt, Renamed", oracleId: "oracle-bolt", id: "print-other");

        RegisterResult result = LibrarySync.RegisterNew(metadata, [(other, @"C:\lib\Bolt.png")]);

        Assert.Equal(before, JsonSerializer.Serialize(Assert.Single(metadata.Cards).Value));
        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.AlreadyInStore);
    }

    [Fact]
    public void RegisterNew_TwoFilesOfOneOracle_AddOneEntry()
    {
        CubeMetadata metadata = new();
        Card first = MakeCard("Island", oracleId: "oracle-x", id: "print-1");
        Card second = MakeCard("Island", oracleId: "oracle-x", id: "print-2");

        RegisterResult result = LibrarySync.RegisterNew(
            metadata, [(first, @"C:\lib\a\Island.png"), (second, @"C:\lib\b\Island.png")]);

        Assert.Single(metadata.Cards);
        Assert.Equal("print-1", metadata.Cards["oracle-x"].ScryfallId);
        Assert.Equal(1, result.Added);
        // The second file is not "already in the store": the store did not hold it
        // before this call.
        Assert.Equal(0, result.AlreadyInStore);
    }

    [Fact]
    public void RegisterNew_EmptyOracleId_IsSkippedAndCounted()
    {
        CubeMetadata metadata = new();
        Card token = MakeCard("Some Token", oracleId: string.Empty, id: "print-t");

        RegisterResult result = LibrarySync.RegisterNew(metadata, [(token, @"C:\lib\Some Token.png")]);

        Assert.Empty(metadata.Cards);
        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.SkippedNoOracleId);
    }

    [Fact]
    public void RegisterNew_Counts_SeparateAddedKnownAndSkipped()
    {
        CubeMetadata metadata = new();
        metadata.Cards["oracle-known"] = new CardMetadataEntry { Name = "Known", Rating = 3 };

        List<(Card Card, string FilePath)> matched =
        [
            (MakeCard("Known", "oracle-known", id: "p1"), @"C:\lib\Known.png"),
            (MakeCard("Known", "oracle-known", id: "p1b"), @"C:\lib\x\Known.png"),
            (MakeCard("Alpha", "oracle-new-a", id: "p2"), @"C:\lib\Alpha.png"),
            (MakeCard("Beta", "oracle-new-b", id: "p3"), @"C:\lib\Beta.png"),
            (MakeCard("Nameless", string.Empty, id: "p4"), @"C:\lib\Nameless.png"),
        ];

        RegisterResult result = LibrarySync.RegisterNew(metadata, matched);

        Assert.Equal(["Alpha", "Beta"], result.AddedNames);
        Assert.Equal(2, result.Added);
        // Two files of the known oracle count as ONE known card.
        Assert.Equal(1, result.AlreadyInStore);
        Assert.Equal(1, result.SkippedNoOracleId);
        Assert.Equal(3, metadata.Cards.Count);
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
