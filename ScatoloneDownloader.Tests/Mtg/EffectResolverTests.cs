using System.Collections.Generic;

using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Covers <see cref="EffectResolver"/>: exact member-name parsing, human
/// aliases (including the Steal / threaten family), numeric rejection, unknown
/// skipping, and stable declared-order serialization.
/// </summary>
public sealed class EffectResolverTests
{
    [Theory]
    [InlineData("Steal", CardEffect.Steal)]
    [InlineData("steal", CardEffect.Steal)]           // case-insensitive member name
    [InlineData("threaten", CardEffect.Steal)]
    [InlineData("act of treason", CardEffect.Steal)]
    [InlineData("control magic", CardEffect.Steal)]
    [InlineData("mind control", CardEffect.Steal)]
    [InlineData("board wipe", CardEffect.Wipe)]
    [InlineData("vindicate", CardEffect.RemovePermanent)]
    [InlineData("direct damage", CardEffect.Burn)]
    [InlineData("Tutor", CardEffect.Tutor)]
    [InlineData("search library", CardEffect.Tutor)]
    [InlineData("search your library", CardEffect.Tutor)]
    [InlineData("Fixing", CardEffect.Fixing)]
    [InlineData("mana fixing", CardEffect.Fixing)]
    [InlineData("colour fixing", CardEffect.Fixing)]
    [InlineData("Pacify", CardEffect.Pacify)]
    [InlineData("pacifism", CardEffect.Pacify)]
    [InlineData("arrest", CardEffect.Pacify)]
    [InlineData("detain", CardEffect.Pacify)]
    [InlineData("tapper", CardEffect.Pacify)]
    public void TryParseSingle_MemberNamesAndAliases_Resolve(string raw, CardEffect expected)
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
    public void TryParseSingle_InvalidOrNumeric_ReturnsFalse(string raw)
    {
        Assert.False(EffectResolver.TryParseSingle(raw, out CardEffect effect));
        Assert.Equal(CardEffect.None, effect);
    }

    [Fact]
    public void Parse_CombinesFlags_AndSkipsUnknown()
    {
        CardEffect result = EffectResolver.Parse(["Ramp", "not-real", "threaten"]);

        Assert.Equal(CardEffect.Ramp | CardEffect.Steal, result);
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
