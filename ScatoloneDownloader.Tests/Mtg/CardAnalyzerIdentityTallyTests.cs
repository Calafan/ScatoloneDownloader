using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// The colour-identity tally of <see cref="CardAnalyzer.Analyze"/>: unlike the
/// effect matrix's one-column split, a card counts once in EVERY colour of its
/// identity, which is what the live stats page's "colour incl. multi" view shows.
/// </summary>
public sealed class CardAnalyzerIdentityTallyTests
{
    [Fact]
    public void GoldCard_CountsInEveryColourOfItsIdentity_AndInNoMultiKey()
    {
        List<Card> cards =
        [
            MakeCard("Gold WU", ["W", "U"], CardEffect.Removal, reviewed: true),
            MakeCard("Mono W", ["W"], CardEffect.None),
            MakeCard("Rock", [], CardEffect.Removal),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();

        Assert.Equal(2, report.CardsByIdentity["W"]);
        Assert.Equal(1, report.CardsByIdentity["U"]);
        Assert.Equal(1, report.CardsByIdentity["Colorless"]);
        Assert.False(report.CardsByIdentity.ContainsKey("Multi"));
        Assert.False(report.CardsByIdentity.ContainsKey("B"));

        // The distinct total is still the three cards, not the four column hits.
        Assert.Equal(3, report.TotalCards);

        Assert.Equal(1, report.EffectCountsByIdentity["W"][CardEffect.Removal]);
        Assert.Equal(1, report.EffectCountsByIdentity["U"][CardEffect.Removal]);
        Assert.Equal(1, report.EffectCountsByIdentity["Colorless"][CardEffect.Removal]);
        Assert.False(report.EffectCountsByIdentity.ContainsKey("Multi"));
    }

    [Fact]
    public void ReviewedAndUntaggedByIdentity_FollowEveryColourOfTheCard()
    {
        List<Card> cards =
        [
            MakeCard("Reviewed Gold", ["B", "G"], CardEffect.Ramp, reviewed: true),
            MakeCard("Untagged Gold", ["B", "G"], CardEffect.None),
            MakeCard("Untagged Rock", [], CardEffect.None, reviewed: true),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();

        Assert.Equal(2, report.CardsByIdentity["B"]);
        Assert.Equal(2, report.CardsByIdentity["G"]);
        Assert.Equal(1, report.ReviewedByIdentity["B"]);
        Assert.Equal(1, report.ReviewedByIdentity["G"]);
        Assert.Equal(1, report.UntaggedByIdentity["B"]);
        Assert.Equal(1, report.UntaggedByIdentity["G"]);
        Assert.Equal(1, report.ReviewedByIdentity["Colorless"]);
        Assert.Equal(1, report.UntaggedByIdentity["Colorless"]);
    }

    [Fact]
    public void AnswerGroups_ByIdentity_CountDistinctCardsPerColour()
    {
        List<Card> cards =
        [
            // Two flags of the same group on one WU card: once in W, once in U.
            MakeCard("Removal And Wipe", ["W", "U"], CardEffect.Removal | CardEffect.Wipe),
            MakeCard("Mono U", ["U"], CardEffect.Removal),
            MakeCard("Colourless", [], CardEffect.Removal),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();
        AnswerGroupCount creatures = report.AnswerGroups.First(g => (g.Effects & CardEffect.Removal) != CardEffect.None);

        Assert.Equal(1, creatures.ByIdentity["W"]);
        Assert.Equal(2, creatures.ByIdentity["U"]);
        Assert.Equal(1, creatures.ByIdentity["Colorless"]);
        Assert.False(creatures.ByIdentity.ContainsKey("Multi"));
        Assert.Equal(3, creatures.Total);
    }

    [Fact]
    public void AnalyzeBench_ReportsOnlyTheBenchCards()
    {
        List<Card> cards =
        [
            MakeCard("Pool Card", ["R"], CardEffect.Burn, rating: 4),
            MakeCard("Bench Card", ["R"], CardEffect.Burn, rating: 2),
        ];

        CardAnalyzer analyzer = CardAnalyzer.ForPool(cards);

        Assert.Equal(1, analyzer.Analyze().TotalCards);
        Assert.Equal(1, analyzer.AnalyzeBench().TotalCards);
        Assert.Equal(1, analyzer.AnalyzeBench().CardsByIdentity["R"]);
    }

    private static Card MakeCard(
        string name,
        string[] colors,
        CardEffect effects,
        bool reviewed = false,
        int rating = 4)
    {
        JsonCard json = new()
        {
            Name = name,
            OracleId = Guid.NewGuid().ToString(),
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = "Instant",
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 2,
            Colors = [.. colors],
            ColorIdentity = [.. colors],
            ManaCost = colors.Length == 0 ? "{2}" : "{1}{" + colors[0] + "}",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.Status = CardStatus.None;
        card.Effects = effects;
        card.Reviewed = reviewed;
        return card;
    }
}
