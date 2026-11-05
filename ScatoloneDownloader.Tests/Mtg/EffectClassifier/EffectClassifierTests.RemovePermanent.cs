using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // An answer that can point at ANY permanent is RemovePermanent, even when it
    // will usually be pointed at a creature. Web Up and Stormplain Detainment say
    // the same sentence, and reading only the bare "target permanent" left most
    // of the O-ring family untouched. Ruled 2026-09-15.
    [InlineData("Vindicate", "Sorcery", "Destroy target permanent.")]
    [InlineData("Web Up", "Enchantment",
        "When this enchantment enters, exile target nonland permanent an opponent controls until this enchantment leaves the battlefield.")]
    [InlineData("Unyielding Gatekeeper", "Creature — Elephant Cleric",
        "When this creature is turned face up, exile another target nonland permanent.")]
    [InlineData("Hide in Plain Sight", "Instant", "Exile up to one target nonland permanent.")]
    // A COLOUR-restricted permanent kill is the same reading: the breadth is the
    // point. Ruled 2026-09-20, having spent a day on Removal instead.
    [InlineData("Northern Paladin", "Creature — Human Knight",
        "{W}{W}, {T}: Destroy target black permanent.")]
    [InlineData("Active Volcano", "Instant",
        "Choose one —\n• Destroy target blue permanent.\n• Return target Island to its owner's hand.")]
    public void Classify_AnsweringAnyPermanent_IsRemovePermanent(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.RemovePermanent));
    }

    [Theory]
    // Guff Rewrites History's targets shuffled into their owners' libraries,
    // RemovePermanent by hand (2026-10-29) like Audacious Swap.
    [InlineData("Guff Rewrites History", "Instant",
        "For each player, choose target nonenchantment, nonland permanent that player controls. Those "
        + "permanents' owners shuffle them into their libraries. Each player who controlled one of those "
        + "permanents exiles cards from the top of their library until they exile a nonland card, then puts the "
        + "rest on the bottom of their library in a random order. Each player may cast the nonland card they "
        + "exiled without paying its mana cost.")]
    public void Classify_PermanentsShuffledAwayOnePerPlayer_IsRemovePermanent(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.RemovePermanent));
    }

    [Theory]
    // A permanent shuffled into its owner's library, ruled RemovePermanent alone
    // on 2026-10-27 ('Solo RemovePermanent': Audacious Swap, corrected by name);
    // Chaos Warp reads the same.
    [InlineData("Audacious Swap", "Instant",
        "Casualty 2 (As you cast this spell, you may sacrifice a creature with power 2 or greater. When you "
        + "do, copy this spell and you may choose a new target for the copy.)\nThe owner of target "
        + "nonenchantment permanent shuffles it into their library, then exiles the top card of their library. "
        + "If it's a land card, they put it onto the battlefield. Otherwise, they may cast it without paying "
        + "its mana cost.")]
    [InlineData("Chaos Warp", "Instant",
        "The owner of target permanent shuffles it into their library, then reveals the top card of their "
        + "library. If it's a permanent card, they put it onto the battlefield.")]
    public void Classify_TheOctober27RemovalPass_APermanentShuffledAway_IsRemovePermanent(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.RemovePermanent));
    }

    [Theory]
    // Read 2026-11-05: you choose a nonland permanent among each opponent's and a
    // die picks which one is destroyed (Chaos Defiler, RemovePermanent by hand).
    // Not the plain edict where they choose.
    [InlineData("Chaos Defiler", "Artifact Creature — Demon Construct",
        "Trample\nBattle Cannon — When this creature enters or dies, for each opponent, choose a nonland "
        + "permanent that player controls. Destroy one of them chosen at random.")]
    public void Classify_TheNovember5RemovalPass_ARandomOneOfTheirChosen_IsRemovePermanent(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.RemovePermanent));
    }
}
