using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Every sweeper wording the rules stopped reading at the first adjective.
    [InlineData("Anarchy", "Sorcery", "Destroy all white permanents.")]
    [InlineData("Perish", "Sorcery", "Destroy all green creatures. They can't be regenerated.")]
    [InlineData("Apocalypse", "Sorcery", "Exile all permanents. You discard your hand.")]
    [InlineData("Evaporate", "Sorcery", "Evaporate deals 1 damage to each white and/or blue creature.")]
    [InlineData("Desynchronization", "Sorcery",
        "Return each nonland permanent that's not historic to its owner's hand.")]
    public void Classify_ASweeperWithAnAdjective_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // The combat trick that reads like a sweeper: it only ever touches what is
    // already in combat with the card. Every false positive the widening added.
    [InlineData("Abu Ja'far", "Creature — Human",
        "When this creature dies, destroy all creatures blocking or blocked by it. They can't be regenerated.")]
    [InlineData("Kjeldoran Frostbeast", "Creature — Elemental Beast",
        "At end of combat, destroy all creatures blocking or blocked by this creature.")]
    public void Classify_DestroyingWhatIsBlockingIt_IsNotWipe(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // The sweepers the rules could not read, widened 2026-09-27: a comma before
    // the noun (Jokulhaups), EACH for all (Wave of Terror, Selective
    // Obliteration), "destroy THOSE creatures" (Day of Black Sun), airbend
    // (Avatar's Wrath), the bare plural and the singular shrink (Dread of Night,
    // Seeker's Folly, Harvester of Misery, Exude Toxin), the -1/-1 counter
    // (Harbinger of Night), the amount that is not a number (Showstopping
    // Surprise, Territorial Aetherkite, Volcanic Eruption, Dwarven Catapult), a
    // player AND each creature they control (Mob Verdict), a comma in a mass
    // bounce (Season of Weaving) and every symmetric sacrifice of the lot
    // (Living Death, Zodiark, Pox, Balance, Promise of Loyalty). Lava Storm pins
    // that a whole side of the combat is still a board.
    [InlineData("Jokulhaups", "Sorcery",
        "Destroy all artifacts, creatures, and lands. They can't be regenerated.")]
    [InlineData("Wave of Terror", "Enchantment",
        "Cumulative upkeep {1} (At the beginning of your upkeep, put an age counter on this permanent, then "
        + "sacrifice it unless you pay its upkeep cost for each age counter on it.)\nAt the beginning of your "
        + "draw step, destroy each creature with mana value equal to the number of age counters on this "
        + "enchantment. They can't be regenerated.")]
    [InlineData("Selective Obliteration", "Sorcery",
        "Each player chooses a color. Then exile each permanent unless it's colorless or it's only the color "
        + "its controller chose.")]
    [InlineData("Day of Black Sun", "Sorcery",
        "Each creature with mana value X or less loses all abilities until end of turn. Destroy those "
        + "creatures.")]
    [InlineData("Avatar's Wrath", "Sorcery",
        "Choose up to one target creature, then airbend all other creatures. (Exile them. While each one is "
        + "exiled, its owner may cast it for {2} rather than its mana cost.)\nUntil your next turn, your "
        + "opponents can't cast spells from anywhere other than their hands.\nExile Avatar's Wrath.")]
    [InlineData("Dread of Night", "Enchantment",
        "White creatures get -1/-1.")]
    [InlineData("Seeker's Folly", "Sorcery",
        "Choose one —\n• Target opponent discards two cards.\n• Creatures your opponents control get -1/-1 "
        + "until end of turn.")]
    [InlineData("Harvester of Misery", "Creature — Spirit",
        "Menace\nWhen this creature enters, other creatures get -2/-2 until end of turn.\n{1}{B}, Discard this "
        + "card: Target creature gets -2/-2 until end of turn.")]
    [InlineData("Scavenger Regent // Exude Toxin", "Creature — Dragon // Sorcery — Omen",
        "Flying\nWard—Discard a card.\nEach non-Dragon creature gets -X/-X until end of turn. (Then shuffle "
        + "this card into its owner's library.)")]
    [InlineData("Harbinger of Night", "Creature — Spirit",
        "At the beginning of your upkeep, put a -1/-1 counter on each creature.")]
    [InlineData("Showstopping Surprise", "Instant",
        "Choose target creature you control. Turn it face up if it's face down. Then it deals damage equal to "
        + "its power to each other creature.")]
    [InlineData("Territorial Aetherkite", "Creature — Cat Dragon",
        "Flying, haste\nWhen this creature enters, you get {E}{E} (two energy counters). Then you may pay one "
        + "or more {E}. When you do, this creature deals that much damage to each other creature.")]
    [InlineData("Volcanic Eruption", "Sorcery",
        "Destroy X target Mountains. Volcanic Eruption deals damage to each creature and each player equal to "
        + "the number of Mountains put into a graveyard this way.")]
    [InlineData("Dwarven Catapult", "Instant",
        "Dwarven Catapult deals X damage divided evenly, rounded down, among all creatures target opponent "
        + "controls.")]
    [InlineData("Mob Verdict", "Sorcery",
        "Secret council — Each player secretly votes for another player, then those votes are revealed. For "
        + "each vote an opponent received, Mob Verdict deals 2 damage to that player and each creature that "
        + "player controls. For each vote you received, draw a card.")]
    [InlineData("Season of Weaving", "Sorcery",
        "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Draw a "
        + "card.\n{P}{P} — Choose an artifact or creature you control. Create a token that's a copy of "
        + "it.\n{P}{P}{P} — Return each nonland, nontoken permanent to its owner's hand.")]
    [InlineData("Living Death", "Sorcery",
        "Each player exiles all creature cards from their graveyard, then sacrifices all creatures they "
        + "control, then puts all cards they exiled this way onto the battlefield.")]
    [InlineData("Zodiark, Umbral God", "Legendary Creature — God",
        "Indestructible\nWhen Zodiark enters, each player sacrifices half the non-God creatures they control "
        + "of their choice, rounded down.\nWhenever a player sacrifices another creature, put a +1/+1 counter on "
        + "Zodiark.")]
    [InlineData("Pox", "Sorcery",
        "Each player loses a third of their life, then discards a third of the cards in their hand, then "
        + "sacrifices a third of the creatures they control of their choice, then sacrifices a third of the "
        + "lands they control of their choice. Round up each time.")]
    [InlineData("Balance", "Sorcery",
        "Each player chooses a number of lands they control equal to the number of lands controlled by the "
        + "player who controls the fewest, then sacrifices the rest. Players discard cards and sacrifice "
        + "creatures the same way.")]
    [InlineData("Promise of Loyalty", "Sorcery",
        "Each player puts a vow counter on a creature they control and sacrifices the rest. Each of those "
        + "creatures can't attack you or planeswalkers you control for as long as it has a vow counter on it.")]
    [InlineData("Lava Storm", "Instant",
        "Lava Storm deals 2 damage to each attacking creature or Lava Storm deals 2 damage to each blocking "
        + "creature.")]
    // A side swept after a kill with a count of its own, "and 1 damage to each
    // other creature with the same controller": Fear, Fire, Foes!, tagged Wipe by
    // the human on 2026-09-29.
    [InlineData("Fear, Fire, Foes!", "Sorcery",
        "Damage can't be prevented this turn. Fear, Fire, Foes! deals X damage to target creature and 1 "
        + "damage to each other creature with the same controller.")]
    public void Classify_ASweeperInAnyWording_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // The shapes that match those words and are not a board, 0 tagged among the
    // reviewed cards written each way: only what attacked, blocked or was aimed
    // at (Choking Vines, Glyph of Doom, Coils of the Medusa, Season of the
    // Witch, Heat Stroke); a dexterity flip (Chaos Orb, Falling Star); your OWN creatures
    // (Cinder Giant, Vampirism, Ghostway); creature CARDS in a graveyard (Zombie
    // Mob); Auras ATTACHED to one permanent
    // or Equipment attached to one creature (Scarab of the Unseen, Blastfire
    // Bolt); a type the card lets you CHOOSE among artifact and enchantment
    // (Season of Gathering, Druid of Purification); a NAME or a VOTE that picks
    // one thing (Eye of Singularity, Bile Blight, Council's Judgment); TARGET
    // creatures (Miasma Demon); ATTACKING creatures shrunk (Wind Shear); an
    // artifact that happens to be NONLAND (Granulate); and a reminder that says
    // "don't destroy those creatures" (Duty Beyond Death).
    [InlineData("Choking Vines", "Instant",
        "Cast this spell only during the declare blockers step.\nX target attacking creatures become blocked. "
        + "Choking Vines deals 1 damage to each of those creatures. (This spell works on creatures that can't "
        + "be blocked.)")]
    [InlineData("Glyph of Doom", "Instant",
        "Choose target Wall creature. At this turn's next end of combat, destroy all creatures that were "
        + "blocked by that creature this turn.")]
    [InlineData("Coils of the Medusa", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets +1/-1.\nSacrifice this Aura: Destroy all non-Wall creatures "
        + "blocking enchanted creature.")]
    [InlineData("Season of the Witch", "Enchantment",
        "At the beginning of your upkeep, sacrifice this enchantment unless you pay 2 life.\nAt the beginning "
        + "of the end step, destroy all untapped creatures that didn't attack this turn, except for creatures "
        + "that couldn't attack.")]
    [InlineData("Heat Stroke", "Enchantment",
        "At end of combat, destroy each creature that blocked or was blocked this turn.")]
    [InlineData("Chaos Orb", "Artifact",
        "{1}, {T}: If this artifact is on the battlefield, flip it onto the battlefield from a height of at "
        + "least one foot. If this artifact turns over completely at least once during the flip, destroy all "
        + "nontoken permanents it touches. Then destroy this artifact.")]
    [InlineData("Falling Star", "Sorcery",
        "Flip Falling Star onto the playing area from a height of at least one foot. Falling Star deals 3 "
        + "damage to each creature it lands on. Tap all creatures dealt damage by Falling Star. If Falling Star "
        + "doesn't turn completely over at least once during the flip, it has no effect.")]
    [InlineData("Cinder Giant", "Creature — Giant",
        "At the beginning of your upkeep, this creature deals 2 damage to each other creature you control.")]
    [InlineData("Zombie Mob", "Creature — Zombie",
        "This creature enters with a +1/+1 counter on it for each creature card in your graveyard.\nWhen this "
        + "creature enters, exile all creature cards from your graveyard.")]
    [InlineData("Scarab of the Unseen", "Artifact",
        "{T}, Sacrifice this artifact: Return all Auras attached to target permanent you own to their owners' "
        + "hands. Draw a card at the beginning of the next turn's upkeep.")]
    [InlineData("Season of Gathering", "Sorcery",
        "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Put a +1/+1 "
        + "counter on a creature you control. It gains vigilance and trample until end of turn.\n{P}{P} — Choose "
        + "artifact or enchantment. Destroy all permanents of the chosen type.\n{P}{P}{P} — Draw cards equal to "
        + "the greatest power among creatures you control.")]
    [InlineData("Eye of Singularity", "World Enchantment",
        "When this enchantment enters, destroy each permanent with the same name as another permanent, except "
        + "for basic lands. They can't be regenerated.\nWhenever a permanent other than a basic land enters, "
        + "destroy all other permanents with that name. They can't be regenerated.")]
    [InlineData("Miasma Demon", "Creature — Demon",
        "Flying\nWhen this creature enters, you may discard any number of cards. When you do, up to that many "
        + "target creatures each get -2/-2 until end of turn.")]
    [InlineData("Vampirism", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, draw a card at the beginning of the next turn's "
        + "upkeep.\nEnchanted creature gets +1/+1 for each other creature you control.\nOther creatures you "
        + "control get -1/-1.")]
    [InlineData("Wind Shear", "Instant",
        "Attacking creatures with flying get -2/-2 and lose flying until end of turn.")]
    [InlineData("Duty Beyond Death", "Instant",
        "As an additional cost to cast this spell, sacrifice a creature.\nCreatures you control gain "
        + "indestructible until end of turn. Put a +1/+1 counter on each creature you control. (Damage and "
        + "effects that say \"destroy\" don't destroy those creatures.)")]
    [InlineData("Bile Blight", "Instant",
        "Target creature and all other creatures with the same name as that creature get -3/-3 until end of "
        + "turn.")]
    [InlineData("Council's Judgment", "Sorcery",
        "Will of the council — Starting with you, each player votes for a nonland permanent you don't "
        + "control. Exile each permanent with the most votes or tied for most votes.")]
    [InlineData("Granulate", "Sorcery",
        "Destroy each nonland artifact with mana value 4 or less.")]
    [InlineData("Blastfire Bolt", "Instant",
        "Blastfire Bolt deals 5 damage to target creature. Destroy all Equipment attached to that creature.")]
    [InlineData("Ghostway", "Instant",
        "Exile each creature you control. Return those cards to the battlefield under their owner's control "
        + "at the beginning of the next end step.")]
    [InlineData("Druid of Purification", "Creature — Human Druid",
        "When this creature enters, starting with you, each player may choose an artifact or enchantment you "
        + "don't control. Destroy each permanent chosen this way.")]
    public void Classify_WordsThatSweepNoBoard_AreNotWipe(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // Damage to EACH creature AND EACH player is Wipe and Burn, however small
    // and however often, ruled 2026-09-27: the hand tags had split twelve to
    // seven on the same sentence (Pestilence against Withering Wisps, Winter Sky
    // against Dry Spell). Magmasaur also pins the +1/+1 counter as a count.
    [InlineData("Pestilence", "Enchantment",
        "At the beginning of the end step, if no creatures are on the battlefield, sacrifice this "
        + "enchantment.\n{B}: This enchantment deals 1 damage to each creature and each player.")]
    [InlineData("Winter Sky", "Sorcery",
        "Flip a coin. If you win the flip, Winter Sky deals 1 damage to each creature and each player. If you "
        + "lose the flip, each player draws a card.")]
    [InlineData("Cyclone", "Enchantment",
        "At the beginning of your upkeep, put a wind counter on this enchantment, then sacrifice this "
        + "enchantment unless you pay {G} for each wind counter on it. If you pay, this enchantment deals "
        + "damage equal to the number of wind counters on it to each creature and each player.")]
    [InlineData("Time Bomb", "Artifact",
        "At the beginning of your upkeep, put a time counter on this artifact.\n{1}, {T}, Sacrifice this "
        + "artifact: This artifact deals damage equal to the number of time counters on it to each creature and "
        + "each player.")]
    [InlineData("Magmasaur", "Creature — Elemental Dinosaur",
        "This creature enters with five +1/+1 counters on it.\nAt the beginning of your upkeep, you may remove "
        + "a +1/+1 counter from this creature. If you don't, sacrifice this creature and it deals damage equal "
        + "to the number of +1/+1 counters on it to each creature without flying and each player.")]
    [InlineData("Ifh-Bíff Efreet", "Creature — Efreet",
        "Flying\n{G}: This creature deals 1 damage to each creature with flying and each player. Any player "
        + "may activate this ability.")]
    public void Classify_DamageToEachCreatureAndEachPlayer_IsWipeAndBurn(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        Assert.True(result.HasFlag(CardEffect.Burn));
    }

    [Theory]
    // The other side of that ruling and the three that came with it, all
    // 2026-09-27: TWO or more each (Barter in Blood, Foreboding Steamboat); the
    // same edict EVERY TURN for every player (Dystopia, The Abyss, Nature's
    // Wrath); everything printed in one EXPANSION (Golgothian Sylex, City in a
    // Bottle); and a NON-type sweeper beside a tribal one (Genesis of the
    // Daleks, Crux of Fate), which keeps its Wrath.
    [InlineData("Barter in Blood", "Sorcery",
        "Each player sacrifices two creatures of their choice.")]
    [InlineData("Dystopia", "Enchantment",
        "Cumulative upkeep—Pay 1 life. (At the beginning of your upkeep, put an age counter on this "
        + "permanent, then sacrifice it unless you pay its upkeep cost for each age counter on it.)\nAt the "
        + "beginning of each player's upkeep, that player sacrifices a green or white permanent of their "
        + "choice.")]
    [InlineData("The Abyss", "World Enchantment",
        "At the beginning of each player's upkeep, destroy target nonartifact creature that player controls "
        + "of their choice. It can't be regenerated.")]
    // Nature's Wrath was pinned here until 2026-10-17; it is LandDestruction
    // alone now, with Mana Vortex, and is pinned in Classify_ASweeperRuledOut_IsNotWipe.
    [InlineData("Golgothian Sylex", "Artifact",
        "{1}, {T}: Each nontoken permanent with a name originally printed in the Antiquities expansion is "
        + "sacrificed by its controller.")]
    [InlineData("City in a Bottle", "Artifact",
        "Whenever one or more other nontoken permanents with a name originally printed in the Arabian Nights "
        + "expansion are on the battlefield, their controllers sacrifice them.\nPlayers can't cast spells or "
        + "play lands with a name originally printed in the Arabian Nights expansion.")]
    [InlineData("Foreboding Steamboat", "Artifact — Vehicle",
        "When this Vehicle enters, each player chooses two nontoken, non-Vehicle creatures they control. "
        + "Exile them until this Vehicle leaves the battlefield.\nWhenever this Vehicle attacks, put a card "
        + "exiled with it into its owner's graveyard. If you do, investigate.\nCrew 2")]
    [InlineData("Genesis of the Daleks", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI, II, III — "
        + "Create a 3/3 black Dalek artifact creature token with menace for each lore counter on Genesis of the "
        + "Daleks.\nIV — Target opponent faces a villainous choice — Destroy all Dalek creatures and each of "
        + "your opponents loses life equal to the total power of Daleks that died this turn, or destroy all "
        + "non-Dalek creatures.")]
    [InlineData("Crux of Fate", "Sorcery",
        "Choose one —\n• Destroy all Dragon creatures.\n• Destroy all non-Dragon creatures.")]
    // Read 2026-10-17, both Wipe until then only because they take the lands
    // too: Balance's line the other way round, and X creatures each closing a
    // list of what each player loses.
    [InlineData("Restore Balance", "Sorcery",
        "Suspend 6—{W}\nEach player chooses a number of lands they control equal to the number of lands "
        + "controlled by the player who controls the fewest, then sacrifices the rest. Players sacrifice "
        + "creatures and discard cards the same way.")]
    [InlineData("Death Cloud", "Sorcery",
        "Each player loses X life, discards X cards, sacrifices X creatures of their choice, then sacrifices "
        + "X lands of their choice.")]
    public void Classify_ASweeperRuledIn_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // One LAND a turn is not a board (Mana Vortex), and a sweeper of
    // ONE creature type is a tribal card's business, ruled 2026-09-27 off Goblin
    // Shrine ("è tribale quindi niente").
    [InlineData("Mana Vortex", "Enchantment",
        "When you cast this spell, counter it unless you sacrifice a land.\nAt the beginning of each player's "
        + "upkeep, that player sacrifices a land of their choice.\nWhen there are no lands on the battlefield, "
        + "sacrifice this enchantment.")]
    [InlineData("Goblin Shrine", "Enchantment — Aura",
        "Enchant land\nAs long as enchanted land is a basic Mountain, Goblin creatures get +1/+0.\nWhen this "
        + "Aura leaves the battlefield, it deals 1 damage to each Goblin creature.")]
    [InlineData("Scorch the Fields", "Sorcery",
        "Destroy target land. Scorch the Fields deals 1 damage to each Human creature.")]
    // Not a land sweep, 2026-10-16: X target lands, pointed at one by one (Rain of
    // Salt, Orcish Settlers), one land each (Hurloon Shaman, Desolation), your own
    // Swamps (Return of the Nightstalkers), and the Auras attached to a land
    // (Street Sweeper).
    [InlineData("Rain of Salt", "Sorcery",
        "Destroy two target lands.")]
    [InlineData("Orcish Settlers", "Creature — Orc",
        "{X}{X}{R}, {T}, Sacrifice this creature: Destroy X target lands.")]
    [InlineData("Hurloon Shaman", "Creature — Minotaur Shaman",
        "When this creature dies, each player sacrifices a land of their choice.")]
    [InlineData("Desolation", "Enchantment",
        "At the beginning of each end step, each player who tapped a land for mana this turn sacrifices a "
        + "land of their choice. This enchantment deals 2 damage to each player who sacrificed a Plains this "
        + "way.")]
    [InlineData("Return of the Nightstalkers", "Sorcery",
        "Return all Nightstalker permanent cards from your graveyard to the battlefield. Then destroy all "
        + "Swamps you control.")]
    [InlineData("Street Sweeper", "Artifact Creature — Construct",
        "Whenever this creature attacks, destroy all Auras attached to target land.")]
    // One land or one coloured permanent sacrificed whenever one enters is
    // LandDestruction alone, like Mana Vortex, ruled 2026-10-17 (Nature's Wrath
    // was Wipe from 2026-09-27).
    [InlineData("Nature's Wrath", "Enchantment",
        "At the beginning of your upkeep, sacrifice this enchantment unless you pay {G}.\nWhenever a player "
        + "puts an Island or blue permanent onto the battlefield, that player sacrifices an Island or blue "
        + "permanent of their choice.\nWhenever a player puts a Swamp or black permanent onto the battlefield, "
        + "that player sacrifices a Swamp or black permanent of their choice.")]
    public void Classify_ASweeperRuledOut_IsNotWipe(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Fact]
    public void Classify_EveryPermanentOfAKindSacrificed_IsWipe_AndLandsReturnedAreNotBounce()
    {
        // Ruled 2026-09-28: Omen of Fire is Wipe and LandDestruction. One
        // permanent FOR EACH white permanent is all of them, and returning every
        // Island sets a mana base back rather than bouncing a threat.
        CardEffect result = EffectClassifier.Classify(MakeCard("Omen of Fire", "Instant",
            "Return all Islands to their owners' hands. Each player sacrifices a Plains or a white permanent of "
            + "their choice for each white permanent they control."));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        Assert.True(result.HasFlag(CardEffect.LandDestruction));
        Assert.False(result.HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // A sweeper that catches the players on its way past is Wipe AND Burn; one
    // that only hits creatures is Wipe alone. Ruled 2026-09-15 off Inferno.
    [InlineData("Inferno", "Instant", "Inferno deals 6 damage to each creature and each player.")]
    [InlineData("Earthquake", "Sorcery", "Earthquake deals X damage to each creature without flying and each player.")]
    public void Classify_SweeperThatHitsPlayers_IsWipeAndBurn(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        Assert.True(result.HasFlag(CardEffect.Burn));
    }

    [Fact]
    public void Classify_SweeperThatSparesPlayers_IsWipeAlone()
    {
        CardEffect result = EffectClassifier.Classify(
            MakeCard("Pyroclasm", "Sorcery", "Pyroclasm deals 2 damage to each creature."));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        Assert.False(result.HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Mass damage is the oldest board sweeper there is and says "destroy" nowhere.
    [InlineData("Earthquake", "Sorcery", "Earthquake deals X damage to each creature without flying and each player.")]
    [InlineData("Crypt Rats", "Creature — Rat", "{X}: This creature deals X damage to each creature and each player. Spend only black mana on X.")]
    [InlineData("Pyroclasm", "Sorcery", "Pyroclasm deals 2 damage to each creature.")]
    public void Classify_MassDamage_IsWipe(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        // Sweeping the board is not aimed at anybody, so the single-target
        // Removal reading must not come along for the ride.
        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Fact]
    public void Classify_TheCreaturesThatCrewedIt_AreNotWiped()
    {
        Card card = MakeCard("Golden Argosy", "Legendary Artifact — Vehicle",
            "Whenever Golden Argosy attacks, exile each creature that crewed it this turn. Return them to the "
            + "battlefield tapped under their owner's control at the beginning of the next end step.\nCrew 1 (Tap "
            + "any number of creatures you control with total power 1 or more: This Vehicle becomes an artifact "
            + "creature until end of turn.)");

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // The card exiled together with each creature is Wipe, 2 of 2 by hand (read
    // 2026-10-30).
    [InlineData("Fraying Line", "Artifact",
        "When this artifact enters, put a rope counter on target creature you control.\nAt the beginning of "
        + "each player's upkeep, that player may pay {2}. If they do, they put a rope counter on a creature "
        + "they control. Otherwise, exile this artifact and each creature without a rope counter on it, then "
        + "remove all rope counters from all creatures.")]
    [InlineData("Gideon, the Oathsworn", "Legendary Planeswalker — Gideon",
        "Whenever you attack with two or more non-Gideon creatures, put a +1/+1 counter on each of those "
        + "creatures.\n+2: Until end of turn, Gideon becomes a 5/5 white Soldier creature that's still a "
        + "planeswalker. Prevent all damage that would be dealt to him this turn. (He can't attack if he was "
        + "cast this turn.)\n−9: Exile Gideon and each creature your opponents control.")]
    public void Classify_TheCardSweptAwayWithTheBoard_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // 'Destroy those creatures' after ONE creature chosen per opponent is an
    // edict, not a board (Highcliff Felidar, Removal alone by hand), read
    // 2026-10-27.
    [InlineData("Highcliff Felidar", "Creature — Cat Beast",
        "Vigilance\nWhen this creature enters, for each opponent, choose a creature with the greatest power "
        + "among creatures that player controls. Destroy those creatures.")]
    public void Classify_TheOctober27RemovalPass_OneCreaturePerOpponent_IsNotWipe(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // A second amount 'and n damage to each ... creature', whoever controls the
    // creatures, and 'X plus 2 damage to each creature' are Wipe, read 2026-11-03:
    // Seismic Wave, The Fall of Kroog and Let the Galaxy Burn, Wipe by hand, were
    // missed.
    [InlineData("Seismic Wave", "Instant",
        "Seismic Wave deals 2 damage to any target and 1 damage to each nonartifact creature target opponent "
        + "controls.")]
    [InlineData("The Fall of Kroog", "Sorcery",
        "Choose target opponent. Destroy target land that player controls. The Fall of Kroog deals 3 damage "
        + "to that player and 1 damage to each creature they control.")]
    [InlineData("Let the Galaxy Burn", "Sorcery",
        "Cascade (When you cast this spell, exile cards from the top of your library until you exile a "
        + "nonland card that costs less. You may cast it without paying its mana cost. Put the exiled cards on "
        + "the bottom in a random order.)\nLet the Galaxy Burn deals X plus 2 damage to each creature that "
        + "didn't enter this turn.")]
    public void Classify_TheNovember3Readings_ASecondDamageToEachCreature_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // Read 2026-11-05: a mode that shrinks every creature TOKEN kills them all
    // (Bloodline Culling, Removal and Wipe by hand), the same reach as a sweep for
    // the tokens' own side.
    [InlineData("Bloodline Culling", "Instant",
        "Choose one —\n• Target creature gets -5/-5 until end of turn.\n• Creature tokens get -2/-2 until end "
        + "of turn.")]
    public void Classify_TheNovember5Readings_CreatureTokensGetMinus_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }
}
