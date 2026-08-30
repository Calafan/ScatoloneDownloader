using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Phase 1 TDD: extended CardAnalyzer metrics (R2). Pins the new metric
/// outputs — MacroType ratios, Guild/Shard density, Rating tier distribution,
/// pip density, and CSV export — so refactors of the analyzer regress loudly.
/// The existing .txt output contract is already covered by CardAnalyzerTests.
/// </summary>
public sealed class CardAnalyzerExtendedTests : IDisposable
{
    private readonly string tempFile;

    public CardAnalyzerExtendedTests()
    {
        tempFile = Path.Combine(Path.GetTempPath(), "cardanalyzer_ext_" + Guid.NewGuid().ToString("N") + ".txt");
    }

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Analyze_MacroTypeRios_ReturnsCreatureSpellPermanentCounts()
    {
        List<Card> cards =
        [
            MakeCard("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            MakeCard("Shock", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            MakeCard("Bear", colors: ["G"], colorIdentity: ["G"], typeLine: "Creature — Bear", cmc: 2),
            MakeCard("Angel", colors: ["W"], colorIdentity: ["W"], typeLine: "Creature — Angel", cmc: 5),
            MakeCard("Sol Ring", colors: [], colorIdentity: [], typeLine: "Artifact", cmc: 1),
        ];

        CardAnalyzer analyzer = new(cards);

        AnalysisReport report = analyzer.Analyze();

        // 2 creatures, 2 spells, 1 other permanent (artifact), 0 lands.
        Assert.Equal(2, report.MacroTypeCounts[MacroType.Creature]);
        Assert.Equal(2, report.MacroTypeCounts[MacroType.Spell]);
        Assert.Equal(1, report.MacroTypeCounts[MacroType.OtherPermanent]);
        Assert.Equal(0, report.MacroTypeCounts[MacroType.Land]);
    }

    [Fact]
    public void Analyze_GuildDensity_ReturnsCountPerGuildCode()
    {
        List<Card> cards =
        [
            MakeCard("Azorius1", colors: ["W", "U"], colorIdentity: ["W", "U"], typeLine: "Instant"),
            MakeCard("Azorius2", colors: ["W", "U"], colorIdentity: ["W", "U"], typeLine: "Creature"),
            MakeCard("Boros1", colors: ["R", "W"], colorIdentity: ["R", "W"], typeLine: "Instant"),
            MakeCard("MonoR", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant"),
        ];

        CardAnalyzer analyzer = new(cards);
        AnalysisReport report = analyzer.Analyze();

        Assert.Equal(2, report.ColorCategoryCounts["WU"]);
        Assert.Equal(1, report.ColorCategoryCounts["RW"]);
        Assert.Equal(1, report.ColorCategoryCounts["R"]);
    }

    [Fact]
    public void Analyze_RatingTierDistribution_ReturnsCountPerTierPerColor()
    {
        List<Card> cards =
        [
            MakeCard("Bolt3", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", rating: 3),
            MakeCard("Bolt4", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", rating: 4),
            MakeCard("Bolt5", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", rating: 5),
            MakeCard("U3", colors: ["U"], colorIdentity: ["U"], typeLine: "Instant", rating: 3),
        ];

        CardAnalyzer analyzer = new(cards);
        AnalysisReport report = analyzer.Analyze();

        Assert.Equal(1, report.RatingTiers[("R", 3)]);
        Assert.Equal(1, report.RatingTiers[("R", 4)]);
        Assert.Equal(1, report.RatingTiers[("R", 5)]);
        Assert.Equal(1, report.RatingTiers[("U", 3)]);
    }

    [Fact]
    public void Analyze_PipDensity_ReturnsAverageColoredPipsPerColor()
    {
        List<Card> cards =
        [
            MakeCard("Bolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1, manaCost: "{R}"),       // 1 pip
            MakeCard("Shock", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1, manaCost: "{R}"),       // 1 pip
            MakeCard("FBolt", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 2, manaCost: "{1}{R}"),  // 1 pip
            MakeCard("Dragon", colors: ["R"], colorIdentity: ["R"], typeLine: "Creature", cmc: 4, manaCost: "{2}{R}{R}"), // 2 pips
        ];

        CardAnalyzer analyzer = new(cards);
        AnalysisReport report = analyzer.Analyze();

        // 4 cards, total pips = 1+1+1+2 = 5, average = 1.25
        Assert.Equal(1.25, report.GlobalPipDensity);
    }

    [Fact]
    public void Analyze_CurveByMacroType_ReturnsCmcBucketsForCreatureAndSpell()
    {
        List<Card> cards =
        [
            MakeCard("C1", colors: ["R"], colorIdentity: ["R"], typeLine: "Creature", cmc: 1),
            MakeCard("C2", colors: ["R"], colorIdentity: ["R"], typeLine: "Creature", cmc: 2),
            MakeCard("C6", colors: ["R"], colorIdentity: ["R"], typeLine: "Creature", cmc: 8), // bucket 6+
            MakeCard("S1", colors: ["R"], colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            MakeCard("S3", colors: ["R"], colorIdentity: ["R"], typeLine: "Sorcery", cmc: 3),
        ];

        CardAnalyzer analyzer = new(cards);
        AnalysisReport report = analyzer.Analyze();

        // Creature curve: 1@1, 1@2, 1@6+, 0@3/4/5
        Assert.Equal(1, report.CurveByMacroType[MacroType.Creature][1]);
        Assert.Equal(1, report.CurveByMacroType[MacroType.Creature][2]);
        Assert.Equal(1, report.CurveByMacroType[MacroType.Creature][6]);
        Assert.False(report.CurveByMacroType[MacroType.Creature].ContainsKey(3));

        // Spell curve: 1@1, 1@3
        Assert.Equal(1, report.CurveByMacroType[MacroType.Spell][1]);
        Assert.Equal(1, report.CurveByMacroType[MacroType.Spell][3]);
    }

    // --- factory -----------------------------------------------------------

    private static Card MakeCard(
        string name,
        string set = "LEA",
        string collectorNumber = "1",
        List<string>? colors = null,
        List<string>? colorIdentity = null,
        string typeLine = "Instant",
        double cmc = 1,
        int rating = 0,
        string xmpLabel = "",
        string manaCost = "",
        string scryfallId = "")
    {
        JsonCard json = new()
        {
            Id = scryfallId,
            CollectorNumber = collectorNumber,
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
            Set = set,
            SetName = "Alpha",
            SetType = "core",
            BorderColor = "black",
            Cmc = cmc,
            Colors = colors ?? ["R"],
            ColorIdentity = colorIdentity ?? ["R"],
            ManaCost = manaCost,
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.XmpLabel = xmpLabel;
        return card;
    }
}