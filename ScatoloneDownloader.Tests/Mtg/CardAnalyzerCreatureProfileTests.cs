using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// The creature profile of <see cref="CardAnalyzer.Analyze"/> and section 8 of the
/// Markdown report, over one hand-built pool: a 2-mana 2/2 flyer, a 2-mana 1/1, a
/// 3-mana 3/3 haste, a 6-mana */* defender, a 7-mana 5/5 (W/U), plus a non-creature
/// 2-drop and a land, which must stay out.
/// </summary>
public sealed class CardAnalyzerCreatureProfileTests : IDisposable
{
    private readonly string tempFile = Path.Combine(Path.GetTempPath(), $"creature_{Guid.NewGuid():N}.md");

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Counts_CoverOnlyTheNonLandCreatures()
    {
        AnalysisReport report = new CardAnalyzer(Pool()).Analyze();

        Assert.Equal(6, report.TotalCards);
        Assert.Equal(5, report.CreatureCount);
        Assert.Equal(2, report.CreaturesMv2);
        Assert.Equal(1, report.AggressiveTwoDrops);
        Assert.Equal(1, report.MvThreePowerThree);
    }

    // A variable power counts as 0 in the thresholds, as in docs/cube-targets.md: a
    // cheap */* (Tarmogoyf-like) is a creature of its cost, never an aggressive one.
    [Fact]
    public void Thresholds_CountAVariablePowerAsZero()
    {
        AnalysisReport report = new CardAnalyzer(
        [
            MakeCard("Goyf", ["G"], "Creature — Lhurgoyf", 2, "*", "1+*"),
            MakeCard("Elemental", ["G"], "Creature — Elemental", 3, "*", "*"),
        ]).Analyze();

        Assert.Equal(2, report.CreatureCount);
        Assert.Equal(1, report.CreaturesMv2);
        Assert.Equal(0, report.AggressiveTwoDrops);
        Assert.Equal(0, report.MvThreePowerThree);
    }

    [Fact]
    public void ByCost_SumsNumericStatsOnly_AndSixPlusHoldsBothBigOnes()
    {
        AnalysisReport report = new CardAnalyzer(Pool()).Analyze();

        Assert.Equal(CostBucket.All, report.CreatureByCost.Keys);

        CreatureCostRow two = report.CreatureByCost["2"];
        Assert.Equal(2, two.Creatures);
        Assert.Equal((3, 2), (two.PowerSum, two.PowerCount));
        Assert.Equal((3, 2), (two.ToughnessSum, two.ToughnessCount));
        Assert.Equal((1, 0, 0), (two.Evasive, two.Haste, two.Defender));

        CreatureCostRow three = report.CreatureByCost["3"];
        Assert.Equal(1, three.Creatures);
        Assert.Equal((3, 1), (three.PowerSum, three.PowerCount));
        Assert.Equal((0, 1, 0), (three.Evasive, three.Haste, three.Defender));

        // The */* defender adds a creature and a defender but no power or toughness.
        CreatureCostRow six = report.CreatureByCost["6_Plus"];
        Assert.Equal(2, six.Creatures);
        Assert.Equal((5, 1), (six.PowerSum, six.PowerCount));
        Assert.Equal((5, 1), (six.ToughnessSum, six.ToughnessCount));
        Assert.Equal((0, 0, 1), (six.Evasive, six.Haste, six.Defender));

        foreach (string empty in new[] { "0", "1", "4", "5" })
        {
            Assert.Equal(0, report.CreatureByCost[empty].Creatures);
        }
    }

    [Fact]
    public void CurveByColumn_PutsAGoldCreatureInMultiOnce()
    {
        AnalysisReport report = new CardAnalyzer(Pool()).Analyze();

        Assert.Equal(1, report.CreatureCurveByColumn["W"]["2"]);
        Assert.Equal(1, report.CreatureCurveByColumn["G"]["2"]);
        Assert.Equal(1, report.CreatureCurveByColumn["R"]["3"]);
        Assert.Equal(1, report.CreatureCurveByColumn["Colorless"]["6_Plus"]);
        Assert.Equal(1, report.CreatureCurveByColumn["Multi"]["6_Plus"]);
        Assert.False(report.CreatureCurveByColumn.ContainsKey("U"));
        Assert.Equal(5, report.CreatureCurveByColumn.Values.Sum(c => c.Values.Sum()));
    }

    [Fact]
    public void CurveByIdentity_CountsAGoldCreatureInEachOfItsColours()
    {
        AnalysisReport report = new CardAnalyzer(Pool()).Analyze();

        Assert.Equal(1, report.CreatureCurveByIdentity["W"]["2"]);
        Assert.Equal(1, report.CreatureCurveByIdentity["W"]["6_Plus"]);
        Assert.Equal(1, report.CreatureCurveByIdentity["U"]["6_Plus"]);
        Assert.Equal(1, report.CreatureCurveByIdentity["Colorless"]["6_Plus"]);
        Assert.False(report.CreatureCurveByIdentity.ContainsKey("Multi"));
        Assert.Equal(6, report.CreatureCurveByIdentity.Values.Sum(c => c.Values.Sum()));
    }

    [Fact]
    public void AnalyzeBench_GetsTheCreatureProfileToo()
    {
        List<Card> cards =
        [
            MakeCard("Pool Bear", ["G"], "Creature — Bear", 2, "2", "2", rating: 4),
            MakeCard("Bench Bear", ["G"], "Creature — Bear", 3, "3", "3", rating: 2),
        ];

        CardAnalyzer analyzer = CardAnalyzer.ForPool(cards);

        Assert.Equal(1, analyzer.Analyze().CreatureCount);
        Assert.Equal(1, analyzer.AnalyzeBench().CreatureCount);
        Assert.Equal(1, analyzer.AnalyzeBench().MvThreePowerThree);
    }

    [Fact]
    public void SaveAnalysis_RendersSectionEightAfterSectionSeven_WithTheExpectedNumbers()
    {
        List<Card> cards = Pool();
        cards.Add(MakeCard("Bench Bear", ["G"], "Creature — Bear", 2, "2", "2", rating: 2));

        CardAnalyzer.ForPool(cards).SaveAnalysis(tempFile);
        string text = File.ReadAllText(tempFile);

        int seven = text.IndexOf("## 7. Bench Availability", StringComparison.Ordinal);
        int eight = text.IndexOf("## 8. Creature Profile (Pool)", StringComparison.Ordinal);
        Assert.True(seven >= 0);
        Assert.True(eight > seven);

        Assert.Contains("**Creatures:** 5 (83.3% of non-land) | **MV ≤ 2:** 2 (33.3%) | **Aggressive 2-drops (MV ≤ 2, power ≥ 2):** 1 (16.7%) | **MV ≤ 3 with power ≥ 3:** 1 (16.7%) | **Evasive:** 1 (16.7%) | **Haste:** 1 (16.7%) | **Defender:** 1 (16.7%)", text);

        Assert.Contains("| Cost | Creatures | % of creatures | Avg power | Avg toughness | Evasive | Haste | Defender |", text);
        Assert.Contains("| 0 | 0 | 0.0% | – | – | 0 | 0 | 0 |", text);
        Assert.Contains("| 2 | 2 | 40.0% | 1.5 | 1.5 | 1 | 0 | 0 |", text);
        Assert.Contains("| 3 | 1 | 20.0% | 3.0 | 3.0 | 0 | 1 | 0 |", text);
        Assert.Contains("| 6+ | 2 | 40.0% | 5.0 | 5.0 | 0 | 0 | 1 |", text);
        Assert.Contains("| **Total** | 5 | 100.0% | 2.8 | 2.8 | 1 | 1 | 1 |", text);

        Assert.Contains("| Cost | White | Blue | Black | Red | Green | Multi | Colorless | Total |", text);
        Assert.Contains("| 2 | 1 | - | - | - | 1 | - | - | 2 |", text);
        Assert.Contains("| 6+ | - | - | - | - | - | 1 | 1 | 2 |", text);
        Assert.Contains("| **Total** | 1 | - | - | 1 | 1 | 1 | 1 | 5 |", text);

        // The bench's own curve, labelled, and the note about variable stats.
        Assert.Contains("### Creature Curve by Color (Bench, Rating 2)", text);
        Assert.Contains("| **Total** | - | - | - | - | 1 | - | - | 1 |", text);
        Assert.Contains("is left out of the averages and counts as 0 in the power thresholds.*", text);
    }

    [Fact]
    public void SaveAnalysis_WithoutABench_RendersOnlyThePoolCurve()
    {
        new CardAnalyzer(Pool()).SaveAnalysis(tempFile);
        string text = File.ReadAllText(tempFile);

        Assert.Contains("## 8. Creature Profile (Pool)", text);
        Assert.DoesNotContain("Bench, Rating 2", text);
        Assert.DoesNotContain("## 7. Bench Availability", text);
    }

    internal static List<Card> Pool() =>
    [
        MakeCard("Flyer", ["W"], "Creature — Bird", 2, "2", "2", ["Flying"]),
        MakeCard("Mouse", ["G"], "Creature — Mouse", 2, "1", "1"),
        MakeCard("Hasty", ["R"], "Creature — Goblin", 3, "3", "3", ["Haste"]),
        MakeCard("Wall", [], "Artifact Creature — Wall", 6, "*", "*", ["Defender"]),
        MakeCard("Big", ["W", "U"], "Creature — Sphinx", 7, "5", "5"),
        MakeCard("Cantrip", ["U"], "Instant", 2, "", ""),
        MakeCard("Dual", [], "Land", 0, "", ""),
    ];

    internal static Card MakeCard(
        string name,
        string[] colors,
        string typeLine,
        double cmc,
        string power,
        string toughness,
        List<string>? keywords = null,
        int rating = 4)
    {
        JsonCard json = new()
        {
            Name = name,
            OracleId = Guid.NewGuid().ToString(),
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = typeLine,
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = cmc,
            Power = power.Length == 0 ? null : power,
            Toughness = toughness.Length == 0 ? null : toughness,
            Keywords = keywords ?? [],
            ColorIdentity = [.. colors],
            ManaCost = colors.Length == 0 ? "{2}" : "{1}{" + colors[0] + "}",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.Status = CardStatus.None;
        return card;
    }
}
