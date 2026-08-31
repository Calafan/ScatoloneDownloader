using System.Collections.Generic;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Covers <see cref="CardAnalyzer.ForPool"/>: the cube analysis must count only
/// the active pool (rating 3-5, no Banned/Token/Jolly), matching the views and
/// the CardStatus doc — the unrated backlog and excluded status cards must not
/// skew the distributions. The plain constructor is unaffected (the download
/// analyze/files path still analyzes whatever it is given).
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

    private static Card MakeCard(string name, int rating, CardStatus status)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = "Creature — Bear",
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 2,
            Colors = ["G"],
            ColorIdentity = ["G"],
            ManaCost = "{1}{G}",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.Status = status;
        return card;
    }
}
