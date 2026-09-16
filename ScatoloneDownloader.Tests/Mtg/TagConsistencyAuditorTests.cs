using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed class TagConsistencyAuditorTests
{
    private static TagConsistencyAuditor.AuditedCard Card(string name, string oracle, CardEffect effects) =>
        new(name.ToLowerInvariant(), name, oracle, effects);

    [Fact]
    public void Signature_IgnoresTheCardsOwnName()
    {
        // Two Tims. The only difference in the text is which name is written in it.
        string a = TagConsistencyAuditor.Signature(
            "Prodigal Sorcerer", "{T}: Prodigal Sorcerer deals 1 damage to any target.");
        string b = TagConsistencyAuditor.Signature(
            "Zuran Spellcaster", "{T}: Zuran Spellcaster deals 1 damage to any target.");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Signature_IgnoresReminderTextAndManaSymbols()
    {
        string withReminder = TagConsistencyAuditor.Signature(
            "A", "Flying (This creature can't be blocked except by creatures with flying.)\n{R}: This creature gets +1/+0 until end of turn.");
        string without = TagConsistencyAuditor.Signature(
            "B", "Flying\n{2}{R}: This creature gets +1/+0 until end of turn.");

        Assert.Equal(withReminder, without);
    }

    [Fact]
    public void Signature_IgnoresHowBigTheNumbersAre()
    {
        // Giant Growth and Monstrous Growth are the same card at different sizes.
        Assert.Equal(
            TagConsistencyAuditor.Signature("Giant Growth", "Target creature gets +3/+3 until end of turn."),
            TagConsistencyAuditor.Signature("Monstrous Growth", "Target creature gets +4/+4 until end of turn."));
    }

    [Fact]
    public void Signature_KeepsZero_BecauseShrinkingToughnessCanKillAndShrinkingPowerCannot()
    {
        // Grandmother Sengir is Removal and Pradesh Gypsies is not, which is a
        // real distinction: -1 toughness kills a 1/1, -0 toughness kills nothing.
        Assert.NotEqual(
            TagConsistencyAuditor.Signature("Grandmother Sengir", "{1}{B}, {T}: Target creature gets -1/-1 until end of turn."),
            TagConsistencyAuditor.Signature("Pradesh Gypsies", "{1}{G}, {T}: Target creature gets -2/-0 until end of turn."));

        // Overkill kills outright; Shrink only takes power away.
        Assert.NotEqual(
            TagConsistencyAuditor.Signature("Overkill", "Target creature gets -0/-9999 until end of turn."),
            TagConsistencyAuditor.Signature("Shrink", "Target creature gets -5/-0 until end of turn."));
    }

    [Fact]
    public void Signature_StillIgnoresSizeAwayFromZero()
    {
        // The point of keeping zero is not to start caring how big a number is.
        Assert.Equal(
            TagConsistencyAuditor.Signature("Adarkar Sentinel", "{1}: This creature gets +0/+1 until end of turn."),
            TagConsistencyAuditor.Signature("Carrion Ants", "{1}: This creature gets +0/+3 until end of turn."));
    }

    [Fact]
    public void Signature_KeepsOneApartFromMany_WhenItCountsCards()
    {
        // Ruled 2026-09-16: "look at N, put ONE into your hand" is Filter and
        // "put TWO" is CardAdvantage, so this tool reported Impulse against Stock
        // Up as a contradiction when it flattened both counts to the same mark.
        Assert.NotEqual(
            TagConsistencyAuditor.Signature("Impulse",
                "Look at the top four cards of your library. Put one of them into your hand and the rest on the bottom."),
            TagConsistencyAuditor.Signature("Stock Up",
                "Look at the top five cards of your library. Put two of them into your hand and the rest on the bottom."));
    }

    [Fact]
    public void Signature_StillIgnoresOne_WhenItIsASize()
    {
        // The distinction is only drawn where the number counts CARDS. One point
        // of toughness is a size like any other, and splitting those apart is the
        // noise the flattening exists to remove.
        Assert.Equal(
            TagConsistencyAuditor.Signature("Adarkar Sentinel", "{1}: This creature gets +0/+1 until end of turn."),
            TagConsistencyAuditor.Signature("Carrion Ants", "{1}: This creature gets +0/+3 until end of turn."));
    }

    [Fact]
    public void Signature_KeepsTheSign_BecausePumpAndShrinkAreOppositeCards()
    {
        Assert.NotEqual(
            TagConsistencyAuditor.Signature("Giant Growth", "Target creature gets +3/+3 until end of turn."),
            TagConsistencyAuditor.Signature("Weakness", "Target creature gets -2/-1 until end of turn."));
    }

    [Fact]
    public void Signature_KeepsTheActivationCost_BecauseTheCostIsWhatTheCardIs()
    {
        // A sacrifice outlet and a mana-activated pump do different things, even
        // though everything after the colon matches.
        Assert.NotEqual(
            TagConsistencyAuditor.Signature("Atog", "Sacrifice an artifact: This creature gets +2/+2 until end of turn."),
            TagConsistencyAuditor.Signature("Frozen Shade", "{B}: This creature gets +1/+1 until end of turn."));
    }

    [Fact]
    public void Audit_ReportsOnlyGroupsThatWereTaggedMoreThanOneWay()
    {
        var cards = new[]
        {
            Card("Rootwater Hunter", "{T}: Rootwater Hunter deals 1 damage to any target.", CardEffect.Removal | CardEffect.Burn),
            Card("Zuran Spellcaster", "{T}: Zuran Spellcaster deals 1 damage to any target.", CardEffect.Removal | CardEffect.Burn),
            Card("Prodigal Sorcerer", "{T}: Prodigal Sorcerer deals 1 damage to any target.", CardEffect.Burn),
            // Agrees with itself, so it must not be reported.
            Card("Unsummon", "Return target creature to its owner's hand.", CardEffect.Bounce),
            Card("Boomerang", "Return target permanent to its owner's hand.", CardEffect.Bounce),
        };

        var found = TagConsistencyAuditor.Audit(cards);

        TagConsistencyAuditor.Disagreement only = Assert.Single(found);
        Assert.Equal(3, only.Cards.Count);
        Assert.Contains(only.Cards, c => c.Name == "Prodigal Sorcerer");
    }

    [Fact]
    public void Majority_NamesTheTagsMostOfTheGroupShares_SoTheOddOneOutIsVisible()
    {
        var cards = new[]
        {
            // The real wording: the Batteries say "this artifact", never their
            // own name, so all three reduce to the same signature.
            Card("Black Mana Battery", "{2}, {T}: Put a charge counter on this artifact.\n{T}, Remove any number of charge counters from this artifact: Add {B} for each counter removed this way.", CardEffect.Ramp),
            Card("Blue Mana Battery", "{2}, {T}: Put a charge counter on this artifact.\n{T}, Remove any number of charge counters from this artifact: Add {U} for each counter removed this way.", CardEffect.Ramp),
            Card("Red Mana Battery", "{2}, {T}: Put a charge counter on this artifact.\n{T}, Remove any number of charge counters from this artifact: Add {R} for each counter removed this way.", CardEffect.None),
        };

        TagConsistencyAuditor.Disagreement group = Assert.Single(TagConsistencyAuditor.Audit(cards));

        Assert.Equal(CardEffect.Ramp, group.Majority);
    }

    [Fact]
    public void Majority_IsNullOnAnEvenSplit_BecauseThatIsABoundaryNotASlip()
    {
        var cards = new[]
        {
            Card("Death Ward", "Regenerate target creature.", CardEffect.Protection),
            Card("Village Elder", "Regenerate target creature.", CardEffect.None),
        };

        TagConsistencyAuditor.Disagreement group = Assert.Single(TagConsistencyAuditor.Audit(cards));

        Assert.Null(group.Majority);
    }

    [Fact]
    public void Audit_SkipsTextTooShortToMeanAnything()
    {
        // Two vanilla fliers tagged differently is noise, not a finding: every
        // keyword-only creature in the set would group with them.
        var cards = new[]
        {
            Card("Bird A", "Flying", CardEffect.None),
            Card("Bird B", "Flying", CardEffect.Buff),
        };

        Assert.Empty(TagConsistencyAuditor.Audit(cards));
    }
}
