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
    // A +1/+1 counter is Buff in the other vocabulary, ruled 2026-09-15. One
    // counter on one creature counts, and so do support and distribute.
    [InlineData("Cloudbound Moogle", "Creature — Moogle",
        "Flying\nWhen this creature enters, put a +1/+1 counter on target creature.")]
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

    [Fact]
    public void Classify_ASymmetricEdict_IsNotRemoval()
    {
        // "Each player sacrifices" costs you a creature too, and the hand-tagging
        // declines those. Only an edict aimed at THEM counts.
        CardEffect result = EffectClassifier.Classify(MakeCard("Abyssal Gatekeeper", "Creature — Horror",
            "When this creature dies, each player sacrifices a creature of their choice."));

        Assert.False(result.HasFlag(CardEffect.Removal));
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
    // The two tags are NOT exclusive, ruled 2026-09-19. A card that selects AND
    // comes out ahead carries both, because it really did both: Casting of
    // Bones draws three and keeps two, which is a card gained and a choice
    // made. An earlier cut of the net ruling read it as either/or and stripped
    // Filter from every profitable loot; recorded here so it is not re-derived.
    [InlineData("Casting of Bones", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, draw three cards, then discard one of them.")]
    [InlineData("Emmessi Tome", "Artifact — Book", "{5}, {T}: Draw two cards, then discard a card.")]
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
    // Scry and surveil ARE Filter, unconditionally, ruled 2026-09-19: it does
    // not matter how small the number is or what else the card does. Measured
    // first — the "rider" theory was that a surveil 1 tacked onto a bounce spell
    // should not count, and it does not distinguish anything: Filter is tagged
    // on 88 of the ~103 reviewed cards that scry or surveil, whether or not the
    // card does something else.
    [InlineData("Unauthorized Exit", "Instant",
        "Return target nonland permanent to its owner's hand. Surveil 1. "
        + "(Look at the top card of your library. You may put it into your graveyard.)")]
    [InlineData("Voyager Glidecar", "Artifact — Vehicle",
        "When this Vehicle enters, scry 1.\nTap three other untapped creatures you control: Until end of turn, "
        + "this Vehicle becomes an artifact creature and gains flying. Put a +1/+1 counter on it.\nCrew 1")]
    // Every printed form, which "scry \\d" was not: the bulk carries "scry X" on
    // 17 cards and a handful say "scries" because the subject is a player.
    [InlineData("Cascade Seer", "Creature — Merfolk Wizard",
        "When this creature enters, scry X, where X is the number of creatures in your party. "
        + "(Your party consists of up to one each of Cleric, Rogue, Warrior, and Wizard.)")]
    [InlineData("Kozilek's Command", "Kindred Instant — Eldrazi",
        "Choose two —\n"
        + "• Target player creates X 0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this token: Add {C}.\"\n"
        + "• Target player scries X, then draws a card.\n"
        + "• Exile target creature with mana value X or less.\n"
        + "• Exile up to X target cards from graveyards.")]
    public void Classify_ScryAndSurveil_AreAlwaysFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
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
    [InlineData("Badgermole Cub", "Creature — Badger Mole",
        "When this creature enters, earthbend 1. (Target land you control becomes a 0/0 creature with haste "
        + "that's still a land. Put a +1/+1 counter on it. When it dies or is exiled, return it to the "
        + "battlefield tapped.)\nWhenever you tap a creature for mana, add an additional {G}.")]
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
    // that answers it sits in the trigger beside the verb.
    [InlineData("Ran and Shaw", "Legendary Creature — Dragon",
        "Flying, firebending 2\n"
        + "When Ran and Shaw enter, if you cast them and there are three or more Dragon and/or Lesson cards in "
        + "your graveyard, create a token that's a copy of Ran and Shaw, except it's not legendary.\n"
        + "{3}{R}: Dragons you control get +2/+0 until end of turn.")]
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
    public void Classify_StaticPrevention_FailsTheSameTimingGateAsAPrintedKeyword()
    {
        // Bubble Matrix sits on the board; it is never held up in response, which
        // is the gate the keyword half has cleared since 2026-09-05.
        CardEffect result = EffectClassifier.Classify(
            MakeCard("Bubble Matrix", "Artifact", "Prevent all damage that would be dealt to creatures."));

        Assert.False(result.HasFlag(CardEffect.Protection));
    }

    [Fact]
    public void Classify_TargetedRegeneration_IsNotProtection()
    {
        // Measured on the reviewed set: "regenerate target" caught 4 and wrongly
        // fired on 12, so it stayed out and Death Ward is a deliberate miss.
        CardEffect result = EffectClassifier.Classify(
            MakeCard("Death Ward", "Instant", "Regenerate target creature."));

        Assert.False(result.HasFlag(CardEffect.Protection));
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
    [InlineData("Ainok Strike Leader", "Creature — Hound Soldier",
        "Sacrifice this creature: Creature tokens you control gain indestructible until end of turn.", CardEffect.Protection)]
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
    // purpose; ONE point of life is a rider, in either direction; "YOU lose life
    // equal to" is the price of the card; a charge attached to a kill is a
    // removal spell with a bonus; and what a TOKEN does is the token's.
    [InlineData("Pyrokinesis", "Instant",
        "You may exile a red card from your hand rather than pay this spell's mana cost.\n"
        + "Pyrokinesis deals 4 damage divided as you choose among any number of target creatures.")]
    [InlineData("Fiery Justice", "Sorcery",
        "Fiery Justice deals 5 damage divided as you choose among any number of targets. "
        + "Target opponent gains 5 life.")]
    [InlineData("Sanguine Syphoner", "Creature — Vampire",
        "Whenever this creature attacks, each opponent loses 1 life and you gain 1 life.")]
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
    public void Classify_WhatOnlyLooksLikeBurn_IsNot(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
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
    public void Classify_ActingOnASpellWithoutCounteringIt_IsRedirect(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }

    [Theory]
    // A DELAYED COPY doubles a spell of your own rather than acting on somebody
    // else's on the stack. Ruled out 2026-09-19: it matched 8 reviewed cards, 4
    // tagged and 4 not, on wording that is word for word the same.
    [InlineData("Ether", "Artifact",
        "{T}, Exile this artifact: Add {U}. When you next cast an instant or sorcery spell this turn, "
        + "copy that spell. You may choose new targets for the copy.")]
    [InlineData("Jeong Jeong, the Deserter", "Legendary Creature — Human",
        "Exhaust — {3}: Put a +1/+1 counter on Jeong Jeong. When you next cast a Lesson spell this "
        + "turn, copy it and you may choose new targets for the copy.")]
    // Storm spells out its own rules, and a keyword's reminder text is never an
    // effect the card has.
    [InlineData("Tempest Technique", "Enchantment — Aura",
        "Storm (When you cast this spell, copy it for each spell cast before it this turn. "
        + "You may choose new targets for the copies. Copies become tokens.)\n"
        + "Enchant creature you control\nEnchanted creature gets +1/+1 for each enchantment you control.")]
    public void Classify_DoublingYourOwnNextSpell_IsNotRedirect(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
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
