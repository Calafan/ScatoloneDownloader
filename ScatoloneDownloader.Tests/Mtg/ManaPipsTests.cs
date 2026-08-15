using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Phase 0 TDD: ManaPips parser — extracts colored pip count from Scryfall
/// mana_cost strings like "{2}{R}{R}" → 2 colored pips (R, R), ignoring generic
/// {N} and {X}. Used for pip density (colored commitment vs total CMC).
/// </summary>
public sealed class ManaPipsTests
{
    [Theory]
    [InlineData("{R}", 1)]
    [InlineData("{R}{R}", 2)]
    [InlineData("{2}{R}{R}", 2)]
    [InlineData("{1}{U}", 1)]
    [InlineData("{X}{R}{R}", 2)]
    [InlineData("{W}{U}{B}{R}{G}", 5)]
    [InlineData("{2}{G}{G}{G}", 3)]
    [InlineData("{0}", 0)]
    [InlineData("{1}", 0)]
    [InlineData("{X}", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void CountColoredPips_ReturnsColoredPipCount(string? manaCost, int expected)
    {
        Assert.Equal(expected, ManaPipsParser.CountColoredPips(manaCost));
    }

    [Theory]
    [InlineData("{R}", new[] { "R" })]
    [InlineData("{R}{R}", new[] { "R", "R" })]
    [InlineData("{2}{R}{R}", new[] { "R", "R" })]
    [InlineData("{W}{U}", new[] { "W", "U" })]
    [InlineData("{X}{B}{B}", new[] { "B", "B" })]
    [InlineData("{0}", new string[] { })]
    public void GetColoredPips_ReturnsPipList(string? manaCost, string[] expected)
    {
        Assert.Equal(expected, ManaPipsParser.GetColoredPips(manaCost));
    }
}