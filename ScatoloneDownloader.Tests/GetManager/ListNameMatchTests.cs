using ScatoloneDownloader.Download;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests;

/// <summary>
/// The name matching of <see cref="GetManager.GetCardList"/>: a list names a
/// multi-face card by its front alone, the way CubeCobra exports it, and still
/// finds the card Scryfall names with every face.
/// </summary>
public sealed class ListNameMatchTests
{
    [Fact]
    public void FrontName_FindsTheFullName()
    {
        Card borrower = Make("Brazen Borrower // Petty Theft", "adventure");

        List<Card> matched = Match(["Brazen Borrower"], borrower);

        Assert.Same(borrower, Assert.Single(matched));
    }

    [Fact]
    public void ExactName_WinsOverAFront()
    {
        Card bind = Make("Bind");
        Card split = Make("Bind // Liberate", "split");

        List<Card> matched = Match(["Bind"], bind, split);

        Assert.Same(bind, Assert.Single(matched));
    }

    [Fact]
    public void FrontSharedByTwoCards_IsAMiss()
    {
        List<Card> matched = Match(["Start"], Make("Start // Finish", "split"), Make("Start // Fire", "split"));

        Assert.Empty(matched);
    }

    [Theory]
    [InlineData("art_series")]
    [InlineData("reversible_card")]
    [InlineData("double_faced_token")]
    public void ArtReversibleAndTokenFaces_AreNotFronts(string layout)
    {
        List<Card> matched = Match(["Abrade"], Make("Abrade // Abrade", layout));

        Assert.Empty(matched);
    }

    [Fact]
    public void ReversiblePrinting_DoesNotMakeAFrontAmbiguous()
    {
        Card adventure = Make("Scavenger Regent // Exude Toxin", "adventure");
        Card reversible = Make("Scavenger Regent // Exude Toxin // Scavenger Regent", "reversible_card");

        List<Card> matched = Match(["Scavenger Regent"], adventure, reversible);

        Assert.Same(adventure, Assert.Single(matched));
    }

    [Fact]
    public void FrontAndFullName_InOneList_KeepTheCardOnce()
    {
        Card fable = Make("Fable of the Mirror-Breaker // Reflection of Kiki-Jiki", "transform");

        List<Card> matched = Match(
            ["Fable of the Mirror-Breaker", "Fable of the Mirror-Breaker // Reflection of Kiki-Jiki"], fable);

        Assert.Same(fable, Assert.Single(matched));
    }

    [Fact]
    public void UnknownNames_AreReturnedAsMisses_InListOrder()
    {
        (List<Card> matched, List<string> missing) =
            MatchWithMisses(["# mainboard", "Sol Ring", "Start", "Not A Card"],
                Make("Sol Ring"), Make("Start // Finish", "split"), Make("Start // Fire", "split"));

        Assert.Equal("Sol Ring", Assert.Single(matched).Name);
        Assert.Equal(["# mainboard", "Start", "Not A Card"], missing);
    }

    [Fact]
    public void DuplicateAndFrontMatch_AreNotMisses()
    {
        (_, List<string> missing) = MatchWithMisses(
            ["Brazen Borrower", "Brazen Borrower // Petty Theft", "Brazen Borrower"],
            Make("Brazen Borrower // Petty Theft", "adventure"));

        Assert.Empty(missing);
    }

    private static Card Make(string name, string layout = "normal")
    {
        return Card.CreateCard(TestCards.Json(name: name, layout: layout));
    }

    private static List<Card> Match(IEnumerable<string> listed, params Card[] cards)
    {
        return MatchWithMisses(listed, cards).Cards;
    }

    // Indexes the cards under their own names, as GetManager.BuildCardsByName
    // does for the first printing of each name.
    private static (List<Card> Cards, List<string> Missing) MatchWithMisses(IEnumerable<string> listed, params Card[] cards)
    {
        Dictionary<string, Card> index = cards.ToDictionary(card => card.Name);

        return GetManager.MatchEntries(
            listed.Select(name => new CardListEntry(name, string.Empty)),
            index,
            GetManager.BuildFullNamesByFront(index.Values));
    }
}
