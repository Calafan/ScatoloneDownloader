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

    [Theory]
    // The definition of 2026-11-08 (Vindicate): somebody else's permanent of any
    // type, chosen by you, taken off the battlefield for good. A nonland permanent
    // phased out for as long as you control the card is an O-ring on any
    // permanent, RULED that day: The Phasing of Zhalfir, Removal by hand,
    // corrected by name to RemovePermanent alone (Wipe stays for its chapter III).
    [InlineData("The Phasing of Zhalfir", "Enchantment — Saga",
        "Read ahead (Choose a chapter and start with that many lore counters. Add one after your draw step. "
        + "Skipped chapters don't trigger. Sacrifice after III.)\nI, II — Another target nonland permanent "
        + "phases out. It can't phase in for as long as you control this Saga.\nIII — Destroy all creatures. For "
        + "each creature destroyed this way, its controller creates a 2/2 black Phyrexian creature token.")]
    public void Classify_TheNovember8Definition_APermanentOfTheirsPhasedOutForGood_IsRemovePermanent(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.RemovePermanent));
        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // RULED 2026-11-08: a nonland permanent exiled while its owner may play or
    // cast it is a bounce with a tax, like airbend and Prowl. Soul Partition was
    // RemovePermanent by hand, corrected by name; Aerial Extortionist was Bounce
    // by hand already.
    [InlineData("Soul Partition", "Instant",
        "Exile target nonland permanent. For as long as that card remains exiled, its owner may play it. A "
        + "spell cast by an opponent this way costs {2} more to cast.")]
    [InlineData("Aerial Extortionist", "Creature — Bird Soldier",
        "Flying\nWhenever this creature enters or deals combat damage to a player, exile up to one target "
        + "nonland permanent. For as long as that card remains exiled, its owner may cast it.\nWhenever another "
        + "player casts a spell from anywhere other than their hand, draw a card.")]
    public void Classify_TheNovember8Definition_AnExileItsOwnerMayPlay_IsBounceNotRemovePermanent(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Bounce));
        Assert.False(result.HasFlag(CardEffect.RemovePermanent));
    }

    [Theory]
    // RULED 2026-11-08: you choose the permanent, so an edict where THEY choose
    // stays out, as on 2026-10-15 and 2026-10-27. Shard of the Void Dragon and
    // Lord Xander were RemovePermanent by hand, corrected by name; Throne of the
    // Grim Captain says the same as Shard and was untagged.
    [InlineData("Shard of the Void Dragon", "Creature — C'tan",
        "Flying\nSpear of the Void Dragon — Whenever this creature attacks, each opponent sacrifices a nonland "
        + "permanent of their choice.\nMatter Absorption — Whenever an artifact is put into a graveyard from the "
        + "battlefield or is put into exile from the battlefield, put two +1/+1 counters on this creature.")]
    [InlineData("Lord Xander, the Collector", "Legendary Creature — Vampire Demon Noble",
        "When Lord Xander enters, target opponent discards half the cards in their hand, rounded "
        + "down.\nWhenever Lord Xander attacks, defending player mills half their library, rounded down.\nWhen "
        + "Lord Xander dies, target opponent sacrifices half the nonland permanents they control of their "
        + "choice, rounded down.")]
    [InlineData("Throne of the Grim Captain // The Grim Captain", "Legendary Artifact // Legendary Creature — Skeleton Spirit Pirate",
        "{T}: Mill two cards.\nCraft with a Dinosaur, a Merfolk, a Pirate, and a Vampire {4} ({4}, Exile this "
        + "artifact, Exile the four from among permanents you control and/or cards in your graveyard: Return "
        + "this card transformed under its owner's control. Craft only as a sorcery.)\nMenace, trample, "
        + "lifelink, hexproof\nWhenever The Grim Captain attacks, each opponent sacrifices a nonland permanent "
        + "of their choice. Then you may put an exiled creature card used to craft The Grim Captain onto the "
        + "battlefield under your control tapped and attacking.")]
    // Braids, Arisen Nightmare, corrected by name the same day: the opponent MAY
    // sacrifice, a punisher, which they choose too.
    [InlineData("Braids, Arisen Nightmare", "Legendary Creature — Nightmare",
        "At the beginning of your end step, you may sacrifice an artifact, creature, enchantment, land, or "
        + "planeswalker. If you do, each opponent may sacrifice a permanent of their choice that shares a card "
        + "type with it. For each opponent who doesn't, that player loses 2 life and you draw a card.")]
    public void Classify_TheNovember8Definition_AnEdictTheyChoose_IsNotRemovePermanent(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.RemovePermanent));
    }

    [Theory]
    // Read 2026-11-12: a colour count qualifies the permanent like 'legendary'.
    [InlineData("Vanishing Verse", "Instant",
        "Exile target monocolored permanent.")]
    public void Classify_TheNovember12Readings_AMonocoloredPermanent_IsRemovePermanent(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.RemovePermanent));
    }
}
