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
    public void Classify_AQuotedAbilityThatPumpsATarget_StaysBuff()
    {
        // The exception to the quotes rule: the granted ability pumps ANY
        // creature, so the card handed you a Buff.
        CardEffect result = EffectClassifier.Classify(MakeCard("Forbidden Lore", "Enchantment — Aura",
            "Enchant land\nEnchanted land has \"{T}: Target creature gets +2/+1 until end of turn.\""));

        Assert.True(result.HasFlag(CardEffect.Buff));
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
    [InlineData("Bygone Colossus", "Creature — Giant",
        "Warp {3} (You may cast this card from your hand for its warp cost. Exile it as it resolves.)")]
    [InlineData("Browse", "Enchantment",
        "{2}{U}{U}: Look at the top five cards of your library, put one of them into your hand, then exile the rest.")]
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
