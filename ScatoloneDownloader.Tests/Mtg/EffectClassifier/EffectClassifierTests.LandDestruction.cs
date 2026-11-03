using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // "island" and "islandwalk" contain the letters of "land", so the patterns
    // require a word boundary before it. Without that, every islandwalk creature
    // would be proposed as land destruction.
    [InlineData("Deep Spawn", "Creature — Kraken", "Islandwalk")]
    [InlineData("Sea Serpent", "Creature — Serpent", "Sea Serpent can't attack unless defending player controls an Island.")]
    // Both of these were caught by auditing already-reviewed cards against a
    // looser first draft of the rules. Pyramids PROTECTS lands, and the thing it
    // destroys is an Aura; Serendib Djinn's land sacrifice is a drawback its own
    // controller pays, not an effect aimed at an opponent.
    [InlineData("Pyramids", "Artifact", "{2}: Destroy target Aura attached to a land.\n{2}: The next time target land would be destroyed this turn, remove all damage marked on it instead.")]
    [InlineData("Serendib Djinn", "Creature — Djinn", "Flying\nAt the beginning of your upkeep, sacrifice a land. If you sacrifice an Island this way, this creature deals 3 damage to you.")]
    public void Classify_LandWordings_ThatAreNotLandDestruction(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // A sweep that takes ONLY lands answers no creature and is LandDestruction
    // alone, ruled 2026-10-17 ("ribaltiamo il lavoro sui soli LandDestruction
    // Wipe"): all lands or all of a basic type destroyed, exiled or returned,
    // every land sharing the name, a pile of lands kept, and two or more each,
    // all, or one per card, sacrificed. These were pinned as Wipe from
    // 2026-10-16, ruled on Omen of Fire against Boil, until that day.
    [InlineData("Boil", "Instant",
        "Destroy all Islands.")]
    [InlineData("Flashfires", "Sorcery",
        "Destroy all Plains.")]
    [InlineData("Ajani Vengeant", "Legendary Planeswalker — Ajani",
        "+1: Target permanent doesn't untap during its controller's next untap step.\n−2: Ajani deals 3 damage "
        + "to any target and you gain 3 life.\n−7: Destroy all lands target player controls.")]
    [InlineData("Sunder", "Instant",
        "Return all lands to their owners' hands.")]
    [InlineData("Wake of Destruction", "Sorcery",
        "Destroy target land and all other lands with the same name as that land.")]
    [InlineData("Natural Balance", "Sorcery",
        "Each player who controls six or more lands chooses five lands they control and sacrifices the rest. "
        + "Each player who controls four or fewer lands may search their library for up to X basic land cards "
        + "and put them onto the battlefield, where X is five minus the number of lands they control. Then each "
        + "player who searched their library this way shuffles.")]
    [InlineData("Global Ruin", "Sorcery",
        "Each player chooses from the lands they control a land of each basic land type, then sacrifices the "
        + "rest.")]
    [InlineData("Keldon Firebombers", "Creature — Human Soldier",
        "When this creature enters, each player sacrifices all lands they control except for three.")]
    [InlineData("Tectonic Break", "Sorcery",
        "Each player sacrifices X lands of their choice.")]
    [InlineData("Wave of Vitriol", "Sorcery",
        "Each player sacrifices all artifacts, enchantments, and nonbasic lands they control. For each land "
        + "sacrificed this way, its controller may search their library for a basic land card and put it onto "
        + "the battlefield tapped. Then each player who searched their library this way shuffles.")]
    [InlineData("Thoughts of Ruin", "Sorcery",
        "Each player sacrifices a land of their choice for each card in your hand.")]
    public void Classify_ALandSweep_IsLandDestructionNotWipe(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.LandDestruction));
        Assert.False(result.HasFlag(CardEffect.Wipe));
    }

    [Fact]
    public void Classify_ManaDenial_WithoutDestroyingALand_IsNotProposed()
    {
        // The tag is deliberately narrow: Winter Orb attacks the mana base but
        // destroys nothing, so it stays for a human to decide.
        Card card = MakeCard("Winter Orb", "Artifact", "Players can't untap more than one land during their untap steps.");

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Chaotic Transformation's "and/or up to one target land" after four other
    // targets, LandDestruction by hand (2026-10-27; 2 of 2 with Boom Box).
    [InlineData("Chaotic Transformation", "Sorcery",
        "Exile up to one target artifact, up to one target creature, up to one target enchantment, up to one "
        + "target planeswalker, and/or up to one target land. For each permanent exiled this way, its "
        + "controller reveals cards from the top of their library until they reveal a card that shares a card "
        + "type with it, puts that card onto the battlefield, then shuffles.")]
    public void Classify_ALandAtTheEndOfALongListOfTargets_IsLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // LandDestruction, widened 2026-09-19. The land is almost never alone in the
    // sentence: it sits in a type list, behind a count, or beside a second target,
    // and the old {0,2} filler words could not cross a comma to reach it.
    [InlineData("Aftershock", "Sorcery",
        "Destroy target artifact, creature, or land. Aftershock deals 3 damage to you.")]
    [InlineData("Creeping Mold", "Sorcery", "Destroy target artifact, enchantment, or land.")]
    [InlineData("Boom Box", "Artifact",
        "{6}, {T}, Sacrifice this artifact: Destroy up to one target artifact, up to one target "
        + "creature, and up to one target land.")]
    [InlineData("Fumarole", "Instant",
        "As an additional cost to cast this spell, pay 3 life.\nDestroy target creature and target land.")]
    [InlineData("Devastation", "Sorcery", "Destroy all creatures and lands.")]
    // A basic land type is a land by another name.
    [InlineData("Boil", "Instant", "Destroy all Islands.")]
    [InlineData("Volcanic Eruption", "Sorcery",
        "Destroy X target Mountains. Volcanic Eruption deals damage to each creature and each player "
        + "equal to the number of Mountains put into a graveyard this way.")]
    // Three wordings that name the victim without ever saying "target land".
    [InlineData("Cleansing", "Sorcery", "For each land, destroy that land unless any player pays 1 life.")]
    [InlineData("Desolation", "Enchantment",
        "At the beginning of each end step, each player who tapped a land for mana this turn "
        + "sacrifices a land of their choice. This enchantment deals 2 damage to each player who "
        + "sacrificed a Plains this way.")]
    [InlineData("Planetary Annihilation", "Sorcery",
        "Each player chooses six lands they control, then sacrifices the rest. Planetary Annihilation "
        + "deals 6 damage to each creature.")]
    // Fallow Earth answers a land without destroying it.
    [InlineData("Fallow Earth", "Sorcery", "Put target land on top of its owner's library.")]
    // Offering a basic back is consolation, not a reprieve.
    [InlineData("Sandworm", "Creature — Worm",
        "Haste\nWhen this creature enters, destroy target land. Its controller may search their "
        + "library for a basic land card, put it onto the battlefield tapped, then shuffle.")]
    public void Classify_TakingALandOffTheBoard_IsLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle))
            .HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Every sweep that takes the opponents' lands is LandDestruction, whether or
    // not it says land, ruled 2026-10-16 on Balance against Planetary Annihilation
    // and Apocalypse against Jokulhaups: all the permanents taken, and a pile kept
    // that may hold lands or is lands, the rest sacrificed. Natural Balance was
    // excused as a rebalance until that day.
    [InlineData("Apocalypse", "Sorcery",
        "Exile all permanents. You discard your hand.")]
    [InlineData("Upheaval", "Sorcery",
        "Return all permanents to their owners' hands.")]
    [InlineData("Worldslayer", "Artifact — Equipment",
        "Whenever equipped creature deals combat damage to a player, destroy all permanents other than this "
        + "Equipment.\nEquip {5} ({5}: Attach to target creature you control. Equip only as a sorcery.)")]
    [InlineData("Urza's Sylex", "Legendary Artifact",
        "{2}{W}{W}, {T}, Exile Urza's Sylex: Each player chooses six lands they control. Destroy all other "
        + "permanents. Activate only as a sorcery.\nWhen Urza's Sylex is put into exile from the battlefield, "
        + "you may pay {2}. If you do, search your library for a planeswalker card, reveal it, put it into your "
        + "hand, then shuffle.")]
    [InlineData("Balance", "Sorcery",
        "Each player chooses a number of lands they control equal to the number of lands controlled by the "
        + "player who controls the fewest, then sacrifices the rest. Players discard cards and sacrifice "
        + "creatures the same way.")]
    [InlineData("Cataclysm", "Sorcery",
        "Each player chooses from among the permanents they control an artifact, a creature, an enchantment, "
        + "and a land, then sacrifices the rest.")]
    public void Classify_ASweepThatTakesTheLandsUnnamed_IsLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Read 2026-10-16: a land closing a type list (Structural Collapse), and a
    // land edict after a creature edict (Din of the Fireherd, Smallpox).
    [InlineData("Structural Collapse", "Sorcery",
        "Target player sacrifices an artifact and a land of their choice. Structural Collapse deals 2 damage "
        + "to that player.")]
    [InlineData("Din of the Fireherd", "Sorcery",
        "Create a 5/5 black and red Elemental creature token. Target opponent sacrifices a creature of their "
        + "choice for each black creature you control, then sacrifices a land of their choice for each red "
        + "creature you control.")]
    [InlineData("Smallpox", "Sorcery",
        "Each player loses 1 life, discards a card, sacrifices a creature of their choice, then sacrifices a "
        + "land of their choice.")]
    public void Classify_ALandSweepTheRulesCouldNotRead_IsLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // "NONland permanent" has no word boundary in front of "land", which is the
    // only thing keeping the whole O-ring family out of this tag.
    [InlineData("Web Up", "Enchantment",
        "When this enchantment enters, exile target nonland permanent an opponent controls until this "
        + "enchantment leaves the battlefield.")]
    // An Aura attached to a land PROTECTS it.
    [InlineData("Savaen Elves", "Creature — Elf", "{G}{G}, {T}: Destroy target Aura attached to a land.")]
    // A land card in a graveyard is fuel, not a target.
    [InlineData("Steward of the Harvest", "Creature — Elemental",
        "When this creature enters, exile up to three target land cards from your graveyard.\n"
        + "Creatures you control have all activated abilities of all land cards exiled with this creature.")]
    // Your own denies nobody anything.
    [InlineData("Rats of Rath", "Creature — Rat", "{B}: Destroy target artifact, creature, or land you control.")]
    // Natural Balance, a rebalance that destroys nothing and hands basics back,
    // was pinned here until 2026-10-16; it is LandDestruction now, with every
    // sweep that takes the opponents' lands.
    // Read 2026-10-16: a pile of NONLAND permanents (Cataclysmic Gearhulk), every
    // permanent of a colour, a mana value or a name (Wash Out, Celestial Kirin,
    // Wirefly Hive), all permanents except for lands (Cyclone Summoner, Elspeth
    // Tirel), your own Swamps (Return of the Nightstalkers), the Auras attached to
    // a land (Street Sweeper), and a land that closes a choice and never has to be
    // one (Braids, Cabal Minion).
    [InlineData("Cataclysmic Gearhulk", "Artifact Creature — Construct",
        "Vigilance\nWhen this creature enters, each player chooses an artifact, a creature, an enchantment, "
        + "and a planeswalker from among the nonland permanents they control, then sacrifices the rest.")]
    [InlineData("Wash Out", "Sorcery",
        "Return all permanents of the color of your choice to their owners' hands.")]
    [InlineData("Celestial Kirin", "Legendary Creature — Kirin Spirit",
        "Flying\nWhenever you cast a Spirit or Arcane spell, destroy all permanents with that spell's mana "
        + "value.")]
    [InlineData("Wirefly Hive", "Artifact",
        "{3}, {T}: Flip a coin. If you win the flip, create a 2/2 colorless Insect artifact creature token "
        + "with flying named Wirefly. If you lose the flip, destroy all permanents named Wirefly.")]
    [InlineData("Cyclone Summoner", "Creature — Giant Wizard",
        "When this creature enters, if you cast it from your hand, return all permanents to their owners' "
        + "hands except for Giants, Wizards, and lands.")]
    [InlineData("Elspeth Tirel", "Legendary Planeswalker — Elspeth",
        "+2: You gain 1 life for each creature you control.\n−2: Create three 1/1 white Soldier creature "
        + "tokens.\n−5: Destroy all other permanents except for lands and tokens.")]
    [InlineData("Return of the Nightstalkers", "Sorcery",
        "Return all Nightstalker permanent cards from your graveyard to the battlefield. Then destroy all "
        + "Swamps you control.")]
    [InlineData("Street Sweeper", "Artifact Creature — Construct",
        "Whenever this creature attacks, destroy all Auras attached to target land.")]
    [InlineData("Braids, Cabal Minion", "Legendary Creature — Human Minion",
        "At the beginning of each player's upkeep, that player sacrifices an artifact, creature, or land of "
        + "their choice.")]
    public void Classify_WhatOnlyReadsLikeLandDestruction_IsNot(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle))
            .HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Lands an opponent controls, destroyed by count rather than by target.
    [InlineData("Krenko's Buzzcrusher", "Artifact Creature — Insect Thopter",
        "Flying, trample\nWhen this creature enters, for each player, destroy up to one nonbasic land that "
        + "player controls. For each land destroyed this way, its controller may search their library for a "
        + "basic land card, put it onto the battlefield tapped, then shuffle.")]
    public void Classify_TheOctober16LandDestructionPass_IsLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Exiling THIS CARD from your hand is a cost, whatever land it then names.
    [InlineData("Spara's Adjudicators", "Creature — Cat Citizen",
        "When this creature enters, target creature an opponent controls can't attack or block until your "
        + "next turn.\n{2}, Exile this card from your hand: Target land gains \"{T}: Add {G}, {W}, or {U}\" until "
        + "this card is cast from exile. You may cast this card for as long as it remains exiled.")]
    public void Classify_TheOctober16LandDestructionPass_IsNotLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Ruled 2026-11-03 on the principle of 2026-09-15 (a tag is what the card aims
    // at somebody, never what it pays): Akuta sacrifices its own Swamp if you have
    // more cards in hand THAN each opponent; the opponent is a comparison, not the
    // victim.
    [InlineData("Akuta, Born of Ash", "Legendary Creature — Spirit",
        "Haste\nAt the beginning of your upkeep, if you have more cards in hand than each opponent, you may "
        + "sacrifice a Swamp. If you do, return Akuta from your graveyard to the battlefield.")]
    public void Classify_TheNovember3LandDestructionPass_AComparisonIsNoVictim_IsNotLandDestruction(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.LandDestruction));
    }
}
