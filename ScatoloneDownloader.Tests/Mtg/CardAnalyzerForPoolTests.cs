using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Tests.Cli;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Covers <see cref="CardAnalyzer.ForPool"/>: the cube analysis must count only
/// the active pool (rating 3-5, no Banned/Token/Jolly), matching the views and
/// the CardStatus doc — the unrated backlog and excluded status cards must not
/// skew the distributions. Rating 2 is the one exception: it is carried along as
/// the bench and rendered as its own sections, still contributing to no
/// metric of the pool. The plain constructor is unaffected (the analyze command
/// still analyzes whatever it is given).
/// </summary>
public sealed class CardAnalyzerForPoolTests
{
    [Fact]
    public void ForPool_ExcludesStatusAndNonPoolCards()
    {
        List<Card> cards =
        [
            MakeCard("Pool Creature", rating: 5, status: CardStatus.None),
            MakeCard("Banned Bomb", rating: 5, status: CardStatus.Banned),
            MakeCard("Token Maker", rating: 5, status: CardStatus.Token),
            MakeCard("Unrated", rating: 0, status: CardStatus.None),
            MakeCard("Fringe", rating: 2, status: CardStatus.None),
        ];

        AnalysisReport report = CardAnalyzer.ForPool(cards).Analyze();

        // Only the rating-5, no-status creature survives into the report.
        Assert.Equal(1, report.TotalCards);
    }

    [Fact]
    public void ForPool_KeepsAllPoolRatings_3To5()
    {
        List<Card> cards =
        [
            MakeCard("Three", rating: 3, status: CardStatus.None),
            MakeCard("Four", rating: 4, status: CardStatus.None),
            MakeCard("Five", rating: 5, status: CardStatus.None),
        ];

        AnalysisReport report = CardAnalyzer.ForPool(cards).Analyze();

        Assert.Equal(3, report.TotalCards);
    }

    [Fact]
    public void ForPool_Rating2_RendersBenchSections_WithoutEnteringPoolTotals()
    {
        List<Card> cards =
        [
            MakeCard("Pool Bear", rating: 4, status: CardStatus.None),
            MakeCard("Bench Bear", rating: 2, status: CardStatus.None, cmc: 2, effects: CardEffect.Ramp),
            MakeCard("Bench Fatty", rating: 2, status: CardStatus.None, cmc: 7),
            MakeCard("Rejected", rating: 1, status: CardStatus.None),
            MakeCard("Banned Two", rating: 2, status: CardStatus.Banned),
        ];

        CardAnalyzer analyzer = CardAnalyzer.ForPool(cards);
        string text = StatsMarkdownTests.Render(cards);

        // The pool metrics see the rating-4 card and nothing else.
        Assert.Equal(1, analyzer.Analyze().TotalCards);
        Assert.Contains("1 cards: 1 non-land + 0 lands", text);

        // The two rating-2 cards land in the bench sections. Rating 1 is not a bench
        // card, and a Banned 2 is a status card — neither may appear.
        StatsPayload payload = StatsPayload.Build(cards);
        Assert.Equal(2, payload.BenchOverview!.NonLand);

        // The cost histogram is the point of the section: one 2-drop, one 6+.
        string byCost = StatsMarkdownTests.Section(text, "Bench (★2) — mana value by colour category");
        Assert.Equal("| Green | – | – | 1 (50%) | – | – | – | 1 (50%) | 2 (100%) |", StatsMarkdownTests.Line(byCost, "| Green |"));

        // And the effect axis, for "the pool is short on ramp" lookups.
        string effects = StatsMarkdownTests.Section(text, "Bench (★2) — available by effect and colour");
        Assert.Equal("| Ramp | – | – | – | – | 1 (50%) | – | – | 1 (50%) |", StatsMarkdownTests.Line(effects, "| Ramp |"));
    }

    [Fact]
    public void ForPool_BenchLands_ShowUnderLands_NotInTheCostTable()
    {
        List<Card> cards =
        [
            MakeCard("Pool Bear", rating: 4, status: CardStatus.None),
            MakeCard("Bench Dual", rating: 2, status: CardStatus.None, typeLine: "Land", cmc: 0),
        ];

        StatsPayload payload = StatsPayload.Build(cards);
        string text = StatsMarkdownTests.Render(cards);

        // A land's CMC says nothing, so it is kept out of the cost table (as in the
        // pool sections) — but it must still be findable, since a bench dual is
        // exactly what a "short on fixing" hole wants.
        Assert.Equal(0, payload.BenchOverview!.NonLand);
        Assert.Equal(1, payload.BenchOverview.Lands);
        Assert.EndsWith("| 1 (100%) |", StatsMarkdownTests.Line(
            StatsMarkdownTests.Section(text, "Bench (★2) — by colour category"), "| Green |"));
        Assert.DoesNotContain("| Green |", StatsMarkdownTests.Section(text, "Bench (★2) — mana value by colour category"));
    }

    [Fact]
    public void ForPool_NoRating2Cards_StillWritesTheBenchSections_Empty()
    {
        // The page always shows the bench sections for the cube, empty when no card
        // is ★2; only a card list has none.
        List<Card> cards = [MakeCard("Pool Bear", rating: 4, status: CardStatus.None)];

        StatsPayload payload = StatsPayload.Build(cards);

        Assert.Equal(0, payload.BenchOverview!.NonLand);
        Assert.Contains("## Bench (★2) — by colour category", StatsMarkdownTests.Render(cards));
    }

    [Fact]
    public void PlainConstructor_AnalyzesWhateverItIsGiven_AndAListHasNoBench()
    {
        // The analyze command builds the analyzer directly from cards that carry no
        // rating at all: every one counts, and there is no bench.
        StatsPayload payload = StatsPayload.BuildForList([MakeCard("Whatever", rating: 0, status: CardStatus.None)], "list");

        Assert.Equal(1, new CardAnalyzer([MakeCard("Whatever", rating: 0, status: CardStatus.None)]).Analyze().TotalCards);
        Assert.Null(payload.Bench);
        Assert.DoesNotContain("Bench", StatsMarkdownTests.RenderList([MakeCard("Whatever", rating: 0, status: CardStatus.None)]));
    }

    private static Card MakeCard(
        string name,
        int rating,
        CardStatus status,
        double cmc = 2,
        CardEffect effects = CardEffect.None,
        string typeLine = "Creature — Bear")
    {
        return TestCards.Make(
            name, typeLine, colors: ["G"], cmc: cmc, rating: rating, status: status, effects: effects);
    }
}
