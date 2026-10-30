using System.Text.Json;
using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Tests.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The overview inside the <c>/stats</c> payload (global distribution, category
/// rows and cost tables of the pool), over one hand-built pool: three white cards
/// (a 2.5 and a 7.5 creature, a 1-cost instant), a blue creature, a W/U instant, a
/// colourless artifact, three lands (green, B/R, colourless), plus a rating-2 and a
/// Banned white creature that must stay out.
/// </summary>
public sealed class StatsPayloadOverviewTests
{
    private static List<Card> Pool()
    {
        Card banned = CardAnalyzerCreatureProfileTests.MakeCard("Banned", ["W"], "Creature — Human", 2, "2", "2", rating: 5);
        banned.Status = CardStatus.Banned;

        return
        [
            CardAnalyzerCreatureProfileTests.MakeCard("Half", ["W"], "Creature — Human", 2.5, "2", "2", rating: 3),
            CardAnalyzerCreatureProfileTests.MakeCard("Giant", ["W"], "Creature — Giant", 7.5, "7", "7", rating: 5),
            CardAnalyzerCreatureProfileTests.MakeCard("Bolt", ["W"], "Instant", 1, "", "", rating: 4),
            CardAnalyzerCreatureProfileTests.MakeCard("Merfolk", ["U"], "Creature — Merfolk", 3, "2", "2", rating: 4),
            CardAnalyzerCreatureProfileTests.MakeCard("Charm", ["W", "U"], "Instant", 3, "", "", rating: 5),
            CardAnalyzerCreatureProfileTests.MakeCard("Relic", [], "Artifact", 6, "", "", rating: 5),
            CardAnalyzerCreatureProfileTests.MakeCard("Forest", ["G"], "Land", 0, "", "", rating: 4),
            CardAnalyzerCreatureProfileTests.MakeCard("Badlands", ["B", "R"], "Land", 0, "", "", rating: 4),
            CardAnalyzerCreatureProfileTests.MakeCard("Wastes", [], "Land", 0, "", "", rating: 4),
            CardAnalyzerCreatureProfileTests.MakeCard("Bench", ["W"], "Creature — Human", 2, "2", "2", rating: 2),
            banned,
        ];
    }

    private static StatsOverview Overview() => StatsPayload.Build(Pool()).Overview;

    private static StatsCategoryRow Row(StatsOverview o, string key) =>
        o.Groups.SelectMany(g => g.Rows).Single(r => r.Key == key);

    [Fact]
    public void Counts_CoverOnlyThePool_AndLandsAreCountedApart()
    {
        StatsOverview o = Overview();

        Assert.Equal(6, o.NonLand);
        Assert.Equal(3, o.Lands);
        Assert.Equal(2, o.Spells);
        Assert.Equal(4, o.Permanents);
        Assert.Equal(23.0 / 6, o.AverageMv, 6);

        // The rating-2 and the Banned white creatures are not in the white row.
        Assert.Equal(3, Row(o, "W").Cards);
    }

    [Fact]
    public void Types_AreInOrder_WithLabelsAndTheTaggerTypeValue()
    {
        StatsOverview o = Overview();

        Assert.Equal(["Creature", "OtherPermanent", "Spell"], o.Types.Select(t => t.Macro));
        Assert.Equal(["Creatures", "Other permanents", "Spells"], o.Types.Select(t => t.Label));
        Assert.Equal([3, 1, 2], o.Types.Select(t => t.Cards));
    }

    [Fact]
    public void Cost_FoldsFractionsDown_AndSixOrMoreIntoSixPlus_EveryBucketPresent()
    {
        StatsOverview o = Overview();

        StatsTypeRow creatures = o.Types[0];
        Assert.Equal(CostBucket.All, creatures.ByCost.Keys);
        Assert.Equal(1, creatures.ByCost["2"]);
        Assert.Equal(1, creatures.ByCost["3"]);
        Assert.Equal(1, creatures.ByCost["6_Plus"]);
        Assert.Equal(0, creatures.ByCost["0"]);

        Assert.Equal(CostBucket.All, o.CurveTotal.Keys);
        Assert.Equal([0, 1, 1, 2, 0, 0, 2], CostBucket.All.Select(b => o.CurveTotal[b]));

        // The same folding for the white row: 2.5 -> "2", 7.5 -> "6_Plus".
        StatsCategoryRow white = Row(o, "W");
        Assert.Equal(CostBucket.All, white.ByCost.Keys);
        Assert.Equal([0, 1, 1, 0, 0, 0, 1], CostBucket.All.Select(b => white.ByCost[b]));
        Assert.Equal([0, 0, 1, 0, 0, 0, 1], CostBucket.All.Select(b => white.CreatureByCost[b]));
    }

    [Fact]
    public void Lands_AreCountedInLandsOnly_AndInNoCost()
    {
        StatsOverview o = Overview();

        Assert.Equal(1, Row(o, "G").Lands);
        Assert.Equal(1, Row(o, "BR").Lands);
        Assert.Equal(1, Row(o, "Colorless").Lands);
        Assert.Equal(0, Row(o, "W").Lands);

        Assert.Equal(6, o.CurveTotal.Values.Sum());
        Assert.Equal(6, o.Groups.SelectMany(g => g.Rows).Sum(r => r.ByCost.Values.Sum()));
        Assert.Equal(3, o.Total.Lands);
    }

    [Fact]
    public void ACategoryWithOnlyALand_GetsARow_AndItsGroupAppears()
    {
        StatsOverview o = Overview();

        StatsCategoryRow green = Row(o, "G");
        Assert.Equal(0, green.Cards);
        Assert.Equal(1, green.Lands);
        Assert.Null(green.CreatureAverageMv);
        Assert.Equal("1 White", Row(o, "W").ColorParam);
        Assert.Equal("Green", green.Name);

        StatsCategoryRow dimirLike = Row(o, "BR");
        Assert.Equal(0, dimirLike.Cards);
        Assert.Contains(o.Groups, g => g.Label == "Guilds (2-Color)" && g.Rows.Any(r => r.Key == "BR"));
    }

    [Fact]
    public void ACategoryWithNoCardsAtAll_HasNoRow_AndAnEmptyGroupIsLeftOut()
    {
        StatsOverview o = Overview();
        string[] keys = o.Groups.SelectMany(g => g.Rows).Select(r => r.Key).ToArray();

        Assert.DoesNotContain("R", keys);
        Assert.DoesNotContain("B", keys);
        Assert.Equal(["W", "U", "G", "WU", "BR", "Colorless"], keys);

        // No shard or wedge in the pool: its group is not there.
        Assert.Equal(["Monocolor", "Guilds (2-Color)", "4-5 Colors & Colorless"], o.Groups.Select(g => g.Label));
        Assert.All(o.Groups, g => Assert.NotEmpty(g.Rows));
    }

    [Fact]
    public void Ratings_AreCountedPerCategory_AndSummedInTheTotal()
    {
        StatsOverview o = Overview();

        StatsCategoryRow white = Row(o, "W");
        Assert.Equal((1, 1, 1), (white.Rated3, white.Rated4, white.Rated5));
        StatsCategoryRow colorless = Row(o, "Colorless");
        Assert.Equal((0, 0, 1), (colorless.Rated3, colorless.Rated4, colorless.Rated5));

        Assert.Equal((1, 2, 3), (o.Total.Rated3, o.Total.Rated4, o.Total.Rated5));
    }

    [Fact]
    public void CreatureAverage_IsNullWithoutCreatures_AndTheTotalIsOverAllCreatures()
    {
        StatsOverview o = Overview();

        Assert.Equal(5.0, Row(o, "W").CreatureAverageMv);
        Assert.Equal(3.0, Row(o, "U").CreatureAverageMv);
        Assert.Null(Row(o, "WU").CreatureAverageMv);
        Assert.Null(Row(o, "Colorless").CreatureAverageMv);

        // (2.5 + 7.5 + 3) / 3 creatures.
        Assert.Equal(13.0 / 3, o.Total.CreatureAverageMv!.Value, 6);
    }

    [Fact]
    public void Total_RowCarriesThePoolWithNoColourFilter()
    {
        StatsOverview o = Overview();
        StatsCategoryRow total = o.Total;

        Assert.Equal(("", "Total", ""), (total.Key, total.Name, total.ColorParam));
        Assert.Equal((6, 3, 1, 2), (total.Cards, total.Creatures, total.OtherPermanents, total.Spells));
        Assert.Equal(o.AverageMv, total.AverageMv);
        Assert.Equal(o.PipDensity, total.PipDensity);
        Assert.Equal(o.CurveTotal, total.ByCost);
        Assert.Equal([0, 0, 1, 1, 0, 0, 1], CostBucket.All.Select(b => total.CreatureByCost[b]));
    }

    [Fact]
    public void Json_KeepsTheDictionaryKeysAsTheyAre()
    {
        string json = JsonSerializer.Serialize(Overview(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"curveTotal\":{\"0\":", json);
        Assert.Contains("\"6_Plus\":", json);
        Assert.Contains("\"creatureAverageMv\":null", json);
    }
}
