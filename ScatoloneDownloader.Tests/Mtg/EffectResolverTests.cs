using System.Collections.Generic;

using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Covers <see cref="EffectResolver"/>: exact member-name parsing (case-insensitive),
/// numeric rejection, unknown skipping, and stable declared-order serialization.
/// </summary>
public sealed class EffectResolverTests
{
    [Theory]
    [InlineData("Steal", CardEffect.Steal)]
    [InlineData("steal", CardEffect.Steal)]           // case-insensitive member name
    [InlineData("Tutor", CardEffect.Tutor)]
    [InlineData("ManaFixing", CardEffect.ManaFixing)]
    [InlineData("Pacify", CardEffect.Pacify)]
    public void TryParseSingle_MemberNames_Resolve(string raw, CardEffect expected)
    {
        Assert.True(EffectResolver.TryParseSingle(raw, out CardEffect effect));
        Assert.Equal(expected, effect);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("3")]          // pure number must NOT be read as a bit combination
    [InlineData("65536")]      // Steal's numeric value must NOT parse either
    [InlineData("None")]       // None is not a real effect
    [InlineData("not an effect")]
    [InlineData("threaten")]   // former alias: the canonical name is the only spelling read
    public void TryParseSingle_InvalidOrNumeric_ReturnsFalse(string raw)
    {
        Assert.False(EffectResolver.TryParseSingle(raw, out CardEffect effect));
        Assert.Equal(CardEffect.None, effect);
    }

    [Fact]
    public void Parse_CombinesFlags_AndSkipsUnknown()
    {
        CardEffect result = EffectResolver.Parse(["Ramp", "not-real", "steal"]);

        Assert.Equal(CardEffect.Ramp | CardEffect.Steal, result);
    }

    [Fact]
    public void ToNames_ManaFixing_SerializesUnderItsNewCanonicalName()
    {
        // The rename is what the tagger and the tier files show; the old "Fixing"
        // spelling no longer parses and never comes back on write.
        Assert.Equal(["ManaFixing"], EffectResolver.ToNames(CardEffect.ManaFixing));
        Assert.Equal(
            CardEffect.ManaFixing,
            EffectResolver.Parse(EffectResolver.ToNames(EffectResolver.Parse(["ManaFixing"]))));
    }

    [Fact]
    public void ToNames_IncludesSteal_InDeclaredBitOrder()
    {
        List<string> names = EffectResolver.ToNames(CardEffect.Steal | CardEffect.Ramp | CardEffect.Burn);

        // Declared (ascending bit) order: Ramp (1<<6), Burn (1<<14), Steal (1<<16).
        Assert.Equal(["Ramp", "Burn", "Steal"], names);
    }

    [Fact]
    public void ToNames_RoundTripsThroughParse()
    {
        CardEffect original = CardEffect.Steal | CardEffect.Reanimate | CardEffect.CardAdvantage;

        Assert.Equal(original, EffectResolver.Parse(EffectResolver.ToNames(original)));
    }
}
