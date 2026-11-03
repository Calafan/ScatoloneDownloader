using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Validates the rule-based <see cref="EffectClassifier"/> against real card
/// oracle text. The classifier only PROPOSES (a human confirms), so these pin
/// the high-confidence cases, the deliberate narrow edges (mana denial that
/// destroys nothing, a land sacrifice paid as a drawback) and the wordings
/// that fooled an earlier draft, rather than every card.
/// </summary>
/// <remarks>
/// One file per tag: <c>EffectClassifierTests.{Tag}.cs</c> holds the tests
/// that pin that tag, filed under the first tag the test name asserts. This
/// file keeps the theories whose expected flags span several tags, and
/// <see cref="MakeCard"/>.
/// </remarks>
public sealed partial class EffectClassifierTests
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
    // "Destroy all lands" is land destruction alone: it answers no creature. It
    // was a wipe too from 2026-10-16, when the lands swept were a board, until
    // that ruling was overturned on 2026-10-17.
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
    // A hyphen in the mass return.
    [InlineData("Jin-Gitaxias // The Great Synthesis", "Legendary Creature — Phyrexian Praetor // Enchantment — Saga",
        CardEffect.Bounce,
        "Ward {2}\nWhenever you cast a noncreature spell with mana value 3 or greater, draw a card.\n{3}{U}: "
        + "Exile Jin-Gitaxias, then return it to the battlefield transformed under its owner's control. Activate "
        + "only as a sorcery and only if you have seven or more cards in hand.\n(As this Saga enters and after "
        + "your draw step, add a lore counter.)\nI — Draw cards equal to the number of cards in your hand. You "
        + "have no maximum hand size for as long as you control this Saga.\nII — Return all non-Phyrexian "
        + "creatures to their owners' hands.\nIII — You may cast any number of spells from your hand without "
        + "paying their mana costs. Exile this Saga, then return it to the battlefield (front face up).")]
    // "+1/+1" in the mass return, both tags.
    [InlineData("Wave Goodbye", "Sorcery", CardEffect.Bounce,
        "Return each creature without a +1/+1 counter on it to its owner's hand.")]
    [InlineData("Wave Goodbye", "Sorcery", CardEffect.Wipe,
        "Return each creature without a +1/+1 counter on it to its owner's hand.")]
    // Fourth from the top.
    [InlineData("Lost to Legend", "Instant", CardEffect.Bounce,
        "Put target nonland historic permanent into its owner's library fourth from the top. (Artifacts, "
        + "legendaries, and Sagas are historic.)")]
    // The permanent an opponent chooses, then the rest shuffled away.
    [InlineData("Teferi, Temporal Pilgrim", "Legendary Planeswalker — Teferi", CardEffect.Bounce,
        "Whenever you draw a card, put a loyalty counter on Teferi.\n0: Draw a card.\n−12: Target opponent "
        + "chooses a permanent they control and returns it to its owner's hand. Then they shuffle each nonland "
        + "permanent they control into its owner's library.")]
    [InlineData("Teferi, Temporal Pilgrim", "Legendary Planeswalker — Teferi", CardEffect.Wipe,
        "Whenever you draw a card, put a loyalty counter on Teferi.\n0: Draw a card.\n−12: Target opponent "
        + "chooses a permanent they control and returns it to its owner's hand. Then they shuffle each nonland "
        + "permanent they control into its owner's library.")]
    // All but the chosen destroyed.
    [InlineData("Mount Doom", "Legendary Land", CardEffect.Wipe,
        "{T}, Pay 1 life: Add {B} or {R}.\n{1}{B}{R}, {T}: Mount Doom deals 1 damage to each opponent.\n"
        + "{5}{B}{R}, {T}, Sacrifice Mount Doom and a legendary artifact: Choose up to two creatures, then "
        + "destroy the rest. Activate only as a sorcery.")]
    [InlineData("Duneblast", "Sorcery", CardEffect.Wipe, "Choose up to one creature. Destroy the rest.")]
    // Any number of tapped creatures turned face down.
    [InlineData("Illithid Harvester // Plant Tadpoles", "Creature — Horror // Sorcery — Adventure", CardEffect.Wipe,
        "Ceremorphosis — When this creature enters, turn any number of target tapped nontoken creatures face "
        + "down. They're 2/2 Horror creatures.\nTap X target creatures. They don't untap during their "
        + "controllers' next untap steps. (Then exile this card. You may cast the creature later from exile.)")]
    public void Classify_TheOctoberBounceAndWipeGaps_CarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // The small reading gaps of the 2026-10-13 plan, read on 2026-10-15.
    // The enchantment second in a type list.
    [InlineData("Get Lost", "Instant", CardEffect.Disenchant,
        "Destroy target creature, enchantment, or planeswalker. Its controller creates two Map tokens. (They're "
        + "artifacts with \"{1}, {T}, Sacrifice this token: Target creature you control explores. Activate only "
        + "as a sorcery.\")")]
    // "Up to one OTHER target".
    [InlineData("Vault 13: Dweller's Journey", "Enchantment — Saga", CardEffect.Disenchant,
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — For each "
        + "player, exile up to one other target enchantment or creature that player controls until this Saga "
        + "leaves the battlefield.\nII — You gain 2 life and scry 2.\nIII — Return two cards exiled with this "
        + "Saga to the battlefield under their owners' control and put the rest on the bottom of their owners' "
        + "libraries.")]
    // The enchantment its controller exiles.
    [InlineData("Debt to the Kami", "Instant", CardEffect.Disenchant,
        "Choose one —\n• Target opponent exiles a creature they control.\n• Target opponent exiles an "
        + "enchantment they control.")]
    // A nonbasic land at the end of a longer list.
    [InlineData("Ray of Ruin", "Sorcery", CardEffect.LandDestruction,
        "Exile target creature, Vehicle, or nonbasic land. Scry 1. (Look at the top card of your library. You "
        + "may put that card on the bottom.)")]
    // The top of their library exiled, and nothing played from it.
    [InlineData("Ulamog, the Defiler", "Legendary Creature — Eldrazi", CardEffect.Mill,
        "When you cast this spell, target opponent exiles the top half of their library, rounded up.\nWard—"
        + "Sacrifice two permanents.\nUlamog enters with a number of +1/+1 counters on it equal to the greatest "
        + "mana value among cards in exile.\nUlamog has annihilator X, where X is the number of +1/+1 counters "
        + "on it.")]
    // …and the card cast face down is the creature itself, not the exiled one.
    [InlineData("Raven Guild Master", "Creature — Human Wizard Mutant", CardEffect.Mill,
        "Whenever this creature deals combat damage to a player, that player exiles the top ten cards of their "
        + "library.\nMorph {2}{U}{U} (You may cast this card face down as a 2/2 creature for {3}. Turn it face up "
        + "any time for its morph cost.)")]
    // Three cards discarded on a villainous choice.
    [InlineData("Great Intelligence's Plan", "Sorcery", CardEffect.Discard,
        "Draw three cards. Then target opponent faces a villainous choice — They discard three cards, or you may "
        + "cast a spell from your hand without paying its mana cost.")]
    // "They exile cards from their hand" once the player is named.
    [InlineData("Isildur's Fateful Strike", "Legendary Instant", CardEffect.Discard,
        "(You may cast a legendary instant only if you control a legendary creature or planeswalker.)\nDestroy "
        + "target creature. If its controller has more than four cards in hand, they exile cards from their hand "
        + "equal to the difference.")]
    // The card you chose from their hand, even though they may still play it.
    [InlineData("Invasion of Gobakhan // Lightshield Array", "Battle — Siege // Enchantment", CardEffect.Discard,
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's defeated, "
        + "exile it, then cast it transformed.)\nWhen this Siege enters, look at target opponent's hand. You may "
        + "exile a nonland card from it. For as long as that card remains exiled, its owner may play it. A spell "
        + "cast this way costs {2} more to cast.\nAt the beginning of your end step, put a +1/+1 counter on each "
        + "creature that attacked this turn.\nSacrifice this enchantment: Creatures you control gain hexproof and "
        + "indestructible until end of turn.")]
    // Any number of targets, "and/or".
    [InlineData("Display of Power", "Instant", CardEffect.Redirect,
        "This spell can't be copied.\nCopy any number of target instant and/or sorcery spells. You may choose "
        + "new targets for the copies.")]
    // Up to three OTHER target nonland permanents.
    [InlineData("Soul of Emancipation", "Creature — Avatar", CardEffect.RemovePermanent,
        "When this creature enters, destroy up to three other target nonland permanents. For each of those "
        + "permanents, its controller creates a 3/3 white Angel creature token with flying.")]
    // The edict whose victim shares the card type you chose.
    [InlineData("Fatal Grudge", "Sorcery", CardEffect.RemovePermanent,
        "As an additional cost to cast this spell, sacrifice a nonland permanent.\nEach opponent chooses a "
        + "permanent they control that shares a card type with the sacrificed permanent and sacrifices it.\nDraw "
        + "a card.")]
    public void Classify_TheOctoberSmallGaps_CarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // A permanent you own or control is no answer.
    [InlineData("Staff of Compleation", "Artifact", CardEffect.RemovePermanent,
        "{T}, Pay 1 life: Destroy target permanent you own.\n{T}, Pay 2 life: Add one mana of any color.\n{T}, "
        + "Pay 3 life: Proliferate.\n{T}, Pay 4 life: Draw a card.\n{5}: Untap this artifact.")]
    [InlineData("Displacer Kitten", "Creature — Cat Beast", CardEffect.RemovePermanent,
        "Avoidance — Whenever you cast a noncreature spell, exile up to one target nonland permanent you "
        + "control, then return that card to the battlefield under its owner's control.")]
    // An Aura attached to this creature is not a creature sacrificed.
    [InlineData("Faunsbane Troll", "Creature — Troll", CardEffect.Sacrifice,
        "When this creature enters, create a Monster Role token attached to it. (Enchanted creature gets +1/+1 "
        + "and has trample.)\n{1}, Sacrifice an Aura attached to this creature: This creature fights target "
        + "creature you don't control. If that creature would die this turn, exile it instead. Activate only as "
        + "a sorcery.")]
    // The top of their library exiled and then played is Steal: cast on the
    // same line, before the exile on a villainous choice, played as "those
    // cards", put under your control, cast on the next line.
    [InlineData("Crabomination", "Creature — Crab Demon", CardEffect.Mill,
        "Emerge from artifact {5}{B}{B} (You may cast this spell by sacrificing an artifact and paying the emerge "
        + "cost reduced by that artifact's mana value.)\nWhen this creature enters, target opponent exiles the "
        + "top card of their library, a card at random from their graveyard, and a card at random from their "
        + "hand. You may cast a spell from among cards exiled this way without paying its mana cost.")]
    [InlineData("Ensnared by the Mara", "Sorcery", CardEffect.Mill,
        "Each opponent faces a villainous choice — They exile cards from the top of their library until they "
        + "exile a nonland card, then you may cast that card without paying its mana cost, or that player exiles "
        + "the top four cards of their library and Ensnared by the Mara deals damage equal to the total mana "
        + "value of those exiled cards to that player.")]
    [InlineData("Outrageous Robbery", "Instant", CardEffect.Mill,
        "Target opponent exiles the top X cards of their library face down. You may look at and play those "
        + "cards for as long as they remain exiled. If you cast a spell this way, you may spend mana as though "
        + "it were mana of any type to cast it.")]
    [InlineData("Oblivion Sower", "Creature — Eldrazi", CardEffect.Mill,
        "When you cast this spell, target opponent exiles the top four cards of their library, then you may put "
        + "any number of land cards that player owns from exile onto the battlefield under your control.")]
    [InlineData("Nightveil Specter", "Creature — Specter", CardEffect.Mill,
        "Flying\nWhenever this creature deals combat damage to a player, that player exiles the top card of "
        + "their library.\nYou may play lands and cast spells from among cards exiled with this creature.")]
    // …and a token's quoted ability is the token's.
    [InlineData("Ashiok, Nightmare Muse", "Legendary Planeswalker — Ashiok", CardEffect.Mill,
        "+1: Create a 2/3 blue and black Nightmare creature token with \"Whenever this token attacks or blocks, "
        + "each opponent exiles the top two cards of their library.\"\n−3: Return target nonland permanent to "
        + "its owner's hand, then that player exiles a card from their hand.\n−7: You may cast up to three "
        + "spells from among face-up cards your opponents own from exile without paying their mana costs.")]
    public void Classify_TheOctoberSmallGaps_DoNotCarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // RemovePermanent and Mill, read 2026-10-15. A permanent narrowed by more
    // than "nonland".
    [InlineData("Commander Sofia Daguerre", "Legendary Creature — Human Pilot", CardEffect.RemovePermanent,
        "Flash\nCrash Landing — When Commander Sofia Daguerre enters, destroy up to one target legendary "
        + "permanent. That permanent's controller creates a Junk token. (It's an artifact with \"{T}, Sacrifice "
        + "this token: Exile the top card of your library. You may play that card this turn. Activate only as a "
        + "sorcery.\")")]
    [InlineData("Extinguisher Battleship", "Artifact — Spacecraft", CardEffect.RemovePermanent,
        "When this Spacecraft enters, destroy target noncreature permanent. Then this Spacecraft deals 4 damage "
        + "to each creature.\nStation (Tap another creature you control: Put charge counters equal to its power "
        + "on this Spacecraft. Station only as a sorcery. It's an artifact creature at 5+.)\n5+ | Flying, trample")]
    [InlineData("Thief of Existence", "Creature — Eldrazi", CardEffect.RemovePermanent,
        "Devoid (This card has no color.)\nWhen you cast this spell, exile up to one target noncreature, nonland "
        + "permanent an opponent controls with mana value 4 or less. If you do, Thief of Existence gains \"When "
        + "this creature leaves the battlefield, target opponent draws a card.\"")]
    // A token is a permanent.
    [InlineData("Kraul Whipcracker", "Creature — Insect Assassin", CardEffect.RemovePermanent,
        "Reach\nWhen this creature enters, destroy target token an opponent controls.")]
    // Named by its mana value, destroyed on the reflexive trigger.
    [InlineData("Behemoth of Vault 0", "Artifact Creature — Robot", CardEffect.RemovePermanent,
        "Trample\nWhen this creature enters, you get {E}{E}{E}{E} (four energy counters).\nWhen this creature "
        + "dies, you may pay an amount of {E} equal to target nonland permanent's mana value. When you do, "
        + "destroy that permanent.")]
    // Chosen by each player among yours.
    [InlineData("The Legend of Yangchen // Avatar Yangchen", "Enchantment — Saga // Legendary Creature — Avatar",
        CardEffect.RemovePermanent,
        "(As this Saga enters and after your draw step, add a lore counter.)\nI — Starting with you, each player "
        + "chooses up to one permanent with mana value 3 or greater from among permanents your opponents control. "
        + "Exile those permanents.\nII — You may have target opponent draw three cards. If you do, draw three "
        + "cards.\nIII — Exile this Saga, then return it to the battlefield transformed under your control.")]
    // A return further off is not the blink.
    [InlineData("Unyielding Gatekeeper", "Creature — Elephant Cleric", CardEffect.RemovePermanent,
        "Disguise {1}{W} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it face "
        + "up any time for its disguise cost.)\nWhen this creature is turned face up, exile another target "
        + "nonland permanent. If you controlled it, return it to the battlefield tapped. Otherwise, its "
        + "controller creates a 2/2 white and blue Detective creature token.")]
    // Rad counters handed to a player.
    [InlineData("Glowing One", "Creature — Zombie Mutant", CardEffect.Mill,
        "Deathtouch\nWhenever this creature deals combat damage to a player, they get four rad counters.\n"
        + "Whenever a player mills a nonland card, you gain 1 life.")]
    [InlineData("Bloatfly Swarm", "Creature — Insect Mutant", CardEffect.Mill,
        "Flying\nThis creature enters with five +1/+1 counters on it.\nIf damage would be dealt to this "
        + "creature while it has a +1/+1 counter on it, prevent that damage, remove that many +1/+1 counters "
        + "from it, then give each player a rad counter for each +1/+1 counter removed this way.")]
    public void Classify_TheOctoberRemovePermanentAndMillGaps_CarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // A permanent CARD is in a graveyard.
    [InlineData("Savvy Trader", "Creature — Human Citizen", CardEffect.RemovePermanent,
        "When this creature enters, exile target permanent card from your graveyard. You may play that card for "
        + "as long as it remains exiled.\nSpells you cast from anywhere other than your hand cost {1} less to "
        + "cast.")]
    // A blink whose return follows straight on.
    [InlineData("Eldrazi Confluence", "Instant", CardEffect.RemovePermanent,
        "Choose three. You may choose the same mode more than once.\n• Target creature gets +3/-3 until end of "
        + "turn.\n• Exile target nonland permanent, then return it to the battlefield tapped under its owner's "
        + "control.\n• Create a 1/1 colorless Eldrazi Scion creature token with \"Sacrifice this token: Add "
        + "{C}.\"")]
    [InlineData("Blizzard Strix", "Snow Creature — Bird", CardEffect.RemovePermanent,
        "Flash\nFlying\nWhen this creature enters, if you control another snow permanent, exile target "
        + "permanent other than this creature. Return that card to the battlefield under its owner's control at "
        + "the beginning of the next end step.")]
    // The exile handed back to the owners' hands.
    [InlineData("The Spot, Living Portal", "Legendary Creature — Human Scientist Villain", CardEffect.RemovePermanent,
        "When The Spot enters, exile up to one target nonland permanent and up to one target nonland permanent "
        + "card from a graveyard.\nWhen The Spot dies, put him on the bottom of his owner's library. If you do, "
        + "return the exiled cards to their owners' hands.")]
    // Rad counters YOU get.
    [InlineData("Contaminated Drink", "Instant", CardEffect.Mill,
        "Draw X cards, then you get half X rad counters, rounded up.")]
    [InlineData("Tato Farmer", "Creature — Zombie Mutant Peasant", CardEffect.Mill,
        "Landfall — Whenever a land you control enters, you may get two rad counters.\n{T}: Put target land card "
        + "in a graveyard that was milled this turn onto the battlefield tapped under your control.")]
    public void Classify_TheOctoberRemovePermanentAndMillGaps_DoNotCarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // The Ramp and ManaFixing gaps of the 2026-10-13 plan, read 2026-10-15.
    // A discount for other spells in colour, with a fallback, or of {X} (ruled 2026-10-15).
    [InlineData("Defiler of Flesh", "Creature — Phyrexian Horror", CardEffect.Ramp,
        "Menace\nAs an additional cost to cast black permanent spells, you may pay 2 life. Those spells cost "
        + "{B} less to cast if you paid life this way. This effect reduces only the amount of black mana you "
        + "pay.\nWhenever you cast a black permanent spell, target creature you control gets +1/+1 and gains "
        + "menace until end of turn.")]
    [InlineData("Eluge, the Shoreless Sea", "Legendary Creature — Elemental Fish", CardEffect.Ramp,
        "Eluge's power and toughness are each equal to the number of Islands you control.\nWhenever Eluge "
        + "enters or attacks, put a flood counter on target land. It's an Island in addition to its other types "
        + "for as long as it has a flood counter on it.\nThe first instant or sorcery spell you cast each turn "
        + "costs {U} (or {1}) less to cast for each land you control with a flood counter on it.")]
    [InlineData("Will, Scion of Peace", "Legendary Creature — Human Wizard", CardEffect.Ramp,
        "Vigilance\n{T}: Spells you cast this turn that are white and/or blue cost {X} less to cast, where X "
        + "is the amount of life you gained this turn. Activate only as a sorcery.")]
    // A colour chosen first, mana handed to "target player", more than five.
    [InlineData("Radiant Lotus", "Artifact", CardEffect.Ramp,
        "{T}, Sacrifice one or more artifacts: Choose a color. Target player adds three mana of the chosen "
        + "color for each artifact sacrificed this way. (Activate only as an instant.)")]
    [InlineData("The Warring Triad", "Legendary Artifact Creature — God", CardEffect.Ramp,
        "Flying, trample, haste\nAs long as there are fewer than eight cards in your graveyard, The Warring "
        + "Triad isn't a creature.\n{T}, Mill a card: Target player adds one mana of any color. (Activate only "
        + "as an instant.)")]
    [InlineData("The Aetherspark", "Legendary Artifact Planeswalker — Equipment", CardEffect.Ramp,
        "As long as The Aetherspark is attached to a creature, The Aetherspark can't be attacked and has "
        + "\"Whenever equipped creature deals combat damage during your turn, put that many loyalty counters on "
        + "The Aetherspark.\"\n+1: Attach The Aetherspark to up to one target creature you control. Put a +1/+1 "
        + "counter on that creature.\n−5: Draw two cards.\n−10: Add ten mana of any one color.")]
    // Ten Treasures; mana tripled or doubled.
    [InlineData("Ancient Adamantoise", "Creature — Turtle", CardEffect.Ramp,
        "Vigilance, ward {3}\nDamage isn't removed from this creature during cleanup steps.\nAll damage that "
        + "would be dealt to you and other permanents you control is dealt to this creature instead.\nWhen this "
        + "creature dies, exile it and create ten tapped Treasure tokens.")]
    [InlineData("Virtue of Strength // Garenbrig Growth", "Enchantment // Sorcery — Adventure", CardEffect.Ramp,
        "If you tap a basic land for mana, it produces three times as much of that mana instead.\nReturn "
        + "target creature or land card from your graveyard to your hand. (Then exile this card. You may cast "
        + "the enchantment later from exile.)")]
    [InlineData("Mana Reflection", "Enchantment", CardEffect.Ramp,
        "If you tap a permanent for mana, it produces twice as much of that mana instead.")]
    // A land creature token; copies of a target land.
    [InlineData("Awaken the Woods", "Sorcery", CardEffect.Ramp,
        "Create X 1/1 green Forest Dryad land creature tokens. (They're affected by summoning sickness.)")]
    [InlineData("Rebuild the City", "Sorcery", CardEffect.Ramp,
        "Choose target land. Create three tokens that are copies of it, except they're 3/3 creatures in "
        + "addition to their other types and they have vigilance and menace. (They're affected by summoning "
        + "sickness.)")]
    // A transformed land face that taps for more than one.
    [InlineData("Matzalantli, the Great Door // The Core", "Legendary Artifact // Legendary Land", CardEffect.Ramp,
        "{T}: Draw a card, then discard a card.\n{4}, {T}: Transform Matzalantli. Activate only if there are "
        + "four or more permanent types among cards in your graveyard. (Artifact, battle, creature, "
        + "enchantment, land, and planeswalker are permanent types.)\n(Transforms from "
        + "Matzalantli.)\nFathomless descent — {T}: Add X mana of any one color, where X is the number of "
        + "permanent cards in your graveyard.")]
    [InlineData("Golden Guardian // Gold-Forge Garrison", "Artifact Creature — Golem // Land", CardEffect.Ramp,
        "Defender\n{2}: This creature fights another target creature you control. When this creature dies "
        + "this turn, return it to the battlefield transformed under your control.\n(Transforms from Golden "
        + "Guardian.)\n{T}: Add two mana of any one color.\n{4}, {T}: Create a 4/4 colorless Golem artifact "
        + "creature token.")]
    // A land sacrificed as it enters, fetching on the reflexive trigger.
    [InlineData("Brokers Hideout", "Land", CardEffect.ManaFixing,
        "When this land enters, sacrifice it. When you do, search your library for a basic Forest, Plains, or "
        + "Island card, put it onto the battlefield tapped, then shuffle and you gain 1 life.")]
    // A chosen colour beside a fixed one; the colours of what it exiled.
    [InlineData("Citadel Gate", "Land — Gate", CardEffect.ManaFixing,
        "This land enters tapped.\nAs this land enters, choose a color other than white.\n{T}: Add {W} or one "
        + "mana of the chosen color.")]
    [InlineData("Pit of Offerings", "Land — Cave", CardEffect.ManaFixing,
        "This land enters tapped.\nWhen this land enters, exile up to three target cards from "
        + "graveyards.\n{T}: Add {C}.\n{T}: Add one mana of any of the exiled cards' colors.")]
    // Lands made every type.
    [InlineData("Leyline of the Guildpact", "Enchantment", CardEffect.ManaFixing,
        "If this card is in your opening hand, you may begin the game with it on the battlefield.\nEach "
        + "nonland permanent you control is all colors.\nLands you control are every basic land type in "
        + "addition to their other types.")]
    [InlineData("Energybending", "Instant — Lesson", CardEffect.ManaFixing,
        "Lands you control gain all basic land types until end of turn.\nDraw a card.")]
    [InlineData("Omo, Queen of Vesuva", "Legendary Creature — Shapeshifter Noble", CardEffect.ManaFixing,
        "Whenever Omo enters or attacks, put an everything counter on each of up to one target land and up to "
        + "one target creature.\nEach land with an everything counter on it is every land type in addition to "
        + "its other types.\nEach nonland creature with an everything counter on it is every creature type.")]
    // One land for one; a card typed Treasure; a land put on top.
    [InlineData("Renewal", "Sorcery", CardEffect.ManaFixing,
        "As an additional cost to cast this spell, sacrifice a land.\nSearch your library for a basic land "
        + "card, put that card onto the battlefield, then shuffle.\nDraw a card at the beginning of the next "
        + "turn's upkeep.")]
    [InlineData("Buried Treasure", "Artifact — Treasure", CardEffect.ManaFixing,
        "{T}, Sacrifice this artifact: Add one mana of any color.\n{5}, Exile this card from your graveyard: "
        + "Discover 5. Activate only as a sorcery. (Exile cards from the top of your library until you exile a "
        + "nonland card with mana value 5 or less. Cast it without paying its mana cost or put it into your "
        + "hand. Put the rest on the bottom in a random order.)")]
    [InlineData("Silver Deputy", "Artifact Creature — Mercenary", CardEffect.ManaFixing,
        "When this creature enters, you may search your library for a basic land card or a Desert card, "
        + "reveal it, then shuffle and put it on top.\n{T}: Target creature you control gets +1/+0 until end of "
        + "turn. Activate only as a sorcery.")]
    // A choice of colours granted to a target land fixes.
    [InlineData("Spara's Adjudicators", "Creature — Cat Citizen", CardEffect.ManaFixing,
        "When this creature enters, target creature an opponent controls can't attack or block until your "
        + "next turn.\n{2}, Exile this card from your hand: Target land gains \"{T}: Add {G}, {W}, or {U}\" "
        + "until this card is cast from exile. You may cast this card for as long as it remains exiled.")]
    // The Treasure YOU make beside the one the opponent may make.
    [InlineData("Tempting Contract", "Artifact", CardEffect.ManaFixing,
        "At the beginning of your upkeep, each opponent may create a Treasure token. For each opponent who "
        + "does, you create a Treasure token.")]
    // A colour chosen, then "an amount of mana".
    [InlineData("Nyx Lotus", "Legendary Artifact", CardEffect.Ramp,
        "Nyx Lotus enters tapped.\n{T}: Choose a color. Add an amount of mana of that color equal to your "
        + "devotion to that color. (Your devotion to a color is the number of mana symbols of that color in the "
        + "mana costs of permanents you control.)")]
    public void Classify_TheOctoberRampAndManaFixingGaps_CarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // A discount on itself, {X} included.
    [InlineData("Fungal Colossus", "Creature — Fungus Beast", CardEffect.Ramp,
        "This spell costs {X} less to cast, where X is the number of differently named lands you control.")]
    // A transformed Temple that taps for one is just a land.
    [InlineData("Ojer Axonil, Deepest Might // Temple of Power", "Legendary Creature — God // Land", CardEffect.Ramp,
        "Trample\nIf a red source you control would deal an amount of noncombat damage less than Ojer "
        + "Axonil's power to an opponent, that source deals damage equal to Ojer Axonil's power instead.\nWhen "
        + "Ojer Axonil dies, return it to the battlefield tapped and transformed under its owner's "
        + "control.\n(Transforms from Ojer Axonil, Deepest Might.)\n{T}: Add {R}.\n{2}{R}, {T}: Transform this "
        + "land. Activate only if red sources you controlled dealt 4 or more noncombat damage this turn and "
        + "only as a sorcery.")]
    // A Treasure handed to somebody else.
    [InlineData("An Offer You Can't Refuse", "Instant", CardEffect.Ramp,
        "Counter target noncreature spell. Its controller creates two Treasure tokens. (They're artifacts "
        + "with \"{T}, Sacrifice this token: Add one mana of any color.\")")]
    [InlineData("An Offer You Can't Refuse", "Instant", CardEffect.ManaFixing,
        "Counter target noncreature spell. Its controller creates two Treasure tokens. (They're artifacts "
        + "with \"{T}, Sacrifice this token: Add one mana of any color.\")")]
    [InlineData("Erestor of the Council", "Legendary Creature — Elf Noble", CardEffect.ManaFixing,
        "Whenever players finish voting, each opponent who voted for a choice you voted for creates a "
        + "Treasure token. You scry X, where X is the number of opponents who voted for a choice you didn't "
        + "vote for. Draw a card.")]
    [InlineData("Blooming Blast", "Instant", CardEffect.ManaFixing,
        "Gift a Treasure (You may promise an opponent a gift as you cast this spell. If you do, they create a "
        + "Treasure token before its other effects. It's an artifact with \"{T}, Sacrifice this token: Add one "
        + "mana of any color.\")\nBlooming Blast deals 2 damage to target creature. If the gift was promised, "
        + "Blooming Blast also deals 3 damage to that creature's controller.")]
    // A card typed Treasure cracking itself.
    [InlineData("Buried Treasure", "Artifact — Treasure", CardEffect.Ramp,
        "{T}, Sacrifice this artifact: Add one mana of any color.\n{5}, Exile this card from your graveyard: "
        + "Discover 5. Activate only as a sorcery. (Exile cards from the top of your library until you exile a "
        + "nonland card with mana value 5 or less. Cast it without paying its mana cost or put it into your "
        + "hand. Put the rest on the bottom in a random order.)")]
    // One Treasure from an ability that sacrifices the card.
    [InlineData("Volatile Fault", "Land — Cave", CardEffect.Ramp,
        "{T}: Add {C}.\n{1}, {T}, Sacrifice this land: Destroy target nonbasic land an opponent controls. "
        + "That player may search their library for a basic land card, put it onto the battlefield, then "
        + "shuffle. You create a Treasure token.")]
    [InlineData("Contested Game Ball", "Artifact", CardEffect.Ramp,
        "Whenever you're dealt combat damage, the attacking player gains control of this artifact and untaps "
        + "it.\n{2}, {T}: Draw a card and put a point counter on this artifact. Then if it has five or more "
        + "point counters on it, sacrifice it and create a Treasure token.")]
    // The land you untap to animate.
    [InlineData("Silvanus's Invoker", "Creature — Dragon Druid", CardEffect.Ramp,
        "Conjure Elemental — {8}: Untap target land you control. It becomes an 8/8 Elemental creature with "
        + "trample and haste until end of turn. It's still a land.")]
    // One mana granted to lands.
    [InlineData("Dune Chanter", "Creature — Plant Druid", CardEffect.Ramp,
        "Reach\nLands you control and land cards you own that aren't on the battlefield are Deserts in "
        + "addition to their other types.\nLands you control have \"{T}: Add one mana of any color.\"\n{T}: "
        + "Mill two cards. You gain 1 life for each land card milled this way.")]
    [InlineData("Preston Garvey, Minuteman", "Legendary Creature — Human Soldier", CardEffect.Ramp,
        "At the beginning of combat on your turn, create a green Aura enchantment token named Settlement "
        + "attached to up to one target land you control. It has enchant land and \"Enchanted land has '{T}: "
        + "Add one mana of any color.'\"\nWhenever Preston Garvey attacks, untap each enchanted permanent you "
        + "control.")]
    [InlineData("Spara's Adjudicators", "Creature — Cat Citizen", CardEffect.Ramp,
        "When this creature enters, target creature an opponent controls can't attack or block until your "
        + "next turn.\n{2}, Exile this card from your hand: Target land gains \"{T}: Add {G}, {W}, or {U}\" "
        + "until this card is cast from exile. You may cast this card for as long as it remains exiled.")]
    // Lands sacrificed for the lands revealed.
    [InlineData("Hew the Entwood", "Sorcery", CardEffect.Ramp,
        "Sacrifice any number of lands. Reveal the top X cards of your library, where X is the number of "
        + "lands sacrificed this way. Choose any number of artifact and/or land cards revealed this way. Put "
        + "all nonland cards chosen this way onto the battlefield, then put all land cards chosen this way onto "
        + "the battlefield tapped, then put the rest on the bottom of your library in a random order.")]
    // A land sacrificing a creature for one mana.
    [InlineData("Lazotep Quarry", "Land — Desert", CardEffect.Ramp,
        "{T}: Add {C}.\n{T}, Sacrifice a creature: Add one mana of any color.\n{X}{2}, {T}, Sacrifice a "
        + "Desert: Exile target creature card with mana value X from your graveyard. Create a token that's a "
        + "copy of it, except it's a 4/4 black Zombie. Activate only as a sorcery.")]
    // Land tokens counted by commander casts.
    [InlineData("Jyoti, Moag Ancient", "Legendary Creature — Elemental", CardEffect.Ramp,
        "When Jyoti enters, create a 1/1 green Forest Dryad land creature token for each time you've cast "
        + "your commander from the command zone this game. (They're affected by summoning sickness.)\nAt the "
        + "beginning of each combat, land creatures you control get +X/+X until end of turn, where X is Jyoti's "
        + "power.")]
    // Lotus Vale gives two lands for its three.
    [InlineData("Lotus Vale", "Land", CardEffect.Ramp,
        "If this land would enter, sacrifice two untapped lands instead. If you do, put this land onto the "
        + "battlefield. If you don't, put it into its owner's graveyard.\n{T}: Add three mana of any one color.")]
    // Free mana on the enchanted land's tap, and to "target player".
    [InlineData("Blighted Burgeoning", "Enchantment — Aura", CardEffect.ManaFixing,
        "Enchant land\nWhen this Aura enters, incubate 2. (Create an Incubator token with two +1/+1 counters "
        + "on it and \"{2}: Transform this token.\" It transforms into a 0/0 Phyrexian artifact "
        + "creature.)\nWhenever enchanted land is tapped for mana, its controller adds an additional one mana "
        + "of any color.")]
    [InlineData("The Warring Triad", "Legendary Artifact Creature — God", CardEffect.ManaFixing,
        "Flying, trample, haste\nAs long as there are fewer than eight cards in your graveyard, The Warring "
        + "Triad isn't a creature.\n{T}, Mill a card: Target player adds one mana of any color. (Activate only "
        + "as an instant.)")]
    [InlineData("Bigger on the Inside", "Enchantment — Aura", CardEffect.ManaFixing,
        "Enchant artifact or land\nEnchanted permanent has \"{T}: Target player adds two mana of any one "
        + "color. The next spell they cast this turn has cascade.\" (When they cast their next spell, they "
        + "exile cards from the top of their library until they exile a nonland card that costs less. They may "
        + "cast it without paying its mana cost. They put the exiled cards on the bottom in a random order.)")]
    // A discount on itself in COLOUR.
    [InlineData("Khalni Hydra", "Creature — Hydra", CardEffect.Ramp,
        "This spell costs {G} less to cast for each green creature you control.\nTrample")]
    public void Classify_TheOctoberRampAndManaFixingGaps_DoNotCarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // A reanimation or a cheat only a creature TYPE can use is neither tag,
    // ruled 2026-10-15 ("Reanimate e Cheat non dovrebbero contare").
    // A creature TYPE out of the hand or the library, or cast free.
    [InlineData("Goblin Wizard", "Creature — Goblin Wizard", CardEffect.Cheat,
        "{T}: You may put a Goblin permanent card from your hand onto the battlefield.\n{R}: Target Goblin "
        + "gains protection from white until end of turn.")]
    [InlineData("Dracogenesis", "Enchantment", CardEffect.Cheat,
        "You may cast Dragon spells without paying their mana costs.")]
    [InlineData("Kaalia of the Vast", "Legendary Creature — Human Cleric", CardEffect.Cheat,
        "Flying\nWhenever Kaalia attacks an opponent, you may put an Angel, Demon, or Dragon creature card "
        + "from your hand onto the battlefield tapped and attacking that opponent.")]
    // A type shared with the enchanted creature. (Belbe's Portal's CHOSEN type
    // stood here until 2026-10-30, moved to the theory below.)
    [InlineData("Call to the Kindred", "Enchantment — Aura", CardEffect.Cheat,
        "Enchant creature\nAt the beginning of your upkeep, you may look at the top five cards of your "
        + "library. If you do, you may put a creature card that shares a creature type with enchanted creature "
        + "from among them onto the battlefield, then you put the rest of those cards on the bottom of your "
        + "library in any order.")]
    // Behind a tribal trigger, in the same sentence (ruled with Kiora).
    [InlineData("Kiora, Sovereign of the Deep", "Legendary Creature — Merfolk Noble", CardEffect.Cheat,
        "Vigilance, ward {3}\nWhenever you cast a Kraken, Leviathan, Octopus, or Serpent spell from your "
        + "hand, look at the top X cards of your library, where X is that spell's mana value. You may cast a "
        + "spell with mana value less than X from among them without paying its mana cost. Put the rest on the "
        + "bottom of your library in a random order.")]
    // Behind a cost only other members of the type pay.
    [InlineData("Shadow-Rite Priest", "Creature — Human Cleric", CardEffect.Cheat,
        "Other Clerics you control get +1/+1.\n{3}{B}{B}, {T}, Sacrifice another Cleric: Search your library "
        + "for a black creature card, put it onto the battlefield, then shuffle.")]
    [InlineData("Gravespawn Sovereign", "Creature — Zombie", CardEffect.Reanimate,
        "Tap five untapped Zombies you control: Put target creature card from a graveyard onto the "
        + "battlefield under your control.")]
    [InlineData("Meanders Guide", "Creature — Merfolk Scout", CardEffect.Reanimate,
        "Whenever this creature attacks, you may tap another untapped Merfolk you control. When you do, "
        + "return target creature card with mana value 3 or less from your graveyard to the battlefield.")]
    // A creature TYPE back from the graveyard: alone, in a list, and/or. (From
    // the Rubble's "of the chosen type" and Haunting Voyage's "of that type"
    // stood here until 2026-10-30, moved to the theory below.)
    [InlineData("Admiral Brass, Unsinkable", "Legendary Creature — Human Pirate", CardEffect.Reanimate,
        "When Admiral Brass enters, mill four cards.\nAt the beginning of combat on your turn, you may return "
        + "target Pirate creature card from your graveyard to the battlefield with a finality counter on it. It "
        + "has base power and toughness 4/4. It gains haste until end of turn. (If a creature with a finality "
        + "counter on it would die, exile it instead.)")]
    [InlineData("Queen's Bay Paladin", "Creature — Vampire Knight", CardEffect.Reanimate,
        "Whenever this creature enters or attacks, return up to one target Vampire card from your graveyard "
        + "to the battlefield with a finality counter on it. You lose life equal to its mana value. (If a "
        + "creature with a finality counter on it would die, exile it instead.)")]
    [InlineData("Bloodline Necromancer", "Creature — Vampire Wizard", CardEffect.Reanimate,
        "Lifelink\nWhen this creature enters, you may return target Vampire or Wizard creature card from your "
        + "graveyard to the battlefield.")]
    [InlineData("Celestial Gatekeeper", "Creature — Bird Cleric", CardEffect.Reanimate,
        "Flying\nWhen this creature dies, exile it, then return up to two target Bird and/or Cleric permanent "
        + "cards from your graveyard to the battlefield.")]
    [InlineData("Kastral, the Windcrested", "Legendary Creature — Bird Scout", CardEffect.Reanimate,
        "Flying\nWhenever one or more Birds you control deal combat damage to a player, choose one —\n• You "
        + "may put a Bird creature card from your hand or graveyard onto the battlefield with a finality "
        + "counter on it.\n• Put a +1/+1 counter on each Bird you control.\n• Draw a card.")]
    [InlineData("Kastral, the Windcrested", "Legendary Creature — Bird Scout", CardEffect.Cheat,
        "Flying\nWhenever one or more Birds you control deal combat damage to a player, choose one —\n• You "
        + "may put a Bird creature card from your hand or graveyard onto the battlefield with a finality "
        + "counter on it.\n• Put a +1/+1 counter on each Bird you control.\n• Draw a card.")]
    // A token copy of what a type-only sentence exiled; the next sentence is about those cards.
    [InlineData("Altaïr Ibn-La'Ahad", "Legendary Creature — Human Assassin", CardEffect.Reanimate,
        "First strike\nWhenever Altaïr attacks, exile up to one target Assassin creature card from your "
        + "graveyard with a memory counter on it. Then for each creature card you own in exile with a memory "
        + "counter on it, create a tapped and attacking token that's a copy of it. Exile those tokens at end of "
        + "combat.")]
    // A Saga card is a type too (Rydia's Summon).
    [InlineData("Rydia, Summoner of Mist", "Legendary Creature — Human Shaman", CardEffect.Reanimate,
        "Landfall — Whenever a land you control enters, you may discard a card. If you do, draw a "
        + "card.\nSummon — {X}, {T}: Return target Saga card with mana value X from your graveyard to the "
        + "battlefield with a finality counter on it. It gains haste until end of turn. Activate only as a "
        + "sorcery.")]
    // A LIST of types is a tribe too, read 2026-11-03 (Dr. Eggman's Cheat): Push
    // the Limit's Mounts and Vehicles, Reanimate by hand on 09-10 before the
    // tribal ruling and reached.
    [InlineData("Push the Limit", "Sorcery", CardEffect.Reanimate,
        "Return all Mount and Vehicle cards from your graveyard to the battlefield. Sacrifice them at the "
        + "beginning of the next end step.\nVehicles you control become artifact creatures until end of turn. "
        + "Creatures you control gain haste until end of turn.")]
    public void Classify_TheTribalReanimateAndCheat_DoNotCarryTheTag(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
    }

    [Theory]
    // An exclusion is no tribe.
    [InlineData("Junji, the Midnight Sky", "Legendary Creature — Dragon Spirit", CardEffect.Reanimate,
        "Flying, menace\nWhen Junji dies, choose one —\n• Each opponent discards two cards and loses 2 "
        + "life.\n• Put target non-Dragon creature card from a graveyard onto the battlefield under your "
        + "control. You lose 2 life.")]
    [InlineData("Body Launderer", "Creature — Ogre Rogue", CardEffect.Reanimate,
        "Deathtouch\nWhenever another nontoken creature you control dies, this creature connives.\nWhen this "
        + "creature dies, return another target non-Rogue creature card with equal or lesser power from your "
        + "graveyard to the battlefield.")]
    // A card any creature could be, beside the tribal one.
    [InlineData("Thwart the Grave", "Sorcery", CardEffect.Reanimate,
        "This spell costs {1} less to cast for each creature in your party. (Your party consists of up to one "
        + "each of Cleric, Rogue, Warrior, and Wizard.)\nReturn target creature card and up to one target "
        + "Cleric, Rogue, Warrior, or Wizard creature card from your graveyard to the battlefield.")]
    // The subtypes of the other card types are no tribe.
    [InlineData("Scrounge for Eternity", "Sorcery", CardEffect.Reanimate,
        "As an additional cost to cast this spell, sacrifice an artifact or creature.\nReturn target creature "
        + "or Spacecraft card with mana value 5 or less from your graveyard to the battlefield. Then create a "
        + "Lander token. (It's an artifact with \"{2}, {T}, Sacrifice this token: Search your library for a "
        + "basic land card, put it onto the battlefield tapped, then shuffle.\")")]
    [InlineData("Academy Researchers", "Creature — Human Wizard", CardEffect.Cheat,
        "When this creature enters, you may put an Aura card from your hand onto the battlefield attached to "
        + "this creature.")]
    [InlineData("Stoneforge Mystic", "Creature — Kor Artificer", CardEffect.Cheat,
        "When this creature enters, you may search your library for an Equipment card, reveal it, put it into "
        + "your hand, then shuffle.\n{1}{W}, {T}: You may put an Equipment card from your hand onto the "
        + "battlefield.")]
    // One creature of the type pays, or the card makes tokens of it.
    [InlineData("Slimefoot and Squee", "Legendary Creature — Fungus Goblin", CardEffect.Reanimate,
        "Whenever Slimefoot and Squee enters or attacks, create a 1/1 green Saproling creature "
        + "token.\n{1}{B}{R}{G}, Sacrifice a Saproling: Return this card and up to one other target creature "
        + "card from your graveyard to the battlefield. Activate only as a sorcery.")]
    [InlineData("Rat King, Verminister", "Legendary Creature — Rat Avatar", CardEffect.Reanimate,
        "Disappear — At the beginning of your end step, if a permanent left the battlefield under your "
        + "control this turn, create a 1/1 black Rat creature token and put a +1/+1 counter on Rat King.\n{T}, "
        + "Sacrifice three Rats: Return target creature card and all other cards with the same name as that "
        + "card from your graveyard to the battlefield tapped.")]
    // A Cave card is a land.
    [InlineData("Glimpse the Core", "Sorcery", CardEffect.Reanimate,
        "Choose one —\n• Search your library for a basic Forest card, put that card onto the battlefield "
        + "tapped, then shuffle.\n• Return target Cave card from your graveyard to the battlefield tapped.")]
    // A type CHOSEN as the card enters or resolves is no tribe in any tag,
    // ruled 2026-10-30 ("Se scegli tu il tipo quando gioco la carta è diverso
    // da un tribale fisso"); these three pinned the opposite from 2026-10-15.
    [InlineData("Belbe's Portal", "Artifact", CardEffect.Cheat,
        "As this artifact enters, choose a creature type.\n{3}, {T}: You may put a creature card of the "
        + "chosen type from your hand onto the battlefield.")]
    [InlineData("From the Rubble", "Enchantment", CardEffect.Reanimate,
        "As this enchantment enters, choose a creature type.\nAt the beginning of your end step, return "
        + "target creature card of the chosen type from your graveyard to the battlefield with a finality "
        + "counter on it. (If a creature with a finality counter on it would die, exile it instead.)")]
    [InlineData("Haunting Voyage", "Sorcery", CardEffect.Reanimate,
        "Choose a creature type. Return up to two creature cards of that type from your graveyard to the "
        + "battlefield. If this spell was foretold, return all creature cards of that type from your graveyard "
        + "to the battlefield instead.\nForetell {5}{B}{B} (During your turn, you may pay {2} and exile this "
        + "card from your hand face down. Cast it on a later turn for its foretell cost.)")]
    public void Classify_TheTribalReanimateAndCheat_KeepTheTagForAnyCreature(string name, string typeLine, CardEffect effect, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(effect));
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
    // A card nobody casts carries no tags (2026-10-23): a token, a dungeon, an
    // emblem, a designation. 29 reviewed, all untagged; Treasure's Ramp is the
    // Ramp of the card that makes it.
    [InlineData("Treasure", "Token Artifact — Treasure",
        "{T}, Sacrifice this token: Add one mana of any color.")]
    [InlineData("The Ring // The Ring Tempts You", "Emblem // Card",
        "Your Ring-bearer is legendary and can't be blocked by creatures with greater power.\nWhenever your "
        + "Ring-bearer attacks, draw a card, then discard a card.\nWhenever your Ring-bearer becomes blocked by "
        + "a creature, that creature's controller sacrifices it at end of combat.\nWhenever your Ring-bearer "
        + "deals combat damage to a player, each opponent loses 3 life.\nAs the Ring tempts you, you get an "
        + "emblem named The Ring if you don't have one. Then your emblem gains its next ability and you choose "
        + "a creature you control to become or remain your Ring-bearer.\n• The Ring can tempt you even if you "
        + "don't control a creature.\n• The Ring gains its abilities in order from top to bottom. Once it gains "
        + "an ability, it has that ability for the rest of the game.\n• Each time the Ring tempts you, you must "
        + "choose a creature if you control one.\n• Each player can have only one emblem named The Ring and only "
        + "one Ring-bearer at a time.")]
    [InlineData("The Monarch", "Card",
        "At the beginning of your end step, draw a card.\nWhenever a creature deals combat damage to you, its "
        + "controller becomes the monarch.")]
    [InlineData("Lost Mine of Phandelver", "Dungeon",
        "Cave Entrance — Scry 1. (Leads to: Goblin Lair, Mine Tunnels)\nGoblin Lair — Create a 1/1 red Goblin "
        + "creature token. (Leads to: Storeroom, Dark Pool)\nMine Tunnels — Create a Treasure token. (Leads to: "
        + "Dark Pool, Fungi Cavern)\nStoreroom — Put a +1/+1 counter on target creature. (Leads to: Temple of "
        + "Dumathoin)\nDark Pool — Each opponent loses 1 life and you gain 1 life. (Leads to: Temple of "
        + "Dumathoin)\nFungi Cavern — Target creature gets -4/-0 until your next turn. (Leads to: Temple of "
        + "Dumathoin)\nTemple of Dumathoin — Draw a card.")]
    public void Classify_AReferenceCard_HasNoTags(string name, string typeLine, string oracle)
    {
        Assert.Equal(CardEffect.None, EffectClassifier.Classify(MakeCard(name, typeLine, oracle)));
    }

    private static Card MakeCard(string name, string typeLine, string oracleText, List<string>? keywords = null) =>
        TestCards.Make(name, typeLine, oracleText, keywords: keywords);
}
