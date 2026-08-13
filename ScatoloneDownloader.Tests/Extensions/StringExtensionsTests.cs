using System;

using ScatoloneDownloader.Extensions;

using Xunit;

namespace ScatoloneDownloader.Tests.Extensions;

/// <summary>
/// Tiny util used by <see cref="Mtg.CardAnalyzer"/> for pluralized headers —
/// fail loudly here so the analyzer output never silently goes lowercase.
/// </summary>
public sealed class StringExtensionsTests
{
    [Theory]
    [InlineData("creature", "Creature")]
    [InlineData("lands", "Lands")]
    [InlineData("sorceries", "Sorceries")]
    [InlineData("enchantment", "Enchantment")]
    [InlineData("PLANESWALKER", "PLANESWALKER")]
    [InlineData("artifact", "Artifact")]
    [InlineData("instant", "Instant")]
    [InlineData("a", "A")]
    public void Capitalize_UpperCasesFirstLetter_LeavesRest(string input, string expected)
    {
        Assert.Equal(expected, input.Capitalize());
    }

    [Theory]
    [InlineData(null)]
    public void Capitalize_ThrowsOnNull(string? input)
    {
        Assert.Throws<ArgumentNullException>(() => input!.Capitalize());
    }

    [Fact]
    public void Capitalize_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => "".Capitalize());
    }

    [Fact]
    public void Capitalize_AlreadyCapitalized_StaysCapitalized()
    {
        Assert.Equal("Lightning", "Lightning".Capitalize());
    }

    [Fact]
    public void Capitalize_Multibyte_UpperCasesFirstRune()
    {
        // "ñ" has an uppercase mapping ("Ñ") in the invariant culture; the
        // extension uses char.ToUpperInvariant via string.Concat(First().ToUpper()).
        Assert.Equal("Ñ", "ñ".Capitalize());
    }
}