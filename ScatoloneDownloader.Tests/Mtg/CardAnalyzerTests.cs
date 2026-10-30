using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Tests.Cli;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Output-contract coverage for <see cref="CardAnalyzer"/> over a small hand-crafted
/// card set, read the way the stats files read it: the list payload
/// (<see cref="StatsPayload.BuildForList"/>) and the Markdown rendered from it. It
/// pins what basic lands, tagged cards, lands and the mana-value buckets do to the
/// totals, so refactors of the analyzer cannot silently change the report.
/// </summary>
public sealed class CardAnalyzerTests
{
    private static StatsOverview Overview(params Card[] cards) => StatsPayload.BuildForList(cards, "list").Overview;

    [Fact]
    public void EmptyCardList_StillWritesTheTypeRows_WithZeroTotals()
    {
        string md = StatsMarkdownTests.RenderList([]);

        StatsOverview o = Overview();
        Assert.Equal(0, o.NonLand);
        Assert.Equal(0, o.Permanents);
        Assert.Equal(0, o.Spells);
        Assert.Equal(0, o.AverageMv);

        string types = StatsMarkdownTests.Section(md, "mylist — global distribution");
        Assert.Contains("0 cards: 0 non-land + 0 lands · permanents 0 (0%) · spells 0 (0%)", types);
        // The three type rows, then Non-land and Lands, are always rendered.
        Assert.Contains("| Creatures |", types);
        Assert.Contains("| Other permanents |", types);
        Assert.Contains("| Spells |", types);
        Assert.Contains("| Non-land |", types);
        Assert.Contains("| Lands |", types);
    }

    [Fact]
    public void MixedCards_ComputesPerColorAndGlobalSections()
    {
        Card[] cards =
        [
            Card("Lightning Bolt", colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            Card("Shock", colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            Card("Counterspell", colorIdentity: ["U"], typeLine: "Instant", cmc: 2),
            Card("Serra Angel", colorIdentity: ["W"], typeLine: "Creature — Angel", cmc: 5),
            Card("Sol Ring", colorIdentity: [], typeLine: "Artifact", cmc: 1),
            Card("Forest", colorIdentity: [], typeLine: "Basic Land — Forest"), // basic land skipped
            Card("Boros Charm", colorIdentity: ["R", "W"], typeLine: "Instant", cmc: 2),
        ];

        StatsOverview o = Overview(cards);

        // Top-level totals: 6 non-basic, non-tag cards.
        Assert.Equal(6, o.NonLand);
        // 4 instants -> Spells=4; 1 creature + 1 artifact -> Permanents=2.
        Assert.Equal(2, o.Permanents);
        Assert.Equal(4, o.Spells);
        // Average CMC global = (1+1+2+5+1+2)/6 = 12/6 = 2.
        Assert.Equal(2, o.AverageMv);

        // The category names are the printable colour / guild names.
        string[] names = o.Groups.SelectMany(g => g.Rows).Select(r => r.Name).ToArray();
        Assert.Contains("Red", names);
        Assert.Contains("Blue", names);
        Assert.Contains("White", names);
        Assert.Contains("Boros", names); // RW guild
        Assert.Contains("Colorless", names);
    }

    [Fact]
    public void CardsPerColour_CountAGoldCardInEachOfItsColours_AndLeaveLandsToTheLandsColumn()
    {
        Card[] cards =
        [
            Card("Bolt1", colorIdentity: ["R"], typeLine: "Instant"),
            Card("Bolt2", colorIdentity: ["R"], typeLine: "Instant"),
            Card("Bear", colorIdentity: ["G"], typeLine: "Creature"),
            Card("Boros Charm", colorIdentity: ["R", "W"], typeLine: "Instant"),
            Card("Sol Ring", colorIdentity: [], typeLine: "Artifact"),
            Card("Savai Triome", colorIdentity: ["W", "B", "R"], typeLine: "Land"),
        ];

        StatsPayload payload = StatsPayload.BuildForList(cards, "list");

        // 5 non-land cards; multicolor cards count in ALL their colors; the land is
        // not in the denominator.
        // R: Bolt1 + Bolt2 + Boros Charm = 3   W: Boros Charm = 1   G: Bear = 1   Colorless: Sol Ring = 1
        IReadOnlyDictionary<string, int> perColour = payload.Pool.Identity.Cards;
        Assert.Equal(3, perColour["R"]);
        Assert.Equal(1, perColour["G"]);
        Assert.Equal(1, perColour["W"]);
        Assert.Equal(1, perColour["Colorless"]);
        Assert.Equal(5, payload.Pool.Identity.TotalCards);

        // Savai Triome (RWB land) shows in the Lands column of its category, "Mardu".
        StatsCategoryRow mardu = payload.Overview.Groups.SelectMany(g => g.Rows).Single(r => r.Name == "Mardu");
        Assert.Equal(1, mardu.Lands);
        Assert.Equal(0, mardu.Cards);
    }

    [Fact]
    public void SkipsBasicLands_AndCardsWithTag()
    {
        Card basicLand = Card("Plains", colorIdentity: ["W"], typeLine: "Basic Land — Plains");
        Card tagged = Card("Sol Ring", colorIdentity: [], typeLine: "Artifact");
        tagged.Tag = "artifacts";

        StatsOverview o = Overview(basicLand, tagged);

        // Both are excluded from the count, so the totals are zero.
        Assert.Equal(0, o.NonLand);
        Assert.Equal(0, o.Lands);
        Assert.Equal(0, o.Permanents);
        Assert.Equal(0, o.Spells);
    }

    [Fact]
    public void CmcDistribution_FoldsIntoZeroToSixPlus()
    {
        Card[] cards =
        [
            Card("Bolt", colorIdentity: ["R"], typeLine: "Instant", cmc: 4),
            Card("Bolt", colorIdentity: ["R"], typeLine: "Instant", cmc: 1),
            Card("Bolt", colorIdentity: ["R"], typeLine: "Instant", cmc: 2),
            Card("Bolt", colorIdentity: ["R"], typeLine: "Instant", cmc: 8),
        ];

        StatsOverview o = Overview(cards);

        // CMC 8 falls into the "6_Plus" bucket (values are capped at 6+, so there is no "8").
        Assert.Equal(["0", "1", "2", "3", "4", "5", "6_Plus"], o.CurveTotal.Keys);
        Assert.Equal([0, 1, 1, 0, 1, 0, 1], o.CurveTotal.Values);
    }

    [Fact]
    public void LandsAreExcludedFromTheCardStats_ButCountedUnderTheirCategory()
    {
        // A non-basic land with no color identity (Command Beacon) shouldn't be
        // double-counted in the card stats but must appear under Lands, by its
        // ColorCategory.
        Card tower = Card("Command Beacon", colorIdentity: [], typeLine: "Land", cmc: 0);
        Card savai = Card("Savai Triome", colorIdentity: ["W", "B", "R"], typeLine: "Land", cmc: 0);
        Card bolt = Card("Bolt", colorIdentity: ["R"], typeLine: "Instant", cmc: 1);

        StatsOverview o = Overview(tower, savai, bolt);

        // Bolt is the only non-land card, so it is the only Spell; lands are NOT
        // counted in the per-type stats.
        Assert.Equal(1, o.Spells);
        Assert.Equal(1, o.NonLand);
        Assert.Equal(2, o.Lands);

        // Command Beacon -> Colorless; Savai Triome -> RWB -> "Mardu".
        StatsCategoryRow[] rows = o.Groups.SelectMany(g => g.Rows).ToArray();
        Assert.Equal(1, rows.Single(r => r.Name == "Colorless").Lands);
        Assert.Equal(1, rows.Single(r => r.Name == "Mardu").Lands);
    }

    [Fact]
    public void LandType_GoesToColorless()
    {
        // A non-basic land with no colors is bucketed as "Colorless" via
        // ColorCategory (Phase 0), not via the old Colors=0 heuristic.
        Card dualPathLand = Card("Burst Habitat", colorIdentity: [], typeLine: "Land", cmc: 0);

        StatsOverview o = Overview(dualPathLand);

        // Lands are excluded from the per-type stats (0 permanents / 0 spells).
        Assert.Equal(0, o.Permanents);
        Assert.Equal(0, o.Spells);
        Assert.Equal(1, o.Lands);
        Assert.Equal(1, o.Groups.SelectMany(g => g.Rows).Single(r => r.Name == "Colorless").Lands);
    }

    // --- factory -----------------------------------------------------------

    private static Card Card(
        string name,
        List<string>? colorIdentity,
        string typeLine,
        double cmc = 0)
    {
        return TestCards.Make(
            name, typeLine, colors: colorIdentity ?? [], manaCost: "", cmc: cmc,
            set: "LEA", setName: "Alpha", setType: "core", releasedAt: "1993-08-05");
    }
}
