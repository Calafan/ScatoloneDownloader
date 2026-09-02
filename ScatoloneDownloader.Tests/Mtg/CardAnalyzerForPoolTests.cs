using System;
using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Covers <see cref="CardAnalyzer.ForPool"/>: the cube analysis must count only
/// the active pool (rating 3-5, no Banned/Token/Jolly), matching the views and
/// the CardStatus doc — the unrated backlog and excluded status cards must not
/// skew the distributions. Rating 2 is the one exception: it is carried along as
/// the bench and rendered as an appendix (section 6), still contributing to no
/// metric above it. The plain constructor is unaffected (the download
/// analyze/files path still analyzes whatever it is given).
/// </summary>
public sealed class CardAnalyzerForPoolTests : IDisposable
{
    private readonly string tempFile = Path.Combine(Path.GetTempPath(), "ScatoloneBench_" + Guid.NewGuid().ToString("N") + ".md");

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

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
    public void ForPool_Rating2_RendersBenchAppendix_WithoutEnteringPoolTotals()
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
        analyzer.SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // The pool metrics see the rating-4 card and nothing else.
        Assert.Equal(1, analyzer.Analyze().TotalCards);
        Assert.Contains("Global Distribution (1 Cards)", text);

        // The two rating-2 cards land in the appendix. Rating 1 is not a bench
        // card, and a Banned 2 is a status card — neither may appear.
        Assert.Contains("## 6. Bench Availability (2 Cards, Rating 2)", text);
        Assert.Contains("### Available by Color and Cost", text);

        // The cost histogram is the point of the section: one 2-drop, one 6+.
        Assert.Contains("| **Green** | 2 | 2 | `0: 0` `1: 0` `2: 1` `3: 0` `4: 0` `5: 0` `6+: 1` |", text);

        // And the effect axis, for "the pool is short on ramp" lookups.
        Assert.Contains("### Available by Effect", text);
        Assert.Contains("| Ramp | 1 |", text);
    }

    [Fact]
    public void ForPool_BenchLands_GetTheirOwnTable_NotTheCostTable()
    {
        List<Card> cards =
        [
            MakeCard("Pool Bear", rating: 4, status: CardStatus.None),
            MakeCard("Bench Dual", rating: 2, status: CardStatus.None, typeLine: "Land", cmc: 0),
        ];

        CardAnalyzer.ForPool(cards).SaveAnalysis(tempFile);

        string text = File.ReadAllText(tempFile);

        // A land's CMC says nothing, so it is kept out of the cost table (as in the
        // pool sections) — but it must still be findable, since a bench dual is
        // exactly what a "short on fixing" hole wants.
        Assert.Contains("**Non-Land:** 0 | **Lands:** 1", text);
        Assert.Contains("### Available Lands", text);
        Assert.Contains("| Green | 1 |", text);
    }

    [Fact]
    public void ForPool_NoRating2Cards_OmitsTheBenchSectionEntirely()
    {
        List<Card> cards = [MakeCard("Pool Bear", rating: 4, status: CardStatus.None)];

        CardAnalyzer.ForPool(cards).SaveAnalysis(tempFile);

        Assert.DoesNotContain("Bench Availability", File.ReadAllText(tempFile));
    }

    [Fact]
    public void PlainConstructor_NeverRendersABenchSection()
    {
        // The download analyze/files path builds the analyzer directly from cards
        // that carry no rating at all; its report must be unchanged.
        new CardAnalyzer([MakeCard("Whatever", rating: 0, status: CardStatus.None)]).SaveAnalysis(tempFile);

        Assert.DoesNotContain("Bench Availability", File.ReadAllText(tempFile));
    }

    private static Card MakeCard(
        string name,
        int rating,
        CardStatus status,
        double cmc = 2,
        CardEffect effects = CardEffect.None,
        string typeLine = "Creature — Bear")
    {
        JsonCard json = new()
        {
            Name = name,
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
            Colors = ["G"],
            ColorIdentity = ["G"],
            ManaCost = "{1}{G}",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.Status = status;
        card.Effects = effects;
        return card;
    }
}
