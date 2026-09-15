using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed class EffectGlossaryTests
{
    public static TheoryData<CardEffect> EveryEffect()
    {
        TheoryData<CardEffect> data = [];
        foreach (CardEffect effect in Enum.GetValues<CardEffect>())
        {
            if (effect != CardEffect.None)
            {
                data.Add(effect);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryEffect))]
    public void EveryEffect_HasALine(CardEffect effect)
    {
        // The same guarantee the hotkey check makes at startup: a new member must
        // not reach the tagger with a blank tooltip, because a blank tooltip is
        // exactly where a slip comes from.
        Assert.False(string.IsNullOrWhiteSpace(EffectGlossary.Describe(effect)), $"{effect} has no glossary line.");
    }

    [Theory]
    [MemberData(nameof(EveryEffect))]
    public void EveryLine_IsShortEnoughToRead(CardEffect effect)
    {
        // A tooltip nobody finishes reading is a tooltip nobody reads.
        Assert.InRange(EffectGlossary.Describe(effect).Length, 40, 340);
    }

    [Fact]
    public void DescribeAll_KeepsTheOrderItWasGiven_SoThePageCanIndexIt()
    {
        string[] lines = EffectGlossary.DescribeAll(["Mill", "Burn", "Tokens"]);

        Assert.Equal(EffectGlossary.Describe(CardEffect.Mill), lines[0]);
        Assert.Equal(EffectGlossary.Describe(CardEffect.Burn), lines[1]);
        Assert.Equal(EffectGlossary.Describe(CardEffect.Tokens), lines[2]);
    }

    [Fact]
    public void DescribeAll_YieldsAnEmptyLineForANameItDoesNotKnow()
    {
        // A tooltip is never load-bearing: an unknown name must not throw and
        // must not shift the alignment of the ones around it.
        string[] lines = EffectGlossary.DescribeAll(["Mill", "NotAnEffect", "Burn"]);

        Assert.Equal(3, lines.Length);
        Assert.Equal(string.Empty, lines[1]);
        Assert.Equal(EffectGlossary.Describe(CardEffect.Burn), lines[2]);
    }
}
