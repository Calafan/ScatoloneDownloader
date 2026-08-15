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
        // 4 MacroType rows (not 7 type-strings anymore).
        Assert.Contains("Creatures", text);
        Assert.Contains("Lands", text);
        Assert.Contains("OtherPermanents", text);
        Assert.Contains("Spells", text);
        Assert.Contains("CMC distribution:", text);
        Assert.Contains("Average CMC: 0", text);
    }

    [Fact]
    public void SaveAnalysis_MixedCards_ComputesPerColorAndGlobalSections()
    {
        List<Card> cards =
        [
            Card("Lightning Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            Card("Shock", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            Card("Counterspell", colors: ["U"], colorIdentity: ["U"], typeLine: "Instant", cmc: 2),
            Card("Serra Angel", colors: ["W"], colorIdentity: ["W"], typeLine: "Creature — Angel", cmc: 5),
            Card("Sol Ring", colors: [], colorIdentity: [], typeLine: "Artifact", cmc: 1),
            Card("Forest", colors: [], colorIdentity: [], typeLine: "Basic Land — Forest"), // basic land skipped
            Card("Boros Charm", colors: ["R", "W"], colorIdentity: ["R", "W"], typeLine: "Instant", cmc: 2),
        ];

        CardAnalyzer analyzer = new(cards);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // Top-level totals: 6 non-basic, non-tag cards.
        Assert.Contains("Cards: 6", text);
        // 4 instants → Spells=4; 1 creature + 1 artifact → Permanents=2.
        Assert.Contains("Permanents: 2", text);
        Assert.Contains("Spells: 4", text);

        // ColorCategory sections use Guild/Color printable names.
        Assert.Contains("Red", text);
        Assert.Contains("Blue", text);
        Assert.Contains("White", text);
        Assert.Contains("Boros", text);      // RW guild
        Assert.Contains("Colorless", text);

        // Average CMC global = (1+1+2+5+1+2)/6 = 12/6 = 2.
        Assert.Contains("Average CMC: 2", text);
    }

    [Fact]
    public void SaveAnalysis_ColorDistributionSection_AtTop_WithCountsAndPercentages()
    {
        List<Card> cards =
        [
            Card("Bolt1", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant"),
            Card("Bolt2", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant"),
            Card("Bear", colors: ["G"], colorIdentity: ["G"], typeLine: "Creature"),
            Card("Boros Charm", colors: ["R", "W"], colorIdentity: ["R", "W"], typeLine: "Instant"),
            Card("Sol Ring", colors: [], colorIdentity: [], typeLine: "Artifact"),
            Card("Savai Triome", colors: [], colorIdentity: ["W", "B", "R"], typeLine: "Land"),
        ];

        CardAnalyzer analyzer = new(cards);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // The color distribution section comes FIRST, before the global stats.
        int distIdx = text.IndexOf("Color distribution");
        Assert.True(distIdx >= 0, "should have a 'Color distribution' header section");

        int globalIdx = text.IndexOf("Cards: 6");
        Assert.True(globalIdx > distIdx, "color distribution should appear before the global stats section");

        // 6 cards total. Multicolor cards count in ALL their colors.
        // Boros Charm (R+W): +1 R, +1 W.  Savai Triome is a Land — separate.
        // R: Bolt1 + Bolt2 + Boros Charm = 3 (50%)
        // W: Boros Charm = 1 (17%)
        // G: Bear = 1 (17%)
        // Colorless: Sol Ring = 1 (17%)
        // Lands: Savai Triome = 1 (17%)
        Assert.Contains("Red:\t3 (50%)", text);
        Assert.Contains("Green:\t1 (17%)", text);
        Assert.Contains("White:\t1 (17%)", text);
        Assert.Contains("Colorless:\t1 (17%)", text);
        Assert.Contains("Lands:\t1 (17%)", text);
    }

    [Fact]
    public void SaveAnalysis_SkipsBasicLands_AndCardsWithTag()
    {
        Card basicLand = Card("Plains", colors: ["W"], colorIdentity: ["W"], typeLine: "Basic Land — Plains");
        Card tagged = Card("Sol Ring", colors: [], colorIdentity: [], typeLine: "Artifact");
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
            Card("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 4),
            Card("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            Card("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 2),
            Card("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 8),
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
    public void SaveAnalysis_LandsExcludedFromStats_AppearsAtBottomSection()
    {
        // A non-basic land with no color identity (Command Beacon) shouldn't be
        // double-counted in the card stats but must appear in a "Lands" block at
        // the bottom, by its ColorCategory.
        Card tower = Card("Command Beacon", colors: [], colorIdentity: [], typeLine: "Land", cmc: 0);
        Card savai = Card("Savai Triome", colors: [], colorIdentity: ["W", "B", "R"], typeLine: "Land", cmc: 0);
        Card bolt = Card("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1);

        CardAnalyzer analyzer = new([tower, savai, bolt]);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // Total cards = 1 (Bolt). Lands are NOT counted.
        Assert.Contains("Cards: 1", text);

        // Lands section at the bottom.
        int globalIdx = text.IndexOf("Cards: 1");
        int landsIdx = text.IndexOf("Lands", globalIdx);
        Assert.True(landsIdx > globalIdx, "Lands section should appear after global/per-color stats");

        // Inside lands section: Savai Triome → ColorCategory "RWB" → "Mardu".
        string landsText = text.Substring(landsIdx);
        Assert.Contains("Colorless:\t1", landsText);   // Command Beacon
        Assert.Contains("Mardu:\t1", landsText);      // Savai via RWB
    }

    [Fact]
    public void SaveAnalysis_LandType_GoesToColorless()
    {
        // A non-basic land with no colors is bucketed as "Colorless" via
        // ColorCategory (Phase 0), not via the old Colors=0 heuristic.
        Card dualPathLand = Card("Burst Habitat", colors: [], colorIdentity: [], typeLine: "Land", cmc: 0);

        CardAnalyzer analyzer = new([dualPathLand]);

        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // Total cards = 0 (lands excluded from stats).
        Assert.Contains("Cards: 0 - Permanents: 0 (0%) Spells: 0 (0%)", text);
        // Lands section present at bottom with the land.
        Assert.Contains("Lands", text);
        Assert.Contains("Colorless:\t1", text);
    }

    // --- factory -----------------------------------------------------------

    private static Card Card(
        string name,
        List<string>? colors,
        List<string>? colorIdentity,
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
            ColorIdentity = colorIdentity ?? [],
            ImageUris = new JsonImageUris { Png = "https://test/img.png" },
        };

        return ScatoloneDownloader.Mtg.Card.CreateCard(json);
    }
}