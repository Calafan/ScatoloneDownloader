using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Disenchant, ruled 2026-09-19. The old rule wanted "destroy target artifact"
    // word for word, so every counting word in front of the noun walked past it.
    [InlineData("Dust to Dust", "Sorcery", "Exile two target artifacts.")]
    [InlineData("Builder's Bane", "Sorcery",
        "Destroy X target artifacts. Builder's Bane deals damage to each player equal to the number of "
        + "artifacts they controlled that were put into a graveyard this way.")]
    [InlineData("Aetherjacket", "Artifact Creature — Equipment",
        "Flying, vigilance\n{2}, {T}, Sacrifice this creature: Destroy another target artifact. "
        + "Activate only as a sorcery.")]
    [InlineData("Webstrike Elite", "Creature — Spider",
        "Reach\nCycling {X}{G}{G}\nWhen you cycle this card, destroy up to one target artifact or "
        + "enchantment with mana value X.")]
    // A sweeper of artifacts alone is still this tag; the Wipe reading is only
    // reached when creatures go with them.
    [InlineData("Shatterstorm", "Sorcery", "Destroy all artifacts. They can't be regenerated.")]
    [InlineData("Serenity", "Enchantment",
        "At the beginning of your upkeep, destroy all artifacts and enchantments. "
        + "They can't be regenerated.")]
    [InlineData("Tranquility", "Sorcery", "Destroy all enchantments.")]
    // An Aura is the third noun, bare.
    [InlineData("Serene Heart", "Sorcery", "Destroy all Auras.")]
    [InlineData("Hope Charm", "Instant",
        "Choose one —\n• Target creature gains first strike until end of turn.\n"
        + "• Target player gains 2 life.\n• Destroy target Aura.")]
    // "non-" carries a hyphen, which the old filler could not cross either.
    [InlineData("Emerald Charm", "Instant",
        "Choose one —\n• Untap target permanent.\n• Destroy target non-Aura enchantment.\n"
        + "• Target creature loses flying until end of turn.")]
    // An edict aimed at an artifact answers one all the same.
    [InlineData("Pick Your Poison", "Sorcery",
        "Choose one —\n• Each opponent sacrifices an artifact of their choice.\n"
        + "• Each opponent sacrifices an enchantment of their choice.\n"
        + "• Each opponent sacrifices a creature with flying of their choice.")]
    // The O-ring splits on what it NAMES, not on the exile coming back: this one
    // says "artifact", so it counts.
    [InlineData("Mystical Tether", "Enchantment",
        "You may cast this spell as though it had flash if you pay {2} more to cast it.\n"
        + "When this enchantment enters, exile target artifact or creature an opponent controls until "
        + "this enchantment leaves the battlefield.")]
    public void Classify_AnAnswerToAnArtifactOrEnchantment_IsDisenchant(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // "NONartifact" and "NONland permanent … until this ENCHANTMENT leaves the
    // battlefield" were 11 of the 15 cards the old unbounded filler caught.
    [InlineData("Terror", "Instant", "Destroy target nonartifact, nonblack creature. It can't be regenerated.")]
    [InlineData("Web Up", "Enchantment",
        "When this enchantment enters, exile target nonland permanent an opponent controls until this "
        + "enchantment leaves the battlefield.")]
    // An artifact CREATURE is a creature.
    [InlineData("Chandler", "Creature — Human", "{R}{R}{R}, {T}: Destroy target artifact creature.")]
    // A sweeper that takes the creatures too is Wipe.
    [InlineData("Jokulhaups", "Sorcery", "Destroy all artifacts, creatures, and lands. They can't be regenerated.")]
    [InlineData("Death Begets Life", "Sorcery",
        "Destroy all creatures and enchantments. Draw a card for each permanent destroyed this way.")]
    // (Savaen Elves and Miracle Worker, "an Aura attached to a named thing is
    // about that thing", stood here until 2026-11-10, "Disenchant a tutte":
    // moved to Classify_TheNovember10Ruling_AnAuraAttachedToSomething_IsDisenchant.)
    // Exile that hands the card straight back is a blink.
    [InlineData("Hide on the Ceiling", "Instant",
        "Exile X target artifacts and/or creatures. Return the exiled cards to the battlefield under "
        + "their owners' control at the beginning of the next end step.")]
    // And your own is never an answer.
    [InlineData("Rats of Rath", "Creature — Rat", "{B}: Destroy target artifact, creature, or land you control.")]
    public void Classify_WhatOnlyReadsLikeAnArtifactAnswer_IsNotDisenchant(
        string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // Readings of 2026-10-16: any number of target artifacts, the type Season of
    // Gathering lets you choose, and an artifact or enchantment shuffled into its
    // owner's library.
    [InlineData("Rampaging Yao Guai", "Creature — Bear Mutant",
        "Vigilance, trample\nThis creature enters with X +1/+1 counters on it.\nWhen this creature enters, "
        + "destroy any number of target artifacts and/or enchantments with total mana value X or less.")]
    [InlineData("Season of Gathering", "Sorcery",
        "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Put a +1/+1 "
        + "counter on a creature you control. It gains vigilance and trample until end of turn.\n{P}{P} — Choose "
        + "artifact or enchantment. Destroy all permanents of the chosen type.\n{P}{P}{P} — Draw cards equal to "
        + "the greatest power among creatures you control.")]
    [InlineData("Cathartic Parting", "Sorcery",
        "The owner of target artifact or enchantment an opponent controls shuffles it into their library. You "
        + "may shuffle up to four target cards from your graveyard into your library.")]
    public void Classify_TheOctober16DisenchantPass_IsDisenchant(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // Read 2026-11-06, each Disenchant by hand: a permanent every player may
    // choose, an artifact or an enchantment, then destroyed (Druid of
    // Purification, as Ultimate Magic: Meteor), a second 'up to one target
    // artifact' after the first target (Sword of Sinew and Steel), and the edict
    // repeated for an enchantment (Invoke Despair, as Pick Your Poison).
    [InlineData("Druid of Purification", "Creature — Human Druid",
        "When this creature enters, starting with you, each player may choose an artifact or enchantment you "
        + "don't control. Destroy each permanent chosen this way.")]
    [InlineData("Sword of Sinew and Steel", "Artifact — Equipment",
        "Equipped creature gets +2/+2 and has protection from black and from red.\nWhenever equipped creature "
        + "deals combat damage to a player, destroy up to one target planeswalker and up to one target "
        + "artifact.\nEquip {2}")]
    [InlineData("Invoke Despair", "Sorcery",
        "Target opponent sacrifices a creature of their choice. If they can't, they lose 2 life and you draw "
        + "a card. Then repeat this process for an enchantment and a planeswalker.")]
    public void Classify_TheNovember6Readings_IsDisenchant(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // A sacrifice an opponent makes on their own is a trigger's condition, not an
    // edict, and a card in a graveyard is no permanent to answer (0 of 31
    // reviewed).
    [InlineData("Vengeful Tracker", "Creature — Human Detective",
        "Whenever an opponent sacrifices an artifact, this creature deals 2 damage to them.")]
    [InlineData("Grave Robbers", "Creature — Human Rogue",
        "{B}, {T}: Exile target artifact card from a graveyard. You gain 2 life.")]
    [InlineData("Saruman of Many Colors", "Legendary Creature — Avatar Wizard",
        "Ward—Discard an enchantment, instant, or sorcery card.\nWhenever you cast your second spell each "
        + "turn, each opponent mills two cards. When one or more cards are milled this way, exile target "
        + "enchantment, instant, or sorcery card with equal or lesser mana value than that spell from an "
        + "opponent's graveyard. Copy the exiled card. You may cast the copy without paying its mana cost.")]
    public void Classify_TheOctober16DisenchantPass_IsNotDisenchant(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // Ruled 2026-11-10, 'Disenchant a tutte', overturning the 2026-09-19 reading
    // that an Aura attached to a named thing is about that thing: the Pacifism on
    // your creature, the Utopia Sprawl on a land, is somebody else's enchantment.
    // A target Aura attached to a creature or a land, or every Aura attached to a
    // target permanent, and the permanent stays (Strip Bare's creature is not
    // swept, Miracle Worker's 'you control' is the creature's).
    [InlineData("Miracle Worker", "Creature — Human Cleric",
        "{T}: Destroy target Aura attached to a creature you control.")]
    [InlineData("Savaen Elves", "Creature — Elf",
        "{G}{G}, {T}: Destroy target Aura attached to a land.")]
    [InlineData("Pyramids", "Artifact",
        "{2}: Choose one —\n• Destroy target Aura attached to a land.\n• The next time target land would be "
        + "destroyed this turn, remove all damage marked on it instead.")]
    [InlineData("Strip Bare", "Instant",
        "Destroy all Auras and Equipment attached to target creature.")]
    [InlineData("Street Sweeper", "Artifact Creature — Construct",
        "Whenever this creature attacks, destroy all Auras attached to target land.")]
    public void Classify_TheNovember10Ruling_AnAuraAttachedToSomething_IsDisenchant(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // The same ruling keeps out the Auras that leave WITH the permanent they are
    // on (Silence the Believers, 'attached to them'; Hakim, attached to itself)
    // and the ones a fight reaches with no target (Treefolk Mystic, 'that
    // creature'): the card answers the permanent, or nothing.
    [InlineData("Silence the Believers", "Instant",
        "Strive — This spell costs {2}{B} more to cast for each target beyond the first.\nExile any number of "
        + "target creatures and all Auras attached to them.")]
    [InlineData("Hakim, Loreweaver", "Legendary Creature — Human Wizard",
        "Flying\n{U}{U}: Return target Aura card from your graveyard to the battlefield attached to Hakim. "
        + "Activate only during your upkeep and only if Hakim isn't enchanted.\n{U}{U}, {T}: Destroy all Auras "
        + "attached to Hakim.")]
    [InlineData("Treefolk Mystic", "Creature — Treefolk",
        "Whenever this creature blocks or becomes blocked by a creature, destroy all Auras attached to that "
        + "creature.")]
    public void Classify_TheNovember10Ruling_AurasThatGoWithTheirPermanent_IsNotDisenchant(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }

    [Theory]
    // Ruled 2026-11-10, 'nessun sottotipo', 'risposta stretta': Equipment,
    // Vehicle, Room or Curse is a niche answer to one kind of permanent, 0 of 6
    // reviewed tagged; the Aura stays because it is the shape in which an
    // enchantment takes a creature out.
    [InlineData("Molten Rebuke", "Sorcery",
        "Choose one or both —\n• Molten Rebuke deals 5 damage to target creature or planeswalker.\n• Destroy "
        + "target Equipment.")]
    [InlineData("Anthropede", "Creature — Insect",
        "Reach\nWhen this creature enters, you may discard a card or pay {2}. When you do, destroy target "
        + "Room.")]
    public void Classify_TheNovember10Ruling_ASubtypeOtherThanAura_IsNotDisenchant(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Disenchant));
    }
}
