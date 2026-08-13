using System;
using System.Collections.Generic;
using System.Linq;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Json.Sets;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests;

/// <summary>
/// Pure-logic coverage for the threshold decision and year filtering that
/// <see cref="GetManager.GetYears"/> uses to switch between
/// paginated search and the bulk-data download path. The actual network calls
/// are exercised elsewhere; here we pin the decision boundary and the filter.
/// </summary>
public sealed class GetYearsBulkPathTests
{
    private const int Threshold = 500;

    [Fact]
    public void ShouldUseBulkPath_ZeroCards_False()
    {
        List<Set> sets = [];

        Assert.False(GetManager.YearsBulkDecision(sets, Threshold));
    }

    [Fact]
    public void ShouldUseBulkPath_BelowThreshold_False()
    {
        List<Set> sets =
        [
            MakeSet("LEA", cardCount: 295),
            MakeSet("LEB", cardCount: 165),
        ];

        Assert.False(GetManager.YearsBulkDecision(sets, Threshold));
    }

    [Fact]
    public void ShouldUseBulkPath_JustAboveThreshold_True()
    {
        // "superano" = strict >. 501 cards triggers the bulk path; exactly 500 does not.
        List<Set> sets = [MakeSet("BIG", cardCount: 501)];

        Assert.True(GetManager.YearsBulkDecision(sets, Threshold));
    }

    [Fact]
    public void ShouldUseBulkPath_AtThreshold_False()
    {
        // Exactly at the threshold: search path is still fine.
        List<Set> sets = [MakeSet("BIG", cardCount: 500)];

        Assert.False(GetManager.YearsBulkDecision(sets, Threshold));
    }

    [Fact]
    public void ShouldUseBulkPath_AboveThreshold_True()
    {
        List<Set> sets =
        [
            MakeSet("neo", cardCount: 300),
            MakeSet("dmu", cardCount: 300),
        ];

        Assert.True(GetManager.YearsBulkDecision(sets, Threshold));
    }

    [Fact]
    public void ShouldUseBulkPath_IgnoresSetsWithZeroCards()
    {
        // A set with CardCount=0 should contribute nothing to the estimate.
        List<Set> sets =
        [
            MakeSet("neo", cardCount: 300),
            MakeSet("empty", cardCount: 0),
            MakeSet("tokens", cardCount: 0),
        ];

        Assert.False(GetManager.YearsBulkDecision(sets, Threshold));
    }

    [Fact]
    public void FilterByYear_KeepsOnlyCardsInRequestedYears()
    {
        HashSet<int> years = [2024, 2026];

        List<Card> cards =
        [
            MakeCard("Card2024", "2024-01-15"),
            MakeCard("Card2025", "2025-06-01"),
            MakeCard("Card2026", "2026-03-10"),
            MakeCard("Card2023", "2023-11-20"),
        ];

        List<Card> filtered = GetManager.FilterByYear(cards, years);

        Assert.Equal(2, filtered.Count);
        Assert.Equal("Card2024", filtered[0].Name);
        Assert.Equal("Card2026", filtered[1].Name);
    }

    [Fact]
    public void FilterByYear_EmptyYears_FiltersEverything()
    {
        HashSet<int> years = [];

        List<Card> cards =
        [
            MakeCard("A", "2024-01-15"),
            MakeCard("B", "2025-06-01"),
        ];

        Assert.Empty(GetManager.FilterByYear(cards, years));
    }

    [Fact]
    public void FilterByYear_EmptyCards_ReturnsEmpty()
    {
        HashSet<int> years = [2024];

        Assert.Empty(GetManager.FilterByYear([], years));
    }

    // --- factory -----------------------------------------------------------

    private static Set MakeSet(string code, int cardCount, string releasedAt = "2024-01-01")
    {
        return new Set
        {
            Code = code,
            Name = code.ToUpperInvariant(),
            SearchUri = "https://api.scryfall.com/cards/search?order=set&q=e%3A" + code,
            ReleasedAt = releasedAt,
            CardCount = cardCount,
        };
    }

    private static Card MakeCard(string name, string releasedAt)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = releasedAt,
            Layout = "normal",
            TypeLine = "Instant",
            Games = ["paper"],
            FrameEffects = [],
            Reprint = false,
            Variation = false,
            Textless = false,
            Set = "LEA",
            SetName = "Alpha",
            SetType = "core",
            BorderColor = "black",
            Cmc = 1,
            Colors = ["R"],
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}