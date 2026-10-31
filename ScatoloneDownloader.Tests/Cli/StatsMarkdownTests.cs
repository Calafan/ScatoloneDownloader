using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The Markdown twin of the stats page: its sections and their order, both column
/// views, the page's cell format and the bench overview, over small hand-built pools.
/// </summary>
public sealed class StatsMarkdownTests
{
    private static readonly DateTime Moment = new(2026, 10, 30, 14, 5, 59);

    internal static readonly string[] CubeTitles =
    [
        "Pool — global distribution",
        "Pool — by colour category",
        "Pool — mana value by colour category",
        "Pool — creature mana value by colour category",
        "Pool — effects by colour",
        "Pool — answers vs targets",
        "Bench (★2) — by colour category",
        "Bench (★2) — mana value by colour category",
        "Bench (★2) — available by effect and colour",
        "Pool — creatures by cost",
        "Pool — creature curve by colour",
        "Bench (★2) — creature curve by colour",
    ];

    /// <summary>The cube's Markdown over <paramref name="cards"/>.</summary>
    internal static string Render(IEnumerable<Card> cards)
    {
        return StatsMarkdown.Render(StatsPayload.Build(cards), StatsDocument.ForCube(Moment));
    }

    /// <summary>The Markdown of a card list called <paramref name="label"/>.</summary>
    internal static string RenderList(IEnumerable<Card> cards, string label = "mylist")
    {
        return StatsMarkdown.Render(
            StatsPayload.BuildForList(cards, label),
            StatsDocument.ForList(label + ".txt", Moment, cards.Count(), 0, 0));
    }

    /// <summary>The text of the section titled <paramref name="title"/>, up to the next <c>## </c>.</summary>
    internal static string Section(string markdown, string title)
    {
        string[] lines = markdown.Split(Environment.NewLine);
        int start = Array.IndexOf(lines, "## " + title);
        Assert.True(start >= 0, "no section " + title);
        IEnumerable<string> body = lines.Skip(start + 1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal));
        return string.Join(Environment.NewLine, body);
    }

    /// <summary>The first table line that starts with <paramref name="prefix"/>.</summary>
    internal static string Line(string text, string prefix)
    {
        return text.Split(Environment.NewLine).First(l => l.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static List<string> Headings(string markdown)
    {
        return markdown.Split(Environment.NewLine)
            .Where(l => l.StartsWith("## ", StringComparison.Ordinal))
            .Select(l => l[3..])
            .ToList();
    }

    [Fact]
    public void Render_Cube_HasTheTwelvePageSectionsInOrder()
    {
        string md = Render([Make("Bolt", ["R"], "Instant", rating: 4)]);

        Assert.Equal(CubeTitles, Headings(md));
    }

    [Fact]
    public void Render_List_HasTheEightPoolSectionsUnderTheListLabel_AndNoBench()
    {
        string md = RenderList([Make("Bolt", ["R"], "Instant", rating: 0)], "mylist");

        Assert.Equal(
            CubeTitles.Where(t => !t.StartsWith("Bench", StringComparison.Ordinal))
                .Select(t => t.Replace("Pool", "mylist", StringComparison.Ordinal))
                .ToList(),
            Headings(md));
        Assert.DoesNotContain("Bench", md);
    }

    [Fact]
    public void Render_WritesOneHeadingPerPayloadSection_InThePayloadsOrder()
    {
        StatsPayload payload = StatsPayload.Build(
        [
            Make("Bolt", ["R"], "Instant", rating: 4),
            Make("Bench", ["W"], "Instant", rating: 2),
        ]);

        string md = StatsMarkdown.Render(payload, StatsDocument.ForCube(Moment));

        Assert.Equal(payload.Sections.Select(s => s.Title).ToList(), Headings(md));
    }

    [Fact]
    public void Sections_OfACubeWithABench_FollowThePagesOrder()
    {
        StatsPayload payload = StatsPayload.Build(
        [
            Make("Bolt", ["R"], "Instant", rating: 4),
            Make("Bench", ["W"], "Instant", rating: 2),
        ]);

        Assert.Equal(
            [
                "types/pool", "categories/pool", "manaValue/pool", "creatureManaValue/pool", "effects/pool", "answers/pool",
                "categories/bench", "manaValue/bench", "effects/bench",
                "creatures/pool", "curve/pool", "curve/bench",
            ],
            payload.Sections.Select(s => s.Kind + "/" + s.Tier).ToList());
        Assert.All(payload.Sections, s => Assert.StartsWith(s.Tier == "pool" ? "Pool — " : "Bench (★2) — ", s.Title));
        Assert.Equal(CubeTitles, payload.Sections.Select(s => s.Title).ToList());
        Assert.Equal(StatsPayload.CubeScope, payload.Scope);
    }

    [Fact]
    public void Sections_OfAListPayload_HaveNoBench()
    {
        StatsPayload payload = StatsPayload.BuildForList([Make("Bolt", ["R"], "Instant", rating: 0)], "mylist");

        Assert.Equal(
            ["types", "categories", "manaValue", "creatureManaValue", "effects", "answers", "creatures", "curve"],
            payload.Sections.Select(s => s.Kind).ToList());
        Assert.All(payload.Sections, s => Assert.Equal("pool", s.Tier));
        Assert.All(payload.Sections, s => Assert.StartsWith("mylist — ", s.Title));
        Assert.Equal(StatsPayload.ListScope, payload.Scope);
    }

    [Fact]
    public void Payload_CarriesTheViewNamesAndTheBuckets()
    {
        StatsPayload payload = StatsPayload.Build([]);

        Assert.Equal("mono + Multi", payload.ViewNames.Mono);
        Assert.Equal("colour incl. multi", payload.ViewNames.Identity);
        Assert.Equal(CostBucket.All.ToList(), payload.Buckets.Select(b => b.Key).ToList());
        Assert.Equal("6+", payload.Buckets[^1].Label);
    }

    [Fact]
    public void Render_StartsWithTheDocumentTitleSubtitleAndNote()
    {
        StatsDocument document = StatsDocument.ForCube(Moment);

        string md = StatsMarkdown.Render(StatsPayload.Build([Make("Bolt", ["R"], "Instant", rating: 4)]), document);

        string[] lines = md.Split(Environment.NewLine);
        Assert.Equal("# Scatolone Cube Stats", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal("*" + StatsDocument.Generated(Moment) + "*", lines[2]);
        Assert.Equal("", lines[3]);
        Assert.Equal(document.Note, lines[4]);
    }

    [Fact]
    public void Render_EmptyCube_StillWritesEveryTableWithZeros()
    {
        string md = Render([]);

        Assert.Equal(CubeTitles, Headings(md));
        string types = Section(md, "Pool — global distribution");
        Assert.Contains("0 cards: 0 non-land + 0 lands", types);
        Assert.Contains("| Creatures | – |", types);
        Assert.Contains("| Spells | – |", types);
        Assert.Contains("| Lands | – |", types);
    }

    [Fact]
    public void Render_IdentityView_CountsAGoldCardInEachOfItsColours()
    {
        string md = Render(
        [
            Make("Gold", ["W", "U"], "Instant", rating: 4),
            Make("Plain", ["W"], "Instant", rating: 4),
        ]);

        string effects = Section(md, "Pool — effects by colour");
        int identity = effects.IndexOf("### colour incl. multi", StringComparison.Ordinal);
        int mono = effects.IndexOf("### mono + Multi", StringComparison.Ordinal);
        Assert.True(mono >= 0 && identity > mono);

        // Columns: White Blue Black Red Green Colorless | Total (two distinct cards).
        string identityNonLand = Line(effects[identity..], "| Non-land |");
        Assert.Equal("| Non-land | 2 (100%) | 1 (100%) | – | – | – | – | 2 (100%) |", identityNonLand);

        // The mono view puts the gold card in Multi instead.
        string monoNonLand = Line(effects[mono..identity], "| Non-land |");
        Assert.Equal("| Non-land | 1 (100%) | – | – | – | – | 1 (100%) | – | 2 (100%) |", monoNonLand);
    }

    [Fact]
    public void Render_AnswersAndCurve_HaveBothViews()
    {
        string md = Render([Make("Bear", ["G"], "Creature — Bear", rating: 4, power: "2", toughness: "2")]);

        foreach (string title in new[] { "Pool — effects by colour", "Pool — answers vs targets", "Pool — creature curve by colour" })
        {
            string section = Section(md, title);
            Assert.Contains("### mono + Multi", section);
            Assert.Contains("### colour incl. multi", section);
        }
    }

    [Fact]
    public void Cell_ZeroIsADash_ElseTheCountAndItsShare()
    {
        Assert.Equal("–", StatsMarkdown.Cell(0, 8));
        Assert.Equal("–", StatsMarkdown.Cell(0, 0));
        Assert.Equal("3 (38%)", StatsMarkdown.Cell(3, 8));
        Assert.Equal("5", StatsMarkdown.Cell(5, 0));
        Assert.Equal("1 (100%)", StatsMarkdown.Cell(1, 1));
    }

    [Fact]
    public void Cell_AHalfShareRoundsUp()
    {
        // 1 of 8 is 12.5%: half away from zero, like the page's Math.round.
        Assert.Equal("1 (13%)", StatsMarkdown.Cell(1, 8));
        Assert.Equal("1 (50%)", StatsMarkdown.Cell(1, 2));
    }

    [Fact]
    public void Render_MatrixCells_UseTheColumnAsDenominator()
    {
        List<Card> cards = [Make("Removal", ["W"], "Instant", CardEffect.Removal, rating: 4)];
        for (int i = 0; i < 7; i++)
        {
            cards.Add(Make("Filler " + i, ["W"], "Instant", rating: 4));
        }

        string effects = Section(Render(cards), "Pool — effects by colour");

        // 1 of the 8 white cards, 12.5 -> 13%; the empty columns are dashes.
        Assert.Equal("| Removal | 1 (13%) | – | – | – | – | – | – | 1 (13%) |", Line(effects, "| Removal |"));
        Assert.Equal("| Non-land | 8 (100%) | – | – | – | – | – | – | 8 (100%) |", Line(effects, "| Non-land |"));
    }

    [Fact]
    public void Render_BenchOverview_ShowsABenchLandUnderLands_WithoutStarColumns()
    {
        string md = Render(
        [
            Make("Pool Bear", ["G"], "Creature — Bear", rating: 4, power: "2", toughness: "2"),
            Make("Bench Dual", ["G"], "Land", rating: 2, cmc: 0),
        ]);

        string bench = Section(md, "Bench (★2) — by colour category");

        string head = Line(bench, "| Category |");
        Assert.EndsWith("| Pips | Lands |", head);
        Assert.DoesNotContain("★", head);

        string green = Line(bench, "| Green |");
        Assert.EndsWith("| 1 (100%) |", green);

        // The pool's table keeps the star columns, and has no land of its own.
        string pool = Section(md, "Pool — by colour category");
        Assert.Contains("| ★3 | ★4 | ★5 | Lands |", Line(pool, "| Category |"));
    }

    [Fact]
    public void Render_BenchManaValue_CountsTheBenchCards()
    {
        string md = Render(
        [
            Make("Pool Bear", ["G"], "Creature — Bear", rating: 4, power: "2", toughness: "2"),
            Make("Bench Fatty", ["R"], "Instant", rating: 2, cmc: 7),
        ]);

        string bench = Section(md, "Bench (★2) — mana value by colour category");

        Assert.Equal("| Red | – | – | – | – | – | – | 1 (100%) | 1 (100%) |", Line(bench, "| Red |"));
        Assert.DoesNotContain("| Green |", bench);
    }

    [Fact]
    public void Render_TypesNote_AndCreatureSummary_UseThePageWording()
    {
        string md = Render(
        [
            Make("Bear", ["G"], "Creature — Bear", rating: 4, cmc: 2, power: "2", toughness: "2"),
            Make("Bolt", ["R"], "Instant", rating: 4, cmc: 1),
        ]);

        Assert.Contains("2 cards: 2 non-land + 0 lands · permanents 1 (50%) · spells 1 (50%) of non-land"
            + " · average mana value 1.50 (lands excluded)", md);
        Assert.Contains("Creatures 1 (50.0%) · MV ≤ 2 1 (50.0%)", md);
        Assert.Contains(" (percent of non-land)", md);
    }

    [Fact]
    public void Render_AnswersTable_HasTargetsAndTheirRatio()
    {
        string md = Render(
        [
            Make("White Removal", ["W"], "Instant", CardEffect.Removal, rating: 4),
            Make("Bear One", ["G"], "Creature — Bear", rating: 3),
            Make("Bear Two", ["G"], "Creature — Bear", rating: 3),
            Make("Bear Three", ["G"], "Creature — Bear", rating: 3),
        ]);

        string answers = Section(md, "Pool — answers vs targets");

        Assert.Contains("| Answer | Tags | White | Blue | Black | Red | Green | Multi | Colorless | Total | Targets | Targets / answer |", answers);
        Assert.Equal(
            "| Creature removal | Removal, RemovePermanent, Wipe | 1 (100%) | – | – | – | – | – | – | 1 (25%) | 3 | 3.0 |",
            Line(answers, "| Creature removal |"));
        Assert.Equal(
            "| Counterspells | Counter | – | – | – | – | – | – | – | – | – | – |",
            Line(answers, "| Counterspells |"));
    }

    internal static Card Make(
        string name,
        string[] colors,
        string typeLine,
        CardEffect effects = CardEffect.None,
        int rating = 4,
        CardStatus status = CardStatus.None,
        double cmc = 2,
        string power = "",
        string toughness = "",
        bool reviewed = false,
        string? oracleId = null,
        string? oracleText = null)
    {
        return TestCards.Make(
            name, typeLine, oracleText, colors, cmc: cmc,
            power: power.Length == 0 ? null : power,
            toughness: toughness.Length == 0 ? null : toughness,
            oracleId: oracleId ?? Guid.NewGuid().ToString(),
            rating: rating, status: status, effects: effects, reviewed: reviewed);
    }
}
