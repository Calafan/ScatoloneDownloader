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
/// analyzer on a small hand-crafted card set, writes the Markdown report to a
/// temp file, and asserts the section headers, totals, percentages, CMC
/// distribution, and the multicolor color-distribution block. This pins the
/// Markdown analysis output shape so refactors of the analyzer do not silently
/// reformat the report.
/// </summary>
public sealed class CardAnalyzerTests : IDisposable
{
    private readonly string tempFile;

    public CardAnalyzerTests()
    {
        tempFile = Path.Combine(Path.GetTempPath(), "cardanalyzer_" + Guid.NewGuid().ToString("N") + ".md");
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

        Assert.Contains("## 1. Global Distribution (0 Cards)", text);
        Assert.Contains("**Permanents:** 0 (0%) | **Spells:** 0 (0%)", text);
        // The 4 MacroType table rows are always rendered.
        Assert.Contains("| Creatures |", text);
        Assert.Contains("| Lands |", text);
        Assert.Contains("| OtherPermanents |", text);
        Assert.Contains("| Spells |", text);
        Assert.Contains("**Global CMC:**", text);
        Assert.Contains("**Global Average CMC:** 0", text);
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

        // Top-level totals: 6 non-basic, non-tag cards (the header counts cards + lands).
        Assert.Contains("Global Distribution (6 Cards)", text);
        // 4 instants → Spells=4; 1 creature + 1 artifact → Permanents=2.
        Assert.Contains("**Permanents:** 2", text);
        Assert.Contains("**Spells:** 4", text);

        // ColorCategory sections use Guild/Color printable names.
        Assert.Contains("Red", text);
        Assert.Contains("Blue", text);
        Assert.Contains("White", text);
        Assert.Contains("Boros", text);      // RW guild
        Assert.Contains("Colorless", text);

        // Average CMC global = (1+1+2+5+1+2)/6 = 12/6 = 2.
        Assert.Contains("**Global Average CMC:** 2", text);
    }

    [Fact]
    public void SaveAnalysis_ColorDistributionSection_AfterGlobal_WithCountsAndPercentages()
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

        // Color distribution is section 2, AFTER the global section 1.
        int globalIdx = text.IndexOf("## 1. Global Distribution");
        int distIdx = text.IndexOf("## 2. Color Distribution");
        Assert.True(globalIdx >= 0, "should have the global section");
        Assert.True(distIdx > globalIdx, "color distribution (section 2) comes after the global section (section 1)");

        // 5 non-land cards; multicolor cards count in ALL their colors; percentages
        // exclude lands from the denominator (Savai Triome is a Land — separate).
        // R: Bolt1 + Bolt2 + Boros Charm = 3 / 5 = 60%
        // W: Boros Charm = 1 / 5 = 20%   G: Bear = 1 / 5 = 20%   Colorless: Sol Ring = 1 / 5 = 20%
        Assert.Contains("| **Red** | 3 | 60% |", text);
        Assert.Contains("| **Green** | 1 | 20% |", text);
        Assert.Contains("| **White** | 1 | 20% |", text);
        Assert.Contains("| **Colorless** | 1 | 20% |", text);
        // Savai Triome (RWB land) shows in the Lands section as "Mardu".
        Assert.Contains("| Mardu | 1 |", text);
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

        // Both are excluded from the count, so the totals are zero.
        Assert.Contains("## 1. Global Distribution (0 Cards)", text);
        Assert.Contains("**Permanents:** 0 (0%) | **Spells:** 0 (0%)", text);
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

        // The Global CMC line lists buckets 0..6+ in ascending order; CMC 8 falls
        // into the "6+" bucket (values are capped at 6+, so there is no "8").
        int gidx = text.IndexOf("**Global CMC:**");
        Assert.True(gidx >= 0);
        int p1 = text.IndexOf("`1:", gidx, StringComparison.Ordinal);
        int p2 = text.IndexOf("`2:", gidx, StringComparison.Ordinal);
        int p4 = text.IndexOf("`4:", gidx, StringComparison.Ordinal);
        int p6 = text.IndexOf("`6+:", gidx, StringComparison.Ordinal);

        Assert.True(p1 >= 0 && p2 >= 0 && p4 >= 0 && p6 >= 0);
        Assert.True(p1 < p2);
        Assert.True(p2 < p4);
        Assert.True(p4 < p6);
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

        // Bolt is the only non-land card, so it is the only Spell; lands are NOT
        // counted in the per-type stats.
        Assert.Contains("**Spells:** 1", text);

        // Lands section (2 lands) appears after the global section.
        Assert.Contains("## 3. Lands Distribution (2 Cards)", text);
        int globalIdx = text.IndexOf("## 1. Global Distribution");
        int landsIdx = text.IndexOf("## 3. Lands Distribution");
        Assert.True(landsIdx > globalIdx, "Lands section should appear after global/per-color stats");

        // Inside the lands section: Command Beacon → Colorless; Savai Triome → RWB → "Mardu".
        string landsText = text.Substring(landsIdx);
        Assert.Contains("| Colorless | 1 |", landsText);
        Assert.Contains("| Mardu | 1 |", landsText);
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

        // Lands are excluded from the per-type stats (0 permanents / 0 spells).
        Assert.Contains("**Permanents:** 0 (0%) | **Spells:** 0 (0%)", text);
        // Lands section present at the bottom with the single land.
        Assert.Contains("## 3. Lands Distribution (1 Cards)", text);
        Assert.Contains("| Colorless | 1 |", text);
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
