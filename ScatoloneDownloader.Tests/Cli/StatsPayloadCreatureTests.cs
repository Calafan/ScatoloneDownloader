using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Tests.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The creature profile inside the <c>/stats</c> payload: cost rows in bucket order,
/// the Total row, the summary and the cost-by-colour curves, over the same
/// hand-built pool as <see cref="CardAnalyzerCreatureProfileTests"/>.
/// </summary>
public sealed class StatsPayloadCreatureTests
{
    [Fact]
    public void Rows_AreInBucketOrder_WithATotalRowAndNullAveragesWhereNothingIsNumeric()
    {
        StatsCreatures creatures = StatsPayload.Build(CardAnalyzerCreatureProfileTests.Pool()).Pool.Creatures;

        Assert.Equal(["0", "1", "2", "3", "4", "5", "6_Plus"], creatures.Rows.Select(r => r.Cost));
        Assert.Equal(["0", "1", "2", "3", "4", "5", "6+"], creatures.Rows.Select(r => r.Label));
        Assert.Equal("Total", creatures.Total.Label);
        Assert.Equal(creatures.Rows.Sum(r => r.Creatures), creatures.Total.Creatures);
        Assert.Equal(5, creatures.Total.Creatures);

        StatsCreatureRow zero = creatures.Rows[0];
        Assert.Null(zero.PowerAvg);
        Assert.Null(zero.ToughnessAvg);

        StatsCreatureRow two = creatures.Rows[2];
        Assert.Equal(1.5, two.PowerAvg);
        Assert.Equal(1, two.Evasive);

        StatsCreatureRow six = creatures.Rows[6];
        Assert.Equal(2, six.Creatures);
        Assert.Equal(5.0, six.PowerAvg);
        Assert.Equal(1, six.Defender);
    }

    [Fact]
    public void Summary_MatchesTheReport()
    {
        List<Card> cards = CardAnalyzerCreatureProfileTests.Pool();
        AnalysisReport report = CardAnalyzer.ForPool(cards).Analyze();

        StatsCreatureSummary summary = StatsPayload.Build(cards).Pool.Creatures.Summary;

        Assert.Equal(report.TotalCards, summary.NonLand);
        Assert.Equal(report.CreatureCount, summary.Creatures);
        Assert.Equal(report.CreaturesMv2, summary.CreaturesMv2);
        Assert.Equal(report.AggressiveTwoDrops, summary.AggressiveTwoDrops);
        Assert.Equal(report.MvThreePowerThree, summary.MvThreePowerThree);
        Assert.Equal((1, 1, 1), (summary.Evasive, summary.Haste, summary.Defender));
    }

    [Fact]
    public void Curves_CarryTheTaggerColourParams_AndColumnTotalsAreTheColumnsCreatures()
    {
        StatsTier pool = StatsPayload.Build(CardAnalyzerCreatureProfileTests.Pool()).Pool;
        StatsCreatureCurve mono = pool.Creatures.CurveMono;
        StatsCreatureCurve identity = pool.Creatures.CurveIdentity;

        Assert.Equal("*multi", mono.Columns.Single(c => c.Key == "Multi").ColorParam);
        Assert.Equal("*has:W", identity.Columns.Single(c => c.Key == "W").ColorParam);
        Assert.Equal("5 Colorless", identity.Columns.Single(c => c.Key == "Colorless").ColorParam);

        Assert.Equal(mono.Columns.Select(c => c.Key).Order(), mono.Totals.Keys.Order());
        Assert.Equal(1, mono.Totals["Multi"]);
        Assert.Equal(5, mono.Totals.Values.Sum());
        Assert.All(mono.Columns, c =>
            Assert.Equal(mono.Totals[c.Key], mono.Rows.Sum(r => r.ByColumn[c.Key])));

        Assert.Equal(2, identity.Totals["W"]);
        Assert.Equal(1, identity.Totals["U"]);
        Assert.All(identity.Columns, c =>
            Assert.Equal(identity.Totals[c.Key], identity.Rows.Sum(r => r.ByColumn[c.Key])));
    }

    [Fact]
    public void CurveRowTotals_AreDistinctCreatures_AndAddUpToTheTotalRow()
    {
        StatsTier pool = StatsPayload.Build(CardAnalyzerCreatureProfileTests.Pool()).Pool;
        StatsCreatureCurve identity = pool.Creatures.CurveIdentity;

        // The W/U sphinx is two column hits but one creature.
        StatsCurveRow sixPlus = identity.Rows.Single(r => r.Cost == "6_Plus");
        Assert.Equal(2, sixPlus.Total);
        Assert.Equal(3, sixPlus.ByColumn.Values.Sum());

        Assert.Equal(pool.Creatures.Total.Creatures, identity.Rows.Sum(r => r.Total));
        Assert.Equal(pool.Creatures.Total.Creatures, pool.Creatures.CurveMono.Rows.Sum(r => r.Total));
    }

    [Fact]
    public void Bench_HasItsOwnCreatureProfile()
    {
        List<Card> cards =
        [
            CardAnalyzerCreatureProfileTests.MakeCard("Pool Bear", ["G"], "Creature — Bear", 2, "2", "2"),
            CardAnalyzerCreatureProfileTests.MakeCard("Bench Bear", ["G"], "Creature — Bear", 4, "4", "4", rating: 2),
        ];

        StatsPayload payload = StatsPayload.Build(cards);

        Assert.Equal(1, payload.Pool.Creatures.Total.Creatures);
        Assert.Equal(1, payload.Bench!.Creatures.Total.Creatures);
        Assert.Equal(1, payload.Bench!.Creatures.CurveMono.Rows.Single(r => r.Cost == "4").ByColumn["G"]);
    }
}
