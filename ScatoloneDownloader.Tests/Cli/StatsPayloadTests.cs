using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The payload behind the tagger's <c>/stats</c> page: column links, tier
/// selection, the shared row order, distinct totals and the embedded page itself.
/// </summary>
public sealed class StatsPayloadTests
{
    [Fact]
    public void Columns_CarryTheColourParamsTheTaggerFilterTakes()
    {
        StatsPayload payload = StatsPayload.Build([]);

        Assert.Equal(
            ["W", "U", "B", "R", "G", "Multi", "Colorless"],
            payload.Pool.Mono.Columns.Select(c => c.Key));
        Assert.Equal("1 White", payload.Pool.Mono.Columns.Single(c => c.Key == "W").ColorParam);
        Assert.Equal("*multi", payload.Pool.Mono.Columns.Single(c => c.Key == "Multi").ColorParam);
        Assert.Equal("5 Colorless", payload.Pool.Mono.Columns.Single(c => c.Key == "Colorless").ColorParam);

        Assert.Equal(
            ["W", "U", "B", "R", "G", "Colorless"],
            payload.Pool.Identity.Columns.Select(c => c.Key));
        Assert.Equal("*has:W", payload.Pool.Identity.Columns.Single(c => c.Key == "W").ColorParam);
        Assert.Equal("*has:G", payload.Pool.Identity.Columns.Single(c => c.Key == "G").ColorParam);
        Assert.Equal("5 Colorless", payload.Pool.Identity.Columns.Single(c => c.Key == "Colorless").ColorParam);
    }

    [Fact]
    public void Tiers_UseTheTaggerRatingParams_AndEffectsListEveryFlag()
    {
        StatsPayload payload = StatsPayload.Build([]);

        Assert.Equal("Pool", payload.Pool.Label);
        Assert.Equal("pool", payload.Pool.RatingParam);
        Assert.Equal("Bench (★2)", payload.Bench!.Label);
        Assert.Equal("r2", payload.Bench!.RatingParam);
        Assert.Equal(EffectResolver.ToNames((CardEffect)~0), payload.Effects);
        Assert.Equal(payload.Effects.Count, payload.Glossary.Count);
    }

    [Fact]
    public void Pool_ExcludesStatusCardsAndLowRatings_AndBenchHoldsOnlyRatingTwo()
    {
        List<Card> cards =
        [
            MakeCard("Pool", ["W"], CardEffect.Removal, rating: 4),
            MakeCard("Banned", ["W"], CardEffect.Removal, rating: 4, status: CardStatus.Banned),
            MakeCard("Fringe", ["W"], CardEffect.Removal, rating: 1),
            MakeCard("Bench", ["W"], CardEffect.Removal, rating: 2),
            MakeCard("Bench Banned", ["W"], CardEffect.Removal, rating: 2, status: CardStatus.Token),
        ];

        StatsPayload payload = StatsPayload.Build(cards);

        Assert.Equal(1, payload.Pool.NonLand);
        Assert.Equal(1, payload.Pool.Mono.TotalCards);
        Assert.Equal(1, payload.Bench!.NonLand);
        Assert.Equal(1, payload.Bench!.Mono.Cards["W"]);
        Assert.Equal(1, Row(payload.Bench!.Mono, "Removal").Total);
    }

    [Fact]
    public void BenchRows_FollowThePoolOrder_EvenWhenBenchTotalsWouldSortDifferently()
    {
        List<Card> cards =
        [
            // Pool: Ramp 2, Burn 1 -> Ramp before Burn.
            MakeCard("Pool Ramp 1", ["G"], CardEffect.Ramp, rating: 4),
            MakeCard("Pool Ramp 2", ["G"], CardEffect.Ramp, rating: 4),
            MakeCard("Pool Burn", ["R"], CardEffect.Burn, rating: 4),
            // Bench: Burn 2, Ramp 0 -> its own order would put Burn first.
            MakeCard("Bench Burn 1", ["R"], CardEffect.Burn, rating: 2),
            MakeCard("Bench Burn 2", ["R"], CardEffect.Burn, rating: 2),
        ];

        StatsPayload payload = StatsPayload.Build(cards);

        List<string> poolOrder = payload.Pool.Mono.Rows.Select(r => r.Effect).ToList();
        List<string> benchOrder = payload.Bench!.Mono.Rows.Select(r => r.Effect).ToList();

        Assert.Equal(poolOrder, benchOrder);
        Assert.Equal(poolOrder, payload.Pool.Identity.Rows.Select(r => r.Effect).ToList());
        Assert.True(poolOrder.IndexOf("Ramp") < poolOrder.IndexOf("Burn"));
        Assert.Equal(2, Row(payload.Bench!.Mono, "Burn").Total);
    }

    [Fact]
    public void IdentityRowTotal_IsDistinct_WhileColumnsCountEveryColour()
    {
        List<Card> cards = [MakeCard("Gold WU", ["W", "U"], CardEffect.Removal, rating: 4)];

        StatsPayload payload = StatsPayload.Build(cards);
        StatsEffectRow identity = Row(payload.Pool.Identity, "Removal");
        StatsEffectRow mono = Row(payload.Pool.Mono, "Removal");

        Assert.Equal(1, identity.ByColumn["W"]);
        Assert.Equal(1, identity.ByColumn["U"]);
        Assert.Equal(1, identity.Total);
        Assert.Equal(1, payload.Pool.Identity.TotalCards);
        Assert.Equal(1, mono.ByColumn["Multi"]);
        Assert.Equal(0, mono.ByColumn["W"]);
        StatsAnswerGroup answer = payload.Pool.Answers.First(a => a.Effects.Contains("Removal"));
        Assert.Equal(1, answer.ByIdentity["U"]);
        Assert.Equal(1, answer.Total);
    }

    [Fact]
    public void ZeroEffects_AreListed_AndEveryColumnKeyIsPresent()
    {
        StatsPayload payload = StatsPayload.Build([MakeCard("Only", ["R"], CardEffect.Burn, rating: 4)]);

        Assert.Equal(payload.Effects.Count, payload.Pool.Mono.Rows.Count);
        StatsEffectRow ramp = Row(payload.Pool.Mono, "Ramp");
        Assert.Equal(0, ramp.Total);
        Assert.Equal(payload.Pool.Mono.Columns.Select(c => c.Key).Order(), ramp.ByColumn.Keys.Order());
        Assert.Equal(payload.Pool.Identity.Columns.Select(c => c.Key).Order(), Row(payload.Pool.Identity, "Ramp").ByColumn.Keys.Order());
        Assert.Equal(payload.Pool.Mono.Columns.Select(c => c.Key).Order(), payload.Pool.Mono.Cards.Keys.Order());
        Assert.Equal(payload.Pool.Mono.Columns.Select(c => c.Key).Order(), payload.Pool.Mono.Untagged.Keys.Order());
        Assert.All(payload.Pool.Answers, a =>
        {
            Assert.Equal(payload.Pool.Mono.Columns.Select(c => c.Key).Order(), a.ByMono.Keys.Order());
            Assert.Equal(payload.Pool.Identity.Columns.Select(c => c.Key).Order(), a.ByIdentity.Keys.Order());
        });
    }

    [Fact]
    public void Totals_AreDistinctCards()
    {
        List<Card> cards =
        [
            MakeCard("Reviewed Gold", ["W", "U"], CardEffect.Removal, rating: 4, reviewed: true),
            MakeCard("Untagged", ["R"], CardEffect.None, rating: 4),
        ];

        StatsPayload payload = StatsPayload.Build(cards);

        Assert.Equal(2, payload.Pool.Identity.TotalCards);
        Assert.Equal(1, payload.Pool.Identity.TotalReviewed);
        Assert.Equal(1, payload.Pool.Identity.TotalUntagged);
        Assert.Equal(2, payload.Pool.Mono.TotalCards);
    }

    [Fact]
    public void GetStatsPageHtml_LoadsEmbeddedResource()
    {
        string html = TagCommand.GetStatsPageHtml();

        Assert.Contains("<!doctype html>", html);
        Assert.Contains("/api/stats", html);
    }

    private static StatsEffectRow Row(StatsMatrix matrix, string effect) =>
        matrix.Rows.Single(r => r.Effect == effect);

    private static Card MakeCard(
        string name,
        string[] colors,
        CardEffect effects,
        int rating,
        bool reviewed = false,
        CardStatus status = CardStatus.None)
    {
        return TestCards.Make(
            name, colors: colors, cmc: 2, oracleId: Guid.NewGuid().ToString(),
            rating: rating, status: status, effects: effects, reviewed: reviewed);
    }
}
