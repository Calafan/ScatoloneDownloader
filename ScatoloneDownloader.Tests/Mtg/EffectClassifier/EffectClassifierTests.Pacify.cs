using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // Behold the Unspeakable's -2/-0 to their creatures until your next turn
    // (tagged 2026-10-27; Mouth of the Storm, untagged 09-11 before the ruling,
    // handed back) and Swift Reconfiguration's creature made a noncreature
    // artifact (like Sugar Coat), read 2026-10-30.
    [InlineData("Behold the Unspeakable // Vision of the Unspeakable", "Enchantment — Saga // Enchantment Creature — Spirit",
        "(As this Saga enters and after your draw step, add a lore counter.)\nI — Creatures you don't control "
        + "get -2/-0 until your next turn.\nII — If you have one or fewer cards in hand, draw four cards. "
        + "Otherwise, scry 2, then draw two cards.\nIII — Exile this Saga, then return it to the battlefield "
        + "transformed under your control.\nFlying, trample\nThis creature gets +1/+1 for each card in your hand.")]
    [InlineData("Mouth of the Storm", "Creature — Elemental",
        "Flying\nWard {2} (Whenever this creature becomes the target of a spell or ability an opponent "
        + "controls, counter it unless that player pays {2}.)\nWhen this creature enters, creatures your "
        + "opponents control get -3/-0 until your next turn.")]
    [InlineData("Swift Reconfiguration", "Enchantment — Aura",
        "Flash\nEnchant creature or Vehicle\nEnchanted permanent is a Vehicle artifact with crew 5 and it loses "
        + "all other card types. (It's not a creature unless it's crewed.)")]
    public void Classify_AShrinkUntilYourNextTurnOrANoncreatureArtifact_IsPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Tawnos's Coffin exiles a creature until the Coffin LEAVES OR BECOMES
    // UNTAPPED: a tapper's lock, Pacify's, not Removal (read 2026-10-16, with the
    // 10-14 reading that remains tapped is Pacify's). An O-Ring that gives the
    // card back only when it leaves stays Removal.
    [InlineData("Tawnos's Coffin", "Artifact",
        "You may choose not to untap this artifact during your untap step.\n{3}, {T}: Exile target creature "
        + "and all Auras attached to it. Note the number and kind of counters that were on that creature. When "
        + "this artifact leaves the battlefield or becomes untapped, return that exiled card to the battlefield "
        + "under its owner's control tapped with the noted number and kind of counters on it. If you do, return "
        + "the other exiled cards to the battlefield under their owner's control attached to that permanent.")]
    public void Classify_TheOctober16Pass_AnExileThatEndsOnUntap_IsPacify(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Pacify));
        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // An O-Ring on a SAGA gives the creature back when the Saga is sacrificed:
    // Pacify alone, not Removal, ruled 2026-10-16 'Si, solo Pacify' on Summon:
    // Ixion against Trial of a Time Lord.
    [InlineData("Summon: Ixion", "Enchantment Creature — Saga Unicorn",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — "
        + "Aerospark — Exile target creature an opponent controls until this Saga leaves the battlefield.\nII, "
        + "III — Put a +1/+1 counter on each of up to two target creatures you control. You gain 2 life.\nFirst "
        + "strike")]
    [InlineData("Trial of a Time Lord", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI, II, III — "
        + "Exile target nontoken creature an opponent controls until this Saga leaves the battlefield.\nIV — "
        + "Starting with you, each player votes for innocent or guilty. If guilty gets more votes, the owner of "
        + "each card exiled with this Saga puts that card on the bottom of their library.")]
    public void Classify_TheOctober16Pass_ASagaORing_IsPacifyAlone(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Pacify));
        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // A TAP, in every shape the game prints it. Ruled 2026-09-22 after the hand
    // tags were found contradicting themselves on identical wording; the human
    // resolved six pairs the same way and rejected both proposed lines — the
    // lock need not LAST ("altrimenti saltano il punto e tutti i tappini") and
    // tapping need not be the card's main job. (Word of Binding and Tidal
    // Surge moved out on 2026-10-27: a bare tap at sorcery speed is not this
    // tag. See Classify_TheOctober27PacifyPass_IsNotPacify.)
    [InlineData("Twiddle", "Instant", "You may tap or untap target artifact, creature, or land.")]
    [InlineData("Riptide", "Instant", "Tap all blue creatures.")]
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
    // A Cursed Role ("Enchanted creature is 1/1") put on an opponent's creature:
    // Diminisher Witch, tagged by the human on 2026-09-29.
    [InlineData("Diminisher Witch", "Creature — Human Warlock",
        "Bargain (You may sacrifice an artifact, enchantment, or token as you cast this spell.)\nWhen this "
        + "creature enters, if it was bargained, create a Cursed Role token attached to target creature an "
        + "opponent controls. (If you control another Role on it, put that one into the graveyard. Enchanted "
        + "creature is 1/1.)")]
    // …without naming the controller, and on each of theirs (2026-10-13).
    [InlineData("Asinine Antics", "Sorcery",
        "You may cast this spell as though it had flash if you pay {2} more to cast it.\nFor each creature "
        + "your opponents control, create a Cursed Role token attached to that creature. (If you control "
        + "another Role on it, put that one into the graveyard. Enchanted creature is 1/1.)")]
    [InlineData("Gadwick's First Duel", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Create "
        + "a Cursed Role token attached to up to one target creature. (If you control another Role on it, put "
        + "that one into the graveyard. Enchanted creature is 1/1.)\nII — Scry 2.\nIII — When you next cast an "
        + "instant or sorcery spell with mana value 3 or less this turn, copy that spell. You may choose new "
        + "targets for the copy.")]
    // A power shrink that LASTS, ruled 2026-10-13 ("Pacify solo se dura"): an
    // Aura, or an activated one you can aim every turn.
    [InlineData("Morgul-Knife Wound", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets -3/-0 and has \"At the beginning of your upkeep, exile this "
        + "creature unless you pay 2 life.\"")]
    [InlineData("Pradesh Gypsies", "Creature — Human Nomad",
        "{1}{G}, {T}: Target creature gets -2/-0 until end of turn.")]
    // Four more shapes read the same day, one reviewed card each.
    [InlineData("Hunted by The Family", "Sorcery",
        "Choose up to four target creatures you don't control. For each of them, that creature's controller "
        + "faces a villainous choice — That creature becomes a 1/1 white Human creature and loses all "
        + "abilities, or you create a token that's a copy of it.")]
    [InlineData("The Eternal Wanderer", "Legendary Planeswalker",
        "No more than one creature can attack The Eternal Wanderer each combat.\n+1: Exile up to one target "
        + "artifact or creature. Return that card to the battlefield under its owner's control at the beginning "
        + "of that player's next end step.\n0: Create a 2/2 white Samurai creature token with double strike.\n−4: "
        + "For each player, choose a creature that player controls. Each player sacrifices all creatures they "
        + "control not chosen this way.")]
    [InlineData("Bewitching Leechcraft", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, tap enchanted creature.\nEnchanted creature has \"If this "
        + "creature would untap during your untap step, remove a +1/+1 counter from it instead. If you do, "
        + "untap it.\" (Otherwise, it doesn't untap.)")]
    [InlineData("The Pandorica", "Legendary Artifact",
        "You may choose not to untap The Pandorica during your untap step.\n{1}{W}, {T}: Untap another target "
        + "nonland permanent, then it phases out. It can't phase in for as long as The Pandorica remains "
        + "tapped. When The Pandorica becomes untapped or leaves the battlefield, that permanent phases in. "
        + "Activate only as a sorcery.")]
    // A stun counter "on it" when the card named the victim first.
    [InlineData("Trap the Trespassers", "Instant",
        "Secret council — Each player secretly votes for a creature you don't control, then those votes are "
        + "revealed. For each creature with one or more votes, put that many stun counters on it, then tap it. "
        + "(If a permanent with a stun counter would become untapped, remove one from it instead.)")]
    [InlineData("Shackle Slinger", "Creature — Human Soldier",
        "Whenever you cast your second spell each turn, choose target creature an opponent controls. If it's "
        + "tapped, put a stun counter on it. Otherwise, tap it. (If a permanent with a stun counter would "
        + "become untapped, remove one from it instead.)")]
    // Read 2026-10-16, one reviewed card each, tagged and never proposed: an
    // Aura's OTHERWISE base 1/1 (Burden of Proof), a target of theirs that loses
    // all abilities until your next turn (Azure Beastbinder), an attacker made
    // blocked (Dazzling Beauty), an Aura that removes its creature from combat
    // (Imprison), and damage dealt to you sent back to that creature (Mangara's
    // Equity).
    [InlineData("Burden of Proof", "Enchantment — Aura",
        "Flash\nEnchant creature\nEnchanted creature gets +2/+2 as long as it's a Detective you control. "
        + "Otherwise, it has base power and toughness 1/1 and can't block Detectives.")]
    [InlineData("Azure Beastbinder", "Creature — Rat Rogue",
        "Vigilance\nThis creature can't be blocked by creatures with power 2 or greater.\nWhenever this "
        + "creature attacks, up to one target artifact, creature, or planeswalker an opponent controls loses "
        + "all abilities until your next turn. If it's a creature, it also has base power and toughness 2/2 "
        + "until your next turn.")]
    [InlineData("Dazzling Beauty", "Instant",
        "Cast this spell only during the declare blockers step.\nTarget unblocked attacking creature becomes "
        + "blocked. (This spell works on creatures that can't be blocked.)\nDraw a card at the beginning of the "
        + "next turn's upkeep.")]
    [InlineData("Imprison", "Enchantment — Aura",
        "Enchant creature\nWhenever a player activates an ability of enchanted creature with {T} in its "
        + "activation cost that isn't a mana ability, you may pay {1}. If you do, counter that ability. If you "
        + "don't, destroy this Aura.\nWhenever enchanted creature attacks or blocks, you may pay {1}. If you do, "
        + "tap the creature, remove it from combat, and creatures it was blocking that had become blocked by "
        + "only that creature this combat become unblocked. If you don't, destroy this Aura.")]
    [InlineData("Mangara's Equity", "Enchantment",
        "As this enchantment enters, choose black or red.\nAt the beginning of your upkeep, sacrifice this "
        + "enchantment unless you pay {1}{W}.\nWhenever a creature of the chosen color deals damage to you or a "
        + "white creature you control, this enchantment deals that much damage to that creature.")]
    // An untap lock GIVEN to the creature a Wall blocked, ruled 2026-10-16
    // 'entrambe Pacify': the quoted 'this creature' is the victim, not the card. A
    // creature turned into an artifact by an AURA, ruled the same day 'Aura =
    // +Pacify' (Sugar Coat). And an Equipment whose lock lands on the creature
    // equipped one BLOCKS is still a lock.
    [InlineData("Mindbender Spores", "Creature — Fungus Wall",
        "Defender (This creature can't attack.)\nFlying\nWhenever this creature blocks a creature, put four "
        + "fungus counters on that creature. The creature gains \"This creature doesn't untap during your untap "
        + "step if it has a fungus counter on it\" and \"At the beginning of your upkeep, remove a fungus counter "
        + "from this creature.\"")]
    [InlineData("Glyph of Delusion", "Instant",
        "Put X glyph counters on target creature that target Wall blocked this turn, where X is the power of "
        + "that blocked creature. The creature gains \"This creature doesn't untap during your untap step if it "
        + "has a glyph counter on it\" and \"At the beginning of your upkeep, remove a glyph counter from this "
        + "creature.\"")]
    [InlineData("Sugar Coat", "Enchantment — Aura",
        "Flash\nEnchant creature or Food\nEnchanted permanent is a colorless Food artifact with \"{2}, {T}, "
        + "Sacrifice this artifact: You gain 3 life\" and loses all other card types and abilities.")]
    [InlineData("Shield of the Righteous", "Artifact — Equipment",
        "Equipped creature gets +0/+2 and has vigilance.\nWhenever equipped creature blocks a creature, that "
        + "creature doesn't untap during its controller's next untap step.\nEquip {2}")]
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
    // A shrink that does NOT last, ruled 2026-10-13: one-shot until end of
    // turn (Desperate Parry named), an activation that sacrifices the card
    // (Cabal Trainee), and an Aura on a creature that becomes yours
    // (Duskmourn's Domination). (Fear of Falling moved out on 2026-10-27: a
    // shrink until your next turn lasts on any trigger. See
    // Classify_TheOctober27PacifyPass_IsPacify.)
    [InlineData("Obyra's Attendants // Desperate Parry", "Creature — Faerie Wizard // Instant — Adventure",
        "Flying\nTarget creature gets -4/-0 until end of turn. (Then exile this card. You may cast the "
        + "creature later from exile.)")]
    [InlineData("Cabal Trainee", "Creature — Human Minion",
        "Sacrifice this creature: Target creature gets -2/-0 until end of turn.")]
    [InlineData("Duskmourn's Domination", "Enchantment — Aura",
        "Enchant creature\nYou control enchanted creature.\nEnchanted creature gets -3/-0 and loses all "
        + "abilities.")]
    // A tap fired at sorcery speed (ruled 2026-10-13, "gli incantesimi si
    // giocano come sorcery"), a creature you just reanimated, and an attack
    // trigger written "up to one target".
    [InlineData("Rimefur Reindeer", "Creature — Elk",
        "Whenever an enchantment you control enters, tap target creature an opponent controls.")]
    [InlineData("Extraction Specialist", "Creature — Human Rogue",
        "Lifelink\nWhen this creature enters, return target creature card with mana value 2 or less from your "
        + "graveyard to the battlefield. That creature can't attack or block for as long as you control this "
        + "creature.")]
    [InlineData("Baseball Bat", "Artifact — Equipment",
        "When this Equipment enters, attach it to target creature you control.\nEquipped creature gets "
        + "+1/+1.\nWhenever equipped creature attacks, tap up to one target creature.\nEquip {3} ({3}: Attach to "
        + "target creature you control. Equip only as a sorcery.)")]
    // Read 2026-10-16: an Equipment's lock and an Aura's on a creature put under
    // your control hold down your own (T-45 Power Armor, Dance of the Dead); can't
    // attack or block ALONE is a statline's price (Toby's token); a base 0 on a
    // creature you cast (Cycle of Life); a tap that makes the creature deal damage
    // (Deadshot); other creatures can't attack (Oracle en-Vec); a permanent
    // Treasure is Removal alone (Vraska); damage sent to the controller is Burn
    // (Backfire); and a flicker to the next end step is never Pacify, ruled the
    // same day (Phelia).
    [InlineData("T-45 Power Armor", "Artifact — Equipment",
        "When this Equipment enters, you get {E}{E} (two energy counters).\nEquipped creature gets +3/+3 and "
        + "doesn't untap during its controller's untap step.\nAt the beginning of your upkeep, you may pay {E}. "
        + "If you do, untap equipped creature, then put your choice of a menace, trample, or lifelink counter "
        + "on it.\nEquip {3}")]
    [InlineData("Dance of the Dead", "Enchantment — Aura",
        "Enchant creature card in a graveyard\nWhen this Aura enters, if it's on the battlefield, it loses "
        + "\"enchant creature card in a graveyard\" and gains \"enchant creature put onto the battlefield with "
        + "this Aura.\" Put enchanted creature card onto the battlefield tapped under your control and attach "
        + "this Aura to it. When this Aura leaves the battlefield, that creature's controller sacrifices "
        + "it.\nEnchanted creature gets +1/+1 and doesn't untap during its controller's untap step.\nAt the "
        + "beginning of the upkeep of enchanted creature's controller, that player may pay {1}{B}. If the "
        + "player does, untap that creature.")]
    [InlineData("Toby, Beastie Befriender", "Legendary Creature — Human Wizard",
        "When Toby enters, create a 4/4 white Beast creature token with \"This token can't attack or block "
        + "alone.\"\nAs long as you control four or more creature tokens, creature tokens you control have "
        + "flying.")]
    [InlineData("Cycle of Life", "Enchantment",
        "Return this enchantment to its owner's hand: Target creature you cast this turn has base power and "
        + "toughness 0/1 until your next upkeep. At the beginning of your next upkeep, put a +1/+1 counter on "
        + "that creature.")]
    [InlineData("Deadshot", "Sorcery",
        "Tap target creature. It deals damage equal to its power to another target creature.")]
    [InlineData("Oracle en-Vec", "Creature — Human Wizard",
        "{T}: Target opponent chooses any number of creatures they control. During that player's next turn, "
        + "the chosen creatures attack if able, and other creatures can't attack. At the beginning of that "
        + "turn's end step, destroy each of the chosen creatures that didn't attack this turn. Activate only "
        + "during your turn.")]
    [InlineData("Vraska, Betrayal's Sting", "Legendary Planeswalker — Vraska",
        "Compleated ({B/P} can be paid with {B} or 2 life. If life was paid, this planeswalker enters with "
        + "two fewer loyalty counters.)\n0: You draw a card and lose 1 life. Proliferate.\n−2: Target creature "
        + "becomes a Treasure artifact with \"{T}, Sacrifice this artifact: Add one mana of any color\" and loses "
        + "all other card types and abilities.\n−9: If target player has fewer than nine poison counters, they "
        + "get a number of poison counters equal to the difference.")]
    [InlineData("Backfire", "Enchantment — Aura",
        "Enchant creature\nWhenever enchanted creature deals damage to you, this Aura deals that much damage "
        + "to that creature's controller.")]
    [InlineData("Phelia, Exuberant Shepherd", "Legendary Creature — Dog",
        "Flash\nWhenever Phelia attacks, exile up to one other target nonland permanent. At the beginning of "
        + "the next end step, return that card to the battlefield under its owner's control. If it entered "
        + "under your control, put a +1/+1 counter on Phelia.")]
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
    // Pinned on Deep Wood, an instant, until 2026-10-13, when a Fog cast once
    // stopped being Pacify; a permanent that does it again and again still is.
    public void Classify_PreventingDamageToYouAlone_IsPacifyAndNotProtection()
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(
            "Conservator", "Artifact",
            "{3}, {T}: Prevent the next 2 damage that would be dealt to you this turn."));

        Assert.True(effects.HasFlag(CardEffect.Pacify));
        Assert.False(effects.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A prevention to you cast ONCE is a Fog, ruled 2026-10-13 ("Ultimate
    // Magic: Holy errore"); Holy keeps the Protection of its indestructible.
    [InlineData("Eerie Interference", "Instant",
        "Prevent all damage that would be dealt to you and creatures you control this turn by creatures.")]
    [InlineData("Ultimate Magic: Holy", "Instant",
        "Permanents you control gain indestructible until end of turn. If this spell was cast from exile, "
        + "prevent all damage that would be dealt to you this turn.\nForetell {2}{W} (During your turn, you may "
        + "pay {2} and exile this card from your hand face down. Cast it on a later turn for its foretell "
        + "cost.)")]
    // A stun bought with a sacrifice on its own attack is the trick's price.
    [InlineData("Vengeful Villagers", "Creature — Human Citizen",
        "Whenever this creature attacks, choose target creature an opponent controls. Tap it, then you may "
        + "sacrifice an artifact or creature. If you do, put a stun counter on the chosen creature. (If a "
        + "permanent with a stun counter would become untapped, remove one from it instead.)")]
    public void Classify_AFogOrAStunBoughtOnTheAttack_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Fact]
    // …while a tap on the attack that STUNS every time outlasts the swing:
    // "Mjölnir lo fa sempre" (2026-10-13).
    public void Classify_ATapAndStunOnEveryAttack_IsPacify()
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard("Mjölnir, Storm Hammer", "Legendary Artifact — Equipment",
            "When Mjölnir enters, attach it to target legendary creature you control.\nWhenever equipped creature "
            + "attacks, tap target creature defending player controls and put a stun counter on it. Then Mjölnir "
            + "deals damage to each opponent equal to the number of tapped creatures that opponent controls.\nEquip "
            + "{4}"));

        Assert.True(effects.HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Ruled 2026-10-25: target creatures phased out in the plural, with no
    // controller named, are Pacify beside Protection, the 2026-09-22 phasing
    // ruling (March of Swirling Mist).
    [InlineData("March of Swirling Mist", "Instant",
        "As an additional cost to cast this spell, you may exile any number of blue cards from your hand. "
        + "This spell costs {2} less to cast for each card exiled this way.\nUp to X target creatures phase out. "
        + "(While they're phased out, they're treated as though they don't exist. Each one phases in before its "
        + "controller untaps during their next untap step.)")]
    public void Classify_TheOctober25PhasingRuling_IsPacifyAndProtection(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Pacify));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Pacify pass of 2026-10-27. A shrink until your next turn lasts into theirs
    // whatever fires it, with no threshold on N (All-Seeing Arbiter, Fear of
    // Falling, Wasp: 'durano fino al prossimo turno quindi funzionano sull'attacco
    // avversario'). A bare tap at instant speed stays: an activation (Ring of the
    // Lucii), a flash ETB (Chrome Prowler), an instant (Riptide), a sorcery
    // castable as though it had flash (Breaking Wave); and a tap at sorcery speed
    // with a lock stays (Summon: Shiva's stun, Croaking Curse's Cursed Role).
    // Readings: an Aura's base 1/1 'as long as enchanted permanent is a creature'
    // (Awakened Awareness), 'it doesn't untap' after the enchanted or copied
    // creature (Stunning Strike, Wall of Stolen Identity).
    [InlineData("All-Seeing Arbiter", "Creature — Avatar",
        "Flying\nWhenever this creature enters or attacks, draw two cards, then discard a card.\nWhenever you "
        + "discard a card, target creature an opponent controls gets -X/-0 until your next turn, where X is the "
        + "number of different mana values among cards in your graveyard.")]
    [InlineData("Fear of Falling", "Enchantment Creature — Nightmare",
        "Flying\nWhenever this creature attacks, target creature defending player controls gets -2/-0 and "
        + "loses flying until your next turn.")]
    [InlineData("Wasp, Shrinking Savior", "Legendary Creature — Human Hero",
        "Flying\nWhenever Wasp attacks, up to one other target creature gets -3/-0 until your next turn. Then "
        + "draw a card for each creature with power less than 0 on the battlefield.")]
    [InlineData("Chrome Prowler", "Artifact Creature — Phyrexian Cat",
        "Flash\nWhen this creature enters, tap target creature an opponent controls.")]
    [InlineData("Riptide", "Instant",
        "Tap all blue creatures.")]
    [InlineData("Breaking Wave", "Sorcery",
        "You may cast this spell as though it had flash if you pay {2} more to cast it. (You may cast it any "
        + "time you could cast an instant.)\nSimultaneously untap all tapped creatures and tap all untapped "
        + "creatures.")]
    [InlineData("Summon: Shiva", "Enchantment Creature — Saga Elemental",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Heavenly Strike — Tap target creature an opponent controls. Put a stun counter on it. (If a "
        + "permanent with a stun counter would become untapped, remove one from it instead.)\nIII — Diamond Dust "
        + "— Draw a card for each tapped creature your opponents control.")]
    [InlineData("Vantress Transmuter // Croaking Curse", "Creature — Human Wizard // Sorcery — Adventure",
        "Tap target creature. Create a Cursed Role token attached to it. (Enchanted creature is 1/1.)")]
    [InlineData("Awakened Awareness", "Enchantment — Aura",
        "Enchant artifact or creature\nWhen this Aura enters, put X +1/+1 counters on enchanted permanent.\nAs "
        + "long as enchanted permanent is a creature, it has base power and toughness 1/1.")]
    [InlineData("Stunning Strike", "Enchantment — Aura",
        "Flash\nEnchant creature\nWhen this Aura enters, tap enchanted creature and remove it from combat.\nAs "
        + "long as enchanted creature isn't legendary, it doesn't untap during its controller's untap step.")]
    [InlineData("Wall of Stolen Identity", "Creature — Shapeshifter Wall",
        "You may have this creature enter as a copy of any creature on the battlefield, except it's a Wall in "
        + "addition to its other types and has defender. When you do, tap the copied creature and it doesn't "
        + "untap during its controller's untap step for as long as you control this creature.")]
    // Blue Dragon writes "until your next turn" before the target, the second form
    // of the -N/-0 that lasts into their turn.
    [InlineData("Blue Dragon", "Creature — Dragon",
        "Flying\nLightning Breath — When this creature enters, until your next turn, target creature an "
        + "opponent controls gets -3/-0, up to one other target creature gets -2/-0, and up to one other target "
        + "creature gets -1/-0.")]
    // A lock on the tap's own line keeps the tag at sorcery speed (Abominable
    // Treefolk's ETB, Illithid Harvester's Adventure), and so does one on another
    // line (Wrath of Marit Lage, Cone of Cold's higher rolls).
    [InlineData("Abominable Treefolk", "Snow Creature — Treefolk",
        "Trample\nAbominable Treefolk's power and toughness are each equal to the number of snow permanents "
        + "you control.\nWhen this creature enters, tap target creature an opponent controls. That creature "
        + "doesn't untap during its controller's next untap step.")]
    [InlineData("Illithid Harvester // Plant Tadpoles", "Creature — Horror // Sorcery — Adventure",
        "Ceremorphosis — When this creature enters, turn any number of target tapped nontoken creatures face "
        + "down. They're 2/2 Horror creatures.\nTap X target creatures. They don't untap during their "
        + "controllers' next untap steps. (Then exile this card. You may cast the creature later from exile.)")]
    [InlineData("Wrath of Marit Lage", "Enchantment",
        "When this enchantment enters, tap all red creatures.\nRed creatures don't untap during their "
        + "controllers' untap steps.")]
    [InlineData("Cone of Cold", "Sorcery",
        "Roll a d20.\n1—9 | Tap all creatures your opponents control.\n10—19 | Tap all creatures your opponents "
        + "control. Those creatures don't untap during their controllers' next untap steps.\n20 | Tap all "
        + "creatures your opponents control. Those creatures don't untap during their controllers' next untap "
        + "steps. Until your next turn, creatures your opponents control enter tapped.")]
    public void Classify_TheOctober27PacifyPass_IsPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Pacify pass of 2026-10-27, ruled on Harrier Strix: 'Se tappa e basta è un
    // Pacify ma deve essere instant. Se è sorcery o sull'attacco (senza mettere
    // uno stun counter) non è Pacify'. The card's own ETB without flash (Harrier
    // Strix, Network Disruptor, Oceanus Dragon, Ultimecia, Thundermare, Assassin
    // Gauntlet), a land of yours entering (Tiller Engine), a Saga chapter (Summon:
    // Alexander), a sorcery (Word of Binding, Tidal Surge, Winter Blast, Blinding
    // Light, Falling Star; the nine reviewed ones named, 'NON sono pacify').
    // Readings: an attack trigger 'attacks alone' (Imperial Subduer), your own
    // creature phased out (Haystack), your own tribe held back (Doomed Artisan),
    // and a shield 'until your next turn, whenever a creature attacks you, it gets
    // -1/-0' (Tamiyo).
    [InlineData("Harrier Strix", "Creature — Bird",
        "Flying\nWhen this creature enters, tap target permanent.\n{2}{U}: Draw a card, then discard a card.")]
    [InlineData("Network Disruptor", "Artifact Creature — Moonfolk Rogue",
        "Flying\nWhen this creature enters, tap target permanent.")]
    [InlineData("Oceanus Dragon", "Creature — Dragon",
        "Flying\nWhen this creature enters, tap target creature an opponent controls. Goad it. (Until your "
        + "next turn, that creature attacks each combat if able and attacks a player other than you if able.)")]
    [InlineData("Ultimecia, Temporal Threat", "Legendary Creature — Human Warlock",
        "When Ultimecia enters, tap all creatures your opponents control.\nWhenever a creature you control "
        + "deals combat damage to a player, draw a card.")]
    [InlineData("Thundermare", "Creature — Elemental Horse",
        "Haste (This creature can attack and {T} as soon as it comes under your control.)\nWhen this creature "
        + "enters, tap all other creatures.")]
    [InlineData("Assassin Gauntlet", "Artifact — Equipment",
        "When this Equipment enters, attach it to up to one target creature you control. Tap all creatures "
        + "target opponent controls.\nEquipped creature gets +1/+1 and has \"Whenever this creature deals combat "
        + "damage to a player, draw a card, then discard a card.\"\nEquip {2}")]
    [InlineData("Tiller Engine", "Artifact Creature — Construct",
        "Whenever a land you control enters tapped, choose one —\n• Untap that land.\n• Tap target nonland "
        + "permanent an opponent controls.")]
    [InlineData("Crystal Fragments // Summon: Alexander", "Artifact — Equipment // Enchantment Creature — Saga Construct",
        "Equipped creature gets +1/+1.\n{5}{W}{W}: Exile this Equipment, then return it to the battlefield "
        + "transformed under its owner's control. Activate only as a sorcery.\nEquip {1}\n(As this Saga enters "
        + "and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — Prevent all damage that "
        + "would be dealt to creatures you control this turn.\nIII — Tap all creatures your opponents "
        + "control.\nFlying")]
    [InlineData("Word of Binding", "Sorcery",
        "Tap X target creatures.")]
    [InlineData("Tidal Surge", "Sorcery",
        "Tap up to three target creatures without flying.")]
    [InlineData("Winter Blast", "Sorcery",
        "Tap X target creatures. Winter Blast deals 2 damage to each of those creatures with flying.")]
    [InlineData("Blinding Light", "Sorcery",
        "Tap all nonwhite creatures.")]
    [InlineData("Falling Star", "Sorcery",
        "Flip Falling Star onto the playing area from a height of at least one foot. Falling Star deals 3 "
        + "damage to each creature it lands on. Tap all creatures dealt damage by Falling Star. If Falling Star "
        + "doesn't turn completely over at least once during the flip, it has no effect.")]
    [InlineData("Imperial Subduer", "Creature — Human Samurai",
        "Whenever a Samurai or Warrior you control attacks alone, tap target creature you don't control.")]
    [InlineData("Haystack", "Artifact",
        "{2}, {T}: Target creature you control phases out. (Treat it and anything attached to it as though "
        + "they don't exist until your next turn.)")]
    [InlineData("Doomed Artisan", "Creature — Human Artificer",
        "Sculptures you control can't attack or block.\nAt the beginning of your end step, create a colorless "
        + "Sculpture artifact creature token with \"This token's power and toughness are each equal to the "
        + "number of Sculptures you control.\"")]
    [InlineData("Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar", "Legendary Creature — Moonfolk Wizard // Legendary Planeswalker — Tamiyo",
        "Flying\nWhenever Tamiyo attacks, investigate. (Create a Clue token. It's an artifact with \"{2}, "
        + "Sacrifice this token: Draw a card.\")\nWhen you draw your third card in a turn, exile Tamiyo, then "
        + "return her to the battlefield transformed under her owner's control.\n+2: Until your next turn, "
        + "whenever a creature attacks you or a planeswalker you control, it gets -1/-0 until end of turn.\n−3: "
        + "Return target instant or sorcery card from your graveyard to your hand. If it's a green card, add "
        + "one mana of any color.\n−7: Draw cards equal to half the number of cards in your library, rounded up. "
        + "You get an emblem with \"You have no maximum hand size.\"")]
    // A sorcery's forecast taps in your own upkeep alone: every line of a sorcery
    // is sorcery speed.
    [InlineData("Piercing Rays", "Sorcery",
        "Exile target tapped creature.\nForecast — {2}{W}, Reveal this card from your hand: Tap target "
        + "untapped creature. (Activate this ability only during your upkeep and only once each turn.)")]
    // Landfall written with its ability word is the same land of yours entering.
    [InlineData("Makindi Ox", "Creature — Ox",
        "Landfall — Whenever a land you control enters, tap target creature an opponent controls.")]
    public void Classify_TheOctober27PacifyPass_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Read 2026-11-05: a creature turned into a vanilla 0-2/1-1 body keeps nothing
    // worth blocking or attacking with (Witness Protection, the same family as the
    // base 0/1 and 1/1 auras), a tap that also keeps it tapped through the next
    // untap (Scion of Stygia, whose pronoun is the creature chosen, not an
    // enchanted one), and a phase-out of other creatures (Spectral Adversary, also
    // Protection).
    [InlineData("Witness Protection", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature loses all abilities and is a green and white Citizen creature "
        + "with base power and toughness 1/1 named Legitimate Businessperson. (It loses all other colors, card "
        + "types, creature types, and names.)")]
    [InlineData("Scion of Stygia", "Creature — Tiefling Shaman",
        "Flash\nCone of Cold — When this creature enters, choose target creature an opponent controls, then "
        + "roll a d20.\n1—9 | Tap that creature.\n10—20 | Tap that creature. It doesn't untap during its "
        + "controller's next untap step.")]
    [InlineData("Spectral Adversary", "Creature — Spirit",
        "Flash\nFlying\nWhen this creature enters, you may pay {1}{U} any number of times. When you pay this "
        + "cost one or more times, put that many +1/+1 counters on this creature, then up to that many other "
        + "target artifacts, creatures, and/or enchantments phase out.")]
    public void Classify_TheNovember5PacifyPass_IsPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Read 2026-11-05 under Pacify.md's ruling that tapping on your own attack
    // trigger is a combat trick and not a lock: a boast (Goldmaw Champion, untagged
    // by hand) and "activate only if <name> is attacking" (Gerrard Capashen,
    // unreviewed; its stored Pacify is an old proposal) are the same window.
    [InlineData("Goldmaw Champion", "Creature — Dwarf Warrior",
        "Boast — {1}{W}: Tap target creature. (Activate only if this creature attacked this turn and only "
        + "once each turn.)")]
    // Gerrard Capashen's activation window, unreviewed: the same reading.
    [InlineData("Gerrard Capashen", "Legendary Creature — Human Soldier",
        "At the beginning of your upkeep, you gain 1 life for each card in target opponent's hand.\n{3}{W}: "
        + "Tap target creature. Activate only if Gerrard Capashen is attacking.")]
    public void Classify_TheNovember5BoastTap_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }
}
