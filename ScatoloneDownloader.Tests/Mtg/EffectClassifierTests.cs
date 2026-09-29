using System.Collections.Generic;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Validates the rule-based <see cref="EffectClassifier"/> against real card
/// oracle text. The classifier only PROPOSES (a human confirms), so these pin
/// the high-confidence cases, the deliberate narrow edges (mana denial that
/// destroys nothing, a land sacrifice paid as a drawback) and the wordings
/// that fooled an earlier draft, rather than every card.
/// </summary>
public sealed class EffectClassifierTests
{
    [Theory]
    // name, typeLine, oracleText, expected flags
    // Both, and deliberately: damage is how a red deck answers a creature, so
    // Bolt is as much removal as Doom Blade. Decided 2026-09-11 against 3664
    // reviewed cards, where "deals N damage to any target" is hand-tagged
    // Removal 68 times in 75.
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.", CardEffect.Removal | CardEffect.Burn)]
    [InlineData("Wrath of God", "Sorcery", "Destroy all creatures. They can't be regenerated.", CardEffect.Wipe)]
    [InlineData("Counterspell", "Instant", "Counter target spell.", CardEffect.Counter)]
    [InlineData("Swords to Plowshares", "Instant", "Exile target creature. Its controller gains life equal to its power.", CardEffect.Removal)]
    [InlineData("Vindicate", "Sorcery", "Destroy target permanent.", CardEffect.RemovePermanent)]
    [InlineData("Demonic Tutor", "Sorcery", "Search your library for a card, then shuffle and put that card into your hand.", CardEffect.Tutor)]
    [InlineData("Worldly Tutor", "Instant", "Search your library for a creature card, reveal that card, then shuffle and put it on top.", CardEffect.Tutor)]
    [InlineData("Sol Ring", "Artifact", "{T}: Add {C}{C}.", CardEffect.Ramp)]
    [InlineData("Pacifism", "Enchantment — Aura", "Enchant creature. Enchanted creature can't attack or block.", CardEffect.Pacify)]
    [InlineData("Icy Manipulator", "Artifact", "{1}, {T}: Tap target artifact, creature, or land.", CardEffect.Pacify)]
    [InlineData("Reanimate", "Sorcery", "Return target creature card from your graveyard to the battlefield. You lose life equal to its mana value.", CardEffect.Reanimate)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Mind Rot", "Sorcery", "Target player discards two cards.", CardEffect.Discard)]
    [InlineData("Divination", "Sorcery", "Draw two cards.", CardEffect.CardAdvantage)]
    [InlineData("Control Magic", "Enchantment — Aura", "Enchant creature. You control enchanted creature.", CardEffect.Steal)]
    [InlineData("Stone Rain", "Sorcery", "Destroy target land.", CardEffect.LandDestruction)]
    [InlineData("Wasteland", "Land", "{T}, Sacrifice Wasteland: Destroy target nonbasic land.", CardEffect.LandDestruction)]
    // "Destroy all lands" is land destruction, not a creature/permanent wipe:
    // Wipe's patterns deliberately list creatures, permanents and nonland only.
    [InlineData("Armageddon", "Sorcery", "Destroy all lands.", CardEffect.LandDestruction)]
    [InlineData("Rain of Salt", "Sorcery", "Destroy two target lands.", CardEffect.LandDestruction)]
    [InlineData("Sinkhole", "Sorcery", "Destroy target land.", CardEffect.LandDestruction)]
    [InlineData("Glimpse the Unthinkable", "Sorcery", "Target player mills ten cards.", CardEffect.Mill)]
    [InlineData("Misdirection", "Instant", "Change the target of target spell with a single target.", CardEffect.Redirect)]
    [InlineData("Fork", "Instant", "Copy target instant or sorcery spell. You may choose new targets for the copy.", CardEffect.Redirect)]
    // A token entering as a copy OF something is Tokens, not stack interaction:
    // the rules require "copy target", never the bare word "copy".
    [InlineData("Clone", "Creature — Shapeshifter", "You may have this creature enter as a copy of any creature on the battlefield.", CardEffect.None)]
    // "Mill" is only keyword wording from 2021 on; the pre-2021 library spells
    // the action out, so the old phrasing has to match too.
    [InlineData("Millstone", "Artifact", "{2}, {T}: Target player puts the top two cards of their library into their graveyard.", CardEffect.Mill)]
    // Self-mill is NOT Mill as of 2026-09-15: dredge fills your own graveyard,
    // which is what a graveyard deck runs on rather than an attack on a library.
    // The card ends up with nothing proposed at all, and that is the point — the
    // rest of its text ("destroy that creature") is combat damage, not removal
    // aimed by you, and dredge's "if you would draw a card" is a replacement for
    // a draw rather than an extra one.
    [InlineData("Stinkweed Imp", "Creature — Imp", "Flying\nWhenever this creature deals combat damage to a creature, destroy that creature.\nDredge 5 (If you would draw a card, you may mill five cards instead.)", CardEffect.None)]
    public void Classify_ExactMatch_ForHighConfidenceCards(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

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
    // A Clue and an impulse are asked the same question: one card, once? Made
    // REPEATEDLY, either is a card; and an impulse that takes more than one card
    // off the top is a draw two whatever else it does. Ruled 2026-09-15.
    [InlineData("Boros Strike-Captain", "Creature — Human Soldier",
        "Battalion — Whenever this creature and at least two other creatures attack, exile the top card of your library. You may play that card this turn.")]
    [InlineData("Charred Foyer", "Land",
        "At the beginning of your upkeep, exile the top card of your library. You may play it this turn.")]
    [InlineData("Zenith Festival", "Sorcery",
        "Exile the top X cards of your library. You may play them until the end of your next turn.")]
    [InlineData("Interdimensional Web Watch", "Artifact",
        "When this artifact enters, exile the top two cards of your library. Until the end of your next turn, you may play those cards.")]
    [InlineData("Morska, Undersea Sleuth", "Legendary Creature — Fish Detective",
        "At the beginning of your upkeep, investigate. (Create a Clue token.)")]
    [InlineData("June, Bounty Hunter", "Legendary Creature — Human Scout",
        "{1}, Sacrifice another creature: Create a Clue token.")]
    public void Classify_ImpulseAndRepeatableClues_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // DISCOVER is a one-card impulse, a card only when the ability comes back
    // (2026-09-29): on every creature spell, off charge counters, on every attack,
    // and granted to an equipped creature. Discover X and discover 10 are the same
    // keyword.
    [InlineData("Monstrous Vortex", "Enchantment",
        "Whenever you cast a creature spell with power 5 or greater, discover X, where X is that spell's mana "
        + "value. (Exile cards from the top of your library until you exile a nonland card with that mana value "
        + "or less. Cast it without paying its mana cost or put it into your hand. Put the rest on the bottom "
        + "in a random order.)")]
    [InlineData("Long-Range Sensor", "Artifact",
        "Whenever you attack a player, put a charge counter on this artifact.\n{1}, Remove two charge counters "
        + "from this artifact: Discover 4. Activate only as a sorcery. (Exile cards from the top of your "
        + "library until you exile a nonland card with mana value 4 or less. Cast it without paying its mana "
        + "cost or put it into your hand. Put the rest on the bottom in a random order.)")]
    [InlineData("Caparocti Sunborn", "Legendary Creature — Human Soldier",
        "Whenever Caparocti Sunborn attacks, you may tap two untapped artifacts and/or creatures you control. "
        + "If you do, discover 3. (Exile cards from the top of your library until you exile a nonland card with "
        + "mana value 3 or less. Cast it without paying its mana cost or put it into your hand. Put the rest on "
        + "the bottom in a random order.)")]
    [InlineData("Swashbuckler's Whip", "Artifact — Equipment",
        "Equipped creature has reach, \"{2}, {T}: Tap target artifact or creature,\" and \"{8}, {T}: Discover "
        + "10.\" (Exile cards from the top of your library until you exile a nonland card with mana value 10 or "
        + "less. Cast it without paying its mana cost or put it into your hand. Put the rest on the bottom in a "
        + "random order.)\nEquip {1}")]
    public void Classify_ARepeatedDiscover_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // …and a discover made ONCE is not, ruled 2026-09-29 when the human took
    // CardAdvantage off Geological Appraiser and Hidden Volcano: an enters
    // trigger, a land that sacrifices itself for it, a spell. The reminder text no
    // longer vouches for it either.
    [InlineData("Geological Appraiser", "Creature — Human Artificer",
        "When this creature enters, if you cast it, discover 3. (Exile cards from the top of your library "
        + "until you exile a nonland card with mana value 3 or less. Cast it without paying its mana cost or "
        + "put it into your hand. Put the rest on the bottom in a random order.)")]
    [InlineData("Hidden Volcano", "Land — Cave",
        "This land enters tapped.\n{T}: Add {R}.\n{4}{R}, {T}, Sacrifice this land: Discover 4. Activate only "
        + "as a sorcery. (Exile cards from the top of your library until you exile a nonland card with mana "
        + "value 4 or less. Cast it without paying its mana cost or put it into your hand. Put the rest on the "
        + "bottom in a random order.)")]
    [InlineData("Daring Discovery", "Sorcery",
        "Up to three target creatures can't block this turn.\nDiscover 4. (Exile cards from the top of your "
        + "library until you exile a nonland card with mana value 4 or less. Cast it without paying its mana "
        + "cost or put it into your hand. Put the rest on the bottom in a random order.)")]
    public void Classify_ADiscoverOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // One card, once, in either vocabulary: the Clue still has to be cashed for
    // {2}, and the one-shot impulse just replaces the card that cast it.
    [InlineData("Cunning Maneuver", "Instant",
        "Create a Clue token. (It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    [InlineData("Equilibrium Adept", "Creature — Human Wizard",
        "When this creature enters, exile the top card of your library. Until the end of your next turn, you may play that card.")]
    [InlineData("Haste Magic", "Instant",
        "Target creature gets +3/+1 and gains haste until end of turn. Exile the top card of your library. You may play that card this turn.")]
    public void Classify_OneCardOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // UNTAP target creature contains the letters of TAP target creature, so every
    // untapper in the library was proposed as a tap-down. Found 2026-09-15; it was
    // a quarter of Pacify's false positives.
    [InlineData("Fyndhorn Brownie", "Creature — Elf", "{2}{G}, {T}: Untap target creature.")]
    [InlineData("Jandor's Saddlebags", "Artifact", "{3}, {T}: Untap target creature.")]
    [InlineData("Infuse", "Instant", "Untap target artifact, creature, or land.\nDraw a card at the beginning of the next turn's upkeep.")]
    // The card's own untap tax, written with a pronoun or with its own name.
    [InlineData("Apes of Rath", "Creature — Ape",
        "Whenever this creature attacks, it doesn't untap during its controller's next untap step.")]
    [InlineData("Merieke Ri Berit", "Legendary Creature — Human Wizard",
        "Merieke Ri Berit doesn't untap during your untap step.\n{T}: Destroy target creature when Merieke Ri Berit leaves the battlefield or becomes untapped.")]
    // Tapping your own, and stopping your own from attacking, are prices you pay.
    [InlineData("Energy Tap", "Instant",
        "Tap target untapped creature you control. If you do, add an amount of {C} equal to that creature's mana value.")]
    [InlineData("Akron Legionnaire", "Creature — Giant Soldier",
        "Except for creatures named Akron Legionnaire and artifact creatures, creatures you control can't attack.")]
    public void Classify_UntappingAndSelfRestraint_AreNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Preventing what a creature DEALS neutralises it, which is what this tag is
    // for. Ruled 2026-09-05 and written into Protection as an exclusion, but not
    // added here until 2026-09-15 — so these were only ever tagged by accident,
    // through the untap bug above.
    [InlineData("Maze of Ith", "Land",
        "{T}: Untap target attacking creature. Prevent all combat damage that would be dealt to and dealt by that creature this turn.")]
    [InlineData("Gaseous Form", "Enchantment — Aura",
        "Enchant creature\nPrevent all combat damage that would be dealt to and dealt by enchanted creature.")]
    [InlineData("Lady Evangela", "Legendary Creature — Human Cleric",
        "{W}{B}, {T}: Prevent all combat damage that would be dealt by target creature this turn.")]
    public void Classify_PreventingWhatACreatureDeals_IsPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // The same sentence pointed the wrong way, twice. Mtenda Lion blunts its own
    // attack, which is a price; Ebony Horse unhorses one of YOURS, which is
    // vigilance bought at instant speed. Foxfire says almost exactly what Ebony
    // Horse says and is Pacify, so the difference is only "you control".
    [InlineData("Mtenda Lion", "Creature — Cat",
        "Whenever this creature attacks, defending player may pay {U}. If that player does, prevent all combat damage that would be dealt by this creature this turn.")]
    [InlineData("Ebony Horse", "Artifact",
        "{2}, {T}: Untap target attacking creature you control. Prevent all combat damage that would be dealt to and dealt by that creature this turn.")]
    // Both of these say "dealt by IT", and the pronoun points somewhere different
    // in each: at the card itself, and at the creature you just untapped. Neither
    // is a lock, which is why the subject list has no bare pronoun in it.
    [InlineData("Goblin Snowman", "Creature — Goblin",
        "Whenever this creature blocks, prevent all combat damage that would be dealt to and dealt by it this turn.\n{T}: This creature deals 1 damage to target creature it's blocking.")]
    [InlineData("Elvish Scout", "Creature — Elf Scout",
        "{G}, {T}: Untap target attacking creature you control. Prevent all combat damage that would be dealt to and dealt by it this turn.")]
    public void Classify_PreventingWhatYourOwnDeals_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // A lock the OPPONENT can buy out of is a tax, not a lock — whether anything
    // is neutralised is their decision. Ruled 2026-09-16.
    [InlineData("Heroism", "Enchantment",
        "Sacrifice a white creature: For each attacking red creature, prevent all combat damage that would be dealt by that creature this turn unless its controller pays {2}{R}.")]
    [InlineData("Winter's Chill", "Instant",
        "Cast this spell only during combat before blockers are declared.\nX can't be greater than the number of snow lands you control.\nChoose X target attacking creatures. For each of those creatures, its controller may pay {1} or {2}. If that player doesn't, destroy that creature at end of combat. If that player pays only {1}, prevent all combat damage that would be dealt to and dealt by that creature this combat.")]
    public void Classify_APreventionTheyCanPayToIgnore_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // And the three that say the same sentence as Horn of Deafening with nothing
    // conditional about them. Realigned by hand the same day.
    [InlineData("Safeguard", "Enchantment",
        "{2}{W}: Prevent all combat damage that would be dealt by target creature this turn.")]
    [InlineData("Warning", "Instant",
        "Prevent all combat damage that would be dealt by target attacking creature this turn.")]
    [InlineData("Subdue", "Instant",
        "Prevent all combat damage that would be dealt by target creature this turn. That creature gets +0/+X until end of turn, where X is its mana value.")]
    public void Classify_AnUnconditionalPrevention_IsPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

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
    // Mob); a pile of LANDS (Natural Balance); Auras ATTACHED to one permanent
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
    [InlineData("Natural Balance", "Sorcery",
        "Each player who controls six or more lands chooses five lands they control and sacrifices the rest. "
        + "Each player who controls four or fewer lands may search their library for up to X basic land cards "
        + "and put them onto the battlefield, where X is five minus the number of lands they control. Then each "
        + "player who searched their library this way shuffles.")]
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
    // ONE creature each is an edict, ruled Removal on 2026-09-27 ("un singolo
    // sacrifice lo mettiamo removal"); the hand tags had split three to two.
    // OVERTURNS Classify_ASymmetricEdict_IsNotRemoval, which pinned Abyssal
    // Gatekeeper as NOT Removal on the reading that the edict costs you one too;
    // it was removed the same day and Abyssal Gatekeeper is pinned here instead.
    [InlineData("Abyssal Gatekeeper", "Creature — Horror",
        "When this creature dies, each player sacrifices a creature of their choice.")]
    [InlineData("Accursed Marauder", "Creature — Zombie Warrior",
        "When this creature enters, each player sacrifices a nontoken creature of their choice.")]
    [InlineData("Tariff", "Sorcery",
        "Each player sacrifices the creature they control with the greatest mana value unless they pay that "
        + "creature's mana cost. If two or more creatures a player controls are tied for greatest, that player "
        + "chooses one.")]
    public void Classify_EachPlayerSacrificesOne_IsRemovalNotWipe(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Wipe));
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
    [InlineData("Nature's Wrath", "Enchantment",
        "At the beginning of your upkeep, sacrifice this enchantment unless you pay {G}.\nWhenever a player "
        + "puts an Island or blue permanent onto the battlefield, that player sacrifices an Island or blue "
        + "permanent of their choice.\nWhenever a player puts a Swamp or black permanent onto the battlefield, "
        + "that player sacrifices a Swamp or black permanent of their choice.")]
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
    public void Classify_ASweeperRuledIn_IsWipe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // A repeated edict of LANDS is not a board (Mana Vortex), and a sweeper of
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
    public void Classify_ASweeperRuledOut_IsNotWipe(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Wipe));
    }

    [Fact]
    public void Classify_AnEdictThatExiles_IsRemovalNotWipe()
    {
        // Ruled 2026-09-28: Sothera's edict comes back every time one of yours
        // dies, but it only ever lands on the opponents, so it is Removal.
        CardEffect result = EffectClassifier.Classify(MakeCard("Sothera, the Supervoid", "Legendary Enchantment",
            "Whenever a creature you control dies, each opponent chooses a creature they control and exiles "
            + "it.\nAt the beginning of your end step, if a player controls no creatures, sacrifice Sothera, then "
            + "put a creature card exiled with it onto the battlefield under your control with two additional +1/+1 "
            + "counters on it."));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Wipe));
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
    // Two more ways a card fixes colours, neither of which says "or" on one line.
    [InlineData("Bleachbone Verge", "Land",
        "{T}: Add {B}.\n{T}: Add {W}. Activate only if you control a Plains or a Swamp.")]
    [InlineData("Balamb T-Rexaur", "Creature — Dinosaur",
        "Trample\nWhen this creature enters, you gain 3 life.\nForestcycling {2} ({2}, Discard this card: Search your library for a Forest card, reveal it, put it into your hand, then shuffle.)")]
    public void Classify_TwoAbilitiesOrTypecycling_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Mana you may only spend on one thing buys the card the designer had in
    // mind; it fixes nothing. Judged per line, so a card with one restricted
    // ability and one free one still counts.
    [InlineData("Herd Heirloom", "Artifact",
        "{T}: Add one mana of any color. Spend this mana only to cast a creature spell.")]
    public void Classify_ManaYouMayOnlySpendOnOneThing_IsNotManaFixing(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Fact]
    public void Classify_OneRestrictedAbilityAndOneFree_StaysManaFixing()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("White Lotus Hideout", "Land",
            "{T}: Add {C}.\n{T}: Add one mana of any color. Spend this mana only to cast a Lesson or Shrine spell."
            + "\n{1}, {T}: Add one mana of any color."));

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Two ways a card burns a face that the rules were not reading. Ruled
    // 2026-09-15; together they were 29 of Burn's 101 misses.
    [InlineData("Corroding Dragonstorm", "Enchantment",
        "When this enchantment enters, each opponent loses 2 life and you gain 2 life.")]
    [InlineData("Cat-Gator", "Creature — Cat Crocodile",
        "Lifelink\nWhen this creature enters, it deals damage equal to the number of Swamps you control to any target.")]
    public void Classify_LifeLossAndCountedDamage_AreBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // A +1/+1 counter is Buff in the other vocabulary, ruled 2026-09-15, and so
    // are support and distribute. "One counter on one creature counts" was
    // OVERTURNED for one-shots on 2026-09-25 — Cloudbound Moogle moved to the
    // size-and-purpose tests below — so a counter every combat on a
    // noncreature stands here instead.
    [InlineData("Innkeeper's Talent", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nAt the beginning of combat on your turn, put a "
        + "+1/+1 counter on target creature you control.\n{G}: Level 2\nPermanents you control with counters on "
        + "them have ward {1}.\n{3}{G}: Level 3\nIf you would put one or more counters on a permanent or player, "
        + "put twice that many of each of those kinds of counters on that permanent or player instead.")]
    [InlineData("Blitzball Stadium", "Artifact",
        "When this artifact enters, support X. (Put a +1/+1 counter on each of up to X target creatures.)")]
    [InlineData("Cloudspire Skycycle", "Artifact — Vehicle",
        "Flying\nWhen this Vehicle enters, distribute two +1/+1 counters among one or two other target Vehicles and/or creatures you control.")]
    public void Classify_ACounterOnSomebodyElse_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump restricted to one creature TYPE is not Buff, ruled 2026-09-15: it
    // promises a payoff a cube with no Minotaur deck cannot collect.
    [InlineData("Anaba Spirit Crafter", "Creature — Minotaur Shaman", "Minotaur creatures get +1/+0.")]
    [InlineData("Muscle Sliver", "Creature — Sliver", "All Sliver creatures get +1/+1.")]
    [InlineData("Zuberi, Golden Feather", "Legendary Creature — Griffin",
        "Flying\nOther Griffin creatures get +1/+1.")]
    [InlineData("Heart Wolf", "Legendary Creature — Wolf",
        "First strike\n{T}: Target Dwarf creature gets +2/+0 and gains first strike until end of turn.")]
    [InlineData("Doctor Octopus, Master Planner", "Legendary Creature — Human Villain",
        "Other Villains you control get +2/+2.\nYour maximum hand size is eight.")]
    public void Classify_APumpForOneTribe_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump for the creatures that share the card's NAME goes to its own copies,
    // which is the card pumping itself: the human took Buff off Gruff Triplets on
    // 2026-09-29.
    [InlineData("Gruff Triplets", "Creature — Satyr Warrior",
        "Trample\nWhen this creature enters, if it isn't a token, create two tokens that are copies of "
        + "it.\nWhen this creature dies, put a number of +1/+1 counters equal to its power on each creature you "
        + "control named Gruff Triplets.")]
    [InlineData("Charmed Stray", "Creature — Cat",
        "Lifelink\nWhen this creature enters, put a +1/+1 counter on each other creature you control named "
        + "Charmed Stray.")]
    public void Classify_APumpForItsOwnCopies_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump sized by X or by a count is still a pump; the rules only read
    // digits. Added 2026-09-18 INSIDE the beneficiary guard, which is the whole
    // point — see the next test.
    [InlineData("Berserk", "Instant",
        "Cast this spell only before the combat damage step.\nTarget creature gains trample and gets +X/+0 until end of turn, where X is its power.")]
    [InlineData("Soulshriek", "Instant",
        "Target creature you control gets +X/+0 until end of turn, where X is the number of creature cards in your graveyard.")]
    [InlineData("Frontline Rush", "Sorcery",
        "Choose one —\n• Create two 1/1 red Goblin creature tokens.\n• Target creature gets +X/+X until end of turn, where X is the number of creatures you control.")]
    public void Classify_APumpSizedByACount_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Buff acts on power and/or toughness, ruled 2026-09-18 — and DOUBLE STRIKE
    // is the one keyword that does, because doubling the damage is doubling the
    // power by another name.
    [InlineData("Dual-Sun Technique", "Instant",
        "Target creature you control gains double strike until end of turn. If it has a +1/+1 counter on it, draw a card.")]
    [InlineData("Genji Glove", "Artifact — Equipment",
        "Equipped creature has double strike.\nEquip {3}")]
    public void Classify_GrantingDoubleStrike_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Every other keyword is not. None of them changes what the numbers are, and
    // the hand-tagging says so 49 cards to 5 — Jump and Cloak of Feathers are the
    // same sentence, and only one of them used to be tagged.
    [InlineData("Jump", "Instant", "Target creature gains flying until end of turn.")]
    [InlineData("Toxin Analysis", "Instant",
        "Target creature gains deathtouch and lifelink until end of turn. Investigate.")]
    [InlineData("Flying Carpet", "Artifact",
        "{2}, {T}: Target creature gains flying until end of turn.")]
    // First strike hits first; it does not hit harder.
    [InlineData("Fyndhorn Bow", "Artifact",
        "{3}, {T}: Target creature gains first strike until end of turn.")]
    public void Classify_GrantingAnyOtherKeyword_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Half the cards that scaling reaches pump only THEMSELVES, and the same
    // guard that handles "+2/+2" has to handle these.
    [InlineData("Rabid Wombat", "Creature — Beast",
        "Vigilance\nThis creature gets +2/+2 for each Aura attached to it.")]
    [InlineData("Guidelight Synergist", "Creature — Bird Artificer",
        "Flying\nThis creature gets +1/+0 for each artifact you control.")]
    public void Classify_AScalingSelfPump_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The ACTIVATION COST was being read as the beneficiary: the creature you tap
    // to pay is not the creature being pumped. Found 2026-09-17.
    [InlineData("Llanowar Behemoth", "Creature — Elemental",
        "Tap an untapped creature you control: This creature gets +1/+1 until end of turn.")]
    [InlineData("Karplusan Giant", "Creature — Giant",
        "Tap an untapped snow land you control: This creature gets +1/+1 until end of turn.")]
    // And a condition in the same sentence does the same thing.
    [InlineData("Comet Crawler", "Creature — Insect",
        "Lifelink\nWhenever this creature attacks, you may sacrifice another creature or artifact. If you do, this creature gets +2/+2 until end of turn.")]
    // A pump the card hands to a token it creates belongs to the token.
    [InlineData("Chocobo Racetrack", "Land",
        "Landfall — Whenever a land you control enters, create a 2/2 green Bird creature token with \"Whenever a land you control enters, this creature gets +1/+1 until end of turn.\"")]
    // Lords the type rule could not see: no "creatures" word, or behind a cost.
    [InlineData("Lord of Atlantis", "Creature — Merfolk",
        "Other Merfolk get +1/+1 and have islandwalk.")]
    [InlineData("Adeliz, the Cinder Wind", "Legendary Creature — Efreet Wizard",
        "Flying, haste\nWhenever you cast an instant or sorcery spell, Wizards you control get +1/+1 until end of turn.")]
    [InlineData("Faerie Noble", "Creature — Faerie Lord",
        "Flying\nOther Faerie creatures you control get +0/+1.\n{T}: Other Faerie creatures get +2/+0 until end of turn.")]
    public void Classify_APumpThatLandsSomewhereElse_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    public void Classify_AQuotedAbilityThatPumpsATarget_IsNotBuff()
    {
        // This used to be the exception to the quotes rule, on the reading that
        // a granted ability pumping ANY creature is a Buff the card handed you.
        // Overturned 2026-09-21 by the same ruling Burn got the day before: what
        // a token or a granted ability does belongs to whatever received it,
        // whoever it then points at. Seven reviewed cards hand out a "{T}:
        // Target creature you control gets +1/+0" and none is tagged Buff.
        CardEffect result = EffectClassifier.Classify(MakeCard("Forbidden Lore", "Enchantment — Aura",
            "Enchant land\nEnchanted land has \"{T}: Target creature gets +2/+1 until end of turn.\""));

        Assert.False(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Ruled 2026-09-21, four families at once. A pump inside PARENTHESES is a
    // keyword's reminder text and does nothing; an Aura that pumps the creature
    // it just took is paying for it; a pump bundled with a shrink or a fight is
    // a rider on the answer; and a lord is not this tag however the tribe is
    // named — chosen, listed, or named as a card.
    [InlineData("Gabriel Angelfire", "Legendary Creature — Angel",
        "At the beginning of your upkeep, choose flying, first strike, trample, or rampage 3. Gabriel "
        + "Angelfire gains that ability until your next upkeep. (Whenever a creature with rampage 3 "
        + "becomes blocked, it gets +3/+3 until end of turn for each creature blocking it beyond the first.)")]
    [InlineData("Binding Grasp", "Enchantment — Aura",
        "Enchant creature\nAt the beginning of your upkeep, sacrifice this Aura unless you pay {1}{U}.\n"
        + "You control enchanted creature.\nEnchanted creature gets +0/+1.")]
    [InlineData("Gurmag Rakshasa", "Creature — Cat Demon",
        "Menace\nWhen this creature enters, target creature an opponent controls gets -2/-2 until end of "
        + "turn and target creature you control gets +2/+2 until end of turn.")]
    [InlineData("Patchwork Banner", "Artifact",
        "As this artifact enters, choose a creature type.\nCreatures you control of the chosen type get "
        + "+1/+1.\n{T}: Add one mana of any color.")]
    [InlineData("The Swarmweaver", "Legendary Creature — Spider",
        "When The Swarmweaver enters, create two 1/1 black and green Insect creature tokens with flying.\n"
        + "Delirium — As long as there are four or more card types among cards in your graveyard, Insects "
        + "and Spiders you control get +1/+1 and have deathtouch.")]
    public void Classify_APumpThatBelongsToSomethingElse_IsNotBuff(
        string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // And the two vocabularies the tag was blind to, both ruled the same day.
    // Setting a base power and toughness raises it just as an addition does, and
    // DOUBLING is multiplication landing in the same place.
    [InlineData("Wrecking Ball Arm", "Artifact — Equipment",
        "Equipped creature has base power and toughness 7/7 and can't be blocked by creatures with power "
        + "2 or less.\nEquip legendary creature {3}\nEquip {7}")]
    [InlineData("Unruly Krasis", "Creature — Fish Beast",
        "Trample\nWhenever this creature attacks, you may have the base power and toughness of another "
        + "target creature you control become X/X until end of turn, where X is the number of creatures "
        + "you control.")]
    [InlineData("Bulk Up", "Instant", "Double target creature's power until end of turn.")]
    [InlineData("Double Trouble", "Sorcery", "Double the power of each creature you control until end of turn.")]
    public void Classify_ABaseOrADoubling_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    public void Classify_ATriggerThatPumpsTheTeam_StaysBuff()
    {
        // The cost-cutting must not swallow this: "this creature" is only the
        // trigger's subject, and the pump lands on the whole team.
        CardEffect result = EffectClassifier.Classify(MakeCard("Dauntless Veteran", "Creature — Human Soldier",
            "Whenever this creature attacks, creatures you control get +1/+1 until end of turn."));

        Assert.True(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // What "tribe" does NOT mean. A colour is not a tribe (Crusade, Bad Moon), a
    // STATE is not a tribe (Castle, Weakstone), and an unrestricted anthem is the
    // thing the tag is for.
    [InlineData("Crusade", "Enchantment", "White creatures get +1/+1.")]
    [InlineData("Bad Moon", "Enchantment", "Black creatures get +1/+1.")]
    [InlineData("Castle", "Enchantment", "Untapped creatures you control get +0/+2.")]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.")]
    public void Classify_AColourOrAStateIsNotATribe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    public void Classify_ALordThatAlsoPumpsSomethingUnrestricted_StaysBuff()
    {
        // The exclusion asks whether EVERY pump is tribal, so a card that both
        // lords and pumps freely keeps the tag.
        CardEffect result = EffectClassifier.Classify(MakeCard("Field Marshal", "Creature — Human Soldier",
            "First strike\nOther Soldier creatures get +1/+1 and have first strike.\n{2}{W}: Target creature gets +3/+3 until end of turn."));

        Assert.True(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The two it still has to keep out: a counter the card puts on ITSELF is a
    // stat line, and a counter on THEIR creatures helps them.
    [InlineData("Aku Djinn", "Creature — Djinn",
        "Trample\nAt the beginning of your upkeep, put a +1/+1 counter on each creature each opponent controls.")]
    [InlineData("Aerith Gainsborough", "Legendary Creature — Human",
        "Lifelink\nWhenever you gain life, put a +1/+1 counter on Aerith Gainsborough.")]
    public void Classify_ACounterOnItselfOrOnThem_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Mana without a {T}, off a permanent that spends itself, or off a spell.
    // Ruled 2026-09-15.
    [InlineData("Black Lotus", "Artifact",
        "{T}, Sacrifice this artifact: Add three mana of any one color.")]
    [InlineData("Blood Pet", "Creature — Thrull", "Sacrifice this creature: Add {B}.")]
    [InlineData("Dark Ritual", "Instant", "Add {B}{B}{B}.")]
    [InlineData("Crystal Vein", "Land", "{T}: Add {C}.\n{T}, Sacrifice this land: Add {C}{C}.")]
    public void Classify_ManaOffAPermanentThatSpendsItself_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // The line the hand-tagging draws inside that family: entering tapped costs
    // the turn the extra mana was meant to buy, so these five are not Ramp.
    [InlineData("Dwarven Ruins", "Land",
        "This land enters tapped.\n{T}: Add {R}.\n{T}, Sacrifice this land: Add {R}{R}.")]
    [InlineData("Svyelunite Temple", "Land",
        "This land enters tapped.\n{T}: Add {U}.\n{T}, Sacrifice this land: Add {U}{U}.")]
    public void Classify_ASelfSacrificingLandThatEntersTapped_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A shrink is an answer — a creature at zero toughness is as dead as a
    // destroyed one, in both vocabularies. Ruled 2026-09-15.
    [InlineData("Locust Spray", "Instant",
        "Target creature gets -1/-1 until end of turn.\nCycling {B} ({B}, Discard this card: Draw a card.)")]
    [InlineData("Grandmother Sengir", "Legendary Creature — Human Wizard",
        "{1}{B}, {T}: Target creature gets -1/-1 until end of turn.")]
    [InlineData("Fevered Convulsions", "Enchantment",
        "{2}{B}{B}: Put a -1/-1 counter on target creature.")]
    public void Classify_AShrink_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The two edges of that reading. Taking the power off leaves the creature
    // standing, which is Pacify's job; and a shrink aimed at everybody is a Wipe.
    [InlineData("Pradesh Gypsies", "Creature — Human Nomad",
        "{1}{G}, {T}: Target creature gets -2/-0 until end of turn.")]
    [InlineData("Dread of Night", "Enchantment", "White creatures get -1/-1.")]
    public void Classify_AShrinkThatKillsNothingOrKillsEverything_IsNotRemoval(
        string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // A land that makes more mana than it costs, every turn, is Ramp — the one
    // exception to "a land tapping for its own mana is just a land". Ruled
    // 2026-09-15 off Ancient Tomb.
    [InlineData("Ancient Tomb", "Land", "{T}: Add {C}{C}. This land deals 2 damage to you.")]
    [InlineData("Mishra's Workshop", "Land", "{T}: Add {C}{C}{C}. Spend this mana only to cast artifact spells.")]
    [InlineData("Karoo", "Land",
        "This land enters tapped.\nWhen this land enters, sacrifice it unless you return an untapped Plains you control to its owner's hand.\n{T}: Add {W}{W}.")]
    public void Classify_ALandThatMakesExtraManaEveryTurn_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // The two land shapes that stay out. A plain dual makes one mana; the
    // self-sacrificing family spends itself for the extra one, which is why
    // Dwarven Ruins is hand-tagged as nothing.
    [InlineData("Tropical Island", "Land — Forest Island", "({T}: Add {G} or {U}.)")]
    [InlineData("Dwarven Ruins", "Land",
        "This land enters tapped.\n{T}: Add {R}.\n{T}, Sacrifice this land: Add {R}{R}.")]
    public void Classify_OrdinaryLands_AreNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // The Removal rules could not cross a comma, so every "destroy target
    // artifact, creature, or land" went unread; nor could they cross the filler
    // in front of "target", so every "exile up to one target creature" O-ring
    // did too. Between them that was 49 of 189 misses.
    [InlineData("Aftershock", "Sorcery", "Destroy target artifact, creature, or land. Aftershock deals 3 damage to you.")]
    [InlineData("Shattered Wings", "Instant", "Destroy target artifact, enchantment, or creature with flying. Surveil 1.")]
    [InlineData("All-Fates Stalker", "Creature — Assassin",
        "When this creature enters, exile up to one target non-Assassin creature until this creature leaves the battlefield.")]
    // Damage sized by a count kills exactly as damage sized by a digit does.
    [InlineData("Cat-Gator", "Creature — Cat Crocodile",
        "Lifelink\nWhen this creature enters, it deals damage equal to the number of Swamps you control to any target.")]
    public void Classify_KillsTheRuleCouldNotRead_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The damage rules could not read a QUALIFIED target: the adjective sits
    // between "target" and "creature", where nothing was allowed to.
    [InlineData("D'Avenant Archer", "Creature — Human Soldier Archer",
        "{T}: This creature deals 1 damage to target attacking or blocking creature.")]
    [InlineData("Femeref Archers", "Creature — Human Archer",
        "{T}: This creature deals 4 damage to target attacking creature with flying.")]
    [InlineData("Cloud of Darkness", "Creature — Elemental",
        "Flying\nParticle Beam — When Cloud of Darkness enters, target creature an opponent controls gets -X/-X until end of turn.")]
    // And the amount is allowed to come after the target.
    [InlineData("Divine Retribution", "Instant",
        "Divine Retribution deals damage to target attacking creature equal to the number of creatures defending player controls.")]
    // A fireball split between several things still kills one of them.
    [InlineData("Fiery Justice", "Sorcery",
        "Fiery Justice deals 5 damage divided as you choose among any number of targets. Target opponent gains 5 life.")]
    [InlineData("Dwarven Catapult", "Sorcery",
        "Dwarven Catapult deals X damage divided evenly, rounded down, among all creatures your opponents control.")]
    // An edict aimed at them, in a wording the single shipped pattern missed.
    [InlineData("Cornered by Black Mages", "Sorcery",
        "Target opponent sacrifices a creature of their choice.\nCreate a 0/1 black Wizard creature token.")]
    public void Classify_KillsTheDamageRulesCouldNotRead_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // NONCREATURE ends in "creature", so these read as kills. The same bug the
    // tap/untap pair had, found the same way.
    [InlineData("Gorilla Shaman", "Creature — Ape",
        "{X}{X}{1}: Destroy target noncreature artifact with mana value X.")]
    [InlineData("Joven", "Legendary Creature — Human Rogue",
        "{R}{R}{R}, {T}: Destroy target noncreature artifact.")]
    // And an answer that can only ever touch what is already blocking the card
    // is a combat trick, not removal — the reading Wipe already had.
    [InlineData("Knight of Dusk", "Creature — Human Knight",
        "{B}{B}: Destroy target creature blocking this creature.")]
    [InlineData("Flowstone Salamander", "Creature — Elemental Lizard",
        "{R}: This creature deals 1 damage to target creature blocking it.")]
    public void Classify_NoncreatureAndCombatTricks_AreNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Two families the wider reading has to keep out. A blink returns the card,
    // so it answers nothing; a card already in a graveyard is already dead.
    [InlineData("Explosive Getaway", "Instant",
        "Exile up to one target artifact or creature. Return it to the battlefield under its owner's control at the beginning of the next end step.")]
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Elf Detective",
        "When Nyla enters, exile up to one target creature card from your graveyard.")]
    public void Classify_BlinkAndGraveyardHate_AreNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // A Wall is not a Pacify effect. "This creature can't attack" is defender,
    // printed on the card, and its reminder text spells it out — which had every
    // Wall in the library proposed as pseudo-removal. A self-restriction on when
    // a creature may attack is the same shape.
    [InlineData("Wall of Ice", "Creature — Wall", "Defender (This creature can't attack.)")]
    [InlineData("Drift of the Dead", "Creature — Wall",
        "Defender (This creature can't attack.)\nThis creature's power and toughness are each equal to the number of snow lands you control.")]
    [InlineData("Deep-Sea Serpent", "Creature — Serpent", "This creature can't attack unless defending player controls an Island.")]
    public void Classify_ACreatureThatOnlyRestrainsItself_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Fact]
    public void Classify_AWallThatAlsoLocksSomebodyElse_StaysPacify()
    {
        // The exclusion asks whether anything is aimed OUTWARD, so a card that
        // both has defender and taps somebody down keeps the tag.
        CardEffect result = EffectClassifier.Classify(MakeCard("Kraken Wall", "Creature — Wall",
            "Defender (This creature can't attack.)\n{2}, {T}: Tap target creature an opponent controls."));

        Assert.True(result.HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Card parity dressed as card advantage. A loot draws and discards in the
    // same breath (ruled Filter alone on 2026-09-04); cycling pays a card to
    // replace itself and fired only through its reminder text; and an ability
    // that sacrifices the permanent runs once, so it is not the repeatable draw
    // the 2026-09-11 ruling asked for.
    [InlineData("Bazaar of Baghdad", "Land", "{T}: Draw two cards, then discard three cards.")]
    [InlineData("Boosted Sloop", "Artifact — Vehicle", "Menace\nWhenever you attack, draw a card, then discard a card.\nCrew 1")]
    [InlineData("Airship Crash", "Sorcery",
        "Destroy target artifact, enchantment, or creature with flying.\nCycling {2} ({2}, Discard this card: Draw a card.)")]
    [InlineData("Airship Engine Room", "Land",
        "This land enters tapped.\n{T}: Add {U} or {R}.\n{4}, {T}, Sacrifice this land: Draw a card.")]
    public void Classify_CardParity_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // THE CARD ITSELF IS A CARD, ruled 2026-09-19, and it completes a count
    // that had been half-done since 2026-09-16. Ponder is -1 for the Ponder and
    // +1 for the draw, which is nothing; Grab the Prize is -1 for the spell, -1
    // for the discard it charges and +2, which is also nothing and makes it
    // Abandon Attachments with the discard moved into the cost line. So a
    // ONE-SHOT has to draw two more than it pays before it has gained anything.
    //
    // Racers' Scoreboard is the case that proves it: it prints the same words
    // as Emmessi Tome ("draw two cards, then discard a card") on an ENTERS
    // trigger instead of an activated ability, and nets nothing.
    [InlineData("Ponder", "Sorcery",
        "Look at the top three cards of your library, then put them back in any order. You may shuffle.\n"
        + "Draw a card.")]
    [InlineData("Grab the Prize", "Sorcery",
        "As an additional cost to cast this spell, discard a card.\n"
        + "Draw two cards. If the discarded card wasn't a land card, Grab the Prize deals 2 damage to each opponent.")]
    [InlineData("Racers' Scoreboard", "Artifact",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns when "
        + "an opponent loses life. Max speed is 4.)\n"
        + "When this artifact enters, draw two cards, then discard a card.\n"
        + "Max speed — Spells you cast cost {1} less to cast.")]
    [InlineData("Romantic Rendezvous", "Sorcery", "Discard a card, then draw two cards.")]
    [InlineData("Careful Study", "Sorcery", "Draw two cards, then discard two cards.")]
    [InlineData("Alpharael, Dreaming Acolyte", "Legendary Creature — Human Cleric",
        "When Alpharael enters, draw two cards. Then discard two cards unless you discard an artifact card.\n"
        + "During your turn, Alpharael has deathtouch.")]
    // An OMEN shuffles itself away, it is not cast twice (2026-09-29).
    [InlineData("Stormshriek Feral // Flush Out", "Creature — Dragon // Sorcery — Omen",
        "Flying, haste\n{1}{R}: This creature gets +1/+0 until end of turn.\nDiscard a card. If you do, draw "
        + "two cards. (Then shuffle this card into its owner's library.)")]
    public void Classify_AOneShotThatPaysForItself_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A REPEATABLE ability paid for the card once and never again, so one more
    // than it hands back is already a card: that is the whole difference
    // between Emmessi Tome and Racers' Scoreboard above. A one-shot qualifies
    // at two, which Casting of Bones reaches — and which never parsed, because
    // "discard ONE OF THEM" does not repeat the word "card".
    [InlineData("Emmessi Tome", "Artifact — Book", "{5}, {T}: Draw two cards, then discard a card.")]
    [InlineData("Casting of Bones", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, draw three cards, then discard one of them.")]
    [InlineData("Focus the Mind", "Instant",
        "This spell costs {2} less to cast if you've cast another spell this turn.\n"
        + "Draw three cards, then discard a card.")]
    public void Classify_ALootThatGainsAfterPayingForTheCard_IsCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The two tags ARE exclusive, ruled 2026-09-27 — "se ha CardAdvantage non ha
    // Filter" — which overturns 2026-09-19. These three had pinned "both" until
    // then: Casting of Bones draws three and keeps two, Emmessi Tome loots two
    // for one every turn, and Plan the Heist surveils beside a draw three.
    [InlineData("Casting of Bones", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, draw three cards, then discard one of them.")]
    [InlineData("Emmessi Tome", "Artifact — Book", "{5}, {T}: Draw two cards, then discard a card.")]
    [InlineData("Plan the Heist", "Sorcery",
        "Surveil 3 if you have no cards in hand. Then draw three cards. (To surveil 3, look at the top three "
        + "cards of your library, then put any number of them into your graveyard and the rest on top of your "
        + "library in any order.)\nPlot {3}{U} (You may pay {3}{U} and exile this card from your hand. Cast it "
        + "as a sorcery on a later turn without paying its mana cost. Plot only as a sorcery.)")]
    public void Classify_ASelectionThatComesOutAhead_IsCardAdvantageAndNotFilter(
        string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // CardAdvantage and Filter on DIFFERENT abilities, ruled 2026-09-29 ("sono due
    // abilità diverse"): a repeatable selection beside a separate draw does both.
    [InlineData("Qiqirn Merchant", "Creature — Beast Citizen",
        "{1}, {T}: Draw a card, then discard a card.\n{7}, {T}, Sacrifice this creature: Draw three cards. "
        + "This ability costs {1} less to activate for each Town you control.")]
    [InlineData("Dimir Strandcatcher", "Creature — Faerie Rogue",
        "Flying\nWhenever you attack, surveil X, where X is the number of opponents being attacked.\nAt the "
        + "beginning of each end step, if three or more cards were put into your graveyard from anywhere other "
        + "than the battlefield this turn, draw a card.")]
    [InlineData("Arcade Gannon", "Legendary Creature — Human Doctor",
        "{T}: Draw a card, then discard a card. Put a quest counter on Arcade Gannon.\nFor Auld Lang Syne — "
        + "Once during each of your turns, you may cast an artifact or Human spell from your graveyard with "
        + "mana value less than or equal to the number of quest counters on Arcade Gannon.")]
    [InlineData("Veronica, Dissident Scribe", "Legendary Creature — Human Artificer Rogue",
        "Menace\nWhenever Veronica attacks, you may discard a card. If you do, draw a card.\nWhenever you "
        + "discard one or more nonland cards for the first time each turn, create a Junk token. (It's an "
        + "artifact with \"{T}, Sacrifice this token: Exile the top card of your library. You may play that card "
        + "this turn. Activate only as a sorcery.\")")]
    public void Classify_SelectingAndGainingOnDifferentAbilities_IsBoth(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // …but not a selection in the SAME ability as the draw (True Identity,
    // Champions from Beyond), one made ONCE beside a draw engine (Case of the
    // Crimson Pulse, Kaho), nor cards searched out and exiled to be CAST (Ugin,
    // 2026-09-29).
    [InlineData("True Identity", "Enchantment",
        "Whenever this enchantment or another permanent you control is turned face up, scry 1, then draw a "
        + "card. This ability triggers only once each turn.\nDisguise {W} (You may cast this card face down for "
        + "{3} as a 2/2 creature with ward {2}. Turn it face up any time for its disguise cost.)")]
    [InlineData("Champions from Beyond", "Enchantment",
        "When this enchantment enters, create X 1/1 colorless Hero creature tokens.\nLight Party — Whenever "
        + "you attack with four or more creatures, scry 2, then draw a card.\nFull Party — Whenever you attack "
        + "with eight or more creatures, those creatures get +4/+4 until end of turn.")]
    [InlineData("Case of the Crimson Pulse", "Enchantment — Case",
        "When this Case enters, discard a card, then draw two cards.\nTo solve — You have no cards in hand. "
        + "(If unsolved, solve at the beginning of your end step.)\nSolved — At the beginning of your upkeep, "
        + "discard your hand, then draw two cards.")]
    [InlineData("Kaho, Minamo Historian", "Legendary Creature — Human Wizard",
        "When Kaho enters, search your library for up to three instant cards, exile them, then shuffle.\n{X}, "
        + "{T}: You may cast a spell with mana value X from among cards exiled with Kaho without paying its "
        + "mana cost.")]
    [InlineData("Ugin, Eye of the Storms", "Legendary Planeswalker — Ugin",
        "When you cast this spell, exile up to one target permanent that's one or more colors.\nWhenever you "
        + "cast a colorless spell, exile up to one target permanent that's one or more colors.\n+2: You gain 3 "
        + "life and draw a card.\n0: Add {C}{C}{C}.\n−11: Search your library for any number of colorless nonland "
        + "cards, exile them, then shuffle. Until end of turn, you may cast those cards without paying their "
        + "mana costs.")]
    public void Classify_SelectingOnceOrInTheSameAbility_IsCardAdvantageAlone(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A card changing places at exactly no net gain, however the halves are
    // worded, is Filter and nothing else.
    [InlineData("Grab the Prize", "Sorcery",
        "As an additional cost to cast this spell, discard a card.\n"
        + "Draw two cards. If the discarded card wasn't a land card, Grab the Prize deals 2 damage to each opponent.")]
    [InlineData("Romantic Rendezvous", "Sorcery", "Discard a card, then draw two cards.")]
    [InlineData("Anvil of Bogardan", "Artifact",
        "Players have no maximum hand size.\n"
        + "At the beginning of each player's draw step, that player draws an additional card, then discards a card.")]
    public void Classify_ACardChangingPlaces_IsAlwaysFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The rummage: "you may discard a card. If you do, draw a card". The
    // plainest Filter shape printed and it had never been read — 14 cards fire
    // on it and 12 were already tagged by hand. Ruled 2026-09-19.
    [InlineData("Yuyan Archers", "Creature — Human Archer",
        "Reach\nWhen this creature enters, you may discard a card. If you do, draw a card.")]
    [InlineData("Rescue Leopard", "Creature — Cat",
        "Whenever this creature becomes tapped, you may discard a card. If you do, draw a card.")]
    // The activated version. The rule carries a lookahead for "this card"
    // because cycling reminder text reads identically and pays a card to
    // replace itself, which attacks nobody.
    [InlineData("Mesmeric Trance", "Enchantment",
        "Cumulative upkeep {1} (At the beginning of your upkeep, put an age counter on this permanent, "
        + "then sacrifice it unless you pay its upkeep cost for each age counter on it.)\n"
        + "{U}, Discard a card: Draw a card.")]
    public void Classify_Rummage_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A scry or surveil made ONCE beside what the card is for is not Filter,
    // ruled 2026-09-27 — "non li voglio come Filter, generano troppo rumore" —
    // which overturns the "unconditionally" these three pinned from 2026-09-19:
    // a bounce spell, a Vehicle and a creature. (Kozilek's Command was the
    // fourth, and moved out the same evening — a spell's MODE is its own effect.)
    [InlineData("Unauthorized Exit", "Instant",
        "Return target nonland permanent to its owner's hand. Surveil 1. "
        + "(Look at the top card of your library. You may put it into your graveyard.)")]
    [InlineData("Voyager Glidecar", "Artifact — Vehicle",
        "When this Vehicle enters, scry 1.\nTap three other untapped creatures you control: Until end of turn, "
        + "this Vehicle becomes an artifact creature and gains flying. Put a +1/+1 counter on it.\nCrew 1")]
    // Read in every printed form, "scry X" and "scries" included, so the veto
    // reaches every card the tag used to.
    [InlineData("Cascade Seer", "Creature — Merfolk Wizard",
        "When this creature enters, scry X, where X is the number of creatures in your party. "
        + "(Your party consists of up to one each of Cleric, Rogue, Warrior, and Wizard.)")]
    // The reminder text goes with the keyword: "(Look at the top two cards of
    // your library …)" is a look at the top in its own right.
    [InlineData("Consuming Ashes", "Instant",
        "Exile target creature. If it had mana value 3 or less, surveil 2. (Look at the top two cards of your "
        + "library, then put any number of them into your graveyard and the rest on top of your library in any "
        + "order.)")]
    // A LOOT tacked onto a spell that does something else is the same rider,
    // ruled on Refute: "non ha Filter, solo counter". Transpose's token carries
    // a quoted trigger of its own, which does not make the loot repeat.
    [InlineData("Refute", "Instant",
        "Counter target spell. Draw a card, then discard a card.")]
    [InlineData("Transpose", "Instant",
        "Draw a card, then discard a card. You lose 1 life. If this spell was cast from your hand, create a "
        + "0/1 black Wizard creature token with \"Whenever you cast a noncreature spell, this token deals 1 "
        + "damage to each opponent.\"\nRebound (If you cast this spell from your hand, exile it as it resolves. "
        + "At the beginning of your next upkeep, you may cast this card from exile without paying its mana "
        + "cost.)")]
    // …and a Blood token is the same rider: "se singoli no".
    [InlineData("Blood Servitor", "Artifact Creature — Construct",
        "When this creature enters, create a Blood token. (It's an artifact with \"{1}, {T}, Discard a card, "
        + "Sacrifice this token: Draw a card.\")")]
    public void Classify_ASelectionMadeOnceInPassing_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // What keeps the tag: a spell that selects and does nothing else (Dreams of
    // Laguna), a scry that comes again (Veteran Guardmouse's valiant, Clandestine
    // Meddler's attacks — both named as slips on 2026-09-27), and Blood made
    // again and again ("se ripetuti sì").
    [InlineData("Dreams of Laguna", "Instant",
        "Surveil 1, then draw a card. (To surveil 1, look at the top card of your library. You may put it "
        + "into your graveyard.)\nFlashback {3}{U} (You may cast this card from your graveyard for its flashback "
        + "cost. Then exile it.)")]
    // A LAND's scry or surveil keeps the tag, ruled later the same day: "mi
    // serve come filtro sulle terre doppie".
    [InlineData("Undercity Sewers", "Land — Island Swamp",
        "({T}: Add {U} or {B}.)\nThis land enters tapped.\nWhen this land enters, surveil 1. (Look at the top "
        + "card of your library. You may put it into your graveyard.)")]
    [InlineData("Veteran Guardmouse", "Creature — Mouse Soldier",
        "Valiant — Whenever this creature becomes the target of a spell or ability you control for the first "
        + "time each turn, it gets +1/+0 and gains first strike until end of turn. Scry 1. (Look at the top "
        + "card of your library. You may put that card on the bottom.)")]
    [InlineData("Clandestine Meddler", "Creature — Vampire Rogue",
        "When this creature enters, suspect up to one other target creature you control. (A suspected "
        + "creature has menace and can't block.)\nWhenever one or more suspected creatures you control attack, "
        + "surveil 1. (Look at the top card of your library. You may put it into your graveyard.)")]
    [InlineData("Ivora, Insatiable Heir", "Legendary Creature — Vampire Warrior",
        "Trample\nWhen Ivora enters and whenever it deals combat damage to a player, create a Blood token. "
        + "(It's an artifact with \"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\nWhenever you "
        + "discard a card, put a +1/+1 counter on Ivora.")]
    // A spell's MODE is an effect of its own — "è una spell, quindi conta
    // l'effetto secco (come Ponder)" — so a Command's scry-and-draw and a
    // Charm's loot are selections you chose, not riders.
    [InlineData("Kozilek's Command", "Kindred Instant — Eldrazi",
        "Choose two —\n"
        + "• Target player creates X 0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this token: Add {C}.\"\n"
        + "• Target player scries X, then draws a card.\n"
        + "• Exile target creature with mana value X or less.\n"
        + "• Exile up to X target cards from graveyards.")]
    [InlineData("Treva's Charm", "Instant",
        "Choose one —\n• Destroy target enchantment.\n• Exile target attacking creature.\n• Draw a card, then "
        + "discard a card.")]
    // A scry HANDED to something that repeats it keeps the tag even when the
    // hand-over is once: the Sorcerer Role scries on every attack.
    [InlineData("Unassuming Sage", "Creature — Human Peasant Wizard",
        "When this creature enters, you may pay {2}. If you do, create a Sorcerer Role token attached to it. "
        + "(Enchanted creature gets +1/+1 and has \"Whenever this creature attacks, scry 1.\")")]
    public void Classify_ASelectionThatIsTheCardOrComesAgain_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    public void Classify_BloodAgainAndAgain_IsARummageNotACard()
    {
        // The Blood reminder's "Draw a card" is half of a rummage, and had been
        // read as a repeatable draw. Ruled 2026-09-27 with the Blood token.
        CardEffect result = EffectClassifier.Classify(MakeCard("Moonstone Eulogist", "Creature — Bat Warlock",
            "Flying\nWhenever a creature an opponent controls dies, you create a Blood token. (It's an artifact "
            + "with \"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\nWhenever you sacrifice an "
            + "artifact, put a +1/+1 counter on this creature and you gain 1 life."));
        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    public void Classify_TwoBloodAtOnce_IsNeitherACardNorASelection()
    {
        // Two made once are two rummages made once: no card, and no stream.
        // The plural reminder ("They're artifacts with") had read as two cards.
        CardEffect result = EffectClassifier.Classify(MakeCard("Falkenrath Celebrants", "Creature — Vampire",
            "Menace (This creature can't be blocked except by two or more creatures.)\nWhen this creature enters, "
            + "create two Blood tokens. (They're artifacts with \"{1}, {T}, Discard a card, Sacrifice this token: "
            + "Draw a card.\")"));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // SOMEBODY ELSE'S TOP, rearranged or trimmed, ruled 2026-09-27: "sì,
    // guardare e basta no". Elemental Augury and Cruel Fate were untagged, Eye
    // Spy tagged, on the same act.
    [InlineData("Elemental Augury", "Enchantment",
        "{3}: Look at the top three cards of target player's library, then put them back in any order.")]
    [InlineData("Cruel Fate", "Sorcery",
        "Look at the top five cards of target opponent's library. Put one of those cards into that player's "
        + "graveyard and the rest on top of their library in any order.")]
    [InlineData("Eye Spy", "Sorcery",
        "Look at the top card of target player's library. You may put that card into their graveyard.")]
    // MILL OR REVEAL A FEW AND KEEP ONE, the same day; Eerie Gravestone was the
    // human's slip.
    [InlineData("Cache Grab", "Instant",
        "Mill four cards. You may put a permanent card from among the cards milled this way into your hand. "
        + "If you control a Squirrel or returned a Squirrel card to your hand this way, create a Food token. "
        + "(To mill four cards, put the top four cards of your library into your graveyard. A Food token is an "
        + "artifact with \"{2}, {T}, Sacrifice this token: You gain 3 life.\")")]
    [InlineData("Eerie Gravestone", "Artifact",
        "When this artifact enters, draw a card.\n{1}{B}, Sacrifice this artifact: Mill four cards. You may "
        + "put a creature card from among them into your hand. (To mill four cards, put the top four cards of "
        + "your library into your graveyard.)")]
    [InlineData("Wood Sage", "Creature — Human Druid",
        "{T}: Choose a creature card name. Reveal the top four cards of your library and put all of them with "
        + "that name into your hand. Put the rest into your graveyard.")]
    // Five more named Filter by the human the same day.
    [InlineData("Winds of Change", "Sorcery",
        "Each player shuffles the cards from their hand into their library, then draws that many cards.")]
    [InlineData("Foresight", "Sorcery",
        "Search your library for three cards, exile them, then shuffle.\nDraw a card at the beginning of the "
        + "next turn's upkeep.")]
    [InlineData("Mana Severance", "Sorcery",
        "Search your library for any number of land cards, exile them, then shuffle.")]
    [InlineData("Scroll Rack", "Artifact",
        "{1}, {T}: Exile any number of cards from your hand face down. Put that many cards from the top of "
        + "your library into your hand. Then look at the exiled cards and put them on top of your library in "
        + "any order.")]
    public void Classify_SelectionRuledIn_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Only LOOKING at somebody's top is nothing, and manifest dread's reminder
    // has the victim look at their own.
    [InlineData("Orcish Spy", "Creature — Orc Rogue",
        "{T}: Look at the top three cards of target player's library.")]
    [InlineData("Fear of Impostors", "Enchantment Creature — Nightmare",
        "Flash\nWhen this creature enters, counter target spell. Its controller manifests dread. (That player "
        + "looks at the top two cards of their library, then puts one onto the battlefield face down as a 2/2 "
        + "creature and the other into their graveyard. If it's a creature card, it can be turned face up any "
        + "time for its mana cost.)")]
    // A pile you then PLAY from is Steal, however it was trimmed.
    [InlineData("Black Cat, Cunning Thief", "Legendary Creature — Human Rogue Villain",
        "When Black Cat enters, look at the top nine cards of target opponent's library, exile two of them "
        + "face down, then put the rest on the bottom of their library in a random order. You may play the "
        + "exiled cards for as long as they remain exiled. Mana of any type can be spent to cast spells this "
        + "way.")]
    // A card looked at and cast for free was cheated in, not chosen between.
    [InlineData("Perception Bobblehead", "Artifact — Bobblehead",
        "{T}: Add one mana of any color.\n{3}, {T}: Look at the top X cards of your library, where X is the "
        + "number of Bobbleheads you control. You may cast a spell with mana value 3 or less from among them "
        + "without paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    public void Classify_SelectionRuledOut_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // LOOKING at the top few and casting one free is Cheat, named on Perception
    // Bobblehead on 2026-09-27; Aetherworks Marvel is the same card.
    [InlineData("Perception Bobblehead", "Artifact — Bobblehead",
        "{T}: Add one mana of any color.\n{3}, {T}: Look at the top X cards of your library, where X is the "
        + "number of Bobbleheads you control. You may cast a spell with mana value 3 or less from among them "
        + "without paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Aetherworks Marvel", "Legendary Artifact",
        "Whenever a permanent you control is put into a graveyard, you get {E} (an energy counter).\n{T}, Pay "
        + "six {E}: Look at the top six cards of your library. You may cast a spell from among them without "
        + "paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    public void Classify_LookAndCastOneFree_IsCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // A LAND kept out of the few is ManaFixing, ruled 2026-09-27: "in caso di
    // terre è ManaFix".
    [InlineData("Ainok Wayfarer", "Creature — Dog Scout",
        "When this creature enters, mill three cards. You may put a land card from among them into your hand. "
        + "If you don't, put a +1/+1 counter on this creature. (To mill three cards, put the top three cards of "
        + "your library into your graveyard.)")]
    [InlineData("Satyr Wayfinder", "Creature — Satyr",
        "When this creature enters, reveal the top four cards of your library. You may put a land card from "
        + "among them into your hand. Put the rest into your graveyard.")]
    [InlineData("Contagious Vorrac", "Creature — Phyrexian Boar Beast",
        "When this creature enters, look at the top four cards of your library. You may reveal a land card "
        + "from among them and put it into your hand. Put the rest on the bottom of your library in a random "
        + "order. If you didn't put a card into your hand this way, proliferate. (Choose any number of "
        + "permanents and/or players, then give each another counter of each kind already there.)")]
    public void Classify_ALandKeptFromTheFew_IsManaFixingAndNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Parity however it is worded, named Filter by the human on 2026-09-27:
    // a land may stand in for the discard, and N for N is still a rummage.
    [InlineData("Highway Robbery", "Sorcery",
        "You may discard a card or sacrifice a land. If you do, draw two cards.\nPlot {1}{R} (You may pay "
        + "{1}{R} and exile this card from your hand. Cast it as a sorcery on a later turn without paying its "
        + "mana cost. Plot only as a sorcery.)")]
    [InlineData("Horrid Shadowspinner", "Creature — Horror",
        "Lifelink\nWhenever this creature attacks, you may draw cards equal to its power. If you do, discard "
        + "that many cards.")]
    public void Classify_AnExchangeAtParity_IsFilterAndNotCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Named CardAdvantage by the human on 2026-09-27: a pile of permanents
    // traded for as many cards, a draw mode back every turn, and a look that
    // keeps every card it saw.
    [InlineData("Pitiless Carnage", "Sorcery",
        "Sacrifice any number of permanents you control, then draw that many cards.\nPlot {1}{B}{B} (You may "
        + "pay {1}{B}{B} and exile this card from your hand. Cast it as a sorcery on a later turn without "
        + "paying its mana cost. Plot only as a sorcery.)")]
    [InlineData("Monument to Endurance", "Artifact",
        "Whenever you discard a card, choose one that hasn't been chosen this turn —\n• Draw a card.\n• Create "
        + "a Treasure token.\n• Each opponent loses 3 life.")]
    [InlineData("Make Your Own Luck", "Sorcery",
        "Look at the top three cards of your library. You may exile a nonland card from among them. If you "
        + "do, it becomes plotted. Put the rest into your hand. (You may cast it as a sorcery on a later turn "
        + "without paying its mana cost.)")]
    // Keeping ALL of a kind, and milling to keep one over and over, are the
    // several-cards reading — both hand-tagged CardAdvantage and read once
    // keeping one became Filter.
    [InlineData("Marina Vendrell", "Legendary Creature — Human Warlock",
        "When Marina Vendrell enters, reveal the top seven cards of your library. Put all enchantment cards "
        + "from among them into your hand and the rest on the bottom of your library in a random order.\n{T}: "
        + "Lock or unlock a door of target Room you control. Activate only as a sorcery.")]
    [InlineData("Sludge Titan", "Creature — Zombie Giant",
        "Trample\nWhenever this creature enters or attacks, mill five cards. You may put a creature card "
        + "and/or a land card from among them into your hand.")]
    [InlineData("Szarekh, the Silent King", "Legendary Artifact Creature — Necron",
        "Flying\nMy Will Be Done — Whenever Szarekh attacks, mill three cards. You may put an artifact "
        + "creature card or Vehicle card from among the cards milled this way into your hand.")]
    // "Up to two", revealed: more than one, whatever the verb.
    [InlineData("Pieces of the Puzzle", "Sorcery",
        "Reveal the top five cards of your library. Put up to two instant and/or sorcery cards from among "
        + "them into your hand and the rest into your graveyard.")]
    // OFFSPRING makes the enters trigger fire twice: "solo CA", ruled the same day.
    [InlineData("Thundertrap Trainer", "Creature — Otter Wizard",
        "Offspring {4} (You may pay an additional {4} as you cast this spell. If you do, when this creature "
        + "enters, create a 1/1 token copy of it.)\nWhen this creature enters, look at the top four cards of "
        + "your library. You may reveal a noncreature, nonland card from among them and put it into your hand. "
        + "Put the rest on the bottom of your library in a random order.")]
    public void Classify_ADrawRuledIn_IsCardAdvantageAndNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The [\w ] runs in the recursion rules could not cross a COMMA or a SLASH,
    // and that is exactly where modern cards put their card-type lists. Widened
    // 2026-09-19; worth 4 Regrowth and 2 Reanimate at no cost.
    [InlineData("Archenemy's Charm", "Instant",
        "Choose one —\n• Exile target creature or planeswalker.\n"
        + "• Return one or two target creature and/or planeswalker cards from your graveyard to your hand.\n"
        + "• Put two +1/+1 counters on target creature you control. It gains lifelink until end of turn.")]
    [InlineData("Elena, Turk Recruit", "Legendary Creature — Human Assassin",
        "When Elena enters, return target non-Assassin historic card from your graveyard to your hand. "
        + "(Artifacts, legendaries, and Sagas are historic.)\n"
        + "Whenever you cast a historic spell, put a +1/+1 counter on Elena.")]
    public void Classify_RecursionWithACardTypeList_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // The same effect written PUT instead of RETURN, with the graveyard named
    // any of the ways the game names it. All six reviewed cards written this
    // way are tagged, and none of them read before 2026-09-19.
    [InlineData("Ashen Powder", "Sorcery",
        "Put target creature card from an opponent's graveyard onto the battlefield under your control.")]
    [InlineData("Chorale of the Void", "Enchantment — Aura",
        "Enchant creature you control\n"
        + "Whenever enchanted creature attacks, put target creature card from defending player's graveyard "
        + "onto the battlefield under your control tapped and attacking.\n"
        + "Void — At the beginning of your end step, sacrifice this Aura unless a nonland permanent left the "
        + "battlefield this turn or a spell was warped this turn.")]
    // A return whose card is qualified at length (mana value less than or equal to
    // its power) is read up to 90 characters, as Regrowth's is: Carmen, tagged by
    // the human on 2026-09-29, and Sandbender Scavengers.
    [InlineData("Carmen, Cruel Skymarcher", "Legendary Creature — Vampire Soldier",
        "Flying\nWhenever a player sacrifices a permanent, put a +1/+1 counter on Carmen and you gain 1 "
        + "life.\nWhenever Carmen attacks, return up to one target permanent card with mana value less than or "
        + "equal to Carmen's power from your graveyard to the battlefield.")]
    [InlineData("Sandbender Scavengers", "Creature — Human Rogue",
        "Whenever you sacrifice another permanent, put a +1/+1 counter on this creature.\nWhen this creature "
        + "dies, you may exile it. When you do, return target creature card with mana value less than or equal "
        + "to this creature's power from your graveyard to the battlefield.")]
    public void Classify_PuttingACreatureCardOntoTheBattlefield_IsReanimate(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // A LAND out of the graveyard rebuilds a mana base; it reanimates nothing.
    // Follows the 2026-09-18 Ramp ruling and was read here 2026-09-19: 7
    // reviewed cards put one back and only 1 carries Reanimate.
    [InlineData("Summon: Titan", "Enchantment Creature — Saga Giant",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Mill five cards.\n"
        + "II — Return all land cards from your graveyard to the battlefield tapped.\n"
        + "III — Until end of turn, another target creature you control gains trample and gets +X/+X, where X "
        + "is the number of lands you control.\nReach, trample")]
    [InlineData("Floral Evoker", "Creature — Snake Druid",
        "Landfall — Whenever a land you control enters, put a +1/+1 counter on this creature.\n"
        + "{G}, Discard a creature card: Return target land card from your graveyard to the battlefield tapped.")]
    public void Classify_ALandOutOfTheGraveyard_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // The TOP OF A LIBRARY is a hand you wait one turn for, so a card lifted
    // out of a graveyard and put there is recursion. Ruled 2026-09-19, and all
    // five reviewed cards written this way were already tagged by hand.
    [InlineData("Reinforcements", "Instant",
        "Put up to three target creature cards from your graveyard on top of your library.")]
    [InlineData("Bone Harvest", "Instant",
        "Put any number of target creature cards from your graveyard on top of your library.\n"
        + "Draw a card at the beginning of the next turn's upkeep.")]
    public void Classify_OutOfAGraveyardOntoALibrary_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // The BOTTOM is not in it: putting a card on the bottom of a library is
    // graveyard hate, not recursion. Nor is an OPPONENT'S graveyard, which is
    // shuffling their answers back in rather than rebuying your own.
    [InlineData("Barkform Harvester", "Artifact Creature — Shapeshifter",
        "Changeling (This card is every creature type.)\nReach\n"
        + "{2}: Put target card from your graveyard on the bottom of your library.")]
    [InlineData("Misinformation", "Instant",
        "Put up to three target cards from an opponent's graveyard on top of their library in any order.")]
    public void Classify_GraveyardHate_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // CASTING ANOTHER CARD out of a graveyard is reanimation for spells: the
    // card never reaches your hand, but it is bought back from the same place
    // and for the same reason. Ruled 2026-09-19. 12 reviewed cards do it and 5
    // were already tagged.
    [InlineData("Edgar, Master Machinist", "Legendary Creature — Human Artificer Noble",
        "Once during each of your turns, you may cast an artifact spell from your graveyard. If you cast a "
        + "spell this way, that artifact enters tapped.\n"
        + "Tools — Whenever Edgar attacks, it gets +X/+0 until end of turn, where X is the greatest mana value "
        + "among artifacts you control.")]
    [InlineData("Noctis, Prince of Lucis", "Legendary Creature — Human Noble",
        "Lifelink\nYou may cast artifact spells from your graveyard by paying 3 life in addition to paying "
        + "their other costs. If you cast a spell this way, that artifact enters with a finality counter on it.")]
    // …including handing another card one of the keywords rather than casting
    // it yourself.
    [InlineData("Iroh, Grand Lotus", "Legendary Creature — Human Noble Ally",
        "Firebending 2\nDuring your turn, each non-Lesson instant and sorcery card in your graveyard has "
        + "flashback. The flashback cost is equal to that card's mana cost.")]
    [InlineData("Songcrafter Mage", "Creature — Human Bard",
        "Flash\nWhen this creature enters, target instant or sorcery card in your graveyard gains harmonize "
        + "until end of turn. Its harmonize cost is equal to its mana cost.")]
    public void Classify_CastingAnotherCardOutOfAGraveyard_IsRegrowth(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // The card that gives ITSELF a second cast is not. That split is what
    // rescued a family which first measured at 10% and looked like noise: 54
    // reviewed cards carry flashback, escape, harmonize, unearth or a cousin,
    // and exactly one is tagged Regrowth — Sorceress's Schemes, which earns it
    // on a different line by returning a card to HAND.
    [InlineData("Roamer's Routine", "Sorcery",
        "Target player mills three cards. You gain 3 life.\nFlashback {3}{W}")]
    [InlineData("Undead Sprinter", "Creature — Zombie",
        "Trample, haste\nYou may cast this card from your graveyard if a non-Zombie creature died this turn. "
        + "If you do, this creature enters with a +1/+1 counter on it.")]
    // Three shapes that read like casting from a graveyard and are not: cost
    // reduction COUNTING cards there, a card exiled from there as a COST, and a
    // trigger that only watches a cast happen.
    [InlineData("Cyan, Vengeful Samurai", "Legendary Creature — Human Samurai",
        "This spell costs {1} less to cast for each creature card in your graveyard.\nDouble strike\n"
        + "Whenever one or more creature cards leave your graveyard, put a +1/+1 counter on Cyan.")]
    [InlineData("Haunting Misery", "Sorcery",
        "As an additional cost to cast this spell, exile X creature cards from your graveyard.\n"
        + "Haunting Misery deals X damage to target player or planeswalker.")]
    [InlineData("Neerdiv, Devious Diver", "Legendary Creature — Merfolk Rogue",
        "Whenever Neerdiv becomes tapped, target player mills cards equal to its power.\n"
        + "Whenever you cast a spell from your graveyard or activate an ability of a card in your graveyard, "
        + "draw a card and put a +1/+1 counter on Neerdiv.")]
    public void Classify_ASecondCastForItself_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // The graveyard is not the only place a creature comes back from. A card
    // THIS CARD exiled, put onto the battlefield under your control, is a
    // reanimation by another route. Ruled 2026-09-19.
    [InlineData("Ghost Vacuum", "Artifact",
        "{T}: Exile target card from a graveyard.\n"
        + "{6}, {T}, Sacrifice this artifact: Put each creature card exiled with this artifact onto the "
        + "battlefield under your control with a flying counter on it. Each of them is a 1/1 Spirit in "
        + "addition to its other types. Activate only as a sorcery.")]
    [InlineData("Purgatory", "Enchantment",
        "Whenever a nontoken creature is put into your graveyard from the battlefield, exile that card.\n"
        + "At the beginning of your upkeep, you may pay {4} and 2 life. If you do, return a card exiled with "
        + "this enchantment to the battlefield.")]
    // …and a token COPY of a creature card in a graveyard, which is not the
    // card itself but puts the same thing on the table.
    [InlineData("Cursecloth Wrappings", "Artifact",
        "Zombies you control get +1/+1.\n"
        + "{T}: Target creature card in your graveyard gains embalm until end of turn. The embalm cost is "
        + "equal to its mana cost. (Exile that card and pay its embalm cost: Create a token that's a copy of "
        + "it, except it's a white Zombie in addition to its other types and has no mana cost. Embalm only as "
        + "a sorcery.)")]
    public void Classify_ComingBackFromExileOrAsACopy_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Exiling a creature YOU CONTROL and handing it back is a blink, and a
    // strange kind of protection rather than a reanimation. Ruled 2026-09-19
    // and deliberately left untagged — it is the guard that separates these
    // two from Ghost Vacuum and Purgatory above.
    [InlineData("Cold Storage", "Artifact",
        "{3}: Exile target creature you control.\n"
        + "Sacrifice this artifact: Return each creature card exiled with this artifact to the battlefield "
        + "under your control.")]
    [InlineData("Safe Haven", "Land",
        "{2}, {T}: Exile target creature you control.\n"
        + "At the beginning of your upkeep, you may sacrifice this land. If you do, return each card exiled "
        + "with this land to the battlefield under its owner's control.")]
    public void Classify_BlinkingYourOwnCreature_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // EMPOWER JACE N makes a Jace planeswalker token whose whole job is
    // "[-1]: Surveil 1" and "[-3]: Draw a card", so the keyword filters by
    // itself. Ruled 2026-09-19. Thirty-five cards print it; 31 carry reminder
    // text that the surveil rule already reads, and these are the four that do
    // not.
    [InlineData("Sanctum Lurker", "Creature — Horror",
        "When this creature enters, empower Jace 1.\n"
        + "Planeswalkers you control aren't put into their owners' graveyards for having 0 loyalty.\n"
        + "Planeswalkers you control have \"[+2]: This planeswalker deals 1 damage to each opponent and you "
        + "gain 1 life.\"")]
    [InlineData("Theorist's Sanctum", "Land — Island",
        "({T}: Add {U}.)\n"
        + "As this land enters, you may behold a Jace. If you don't, this land enters tapped. (To behold a "
        + "Jace, choose a Jace you control or reveal a Jace card from your hand.)\n"
        + "{2}{U}, {T}: Empower Jace 2.")]
    [InlineData("Jace, Reality Sculptor", "Legendary Planeswalker — Jace",
        "+1: Empower Jace X, where X is the number of Islands you control.\n"
        + "−3: Until your next turn, whenever a creature attacks you or a planeswalker you control, it gets "
        + "-5/-0 until end of turn.\n"
        + "0: Exile all but the bottom card of each opponent's library. Activate only if there are twenty-five "
        + "or more loyalty counters among Jaces you control.")]
    [InlineData("Fatehold Charm", "Instant",
        "Choose one —\n• Draw a card. Empower Jace 2.\n• Return target spell or creature to its owner's hand.\n"
        + "• Creatures you control get +1/+2 until end of turn.")]
    public void Classify_EmpowerJace_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    // …and the Jace it makes is a PLANESWALKER token, not a creature, so the
    // keyword must not drag Tokens along with it.
    public void Classify_EmpowerJace_IsNotTokens()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(
            "Arcane Amphisbaena", "Creature — Snake",
            "Deathtouch\nWhen this creature enters, empower Jace 2. (Put two loyalty counters on a Jace token "
            + "you control. If you don't control one, first create a blue Jace planeswalker token with "
            + "\"[−1]: Surveil 1\" and \"[−3]: Draw a card.\")"));

        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.False(result.HasFlag(CardEffect.Tokens));
    }

    [Fact]
    // The one shape the count keeps out: a trigger that WATCHES a scry happen
    // is not a scry, the same reading that keeps "whenever you draw a card" out
    // of CardAdvantage. Planetarium of Wan Shi Tong still filters — it has its
    // own "{1}, {T}: Scry 2" — so the card is checked without that line.
    public void Classify_ATriggerThatWatchesAScry_IsNotItselfFilter()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(
            "Planetarium of Wan Shi Tong", "Legendary Artifact",
            "Whenever you scry or surveil, look at the top card of your library. You may cast that card without "
            + "paying its mana cost. Do this only once each turn. (Look at the card after you scry or surveil.)"));

        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Whose draw is it? A card that only ever draws for somebody ELSE gives
    // nothing away for free. Ruled 2026-09-19 over 10 cards, 8 untagged. The
    // test is done by STRIPPING rather than a lookbehind, because "defending
    // player MAY draw a card" puts a word between the subject and the verb.
    [InlineData("Sibilant Spirit", "Creature — Spirit",
        "Flying\nWhenever this creature attacks, defending player may draw a card.")]
    [InlineData("Phelddagrif", "Legendary Creature — Phelddagrif",
        "{G}: Phelddagrif gains trample until end of turn. Target opponent creates a 1/1 green Hippo creature token.\n"
        + "{W}: Phelddagrif gains flying until end of turn. Target opponent gains 2 life.\n"
        + "{U}: Return Phelddagrif to its owner's hand. Target opponent may draw a card.")]
    // Triggering OFF a draw is not drawing: 9 of the 10 cards written this way
    // carry no tag at all.
    [InlineData("Underworld Dreams", "Enchantment",
        "Whenever an opponent draws a card, this enchantment deals 1 damage to that player.")]
    [InlineData("Clinquant Skymage", "Creature — Bird Wizard",
        "Flying\nWhenever you draw a card, put a +1/+1 counter on this creature.")]
    // And a draw that exiles the card ITSELF out of your graveyard runs once,
    // the same reading that keeps a one-shot sacrifice out. Four of the fifty
    // over-fires were identical Surveyors printed with this line.
    [InlineData("Goblin Surveyor", "Creature — Goblin Scout",
        "Trample\nStart your engines! (If you have no speed, it starts at 1. It increases once on each of your "
        + "turns when an opponent loses life. Max speed is 4.)\n"
        + "Max speed — {3}, Exile this card from your graveyard: Draw a card.")]
    public void Classify_ADrawThatIsNotYoursOrRunsOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    // The victim has to be the one DRAWING. "A creature you control becomes the
    // target of a spell AN OPPONENT CONTROLS, draw a card" names an opponent in a
    // possessive clause and hands the card to you; twenty characters of filler
    // between the subject and the verb read that as somebody else's draw. Ruled
    // 2026-09-20 — the same trap GivesControlAway fell into the day before.
    public void Classify_ADrawNamingAnOpponentInPassing_IsStillCardAdvantage()
    {
        Card card = MakeCard("Surrak, Elusive Hunter", "Legendary Creature — Human Warrior",
            "This spell can't be countered.\nTrample\nWhenever a creature you control or a creature spell "
            + "you control becomes the target of a spell or ability an opponent controls, draw a card.");

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-09-20. Every one of these hands you a card you would otherwise
    // have had to draw, and hands you another one next turn: the ability has a
    // cost you can pay again, or a trigger that comes round again, so the card
    // it cost was paid once. Browse and Water Tribe Rallier take one off the top
    // of the library, Rediscover the Way does it on two Saga chapters, Elkin
    // Bottle and Parapet Thrasher exile the top card and let you play it, Tersa
    // Lightshatter does the same out of the graveyard, and Banon casts one
    // creature out of it on each of your turns.
    [InlineData("Browse", "Enchantment",
        "{2}{U}{U}: Look at the top five cards of your library, put one of them into your hand, "
        + "and exile the rest.")]
    [InlineData("Water Tribe Rallier", "Creature — Human Soldier Ally",
        "Waterbend {5}: Look at the top four cards of your library. You may reveal a creature card "
        + "with power 3 or less from among them and put it into your hand. Put the rest on the bottom "
        + "of your library in a random order.")]
    [InlineData("Rediscover the Way", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I, II — Look at the top three cards of your library. Put one of them into your hand and the "
        + "rest on the bottom of your library in any order.\n"
        + "III — Whenever you cast a noncreature spell this turn, target creature you control gains "
        + "double strike until end of turn.")]
    [InlineData("Elkin Bottle", "Artifact",
        "{3}, {T}: Exile the top card of your library. Until the beginning of your next upkeep, "
        + "you may play that card.")]
    [InlineData("Parapet Thrasher", "Creature — Dragon",
        "Flying\nWhenever one or more Dragons you control deal combat damage to an opponent, choose "
        + "one that hasn't been chosen this turn —\n• Destroy target artifact that opponent controls.\n"
        + "• This creature deals 4 damage to each other opponent.\n"
        + "• Exile the top card of your library. You may play it this turn.")]
    [InlineData("Tersa Lightshatter", "Legendary Creature — Orc Wizard",
        "Haste\nWhen Tersa Lightshatter enters, discard up to two cards, then draw that many cards.\n"
        + "Whenever Tersa Lightshatter attacks, if there are seven or more cards in your graveyard, "
        + "exile a card at random from your graveyard. You may play that card this turn.")]
    [InlineData("Banon, the Returners' Leader", "Legendary Creature — Human Rebel",
        "Pray — Once during each of your turns, you may cast a creature spell from among cards in your "
        + "graveyard that were put there from anywhere other than the battlefield this turn.\n"
        + "Whenever you attack, you may pay {1} and discard a card. If you do, draw a card.")]
    // The kept card qualified at length before "put it into your hand" (read up to
    // 200 characters since 2026-09-29): Radagast the Brown, tagged by the human,
    // and Whiskervale Forerunner.
    [InlineData("Radagast the Brown", "Legendary Creature — Avatar Wizard",
        "Whenever Radagast or another nontoken creature you control enters, look at the top X cards of your "
        + "library, where X is that creature's mana value. You may reveal a creature card that doesn't share a "
        + "creature type with a creature you control from among those cards and put it into your hand. Put the "
        + "rest on the bottom of your library in a random order.")]
    [InlineData("Whiskervale Forerunner", "Creature — Mouse Bard",
        "Valiant — Whenever this creature becomes the target of a spell or ability you control for the first "
        + "time each turn, look at the top five cards of your library. You may reveal a creature card with mana "
        + "value 3 or less from among them. You may put it onto the battlefield if it's your turn. If you don't "
        + "put it onto the battlefield, put it into your hand. Put the rest on the bottom of your library in a "
        + "random order.")]
    // Mill a few and keep ALL of a kind from among them, a card even once: Tazri
    // and Beluna Grandsquall, tagged by the human on 2026-09-29.
    [InlineData("Tazri, Stalwart Survivor", "Legendary Creature — Human Warrior",
        "Each creature you control has \"{T}: Add one mana of any of this creature's colors. Spend this mana "
        + "only to activate an ability of a creature. Activate only if this creature has another activated "
        + "ability.\"\n{W}{U}{B}{R}{G}, {T}: Mill five cards. Put all creature cards with activated abilities "
        + "that aren't mana abilities from among the milled cards into your hand.")]
    [InlineData("Beluna Grandsquall // Seek Thrills", "Legendary Creature — Giant Noble // Instant — Adventure",
        "Trample\nPermanent spells you cast that have an Adventure cost {1} less to cast.\nMill seven cards. "
        + "Then put all cards that have an Adventure from among the milled cards into your hand.")]
    public void Classify_ACardYouCanGoBackForEveryTurn_IsCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // And the one-shots that print the same words. Repeatability belongs to ONE
    // ABILITY: Morbius the Living Vampire and Lupinflower Village spend
    // themselves to pay for theirs, and Guru Pathik and Equilibrium Adept run
    // off an ENTERS trigger while carrying an unrelated "Whenever" underneath —
    // reading the card as a whole billed them for somebody else's repetition.
    // Rook Turret and Brainstorm hand back exactly what they took.
    [InlineData("Morbius the Living Vampire", "Legendary Creature — Vampire Scientist Villain",
        "Flying, vigilance, lifelink\n{U}{B}, Exile this card from your graveyard: Look at the top "
        + "three cards of your library. Put one of them into your hand and the rest on the bottom of "
        + "your library in any order.")]
    [InlineData("Lupinflower Village", "Land",
        "{T}: Add {C}.\n{T}: Add {W}. Spend this mana only to cast a creature spell.\n"
        + "{1}{W}, {T}, Sacrifice this land: Look at the top six cards of your library. You may reveal "
        + "a Bat, Bird, Mouse, or Rabbit card from among them and put it into your hand. Put the rest "
        + "on the bottom of your library in a random order.")]
    [InlineData("Guru Pathik", "Legendary Creature — Human Monk Ally",
        "When Guru Pathik enters, look at the top five cards of your library. You may reveal a Lesson, "
        + "Saga, or Shrine card from among them and put it into your hand. Put the rest on the bottom "
        + "of your library in a random order.\n"
        + "Whenever you cast a Lesson, Saga, or Shrine spell, put a +1/+1 counter on another target "
        + "creature you control.")]
    [InlineData("Equilibrium Adept", "Creature — Dog Monk",
        "When this creature enters, exile the top card of your library. Until the end of your next "
        + "turn, you may play that card.\n"
        + "Flurry — Whenever you cast your second spell each turn, this creature gains double strike "
        + "until end of turn.")]
    [InlineData("Rook Turret", "Artifact Creature — Construct",
        "Flying\nWhenever another artifact you control enters, you may draw a card. If you do, "
        + "discard a card.")]
    [InlineData("Brainstorm", "Instant",
        "Draw three cards, then put two cards from your hand on top of your library in any order.")]
    public void Classify_TheSameWordsRunOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-09-20: several cards off the top and only ONE of them playable
    // is two seen and one played, which is the count of a Filter. All three
    // reviewed cards say it in the same words, and the two that are not one-shot
    // spells still cannot repeat — Case of the Burning Masks sacrifices itself.
    [InlineData("Riverwheel Sweep", "Sorcery",
        "Tap target creature. Put three stun counters on it.\n"
        + "Exile the top two cards of your library. Choose one of them. Until the end of your next "
        + "turn, you may play that card.")]
    [InlineData("Heroes' Hangout", "Land",
        "Choose one —\n• Date Night — Exile the top two cards of your library. Choose one of them. "
        + "Until the end of your next turn, you may play that card.\n"
        + "• Patrol Night — One or two target creatures each get +1/+0 and gain first strike until "
        + "end of turn.")]
    [InlineData("Case of the Burning Masks", "Enchantment — Case",
        "When this Case enters, it deals 3 damage to target creature an opponent controls.\n"
        + "To solve — Three or more sources you controlled dealt damage this turn.\n"
        + "Solved — Sacrifice this Case: Exile the top three cards of your library. Choose one of "
        + "them. You may play that card this turn.")]
    public void Classify_SeveralOffTheTopAndOnePlayed_IsFilterNotCardAdvantage(
        string name, string typeLine, string oracle)
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(effects.HasFlag(CardEffect.CardAdvantage));
        Assert.True(effects.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-09-20, from the nine cards where the classifier said Filter and
    // the hand did not. Cloak and manifest dread turn what you picked FACE DOWN
    // into a 2/2, so what the ability gave you is a body and not a choice. A
    // LAND off the top is Ramp, the same rail that makes a land search Ramp
    // rather than Tutor. And "each opponent discards a card AND YOU DRAW a card"
    // is two players doing two different things, not one player trading.
    [InlineData("Curator Beastie", "Creature — Beast",
        "Reach\nColorless creatures you control enter with two additional +1/+1 counters on them.\n"
        + "Whenever this creature enters or attacks, manifest dread. (Look at the top two cards of "
        + "your library. Put one onto the battlefield face down as a 2/2 creature and the other into "
        + "your graveyard. Turn it face up any time for its mana cost if it's a creature card.)")]
    [InlineData("Hide in Plain Sight", "Sorcery",
        "Look at the top five cards of your library, cloak two of them, and put the rest on the bottom "
        + "of your library in a random order.")]
    [InlineData("Ignis Scientia", "Legendary Creature — Human Advisor",
        "When Ignis Scientia enters, look at the top six cards of your library. You may put a land card "
        + "from among them onto the battlefield tapped. Put the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Famished Worldsire", "Creature — Avatar",
        "Ward {3}\nDevour land 3\nWhen this creature enters, look at the top X cards of your library, "
        + "where X is this creature's power. Put any number of land cards from among them onto the "
        + "battlefield tapped, then shuffle.")]
    [InlineData("Jecht, Reluctant Guardian", "Legendary Enchantment Creature — Saga Human",
        "Menace\n(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I, II — Jecht Beam — Each opponent discards a card and you draw a card.\n"
        + "III — Ultimate Jecht Shot — Each opponent sacrifices two creatures of their choice.")]
    public void Classify_ThreeShapesThatOnlyLookLikeSelection_AreNotFilter(
        string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The carve-outs those three vetoes have to leave standing, each ruled the
    // same day. Planar Genesis takes a land if there is one and a CARD if there
    // is not, so it selects after all; hideaway looks at four and exiles one you
    // may play later, which is a hand you wait for; and "TARGET player" and
    // "EACH player" keep the loot, because you can point Forget at yourself and
    // because Flux loots everybody including you.
    [InlineData("Planar Genesis", "Sorcery",
        "Look at the top four cards of your library. You may put a land card from among them onto the "
        + "battlefield tapped. If you don't, put a card from among them into your hand. Put the rest on "
        + "the bottom of your library in a random order.")]
    [InlineData("Clive's Hideaway", "Land",
        "Hideaway 4 (When this land enters, look at the top four cards of your library, exile one face "
        + "down, then put the rest on the bottom in a random order.)\n{T}: Add {C}.\n"
        + "{2}, {T}: You may play the exiled card without paying its mana cost if you control four or "
        + "more legendary creatures.")]
    [InlineData("Forget", "Sorcery",
        "Target player discards two cards, then draws as many cards as they discarded this way.")]
    [InlineData("Flux", "Sorcery",
        "Each player discards any number of cards, then draws that many cards.\nDraw a card.")]
    public void Classify_WhatStillSelects_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    // The other half of the Jecht ruling: taking Filter away must not take the
    // three tags it really has. The edict on chapter III is Removal, the
    // opponents' discard is Discard, and the draw beside it is yours.
    public void Classify_ALootSplitBetweenTwoPlayers_KeepsItsOtherTags()
    {
        Card card = MakeCard("Jecht, Reluctant Guardian", "Legendary Enchantment Creature — Saga Human",
            "Menace\n(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
            + "I, II — Jecht Beam — Each opponent discards a card and you draw a card.\n"
            + "III — Ultimate Jecht Shot — Each opponent sacrifices two creatures of their choice.");

        CardEffect effects = EffectClassifier.Classify(card);

        Assert.True(effects.HasFlag(CardEffect.Discard));
        Assert.True(effects.HasFlag(CardEffect.CardAdvantage));
        Assert.True(effects.HasFlag(CardEffect.Removal));
        Assert.False(effects.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Three one-offs, each recovered 2026-09-20 rather than left as a known
    // miss. Three Wishes puts 108 characters between exiling three cards and
    // letting you play them, where the rule allowed 80. Memories Returning
    // reaches into your hand three times, one card at a time. And Mnemonic
    // Sliver's draw sacrifices "this permanent" inside QUOTES, handed to every
    // Sliver — the body that pays is a different one each time, so the card
    // spends none of itself and the one-shot veto does not apply.
    [InlineData("Three Wishes", "Instant",
        "Exile the top three cards of your library face down. You may look at those cards for as long "
        + "as they remain exiled. Until your next turn, you may play those cards. At the beginning of "
        + "your next upkeep, put any of those cards you didn't play into your graveyard.")]
    [InlineData("Memories Returning", "Sorcery",
        "Reveal the top five cards of your library. Put one of them into your hand. Then choose an "
        + "opponent. They put one on the bottom of your library. Then you put one into your hand. Then "
        + "they put one on the bottom of your library. Put the other into your hand.\nFlashback {7}{U}{U}")]
    [InlineData("Mnemonic Sliver", "Creature — Sliver",
        "All Slivers have \"{2}, Sacrifice this permanent: Draw a card.\"")]
    // Ruled 2026-09-20 after it was put to the human as the one card standing
    // against the self-sacrifice rule: the sacrifice is refunded by the trigger
    // above it, which hands back a token copy of the artifact just spent, so the
    // ability is on the table again and the card was never really paid.
    [InlineData("Esoteric Duplicator", "Artifact",
        "Whenever you sacrifice this artifact or another artifact, you may pay {2}. If you do, at "
        + "the beginning of the next end step, create a token that's a copy of that artifact.\n"
        + "{2}, Sacrifice this artifact: Draw a card.")]
    public void Classify_TheOneOffsThatStillGainACard_AreCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-09-21. A REPLACEMENT draw adds nothing — it spends the draw you
    // were going to have anyway (0 of 3 reviewed cards tagged). A repeatable
    // draw whose trigger names something NARROW is not one you can count on: a
    // coin flip, or becoming the target of an Aura spell or an activated
    // ability, or paying an opponent a permanent for it. The line this does not
    // cross is Surrak, whose trigger is any spell an opponent aims at your
    // creatures — that happens by itself, and Surrak keeps the tag.
    [InlineData("Aladdin's Lamp", "Artifact",
        "{X}, {T}: The next time you would draw a card this turn, instead look at the top X cards of "
        + "your library, put all but one of them on the bottom of your library in a random order, then "
        + "draw a card. X can't be 0.")]
    [InlineData("Goblin Artisans", "Creature — Goblin Artificer",
        "{T}: Flip a coin. If you win the flip, draw a card. If you lose the flip, counter target "
        + "artifact spell you control.")]
    [InlineData("Fugitive Druid", "Creature — Human Druid",
        "Whenever this creature becomes the target of an Aura spell, you draw a card.")]
    [InlineData("Stiltzkin, Moogle Merchant", "Legendary Creature — Moogle",
        "Lifelink\n{2}, {T}: Target opponent gains control of another target permanent you control. "
        + "If they do, you draw a card.")]
    public void Classify_ADrawYouCannotCountOn_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // And the four wordings the tag could not read, all ruled the same day. An
    // ADDITIONAL card is a card (Howling Mine, Sylvan Library). A trigger can
    // have one sentence in front of it (Rowen). "That many cards FROM THE TOP"
    // is the same look with the count carried in from the trigger (Symbiote
    // Spider-Man). And a card that gives ITSELF a second cast is not one card,
    // so the loot that nets zero on the first cast comes out ahead over both —
    // Welcome the Dead is -3 +4 with its flashback, which is how the human read
    // it.
    [InlineData("Howling Mine", "Artifact",
        "At the beginning of each player's draw step, if this artifact is untapped, that player draws "
        + "an additional card.")]
    [InlineData("Rowen", "Enchantment",
        "Reveal the first card you draw each turn. Whenever you reveal a basic land card this way, "
        + "draw a card.")]
    [InlineData("Symbiote Spider-Man", "Legendary Creature — Symbiote Human Hero",
        "Whenever this creature deals combat damage to a player, look at that many cards from the top "
        + "of your library. Put one of them into your hand and the rest into your graveyard.")]
    [InlineData("Welcome the Dead", "Sorcery",
        "Draw two cards, then discard a card and you lose 2 life.\nFlashback {4}{B}")]
    public void Classify_TheWordingsTheTagCouldNotRead_AreCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The carve-out that keeps the rule above honest: "TARGET player" and
    // "EACH player" are NOT somebody else's draw, because you point these at
    // yourself. 12 of the 21 cards written that way are tagged.
    [InlineData("Ancestral Recall", "Instant", "Target player draws three cards.")]
    [InlineData("Braingeyser", "Sorcery", "Target player draws X cards.")]
    public void Classify_TargetPlayerDraws_IsStillCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The 2026-09-16 second-hand ruling, widened 2026-09-19 because its window
    // was too short for the very cards it named: Glarb puts 44 characters
    // between "play lands" and "from the top of your library" where 40 were
    // allowed, and Assemble the Players never says "play" at all.
    [InlineData("Glarb, Calamity's Augur", "Legendary Creature — Frog Wizard Noble",
        "Deathtouch\nYou may look at the top card of your library any time.\n"
        + "You may play lands and cast spells with mana value 4 or greater from the top of your library.\n"
        + "{T}: Surveil 2.")]
    [InlineData("Assemble the Players", "Enchantment",
        "You may look at the top card of your library any time.\n"
        + "Once each turn, you may cast a creature spell with power 2 or less from the top of your library.")]
    // Several Clues in one breath, the reading the Treasure got on 2026-09-18:
    // one has to be cashed for {2} and is a rider, X of them is a draw X.
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Mole Beast",
        "When Nyla enters, exile up to one target creature card from your graveyard. If you do, you lose X life "
        + "and create X Clue tokens, where X is that card's mana value. (A Clue token is an artifact with "
        + "\"{2}, Sacrifice this token: Draw a card.\")\n"
        + "At the beginning of your end step, if you control no Clues, return target card exiled with Nyla to "
        + "its owner's hand.")]
    [InlineData("Tamiyo Meets the Story Circle", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Until your next turn, whenever a creature attacks you or a planeswalker you control, it gets "
        + "-2/-0 until end of turn.\n"
        + "II — Discard any number of cards, then investigate twice for each card discarded this way.\n"
        + "III — Shuffle up to three target cards from your graveyard into your library.")]
    // "Draw that many cards" is a draw-for-each written the other way round,
    // and the count is never one. Restricted to YOUR draw, because "each player
    // draws that many" is a wheel and pays everybody.
    [InlineData("Niv-Mizzet, Visionary", "Legendary Creature — Dragon Wizard",
        "Flying\nYou have no maximum hand size.\n"
        + "Whenever a source you control deals noncombat damage to an opponent, you draw that many cards.")]
    public void Classify_MoreWaysToGetACard_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The top of your library is a second hand, ruled 2026-09-16: these never
    // run out of cards to play even though they never draw one.
    [InlineData("Fblthp, Lost on the Range", "Legendary Creature — Homunculus",
        "Ward {2}\nYou may look at the top card of your library any time.\nYou may play the top card of your library.")]
    [InlineData("Traveling Chocobo", "Creature — Bird",
        "You may look at the top card of your library any time.\nYou may play lands and cast Bird spells from the top of your library.")]
    // Looking at N and taking MORE THAN ONE is a draw with selection; taking
    // exactly one is Filter, and Stock Up is both.
    [InlineData("Stock Up", "Sorcery",
        "Look at the top five cards of your library. Put two of them into your hand and the rest on the bottom of your library in any order.")]
    // Casting out of a graveyard, when you can go back to it every turn.
    [InlineData("Festival of Embers", "Enchantment",
        "During your turn, you may cast instant and sorcery spells from your graveyard by paying 1 life in addition to their other costs.")]
    [InlineData("Edgar, Master Machinist", "Legendary Creature — Human Artificer",
        "Once during each of your turns, you may cast an artifact spell from your graveyard. If you cast a spell this way, that artifact enters tapped.")]
    public void Classify_ASecondHand_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The edges of those three. One card out of the graveyard is one card, and
    // warp and flashback reminder text says "cast THIS CARD" — from your hand at
    // that, which is why the rule refuses the pronoun.
    //
    // Browse used to stand here, on the reading that taking ONE card off the top
    // is selection and not a card gained. The 2026-09-20 ruling overturned that
    // for the repeatable case and Browse is the card it was ruled on, so it has
    // moved to Classify_ACardYouCanGoBackForEveryTurn_IsCardAdvantage.
    [InlineData("Bygone Colossus", "Creature — Giant",
        "Warp {3} (You may cast this card from your hand for its warp cost. Exile it as it resolves.)")]
    public void Classify_OneCardFromElsewhere_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Four more ways the card is handed straight back, none of which says
    // "discard" in the shape the loot pattern reads.
    [InlineData("Dream Cache", "Sorcery",
        "Draw three cards, then put two cards from your hand both on top of your library or both on the bottom.")]
    [InlineData("Lat-Nam's Legacy", "Instant",
        "Shuffle a card from your hand into your library. If you do, draw two cards at the beginning of the next turn's upkeep.")]
    [InlineData("Jandor's Ring", "Artifact",
        "{2}, {T}, Discard the last card you drew this turn: Draw a card.")]
    [InlineData("Green Goblin, Revenant", "Legendary Creature — Goblin",
        "Flying, deathtouch\nWhenever Green Goblin attacks, discard a card. Then draw a card.")]
    public void Classify_ADrawPaidForWithACard_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A repeating draw hiding behind a label. The anchored rule wants "whenever"
    // at the start of the line; modern cards put an ability word or a Siege
    // bullet in front of it.
    [InlineData("Entity Tracker", "Creature — Spirit",
        "Flash\nEerie — Whenever an enchantment you control enters and whenever you fully unlock a Room, draw a card.")]
    [InlineData("G'raha Tia", "Legendary Creature — Cat Scholar",
        "Reach\nThe Allagan Eye — Whenever one or more other creatures and/or artifacts you control enter, draw a card.")]
    [InlineData("Frostcliff Siege", "Enchantment",
        "As this enchantment enters, choose Jeskai or Temur.\n• Jeskai — Whenever a creature you control attacks alone, draw a card.")]
    public void Classify_ATriggerBehindALabel_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // "Draw a card for each X" is multi-card draw written the other way round,
    // and the count-first wording was missed entirely.
    [InlineData("Balance of Power", "Sorcery", "If target opponent has more cards in hand than you, draw cards equal to the difference.")]
    [InlineData("Become the Avalanche", "Sorcery", "Draw a card for each creature you control with power 4 or greater.")]
    public void Classify_CountFirstDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

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
    // A creature body does not have to arrive as a token: the deck cares about
    // the creatures, not the wording that made them. Ruled 2026-09-15 and
    // REAFFIRMED 2026-09-19, when the hand tags were found split 21 to 14 on
    // the identical earthbend wording. None of the 21 tagged ones makes a real
    // token, so the split was an inconsistency rather than a distinction.
    [InlineData("Nature's Revolt", "Enchantment", "All lands are 2/2 creatures that are still lands.")]
    // This case was Badgermole Cub until 2026-09-24, when a CREATURE that
    // earthbends once stopped being Tokens (see the ruling test below); the
    // keyword still counts, and a sorcery is where it is pinned now.
    [InlineData("Earthbending Lesson", "Sorcery — Lesson",
        "Earthbend 4. (Target land you control becomes a 0/0 creature with haste that's still a land. Put four "
        + "+1/+1 counters on it. When it dies or is exiled, return it to the battlefield tapped.)")]
    // The animated land written without the keyword. The pattern reads the
    // "2/2" and so cannot be spelled with [\w ], which is the bug that made
    // the first realignment pass miss all five of these.
    [InlineData("Quirion Druid", "Creature — Elf Druid",
        "{G}, {T}: Target land becomes a 2/2 green creature that's still a land. (This effect lasts indefinitely.)")]
    // Four keywords that put a body on the board and never say "token" in a
    // shape the rules can read. Ruled 2026-09-19; 29 reviewed cards carry one
    // and 24 were already tagged by hand.
    [InlineData("Cryptic Coat", "Artifact — Equipment",
        "When this Equipment enters, cloak the top card of your library, then attach this Equipment to it. "
        + "(To cloak a card, put it onto the battlefield face down as a 2/2 creature with ward {2}. Turn it "
        + "face up any time for its mana cost if it's a creature card.)\n"
        + "Equipped creature gets +1/+0 and can't be blocked.\n{1}{U}: Return this Equipment to its owner's hand.")]
    [InlineData("Curator Beastie", "Creature — Beast",
        "Reach\nColorless creatures you control enter with two additional +1/+1 counters on them.\n"
        + "Whenever this creature enters or attacks, manifest dread. (Look at the top two cards of your library. "
        + "Put one onto the battlefield face down as a 2/2 creature and the other into your graveyard. Turn it "
        + "face up any time for its mana cost if it's a creature card.)")]
    // A token with a PROPER NAME, whose creature type lives only in the
    // reminder text: this never says "creature token" anywhere.
    [InlineData("Ral and the Implicit Maze", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — This Saga deals 2 damage to each creature and planeswalker your opponents control.\n"
        + "II — You may discard a card. If you do, exile the top two cards of your library. You may play them "
        + "until the end of your next turn.\n"
        + "III — Create a Spellgorger Weird token. (It's a {2}{R} 2/2 Weird creature with \"Whenever you cast a "
        + "noncreature spell, put a +1/+1 counter on Spellgorger Weird.\")")]
    // And the same sentence with the verb at the end.
    [InlineData("Stridehangar Automaton", "Artifact Creature — Construct",
        "Thopters you control get +1/+1.\n"
        + "If one or more artifact tokens would be created under your control, those tokens plus an additional "
        + "1/1 colorless Thopter artifact creature token with flying are created instead.")]
    public void Classify_MakingCreaturesWithoutTokens_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Living weapon and job select say "creature token" in their reminder text,
    // so they already read — pinned because the RULING is about the keyword and
    // a card printed without reminder text must still count.
    [InlineData("Mandibular Kite", "Artifact — Equipment",
        "Living weapon (When this Equipment enters, create a 0/0 black Phyrexian Germ creature token, then "
        + "attach this to it.)\nEquipped creature gets +1/+1 and has flying.\nEquip {3}{W}")]
    [InlineData("Thief's Knife", "Artifact — Equipment", "Job select\nEquip {4}")]
    public void Classify_EquipmentThatBringsItsOwnBody_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A TOKEN COPY is a body only when what it copies is a creature, ruled
    // 2026-09-19. Asked of the LINE that makes the copy, because the type word
    // that answers it sits in the trigger beside the verb. The case was Ran and
    // Shaw until 2026-09-24, when a creature copying itself ONCE stopped being
    // Tokens; it moved to the ruling test below, and a copy made on every
    // attack (exert) is pinned here instead.
    [InlineData("Sandstorm Crasher", "Creature — Minotaur Berserker Wizard",
        "Trample\nYou may exert this creature as it attacks. When you do, create a tapped and attacking token "
        + "that's a copy of target creature you control. Sacrifice the token at the beginning of the next end "
        + "step. (An exerted creature won't untap during your next untap step.)")]
    public void Classify_ATokenCopyOfACreature_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // …and the other side: Esoteric Duplicator copies an artifact and Firion an
    // Equipment, and neither puts anything on the board to attack with.
    [InlineData("Esoteric Duplicator", "Artifact — Clue",
        "Whenever you sacrifice this artifact or another artifact, you may pay {2}. If you do, at the beginning "
        + "of the next end step, create a token that's a copy of that artifact.\n"
        + "{2}, Sacrifice this artifact: Draw a card.")]
    [InlineData("Firion, Wild Rose Warrior", "Legendary Creature — Human Rebel Warrior",
        "Equipped creatures you control have haste.\n"
        + "Whenever a nontoken Equipment you control enters, create a token that's a copy of it, except it has "
        + "\"This Equipment's equip abilities cost {2} less to activate.\" Sacrifice that token at the beginning "
        + "of the next upkeep.")]
    public void Classify_ATokenCopyOfSomethingElse_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A CREATURE that makes one or two bodies, ONCE, is not Tokens. Ruled
    // 2026-09-24: a creature is already a creature, and the tag marks the
    // noncreature cards the cube files among them. It settled a split — the
    // Ally sentence on Katara was tagged, the same sentence on Kyoshi Warriors
    // was not.
    [InlineData("Katara, Water Tribe's Hope", "Legendary Creature — Human Warrior Ally",
        "Vigilance\nWhen Katara enters, create a 1/1 white Ally creature token.\n"
        + "Waterbend {X}: Creatures you control have base power and toughness X/X until end of turn. X can't be "
        + "0. Activate only during your turn. (While paying a waterbend cost, you can tap your artifacts and "
        + "creatures to help. Each one pays for {1}.)")]
    [InlineData("Head of the Homestead", "Creature — Rabbit Citizen",
        "When this creature enters, create two 1/1 white Rabbit creature tokens.")]
    // Offspring keeps its body in the reminder text, which is read.
    [InlineData("Manifold Mouse", "Creature — Mouse Soldier",
        "Offspring {2} (You may pay an additional {2} as you cast this spell. If you do, when this creature "
        + "enters, create a 1/1 token copy of it.)\nAt the beginning of combat on your turn, target Mouse you "
        + "control gains your choice of double strike or trample until end of turn.")]
    // A copy of ITSELF, once — overturned from Tokens by this ruling.
    [InlineData("Ran and Shaw", "Legendary Creature — Dragon",
        "Flying, firebending 2\n"
        + "When Ran and Shaw enter, if you cast them and there are three or more Dragon and/or Lesson cards in "
        + "your graveyard, create a token that's a copy of Ran and Shaw, except it's not legendary.\n"
        + "{3}{R}: Dragons you control get +2/+0 until end of turn.")]
    // A DELAYED trigger fires once, however much it reads like an upkeep.
    [InlineData("Rukh Egg", "Creature — Bird Egg",
        "When this creature dies, create a 4/4 red Bird creature token with flying at the beginning of the next "
        + "end step.")]
    // Two earthbends are two bodies: the keyword is counted OUTSIDE its own
    // reminder text, which names it a third time.
    [InlineData("Dai Li Agents", "Creature — Human Soldier",
        "When this creature enters, earthbend 1, then earthbend 1. (To earthbend 1, target land you control "
        + "becomes a 0/0 creature with haste that's still a land. Put a +1/+1 counter on it. When it dies or is "
        + "exiled, return it to the battlefield tapped.)\nWhenever this creature attacks, each opponent loses X "
        + "life and you gain X life, where X is the number of creatures you control with +1/+1 counters on them.")]
    // An ability that pays with the card itself runs once.
    [InlineData("Leering Onlooker", "Creature — Vampire",
        "Flying\n{2}{B}{B}, Exile this card from your graveyard: Create two tapped 1/1 black Bat creature tokens "
        + "with flying.")]
    public void Classify_ACreatureMakingOneOrTwoBodiesOnce_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // …and the creatures that keep it: THREE at once, counted across a list.
    [InlineData("Somberwald Beastmaster", "Creature — Human Ranger",
        "When this creature enters, create a 2/2 green Wolf creature token, a 3/3 green Beast creature token, "
        + "and a 4/4 green Beast creature token.\nCreature tokens you control have deathtouch. (Any amount of "
        + "damage they deal to a creature is enough to destroy it.)")]
    // A count that comes before the verb.
    [InlineData("Tobias, Doomed Conqueror", "Legendary Creature — Human Soldier",
        "Flash\nWhen Tobias dies, for each nontoken creature you controlled that died this turn, create a 2/2 "
        + "black Zombie creature token.")]
    // A trigger behind an ability word, which repeats.
    [InlineData("Barret, Avalanche Leader", "Legendary Creature — Human Rebel",
        "Reach\nAvalanche! — Whenever an Equipment you control enters, create a 2/2 red Rebel creature token.\n"
        + "At the beginning of combat on your turn, attach up to one target Equipment you control to target "
        + "Rebel you control.")]
    // Myriad printed BARE, with no reminder text to read a trigger from.
    [InlineData("Chittering Dispatcher", "Creature — Eldrazi Drone",
        "Devoid (This card has no color.)\nMyriad\nWhen this creature leaves the battlefield, create a 0/1 "
        + "colorless Eldrazi Spawn creature token with \"Sacrifice this token: Add {C}.\"")]
    // A replacement that answers every death.
    [InlineData("Valentin, Dean of the Vein // Lisette, Dean of the Root",
        "Legendary Creature — Vampire Warlock // Legendary Creature — Human Druid",
        "Menace, lifelink\nIf a nontoken creature an opponent controls would die, exile it instead. When you do, "
        + "you may pay {2}. If you do, create a 1/1 black and green Pest creature token with \"When this token "
        + "dies, you gain 1 life.\"\nWhenever you gain life, you may pay {1}. If you do, put a +1/+1 counter on "
        + "each creature you control and those creatures gain trample until end of turn.")]
    public void Classify_ACreatureMakingManyOrRepeatedBodies_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A NONCREATURE card is Tokens for a single body — the other half of the
    // 2026-09-24 ruling. The FRONT face decides: this Sidequest transforms into
    // a creature, and the card you cast is an enchantment.
    [InlineData("Sarcomancy", "Enchantment",
        "When this enchantment enters, create a 2/2 black Zombie creature token.\nAt the beginning of your "
        + "upkeep, if there are no Zombies on the battlefield, this enchantment deals 1 damage to you.")]
    [InlineData("Sidequest: Raise a Chocobo // Black Chocobo", "Enchantment // Creature — Bird",
        "When this enchantment enters, create a 2/2 green Bird creature token with \"Whenever a land you "
        + "control enters, this token gets +1/+0 until end of turn.\"\nAt the beginning of your first main "
        + "phase, if you control four or more Birds, transform this enchantment.\nWhen this permanent "
        + "transforms into Black Chocobo, search your library for a land card, put it onto the battlefield "
        + "tapped, then shuffle.\nLandfall — Whenever a land you control enters, Birds you control get +1/+0 "
        + "until end of turn.")]
    public void Classify_ANoncreatureMakingOneBody_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A SMALL pump that is not what the card is for, ruled 2026-09-25.
    // B1 — one-shot at sorcery speed: "1 segnalino o un +1/+1 a sorcery non fa
    // niente". This was Buff under the 2026-09-15 counter ruling.
    [InlineData("Cloudbound Moogle", "Creature — Moogle",
        "Flying\nWhen this creature enters, put a +1/+1 counter on target creature.")]
    [InlineData("Keen-Eyed Raven", "Creature — Bird",
        "Flying (This creature can't be blocked except by creatures with flying or reach.)\nWhen this creature "
        + "enters, put a +1/+1 counter on another target creature you control.")]
    // Honor stood here until the same day's re-review overturned it: a spell
    // that is ONLY the pump is Buff. See Classify_APumpRuledIn_IsBuff.
    // B1 now covers a sorcery that does something else as well.
    [InlineData("Aggressive Negotiations", "Sorcery",
        "Target opponent reveals their hand. You choose a nonland card from it and exile that card. Put a "
        + "+1/+1 counter on up to one target creature you control.")]
    // …and from the graveyard, which runs once because the card exiles itself.
    [InlineData("Wither and Bloom", "Instant",
        "Target creature gets -3/-3 until end of turn.\n{1}{B}, Exile this card from your graveyard: Put a +1/+1 "
        + "counter on target creature you control. Activate only as a sorcery.")]
    // B2 — at instant speed, beside something the card is really for:
    // Protection with a rider.
    [InlineData("Magic Damper", "Instant",
        "Target creature you control gets +1/+1 and gains hexproof until end of turn. Untap it.")]
    [InlineData("Lightfoot Technique", "Instant",
        "Put a +1/+1 counter on target creature. It gains flying and indestructible until end of turn. (Damage "
        + "and effects that say \"destroy\" don't destroy it.)")]
    // B3 — repeated, but a small extra on a CREATURE judged as a whole.
    [InlineData("Expanding Ooze", "Creature — Ooze",
        "{B}{G}: Adapt 1. (If this creature has no +1/+1 counters on it, put a +1/+1 counter on it.)\nWhenever "
        + "this creature attacks, put a +1/+1 counter on target modified creature you control. (Equipment, Auras "
        + "you control, and counters are modifications.)")]
    // A BITE is Removal alone, whatever the size: the pump aims it.
    [InlineData("Felling Blow", "Sorcery",
        "Put a +1/+1 counter on target creature you control. Then that creature deals damage equal to its power "
        + "to target creature an opponent controls.")]
    [InlineData("Bite Down on Crime", "Sorcery",
        "As an additional cost to cast this spell, you may collect evidence 6. This spell costs {2} less to cast "
        + "if evidence was collected. (To collect evidence 6, exile cards with total mana value 6 or greater from "
        + "your graveyard.)\nTarget creature you control gets +2/+0 until end of turn. It deals damage equal to its "
        + "power to target creature you don't control.")]
    public void Classify_ASmallPumpBesideThePoint_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump on ITSELF, refused since 2026-09-18, in the three wordings the
    // earlier gate could not see — found in the 2026-09-25 pass.
    // "It gets", when the card is what the sentence is about.
    [InlineData("Slimy Piper", "Creature — Fungus Bard",
        "Whenever this creature attacks, it gets +1/+1 until end of turn. If you control four or more creatures, "
        + "it gets +2/+2 and gains indestructible until end of turn instead. (Damage and effects that say "
        + "\"destroy\" don't destroy it.)")]
    // Counters doubled on "this creature".
    [InlineData("Mossborn Hydra", "Creature — Elemental Hydra",
        "Trample (This creature can deal excess combat damage to the player or planeswalker it's attacking.)\n"
        + "This creature enters with a +1/+1 counter on it.\nLandfall — Whenever a land you control enters, double "
        + "the number of +1/+1 counters on this creature.")]
    // The card's own first name.
    [InlineData("Rashka the Slayer", "Legendary Creature — Human Archer",
        "Reach (This creature can block creatures with flying.)\nWhenever Rashka blocks one or more black "
        + "creatures, Rashka gets +1/+2 until end of turn.")]
    // "Equipped" is the CONDITION, and the card is still the one that gets it.
    [InlineData("Leonin Den-Guard", "Creature — Cat Soldier",
        "As long as this creature is equipped, it gets +1/+1 and has vigilance.")]
    // A condition that names creatures you control is not who gets the pump.
    [InlineData("Hundred-Battle Veteran", "Creature — Zombie Warrior",
        "As long as there are three or more different kinds of counters among creatures you control, this "
        + "creature gets +2/+4.\nYou may cast this card from your graveyard. If you do, it enters with a finality "
        + "counter on it. (If a creature with a finality counter on it would die, exile it instead.)")]
    public void Classify_APumpOnItselfInAnotherWording_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // …and the same pronoun about SOMEBODY ELSE keeps the tag.
    [InlineData("Team Avatar", "Enchantment",
        "Whenever a creature you control attacks alone, it gets +X/+X until end of turn, where X is the number of "
        + "creatures you control.\n{2}{W}, Discard this card: It deals damage equal to the number of creatures you "
        + "control to target creature.")]
    [InlineData("Bestial Fury", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, draw a card at the beginning of the next turn's upkeep.\nWhenever "
        + "enchanted creature becomes blocked, it gets +4/+0 and gains trample until end of turn.")]
    // "It" is the TARGET, even though the card names itself in the sentence.
    [InlineData("Growth Cycle", "Instant",
        "Target creature gets +3/+3 until end of turn. It gets an additional +2/+2 until end of turn for each card "
        + "named Growth Cycle in your graveyard.")]
    // One counter on entry is B1, but DOUBLING them on every attack is a
    // repeated, big pump, and keeps it.
    [InlineData("Seismic Tutelage", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, put a +1/+1 counter on enchanted creature.\nWhenever enchanted "
        + "creature attacks, double the number of +1/+1 counters on it.")]
    public void Classify_ItGetsAboutSomebodyElse_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // …and the pumps that keep it. B2's other half: the pump IS the card.
    [InlineData("Gift of the Viper", "Instant",
        "Put a +1/+1 counter, a reach counter, and a deathtouch counter on target creature. Untap it.")]
    [InlineData("Guided Strike", "Instant",
        "Target creature gets +1/+0 and gains first strike until end of turn.\nDraw a card.")]
    // A scry beside it is a rider, not a second purpose.
    [InlineData("Storm Strike", "Instant",
        "Target creature gets +1/+0 and gains first strike until end of turn. Scry 1.")]
    // Repeated on a NONCREATURE: the pump is the card.
    [InlineData("Firebreathing", "Enchantment — Aura", "Enchant creature\n{R}: Enchanted creature gets +1/+0 until end of turn.")]
    // A modal bullet with its own trigger repeats, whatever its head says.
    [InlineData("Hollowmurk Siege", "Enchantment",
        "As this enchantment enters, choose Sultai or Abzan.\n• Sultai — Whenever a counter is put on a creature "
        + "you control, draw a card. This ability triggers only once each turn.\n• Abzan — Whenever you attack, "
        + "put a +1/+1 counter on target attacking creature. It gains menace until end of turn.")]
    // Bigger than +1/+1, once, on a creature entering.
    [InlineData("Friendly Ghost", "Creature — Spirit",
        "Flying\nWhen this creature enters, target creature gets +2/+4 until end of turn.")]
    // Repeated AND big on a creature.
    [InlineData("Ashroot Animist", "Creature — Lizard Druid",
        "Trample\nWhenever this creature attacks, another target creature you control gains trample and gets "
        + "+X/+X until end of turn, where X is this creature's power.")]
    // A double strike grant is not judged by size at all (ruled 2026-09-18).
    [InlineData("Origin of Spider-Man", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Create a 2/1 "
        + "green Spider creature token with reach.\nII — Put a +1/+1 counter on target creature you control. It "
        + "becomes a legendary Spider Hero in addition to its other types.\nIII — Target creature you control gains "
        + "double strike until end of turn.")]
    public void Classify_APumpThatIsThePoint_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Three cards the human ruled by name on 2026-09-25, each for a reason the
    // size rule does not cover. A STATE in front of a tribe is still a tribe.
    [InlineData("Gornog, the Red Reaper", "Legendary Creature — Minotaur Warrior",
        "Haste\nCowards can't block Warriors.\nWhenever one or more Warriors you control attack a player, target "
        + "creature that player controls becomes a Coward.\nAttacking Warriors you control get +X/+0, where X is the "
        + "number of Cowards your opponents control.")]
    // Counters that ARE the body: on the creatures the sentence just made…
    [InlineData("Valgavoth's Onslaught", "Sorcery",
        "Manifest dread X times, then put X +1/+1 counters on each of those creatures. (To manifest dread, look at "
        + "the top two cards of your library, then put one onto the battlefield face down as a 2/2 creature and the "
        + "other into your graveyard. Turn it face up any time for its mana cost if it's a creature card.)")]
    // …and on a noncreature that becomes a 0/0.
    [InlineData("Case of the Filched Falcon", "Enchantment — Case",
        "When this Case enters, investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this "
        + "token: Draw a card.\")\nTo solve — You control three or more artifacts. (If unsolved, solve at the "
        + "beginning of your end step.)\nSolved — {2}{U}, Sacrifice this Case: Put four +1/+1 counters on target "
        + "noncreature artifact. It becomes a 0/0 Bird creature with flying in addition to its other types.")]
    // …and on a land the next sentence turns into a creature (2026-09-25).
    [InlineData("Rootwise Survivor", "Creature — Human Survivor",
        "Haste\nSurvival — At the beginning of your second main phase, if this creature is tapped, put three "
        + "+1/+1 counters on up to one target land you control. That land becomes a 0/0 Elemental creature in "
        + "addition to its other types. It gains haste until your next turn.")]
    // A tribe in the other vocabularies, ruled on the 2024 re-review of
    // 2026-09-25: counters on each of a tribe…
    [InlineData("Camellia, the Seedmiser", "Legendary Creature — Squirrel Warlock",
        "Menace\nOther Squirrels you control have menace.\nWhenever you sacrifice one or more Foods, create a "
        + "1/1 green Squirrel creature token.\n{2}, Forage: Put a +1/+1 counter on each other Squirrel you "
        + "control. (To forage, exile three cards from your graveyard or sacrifice a Food.)")]
    [InlineData("Kastral, the Windcrested", "Legendary Creature — Bird Scout",
        "Flying\nWhenever one or more Birds you control deal combat damage to a player, choose one —\n• You may "
        + "put a Bird creature card from your hand or graveyard onto the battlefield with a finality counter on "
        + "it.\n• Put a +1/+1 counter on each Bird you control.\n• Draw a card.")]
    // …a counter the tribe enters with…
    [InlineData("Slinza, the Spiked Stampede", "Legendary Creature — Beast",
        "Beast spells you cast cost {2} less to cast.\nEach other Beast creature you control enters with an "
        + "additional +1/+1 counter on it.\nWhenever Slinza or another creature with power 4 or greater enters, "
        + "you may pay {1}{R/G}. When you do, Slinza fights target creature you don't control.")]
    // …a target that has to belong to the tribe, one type or a list…
    [InlineData("Inside Source", "Creature — Human Citizen",
        "When this creature enters, create a 2/2 white and blue Detective creature token.\n{3}, {T}: Target "
        + "Detective you control gets +2/+0 and gains vigilance until end of turn. Activate only as a sorcery.")]
    [InlineData("Rockface Village", "Land",
        "{T}: Add {C}.\n{T}: Add {R}. Spend this mana only to cast a creature spell.\n{R}, {T}: Target Lizard, "
        + "Mouse, Otter, or Raccoon you control gets +1/+0 and gains haste until end of turn. Activate only as "
        + "a sorcery.")]
    // …and TOKENS, a tribe by another name, in both vocabularies.
    [InlineData("Hildibrand Manderville // Gentleman's Rise", "Legendary Creature — Human Detective // Instant — Adventure",
        "Creature tokens you control get +1/+1.\nWhen Hildibrand Manderville dies, you may cast it from your "
        + "graveyard as an Adventure until the end of your next turn.\nCreate a 2/2 black Zombie creature token. "
        + "(Then exile this card. You may cast the creature later from exile.)")]
    [InlineData("Sandstorm Salvager", "Creature — Human Artificer",
        "When this creature enters, create a 3/3 colorless Golem artifact creature token.\n{2}, {T}: Put a "
        + "+1/+1 counter on each creature token you control. They gain trample until end of turn.")]
    public void Classify_ATribeOrABodyBeingMade_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The two bodies above are Tokens instead.
    [InlineData("Valgavoth's Onslaught", "Sorcery",
        "Manifest dread X times, then put X +1/+1 counters on each of those creatures. (To manifest dread, look at "
        + "the top two cards of your library, then put one onto the battlefield face down as a 2/2 creature and the "
        + "other into your graveyard. Turn it face up any time for its mana cost if it's a creature card.)")]
    [InlineData("Case of the Filched Falcon", "Enchantment — Case",
        "When this Case enters, investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this "
        + "token: Draw a card.\")\nTo solve — You control three or more artifacts. (If unsolved, solve at the "
        + "beginning of your end step.)\nSolved — {2}{U}, Sacrifice this Case: Put four +1/+1 counters on target "
        + "noncreature artifact. It becomes a 0/0 Bird creature with flying in addition to its other types.")]
    [InlineData("Rootwise Survivor", "Creature — Human Survivor",
        "Haste\nSurvival — At the beginning of your second main phase, if this creature is tapped, put three "
        + "+1/+1 counters on up to one target land you control. That land becomes a 0/0 Elemental creature in "
        + "addition to its other types. It gains haste until your next turn.")]
    public void Classify_CountersThatMakeABody_AreTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Ruled Buff by name on 2026-09-25: a big pump that repeats…
    [InlineData("Hurska Sweet-Tooth", "Legendary Creature — Bear",
        "Whenever Hurska attacks, create a Food token. (It's an artifact with \"{2}, {T}, Sacrifice this token: "
        + "You gain 3 life.\")\nWhenever you gain life, you may pay {G/W}. When you do, target creature gets +X/+X "
        + "until end of turn, where X is the amount of life you gained.")]
    // …and a big one at instant speed, whatever it costs afterwards.
    [InlineData("Soulshriek", "Instant",
        "Target creature you control gets +X/+0 until end of turn, where X is the number of creature cards in your "
        + "graveyard. Sacrifice that creature at the beginning of the next end step.")]
    // "Those creatures" that were TARGETED, not made, are still pumped.
    [InlineData("Biogenic Upgrade", "Sorcery",
        "Distribute three +1/+1 counters among one, two, or three target creatures, then double the number of +1/+1 "
        + "counters on each of those creatures.")]
    public void Classify_ABigPumpTheHumanNamed_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    // Several targets and a shield: Buff and Protection, ruled 2026-09-25.
    public void Classify_EnduranceBobblehead_IsBuffAndProtection()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Endurance Bobblehead", "Artifact — Bobblehead",
            "{T}: Add one mana of any color.\n{3}, {T}: Up to X target creatures you control get +1/+0 and gain "
            + "indestructible until end of turn, where X is the number of Bobbleheads you control as you activate "
            + "this ability. Activate only as a sorcery."));

        Assert.True(result.HasFlag(CardEffect.Buff));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Fact]
    // +5/+5 AND a shield: both tags, named by the human on 2026-09-25.
    public void Classify_StonewoodInvocation_IsBuffAndProtection()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Stonewood Invocation", "Instant",
            "Split second (As long as this spell is on the stack, players can't cast spells or activate abilities "
            + "that aren't mana abilities.)\nTarget creature gets +5/+5 and gains shroud until end of turn. (It can't "
            + "be the target of spells or abilities.)"));

        Assert.True(result.HasFlag(CardEffect.Buff));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Buff the human tagged on the 2024 re-review of 2026-09-25 and the
    // classifier could not read. Double strike outside the quotes that pump a
    // token:
    [InlineData("Blacksmith's Talent", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nWhen this Class enters, create a colorless "
        + "Equipment artifact token named Sword with \"Equipped creature gets +1/+1\" and equip {2}.\n{2}{R}: "
        + "Level 2\nAt the beginning of combat on your turn, attach target Equipment you control to up to one "
        + "target creature you control.\n{3}{R}: Level 3\nDuring your turn, equipped creatures you control have "
        + "double strike and haste.")]
    // The creature just CAST entering with counters, which is not the card:
    [InlineData("Communal Brewing", "Enchantment",
        "When this enchantment enters, any number of target opponents each draw a card. Put an ingredient "
        + "counter on this enchantment, then put an ingredient counter on it for each card drawn this "
        + "way.\nWhenever you cast a creature spell, that creature enters with X additional +1/+1 counters on "
        + "it, where X is the number of ingredient counters on this enchantment.")]
    // Scavenge handed to a whole graveyard:
    [InlineData("Young Deathclaws", "Creature — Lizard Mutant",
        "Menace (This creature can't be blocked except by two or more creatures.)\nEach creature card in your "
        + "graveyard has scavenge. The scavenge cost is equal to its mana cost. (Exile a creature card from "
        + "your graveyard and pay its mana cost: Put a number of +1/+1 counters equal to that card's power on "
        + "target creature. Scavenge only as a sorcery.)")]
    // A counter "on a creature you control", in a mode bought up to five times:
    [InlineData("Season of Gathering", "Sorcery",
        "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Put a +1/+1 "
        + "counter on a creature you control. It gains vigilance and trample until end of turn.\n{P}{P} — Choose "
        + "artifact or enchantment. Destroy all permanents of the chosen type.\n{P}{P}{P} — Draw cards equal to "
        + "the greatest power among creatures you control.")]
    // A creature that grows another each time it connects — the B3 exception:
    [InlineData("Prowler, Misguided Mentor", "Legendary Creature — Human Rogue Villain",
        "Prowler can't be blocked by creatures with power 2 or less.\nWhenever Prowler deals combat damage to "
        + "a player, put a +1/+1 counter on another target creature you control.")]
    [InlineData("Scurry of Squirrels", "Creature — Squirrel Scout",
        "Myriad, myriad (Whenever this creature attacks, for each opponent other than defending player, you "
        + "may create a token that's a copy of this creature that's tapped and attacking that player or a "
        + "planeswalker they control. Then do it again. Exile the tokens at end of combat.)\nWhenever this "
        + "creature deals combat damage to a player, put a +1/+1 counter on target creature you control.")]
    // And two sizes the reading got wrong: a count behind "until end of turn",
    // and counters spread over a third target.
    [InlineData("Hunger of the Nim", "Sorcery",
        "Target creature gets +1/+0 until end of turn for each artifact you control.")]
    [InlineData("Incremental Growth", "Sorcery",
        "Put a +1/+1 counter on target creature, two +1/+1 counters on another target creature, and three "
        + "+1/+1 counters on a third target creature.")]
    public void Classify_APumpTheReadingMissed_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // B3 still holds for every other repeated small counter a creature hands out.
    [InlineData("Loxodon Battle Priest", "Creature — Elephant Cleric",
        "At the beginning of combat on your turn, put a +1/+1 counter on another target creature you control.")]
    // A new base on the creature an Aura stole is the contour of the theft.
    [InlineData("Coerced to Kill", "Enchantment — Aura",
        "Enchant creature\nYou control enchanted creature.\nEnchanted creature has base power and toughness "
        + "1/1, has deathtouch, and is an Assassin in addition to its other types.")]
    // "That creature" is the one reanimated here, not one being cast.
    [InlineData("Necromantic Summons", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control.\nSpell mastery — "
        + "If there are two or more instant and/or sorcery cards in your graveyard, that creature enters with "
        + "two additional +1/+1 counters on it.")]
    public void Classify_APumpThatIsNotThePoint_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    // The owner's choice of top or bottom of the library: 9 of 9 reviewed
    // cards are tagged Bounce (2026-09-25).
    public void Classify_TopOrBottomOfTheLibrary_IsBounce()
    {
        Assert.True(EffectClassifier.Classify(MakeCard("Trip Up", "Instant",
            "Target nonland permanent's owner puts it on their choice of the top or bottom of their "
            + "library.\nCycling {2} ({2}, Discard this card: Draw a card.)")).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Taking a card out of their revealed hand into exile is Discard (2026-09-25),
    // Intimidation Tactics by name the same day.
    [InlineData("Aggressive Negotiations", "Sorcery",
        "Target opponent reveals their hand. You choose a nonland card from it and exile that card. Put a "
        + "+1/+1 counter on up to one target creature you control.")]
    [InlineData("Intimidation Tactics", "Sorcery",
        "Target opponent reveals their hand. You choose an artifact or creature card from it. Exile that "
        + "card.\nCycling {3} ({3}, Discard this card: Draw a card.)")]
    public void Classify_ExilingFromTheirRevealedHand_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Five wordings read 2026-09-28, each tagged on every reviewed card using
    // it: the punisher's "unless that player … discards a card", the chosen card
    // discarded by a pronoun ("they discard it", "the player discards that
    // card"), "they discard a card" after a player is named, a list ("sacrifices
    // a creature, discards a card"), and the name search through a hand. Tinybones
    // pins that a card watching a discard AND causing one keeps the tag.
    [InlineData("Perforating Artist", "Creature — Devil",
        "Deathtouch (Any amount of damage this deals to a creature is enough to destroy it.)\nRaid — At the "
        + "beginning of your end step, if you attacked this turn, each opponent loses 3 life unless that player "
        + "sacrifices a nonland permanent of their choice or discards a card.")]
    [InlineData("Binding Negotiation", "Sorcery",
        "Target opponent reveals their hand. You may choose a nonland card from it. If you do, they discard "
        + "it. Otherwise, you may put a face-up exiled card they own into their graveyard.")]
    [InlineData("Leshrac's Sigil", "Enchantment",
        "Whenever an opponent casts a green spell, you may pay {B}{B}. If you do, look at that player's hand "
        + "and choose a card from it. The player discards that card.\n{B}{B}: Return this enchantment to its "
        + "owner's hand.")]
    [InlineData("Sonic Shrieker", "Creature — Dragon",
        "Flying\nWhen this creature enters, it deals 2 damage to any target and you gain 2 life. If a player "
        + "is dealt damage this way, they discard a card.")]
    [InlineData("Scarring Memories", "Sorcery — Lesson",
        "You may cast this spell as though it had flash if you control an attacking legendary "
        + "creature.\nTarget opponent sacrifices a creature of their choice, discards a card, and loses 3 life.")]
    [InlineData("Lobotomy", "Sorcery",
        "Target player reveals their hand, then you choose a card other than a basic land card from it. "
        + "Search that player's graveyard, hand, and library for all cards with the same name as the chosen "
        + "card and exile them. Then that player shuffles.")]
    [InlineData("Tinybones, Bauble Burglar", "Legendary Creature — Skeleton Rogue",
        "Whenever an opponent discards a card, exile it from their graveyard with a stash counter on "
        + "it.\nDuring your turn, you may play cards you don't own with stash counters on them from exile, and "
        + "mana of any type can be spent to cast those spells.\n{3}{B}, {T}: Each opponent discards a card. "
        + "Activate only as a sorcery.")]
    public void Classify_AnAttackOnAHandInAnyWording_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Four shapes that name a player beside "discard" and attack nobody's hand,
    // each untagged on every reviewed card: the madness-style punishment, a loot
    // handed to a player (Filter), a condition that watches a discard, and an
    // alternative cost.
    [InlineData("Guerrilla Tactics", "Instant",
        "Guerrilla Tactics deals 2 damage to any target.\nWhen a spell or ability an opponent controls causes "
        + "you to discard this card, it deals 4 damage to any target.")]
    [InlineData("Depth Defiler", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nKicker {C} (You may pay an additional {C} as you cast this "
        + "spell.)\nWhen you cast this spell, choose one. If it was kicked, choose both instead.\n• Return target "
        + "creature to its owner's hand.\n• Target player draws two cards, then discards a card.")]
    [InlineData("Cait, Cage Brawler", "Legendary Creature — Human Warrior",
        "During your turn, Cait has indestructible.\nWhenever Cait attacks, you and defending player each draw "
        + "a card, then discard a card. Put two +1/+1 counters on Cait if you discarded the card with the "
        + "greatest mana value among those cards or tied for greatest.")]
    [InlineData("Lo and Li, Royal Advisors", "Legendary Creature — Human Advisor",
        "Whenever an opponent discards a card or mills one or more cards, put a +1/+1 counter on each Advisor "
        + "you control.\n{2}{U/B}: Target player mills four cards. (They put the top four cards of their library "
        + "into their graveyard.)")]
    [InlineData("Dream Halls", "Enchantment",
        "Rather than pay the mana cost for a spell, its controller may discard a card that shares a color "
        + "with that spell.")]
    public void Classify_DiscardWordsThatAttackNoHand_AreNotDiscard(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Ruled 2026-09-28, each settling a split: a card EXILED from their hand
    // (Unscrupulous Agent, the same sentence as Ruthless Negotiation;
    // Crabomination's card at random), a card chosen from their hand and put on
    // their LIBRARY (Painful Memories, as Agonizing Memories), the whole hand
    // looked at and EXILED (Apple of Eden, "come Wheel of Fortune e sorelle"), a
    // symmetric REPLACEMENT of the draw (Chains of Mephistopheles), and Nicol
    // Bolas's "that player discards their hand", whose missing tag was a slip.
    [InlineData("Unscrupulous Agent", "Creature — Elf Detective",
        "When this creature enters, target opponent exiles a card from their hand.")]
    [InlineData("Crabomination", "Creature — Crab Demon",
        "Emerge from artifact {5}{B}{B} (You may cast this spell by sacrificing an artifact and paying the "
        + "emerge cost reduced by that artifact's mana value.)\nWhen this creature enters, target opponent "
        + "exiles the top card of their library, a card at random from their graveyard, and a card at random "
        + "from their hand. You may cast a spell from among cards exiled this way without paying its mana cost.")]
    [InlineData("Painful Memories", "Sorcery",
        "Look at target opponent's hand and choose a card from it. Put that card on top of that player's "
        + "library.")]
    [InlineData("Apple of Eden, Isu Relic", "Legendary Artifact",
        "{T}, Pay 4 life, Sacrifice Apple of Eden: Look at target opponent's hand and exile those cards face "
        + "down. You may play those cards this turn, and mana of any type can be spent to cast them. Until end "
        + "of turn, whenever you play a land or cast a spell this way, its owner draws a card. At the beginning "
        + "of the next end step, return the exiled cards to their owner's hand. Activate only as a sorcery.")]
    [InlineData("Chains of Mephistopheles", "Enchantment",
        "If a player would draw a card except the first one they draw in each of their draw steps, that "
        + "player discards a card instead. If the player discards a card this way, they draw a card. If the "
        + "player doesn't discard a card this way, they mill a card.")]
    [InlineData("Nicol Bolas", "Legendary Creature — Elder Dragon",
        "Flying\nAt the beginning of your upkeep, sacrifice Nicol Bolas unless you pay {U}{B}{R}.\nWhenever "
        + "Nicol Bolas deals damage to an opponent, that player discards their hand.")]
    // Confirmed 2026-09-29: a punisher that offers the OPPONENTS the choice
    // outright, "each opponent may sacrifice … or discard a card".
    [InlineData("Osseous Sticktwister", "Artifact Creature — Scarecrow",
        "Lifelink\nDelirium — At the beginning of your end step, if there are four or more card types among "
        + "cards in your graveyard, each opponent may sacrifice a nonland permanent of their choice or discard "
        + "a card. Then this creature deals damage equal to its power to each opponent who didn't sacrifice a "
        + "permanent or discard a card this way.")]
    // The hand looked at and a card exiled from it in a second sentence: Deep-
    // Cavern Bat, tagged by the human on 2026-09-29.
    [InlineData("Deep-Cavern Bat", "Creature — Bat",
        "Flying, lifelink\nWhen this creature enters, look at target opponent's hand. You may exile a nonland "
        + "card from it until this creature leaves the battlefield.")]
    public void Classify_AHandAttackRuledIn_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Ruled 2026-09-28: "each player MAY discard" is a choice everybody makes,
    // you included, and attacks nobody's hand (Mind Bomb; The Death of Gwen
    // Stacy, whose hand tag was corrected); a card exiled that they may still
    // PLAY is not lost (Lightstall Inquisitor, Elkin Lair); and a discard of
    // YOUR OWN is a cost — the four hand tags that said otherwise were ruled
    // errors (Professor Zei, Sabin, Stormbind, Summon: G.F. Ifrit).
    [InlineData("Mind Bomb", "Sorcery",
        "Each player may discard up to three cards. Mind Bomb deals damage to each player equal to 3 minus "
        + "the number of cards they discarded this way.")]
    [InlineData("The Death of Gwen Stacy", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Destroy "
        + "target creature.\nII — Each player may discard a card. Each player who doesn't loses 3 life.\nIII — "
        + "Exile any number of target players' graveyards.")]
    [InlineData("Lightstall Inquisitor", "Creature — Angel Wizard",
        "Vigilance\nWhen this creature enters, each opponent exiles a card from their hand and may play that "
        + "card for as long as it remains exiled. Each spell cast this way costs {1} more to cast. Each land "
        + "played this way enters tapped.")]
    [InlineData("Elkin Lair", "World Enchantment",
        "At the beginning of each player's upkeep, that player exiles a card at random from their hand. The "
        + "player may play that card this turn. At the beginning of the next end step, if the player hasn't "
        + "played the card, they put it into their graveyard.")]
    [InlineData("Professor Zei, Anthropologist", "Legendary Creature — Human Advisor Ally",
        "{T}, Discard a card: Draw a card.\n{1}, {T}, Sacrifice Professor Zei: Return target instant or "
        + "sorcery card from your graveyard to your hand. Activate only during your turn.")]
    [InlineData("Sabin, Master Monk", "Legendary Creature — Human Noble Monk",
        "Double strike\nBlitz—{2}{R}{R}, Discard a card. (If you cast this spell for its blitz cost, it gains "
        + "haste and \"When this creature dies, draw a card.\" Sacrifice it at the beginning of the next end "
        + "step.)\nYou may cast this card from your graveyard using its blitz ability.")]
    [InlineData("Stormbind", "Enchantment",
        "{2}, Discard a card at random: This enchantment deals 2 damage to any target.")]
    [InlineData("Summon: G.F. Ifrit", "Enchantment Creature — Saga Demon",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI, II — You "
        + "may discard a card. If you do, draw a card.\nIII, IV — Add {R}.")]
    // Confirmed 2026-09-29: a SYMMETRIC punisher follows "each player may
    // discard" — everybody chooses, you included.
    [InlineData("Possessed Portal", "Artifact",
        "If a player would draw a card, that player skips that draw instead.\nAt the beginning of each end "
        + "step, each player sacrifices a permanent of their choice unless they discard a card.")]
    [InlineData("Doom Foretold", "Enchantment",
        "At the beginning of each player's upkeep, that player sacrifices a nonland, nontoken permanent of "
        + "their choice. If that player can't, they discard a card, they lose 2 life, you draw a card, you gain "
        + "2 life, you create a 2/2 white Knight creature token with vigilance, then you sacrifice this "
        + "enchantment.")]
    // …but not when its owner may still play the exiled card (Elite Spellbinder),
    // the line drawn on 2026-09-28.
    [InlineData("Elite Spellbinder", "Creature — Human Cleric",
        "Flying\nWhen this creature enters, look at target opponent's hand. You may exile a nonland card from "
        + "it. For as long as that card remains exiled, its owner may play it. A spell cast this way costs {2} "
        + "more to cast.")]
    public void Classify_AHandAttackRuledOut_IsNotDiscard(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Four rulings of 2026-09-25, Buff side. A spell that is ONLY the pump is
    // Buff however small, at sorcery speed too — "anche se fa schifo".
    [InlineData("Honor", "Sorcery",
        "Put a +1/+1 counter on target creature.\nDraw a card.")]
    // The raised bar is for COUNTERS a creature hands out as it enters: a
    // temporary pump on entry is a trick and still counts…
    [InlineData("Toucan-Puffin", "Creature — Bird",
        "Flying\nWhen this creature enters, target creature you control gets +2/+0 until end of turn.")]
    // …and so do counters that come from the graveyard rather than the entry.
    [InlineData("Agent of Kotis", "Creature — Human Rogue",
        "Renew — {3}{U}, Exile this card from your graveyard: Put two +1/+1 counters on target creature. "
        + "Activate only as a sorcery.")]
    // A sorcery-speed COUNTER is not the irrelevant kind: it stays.
    [InlineData("Perilous Snare", "Artifact",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns "
        + "when an opponent loses life. Max speed is 4.)\nWhen this artifact enters, exile target nonland "
        + "permanent an opponent controls until this artifact leaves the battlefield.\nMax speed — {T}: Put a "
        + "+1/+1 counter on target creature or Vehicle you control. Activate only as a sorcery.")]
    public void Classify_APumpRuledIn_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Two counters as the creature enters are "come se fosse un 6/6".
    [InlineData("Apothecary Stomper", "Creature — Elephant",
        "Vigilance (Attacking doesn't cause this creature to tap.)\nWhen this creature enters, choose one —\n• "
        + "Put two +1/+1 counters on target creature you control.\n• You gain 4 life.")]
    // "+1/+1 a velocità sorcery è irrilevante come effetto. La carta fa altro."
    [InlineData("Starnheim Memento", "Artifact",
        "{T}: Add {W}.\n{1}{W}, {T}: Target creature gets +1/+1 and gains flying until end of turn. Activate "
        + "only as a sorcery.")]
    // "Colorless" counts as a tribe, as a lord and as counters.
    [InlineData("Kozilek, the Broken Reality", "Legendary Creature — Eldrazi",
        "When you cast this spell, up to two target players each manifest two cards from their hands. For "
        + "each card manifested this way, you draw a card. (To manifest a card, put it onto the battlefield "
        + "face down as a 2/2 creature. Turn it face up any time for its mana cost if it's a creature "
        + "card.)\nOther colorless creatures you control get +3/+2.")]
    [InlineData("It That Heralds the End", "Creature — Eldrazi Drone",
        "Colorless spells you cast with mana value 7 or greater cost {1} less to cast.\nOther colorless "
        + "creatures you control get +1/+1.")]
    [InlineData("Titans' Vanguard", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nWhen you cast this spell and whenever this creature attacks, put a "
        + "+1/+1 counter on each colorless creature you control.\nTrample")]
    public void Classify_APumpRuledOut_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Damage prevention is the other half of Protection, ruled 2026-09-15. Both
    // wordings count: the effect first, and the source first.
    [InlineData("Circle of Protection: Red", "Enchantment",
        "{1}: The next time a red source of your choice would deal damage to you this turn, prevent that damage.")]
    [InlineData("Healing Salve", "Instant",
        "Choose one —\n• Target player gains 3 life.\n• Prevent the next 3 damage that would be dealt to any target this turn.")]
    [InlineData("Orim, Samite Healer", "Legendary Creature — Human Cleric",
        "{T}: Prevent the next 3 damage that would be dealt to any target this turn.")]
    [InlineData("Indestructible Aura", "Instant", "Prevent all damage that would be dealt to target creature this turn.")]
    public void Classify_PreventingDamageForSomebodyElse_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // The three shapes of prevention that are NOT Protection. A shield the card
    // puts on itself is a stat line (modern wording, then a card naming itself);
    // a fog names no recipient at all; and preventing what a creature DEALS
    // neutralises it, which is Pacify's job — Maze of Ith and Gaseous Form are
    // hand-tagged that way.
    [InlineData("Ethereal Champion", "Creature — Spirit",
        "Pay 1 life: Prevent the next 1 damage that would be dealt to this creature this turn.")]
    [InlineData("Diamond Weapon", "Artifact Creature — Equipment",
        "Reach\nImmune — Prevent all combat damage that would be dealt to Diamond Weapon.")]
    [InlineData("Fog", "Instant", "Prevent all combat damage that would be dealt this turn.")]
    [InlineData("Maze of Ith", "Land",
        "{T}: Untap target attacking creature. Prevent all combat damage that would be dealt to and dealt by that creature this turn.")]
    public void Classify_PreventionThatIsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Fact]
    public void Classify_StaticPreventionOverAnArea_IsProtection()
    {
        // OVERTURNED 2026-09-29. Until then this test was
        // Classify_StaticPrevention_FailsTheSameTimingGateAsAPrintedKeyword and
        // asserted the opposite: Bubble Matrix sits on the board and is never
        // held up. The human then ruled that a static shield over an AREA is
        // Protection ("le protezioni statiche ad area … mettiamole Protection").
        CardEffect result = EffectClassifier.Classify(
            MakeCard("Bubble Matrix", "Artifact", "Prevent all damage that would be dealt to creatures."));

        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A STATIC shield over an AREA is Protection, ruled 2026-09-29: a group of
    // your permanents named by card type or colour (Restricted Office, The Walls
    // of Ba Sing Se, Righteous War, Shalai, Tam), a targeting ban on everybody
    // (Dense Foliage), static prevention for creatures (Inner Sanctum, Crystal
    // Barricade), and damage to you and your permanents redirected to one
    // (Ancient Adamantoise).
    [InlineData("Restricted Office // Lecture Hall", "Enchantment — Room // Enchantment — Room",
        "When you unlock this door, destroy all creatures with power 3 or greater.\n(You may cast either half. "
        + "That door unlocks on the battlefield. As a sorcery, you may pay the mana cost of a locked door to "
        + "unlock it.)\nOther permanents you control have hexproof.\n(You may cast either half. That door unlocks "
        + "on the battlefield. As a sorcery, you may pay the mana cost of a locked door to unlock it.)")]
    [InlineData("The Walls of Ba Sing Se", "Legendary Artifact Creature — Wall",
        "Defender\nOther permanents you control have indestructible.")]
    [InlineData("Righteous War", "Enchantment",
        "White creatures you control have protection from black.\nBlack creatures you control have protection "
        + "from white.")]
    [InlineData("Dense Foliage", "Enchantment",
        "Creatures can't be the targets of spells.")]
    [InlineData("Inner Sanctum", "Enchantment",
        "Cumulative upkeep—Pay 2 life. (At the beginning of your upkeep, put an age counter on this "
        + "permanent, then sacrifice it unless you pay its upkeep cost for each age counter on it.)\nPrevent all "
        + "damage that would be dealt to creatures you control.")]
    [InlineData("Crystal Barricade", "Artifact Creature — Wall",
        "Defender (This creature can't attack.)\nYou have hexproof. (You can't be the target of spells or "
        + "abilities your opponents control.)\nPrevent all noncombat damage that would be dealt to other "
        + "creatures you control.")]
    [InlineData("Ancient Adamantoise", "Creature — Turtle",
        "Vigilance, ward {3}\nDamage isn't removed from this creature during cleanup steps.\nAll damage that "
        + "would be dealt to you and other permanents you control is dealt to this creature instead.\nWhen this "
        + "creature dies, exile it and create ten tapped Treasure tokens.")]
    [InlineData("Shalai, Voice of Plenty", "Legendary Creature — Angel",
        "Flying\nYou, planeswalkers you control, and other creatures you control have hexproof.\n{4}{G}{G}: Put "
        + "a +1/+1 counter on each creature you control.")]
    [InlineData("Tam, Mindful First-Year", "Legendary Creature — Gorgon Wizard",
        "Each other creature you control has hexproof from each of its colors.\n{T}: Target creature you "
        + "control becomes all colors until end of turn.")]
    // Every permanent you control phased out (Teferi's Protection), and "you AND
    // permanents you control" (Veil of Summer): the permanents are shielded.
    [InlineData("Teferi's Protection", "Instant",
        "Until your next turn, your life total can't change and you gain protection from everything. All "
        + "permanents you control phase out. (While they're phased out, they're treated as though they don't "
        + "exist. They phase in before you untap during your untap step.)\nExile Teferi's Protection.")]
    [InlineData("Veil of Summer", "Instant",
        "Draw a card if an opponent has cast a blue or black spell this turn. Spells you control can't be "
        + "countered this turn. You and permanents you control gain hexproof from blue and from black until end "
        + "of turn. (You and they can't be the targets of blue or black spells or abilities your opponents "
        + "control.)")]
    public void Classify_AStaticShieldOverAnArea_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // …but not one with a CONDITION — counters, tapped or untapped (Innkeeper's
    // Talent, Saryth, The Seriema) — nor one for a creature TYPE (Riders of
    // Gavony's Humans, Sigarda's), nor an Equipment's single creature (Swiftfoot
    // Boots).
    [InlineData("Innkeeper's Talent", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nAt the beginning of combat on your turn, put "
        + "a +1/+1 counter on target creature you control.\n{G}: Level 2\nPermanents you control with counters on "
        + "them have ward {1}.\n{3}{G}: Level 3\nIf you would put one or more counters on a permanent or player, "
        + "put twice that many of each of those kinds of counters on that permanent or player instead.")]
    [InlineData("Saryth, the Viper's Fang", "Legendary Creature — Human Warlock",
        "Other tapped creatures you control have deathtouch.\nOther untapped creatures you control have "
        + "hexproof.\n{1}, {T}: Untap another target creature or land you control.")]
    [InlineData("The Seriema", "Legendary Artifact — Spacecraft",
        "When The Seriema enters, search your library for a legendary creature card, reveal it, put it into "
        + "your hand, then shuffle.\nStation (Tap another creature you control: Put charge counters equal to its "
        + "power on this Spacecraft. Station only as a sorcery. It's an artifact creature at 7+.)\n7+ | "
        + "Flying\nOther tapped legendary creatures you control have indestructible.")]
    [InlineData("Riders of Gavony", "Creature — Human Knight",
        "Vigilance\nAs this creature enters, choose a creature type.\nHuman creatures you control have "
        + "protection from creatures of the chosen type.")]
    [InlineData("Swiftfoot Boots", "Artifact — Equipment",
        "Equipped creature has hexproof and haste. (It can't be the target of spells or abilities your "
        + "opponents control. It can attack and {T} no matter when it came under your control.)\nEquip {1} ({1}: "
        + "Attach to target creature you control. Equip only as a sorcery.)")]
    [InlineData("Sigarda, Heron's Grace", "Legendary Creature — Angel",
        "Flying\nYou and Humans you control have hexproof.\n{2}, Exile a card from your graveyard: Create a 1/1 "
        + "white Human Soldier creature token.")]
    // A shield for the PLAYER ALONE is neither Protection nor Pacify, ruled
    // 2026-09-29 ("nessuno dei due"); shroud's reminder goes with it.
    [InlineData("Absolute Virtue", "Legendary Creature — Avatar Warrior",
        "This spell can't be countered.\nFlying\nYou have protection from each of your opponents. (You can't be "
        + "dealt damage, enchanted, or targeted by anything controlled by your opponents.)")]
    [InlineData("Blossoming Calm", "Instant",
        "You gain hexproof until your next turn. You gain 2 life.\nRebound (If you cast this spell from your "
        + "hand, exile it as it resolves. At the beginning of your next upkeep, you may cast this card from "
        + "exile without paying its mana cost.)")]
    [InlineData("Gilded Light", "Instant",
        "You gain shroud until end of turn. (You can't be the target of spells or abilities.)\nCycling {2} "
        + "({2}, Discard this card: Draw a card.)")]
    public void Classify_AStaticShieldNotOverAnArea_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // REGENERATION of somebody else is Protection, ruled 2026-09-28 ("sì"). This
    // OVERTURNS Classify_TargetedRegeneration_IsNotProtection, which pinned Death
    // Ward as a deliberate miss on the 2026-09-05 measurement; it was removed the
    // same day and Death Ward is pinned here instead.
    [InlineData("Death Ward", "Instant",
        "Regenerate target creature.")]
    [InlineData("Village Elder", "Creature — Human Druid",
        "{G}, {T}, Sacrifice a Forest: Regenerate target creature.")]
    [InlineData("Life Matrix", "Artifact",
        "{4}, {T}: Put a matrix counter on target creature and that creature gains \"Remove a matrix counter "
        + "from this creature: Regenerate this creature.\" Activate only during your upkeep.")]
    [InlineData("Broken Fall", "Enchantment",
        "Return this enchantment to its owner's hand: Regenerate target creature.")]
    // A BLINK that saves is Protection, ruled the same day: your own exiled and
    // returned, anybody's until the next end step, your own airbent when
    // targeted, your own cloaked. (Your own returned to HAND is pinned in
    // Classify_AnUnsummonOfYourOwn_IsProtectionNotBounce.)
    [InlineData("Salvation Swan", "Creature — Bird Cleric",
        "Flash\nFlying\nWhenever this creature or another Bird you control enters, exile up to one target "
        + "creature you control without flying. Return it to the battlefield under its owner's control with a "
        + "flying counter on it at the beginning of the next end step.")]
    [InlineData("Waterbender's Restoration", "Instant — Lesson",
        "As an additional cost to cast this spell, waterbend {X}. (While paying a waterbend cost, you can tap "
        + "your artifacts and creatures to help. Each one pays for {1}.)\nExile X target creatures you control. "
        + "Return those cards to the battlefield under their owner's control at the beginning of the next end "
        + "step.")]
    [InlineData("Getaway Glamer", "Instant",
        "Spree (Choose one or more additional costs.)\n+ {1} — Exile target nontoken creature. Return it to "
        + "the battlefield under its owner's control at the beginning of the next end step.\n+ {2} — Destroy "
        + "target creature if no other creature has greater power.")]
    [InlineData("Safe Haven", "Land",
        "{2}, {T}: Exile target creature you control.\nAt the beginning of your upkeep, you may sacrifice this "
        + "land. If you do, return each card exiled with this land to the battlefield under its owner's "
        + "control.")]
    [InlineData("Hide on the Ceiling", "Instant",
        "Exile X target artifacts and/or creatures. Return the exiled cards to the battlefield under their "
        + "owners' control at the beginning of the next end step.")]
    [InlineData("Parting Gust", "Instant",
        "Gift a tapped Fish (You may promise an opponent a gift as you cast this spell. If you do, they "
        + "create a tapped 1/1 blue Fish creature token before its other effects.)\nExile target nontoken "
        + "creature. If the gift wasn't promised, return that card to the battlefield under its owner's control "
        + "with a +1/+1 counter on it at the beginning of the next end step.")]
    [InlineData("Monk Gyatso", "Legendary Creature — Human Monk",
        "Whenever another creature you control becomes the target of a spell or ability, you may airbend that "
        + "creature. (Exile it. While it's exiled, its owner may cast it for {2} rather than its mana cost.)")]
    [InlineData("Expose the Culprit", "Instant",
        "Choose one or both —\n• Turn target face-down creature face up.\n• Exile any number of face-up "
        + "creatures you control with disguise in a face-down pile, shuffle that pile, then cloak them. (To "
        + "cloak a card, put it onto the battlefield face down as a 2/2 creature with ward {2}. Turn it face up "
        + "any time for its mana cost if it's a creature card.)")]
    // A SHIELD COUNTER (ruled the same day), a creature handed a return from
    // death, damage redirected away from a creature, umbra armor cast with
    // flash, and an artifact phased out.
    [InlineData("Protection Magic", "Instant",
        "Put a shield counter on each of up to three target creatures. (If a creature with a shield counter "
        + "would be dealt damage or destroyed, remove a shield counter from it instead.)")]
    [InlineData("Presumed Dead", "Instant",
        "Until end of turn, target creature gets +2/+0 and gains \"When this creature dies, return it to the "
        + "battlefield under its owner's control and suspect it.\" (A suspected creature has menace and can't "
        + "block.)")]
    [InlineData("Vincent's Limit Break", "Instant",
        "Tiered (Choose one additional cost.)\nUntil end of turn, target creature you control gains \"When this "
        + "creature dies, return it to the battlefield tapped under its owner's control\" and has the chosen "
        + "base power and toughness.\n• Galian Beast — {0} — 3/2.\n• Death Gigas — {1} — 5/2.\n• Hellmasker — {3} "
        + "— 7/2.")]
    [InlineData("Blood of the Martyr", "Instant",
        "Until end of turn, if damage would be dealt to any creature, you may have that damage dealt to you "
        + "instead.")]
    [InlineData("Reflect Damage", "Instant",
        "The next time a source of your choice would deal damage this turn, that damage is dealt to that "
        + "source's controller instead.")]
    [InlineData("Reverberation", "Instant",
        "All damage that would be dealt this turn by target sorcery spell is dealt to that spell's controller "
        + "instead.")]
    [InlineData("Dog Umbra", "Enchantment — Aura",
        "Flash\nEnchant creature\nAs long as another player controls enchanted creature, it can't attack or "
        + "block. Otherwise, this Aura has umbra armor. (If enchanted creature would be destroyed, instead "
        + "remove all damage from it and destroy this Aura.)")]
    [InlineData("Martyrdom", "Instant",
        "Until end of turn, target creature you control gains \"{0}: The next 1 damage that would be dealt to "
        + "target creature, planeswalker, or player this turn is dealt to this creature instead.\" Only you may "
        + "activate this ability.")]
    [InlineData("Vision Charm", "Instant",
        "Choose one —\n• Target player mills four cards.\n• Choose a land type and a basic land type. Each land "
        + "of the first chosen type becomes the second chosen type until end of turn.\n• Target artifact phases "
        + "out. (While it's phased out, it's treated as though it doesn't exist. It phases in before its "
        + "controller untaps during their next untap step.)")]
    // Readings the hand tags already had: prevention "dealt THIS TURN to", in
    // the passive, to "you and/or creatures you control", or to IT when IT is a
    // target; "as though it had flash"; a card turned face up and a card cycled
    // are held up; and "It gains" is a pronoun, not a tribe.
    [InlineData("Remedy", "Instant",
        "Prevent the next 5 damage that would be dealt this turn to any number of targets, divided as you "
        + "choose.")]
    [InlineData("Samite Alchemist", "Creature — Human Cleric",
        "{W}{W}, {T}: Prevent the next 4 damage that would be dealt this turn to target creature you control. "
        + "Tap that creature. It doesn't untap during your next untap step.")]
    [InlineData("Gatta and Luzzu", "Legendary Creature — Human Soldier",
        "Flash\nWhen Gatta and Luzzu enters, choose target creature you control. If damage would be dealt to "
        + "that creature this turn, prevent that damage and put that many +1/+1 counters on it.")]
    [InlineData("Silhouette", "Instant",
        "Choose target creature. If a spell or ability that targets that creature would cause a source to "
        + "deal damage to that creature this turn, prevent that damage.")]
    [InlineData("Fleeting Flight", "Instant",
        "Put a +1/+1 counter on target creature. It gains flying until end of turn. Prevent all combat damage "
        + "that would be dealt to it this turn.")]
    [InlineData("Shadowbane", "Instant",
        "The next time a source of your choice would deal damage to you and/or creatures you control this "
        + "turn, prevent that damage. If damage from a black source is prevented this way, you gain that much "
        + "life.")]
    [InlineData("Mystic Veil", "Enchantment — Aura",
        "You may cast this spell as though it had flash. If you cast it any time a sorcery couldn't have been "
        + "cast, the controller of the permanent it becomes sacrifices it at the beginning of the next cleanup "
        + "step.\nEnchant creature\nEnchanted creature has shroud. (It can't be the target of spells or "
        + "abilities.)")]
    [InlineData("Ward of Lights", "Enchantment — Aura",
        "You may cast this spell as though it had flash. If you cast it any time a sorcery couldn't have been "
        + "cast, the controller of the permanent it becomes sacrifices it at the beginning of the next cleanup "
        + "step.\nEnchant creature\nAs this Aura enters, choose a color.\nEnchanted creature has protection from "
        + "the chosen color. This effect doesn't remove this Aura.")]
    [InlineData("Essence of Antiquity", "Artifact Creature — Golem",
        "Disguise {2}{W} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it "
        + "face up any time for its disguise cost.)\nWhen this creature is turned face up, creatures you control "
        + "gain hexproof until end of turn. Untap them.")]
    [InlineData("Agonasaur Rex", "Creature — Dinosaur",
        "Trample\nCycling {2}{G} ({2}{G}, Discard this card: Draw a card.)\nWhen you cycle this card, put two "
        + "+1/+1 counters on up to one target creature or Vehicle. It gains trample and indestructible until "
        + "end of turn.")]
    [InlineData("Revitalizing Repast // Old-Growth Grove", "Instant // Land",
        "Put a +1/+1 counter on target creature. It gains indestructible until end of turn.\nThis land enters "
        + "tapped.\n{T}: Add {B} or {G}.")]
    // Ruled 2026-09-29, "si possono usare instant quindi contano": a blink on a
    // modal bullet under a trigger on casting a NONCREATURE spell (Kykar), and
    // one with the clock first, "at the beginning of the next end step, return
    // that card", on a card with flash (Phelia).
    [InlineData("Kykar, Zephyr Awakener", "Legendary Creature — Bird Wizard",
        "Flying\nWhenever you cast a noncreature spell, choose one —\n• Exile another target creature you "
        + "control. Return that card to the battlefield under its owner's control at the beginning of the next "
        + "end step.\n• Create a 1/1 white Spirit creature token with flying.")]
    [InlineData("Phelia, Exuberant Shepherd", "Legendary Creature — Dog",
        "Flash\nWhenever Phelia attacks, exile up to one other target nonland permanent. At the beginning of "
        + "the next end step, return that card to the battlefield under its owner's control. If it entered "
        + "under your control, put a +1/+1 counter on Phelia.")]
    public void Classify_ASaveHeldUpForSomethingElse_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // An UNSUMMON of your own held up in response IS Protection. Taken out on
    // 2026-09-29 ("Unsummon su se stessi non è protection") and put back the
    // same day — "una creatura flash che entra e rimbalza qualcosa di tuo conta
    // protection -> anche Dour Port-Mage può tornare Protection" — for every
    // instant-speed form, the instant included (Narrow Escape). It answers
    // nobody, so it is still not Bounce.
    [InlineData("Dour Port-Mage", "Creature — Frog Wizard",
        "Whenever one or more other creatures you control leave the battlefield without dying, draw a "
        + "card.\n{1}{U}, {T}: Return another target creature you control to its owner's hand.")]
    [InlineData("Sunpearl Kirin", "Creature — Kirin",
        "Flash\nFlying\nWhen this creature enters, return up to one other target nonland permanent you control "
        + "to its owner's hand. If it was a token, draw a card.")]
    [InlineData("Ambrosia Whiteheart", "Legendary Creature — Bird",
        "Flash\nWhen Ambrosia Whiteheart enters, you may return another permanent you control to its owner's "
        + "hand.\nLandfall — Whenever a land you control enters, Ambrosia Whiteheart gets +1/+0 until end of "
        + "turn.")]
    [InlineData("Forum Familiar", "Creature — Cat",
        "Disguise {1}{W} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it "
        + "face up any time for its disguise cost.)\nWhen this creature is turned face up, return another target "
        + "permanent you control to its owner's hand and put a +1/+1 counter on this creature.")]
    [InlineData("Vedalken Mastermind", "Creature — Vedalken Wizard",
        "{U}, {T}: Return target permanent you control to its owner's hand.")]
    [InlineData("Narrow Escape", "Instant",
        "Return target permanent you control to its owner's hand. You gain 4 life.")]
    public void Classify_AnUnsummonOfYourOwn_IsProtectionNotBounce(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Protection));
        Assert.False(result.HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // The gate asked of the LINE since 2026-09-28: an unrelated activation no
    // longer vouches for a static shield (Flickering Ward's return, Paladin's
    // Arms' equip, Cathedral Acolyte's counter, Redemption Arc's exile).
    [InlineData("Flickering Ward", "Enchantment — Aura",
        "Enchant creature\nAs this Aura enters, choose a color.\nEnchanted creature has protection from the "
        + "chosen color. This effect doesn't remove this Aura.\n{W}: Return this Aura to its owner's hand.")]
    [InlineData("Paladin's Arms", "Artifact — Equipment",
        "Job select (When this Equipment enters, create a 1/1 colorless Hero creature token, then attach this "
        + "to it.)\nEquipped creature gets +2/+1, has ward {1}, and is a Knight in addition to its other "
        + "types.\nLightbringer and Hero's Shield — Equip {4} ({4}: Attach to target creature you control. Equip "
        + "only as a sorcery.)")]
    [InlineData("Cathedral Acolyte", "Creature — Human Cleric",
        "Each creature you control with a counter on it has ward {1}. (Whenever it becomes the target of a "
        + "spell or ability an opponent controls, counter it unless that player pays {1}.)\n{T}: Put a +1/+1 "
        + "counter on target creature that entered this turn.")]
    [InlineData("Redemption Arc", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has indestructible and is goaded. (It attacks each combat if "
        + "able and attacks a player other than you if able.)\n{1}{W}: Exile enchanted creature.")]
    [InlineData("Codsworth, Handy Helper", "Legendary Artifact Creature — Robot",
        "Commanders you control have ward {2}.\n{T}: Add {W}{W}. Spend this mana only to cast Aura and/or "
        + "Equipment spells.\n{T}: Attach target Aura or Equipment you control to target creature you control. "
        + "Activate only as a sorcery.")]
    // A shield for ONE TRIBE or for TOKENS (0 of 4 tagged, the Buff reading of
    // 2026-09-21), a tribe's regeneration, and a shield being TAKEN AWAY (0 of
    // 5). Ainok Strike Leader was pinned as Protection by
    // Classify_EffectsAimedAtSomethingElse_Survive until that day.
    [InlineData("Ainok Strike Leader", "Creature — Dog Warrior",
        "Whenever you attack with this creature and/or your commander, for each opponent, create a 1/1 red "
        + "Goblin creature token that's tapped and attacking that player.\nSacrifice this creature: Creature "
        + "tokens you control gain indestructible until end of turn.")]
    [InlineData("Azlask, the Swelling Scourge", "Legendary Creature — Eldrazi",
        "Whenever Azlask or another colorless creature you control dies, you get an experience "
        + "counter.\n{W}{U}{B}{R}{G}: Creatures you control get +X/+X until end of turn, where X is the number "
        + "of experience counters you have. Scions and Spawns you control gain indestructible and annihilator 1 "
        + "until end of turn.")]
    [InlineData("Basri, Tomorrow's Champion", "Legendary Creature — Human Knight",
        "{W}, {T}, Exert Basri: Create a 1/1 white Cat creature token with lifelink. (An exerted creature "
        + "won't untap during your next untap step.)\nCycling {2}{W} ({2}{W}, Discard this card: Draw a "
        + "card.)\nWhen you cycle this card, Cats you control gain hexproof and indestructible until end of "
        + "turn.")]
    [InlineData("Goblin Wizard", "Creature — Goblin Wizard",
        "{T}: You may put a Goblin permanent card from your hand onto the battlefield.\n{R}: Target Goblin "
        + "gains protection from white until end of turn.")]
    [InlineData("Baron Sengir", "Legendary Creature — Vampire Noble",
        "Flying\nWhenever a creature dealt damage by Baron Sengir this turn dies, put a +2/+2 counter on Baron "
        + "Sengir.\n{T}: Regenerate another target Vampire.")]
    [InlineData("Nowhere to Run", "Enchantment",
        "Flash\nWhen this enchantment enters, target creature an opponent controls gets -3/-3 until end of "
        + "turn.\nCreatures your opponents control can be the targets of spells and abilities as though they "
        + "didn't have hexproof. Ward abilities of those creatures don't trigger.")]
    [InlineData("Spectacular Pileup", "Sorcery",
        "All creatures and Vehicles lose indestructible until end of turn, then destroy all creatures and "
        + "Vehicles.\nCycling {2} ({2}, Discard this card: Draw a card.)")]
    [InlineData("Autumn Willow", "Legendary Creature — Avatar",
        "Shroud (This creature can't be the target of spells or abilities.)\n{G}: Until end of turn, Autumn "
        + "Willow can be the target of spells and abilities controlled by target player as though it didn't "
        + "have shroud.")]
    // A creature regenerating ITSELF; a blink or a bounce that cannot be held
    // up, or is paid as a cost; prevention for YOU alone or for the card
    // itself; flash only for a commander; umbra armor without flash.
    [InlineData("River Boa", "Creature — Snake",
        "Islandwalk (This creature can't be blocked as long as defending player controls an Island.)\n{G}: "
        + "Regenerate this creature.")]
    [InlineData("Skyskipper Duo", "Creature — Bird Frog",
        "Flying\nWhen this creature enters, exile up to one other target creature you control. Return it to "
        + "the battlefield under its owner's control at the beginning of the next end step.")]
    [InlineData("Familiar's Ruse", "Instant",
        "As an additional cost to cast this spell, return a creature you control to its owner's hand.\nCounter "
        + "target spell.")]
    [InlineData("Shrieking Drake", "Creature — Drake",
        "Flying\nWhen this creature enters, return a creature you control to its owner's hand.")]
    [InlineData("Immortal Coil", "Artifact",
        "{T}, Exile two cards from your graveyard: Draw a card.\nIf damage would be dealt to you, prevent that "
        + "damage. Exile a card from your graveyard for each 1 damage prevented this way.\nWhen there are no "
        + "cards in your graveyard, you lose the game.")]
    [InlineData("Phantom Nantuko", "Creature — Insect Spirit",
        "Trample\nThis creature enters with two +1/+1 counters on it.\nIf damage would be dealt to this "
        + "creature, prevent that damage. Remove a +1/+1 counter from this creature.\n{T}: Put a +1/+1 counter "
        + "on this creature.")]
    [InlineData("Timely Ward", "Enchantment — Aura",
        "You may cast this spell as though it had flash if it targets a commander.\nEnchant creature\nEnchanted "
        + "creature has indestructible.")]
    [InlineData("Lion Umbra", "Enchantment — Aura",
        "Enchant modified creature (Equipment, Auras its controller controls, and counters are "
        + "modifications.)\nEnchanted creature gets +3/+3 and has reach and vigilance.\nUmbra armor (If enchanted "
        + "creature would be destroyed, instead remove all damage from it and destroy this Aura.)")]
    // "IT" opening a sentence after "this creature" is the card itself (2026-09-29).
    [InlineData("Pristine Skywise", "Creature — Dragon",
        "Flying\nWhenever you cast a noncreature spell, untap this creature. It gains protection from the "
        + "color of your choice until end of turn.")]
    [InlineData("Seasoned Hallowblade", "Creature — Human Warrior",
        "Discard a card: Tap this creature. It gains indestructible until end of turn. (Damage and effects "
        + "that say \"destroy\" don't destroy it.)")]
    // "HE" or "SHE" is always the card itself (2026-09-29).
    [InlineData("Miles Morales // Ultimate Spider-Man", "Legendary Creature — Human Citizen Hero // Legendary Creature — Spider Human Hero",
        "When Miles Morales enters, put a +1/+1 counter on each of up to two target creatures.\n{3}{R}{G}{W}: "
        + "Transform Miles Morales. Activate only as a sorcery.\nFirst strike, haste\nCamouflage — {2}: Put a "
        + "+1/+1 counter on Ultimate Spider-Man. He gains hexproof and becomes colorless until end of "
        + "turn.\nWhenever you attack, double the number of each kind of counter on each Spider and legendary "
        + "creature you control.")]
    // A BLINK at SORCERY SPEED saves nothing (2026-09-29); a shield that lasts
    // the turn still does, see Classify_EnduranceBobblehead_IsBuffAndProtection.
    [InlineData("Lilysplash Mentor", "Creature — Frog Druid",
        "Reach\n{1}{G}{U}: Exile another target creature you control, then return it to the battlefield under "
        + "its owner's control with a +1/+1 counter on it. Activate only as a sorcery.")]
    // A shield taken away in the singular, joined 2026-09-29 when the human took
    // Protection off Rebel Salvo.
    [InlineData("Rebel Salvo", "Instant",
        "Affinity for Equipment (This spell costs {1} less to cast for each Equipment you control.)\nRebel "
        + "Salvo deals 5 damage to target creature or planeswalker. That permanent loses indestructible until "
        + "end of turn.")]
    public void Classify_NotASaveHeldUpForSomethingElse_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A shield an Aura or an Equipment gives the creature it is ON, ruled
    // 2026-09-29: the Aura went on at sorcery speed, whatever its ability costs
    // afterwards.
    [InlineData("Carapace", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets +0/+2.\nSacrifice this Aura: Regenerate enchanted creature.")]
    [InlineData("Regeneration", "Enchantment — Aura",
        "Enchant creature (Target a creature as you cast this. This card enters attached to that "
        + "creature.)\n{G}: Regenerate enchanted creature. (The next time that creature would be destroyed this "
        + "turn, instead tap it, remove it from combat, and heal all damage on it.)")]
    [InlineData("Nurturing Licid", "Creature — Licid",
        "{G}, {T}: This creature loses this ability and becomes an Aura enchantment with enchant creature. "
        + "Attach it to target creature. You may pay {G} to end this effect.\n{G}: Regenerate enchanted "
        + "creature.")]
    [InlineData("Kithkin Armor", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature can't be blocked by creatures with power 3 or greater.\nSacrifice "
        + "this Aura: The next time a source of your choice would deal damage to enchanted creature this turn, "
        + "prevent that damage.")]
    [InlineData("Ring of Evos Isle", "Artifact — Equipment",
        "{2}: Equipped creature gains hexproof until end of turn. (It can't be the target of spells or "
        + "abilities your opponents control.)\nAt the beginning of your upkeep, put a +1/+1 counter on equipped "
        + "creature if it's blue.\nEquip {1} ({1}: Attach to target creature you control. Equip only as a "
        + "sorcery.)")]
    [InlineData("General's Kabuto", "Artifact — Equipment",
        "Equipped creature has shroud. (It can't be the target of spells or abilities.)\nPrevent all combat "
        + "damage that would be dealt to equipped creature.\nEquip {2} ({2}: Attach to target creature you "
        + "control. Equip only as a sorcery.)")]
    public void Classify_AShieldAnAuraOrEquipmentWears_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // …but a FLASH Aura still answers, and an ability the Aura hands its creature
    // in quotes, or one aimed at a target, shields somebody else (2026-09-29).
    [InlineData("Serpent Skin", "Enchantment — Aura",
        "Flash\nEnchant creature\nEnchanted creature gets +1/+1.\n{G}: Regenerate enchanted creature.")]
    [InlineData("Samite Blessing", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has \"{T}: The next time a source of your choice would deal "
        + "damage to target creature this turn, prevent that damage.\"")]
    [InlineData("Floating Shield", "Enchantment — Aura",
        "Enchant creature\nAs this Aura enters, choose a color.\nEnchanted creature has protection from the "
        + "chosen color. This effect doesn't remove this Aura.\nSacrifice this Aura: Target creature gains "
        + "protection from the chosen color until end of turn.")]
    [InlineData("Healer's Headdress", "Artifact — Equipment",
        "Equipped creature gets +0/+2 and has \"{T}: Prevent the next 1 damage that would be dealt to any "
        + "target this turn.\"\n{W}{W}: Attach this Equipment to target creature you control.\nEquip {1} ({1}: "
        + "Attach to target creature you control. Equip only as a sorcery.)")]
    public void Classify_AnAuraThatAnswersOrShieldsSomebodyElse_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A shield on a creature the same card destroys at the next end step saves
    // nothing, ruled 2026-09-29 by name: "il muro viene distrutto alla fine, non è
    // una protezione".
    [InlineData("Glyph of Destruction", "Instant",
        "Target blocking Wall you control gets +10/+0 until end of combat. Prevent all damage that would be "
        + "dealt to it this turn. Destroy it at the beginning of the next end step.")]
    public void Classify_AShieldOnACreatureTheCardDestroys_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Mill has to be aimed at somebody else. Everything here fills the caster's
    // own graveyard: as an upkeep tax (Deep Spawn), as an activation cost
    // (Millikin), as a recursion cost (Rot Farm Skeleton), or as the whole point
    // of a graveyard deck (Ooze Patrol). The old rule fired on all four because
    // it only looked for the verb, and needed a separate cost guard to undo the
    // first three; naming the victim covers all four at once.
    [InlineData("Deep Spawn", "Creature — Kraken",
        "Trample\nAt the beginning of your upkeep, sacrifice this creature unless you mill two cards.\n{U}: This creature gains shroud until end of turn.")]
    [InlineData("Millikin", "Artifact Creature — Construct", "{T}, Mill a card: Add {C}.")]
    [InlineData("Rot Farm Skeleton", "Creature — Plant Skeleton",
        "This creature can't block.\n{2}{B}{G}, Mill four cards: Return this card from your graveyard to the battlefield. Activate only as a sorcery.")]
    [InlineData("Ooze Patrol", "Creature — Ooze",
        "When this creature enters, mill two cards, then put a +1/+1 counter on this creature for each creature card in your graveyard.")]
    public void Classify_MillingYourself_IsNotMill(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // The shapes the victim gets named in. Altar of Dementia puts it after a
    // cost, Millstone in front of the spelled-out pre-2021 wording, Specimen
    // Freighter names the defending player, and Bruvac's only mill wording is
    // reminder text — which is part of oracle_text and counts.
    [InlineData("Altar of Dementia", "Artifact", "Sacrifice a creature: Target player mills cards equal to the sacrificed creature's power.")]
    [InlineData("Millstone", "Artifact", "{2}, {T}: Target player puts the top two cards of their library into their graveyard.")]
    [InlineData("Specimen Freighter", "Artifact — Spacecraft", "Whenever this Spacecraft attacks, defending player mills four cards.")]
    [InlineData("Bruvac the Grandiloquent", "Legendary Creature — Human Advisor",
        "If an opponent would mill one or more cards, they mill twice that many cards instead. (To mill a card, a player puts the top card of their library into their graveyard.)")]
    public void Classify_MillingSomebodyElse_IsMill(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // Tokens means creature bodies on YOUR side. A Treasure, a Clue, a Food or a
    // Lander is a resource the card hands over in passing, and Afterlife's
    // Spirit is a body for the other player.
    [InlineData("Ichor Wellspring", "Artifact", "When this artifact enters, create a Treasure token.")]
    [InlineData("Tireless Tracker", "Creature — Human Scout", "Whenever a land you control enters, investigate. (Create a Clue token.)")]
    [InlineData("Afterlife", "Instant", "Destroy target creature. It can't be regenerated. Its controller creates a 1/1 white Spirit creature token with flying.")]
    [InlineData("Phelddagrif", "Legendary Creature — Hippo", "{G}: Phelddagrif gains trample until end of turn. Target opponent creates a 1/1 green Hippo creature token.")]
    public void Classify_TokensThatAreNotYourCreatures_IsNotTokens(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    [InlineData("Lingering Souls", "Sorcery", "Create two 1/1 white Spirit creature tokens with flying.")]
    // The token that never says "creature token" because it is a copy of one.
    [InlineData("Kiki-Jiki, Mirror Breaker", "Legendary Creature — Goblin Shaman",
        "{T}: Create a token that's a copy of another target nonlegendary creature you control. That token gains haste.")]
    public void Classify_CreatureTokensForYou_IsTokens(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Discarding is only an effect when somebody else's hand is emptied. These
    // three PAY a card: as an activation cost, as an alternative cast cost, and
    // as the back half of a loot.
    [InlineData("Wild Mongrel", "Creature — Dog", "Discard a card: This creature gets +1/+1 and becomes the color of your choice until end of turn.")]
    [InlineData("Basking Rootwalla", "Creature — Lizard", "Madness {0} (If you discard this card, discard it into exile.)")]
    [InlineData("Faithless Looting", "Sorcery", "Draw two cards, then discard two cards.")]
    public void Classify_DiscardingYourOwnCards_IsNotDiscard(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Returning something to hand is Bounce only when it names what goes back.
    // Ovinomancer pays its own return as an activation cost and Fleeting Effigy
    // pays it as a drawback; neither answers anything.
    [InlineData("Ovinomancer", "Creature — Human Wizard",
        "{T}, Return this creature to its owner's hand: Destroy target creature. It can't be regenerated.")]
    [InlineData("Fleeting Effigy", "Creature — Spirit",
        "Haste\nAt the beginning of your end step, return this creature to its owner's hand.")]
    public void Classify_ReturningItself_IsNotBounce(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    [InlineData("Unsummon", "Instant", "Return target creature to its owner's hand.")]
    // "up to one other" sits between the verb and "target", and the plural form
    // is "to their owners' hands" — both broke a tighter first draft.
    [InlineData("Spider-Byte, Web Warden", "Creature — Spider",
        "When Spider-Byte enters, return up to one target nonland permanent to its owner's hand.")]
    [InlineData("Undo", "Instant", "Return two target creatures to their owners' hands.")]
    [InlineData("Evacuation", "Instant", "Return all creatures to their owners' hands.")]
    public void Classify_ReturningSomethingNamed_IsBounce(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Read 2026-09-29, each shape tagged on every reviewed card using it: a
    // target put on TOP of its owner's library (Time Ebb, Ether Well), SECOND or
    // THIRD from the top (Deem Inferior, Riptide Gearhulk), "the OWNER OF TARGET
    // … puts it on their choice of the top or bottom" (Riverwalk Technique), the
    // owner as the target (Hurkyl's Recall), creatures the opponents chose
    // (Summon: Valefor). A permanent of YOURS in the same clause as one of theirs
    // still bounces theirs (Aether Tradewinds), the Auras you own on the target
    // are not the target (Word of Undoing), and lands paid as a cost do not
    // hide the creature bounced (Flooded Shoreline).
    [InlineData("Time Ebb", "Sorcery",
        "Put target creature on top of its owner's library.")]
    [InlineData("Ether Well", "Instant",
        "Put target creature on top of its owner's library. If that creature is red, you may put it on the "
        + "bottom of its owner's library instead.")]
    [InlineData("Deem Inferior", "Sorcery",
        "This spell costs {1} less to cast for each card you've drawn this turn.\nThe owner of target nonland "
        + "permanent puts it into their library second from the top or on the bottom.")]
    [InlineData("Riptide Gearhulk", "Artifact Creature — Construct",
        "Double strike\nProwess (Whenever you cast a noncreature spell, this creature gets +1/+1 until end of "
        + "turn.)\nWhen this creature enters, for each opponent, put up to one target nonland permanent that "
        + "player controls into its owner's library third from the top.")]
    [InlineData("Riverwalk Technique", "Instant",
        "Choose one —\n• The owner of target nonland permanent puts it on their choice of the top or bottom of "
        + "their library.\n• Counter target noncreature spell.")]
    [InlineData("Hurkyl's Recall", "Instant",
        "Return all artifacts target player owns to their hand.")]
    [InlineData("Summon: Valefor", "Enchantment Creature — Saga Drake",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI — Sonic "
        + "Wings — Each opponent chooses a creature with the greatest mana value among creatures they control. "
        + "Return those creatures to their owners' hands.\nII, III, IV — Tap up to one target creature and put a "
        + "stun counter on it.\nFlying")]
    [InlineData("Word of Undoing", "Instant",
        "Return target creature and all white Auras you own attached to it to their owners' hands.")]
    [InlineData("Aether Tradewinds", "Instant",
        "Return target permanent you control and target permanent you don't control to their owners' hands.")]
    [InlineData("Flooded Shoreline", "Enchantment",
        "{U}{U}, Return two Islands you control to their owner's hand: Return target creature to its owner's "
        + "hand.")]
    public void Classify_AReturnThatAnswersSomething_IsBounce(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Returns that answer nobody, each untagged on (nearly) every reviewed card:
    // your OWN permanent (3 of 31 tagged, taken to the human as slips), a LAND
    // (0 of 5), the Auras on your own permanent, a card EXILED WITH the card;
    // and the library placements of a creature of YOURS (Civic Guildmage), of
    // the card ITSELF (God-Eternal Kefnet) and of a card from a HAND (Lost Hours).
    [InlineData("Exosuit Savior", "Creature — Human Soldier",
        "Flying\nWhen this creature enters, return up to one other target permanent you control to its owner's "
        + "hand.")]
    [InlineData("Essence Reliquary", "Artifact",
        "{T}: Return another target permanent you control and all Auras you control attached to it to their "
        + "owner's hand. Activate only during your turn.")]
    [InlineData("Scarab of the Unseen", "Artifact",
        "{T}, Sacrifice this artifact: Return all Auras attached to target permanent you own to their owners' "
        + "hands. Draw a card at the beginning of the next turn's upkeep.")]
    [InlineData("Active Volcano", "Instant",
        "Choose one —\n• Destroy target blue permanent.\n• Return target Island to its owner's hand.")]
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Mole Beast",
        "When Nyla enters, exile up to one target creature card from your graveyard. If you do, you lose X "
        + "life and create X Clue tokens, where X is that card's mana value. (A Clue token is an artifact with "
        + "\"{2}, Sacrifice this token: Draw a card.\")\nAt the beginning of your end step, if you control no "
        + "Clues, return target card exiled with Nyla to its owner's hand.")]
    [InlineData("Civic Guildmage", "Creature — Human Wizard",
        "{G}, {T}: Target creature gets +0/+1 until end of turn.\n{U}, {T}: Put target creature you control on "
        + "top of its owner's library.")]
    [InlineData("God-Eternal Kefnet", "Legendary Creature — Zombie God",
        "Flying\nYou may reveal the first card you draw each turn as you draw it. Whenever you reveal an "
        + "instant or sorcery card this way, copy that card and you may cast the copy. That copy costs {2} less "
        + "to cast.\nWhen God-Eternal Kefnet dies or is put into exile from the battlefield, you may put it into "
        + "its owner's library third from the top.")]
    [InlineData("Lost Hours", "Sorcery",
        "Target player reveals their hand. You choose a nonland card from it. That player puts that card into "
        + "their library third from the top.")]
    public void Classify_AReturnThatAnswersNobody_IsNotBounce(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Ruled 2026-09-29: AIRBEND aimed where an opponent's permanent can be is
    // Bounce (Airbending Lesson, Aang the Last Airbender, Avatar's Wrath); an
    // Aura's creature returned is Bounce whoever pays (Phantom Wings, Sun Clasp —
    // "bounce entrambe"); and a permanent exiled until the card leaves or dies,
    // then returned to its owner's hand, is Bounce and — for the graveyard cards
    // exiled with it — Regrowth (Aurelia's Vindicator, The Spot).
    [InlineData("Airbending Lesson", "Instant — Lesson",
        "Airbend target nonland permanent. (Exile it. While it's exiled, its owner may cast it for {2} rather "
        + "than its mana cost.)\nDraw a card.")]
    [InlineData("Aang, the Last Airbender", "Legendary Creature — Human Avatar Ally",
        "Flying\nWhen Aang enters, airbend up to one other target nonland permanent. (Exile it. While it's "
        + "exiled, its owner may cast it for {2} rather than its mana cost.)\nWhenever you cast a Lesson spell, "
        + "Aang gains lifelink until end of turn.")]
    [InlineData("Avatar's Wrath", "Sorcery",
        "Choose up to one target creature, then airbend all other creatures. (Exile them. While each one is "
        + "exiled, its owner may cast it for {2} rather than its mana cost.)\nUntil your next turn, your "
        + "opponents can't cast spells from anywhere other than their hands.\nExile Avatar's Wrath.")]
    [InlineData("Sun Clasp", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets +1/+3.\n{W}: Return enchanted creature to its owner's hand.")]
    [InlineData("Phantom Wings", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has flying.\nSacrifice this Aura: Return enchanted creature to "
        + "its owner's hand.")]
    [InlineData("Aurelia's Vindicator", "Creature — Angel",
        "Flying, lifelink, ward {2}\nDisguise {X}{3}{W}\nWhen this creature is turned face up, exile up to X "
        + "other target creatures from the battlefield and/or creature cards from graveyards.\nWhen this "
        + "creature leaves the battlefield, return the exiled cards to their owners' hands.")]
    [InlineData("The Spot, Living Portal", "Legendary Creature — Human Scientist Villain",
        "When The Spot enters, exile up to one target nonland permanent and up to one target nonland "
        + "permanent card from a graveyard.\nWhen The Spot dies, put him on the bottom of his owner's library. "
        + "If you do, return the exiled cards to their owners' hands.")]
    public void Classify_ABounceRuledIn_IsBounce(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Airbending YOUR OWN is Protection, not Bounce ("su di te è protection").
    [InlineData("Airbender's Reversal", "Instant — Lesson",
        "Choose one —\n• Destroy target attacking creature.\n• Airbend target creature you control. (Exile it. "
        + "While it's exiled, its owner may cast it for {2} rather than its mana cost.)")]
    [InlineData("Appa, Steadfast Guardian", "Legendary Creature — Bison Ally",
        "Flash\nFlying\nWhen Appa enters, airbend any number of other target nonland permanents you control. "
        + "(Exile them. While each one is exiled, its owner may cast it for {2} rather than its mana "
        + "cost.)\nWhenever you cast a spell from exile, create a 1/1 white Ally creature token.")]
    [InlineData("Monk Gyatso", "Legendary Creature — Human Monk",
        "Whenever another creature you control becomes the target of a spell or ability, you may airbend that "
        + "creature. (Exile it. While it's exiled, its owner may cast it for {2} rather than its mana cost.)")]
    public void Classify_AirbendingYourOwn_IsProtectionNotBounce(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Protection));
        Assert.False(result.HasFlag(CardEffect.Bounce));
    }

    [Fact]
    public void Classify_AHastyCreatureBackInHandAtYourEndStep_IsBurnNotBounce()
    {
        // "Come il viashino" (2026-09-29): the 2026-09-20 ruling on haste that
        // is gone at the end of the turn, read for "YOUR end step" too.
        CardEffect result = EffectClassifier.Classify(MakeCard(
            "Fleeting Effigy", "Creature — Elemental",
            "Haste\nAt the beginning of your end step, return this creature to its owner's hand. (Return it only "
            + "if it's on the battlefield.)\n{2}{R}: This creature gets +2/+0 until end of turn."));

        Assert.True(result.HasFlag(CardEffect.Burn));
        Assert.False(result.HasFlag(CardEffect.Bounce));
    }

    [Fact]
    public void Classify_GraveyardCardsExiledThenReturnedToHand_IsRegrowthAndBounce()
    {
        // Ruled 2026-09-29, "Regrowth E Bounce (lo fa anche dal battlefield)".
        CardEffect result = EffectClassifier.Classify(MakeCard("The Spot, Living Portal",
            "Legendary Creature — Human Scientist Villain",
            "When The Spot enters, exile up to one target nonland permanent and up to one target nonland permanent "
            + "card from a graveyard.\nWhen The Spot dies, put him on the bottom of his owner's library. If you do, "
            + "return the exiled cards to their owners' hands."));

        Assert.True(result.HasFlag(CardEffect.Regrowth));
        Assert.True(result.HasFlag(CardEffect.Bounce));
    }

    [Fact]
    public void Classify_ACreatureCardFromYourGraveyardWithALongCondition_IsRegrowth()
    {
        // Krile Baldesion's condition runs the clause past 70 characters; its
        // Bounce tag was a misclick (2026-09-29).
        CardEffect result = EffectClassifier.Classify(MakeCard(
            "Krile Baldesion", "Legendary Creature — Dwarf Wizard",
            "Lifelink\nTrace Aether — Whenever you cast a noncreature spell, you may return target creature card "
            + "with mana value equal to that spell's mana value from your graveyard to your hand. Do this only once "
            + "each turn."));

        Assert.True(result.HasFlag(CardEffect.Regrowth));
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
    // Mana you get for nothing but a tap is a mana SOURCE: the card is Ramp, and
    // the colour is a property of the source rather than a service. Ruled
    // 2026-09-18 — a bare {T} for any colour was tagged ManaFixing on 2 of 28
    // reviewed cards, and for a choice of two colours 0 of 3.
    [InlineData("Birds of Paradise", "Creature — Bird", "Flying\n{T}: Add one mana of any color.")]
    [InlineData("Mox Jasper", "Legendary Artifact",
        "{T}: Add one mana of any color. Activate only if you control a Dragon.")]
    [InlineData("Wandertale Mentor", "Creature — Raccoon Bard", "{T}: Add {R} or {G}.")]
    // …including when the bare tap is granted to something else, which is the
    // same ability one step removed.
    [InlineData("A Realm Reborn", "Enchantment",
        "Other permanents you control have \"{T}: Add one mana of any color.\"")]
    // The same bare tap behind an ability word (Corrupted), which the human took
    // ManaFixing off on 2026-09-29.
    [InlineData("Glistening Sphere", "Artifact",
        "This artifact enters tapped.\nWhen this artifact enters, proliferate.\n{T}: Add one mana of any "
        + "color.\nCorrupted — {T}: Add three mana of any one color. Activate only if an opponent has three or "
        + "more poison counters.")]
    public void Classify_ManaForNothingButATap_IsRampNotManaFixing(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // …but pay something on top of the tap and the card converts colour instead
    // of making it, which is the whole job: 11 of 12 reviewed cards of this
    // shape were tagged. A LAND is exempt and never asked — 17 of 17.
    [InlineData("Mana Prism", "Artifact", "{T}: Add {C}.\n{1}, {T}: Add one mana of any color.")]
    [InlineData("Celestial Prism", "Artifact", "{2}, {T}: Add one mana of any color.")]
    [InlineData("Gene Pollinator", "Creature — Phyrexian Insect",
        "{T}, Tap an untapped permanent you control: Add one mana of any color.")]
    [InlineData("Birds of Paradise, but a land", "Land", "{T}: Add one mana of any color.")]
    public void Classify_ManaThatCostsSomething_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // A land fetched to HAND fixes the colour you are short of and ramps
    // nothing — the land still has to be played, off your one land drop. Ruled
    // 2026-09-18; tagged on 28 of the 30 reviewed cards of this shape.
    [InlineData("Abzan Monument", "Artifact",
        "When this artifact enters, search your library for a basic Plains, Swamp, or Forest card, reveal it, put it into your hand, then shuffle.")]
    [InlineData("Spineseeker Centipede", "Creature — Insect",
        "When this creature enters, search your library for a basic land card, reveal it, put it into your hand, then shuffle.")]
    [InlineData("Spider-Bot", "Artifact Creature — Spider Robot Scout",
        "When this creature enters, you may search your library for a basic land card, reveal it, then shuffle and put that card on top.")]
    public void Classify_LandSearchToHand_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Fact]
    public void Classify_LandSearchToTheBattlefield_IsRampNotManaFixing()
    {
        // The other half of the same ruling: putting the land straight onto the
        // battlefield is the extra mana, not the colour. Tagged ManaFixing on
        // only 8 of the 51 reviewed cards of this shape.
        CardEffect result = EffectClassifier.Classify(MakeCard("Rampant Growth", "Sorcery",
            "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."));

        Assert.True(result.HasFlag(CardEffect.Ramp));
        Assert.False(result.HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // The eight Ramp families ruled 2026-09-18. Each is mana you did not have to
    // make, or a land you did not have to draw.
    [InlineData("Stone Calendar", "Artifact", "Spells you cast cost {1} less to cast.")]
    [InlineData("Wall of Roots", "Creature — Plant Wall",
        "Defender\nPut a -0/-1 counter on this creature: Add {G}. Activate only once each turn.")]
    [InlineData("Elvish Spirit Guide", "Creature — Elf Spirit", "Exile this card from your hand: Add {G}.")]
    [InlineData("Fastbond", "Enchantment", "You may play any number of lands on each of your turns.")]
    [InlineData("Wild Growth", "Enchantment — Aura",
        "Enchant land\nWhenever enchanted land is tapped for mana, its controller adds an additional {G}.")]
    [InlineData("Skyshroud Ranger", "Creature — Elf Scout",
        "{T}: You may put a land card from your hand onto the battlefield. Activate only as a sorcery.")]
    [InlineData("Ley Druid", "Creature — Human Druid", "{T}: Untap target land.")]
    [InlineData("Veteran Explorer", "Creature — Human Soldier Scout",
        "When this creature dies, each player may search their library for up to two basic land cards, "
        + "put them onto the battlefield, then shuffle.")]
    // A fetch aimed at TARGET player is one you point at yourself: Fertilid's
    // Favor, tagged Ramp by the human on 2026-09-29.
    [InlineData("Fertilid's Favor", "Instant",
        "Target player searches their library for a basic land card, puts it onto the battlefield tapped, "
        + "then shuffles. Put two +1/+1 counters on up to one target artifact or creature.")]
    public void Classify_ManaYouDidNotHaveToMake_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A discount on the card's own cost buys nothing you did not already have —
    // hand-tagged Ramp on 0 of 40 cards, against 20 of 40 for the rest.
    [InlineData("Affinity-style", "Artifact", "This spell costs {1} less to cast for each artifact you control.")]
    // The land handed to the player a removal spell was aimed at is their
    // consolation, not your ramp.
    [InlineData("Emergency Eject", "Instant",
        "Destroy target nonland permanent. Its controller creates a Lander token. (It's an artifact with "
        + "\"{2}, {T}, Sacrifice this token: Search your library for a basic land card, put it onto the "
        + "battlefield tapped, then shuffle.\")")]
    // One land for one land is a swap. Harrow pays one for TWO and does ramp,
    // which is why the count is the test and not the cost.
    [InlineData("Renewal", "Sorcery",
        "As an additional cost to cast this spell, sacrifice a land.\n"
        + "Search your library for a basic land card, put that card onto the battlefield, then shuffle.")]
    public void Classify_ManaOrLandThatIsNotYours_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Fact]
    public void Classify_Harrow_PaysOneLandForTwo_IsRamp()
    {
        Assert.True(EffectClassifier.Classify(MakeCard("Harrow", "Instant",
            "As an additional cost to cast this spell, sacrifice a land.\n"
            + "Search your library for up to two basic land cards, put them onto the battlefield, then shuffle."))
            .HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Mana that costs mana converts colour; it adds none. Asked of the whole
    // card, so one free ability elsewhere keeps the tag. The cost is any mana
    // symbol, coloured included — Fire Sprites asks {G} for its {R} and is the
    // plainest card in the family.
    [InlineData("Fire Sprites", "Creature — Faerie", "Flying\n{G}, {T}: Add {R}.")]
    [InlineData("Celestial Prism", "Artifact", "{2}, {T}: Add one mana of any color.")]
    public void Classify_ManaThatCostsMana_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Fact]
    public void Classify_PaidManaThatGivesBackMore_IsStillRamp()
    {
        // "…salvo esempi che generano più mana del costo": Astrolabe pays {1}
        // for two, so it is up on the trade.
        Assert.True(EffectClassifier.Classify(MakeCard("Astrolabe", "Artifact",
            "{1}, {T}, Sacrifice this artifact: Add two mana of any one color."))
            .HasFlag(CardEffect.Ramp));
    }

    [Fact]
    public void Classify_OneFreeAbilityBesideAPaidOne_IsStillRamp()
    {
        Assert.True(EffectClassifier.Classify(MakeCard("Mana Prism", "Artifact",
            "{T}: Add {C}.\n{1}, {T}: Add one mana of any color."))
            .HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A Treasure is a Lotus Petal in token form: it always fixes, and it ramps
    // when you get several at once or over and over. One, once, is a rider —
    // the line already drawn for the Clue. Ruled 2026-09-18.
    [InlineData("Professional Wrestler", "Creature — Human Warrior",
        "When this creature enters, create a Treasure token.", false)]
    [InlineData("Gilded Ghoda", "Creature — Frog Mount",
        "Whenever this creature attacks while saddled, create a Treasure token.", true)]
    [InlineData("Unexpected Windfall", "Instant",
        "As an additional cost to cast this spell, discard a card.\n"
        + "Draw two cards and create two Treasure tokens.", true)]
    public void Classify_Treasure_AlwaysFixes_AndRampsWhenThereAreSeveral(
        string name, string typeLine, string oracle, bool ramps)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.Equal(ramps, result.HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A Treasure a token the card creates will make is the token's, ruled
    // 2026-09-29 on There and Back Again ("non crea Treasure ma li crea il token
    // che viene creato solo quando muore"): Smaug's fourteen Treasures are
    // Smaug's, and the Goblin Shaman's are the Goblin's.
    [InlineData("There and Back Again", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Up to "
        + "one target creature can't block for as long as you control this Saga. The Ring tempts you.\nII — "
        + "Search your library for a Mountain card, put it onto the battlefield, then shuffle.\nIII — Create "
        + "Smaug, a legendary 6/6 red Dragon creature token with flying, haste, and \"When Smaug dies, create "
        + "fourteen Treasure tokens.\"")]
    [InlineData("Fable of the Mirror-Breaker // Reflection of Kiki-Jiki", "Enchantment — Saga // Enchantment Creature — Goblin Shaman",
        "(As this Saga enters and after your draw step, add a lore counter.)\nI — Create a 2/2 red Goblin "
        + "Shaman creature token with \"Whenever this token attacks, create a Treasure token.\"\nII — You may "
        + "discard up to two cards. If you do, draw that many cards.\nIII — Exile this Saga, then return it to "
        + "the battlefield transformed under your control.\n{1}, {T}: Create a token that's a copy of another "
        + "target nonlegendary creature you control, except it has haste. Sacrifice it at the beginning of the "
        + "next end step.")]
    public void Classify_ATreasureATokenMakes_IsTheTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // …and it does not RAMP the card either: the Goblin Shaman's Treasure on
    // every attack was reading as a repeated maker. (There and Back Again is not
    // here: its Mountain put onto the battlefield is its own Ramp.)
    [InlineData("Fable of the Mirror-Breaker // Reflection of Kiki-Jiki", "Enchantment — Saga // Enchantment Creature — Goblin Shaman",
        "(As this Saga enters and after your draw step, add a lore counter.)\nI — Create a 2/2 red Goblin "
        + "Shaman creature token with \"Whenever this token attacks, create a Treasure token.\"\nII — You may "
        + "discard up to two cards. If you do, draw that many cards.\nIII — Exile this Saga, then return it to "
        + "the battlefield transformed under your control.\n{1}, {T}: Create a token that's a copy of another "
        + "target nonlegendary creature you control, except it has haste. Sacrifice it at the beginning of the "
        + "next end step.")]
    public void Classify_ATreasureATokenMakes_DoesNotRampTheCard(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Every LAND flavour of cycling fixes. "Basic landcycling" is printed on 125
    // cards, more than all five named types together, and was missed until now.
    [InlineData("Sylvan Reclamation", "Instant",
        "Exile up to two target artifacts and/or enchantments.\nBasic landcycling {2}")]
    [InlineData("Valley Rannet", "Creature — Beast", "Mountaincycling {2}, forestcycling {2}")]
    public void Classify_Landcycling_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // A colour filter: you feed it mana and it hands back a colour you did not
    // have. Ruled 2026-09-18, the mirror of the Ramp ruling — the ability that
    // adds no mana converts colour, and that IS the job.
    [InlineData("Farrelite Priest", "Creature — Human Cleric", "{1}: Add {W}.")]
    [InlineData("Fire Sprites", "Creature — Faerie", "Flying\n{G}, {T}: Add {R}.")]
    [InlineData("Agent of Stromgald", "Creature — Human Spellshaper", "{R}: Add {B}.")]
    [InlineData("Sea Scryer", "Creature — Merfolk Wizard", "{T}: Add {C}.\n{1}, {T}: Add {U}.")]
    [InlineData("Viridescent Bog", "Land", "{1}, {T}: Add {B}{G}.")]
    public void Classify_AColourFilter_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // …but paying a colour for MORE OF THE SAME colour filters nothing, and
    // paying for colourless fixes nothing either.
    [InlineData("Evendo, Waking Haven", "Land — Planet",
        "This land enters tapped.\n{T}: Add {G}.\n12+ | {G}, {T}: Add {G} for each creature you control.")]
    [InlineData("Sisay's Ring", "Artifact", "{T}: Add {C}{C}.")]
    public void Classify_MoreOfWhatYouPaid_IsNotAColourFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Cheat, ruled 2026-09-18: the permanent arrives without being cast.
    [InlineData("Sneak Attack", "Enchantment",
        "{R}: You may put a creature card from your hand onto the battlefield. That creature gains haste. "
        + "Sacrifice the creature at the beginning of the next end step.")]
    [InlineData("Elvish Piper", "Creature — Elf Shaman",
        "{G}, {T}: You may put a creature card from your hand onto the battlefield.")]
    [InlineData("Show and Tell", "Sorcery",
        "Each player may put an artifact, creature, enchantment, or land card from their hand onto the battlefield.")]
    [InlineData("Natural Order", "Sorcery",
        "As an additional cost to cast this spell, sacrifice a green creature.\n"
        + "Search your library for a green creature card, put it onto the battlefield, then shuffle.")]
    [InlineData("Omniscience", "Enchantment",
        "You may cast spells from your hand without paying their mana costs.")]
    public void Classify_APermanentThatArrivesWithoutBeingCast_IsCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Three edges of the same ruling. A LAND put onto the battlefield is Ramp —
    // that is the mana, not the cheat.
    [InlineData("Skyshroud Ranger", "Creature — Elf Scout",
        "{T}: You may put a land card from your hand onto the battlefield. Activate only as a sorcery.")]
    // A GRAVEYARD is Reanimate, which keeps its own tag.
    [InlineData("Reanimate", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control. "
        + "You lose life equal to that card's mana value.")]
    // And casting the cards THIS card just exiled is the impulse rider, which is
    // CardAdvantage. Ugin says "those cards … their mana costs", so the plural
    // alone does not separate it — the object phrase has to be read too.
    [InlineData("Ugin, Eye of the Storms", "Legendary Planeswalker — Ugin",
        "−11: Search your library for any number of colorless nonland cards, exile them, then shuffle. "
        + "Until end of turn, you may cast those cards without paying their mana costs.")]
    public void Classify_TheOtherWaysAPermanentArrives_AreNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Fact]
    public void Classify_TriassicEgg_IsBothCheatAndReanimate()
    {
        // The card that shows the two tags are one trick from two zones, and
        // why they are not merged: it offers you the choice.
        CardEffect result = EffectClassifier.Classify(MakeCard("Triassic Egg", "Artifact",
            "{3}, {T}: Put a hatchling counter on this artifact.\n"
            + "Sacrifice this artifact: Choose one. Activate only if there are two or more hatchling counters "
            + "on this artifact.\n• You may put a creature card from your hand onto the battlefield.\n"
            + "• Return target creature card from your graveyard to the battlefield."));

        Assert.True(result.HasFlag(CardEffect.Cheat));
        Assert.True(result.HasFlag(CardEffect.Reanimate));
    }

    [Fact]
    public void Classify_CyclingThatFetchesACreature_IsNotManaFixing()
    {
        // Slivercycling, wizardcycling and halflingcycling fetch a body, not a
        // colour. Note "islandcycling" contains "landcycling" with no word
        // boundary in front of it, which is why \b keeps the two apart.
        Assert.False(EffectClassifier.Classify(MakeCard("Homing Sliver", "Creature — Sliver",
            "Each Sliver card in each player's hand has slivercycling {3}.\nSlivercycling {3}"))
            .HasFlag(CardEffect.ManaFixing));
    }

    [Fact]
    public void Classify_DualLand_IsManaFixing_NotRamp()
    {
        // A land tapping for its own (fixing) mana must be ManaFixing but NOT Ramp.
        Card card = MakeCard("Tundra", "Land", "{T}: Add {W} or {U}.");

        CardEffect result = EffectClassifier.Classify(card);

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Protection counts only when it can be deployed in response. Mother of Runes
    // answers a removal spell on the stack; a bogle with hexproof printed on it
    // protects nobody but itself, and holds nothing up.
    [InlineData("Mother of Runes", "Creature — Human Cleric", "{T}: Target creature you control gains protection from the color of your choice until end of turn.", true)]
    [InlineData("Giant Growth", "Instant", "Target creature gets +3/+3 until end of turn. It gains hexproof until end of turn.", true)]
    // NB: a Circle of Protection would pass this gate (its "{1}:" is an activated
    // ability) but never reaches it — the rules' vocabulary has no word for
    // PREVENTION yet, only protection/hexproof/shroud/indestructible. Whether
    // prevention joins them is still open, and deciding it here by side effect
    // would also silently rule on every fog.
    [InlineData("Slippery Bogle", "Creature — Beast", "", false)]
    [InlineData("Darksteel Colossus", "Artifact Creature — Golem", "Trample\nThis creature is indestructible.", false)]
    [InlineData("Asceticism-style aura", "Enchantment — Aura", "Enchanted creature has hexproof.", false)]
    public void Classify_Protection_RequiresInstantSpeed(string name, string typeLine, string oracle, bool expected)
    {
        List<string>? keywords = name == "Slippery Bogle" ? ["Hexproof"] : null;
        Card card = MakeCard(name, typeLine, oracle, keywords);

        Assert.Equal(expected, EffectClassifier.Classify(card).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A creature that pumps or shields only ITSELF has a stat line, not an
    // effect — it answers nothing for the rest of the board. Decided 2026-09-05.
    [InlineData("Adanto Vanguard", "Creature — Vampire Soldier",
        "As long as this creature is attacking, it gets +2/+0.\nPay 4 life: This creature gains indestructible until end of turn.")]
    [InlineData("Advanced Hoverguard", "Creature — Drone",
        "Flying\n{U}: This creature gains shroud until end of turn.")]
    // Its own name is the same self-reference the older printings write.
    [InlineData("Akroma, Angel of Fury", "Legendary Creature — Angel",
        "This spell can't be countered.\nFlying, trample, protection from white and from blue\n{R}: Akroma gets +1/+0 until end of turn.")]
    [InlineData("Armored Armadillo", "Creature — Armadillo",
        "Ward {1} (Whenever this creature becomes the target of a spell or ability an opponent controls, counter it unless that player pays {1}.)")]
    // A condition ON what you control is not a beneficiary: the pump still lands
    // on the creature itself. This is the wording the stop-word list exists for.
    [InlineData("Aerial Engineer", "Artifact Creature — Construct",
        "As long as you control an artifact, this creature gets +2/+0 and has flying.")]
    public void Classify_SelfBuffAndSelfProtection_AreNotEffects(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Buff));
        Assert.False(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // The same creatures, aimed outward, keep the tag. A "target" two lines up
    // must not vouch for a self-buff, which is why the window is one line: the
    // Armored Guardian entry below grants on one line and shields itself on the
    // next, and only the first is why it counts.
    [InlineData("Aerie Mystics", "Creature — Bird Wizard",
        "Flying\n{1}{G}{U}: Creatures you control gain shroud until end of turn.", CardEffect.Protection)]
    [InlineData("Armored Guardian", "Creature — Giant",
        "{1}{W}{W}: Target creature you control gains protection from the color of your choice until end of turn.\n{1}{U}{U}: This creature gains shroud until end of turn.", CardEffect.Protection)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Bonesplitter", "Artifact — Equipment", "Equipped creature gets +2/+0.\nEquip {1}", CardEffect.Buff)]
    // The beneficiary is whatever type line the card names, not just "creatures",
    // and it can be a battlefield state rather than a controller.
    //
    // Adeliz used to be here as a Buff. It pumps WIZARDS, and the 2026-09-15
    // ruling took tribal lords out of the tag; it only kept passing because the
    // tribal rule wanted the phrase at the start of a line, and Adeliz puts it
    // after a trigger comma. Widening the clause openers on 2026-09-17 made the
    // classifier agree with the ruling, so the case moved to the tribal tests.
    [InlineData("Agrus Kos, Wojek Veteran", "Legendary Creature — Human Soldier",
        "Whenever Agrus Kos attacks, attacking red creatures get +2/+0 and attacking white creatures get +0/+2 until end of turn.", CardEffect.Buff)]
    public void Classify_EffectsAimedAtSomethingElse_Survive(string name, string typeLine, string oracle, CardEffect expected)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(expected));
    }

    [Fact]
    public void Classify_VanillaCreature_IsNone()
    {
        Card card = MakeCard("Grizzly Bears", "Creature — Bear", "");

        Assert.Equal(CardEffect.None, EffectClassifier.Classify(card));
    }

    [Theory]
    // Graveyard to HAND is Regrowth; graveyard to the BATTLEFIELD stays Reanimate.
    // This used to be a documented gap: the Reanimate rules never matched a return
    // to hand, so Regrowth itself came back untagged.
    [InlineData("Regrowth", "Sorcery", "Return target card from your graveyard to your hand.", CardEffect.Regrowth)]
    [InlineData("Raise Dead", "Sorcery", "Return target creature card from your graveyard to your hand.", CardEffect.Regrowth)]
    [InlineData("Animate Dead", "Enchantment — Aura", "Return target creature card from your graveyard to the battlefield under your control.", CardEffect.Reanimate)]
    public void Classify_GraveyardRecursion_SplitsByDestination(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

    [Theory]
    // Targeted damage answers a threat, so it is Removal as well as Burn. The
    // scaling ones matter most: "deals X damage" is how the whole pre-modern
    // library writes a kill spell.
    [InlineData("Blaze", "Sorcery", "Blaze deals X damage to any target.")]
    [InlineData("Broadside Barrage", "Instant", "Broadside Barrage deals 5 damage to target creature or planeswalker. Draw a card, then discard a card.")]
    [InlineData("Char", "Instant", "Char deals 4 damage to any target and 2 damage to you.")]
    // Fight is the same act with the damage delegated; an edict never says "target".
    [InlineData("Prey Upon", "Sorcery", "Target creature you control fights target creature you don't control.")]
    [InlineData("Diabolic Edict", "Instant", "Target player sacrifices a creature.")]
    // An edict aimed at DEFENDING PLAYER on every attack, tagged Removal by the
    // human on 2026-09-29.
    [InlineData("Witch-king, Bringer of Ruin", "Legendary Creature — Wraith Noble",
        "Flying\nWhenever Witch-king attacks, defending player sacrifices a creature with the least power "
        + "among creatures they control.")]
    public void Classify_DamageAndItsCousins_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // "Any target" is the one wording that is both at once, because it can be
    // pointed at a face or at a creature. Scaling and fixed amounts read the
    // same, which is why [\dX] is in the pattern. Disintegrate's CURRENT oracle
    // text says "any target" — the creature-only wording is the Alpha printing,
    // and using it here once pinned the wrong behaviour.
    [InlineData("Blaze", "Sorcery", "Blaze deals X damage to any target.")]
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.")]
    [InlineData("Disintegrate", "Sorcery",
        "Disintegrate deals X damage to any target. If it's a creature, it can't be regenerated this turn, and if it would die this turn, exile it instead.")]
    public void Classify_DamageAtAnyTarget_IsBothBurnAndRemoval(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Burn));
        Assert.True(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Damage that can only ever hit a creature answers a threat, and answering a
    // threat is Removal. Reading it as Burn too was worth 129 false positives.
    // A variable amount changes nothing: measured, "deals X damage to target
    // creature" is hand-tagged Burn on none of the six reviewed cards that say it.
    [InlineData("Explosive Shot", "Instant", "Explosive Shot deals 4 damage to target creature.")]
    [InlineData("Thunder Salvo", "Instant",
        "Thunder Salvo deals X damage to target creature, where X is 2 plus the number of other spells you've cast this turn.")]
    // A player named only as the controller of the creature hit is no face: the
    // human took Burn off Lothlórien Blade on 2026-09-29.
    [InlineData("Lothlórien Blade", "Artifact — Equipment",
        "Whenever equipped creature attacks, it deals damage equal to its power to target creature defending "
        + "player controls.\nEquip Elf {2}\nEquip {5}")]
    public void Classify_DamageAtACreature_IsRemovalNotBurn(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Burn));
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
    // Damage to YOURSELF is a price, not an effect — the same reading that keeps
    // a self-mill out of Mill and an additional cost out of Sacrifice.
    [InlineData("Ancient Tomb", "Land", "{T}: Add {C}{C}. This land deals 2 damage to you.")]
    [InlineData("City of Brass", "Land", "Whenever this land becomes tapped, it deals 1 damage to you.\n{T}: Add one mana of any color.")]
    [InlineData("Juzam Djinn", "Creature — Djinn", "At the beginning of your upkeep, this creature deals 1 damage to you.")]
    public void Classify_DamageToYourself_IsNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Seven rulings on 2026-09-20, each read off a family that had split in the
    // hand tags. Damage divided among bare "TARGETS" can pick a face; the word
    // order runs both ways and the rule could only read one of them; the old
    // punishers name a face as "that permanent's controller"; life loss counts
    // from TWO; "loses life equal to" is a burn when somebody else is losing it;
    // the upkeep tax is a burn however slowly it burns; and life paid to stop a
    // card is life lost.
    [InlineData("Rolling Thunder", "Sorcery",
        "Rolling Thunder deals X damage divided as you choose among any number of targets.")]
    [InlineData("Storm Seeker", "Instant",
        "Storm Seeker deals damage to target player equal to the number of cards in that player's hand.")]
    [InlineData("Psychic Venom", "Enchantment — Aura",
        "Enchant land\nWhenever enchanted land becomes tapped, this Aura deals 2 damage to that land's controller.")]
    [InlineData("Vein Ripper", "Creature — Vampire Horror",
        "Flying\nWard—Sacrifice a creature.\nWhenever a creature dies, target opponent loses 2 life and you gain 2 life.")]
    [InlineData("Death Watch", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, its controller loses life equal to its power and "
        + "you gain life equal to its toughness.")]
    [InlineData("Karma", "Enchantment",
        "At the beginning of each player's upkeep, this enchantment deals damage to that player equal to "
        + "the number of Swamps they control.")]
    [InlineData("Breathstealer's Crypt", "Enchantment",
        "If a player would draw a card, instead they draw a card and reveal it. If it's a creature card, "
        + "that player discards it unless they pay 3 life.")]
    public void Classify_TheSevenWaysACardBurnsAFace_AreBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Two more the same day. A creature with HASTE that is gone at the end of
    // the turn it arrived attacks once and then is not there any more, so what
    // it really did was put its power on a face — whether it dies (Ball
    // Lightning) or bounces (Viashino Sandstalker). And on a LAND-DESTROYER the
    // damage is a second effect worth counting, which settles the split this
    // family had: Orcish Mine was tagged and Icequake, printing the same
    // sentence, was not.
    [InlineData("Ball Lightning", "Creature — Elemental",
        "Trample\nHaste\nAt the beginning of the end step, sacrifice this creature.")]
    [InlineData("Viashino Sandstalker", "Creature — Viashino Warrior",
        "Haste\nAt the beginning of the end step, return this creature to its owner's hand.")]
    [InlineData("Icequake", "Sorcery",
        "Destroy target land. If that land was a snow land, Icequake deals 1 damage to that land's controller.")]
    public void Classify_AShotWearingLegsOrALand_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Fact]
    // …and the drawback handed to SOMEBODY ELSE'S creature is not the card
    // shooting anything. Strago and Relm, Skirk Alarmist and Apprentice
    // Necromancer all print those words inside quotes, about a creature they
    // just gave you.
    public void Classify_AnEndOfTurnDrawbackGrantedAway_IsNotBurn()
    {
        Card card = MakeCard("Strago and Relm", "Legendary Creature — Human Wizard",
            "Sketch and Lore — {2}{R}, {T}: Target opponent exiles cards from the top of their library "
            + "until they exile an instant, sorcery, or creature card. You may cast that card without "
            + "paying its mana cost. If you cast a creature spell this way, it gains haste and \"At the "
            + "beginning of the end step, sacrifice this creature.\" Activate only as a sorcery.");

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // And the seven edges each of those rulings has to leave alone. Damage
    // divided among "target CREATURES" can never reach a player; Fiery Justice
    // hands the five life straight back, which the human ruled an exception on
    // purpose; ONE point of life ONCE is a rider (a repeated one is Burn since
    // 2026-09-29, see Classify_ARepeatedOnePointDrain_IsBurn, where Sanguine
    // Syphoner moved); "YOU lose life equal to" is the price of the card; a
    // charge attached to a kill is a removal spell with a bonus; and what a TOKEN
    // does is the token's.
    [InlineData("Pyrokinesis", "Instant",
        "You may exile a red card from your hand rather than pay this spell's mana cost.\n"
        + "Pyrokinesis deals 4 damage divided as you choose among any number of target creatures.")]
    [InlineData("Fiery Justice", "Sorcery",
        "Fiery Justice deals 5 damage divided as you choose among any number of targets. "
        + "Target opponent gains 5 life.")]
    [InlineData("Reanimate", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control. "
        + "You lose life equal to that card's mana value.")]
    [InlineData("Reign of Terror", "Sorcery",
        "Destroy all green creatures or all white creatures. They can't be regenerated. "
        + "You lose 2 life for each creature that died this way.")]
    [InlineData("Detonate", "Sorcery",
        "Destroy target artifact with mana value X. It can't be regenerated. "
        + "Detonate deals X damage to that artifact's controller.")]
    [InlineData("Mysidian Elder", "Creature — Human Wizard",
        "When this creature enters, create a 0/1 black Wizard creature token with \"Whenever you cast a "
        + "noncreature spell, this token deals 1 damage to each opponent.\"")]
    // One point of life drained ONCE is still a rider (a spell's mode), and a
    // drain printed inside the quotes of a token the card creates is the token's
    // (Keimi), read with the repeated drain on 2026-09-29.
    [InlineData("Ebony Charm", "Instant",
        "Choose one —\n• Target opponent loses 1 life and you gain 1 life.\n• Exile up to three target cards "
        + "from a single graveyard.\n• Target creature gains fear until end of turn. (It can't be blocked except "
        + "by artifact creatures and/or black creatures.)")]
    [InlineData("Tatsunari, Toad Rider", "Legendary Creature — Human Ninja",
        "Whenever you cast an enchantment spell, if you don't control a creature named Keimi, create Keimi, a "
        + "legendary 3/3 black and green Frog creature token with \"Whenever you cast an enchantment spell, each "
        + "opponent loses 1 life and you gain 1 life.\"\n{1}{G/U}: Tatsunari and target Frog you control can't be "
        + "blocked this turn except by creatures with flying or reach.")]
    public void Classify_WhatOnlyLooksLikeBurn_IsNot(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // ONE point of life that comes AGAIN AND AGAIN is Burn, revised 2026-09-29
    // ("ripetuto sì, singolo no") from the 09-20 threshold of two: a trigger, an
    // activation, or a keyword whose reminder says so (extort).
    [InlineData("Sanguine Syphoner", "Creature — Vampire Warlock",
        "Whenever this creature attacks, each opponent loses 1 life and you gain 1 life.")]
    [InlineData("Vengeful Bloodwitch", "Creature — Vampire Warlock",
        "Whenever this creature or another creature you control dies, target opponent loses 1 life and you "
        + "gain 1 life.")]
    [InlineData("Mirkwood Bats", "Creature — Bat",
        "Flying\nWhenever you create or sacrifice a token, each opponent loses 1 life.")]
    [InlineData("Blood Hustler", "Creature — Vampire Rogue",
        "Whenever you commit a crime, put a +1/+1 counter on this creature. This ability triggers only once "
        + "each turn. (Targeting opponents, anything they control, and/or cards in their graveyards is a "
        + "crime.)\n{3}{B}: Target opponent loses 1 life and you gain 1 life.")]
    [InlineData("Syndicate Heavy", "Creature — Giant Rogue",
        "Extort (Whenever you cast a spell, you may pay {W/B}. If you do, each opponent loses 1 life and you "
        + "gain that much life.)\nAt the beginning of each end step, if you gained 4 or more life this turn, "
        + "investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    public void Classify_ARepeatedOnePointDrain_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Sacrifice means an OUTLET for your own creatures — the half of the combo
    // that makes a stolen creature worth taking. An activation cost in front of a
    // colon is the canonical shape; a trigger that OFFERS the sacrifice counts too.
    [InlineData("Ashnod's Altar", "Artifact", "Sacrifice a creature: Add {C}{C}.")]
    [InlineData("Goblin Bombardment", "Enchantment", "Sacrifice a creature: This enchantment deals 1 damage to any target.")]
    [InlineData("Comet Crawler", "Creature — Beast",
        "Lifelink\nWhenever this creature attacks, you may sacrifice another creature or artifact. If you do, this creature gets +2/+0 until end of turn.")]
    // An artifact outlet, or one that names the fuel as a permanent, is the same
    // engine with something else going in.
    [InlineData("Atog", "Creature — Atog", "Sacrifice an artifact: This creature gets +2/+2 until end of turn.")]
    [InlineData("Infernal Tribute", "Enchantment", "{2}, Sacrifice a nontoken permanent: Draw a card.")]
    [InlineData("Dwarven Weaponsmith", "Creature — Dwarf",
        "{T}, Sacrifice an artifact: Put a +1/+1 counter on target creature. Activate only during your upkeep.")]
    // Ruled 2026-09-25: only a REPEATABLE outlet, fed creatures or artifacts.
    // An artifact outlet the human had left untagged…
    [InlineData("Orcish Mechanics", "Creature — Orc",
        "{T}, Sacrifice an artifact: This creature deals 2 damage to any target.")]
    // …a sacrifice every upkeep, forced or usable only then…
    [InlineData("Lord of the Pit", "Creature — Demon",
        "Flying, trample\nAt the beginning of your upkeep, sacrifice a creature other than this creature. If "
        + "you can't, this creature deals 7 damage to you.")]
    [InlineData("Marjhan", "Creature — Serpent",
        "This creature doesn't untap during your untap step.\n{U}{U}, Sacrifice a creature: Untap this "
        + "creature. Activate only during your upkeep.\nThis creature can't attack unless defending player "
        + "controls an Island.\n{U}{U}: This creature gets -1/-0 until end of turn and deals 1 damage to target "
        + "attacking creature without flying.\nWhen you control no Islands, sacrifice this creature.")]
    // …a card cast again for it, by buyback or from the graveyard…
    [InlineData("Worthy Cause", "Instant",
        "Buyback {2} (You may pay an additional {2} as you cast this spell. If you do, put this card into "
        + "your hand as it resolves.)\nAs an additional cost to cast this spell, sacrifice a creature.\nYou gain "
        + "life equal to the sacrificed creature's toughness.")]
    [InlineData("Wickerfolk Indomitable", "Artifact Creature — Scarecrow",
        "You may cast this card from your graveyard by paying 2 life and sacrificing an artifact or creature "
        + "in addition to paying its other costs.")]
    // …an equip cost, "any number" on every attack, a keyword handed on, a
    // loyalty ability, and an activated ability whose EFFECT sacrifices…
    [InlineData("Dissection Tools", "Artifact — Equipment",
        "When this Equipment enters, manifest dread, then attach this Equipment to that creature.\nEquipped "
        + "creature gets +2/+2 and has deathtouch and lifelink.\nEquip—Sacrifice a creature.")]
    [InlineData("Kylox, Visionary Inventor", "Legendary Creature — Lizard Artificer",
        "Menace, ward {2}, haste\nWhenever Kylox attacks, sacrifice any number of other creatures, then exile "
        + "the top X cards of your library, where X is their total power. You may cast any number of instant "
        + "and/or sorcery spells from among the exiled cards without paying their mana costs.")]
    [InlineData("Colonel Autumn", "Legendary Creature — Human Soldier",
        "Lifelink\nExploit (When this creature enters, you may sacrifice a creature.)\nOther legendary "
        + "creatures you control have exploit.\nWhenever a creature you control exploits a creature, put a +1/+1 "
        + "counter on each creature you control.")]
    [InlineData("Chandra, Spark Hunter", "Legendary Planeswalker — Chandra",
        "At the beginning of combat on your turn, choose up to one target Vehicle you control. Until end of "
        + "turn, it becomes an artifact creature and gains haste.\n+2: You may sacrifice an artifact or discard "
        + "a card. If you do, draw a card.\n0: Create a 3/2 colorless Vehicle artifact token with crew 1.\n−7: "
        + "You get an emblem with \"Whenever an artifact you control enters, this emblem deals 3 damage to any "
        + "target.\"")]
    [InlineData("Joo Dee, One of Many", "Creature — Human Advisor",
        "{B}, {T}: Surveil 1. Create a token that's a copy of this creature, then sacrifice an artifact or "
        + "creature. Activate only as a sorcery. (To surveil 1, look at the top card of your library. You may "
        + "put it into your graveyard.)")]
    // "One or more" is a count like "two" — the base pattern once missed it.
    [InlineData("Radiant Lotus", "Artifact",
        "{T}, Sacrifice one or more artifacts: Choose a color. Target player adds three mana of the chosen "
        + "color for each artifact sacrificed this way. (Activate only as an instant.)")]
    // …and destroying or exiling your own at will, an outlet by another verb.
    [InlineData("Despotic Scepter", "Artifact",
        "{T}: Destroy target permanent you own. It can't be regenerated.")]
    [InlineData("Rats of Rath", "Creature — Rat",
        "{B}: Destroy target artifact, creature, or land you control.")]
    [InlineData("City of Shadows", "Land",
        "{T}, Exile a creature you control: Put a storage counter on this land.\n{T}: Add {C} for each storage "
        + "counter on this land.")]
    public void Classify_SacrificeOutlet_IsSacrifice(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
    }

    [Theory]
    // Three things that are not an outlet: an edict empties somebody ELSE's
    // board, an additional cost to cast is paid once on the way to a different
    // effect, and a land is not a creature.
    [InlineData("Diabolic Edict", "Instant", "Target player sacrifices a creature of their choice.")]
    [InlineData("Natural Order", "Sorcery",
        "As an additional cost to cast this spell, sacrifice a green creature.\nSearch your library for a green creature card, put it onto the battlefield, then shuffle.")]
    [InlineData("Harrow", "Instant",
        "As an additional cost to cast this spell, sacrifice a land.\nSearch your library for up to two basic land cards, put them onto the battlefield, then shuffle.")]
    // Ruled 2026-09-25: a sacrifice made ONCE is not an outlet, however it is
    // worded — entering, "rather than pay", kicker, exploit, a spell, "any
    // number" at a time.
    [InlineData("Boilerbilges Ripper", "Creature — Human Assassin",
        "When this creature enters, you may sacrifice another creature or enchantment. When you do, this "
        + "creature deals 2 damage to any target.")]
    [InlineData("Flare of Denial", "Instant",
        "You may sacrifice a nontoken blue creature rather than pay this spell's mana cost.\nCounter target "
        + "spell.")]
    [InlineData("Vayne's Treachery", "Instant",
        "Kicker—Sacrifice an artifact or creature. (You may sacrifice an artifact or creature in addition to "
        + "any other costs as you cast this spell.)\nTarget creature gets -2/-2 until end of turn. If this spell "
        + "was kicked, that creature gets -6/-6 until end of turn instead.")]
    [InlineData("Infernal Captor", "Creature — Devil Rogue",
        "Exploit (When this creature enters, you may sacrifice a creature.)\nWhen this creature exploits a "
        + "creature, gain control of target artifact or creature until end of turn. Untap that permanent. It "
        + "gains haste until end of turn.")]
    [InlineData("Tip the Scales", "Sorcery",
        "Sacrifice a creature. When you do, all creatures get -X/-X until end of turn, where X is the "
        + "sacrificed creature's toughness.")]
    [InlineData("Angelic Aberration", "Creature — Eldrazi Angel",
        "Devoid (This card has no color.)\nFlying, vigilance\nWhen this creature enters, sacrifice any number "
        + "of creatures each with base power or toughness 1 or less. Create that many 4/4 colorless Eldrazi "
        + "Angel creature tokens with flying and vigilance.")]
    // Emerge's reminder reads "cast this spell by sacrificing a creature", the
    // shape Wickerfolk Indomitable repeats — but it is paid once, as it is cast.
    [InlineData("Wretched Gryff", "Creature — Eldrazi Hippogriff",
        "Emerge {5}{U} (You may cast this spell by sacrificing a creature and paying the emerge cost reduced "
        + "by that creature's mana value.)\nWhen you cast this spell, draw a card.\nFlying")]
    // Not a LAND (Zuran Orb, corrected by name), not an artifact TOKEN, which
    // cashes in the card's own Clues, and not an edict inside a trigger.
    [InlineData("Zuran Orb", "Artifact",
        "Sacrifice a land: You gain 2 life.")]
    [InlineData("Sophia, Dogged Detective", "Legendary Creature — Human Detective",
        "When Sophia enters, create Tiny, a legendary 2/2 green Dog Detective creature token with "
        + "trample.\n{1}, Sacrifice an artifact token: Put a +1/+1 counter on each Dog you control.\nWhenever a "
        + "Dog you control deals combat damage to a player, create a Food token, then investigate.")]
    [InlineData("Grave Pact", "Enchantment",
        "Whenever a creature you control dies, each other player sacrifices a creature of their choice.")]
    // A trigger the OPPONENT fires is a price, not an outlet: ruled 2026-09-25,
    // "un effetto extra non è a comando".
    [InlineData("Oath of Lim-Dûl", "Enchantment",
        "Whenever you lose life, for each 1 life you lost, sacrifice a permanent other than this enchantment "
        + "unless you discard a card. (Damage dealt to you causes you to lose life.)\n{B}{B}: Draw a card.")]
    // Damage is the same price as lost life (Phyrexian Negator, named by the
    // human the same day). Both are held out today because "sacrifice THAT
    // MANY" is not read at all; teach the base pattern that count and the
    // trigger guard in SacrificeOutlet has to learn "is dealt damage" too.
    [InlineData("Phyrexian Negator", "Creature — Phyrexian Horror",
        "Trample\nWhenever this creature is dealt damage, sacrifice that many permanents.")]
    [InlineData("Lich", "Enchantment",
        "As this enchantment enters, you lose life equal to your life total.\nYou don't lose the game for "
        + "having 0 or less life.\nIf you would gain life, draw that many cards instead.\nWhenever you're dealt "
        + "damage, sacrifice that many nontoken permanents. If you can't, you lose the game.\nWhen this "
        + "enchantment is put into a graveyard from the battlefield, you lose the game.")]
    // Destroying an Aura on your creature saves it, and a harness is paid once.
    [InlineData("Miracle Worker", "Creature — Human Cleric",
        "{T}: Destroy target Aura attached to a creature you control.")]
    [InlineData("The Soul Stone", "Legendary Artifact — Infinity Stone",
        "Indestructible\n{T}: Add {B}.\n{6}{B}, {T}, Exile a creature you control: Harness The Soul Stone. "
        + "(Once harnessed, its ∞ ability is active.)\n∞ — At the beginning of your upkeep, return target "
        + "creature card from your graveyard to the battlefield.")]
    public void Classify_SacrificeThatIsNotAnOutlet_IsNotSacrifice(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
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

    [Theory]
    // "This artifact doesn't untap" is a tax the card pays itself. Neutralising
    // nobody is not Pacify — the same self-versus-other reading as Buff.
    [InlineData("Basalt Monolith", "Artifact", "This artifact doesn't untap during your untap step.\n{T}: Add {C}{C}{C}.\n{3}: Untap this artifact.")]
    [InlineData("Mana Vault", "Artifact", "This artifact doesn't untap during your untap step.\nAt the beginning of your upkeep, you may pay {4}. If you do, untap this artifact.")]
    [InlineData("Time Vault", "Artifact", "This artifact enters tapped.\nThis artifact doesn't untap during your untap step.")]
    public void Classify_SelfUntapTax_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // A lock put on somebody else keeps the tag, whichever wording carries it.
    [InlineData("Icy Manipulator", "Artifact", "{1}, {T}: Tap target artifact, creature, or land.")]
    [InlineData("Frozen Solid", "Enchantment — Aura", "Enchant creature\nEnchanted creature doesn't untap during its controller's untap step.")]
    [InlineData("Kismet", "Enchantment", "Artifacts, creatures, and lands your opponents control enter tapped.\nEnchanted creature can't attack or block.")]
    public void Classify_LockOnSomebodyElse_StaysPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Two or more cards, or a draw you can come back to.
    [InlineData("Divination", "Sorcery", "Draw two cards.")]
    [InlineData("Ancestral Recall", "Instant", "Target player draws three cards.")]
    // The user's own example: an activated draw on a body is card advantage
    // because nothing stops you doing it again.
    [InlineData("Azure Drake", "Creature — Drake", "Flying\n{3}{U}: Draw a card.")]
    [InlineData("Ophidian", "Creature — Serpent", "Whenever this creature deals combat damage to a player, you may draw a card.")]
    [InlineData("Howling Mine", "Artifact", "At the beginning of each player's draw step, that player draws a card.")]
    public void Classify_RepeatableOrMultipleDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // One card off a spell you cast replaces the spell: parity, not advantage.
    [InlineData("Eject", "Instant", "This spell can't be countered.\nReturn target nonland permanent to its owner's hand.\nDraw a card.")]
    [InlineData("Airbending Lesson", "Sorcery", "Airbend target nonland permanent.\nDraw a card.")]
    // An enters-the-battlefield draw fires once, so it is the same one-shot
    // replacement as a cantrip — "whenever" would be a different matter.
    [InlineData("Wall of Omens", "Creature — Wall", "Defender\nWhen this creature enters, draw a card.")]
    public void Classify_SingleOneShotDraw_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

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
    // An Aura attached to a named thing is about that thing.
    [InlineData("Savaen Elves", "Creature — Elf", "{G}{G}, {T}: Destroy target Aura attached to a land.")]
    [InlineData("Miracle Worker", "Creature — Human Cleric",
        "{T}: Destroy target Aura attached to a creature you control.")]
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
    // Steal, ruled 2026-09-19. Handing a permanent to somebody ELSE is the mirror
    // image of this tag, and was 12 of its 14 false positives.
    [InlineData("Jinxed Idol", "Artifact",
        "At the beginning of your upkeep, this artifact deals 2 damage to you.\n"
        + "Sacrifice a creature: Target opponent gains control of this artifact.")]
    [InlineData("Rainbow Vale", "Land",
        "{T}: Add one mana of any color. An opponent gains control of this land at the beginning of "
        + "the next end step.")]
    [InlineData("Guardian Beast", "Creature — Beast",
        "As long as this creature is untapped, noncreature artifacts you control can't be enchanted, "
        + "they have indestructible, and other players can't gain control of them. This effect doesn't "
        + "remove Auras already attached to those artifacts.")]
    [InlineData("Emberwilde Djinn", "Creature — Djinn",
        "Flying\nAt the beginning of each player's upkeep, that player may pay {R}{R} or 2 life. "
        + "If the player does, they gain control of this creature.")]
    public void Classify_GivingAPermanentAway_IsNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // The veto above reads the subject IMMEDIATELY in front of the verb. These two
    // put the victim in front of a verb that is yours — "untap target creature AN
    // OPPONENT CONTROLS and gain control of it" — and any filler at all loses them.
    [InlineData("Ray of Command", "Instant",
        "Untap target creature an opponent controls and gain control of it until end of turn. "
        + "That creature gains haste until end of turn. When you lose control of the creature, tap it.")]
    [InlineData("Magus of the Unseen", "Creature — Human Wizard",
        "{1}{U}, {T}: Untap target artifact an opponent controls and gain control of it until end of "
        + "turn. It gains haste until end of turn. When you lose control of the artifact, tap it.")]
    // An exchange is a theft you paid for.
    [InlineData("Political Trickery", "Sorcery",
        "Exchange control of target land you control and target land an opponent controls. "
        + "(This effect lasts indefinitely.)")]
    [InlineData("Legerdemain", "Sorcery",
        "Exchange control of target artifact or creature and another target permanent that shares one "
        + "of those types with it. (This effect lasts indefinitely.)")]
    // Taking a CARD out of somebody else's library, hand or graveyard and playing
    // it is a theft of a card rather than of a permanent.
    [InlineData("Outrageous Robbery", "Sorcery",
        "Target opponent exiles the top X cards of their library face down. You may look at and play "
        + "those cards for as long as they remain exiled. If you cast a spell this way, you may spend "
        + "mana as though it were mana of any type to cast it.")]
    [InlineData("Laughing Jasper Flint", "Legendary Creature — Goblin Mercenary",
        "Creatures you control but don't own are Mercenaries in addition to their other types.\n"
        + "At the beginning of your upkeep, exile the top X cards of target opponent's library, where "
        + "X is the number of outlaws you control. Until end of turn, you may cast spells from among "
        + "those cards, and mana of any type can be spent to cast those spells.")]
    [InlineData("Vaan, Street Thief", "Legendary Creature — Human Rogue",
        "Whenever one or more Scouts, Pirates, and/or Rogues you control deal combat damage to a "
        + "player, exile the top card of that player's library. You may cast it. If you don't, create "
        + "a Treasure token.")]
    // Word of Command takes the player rather than the permanent.
    [InlineData("Word of Command", "Sorcery",
        "Look at target opponent's hand and choose a card from it. You control that player until Word "
        + "of Command finishes resolving. The player plays that card if able.")]
    // Reanimating out of an OPPONENT'S graveyard is both Reanimate and this,
    // ruled 2026-09-19.
    [InlineData("Ashen Powder", "Sorcery",
        "Put target creature card from an opponent's graveyard onto the battlefield under your control.")]
    [InlineData("Bone Dancer", "Creature — Zombie",
        "Whenever this creature attacks and isn't blocked, you may put the top creature card of "
        + "defending player's graveyard onto the battlefield under your control. If you do, this "
        + "creature assigns no combat damage this turn.")]
    // "TAPPED" inside the phrase: an Oracle update in September 2026 rewrote
    // Geth from "under your control tapped" to this, and the card quietly lost
    // the tag.
    [InlineData("Geth, Lord of the Vault", "Legendary Creature — Phyrexian Zombie",
        "Intimidate (This creature can't be blocked except by artifact creatures and/or creatures that share a "
        + "color with it.)\n{X}{B}: Put target artifact or creature card with mana value X from an opponent's "
        + "graveyard onto the battlefield tapped under your control. Then that player mills X cards.")]
    // And the life total is a thing you can take.
    [InlineData("Mirror Universe", "Artifact",
        "{T}, Sacrifice this artifact: Exchange life totals with target opponent. "
        + "Activate only during your upkeep.")]
    // "THEY may play" against a NAMED zone: the creature's controller takes a card
    // out of the opponent's library, so the two are different people.
    [InlineData("Gonti, Night Minister", "Legendary Creature — Phyrexian Rogue",
        "Whenever a player casts a spell they don't own, that player creates a Treasure token.\n"
        + "Whenever a creature deals combat damage to one of your opponents, its controller looks at "
        + "the top card of that opponent's library and exiles it face down. They may play that card "
        + "for as long as it remains exiled. Mana of any type can be spent to cast a spell this way.")]
    // A mill IS the zone — Locke never names a library at all.
    [InlineData("Locke, Treasure Hunter", "Legendary Creature — Human Rogue",
        "Locke can't be blocked by creatures with greater power.\n"
        + "Mug — Whenever Locke attacks, each player mills a card. If a land card was milled this "
        + "way, create a Treasure token. Until end of turn, you may cast a spell from among those cards.")]
    // Moving somebody else's Aura is theft of its job if not of its control.
    [InlineData("Enchantment Alteration", "Instant",
        "Attach target Aura attached to a creature or land to another permanent of that type.")]
    [InlineData("Crown of the Ages", "Artifact",
        "{4}, {T}: Attach target Aura attached to a creature to another creature.")]
    public void Classify_TakingWhatIsSomebodyElses_IsSteal(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // A symmetric exile hands everyone their own cards back, and playing YOUR OWN
    // is not theft however much of the opponent's library the text mentions.
    [InlineData("Triple Triad", "Enchantment",
        "At the beginning of your upkeep, each player exiles the top card of their library. Until end "
        + "of turn, you may play the card you own exiled this way and each other card exiled this way "
        + "with lesser mana value than it without paying their mana costs.")]
    [InlineData("Wheel of Potential", "Sorcery",
        "You get {E}{E}{E} (three energy counters), then you may pay any amount of {E}.\n"
        + "Each player may exile their hand and draw a number of cards equal to the amount of {E} paid "
        + "this way. If seven or more {E} was paid this way, you may play cards you own exiled this "
        + "way until the end of your next turn.")]
    // Ordinary reanimation belongs to nobody in particular: "from A graveyard"
    // names no victim, so it stays Reanimate alone.
    [InlineData("Hymn of Rebirth", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control.")]
    [InlineData("Coffin Queen", "Creature — Zombie",
        "You may choose not to untap this creature during your untap step.\n"
        + "{2}{B}, {T}: Put target creature card from a graveyard onto the battlefield under your "
        + "control. When this creature becomes tapped or you lose control of it, exile that card.")]
    // "THEY may cast" against a PRONOUN zone is one player doing both halves: the
    // opponent digs through their own library and casts what they find.
    [InlineData("Transforming Flourish", "Instant",
        "Demonstrate (When you cast this spell, you may copy it.)\n"
        + "Destroy target artifact or creature you don't control. If that permanent is destroyed this "
        + "way, its controller exiles cards from the top of their library until they exile a nonland "
        + "card, then they may cast that card without paying its mana cost.")]
    // One half mills a player, the other plays lands out of YOUR graveyard; only a
    // rule reading the whole card at once could join them into a theft.
    [InlineData("Glacierwood Siege", "Enchantment",
        "As this enchantment enters, choose Temur or Sultai.\n"
        + "• Temur — Whenever you cast an instant or sorcery spell, target player mills four cards.\n"
        + "• Sultai — You may play lands from your graveyard.")]
    // A token copy of their creature is a body you made, not a card you took.
    [InlineData("Echo Chamber", "Artifact",
        "{4}, {T}: An opponent chooses target creature they control. Create a token that's a copy of "
        + "that creature. That token gains haste until end of turn. Exile the token at the beginning "
        + "of the next end step. Activate only as a sorcery.")]
    // And a card pulled out of "that graveyard" at random names no victim.
    [InlineData("Mysterious Stranger", "Creature — Human",
        "Flash\nWhen this creature enters, for each graveyard with an instant or sorcery card in it, "
        + "exile target instant or sorcery card from that graveyard. If two or more cards are exiled "
        + "this way, choose one of them at random and copy it. You may cast the copy without paying "
        + "its mana cost.")]
    public void Classify_WhatIsAlreadyYours_IsNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
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
    // And a rebalance that destroys nothing and hands basics back is not denial.
    [InlineData("Natural Balance", "Sorcery",
        "Each player who controls six or more lands chooses five lands they control and sacrifices "
        + "the rest. Each player who controls four or fewer lands may search their library for up to "
        + "X basic land cards and put them onto the battlefield, where X is five minus the number of "
        + "lands they control. Then each player who searched their library this way shuffles.")]
    public void Classify_WhatOnlyReadsLikeLandDestruction_IsNot(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle))
            .HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Tutor, widened 2026-09-19. The game names what it fetches in every way there
    // is, and the old rule knew only "a card" and five card types.
    [InlineData("Cloud, Midgar Mercenary", "Legendary Creature — Human Soldier",
        "When Cloud enters, search your library for an Equipment card, reveal it, put it into your "
        + "hand, then shuffle.")]
    [InlineData("Demonic Counsel", "Sorcery",
        "Search your library for a Demon card, reveal it, put it into your hand, then shuffle.")]
    [InlineData("Intuition", "Instant",
        "Search your library for three cards and reveal them. Target opponent chooses one. Put that "
        + "card into your hand and the rest into your graveyard. Then shuffle.")]
    [InlineData("Brightglass Gearhulk", "Artifact Creature — Construct",
        "First strike, trample\nWhen this creature enters, you may search your library for up to two "
        + "artifact, creature, and/or enchantment cards with mana value 1 or less, reveal them, put "
        + "them into your hand, then shuffle.")]
    [InlineData("Delivery Moogle", "Creature — Moogle",
        "Flying\nWhen this creature enters, search your library and/or graveyard for an artifact card "
        + "with mana value 2 or less, reveal it, and put it into your hand. If you search your library "
        + "this way, shuffle.")]
    // A land on one side of the "or" does not make the whole search a land search.
    [InlineData("Starfield Shepherd", "Creature — Bird Cleric",
        "Flying\nWhen this creature enters, search your library for a basic Plains card or a creature "
        + "card with mana value 1 or less, reveal it, put it into your hand, then shuffle.")]
    // Somebody else's own search of their own library still counts.
    [InlineData("Noble Benefactor", "Creature — Human",
        "When this creature dies, each player may search their library for a card and put that card "
        + "into their hand. Then each player who searched their library this way shuffles.")]
    // Naming the card you want is the purest form of this tag, ruled 2026-09-19
    // — whether it finds another copy of itself or somebody else's payoff.
    [InlineData("Llanowar Sentinel", "Creature — Elf",
        "When this creature enters, you may pay {1}{G}. If you do, search your library for a card "
        + "named Llanowar Sentinel, put that card onto the battlefield, then shuffle.")]
    [InlineData("Tower Winder", "Creature — Snake",
        "Reach, deathtouch\nWhen this creature enters, search your library and/or graveyard for a "
        + "card named Command Tower, reveal it, and put it into your hand. If you search your library "
        + "this way, shuffle.")]
    // Demonic Consultation never searches — it names a card and digs for the NAME.
    [InlineData("Demonic Consultation", "Instant",
        "Choose a card name. Exile the top six cards of your library, then reveal cards from the top "
        + "of your library until you reveal a card with the chosen name. Put that card into your hand "
        + "and exile all other cards revealed this way.")]
    // The zones listed with commas, "your library, graveyard, and/or outside the
    // game": Invasion of Arcavios, tagged Tutor by the human on 2026-09-29.
    [InlineData("Invasion of Arcavios // Invocation of the Founders", "Battle — Siege // Enchantment",
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's "
        + "defeated, exile it, then cast it transformed.)\nWhen this Siege enters, search your library, "
        + "graveyard, and/or outside the game for an instant or sorcery card you own, reveal it, and put it "
        + "into your hand. If you search your library this way, shuffle.\nWhenever you cast an instant or "
        + "sorcery spell from your hand, you may copy that spell. You may choose new targets for the copy.")]
    public void Classify_FetchingANamedCardOutOfYourLibrary_IsTutor(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }

    [Theory]
    // A land search is Ramp or ManaFixing, the ruling this tag was written around.
    [InlineData("Rampant Growth", "Sorcery",
        "Search your library for a basic land card, put that card onto the battlefield tapped, "
        + "then shuffle.")]
    // Digging until a TYPE turns up hands you whichever one was nearest the top,
    // which is not choosing a card.
    [InlineData("The Regalia", "Artifact — Vehicle",
        "Haste\nWhenever The Regalia attacks, reveal cards from the top of your library until you "
        + "reveal a land card. Put that card onto the battlefield tapped and the rest on the bottom "
        + "of your library in a random order.\nCrew 1")]
    [InlineData("Yuna's Whistle", "Sorcery",
        "Reveal cards from the top of your library until you reveal a creature card. Put that card "
        + "into your hand and the rest on the bottom of your library in a random order.")]
    public void Classify_ASearchThatChoosesNothing_IsNotTutor(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }

    [Theory]
    // Redirect, widened 2026-09-19 for the possessive: Meddle says "change THAT
    // SPELL'S TARGET" where Deflection says "change the target of".
    [InlineData("Meddle", "Instant",
        "If target spell has only one target and that target is a creature, change that spell's "
        + "target to another creature.")]
    [InlineData("Reflecting Mirror", "Artifact",
        "{X}, {T}: Change the target of target spell with a single target if that target is you. "
        + "The new target must be a player. X is twice the mana value of that spell.")]
    [InlineData("Twincast", "Instant", "Copy target instant or sorcery spell. You may choose new targets "
        + "for the copy.")]
    // Widened 2026-09-28: new targets chosen outright, and a spell taken over.
    [InlineData("Boltbender", "Creature — Goblin Wizard",
        "Disguise {1}{R} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it "
        + "face up any time for its disguise cost.)\nWhen this creature is turned face up, you may choose new "
        + "targets for any number of other spells and/or abilities.")]
    [InlineData("Invert Polarity", "Instant",
        "Choose target spell, then flip a coin. If you win the flip, gain control of that spell and you may "
        + "choose new targets for it. If you lose the flip, counter that spell.")]
    public void Classify_ActingOnASpellWithoutCounteringIt_IsRedirect(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }

    [Fact]
    public void Classify_TakingControlOfASpell_IsNotSteal()
    {
        // A spell is not a permanent; taking one over is Redirect's (2026-09-28).
        CardEffect result = EffectClassifier.Classify(MakeCard("Invert Polarity", "Instant",
            "Choose target spell, then flip a coin. If you win the flip, gain control of that spell and you may "
            + "choose new targets for it. If you lose the flip, counter that spell."));

        Assert.False(result.HasFlag(CardEffect.Steal));
    }

    [Theory]
    // Copying a spell of YOUR OWN is Redirect, ruled 2026-09-28. This OVERTURNS
    // the ruling of 2026-09-19, which took the delayed copy off all eight cards
    // then reviewed: every such card reviewed since 09-23 had been tagged, and
    // Ether and Jeong Jeong — pinned as NOT Redirect by this test until that day —
    // are pinned here as Redirect instead. Taigam copies "your second spell"
    // without offering new targets, and GIVING your spells storm or replicate is
    // copying them, read through the reminder (Crackling Spellslinger, Djinn
    // Illuminatus).
    [InlineData("Ether", "Artifact",
        "{T}, Exile this artifact: Add {U}. When you next cast an instant or sorcery spell this turn, "
        + "copy that spell. You may choose new targets for the copy.")]
    [InlineData("Jeong Jeong, the Deserter", "Legendary Creature — Human",
        "Exhaust — {3}: Put a +1/+1 counter on Jeong Jeong. When you next cast a Lesson spell this "
        + "turn, copy it and you may choose new targets for the copy.")]
    [InlineData("Sword of Wealth and Power", "Artifact — Equipment",
        "Equipped creature gets +2/+2 and has protection from instants and from sorceries.\nWhenever equipped "
        + "creature deals combat damage to a player, create a Treasure token. When you next cast an instant or "
        + "sorcery spell this turn, copy that spell. You may choose new targets for the copy.\nEquip {2}")]
    [InlineData("Taigam, Master Opportunist", "Legendary Creature — Human Monk",
        "Flurry — Whenever you cast your second spell each turn, copy it, then exile the spell you cast with "
        + "four time counters on it. If it doesn't have suspend, it gains suspend. (At the beginning of its "
        + "owner's upkeep, they remove a time counter. When the last is removed, they may play it without "
        + "paying its mana cost. If it's a creature, it has haste.)")]
    [InlineData("Crackling Spellslinger", "Creature — Human Wizard",
        "Flash\nWhen this creature enters, if you cast it, the next instant or sorcery spell you cast this "
        + "turn has storm. (When you cast that spell, copy it for each spell cast before it this turn. You may "
        + "choose new targets for the copies.)")]
    [InlineData("Sunken Palace", "Land — Cave",
        "This land enters tapped.\n{T}: Add {U}.\n{1}{U}, {T}, Exile seven cards from your graveyard: Add {U}. "
        + "When you spend this mana to cast a spell or activate an ability, copy that spell or ability. You may "
        + "choose new targets for the copy. (Mana abilities can't be copied.)")]
    [InlineData("Djinn Illuminatus", "Creature — Djinn",
        "({U/R} can be paid with either {U} or {R}.)\nFlying\nEach instant and sorcery spell you cast has "
        + "replicate. The replicate cost is equal to its mana cost. (When you cast it, copy it for each time "
        + "you paid its replicate cost. You may choose new targets for the copies.)")]
    public void Classify_CopyingYourOwnSpell_IsRedirect(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }

    [Theory]
    // What the ruling leaves out. Storm spells out its own rules, and a keyword's
    // reminder text is never an effect the card has; nor is a spell that copies
    // ITSELF in so many words (Mentor's Guidance, Banish into Fable); nor a copy
    // counted by COMMANDER casts, which is none in the cube (Thunderclap Drake).
    [InlineData("Tempest Technique", "Enchantment — Aura",
        "Storm (When you cast this spell, copy it for each spell cast before it this turn. "
        + "You may choose new targets for the copies. Copies become tokens.)\n"
        + "Enchant creature you control\nEnchanted creature gets +1/+1 for each enchantment you control.")]
    [InlineData("Mentor's Guidance", "Sorcery",
        "When you cast this spell, copy it if you control a planeswalker, Cleric, Druid, Shaman, Warlock, or "
        + "Wizard.\nScry 1, then draw a card.")]
    [InlineData("Banish into Fable", "Instant",
        "When you cast this spell from your hand, copy it if you control an artifact, then copy it if you "
        + "control an enchantment. You may choose new targets for the copies.\nReturn target nonland permanent "
        + "to its owner's hand. You create a 2/2 white Knight creature token with vigilance.")]
    [InlineData("Thunderclap Drake", "Creature — Drake",
        "Flying\nInstant and sorcery spells you cast cost {1} less to cast.\n{2}{U}, Sacrifice this creature: "
        + "When you next cast an instant or sorcery spell this turn, copy it for each time you've cast your "
        + "commander from the command zone this game. You may choose new targets for the copies.")]
    public void Classify_ACardCopyingOnlyItself_IsNotRedirect(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }

    [Theory]
    // A copy of a CREATURE spell becomes a token and is Tokens, not Redirect,
    // ruled 2026-09-28: "copia una magia creature è Token".
    [InlineData("Case of the Shifting Visage", "Enchantment — Case",
        "At the beginning of your upkeep, surveil 1.\nTo solve — There are fifteen or more cards in your "
        + "graveyard. (If unsolved, solve at the beginning of your end step.)\nSolved — Whenever you cast a "
        + "nonlegendary creature spell, copy that spell. (The copy becomes a token.)")]
    [InlineData("Double Down", "Enchantment",
        "Whenever you cast an outlaw spell, copy that spell. (Assassins, Mercenaries, Pirates, Rogues, and "
        + "Warlocks are outlaws. Copies of permanent spells become tokens.)")]
    [InlineData("Thurid, Mare of Destiny", "Legendary Creature — Pegasus",
        "Flying, lifelink\nWhenever you cast a Pegasus, Unicorn, or Horse creature spell, copy it. (The copy "
        + "becomes a token.)\nOther Pegasi, Unicorns, and Horses you control get +1/+1.")]
    [InlineData("Jackal, Genius Geneticist", "Legendary Creature — Human Scientist Villain",
        "Trample\nWhenever you cast a creature spell with mana value equal to Jackal's power, copy that spell, "
        + "except the copy isn't legendary. Then put a +1/+1 counter on Jackal. (The copy becomes a token.)")]
    public void Classify_CopyingYourCreatureSpell_IsTokensNotRedirect(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Tokens));
        Assert.False(result.HasFlag(CardEffect.Redirect));
    }

    [Theory]
    // Removal, widened 2026-09-19. "Equal to" is how the game writes a creature
    // hitting another creature, and the rule read one word order, one length of
    // filler and four destinations.
    [InlineData("Repentance", "Instant", "Target creature deals damage to itself equal to its power.")]
    [InlineData("Allies at Last", "Sorcery",
        "Affinity for Allies (This spell costs {1} less to cast for each Ally you control.)\n"
        + "Up to two target creatures you control each deal damage equal to their power to target "
        + "creature an opponent controls.")]
    [InlineData("Betrayal at the Vault", "Sorcery",
        "Target creature you control deals damage equal to its power to each of two other target creatures.")]
    [InlineData("Slash of Light", "Instant",
        "Slash of Light deals damage equal to the number of creatures you control plus the number of "
        + "Equipment you control to target creature.")]
    // The amount is written as freely as the target.
    [InlineData("Banshee", "Creature — Spirit",
        "{X}, {T}: This creature deals half X damage, rounded down, to any target, and half X damage, "
        + "rounded up, to you.")]
    [InlineData("Firestorm", "Instant",
        "As an additional cost to cast this spell, discard X cards.\n"
        + "Firestorm deals X damage to each of X targets.")]
    // "Destroy UP TO ONE OTHER target creature" is sixteen characters of filler.
    [InlineData("Faller's Faithful", "Creature — Human",
        "When this creature enters, destroy up to one other target creature. If that creature wasn't "
        + "dealt damage this turn, its controller draws two cards.")]
    // An Aura shrinks without ever saying "target", and the counter can land on
    // the victim after one has landed on the card itself.
    [InlineData("Weakness", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets -2/-1.")]
    [InlineData("Immolation", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +2/-2.")]
    [InlineData("Serrated Biskelion", "Artifact Creature — Construct",
        "{T}: Put a -1/-1 counter on this creature and a -1/-1 counter on target creature.")]
    // ANY toughness malus counts, ruled 2026-09-19 — the size is not the question
    // and neither is what happens to the power.
    [InlineData("Funeral Charm", "Instant",
        "Choose one —\n• Target player discards a card.\n• Target creature gets +2/-1 until end of turn.\n"
        + "• Target creature gains swampwalk until end of turn.")]
    [InlineData("Coils of the Medusa", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets +1/-1.\n"
        + "Sacrifice this Aura: Destroy all non-Wall creatures blocking enchanted creature.")]
    [InlineData("Ironclaw Curse", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets -0/-1.\n"
        + "Enchanted creature can't block creatures with power equal to or greater than the enchanted "
        + "creature's toughness.")]
    [InlineData("Contagion", "Instant",
        "You may pay 1 life and exile a black card from your hand rather than pay this spell's mana cost.\n"
        + "Distribute two -2/-1 counters among one or two target creatures.")]
    // A creature that ends up on the BOTTOM of a library is as answered as one
    // that is destroyed.
    [InlineData("The Spot's Portal", "Instant",
        "Put target creature on the bottom of its owner's library. You lose 2 life unless you control "
        + "a Villain.")]
    [InlineData("Dramatic Accusation", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, tap enchanted creature.\n"
        + "Enchanted creature doesn't untap during its controller's untap step.\n"
        + "{U}{U}: Shuffle enchanted creature into its owner's library.")]
    public void Classify_AnswersOneCreature_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Your OWN creature is a blink, an outlet or a way to hide it from a Wrath.
    [InlineData("Cold Storage", "Artifact",
        "{3}: Exile target creature you control.\n"
        + "Sacrifice this artifact: Return each creature card exiled with this artifact to the "
        + "battlefield under your control.")]
    // A creature CARD IN A GRAVEYARD is already dead.
    [InlineData("Eater of the Dead", "Creature — Horror",
        "{0}: If this creature is tapped, exile target creature card from a graveyard and untap this creature.")]
    // An answer that can only touch what it is already fighting is a combat
    // trick, in the active voice as much as the passive.
    [InlineData("Wall of Corpses", "Creature — Wall",
        "Defender (This creature can't attack.)\n"
        + "{B}, Sacrifice this creature: Destroy target creature this creature is blocking.")]
    [InlineData("Elite Javelineer", "Creature — Human Soldier",
        "Whenever this creature blocks, it deals 1 damage to target attacking creature.")]
    // A shrink aimed at YOUR OWN creature is a pump with a price.
    [InlineData("Ashnod's Battle Gear", "Artifact",
        "You may choose not to untap this artifact during your untap step.\n"
        + "{2}, {T}: Target creature you control gets +2/-2 for as long as this artifact remains tapped.")]
    // A fight has to name what it fights: "Fight Crime" is the name of a mode.
    [InlineData("School Daze", "Instant",
        "Choose one —\n• Do Homework — Draw three cards.\n• Fight Crime — Counter target spell. Draw a card.")]
    // A COLOUR-restricted permanent kill is RemovePermanent, not this: the
    // breadth is the point, exactly as it is for Vindicate. Ruled 2026-09-20,
    // reversing the reading of the day before.
    [InlineData("Northern Paladin", "Creature — Human Knight",
        "{W}{W}, {T}: Destroy target black permanent.")]
    // "That creature's CONTROLLER" is a face, not the creature.
    [InlineData("Dingus Staff", "Artifact",
        "Whenever a creature dies, this artifact deals 2 damage to that creature's controller.")]
    // And the TOP of a library hands the card straight back.
    [InlineData("Time Ebb", "Sorcery", "Put target creature on top of its owner's library.")]
    // …but UNLESS defending player sacrifices one is the opponent's choice of a
    // price, not an edict.
    [InlineData("Ogre Marauder", "Creature — Ogre Warrior",
        "Whenever this creature attacks, it gains \"this creature can't be blocked\" until end of turn unless "
        + "defending player sacrifices a creature of their choice.")]
    public void Classify_WhatOnlyReadsLikeAnAnswer_IsNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // An ANTE card carries no tags at all, ruled by measurement 2026-09-21: the
    // first line takes it out of the deck, so none of what it says ever
    // happens. Nine reviewed cards say it and all nine are untagged, which is
    // why this is a veto on the whole classifier rather than on one effect.
    [InlineData("Contract from Below", "Sorcery",
        "Remove this card from your deck before playing if you're not playing for ante.\n"
        + "Discard your hand, ante the top card of your library, then draw seven cards.")]
    [InlineData("Jeweled Bird", "Artifact",
        "Remove this card from your deck before playing if you're not playing for ante.\n"
        + "{T}: Ante this artifact. If you do, put all other cards you own from the ante into your "
        + "graveyard, then draw a card.")]
    public void Classify_AnAnteCard_CarriesNothingAtAll(string name, string typeLine, string oracle)
    {
        Assert.Equal(CardEffect.None, EffectClassifier.Classify(MakeCard(name, typeLine, oracle)));
    }

    [Theory]
    // ONE card off the top, into your hand, every turn. The put-into-hand rule
    // wanted a count where these cards write none, and it is the same second
    // hand either way (2026-09-21). 13 reviewed cards, 11 tagged.
    [InlineData("Darkstar Augur", "Token Creature — Bat Warlock",
        "Flying\nAt the beginning of your upkeep, reveal the top card of your library and put that card "
        + "into your hand. You lose life equal to its mana value.")]
    [InlineData("Traveling Botanist", "Creature — Dog Scout",
        "Whenever this creature becomes tapped, look at the top card of your library. If it's a land card, "
        + "you may reveal it and put it into your hand. If you don't put the card into your hand, you may "
        + "put it into your graveyard.")]
    // …and the same hand dug for with a SEARCH, which only counts when the
    // ability repeats: Gift of Estates says the identical words on a one-shot
    // sorcery and is Ramp alone.
    [InlineData("Land Tax", "Enchantment",
        "At the beginning of your upkeep, if an opponent controls more lands than you, you may search your "
        + "library for up to three basic land cards, reveal them, put them into your hand, then shuffle.")]
    // The opponent may be the one doing the looking.
    [InlineData("Phyrexian Portal", "Artifact",
        "{3}: If your library has ten or more cards in it, target opponent looks at the top ten cards of "
        + "your library and separates them into two face-down piles. Exile one of those piles. Search the "
        + "other pile for a card, put it into your hand, then shuffle the rest of that pile into your library.")]
    // A permanent sold for MORE THAN ONE card is a trade it wins. The one-shot
    // sacrifice rule was written for the one-card case and still reads that.
    [InlineData("Blitzball", "Artifact",
        "{T}: Add one mana of any color.\n"
        + "GOOOOAAAALLL! — {T}, Sacrifice this artifact: Draw two cards. Activate only if an opponent was "
        + "dealt combat damage by a legendary creature this turn.")]
    // Emptying your hand for a fixed number back is not a loot: the hand you
    // paid can be empty, which is what the Case solves itself on.
    [InlineData("Case of the Crimson Pulse", "Enchantment — Case",
        "When this Case enters, discard a card, then draw two cards.\n"
        + "To solve — You have no cards in hand. (If unsolved, solve at the beginning of your end step.)\n"
        + "Solved — At the beginning of your upkeep, discard your hand, then draw two cards.")]
    // A triggered draw printed INSIDE QUOTES was handed to a body that keeps
    // it, the reading Mnemonic Sliver already got.
    [InlineData("Thief's Knife", "Artifact — Equipment",
        "Job select (When this Equipment enters, create a 1/1 colorless Hero creature token, then attach "
        + "this to it.)\nEquipped creature gets +1/+1, has \"Whenever this creature deals combat damage to "
        + "a player, draw a card,\" and is a Rogue in addition to its other types.\nEquip {4}")]
    // The monarchy is a card every turn with no draw written down, and it
    // outlives the permanent that handed it over.
    [InlineData("Coin of Fate", "Artifact",
        "When this artifact enters, surveil 1.\n"
        + "{3}{W}, {T}, Exile two creature cards from your graveyard, Sacrifice this artifact: An opponent "
        + "chooses one of the exiled cards. You put that card on the bottom of your library and return the "
        + "other to the battlefield tapped. You become the monarch.")]
    // One trigger word covering TWO events is two cards. The stun ability is a
    // different line and may not be billed for this draw.
    [InlineData("Cryogen Relic", "Artifact",
        "When this artifact enters or leaves the battlefield, draw a card.\n"
        + "{1}{U}, Sacrifice this artifact: Put a stun counter on up to one target tapped creature.")]
    // The count-first sentence, and a graveyard handed flashback wholesale.
    [InlineData("Mob Verdict", "Sorcery",
        "Secret council — Each player secretly votes for another player, then those votes are revealed. "
        + "For each vote an opponent received, Mob Verdict deals 2 damage to that player and each creature "
        + "that player controls. For each vote you received, draw a card.")]
    [InlineData("Iroh, Grand Lotus", "Legendary Creature — Human Noble Ally",
        "Firebending 2\nDuring your turn, each non-Lesson instant and sorcery card in your graveyard has "
        + "flashback. The flashback cost is equal to that card's mana cost.\n"
        + "During your turn, each Lesson card in your graveyard has flashback {1}.")]
    // A Saga chapter naming more than one number fires more than once. Any
    // number of numbers, and the draw need not say "you".
    [InlineData("Summon: Anima", "Enchantment Creature — Saga Horror",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\n"
        + "I, II, III — Pain — You draw a card and you lose 1 life.\n"
        + "IV — Oblivion — Each opponent sacrifices a creature of their choice and loses 3 life.\nMenace")]
    [InlineData("Summon: Leviathan", "Enchantment Creature — Saga Leviathan",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Return each creature that isn't a Kraken, Leviathan, Merfolk, Octopus, or Serpent to its "
        + "owner's hand.\n"
        + "II, III — Until end of turn, whenever a Kraken, Leviathan, Merfolk, Octopus, or Serpent attacks, "
        + "draw a card.\nWard {2}")]
    // One card off SOMEBODY ELSE'S top, cast from where it lies.
    [InlineData("Vaan, Street Thief", "Legendary Creature — Human Scout",
        "Whenever one or more Scouts, Pirates, and/or Rogues you control deal combat damage to a player, "
        + "exile the top card of that player's library. You may cast it. If you don't, create a Treasure "
        + "token.\nWhenever you cast a spell you don't own, put a +1/+1 counter on each Scout, Pirate, and "
        + "Rogue you control.")]
    public void Classify_TheSecondPassWordings_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // PARITY ACROSS THE TABLE. The 2026-09-20 parity ruling was about one
    // player trading a card for a card; this is the two-player version, where
    // the count is level and nobody has gained on anybody.
    [InlineData("Tataru Taru", "Legendary Creature — Dwarf Advisor",
        "When Tataru Taru enters, you draw a card and target opponent may draw a card.\n"
        + "Scions' Secretary — Whenever an opponent draws a card, if it isn't that player's turn, create a "
        + "tapped Treasure token. This ability triggers only once each turn.")]
    [InlineData("Mog, Moogle Warrior", "Legendary Creature — Moogle Warrior",
        "Lifelink\nDance — At the beginning of your end step, each player may discard a card. Each player "
        + "who discarded a card this way draws a card. If a creature card was discarded this way, you "
        + "create a 1/2 white Moogle creature token with lifelink.")]
    // The self-sacrifice trade only wins when the count is READABLE and the
    // line hands nothing back. All-Fates Scroll draws "X cards, where X is …"
    // and Conch Horn puts one from your hand back on top; both are untagged.
    [InlineData("All-Fates Scroll", "Artifact",
        "{T}: Add one mana of any color.\n"
        + "{7}, {T}, Sacrifice this artifact: Draw X cards, where X is the number of differently named "
        + "lands you control.")]
    [InlineData("Conch Horn", "Artifact",
        "{1}, {T}, Sacrifice this artifact: Draw two cards, then put a card from your hand on top of your "
        + "library.")]
    // A quoted draw that is a LOOT is parity wherever it is printed.
    [InlineData("Ninja's Blades", "Artifact — Equipment",
        "Job select\nEquipped creature gets +1/+1, is a Ninja in addition to its other types, and has "
        + "\"Whenever this creature deals combat damage to a player, draw a card, then discard a card. "
        + "That player loses life equal to the discarded card's mana value.\"\nMutsunokami — Equip {2}")]
    // THE PILE IS THEIRS reaches their graveyard too: six reviewed cards exile
    // from a named opponent's graveyard and none is CardAdvantage.
    [InlineData("Hama, the Bloodbender", "Legendary Creature — Human Warlock",
        "When Hama enters, target opponent mills three cards. Exile up to one noncreature, nonland card "
        + "from that player's graveyard. For as long as you control Hama, you may cast the exiled card "
        + "during your turn by waterbending {X} rather than paying its mana cost, where X is its mana value.")]
    // A chapter that draws only when something else went its way is the
    // conditional rider this tag has no ruling on.
    [InlineData("The Tale of Tamiyo", "Legendary Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\n"
        + "I, II, III — Mill two cards. If two cards that share a card type were milled this way, draw a "
        + "card and repeat this process.\n"
        + "IV — Exile any number of target instant, sorcery, and/or Tamiyo planeswalker cards from your "
        + "graveyard. Copy them. You may cast any number of the copies.")]
    // "Exile the top card, you may play it" is the one-card impulse, which is a
    // rider — the second-hand rule has to say LOOK or REVEAL, never EXILE.
    [InlineData("Haste Magic", "Instant",
        "Target creature gets +3/+1 and gains haste until end of turn. Exile the top card of your library. "
        + "You may play it until your next end step.")]
    // A draw you cannot ask for, counted the other way round: Kain pays a card
    // per point only once he has changed sides.
    [InlineData("Kain, Traitorous Dragoon", "Legendary Creature — Human Knight",
        "Jump — During your turn, Kain has flying.\n"
        + "Whenever Kain deals combat damage to a player, that player gains control of Kain. If they do, "
        + "you draw that many cards, create that many tapped Treasure tokens, then lose that much life.")]
    // A draw the card spends ITSELF on, written without a colon.
    [InlineData("Lim-Dûl's Paladin", "Creature — Human Knight",
        "Trample\nAt the beginning of your upkeep, you may discard a card. If you don't, sacrifice this "
        + "creature and draw a card.\n"
        + "Whenever this creature becomes blocked, it gets +6/+3 until end of turn.")]
    public void Classify_TheSecondPassNonWordings_AreNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // CUMULATIVE UPKEEP paid in CARDS draws one more every turn it survives,
    // and it is written where a cost goes, so no rule was looking there.
    [InlineData("Psychic Vortex", "Enchantment",
        "Cumulative upkeep—Draw a card. (At the beginning of your upkeep, put an age counter on this "
        + "permanent, then sacrifice it unless you pay its upkeep cost for each age counter on it.)\n"
        + "At the beginning of your end step, sacrifice a land and discard your hand.")]
    // Symmetry is not parity when EVERYBODY gains — Howling Mine's reading,
    // given by name on 2026-09-22.
    [InlineData("Parker Luck", "Enchantment",
        "At the beginning of your end step, two target players each reveal the top card of their library. "
        + "They each lose life equal to the mana value of the card revealed by the other player. Then they "
        + "each put the card they revealed into their hand.")]
    // Several tokens that EACH draw: one card spent, two bought. "-1 +2 = +1".
    [InlineData("Niko, Light of Hope", "Legendary Creature — Human Wizard",
        "When Niko enters, create two Shard tokens. (They're enchantments with \"{2}, Sacrifice this "
        + "token: Scry 1, then draw a card.\")\n"
        + "{2}, {T}: Exile target nonlegendary creature you control. Shards you control become copies of "
        + "it until the next end step. Return it to the battlefield under its owner's control at the "
        + "beginning of the next end step.")]
    // WARP is a second cast, so a card that pays you on the way OUT pays twice.
    [InlineData("Anticausal Vestige", "Creature — Eldrazi",
        "When this creature leaves the battlefield, draw a card, then you may put a permanent card with "
        + "mana value less than or equal to the number of lands you control from your hand onto the "
        + "battlefield tapped.\n"
        + "Warp {4} (You may cast this card from your hand for its warp cost. Exile this creature at the "
        + "beginning of the next end step, then you may cast it from exile on a later turn.)")]
    // Copying your own spell is a second copy of a card you paid for once.
    [InlineData("Taigam, Master Opportunist", "Legendary Creature — Human Monk",
        "Flurry — Whenever you cast your second spell each turn, copy it, then exile the spell you cast "
        + "with four time counters on it. If it doesn't have suspend, it gains suspend.")]
    // Casting from the top of your library is the second hand, whatever you
    // pay for the card — ruled 2026-09-22, overturning the hand tag.
    [InlineData("Madame Web, Clairvoyant", "Legendary Creature — Mutant Advisor",
        "You may look at the top card of your library any time.\n"
        + "You may cast Spider spells and noncreature spells from the top of your library.\n"
        + "Whenever you attack, you may mill a card.")]
    public void Classify_TheThirdPassWordings_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The copy has to follow the trigger IMMEDIATELY. Mendicant Core puts Max
    // speed and a further {1} in front of its copy and carries no tag; Max
    // speed is NOT a blanket difficulty, since 34 reviewed cards print it and
    // 4 are tagged.
    [InlineData("Mendicant Core, Guidelight", "Legendary Artifact Creature — Robot",
        "Mendicant Core's power is equal to the number of artifacts you control.\n"
        + "Start your engines!\n"
        + "Max speed — Whenever you cast an artifact spell, you may pay {1}. If you do, copy it. "
        + "(The copy becomes a token.)")]
    // Two more triggers that NAME their own difficulty, confirmed 2026-09-22.
    // Bending is a keyword action a deck has to be built to perform…
    [InlineData("Avatar Aang", "Legendary Creature — Human Avatar Ally",
        "Flying, firebending 2\n"
        + "Whenever you waterbend, earthbend, firebend, or airbend, draw a card. Then if you've done all "
        + "four this turn, transform Avatar Aang.")]
    // …and a CHOSEN CARD NAME asks you to name a card and then meet it, that
    // same turn.
    [InlineData("The Clone Saga", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Surveil 3.\n"
        + "II — When you next cast a creature spell this turn, copy it, except the copy isn't legendary.\n"
        + "III — Choose a card name. Whenever a creature with the chosen name deals combat damage to a "
        + "player this turn, draw a card.")]
    // An ability that TRANSFORMS the permanent the moment it succeeds runs
    // once. This is the whole difference between the Sidequest and Traveling
    // Botanist, which print the same sentence word for word.
    [InlineData("Sidequest: Catch a Fish", "Enchantment",
        "At the beginning of your upkeep, look at the top card of your library. If it's an artifact or "
        + "creature card, you may reveal it and put it into your hand. If you put a card into your hand "
        + "this way, create a Food token and transform this enchantment.")]
    // A discard charged as an ADDITIONAL COST, with the spell itself counted,
    // is parity however many the card draws: Grab the Prize's reading, applied
    // 2026-09-22 to the two cards that were hand-tagged the other way.
    [InlineData("Laughing Mad", "Instant",
        "As an additional cost to cast this spell, discard a card.\nDraw two cards.\n"
        + "Flashback {3}{R}")]
    [InlineData("Sazacap's Brew", "Instant",
        "Gift a tapped Fish\nAs an additional cost to cast this spell, discard a card.\n"
        + "Target player draws two cards. If the gift was promised, target creature you control gets "
        + "+2/+0 until end of turn.")]
    // A draw for one card handed straight back is parity even when the discard
    // is conditional: the COUNT is the answer, not the condition.
    [InlineData("Chakra Meditation", "Enchantment",
        "When this enchantment enters, return up to one target instant or sorcery card from your "
        + "graveyard to your hand.\n"
        + "Whenever you cast an instant or sorcery spell, draw a card. Then discard a card unless there "
        + "are three or more Lesson cards in your graveyard.")]
    public void Classify_TheThirdPassNonWordings_AreNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    // A loot the card spreads over TWO TRIGGERS is still one card for one card
    // on a timer, ruled 2026-09-22: Filter, and not CardAdvantage.
    public void Classify_ALootSpreadOverTwoTriggers_IsFilterAndNotCardAdvantage()
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(
            "Teferi's Imp", "Creature — Imp",
            "Flying\n"
            + "Phasing (This phases in or out before you untap during each of your untap steps. While it's "
            + "phased out, it's treated as though it doesn't exist.)\n"
            + "Whenever this creature phases out, discard a card.\n"
            + "Whenever this creature phases in, draw a card."));

        Assert.True(effects.HasFlag(CardEffect.Filter));
        Assert.False(effects.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A TAP, in every shape the game prints it. Ruled 2026-09-22 after the hand
    // tags were found contradicting themselves on identical wording; the human
    // resolved six pairs the same way and rejected both proposed lines — the
    // lock need not LAST ("altrimenti saltano il punto e tutti i tappini") and
    // tapping need not be the card's main job.
    [InlineData("Twiddle", "Instant", "You may tap or untap target artifact, creature, or land.")]
    [InlineData("Riptide", "Instant", "Tap all blue creatures.")]
    [InlineData("Word of Binding", "Sorcery", "Tap X target creatures.")]
    [InlineData("Tidal Surge", "Sorcery", "Tap up to three target creatures without flying.")]
    [InlineData("Starport Security", "Artifact Creature — Robot Soldier",
        "{3}{W}, {T}: Tap another target creature. This ability costs {2} less to activate if you control "
        + "a creature with a +1/+1 counter on it.")]
    // A hyphen in the type line is not a reason to stop reading.
    [InlineData("Sterling Keykeeper", "Creature — Human Mercenary", "{2}, {T}: Tap target non-Mount creature.")]
    // A PERMANENT counts as much as a creature.
    [InlineData("Ring of the Lucii", "Legendary Artifact",
        "{T}: Add {C}{C}.\n{2}, {T}, Pay 1 life: Tap target nonland permanent.")]
    // The PLURAL untap lock, which is how every classic one is written and
    // which the rule only knew in the singular.
    [InlineData("Meekstone", "Artifact",
        "Creatures with power 3 or greater don't untap during their controllers' untap steps.")]
    // Nobody untaps at all.
    [InlineData("Stasis", "Enchantment",
        "Players skip their untap steps.\n"
        + "At the beginning of your upkeep, sacrifice this enchantment unless you pay {U}.")]
    // The Propaganda tax: the rule wanted punctuation straight after the verb.
    [InlineData("Propaganda", "Enchantment",
        "Creatures can't attack you unless their controller pays {2} for each creature they control "
        + "that's attacking you.")]
    // NEUTRALISING WITHOUT KILLING, ruled 2026-09-22 — the creature stays on
    // the board and stops mattering.
    [InlineData("Island of Wak-Wak", "Land", "{T}: Target creature with flying has base power 0 until end of turn.")]
    [InlineData("Spider-Man No More", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature is a Citizen with base power and toughness 1/1. It has "
        + "defender and loses all other abilities.")]
    [InlineData("Weakstone", "Artifact", "Attacking creatures get -1/-0.")]
    [InlineData("Fresh Start", "Enchantment — Aura",
        "Flash\nEnchant creature\nEnchanted creature gets -5/-0 and loses all abilities.")]
    // PHASING OUT, which earns this tag and Protection both.
    [InlineData("Vodalian Illusionist", "Creature — Merfolk Wizard",
        "{U}{U}, {T}: Target creature phases out.")]
    // Preventing damage to the PLAYER ALONE: nothing of yours is saved, the
    // attack simply stops mattering.
    [InlineData("Conservator", "Artifact",
        "{3}, {T}: Prevent the next 2 damage that would be dealt to you this turn.")]
    // An Aura's untap lock names neither a creature nor a land, and the land
    // veto must not swallow it.
    [InlineData("Flood the Engine", "Enchantment — Aura",
        "Enchant creature or Vehicle\nWhen this Aura enters, tap enchanted permanent.\n"
        + "Enchanted permanent loses all abilities and doesn't untap during its controller's untap step.")]
    public void Classify_TheWaysACreatureIsNeutralised_ArePacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // The tap must be able to land on a CREATURE. "Tap target artifact" is mana
    // denial and neutralises nobody.
    [InlineData("Relic Barrier", "Artifact", "{T}: Tap target artifact.")]
    [InlineData("Hyperion Blacksmith", "Creature — Human Artificer",
        "{T}: You may tap or untap target artifact an opponent controls.")]
    // Tapping on your own ATTACK TRIGGER is a combat trick: it clears one
    // blocker for a swing that is already happening. Seven reviewed cards do
    // it and none is tagged.
    [InlineData("Seasoned Marshal", "Creature — Human Soldier",
        "Whenever this creature attacks, you may tap target creature.")]
    [InlineData("Kimahri, Valiant Guardian", "Legendary Creature — Cat Warrior",
        "Vigilance\nRonso Rage — At the beginning of combat on your turn, put a +1/+1 counter on Kimahri "
        + "and tap target creature an opponent controls.")]
    // "<this creature> can't attack or block UNLESS …" is the price of a
    // statline, read per line, and an older card writes its own name.
    [InlineData("Tiger-Dillo", "Creature — Cat Armadillo",
        "This creature can't attack or block unless you control another creature with power 4 or greater.")]
    [InlineData("The Lion-Turtle", "Legendary Creature — Elder Cat Turtle",
        "Reach, vigilance\nWhen The Lion-Turtle enters, you gain 3 life.\n"
        + "The Lion-Turtle can't attack or block unless there are three or more Lesson cards in your "
        + "graveyard.\n{T}: Add one mana of any color.")]
    // A stun counter the card puts on ITSELF is a drawback, not a lock.
    [InlineData("Tonberry", "Creature — Salamander Horror",
        "This creature enters tapped with a stun counter on it.\n"
        + "Chef's Knife — During your turn, this creature has first strike and deathtouch.")]
    // An untap lock on LANDS is mana denial: "per le terre niente Pacify".
    [InlineData("Choke", "Enchantment", "Islands don't untap during their controllers' untap steps.")]
    [InlineData("Curse of Marit Lage", "Enchantment",
        "When this enchantment enters, tap all Islands.\n"
        + "Islands don't untap during their controllers' untap steps.")]
    // REMINDER TEXT and a worked EXAMPLE lock nobody down.
    [InlineData("Primal Clay", "Artifact Creature — Shapeshifter",
        "As this creature enters, it becomes your choice of a 3/3 artifact creature, a 2/2 artifact "
        + "creature with flying, or a 1/6 Wall artifact creature with defender in addition to its other "
        + "types. (A creature with defender can't attack.)")]
    public void Classify_WhatOnlyLooksLikeALock_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Fact]
    // Phasing out is BOTH tags, ruled 2026-09-22: the same words buy time
    // against a threat and save something of yours from an answer.
    public void Classify_PhasingSomethingOut_IsPacifyAndProtection()
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(
            "Reality Ripple", "Instant",
            "Target artifact, creature, or land phases out."));

        Assert.True(effects.HasFlag(CardEffect.Pacify));
        Assert.True(effects.HasFlag(CardEffect.Protection));
    }

    [Fact]
    // …and a prevention aimed at the PLAYER ALONE is Pacify and NOT Protection.
    public void Classify_PreventingDamageToYouAlone_IsPacifyAndNotProtection()
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(
            "Deep Wood", "Instant",
            "Cast this spell only during the declare attackers step and only if you've been attacked "
            + "this step.\nPrevent all damage that would be dealt to you this turn by attacking creatures."));

        Assert.True(effects.HasFlag(CardEffect.Pacify));
        Assert.False(effects.HasFlag(CardEffect.Protection));
    }

    private static Card MakeCard(string name, string typeLine, string oracleText, List<string>? keywords = null)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = typeLine,
            OracleText = oracleText,
            Keywords = keywords,
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 1,
            Colors = [],
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
