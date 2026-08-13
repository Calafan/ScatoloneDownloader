using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Output-contract coverage for <see cref="CardAnalyzer"/>. It runs the
/// analyzer on a small hand-crafted card set, writes the report to a temp
/// file, and asserts the headers, totals, percentages, CMC distribution, and
/// the multicolor color-distribution block. This pins the analysis output
/// shape so refactors of the analyzer do not silently reformat the stats
/// file consumed by the <c>files</c>/<c>analyze</c> commands.
/// </summary>
public sealed class CardAnalyzerTests : IDisposable
{
    private readonly string tempFile;

    public CardAnalyzerTests()
    {
        tempFile = Path.Combine(Path.GetTempPath(), "cardanalyzer_" + Guid.NewGuid().ToString("N") + ".txt");
    }

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SaveAnalysis_EmptyCardList_StillWritesHeaderSection_WithZeroTotals()
    {
        CardAnalyzer analyzer = new([]);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        Assert.Contains("Cards: 0 - Permanents: 0 (0%) Spells: 0 (0%)", text);
        Assert.Contains("Creatures", text);
        Assert.Contains("Lands", text);
        Assert.Contains("Artifacts", text);
        Assert.Contains("Enchantments", text);
        Assert.Contains("Planeswalkers", text);
        Assert.Contains("Instants", text);
        Assert.Contains("Sorceries", text);
        Assert.Contains("CMC distribution:", text);
        Assert.Contains("Average CMC: 0", text);
    }

    [Fact]
    public void SaveAnalysis_MixedCards_ComputesPerColorAndGlobalSections()
    {
        List<Card> cards =
        [
            Card("Lightning Bolt", colors: ["R"], typeLine: "Instant", cmc: 1),
            Card("Shock", colors: ["R"], typeLine: "Instant", cmc: 1),
            Card("Counterspell", colors: ["U"], typeLine: "Instant", cmc: 2),
            Card("Serra Angel", colors: ["W"], typeLine: "Creature — Angel", cmc: 5),
            Card("Sol Ring", colors: [], typeLine: "Artifact", cmc: 1),
            Card("Forest", colors: [], typeLine: "Basic Land — Forest"), // basic land skipped
            Card("Boros Charm", colors: ["R", "W"], typeLine: "Instant", cmc: 2),
        ];

        CardAnalyzer analyzer = new(cards);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // Top-level totals: 6 non-basic, non-tag cards.
        Assert.Contains("Cards: 6", text);
        // Lightning Bolt, Shock, Counterspell, Boros Charm = 4 instants; 0 sorceries.
        Assert.Contains("Instants", text);
        Assert.Contains("Sorceries", text);
        // 1 creature (Serra Angel), 1 artifact (Sol Ring) → 2 permanents.
        Assert.Contains("Permanents: 2", text);
        // 4 spells (4 instants) → spells.
        Assert.Contains("Spells: 4", text);

        // Color sections print printable names.
        Assert.Contains("Red", text);
        Assert.Contains("Blue", text);
        Assert.Contains("White", text);
        Assert.Contains("Multicolor", text);
        Assert.Contains("Colorless", text);

        // Multicolor distribution: Boros Charm = R+W → both get +1.
        Assert.Contains("Red:\t1", text);
        Assert.Contains("White:\t1", text);
        Assert.Contains("Color distribution:", text);

        // Average CMC global = (1+1+2+5+1+2)/6 = 12/6 = 2.
        Assert.Contains("Average CMC: 2", text);
    }

    [Fact]
    public void SaveAnalysis_SkipsBasicLands_AndCardsWithTag()
    {
        Card basicLand = Card("Plains", colors: ["W"], typeLine: "Basic Land — Plains");
        Card tagged = Card("Sol Ring", colors: [], typeLine: "Artifact");
        tagged.Tag = "artifacts";

        CardAnalyzer analyzer = new([basicLand, tagged]);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // Both should be excluded from the count, so totals are zero.
        Assert.Contains("Cards: 0 - Permanents: 0 (0%) Spells: 0 (0%)", text);
    }

    [Fact]
    public void SaveAnalysis_CmcDistribution_OrderedAscending()
    {
        List<Card> cards =
        [
            Card("Bolt", colors: ["R"], typeLine: "Instant", cmc: 4),
            Card("Bolt", colors: ["R"], typeLine: "Instant", cmc: 1),
            Card("Bolt", colors: ["R"], typeLine: "Instant", cmc: 2),
            Card("Bolt", colors: ["R"], typeLine: "Instant", cmc: 8),
        ];

        CardAnalyzer analyzer = new(cards);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // The "CMC distribution:" lines follow the card's section header. We only
        // need to verify that each CMC value is present and CMC values appear in
        // ascending order in the global section.
        int i1 = text.IndexOf("CMC distribution:");
        Assert.True(i1 >= 0);
        // Find the CMC keys' positions; assert ascending by their numeric order.
        int i4 = text.IndexOf("\t4:\t", i1, StringComparison.Ordinal);
        int i1line = text.IndexOf("\t1:\t", i1, StringComparison.Ordinal);
        int i2 = text.IndexOf("\t2:\t", i1, StringComparison.Ordinal);
        int i8 = text.IndexOf("\t8:\t", i1, StringComparison.Ordinal);

        Assert.True(i1line >= 0 && i2 >= 0 && i4 >= 0 && i8 >= 0);
        Assert.True(i1line < i2);
        Assert.True(i2 < i4);
        Assert.True(i4 < i8);
    }

    [Fact]
    public void SaveAnalysis_LandType_GoesToColorless()
    {
        // A non-basic land with no colors is bucketed as "Colorless" (see ctor).
        Card dualPathLand = Card("Burst Habitat", colors: [], typeLine: "Land", cmc: 0);

        CardAnalyzer analyzer = new([dualPathLand]);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // The card is a land → colorless land bucket; total is 1 in colorless.
        Assert.Contains("Colorless", text);
        // A land counts as a "land" type entry; pluralization is "Lands".
        Assert.Contains("Lands\t", text);
    }

    // --- factory -----------------------------------------------------------

    private static Card Card(
        string name,
        List<string>? colors,
        string typeLine,
        double cmc = 0)
    {
        JsonCard json = new()
        {
            Name = name,
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
            Cmc = cmc,
            Colors = colors ?? [],
            ImageUris = new JsonImageUris { Png = "https://test/img.png" },
        };

        return ScatoloneDownloader.Mtg.Card.CreateCard(json);
    }
}