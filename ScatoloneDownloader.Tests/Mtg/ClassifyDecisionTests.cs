using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Pins the classifier's propose-not-decide contract via the pure
/// <see cref="ClassifyDecision.Decide"/>: it suggests effects for unreviewed,
/// empty entries, never touches a human-reviewed entry, respects
/// <c>--overwrite</c> for already-tagged (still-unreviewed) entries, and never
/// implies a reviewedAt stamp.
/// </summary>
public sealed class ClassifyDecisionTests
{
    private static readonly Card Bolt = MakeCard("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.");
    private static readonly Card Vanilla = MakeCard("Grizzly Bears", "Creature — Bear", "");

    [Fact]
    public void Decide_UnreviewedEmptyEntry_ClassifiableCard_ProposesEffects()
    {
        CardMetadataEntry entry = new() { Name = "Lightning Bolt" };

        (ClassifyOutcome outcome, CardEffect proposed) = ClassifyDecision.Decide(entry, Bolt, overwrite: false);

        Assert.Equal(ClassifyOutcome.Classified, outcome);
        Assert.Equal(CardEffect.Removal | CardEffect.Burn, proposed);
    }

    [Fact]
    public void Decide_ReviewedEntry_NeverTouched_EvenIfClassifiable()
    {
        CardMetadataEntry entry = new()
        {
            Name = "Lightning Bolt",
            ReviewedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        (ClassifyOutcome outcome, CardEffect proposed) = ClassifyDecision.Decide(entry, Bolt, overwrite: true);

        Assert.Equal(ClassifyOutcome.Reviewed, outcome);
        Assert.Equal(CardEffect.None, proposed);
    }

    [Fact]
    public void Decide_AlreadyTagged_NoOverwrite_Kept()
    {
        CardMetadataEntry entry = new() { Name = "Lightning Bolt", EffectFlags = CardEffect.Removal };

        (ClassifyOutcome outcome, _) = ClassifyDecision.Decide(entry, Bolt, overwrite: false);

        Assert.Equal(ClassifyOutcome.AlreadyTagged, outcome);
    }

    [Fact]
    public void Decide_AlreadyTagged_Overwrite_ReProposes()
    {
        // Unreviewed but already auto-tagged: --overwrite re-proposes from text.
        // The stale tag is deliberately one Bolt's text cannot produce, so the
        // assertion still proves the proposal was REDERIVED rather than kept.
        CardMetadataEntry entry = new() { Name = "Lightning Bolt", EffectFlags = CardEffect.Tokens };

        (ClassifyOutcome outcome, CardEffect proposed) = ClassifyDecision.Decide(entry, Bolt, overwrite: true);

        Assert.Equal(ClassifyOutcome.Classified, outcome);
        Assert.Equal(CardEffect.Removal | CardEffect.Burn, proposed);
    }

    [Fact]
    public void Decide_StaleTagsTheRulesNoLongerProduce_AreClearedUnderOverwrite()
    {
        // The case that made this necessary: Protection was narrowed to
        // instant-speed effects, so a vanilla creature carrying an old Protection
        // guess has to lose it. Without this the tag would survive every future
        // classify run and no rule in the codebase would stand behind it.
        CardMetadataEntry entry = new() { Name = "Grizzly Bears", EffectFlags = CardEffect.Protection };

        (ClassifyOutcome outcome, CardEffect proposed) = ClassifyDecision.Decide(entry, Vanilla, overwrite: true);

        Assert.Equal(ClassifyOutcome.Cleared, outcome);
        Assert.Equal(CardEffect.None, proposed);
    }

    [Fact]
    public void Decide_StaleTags_SurviveWithoutOverwrite()
    {
        // Clearing is an --overwrite behaviour only: the default run must stay the
        // conservative "fill the blanks" pass it has always been.
        CardMetadataEntry entry = new() { Name = "Grizzly Bears", EffectFlags = CardEffect.Protection };

        (ClassifyOutcome outcome, _) = ClassifyDecision.Decide(entry, Vanilla, overwrite: false);

        Assert.Equal(ClassifyOutcome.AlreadyTagged, outcome);
    }

    [Fact]
    public void Decide_ReviewedEntryWithStaleTags_IsStillNeverTouched()
    {
        // Clearing must never reach a human decision, even a stale-looking one.
        CardMetadataEntry entry = new()
        {
            Name = "Grizzly Bears",
            EffectFlags = CardEffect.Protection,
            ReviewedAt = DateTimeOffset.UtcNow,
        };

        (ClassifyOutcome outcome, _) = ClassifyDecision.Decide(entry, Vanilla, overwrite: true);

        Assert.Equal(ClassifyOutcome.Reviewed, outcome);
    }

    [Fact]
    public void Decide_UnclassifiableCard_NoProposal()
    {
        CardMetadataEntry entry = new() { Name = "Grizzly Bears" };

        (ClassifyOutcome outcome, CardEffect proposed) = ClassifyDecision.Decide(entry, Vanilla, overwrite: false);

        Assert.Equal(ClassifyOutcome.NoProposal, outcome);
        Assert.Equal(CardEffect.None, proposed);
    }

    private static Card MakeCard(string name, string typeLine, string oracleText) =>
        TestCards.Make(name, typeLine, oracleText);
}
