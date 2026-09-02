using System.Collections.Generic;

using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Covers the two pure pieces of <c>import</c>'s XMP seed that carry the
/// data-correctness fixes from the code review: <see cref="ImportCommand.ApplyImportSeed"/>
/// (the upsert gating that protects tagger-authored work — #6 scryfallId,
/// #7 rating-demote) and <see cref="ImportCommand.ReduceByOracle"/> (collapsing
/// reprints that share one Scryfall <see cref="Card"/> so seeding is not
/// last-file-wins — #8).
/// </summary>
public sealed class ImportCommandTests
{
    // --- ApplyImportSeed: upsert gating (#6, #7) -----------------------------

    [Fact]
    public void ApplyImportSeed_NewEntry_FullySeededFromCard()
    {
        Card card = MakeCard(oracleId: "o1", id: "printing-1", name: "Bolt", rating: 4, xmpLabel: "Green");
        CardMetadataEntry entry = new();

        ImportCommand.ApplyImportSeed(entry, card, isNew: true, overwrite: false);

        Assert.Equal("Bolt", entry.Name);
        Assert.Equal("printing-1", entry.ScryfallId);
        Assert.Equal("Green", entry.Label);
        Assert.Equal(4, entry.Rating);
    }

    [Fact]
    public void ApplyImportSeed_ExistingEntry_NoOverwrite_PreservesRatingScryfallIdAndLabel()
    {
        // A tagger-authored entry: high rating, a pinned printing, its own label.
        CardMetadataEntry entry = new()
        {
            Name = "Bolt",
            Rating = 5,
            ScryfallId = "tagger-pinned-printing",
            Label = "tagger-label",
        };
        // The bulk match resolved a DIFFERENT printing and carries XMP 0 / no label.
        Card card = MakeCard(oracleId: "o1", id: "some-other-printing", name: "Bolt", rating: 0, xmpLabel: "");

        ImportCommand.ApplyImportSeed(entry, card, isNew: false, overwrite: false);

        Assert.Equal(5, entry.Rating);                          // not demoted
        Assert.Equal("tagger-pinned-printing", entry.ScryfallId); // #6: not repointed
        Assert.Equal("tagger-label", entry.Label);              // not touched
    }

    [Fact]
    public void ApplyImportSeed_ExistingEntry_EmptyScryfallId_IsFilledEvenWithoutOverwrite()
    {
        CardMetadataEntry entry = new() { Name = "Bolt", Rating = 3, ScryfallId = null };
        Card card = MakeCard(oracleId: "o1", id: "printing-1", name: "Bolt", rating: 0, xmpLabel: "");

        ImportCommand.ApplyImportSeed(entry, card, isNew: false, overwrite: false);

        Assert.Equal("printing-1", entry.ScryfallId); // filled because it was empty
        Assert.Equal(3, entry.Rating);                // still not touched w/o overwrite
    }

    [Fact]
    public void ApplyImportSeed_Overwrite_XmpZero_DoesNotDemoteStoredRating()
    {
        // #7: --overwrite is meant to refresh labels; an XMP 0 (nobody rates in
        // Bridge anymore) must never wipe a tagger pool rating to unrated.
        CardMetadataEntry entry = new() { Name = "Bolt", Rating = 5, Label = "old" };
        Card card = MakeCard(oracleId: "o1", id: "printing-1", name: "Bolt", rating: 0, xmpLabel: "Green");

        ImportCommand.ApplyImportSeed(entry, card, isNew: false, overwrite: true);

        Assert.Equal(5, entry.Rating);     // preserved, NOT demoted to 0
        Assert.Equal("Green", entry.Label); // label still refreshed under overwrite
    }

    [Fact]
    public void ApplyImportSeed_Overwrite_RealXmpRating_RefreshesRating()
    {
        CardMetadataEntry entry = new() { Name = "Bolt", Rating = 3 };
        Card card = MakeCard(oracleId: "o1", id: "printing-1", name: "Bolt", rating: 5, xmpLabel: "");

        ImportCommand.ApplyImportSeed(entry, card, isNew: false, overwrite: true);

        Assert.Equal(5, entry.Rating);
    }

    [Fact]
    public void ApplyImportSeed_ExplicitStatus_NeverOverwritten_EvenWithOverwrite()
    {
        CardMetadataEntry entry = new() { Name = "Bolt", Rating = 5, Status = "Banned" };
        // XMP "Red" label would default to Banned anyway, but the point is the
        // existing explicit status must be left exactly as the tagger set it.
        Card card = MakeCard(oracleId: "o1", id: "printing-1", name: "Bolt", rating: 5, xmpLabel: "Green");

        ImportCommand.ApplyImportSeed(entry, card, isNew: false, overwrite: true);

        Assert.Equal("Banned", entry.Status);
    }

    // --- ReduceByOracle: reprint collapsing (#8) -----------------------------

    [Fact]
    public void ReduceByOracle_TwoFilesSameOracle_KeepsHighestRating_NotLastFileWins()
    {
        // Reprints share ONE Card object (bulk is keyed by name). File A carries
        // rating 5, file B carries rating 0; last-file-wins would seed 0.
        Card shared = MakeCard(oracleId: "o1", id: "p1", name: "Bolt", rating: 0, xmpLabel: "");
        List<(Card, string, int, string)> perFile =
        [
            (shared, "A.png", 5, "Green"),
            (shared, "B.png", 0, ""),
        ];

        List<(Card Card, string FilePath)> result = ImportCommand.ReduceByOracle(perFile);

        (Card Card, string FilePath) reduced = Assert.Single(result);
        Assert.Equal(5, reduced.Card.Rating);          // strongest evaluation kept
        Assert.Equal("Green", reduced.Card.XmpLabel);
    }

    [Fact]
    public void ReduceByOracle_TieOnRating_PrefersFileWithLabel()
    {
        Card shared = MakeCard(oracleId: "o1", id: "p1", name: "Bolt", rating: 0, xmpLabel: "");
        List<(Card, string, int, string)> perFile =
        [
            (shared, "A.png", 3, ""),
            (shared, "B.png", 3, "Red"),
        ];

        List<(Card Card, string FilePath)> result = ImportCommand.ReduceByOracle(perFile);

        (Card Card, string FilePath) reduced = Assert.Single(result);
        Assert.Equal(3, reduced.Card.Rating);
        Assert.Equal("Red", reduced.Card.XmpLabel);
    }

    [Fact]
    public void ReduceByOracle_DistinctOracles_AllKept()
    {
        Card a = MakeCard(oracleId: "o1", id: "p1", name: "Bolt", rating: 5, xmpLabel: "");
        Card b = MakeCard(oracleId: "o2", id: "p2", name: "Counterspell", rating: 4, xmpLabel: "");
        List<(Card, string, int, string)> perFile =
        [
            (a, "A.png", 5, ""),
            (b, "B.png", 4, ""),
        ];

        List<(Card Card, string FilePath)> result = ImportCommand.ReduceByOracle(perFile);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ReduceByOracle_CardsWithoutOracleId_PassThroughUnreduced()
    {
        // Two DIFFERENT cards with no oracle_id can't be keyed/deduped — both must
        // survive (they are skipped later when the entry is written).
        Card a = MakeCard(oracleId: "", id: "p1", name: "Token A", rating: 0, xmpLabel: "");
        Card b = MakeCard(oracleId: "", id: "p2", name: "Token B", rating: 0, xmpLabel: "");
        List<(Card, string, int, string)> perFile =
        [
            (a, "A.png", 2, ""),
            (b, "B.png", 3, ""),
        ];

        List<(Card Card, string FilePath)> result = ImportCommand.ReduceByOracle(perFile);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, a.Rating); // stamped from its own file
        Assert.Equal(3, b.Rating);
    }

    // --- helper --------------------------------------------------------------

    private static Card MakeCard(string oracleId, string id, string name, int rating, string xmpLabel)
    {
        JsonCard json = new()
        {
            Name = name,
            Id = id,
            OracleId = oracleId,
            Language = "en",
            ReleasedAt = "1993-08-05",
            Layout = "normal",
            TypeLine = "Instant",
            Games = ["paper"],
            FrameEffects = [],
            Set = "LEA",
            SetName = "Alpha",
            SetType = "core",
            BorderColor = "black",
            Cmc = 1,
            Colors = [],
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.XmpLabel = xmpLabel;
        return card;
    }
}
