using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // A copy of the next spell cast or the next ability activated, with new
    // targets, is Redirect, read 2026-10-30: Tzaangor Shaman (tagged 10-27;
    // Flamehold Grappler handed back) and Dynaheir, Invoker Adept (2 of 2 with Pit
    // Automaton).
    [InlineData("Tzaangor Shaman", "Creature — Mutant Shaman",
        "Flying\nSorcerous Elixir — Whenever this creature deals combat damage to a player, copy the next "
        + "instant or sorcery spell you cast this turn when you cast it. You may choose new targets for the "
        + "copy.")]
    [InlineData("Flamehold Grappler", "Creature — Human Monk",
        "First strike\nWhen this creature enters, copy the next spell you cast this turn when you cast it. You "
        + "may choose new targets for the copy. (A copy of a permanent spell becomes a token.)")]
    [InlineData("Dynaheir, Invoker Adept", "Legendary Creature — Human Wizard",
        "Haste\nYou may activate abilities of other creatures you control as though those creatures had "
        + "haste.\n{T}: When you next activate an ability that isn't a mana ability this turn by spending four "
        + "or more mana to activate it, copy that ability. You may choose new targets for the copy.")]
    public void Classify_TheNextSpellOrAbilityCopied_IsRedirect(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
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
    // Read 2026-11-09 under the definition (Redirect, docs/ontology/Redirect.md):
    // an exchange of control takes the spell (Sudden Substitution, Steal for the
    // creature), a target spell chosen and copied in the next sentence (Tempt with
    // Mayhem), and a copy aimed at the chosen permanent (Exterminator Magmarch,
    // Zevlor).
    [InlineData("Sudden Substitution", "Instant",
        "Split second (As long as this spell is on the stack, players can't cast spells or activate abilities "
        + "that aren't mana abilities.)\nExchange control of target noncreature spell and target creature. Then "
        + "the spell's controller may choose new targets for it.")]
    [InlineData("Tempt with Mayhem", "Instant",
        "Tempting offer — Choose target instant or sorcery spell. Each opponent may copy that spell and may "
        + "choose new targets for the copy they control. You copy that spell once plus an additional time for "
        + "each opponent who copied the spell this way. You may choose new targets for the copies you control.")]
    [InlineData("Exterminator Magmarch", "Artifact Creature — Phyrexian Construct",
        "Whenever you cast an instant or sorcery spell that targets only a single nonland permanent an "
        + "opponent controls, if another opponent controls one or more nonland permanents that spell could "
        + "target, choose one of those permanents. Copy that spell. The copy targets the chosen "
        + "permanent.\n{1}{B}: Regenerate this creature.")]
    [InlineData("Zevlor, Elturel Exile", "Legendary Creature — Tiefling Warrior",
        "Haste\n{2}, {T}: When you next cast an instant or sorcery spell that targets only a single opponent "
        + "or a single permanent an opponent controls this turn, for each other opponent, choose that player or "
        + "a permanent they control, copy that spell, and the copy targets the chosen player or permanent.")]
    public void Classify_ActingOnASpellWithoutCounteringIt_IsRedirect(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
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
    // Reached 2026-11-09 by the definition, reviewed before the ruling of
    // 09-28 and handed back: "you may copy it" (Spider-Verse, confirmed in the
    // sitting of 11-10). Ulalek, Feather and Frontline Heroism were here too
    // and moved below with the ruling of 11-10 evening.
    [InlineData("Spider-Verse", "Enchantment",
        "The \"legend rule\" doesn't apply to Spiders you control.\nWhenever you cast a spell from anywhere "
        + "other than your hand, you may copy it. If you do, you may choose new targets for the copy. If the "
        + "copy is a permanent spell, it gains haste. Do this only once each turn.")]
    public void Classify_CopyingYourOwnSpell_IsRedirect(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }

    [Theory]
    // Ruled 2026-11-10 evening, overturning the readings of 11-09 that the
    // three cards were pinned by above. R2: a copy aimed at "that creature /
    // token" the trigger picked off your own spell on your own creature can
    // only land on one of yours (Feather, Frontline Heroism, named). R3 ("Sia
    // 1 che 2"): a tribal trigger is never Redirect, and copying everything
    // you control is a doubler (Ulalek, named).
    [InlineData("Feather, Radiant Arbiter", "Legendary Creature — Angel",
        "Flying, lifelink\nWhenever you cast a noncreature spell that targets only Feather, you may choose any "
        + "number of other creatures that spell could target and pay {2} for each of those creatures. If you "
        + "do, for each of those creatures, copy that spell. The copy targets that creature. (Copies of "
        + "permanent spells become tokens.)")]
    [InlineData("Frontline Heroism", "Enchantment",
        "When this enchantment enters, create a 1/1 red Soldier creature token with haste.\nWhenever you cast "
        + "a spell that targets only a single creature you control, create a 1/1 red Soldier creature token "
        + "with haste, then copy that spell. The copy targets that token.")]
    [InlineData("Ulalek, Fused Atrocity", "Legendary Creature — Eldrazi",
        "Devoid (This card has no color.)\nWhenever you cast an Eldrazi spell, you may pay {C}{C}. If you do, "
        + "copy all spells you control, then copy all other activated and triggered abilities you control. You "
        + "may choose new targets for the copies. (Mana abilities can't be copied.)")]
    public void Classify_TheNovember10Ruling_ACopyOnlyOnYoursOrAllYouControl_IsNotRedirect(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
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
    // Ruled 2026-11-09 with the definition. Q1: an ability copied on EVERY
    // activation is a doubler like Panharmonicon, not a play on the stack (The
    // Enigma Jewel ruled -Redirect, Rowan's Talent untagged, 11 of 12). Q2:
    // demonstrate hands an opponent a copy too (The Twelfth Doctor). Q3: a copy
    // that can only target the card itself is the self rule (Ivy). A copied
    // ARTIFACT spell is always a permanent and becomes a token (Iron Man,
    // unreviewed).
    [InlineData("The Enigma Jewel // Locus of Enlightenment", "Legendary Artifact // Legendary Artifact",
        "The Enigma Jewel enters tapped.\n{T}: Add {C}{C}. Spend this mana only to activate abilities.\nCraft "
        + "with four or more nonlands with activated abilities {8}{U} ({8}{U}, Exile this artifact, Exile the "
        + "four or more from among other permanents you control and/or cards in your graveyard: Return this "
        + "card transformed under its owner's control. Craft only as a sorcery.)\nLocus of Enlightenment has "
        + "each activated ability of the exiled cards used to craft it. You may activate each of those "
        + "abilities only once each turn.\nWhenever you activate an ability that isn't a mana ability, copy it. "
        + "You may choose new targets for the copy.")]
    [InlineData("Rowan's Talent", "Enchantment — Aura",
        "Enchant planeswalker\nEnchanted planeswalker has \"[+1]: Up to one target creature gets +2/+0 and "
        + "gains first strike and trample until end of turn.\"\nWhenever you activate a loyalty ability of "
        + "enchanted planeswalker, copy that ability. You may choose new targets for the copy.")]
    [InlineData("The Twelfth Doctor", "Legendary Creature — Time Lord Doctor",
        "The first spell you cast from anywhere other than your hand each turn has demonstrate. (When you "
        + "cast that spell, you may copy it. If you do, choose an opponent to also copy it. A copy of a "
        + "permanent spell becomes a token.)\nWhenever you copy a spell, put a +1/+1 counter on The Twelfth "
        + "Doctor.")]
    [InlineData("Ivy, Gleeful Spellthief", "Legendary Creature — Faerie Rogue",
        "Flying\nWhenever a player casts a spell that targets only a single creature other than Ivy, you may "
        + "copy that spell. The copy targets Ivy. (A copy of an Aura spell becomes a token.)")]
    [InlineData("Iron Man, Bleeding Edge", "Legendary Artifact Creature — Human Hero",
        "Flying\nWhenever you cast an artifact spell, you may copy it, except the copy isn't legendary. Do "
        + "this only once each turn. (The copy becomes a token.)")]
    public void Classify_OutsideTheRedirectDefinition_IsNotRedirect(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }

    [Theory]
    // Read 2026-11-12: your own spell that targets only this creature, copied, is
    // your own trick doubled.
    [InlineData("Reflective Golem", "Artifact Creature — Golem",
        "Whenever you cast an instant or sorcery spell that targets only this creature, you may pay {2}. If "
        + "you do, copy that spell. You may choose new targets for the copy.")]
    public void Classify_TheNovember12Readings_ASpellThatTargetsOnlyThisCreature_IsNotRedirect(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Redirect));
    }
}
