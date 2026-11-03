using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // INCUBATE from a spell or as something enters makes a body, ruled 2026-09-29
    // ("solo da magia/ingresso"): Eyes of Gitaxias, tagged by the human, and
    // Sunfall.
    [InlineData("Eyes of Gitaxias", "Sorcery",
        "Incubate 3. (Create an Incubator token with three +1/+1 counters on it and \"{2}: Transform this "
        + "token.\" It transforms into a 0/0 Phyrexian artifact creature.)\nDraw a card.")]
    [InlineData("Sunfall", "Sorcery",
        "Exile all creatures. Incubate X, where X is the number of creatures exiled this way. (Create an "
        + "Incubator token with X +1/+1 counters on it and \"{2}: Transform this token.\" It transforms into a "
        + "0/0 Phyrexian artifact creature.)")]
    // Read 2026-10-13: a LAND or ARTIFACT that animates itself (ruled), an
    // amass printed bare, cards put face down as 2/2s from a library, and a
    // permanent spell copied.
    [InlineData("Restless Vinestalk", "Land",
        "This land enters tapped.\n{T}: Add {G} or {U}.\n{3}{G}{U}: Until end of turn, this land becomes a 5/5 "
        + "green and blue Plant creature with trample. It's still a land.\nWhenever this land attacks, up to one "
        + "other target creature has base power and toughness 3/3 until end of turn.")]
    [InlineData("Jade Statue", "Artifact",
        "{2}: This artifact becomes a 3/6 Golem artifact creature until end of combat. Activate only during "
        + "combat.")]
    [InlineData("Barad-dûr", "Legendary Land",
        "Barad-dûr enters tapped unless you control a legendary creature.\n{T}: Add {B}.\n{X}{X}{B}, {T}: Amass "
        + "Orcs X. Activate only if a creature died this turn.")]
    [InlineData("Cybership", "Artifact — Vehicle",
        "Flying\nWhenever this Vehicle deals combat damage to a player, put the top two cards of that player's "
        + "library onto the battlefield face down under your control. They're 2/2 Cyberman artifact "
        + "creatures.\nCrew 4")]
    [InlineData("Archmage of Echoes", "Creature — Faerie Wizard",
        "Flying, ward {2}\nWhenever you cast a Faerie or Wizard permanent spell, copy it. (The copy becomes a "
        + "token.)")]
    // Somebody else creating is blanked sentence by sentence, not for the whole
    // card: the Spirits of chapter III and the Samurai you make are yours.
    [InlineData("Oath of the Grey Host", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — You and "
        + "target opponent each create a Food token.\nII — Each opponent loses 3 life. Create a Treasure "
        + "token.\nIII — Create three tapped 1/1 white Spirit creature tokens with flying.")]
    [InlineData("Eiganjo Uprising", "Sorcery",
        "Create X 2/2 white Samurai creature tokens with vigilance. They gain menace and haste until end of "
        + "turn.\nEach opponent creates X minus one 2/2 white Samurai creature tokens with vigilance.")]
    // Land animations read wider 2026-10-15 (tagged 11 to 2): an X/X, an Aura's
    // "enchanted land is a 3/3", "It becomes" after the land is named, lands
    // becoming a copy of a creature, and a land cast face down by disguise.
    [InlineData("Jolrael, Voice of Zhalfir", "Legendary Creature — Human Druid",
        "At the beginning of combat on your turn, up to one target land you control becomes an X/X green and "
        + "blue Bird creature with flying and haste until end of turn, where X is the number of cards in your "
        + "hand. It's still a land.\nWhenever a land creature you control deals combat damage to a player, draw "
        + "a card.")]
    [InlineData("Crackling Emergence", "Enchantment — Aura",
        "Enchant land you control\nEnchanted land is a 3/3 red Spirit creature with haste. It's still a "
        + "land.\nIf enchanted land would be destroyed, instead sacrifice this Aura and that land gains "
        + "indestructible until end of turn.")]
    [InlineData("Silvanus's Invoker", "Creature — Dragon Druid",
        "Conjure Elemental — {8}: Untap target land you control. It becomes an 8/8 Elemental creature with "
        + "trample and haste until end of turn. It's still a land.")]
    [InlineData("March from Velis Vel", "Sorcery",
        "Choose a nonbasic land type. Each land you control of that type becomes a copy of target creature "
        + "you control until end of turn and gains haste until end of turn.\nFlashback {4}{U} (You may cast this "
        + "card from your graveyard for its flashback cost. Then exile it.)")]
    [InlineData("Branch of Vitu-Ghazi", "Land",
        "{T}: Add {C}.\nDisguise {3} (You may cast this card face down for {3} as a 2/2 creature with ward "
        + "{2}. Turn it face up any time for its disguise cost.)\nWhen this land is turned face up, add two mana "
        + "of any one color. Until end of turn, you don't lose this mana as steps and phases end.")]
    public void Classify_MakingCreaturesWithoutTokens_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // An ARTIFACT the card animates is a body like the land, ruled 2026-10-15
    // ("Tokens, come le terre") after the hand tags were split 6 to 7 on identical
    // wordings: Majestic Metamorphosis tagged and Relic's Roar not, Katsumasa
    // tagged and Tough Cookie not. Target or enchanted, until end of turn or for
    // good.
    [InlineData("Majestic Metamorphosis", "Instant",
        "Until end of turn, target artifact or creature becomes a 4/4 Angel artifact creature and gains "
        + "flying.\nDraw a card.")]
    [InlineData("Relic's Roar", "Instant",
        "Until end of turn, target artifact or creature becomes a Dinosaur artifact creature with base power "
        + "and toughness 4/3 in addition to its other types.")]
    [InlineData("Katsumasa, the Animator", "Legendary Creature — Moonfolk Artificer",
        "Flying\n{2}{U}: Until end of turn, target noncreature artifact you control becomes an artifact "
        + "creature and gains flying. If it's not a Vehicle, it has base power and toughness 1/1 until end of "
        + "turn.\nAt the beginning of your upkeep, put a +1/+1 counter on each of up to three target noncreature "
        + "artifacts.")]
    [InlineData("Tough Cookie", "Artifact Creature — Food Golem",
        "When this creature enters, create a Food token. (It's an artifact with \"{2}, {T}, Sacrifice this "
        + "token: You gain 3 life.\")\n{2}{G}: Until end of turn, target noncreature artifact you control becomes "
        + "a 4/4 artifact creature.\n{2}, {T}, Sacrifice this creature: You gain 3 life.")]
    [InlineData("Mightstone's Animation", "Enchantment — Aura",
        "Enchant artifact\nWhen this Aura enters, draw a card.\nEnchanted artifact is a creature with base "
        + "power and toughness 4/4 in addition to its other types.")]
    [InlineData("Depthshaker Titan", "Artifact Creature — Robot",
        "When this creature enters, any number of target noncreature artifacts you control become 3/3 "
        + "artifact creatures. Sacrifice them at the beginning of the next end step.\nEach artifact creature you "
        + "control has melee, trample, and haste. (Whenever a creature with melee attacks, it gets +1/+1 until "
        + "end of turn for each opponent you attacked this combat.)")]
    [InlineData("Tezzeret, Betrayer of Flesh", "Legendary Planeswalker — Tezzeret",
        "The first activated ability of an artifact you activate each turn costs {2} less to activate.\n+1: "
        + "Draw two cards. Then discard two cards unless you discard an artifact card.\n−2: Target artifact "
        + "becomes an artifact creature. If it isn't a Vehicle, it has base power and toughness 4/4.\n−6: You "
        + "get an emblem with \"Whenever an artifact you control becomes tapped, draw a card.\"")]
    public void Classify_AnArtifactAnimated_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // An ENCHANTMENT that animates itself joined the 2026-10-13 ruling on lands
    // and artifacts on 2026-10-15 ("Tokens, come terre e artefatti"): three tagged
    // by hand, Ride the Shoopuf reached.
    [InlineData("Reverberating Summons", "Enchantment",
        "At the beginning of each combat, if you've cast two or more spells this turn, this enchantment "
        + "becomes a 3/3 Monk creature with haste in addition to its other types until end of turn.\n{1}{R}, "
        + "Discard your hand, Sacrifice this enchantment: Draw two cards.")]
    [InlineData("Emergent Haunting", "Enchantment",
        "At the beginning of your end step, if you haven't cast a spell from your hand this turn and this "
        + "enchantment isn't a creature, it becomes a 3/3 Spirit creature with flying in addition to its other "
        + "types.\n{2}{U}: Surveil 1. (Look at the top card of your library. You may put it into your "
        + "graveyard.)")]
    [InlineData("Case of the Gorgon's Kiss", "Enchantment — Case",
        "When this Case enters, destroy up to one target creature that was dealt damage this turn.\nTo solve — "
        + "Three or more creature cards were put into graveyards from anywhere this turn. (If unsolved, solve "
        + "at the beginning of your end step.)\nSolved — This Case is a 4/4 Gorgon creature with deathtouch and "
        + "lifelink in addition to its other types.")]
    [InlineData("Ride the Shoopuf", "Enchantment",
        "Landfall — Whenever a land you control enters, put a +1/+1 counter on target creature you "
        + "control.\n{5}{G}{G}: This enchantment becomes a 7/7 Beast creature in addition to its other types.")]
    public void Classify_AnEnchantmentAnimatingItself_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Plain MANIFEST is a body like cloak, ruled 2026-10-15 ("Tokens come cloak"):
    // Orochi Soul-Reaver and Kozilek tagged by hand, Guardian of the Forgotten
    // reached. Kozilek's players each manifest two, which can reach three for a
    // creature cast once. Vannifar cloaks a card from hand in a mode of a
    // repeating trigger.
    [InlineData("Orochi Soul-Reaver", "Creature — Snake Ninja Rogue",
        "Ninjutsu {3}{B} ({3}{B}, Return an unblocked attacker you control to hand: Put this card onto the "
        + "battlefield from your hand tapped and attacking.)\nWhenever one or more creatures you control deal "
        + "combat damage to a player, create a Treasure token and manifest the top card of that player's "
        + "library. (Put it onto the battlefield face down as a 2/2 creature. Turn it face up any time for its "
        + "mana cost if it's a creature card.)")]
    [InlineData("Guardian of the Forgotten", "Creature — Elephant Warrior",
        "Vigilance\nWhenever a modified creature you control dies, manifest the top card of your library. "
        + "(Equipment, Auras you control, and counters are modifications. To manifest a card, put it onto the "
        + "battlefield face down as a 2/2 creature. Turn it face up any time for its mana cost if it's a "
        + "creature card.)")]
    [InlineData("Kozilek, the Broken Reality", "Legendary Creature — Eldrazi",
        "When you cast this spell, up to two target players each manifest two cards from their hands. For "
        + "each card manifested this way, you draw a card. (To manifest a card, put it onto the battlefield "
        + "face down as a 2/2 creature. Turn it face up any time for its mana cost if it's a creature "
        + "card.)\nOther colorless creatures you control get +3/+2.")]
    [InlineData("Vannifar, Evolved Enigma", "Legendary Creature — Elf Ooze Wizard",
        "At the beginning of combat on your turn, choose one —\n• Cloak a card from your hand. (Put it onto "
        + "the battlefield face down as a 2/2 creature with ward {2}. Turn it face up any time for its mana "
        + "cost if it's a creature card.)\n• Put a +1/+1 counter on each colorless creature you control.")]
    public void Classify_AManifest_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A LIEUTENANT line works only while you control your commander, and a cube
    // game has none: ruled 2026-10-15 for every tag ("mai conta, per nessun tag").
    // Ironwill Forger was tagged for its myriad, handed back. An X sized by your
    // commanders is the same nothing (Cactus Preserve).
    [InlineData("Siege-Gang Lieutenant", "Creature — Goblin",
        "Lieutenant — At the beginning of combat on your turn, if you control your commander, create two 1/1 "
        + "red Goblin creature tokens. Those tokens gain haste until end of turn.\n{2}, Sacrifice a Goblin: This "
        + "creature deals 1 damage to any target.")]
    [InlineData("Ironwill Forger", "Creature — Orc Artificer",
        "Lieutenant — At the beginning of combat on your turn, if you control your commander, target "
        + "nonlegendary creature you control gains myriad until end of turn. (Whenever it attacks, for each "
        + "opponent other than defending player, you may create a token copy that's tapped and attacking that "
        + "player or a planeswalker they control. Exile the tokens at end of combat.)")]
    [InlineData("Loyal Apprentice", "Creature — Human Artificer",
        "Haste\nLieutenant — At the beginning of combat on your turn, if you control your commander, create a "
        + "1/1 colorless Thopter artifact creature token with flying. That token gains haste until end of turn.")]
    [InlineData("Cactus Preserve", "Land — Desert",
        "This land enters tapped.\n{T}: Add one mana of any type that a land you control could produce.\n{3}: "
        + "Until end of turn, this land becomes an X/X green Plant creature with reach, where X is the greatest "
        + "mana value among your commanders. It's still a land.")]
    public void Classify_ALieutenantOrACommanderX_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
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
    // A copy of a permanent ATTACHED to a creature is an Aura or an Equipment,
    // whatever creature the line names (2026-10-15). And a copy made of another
    // permanent is no body.
    [InlineData("Arna Kennerüd, Skycaptain", "Legendary Creature — Human Knight",
        "Flying, lifelink\nWard—Discard a card.\nWhenever a modified creature you control attacks, double the "
        + "number of each kind of counter on it. Then for each nontoken permanent attached to it, create a "
        + "token that's a copy of that permanent attached to that creature.")]
    [InlineData("Three Dog, Galaxy News DJ", "Legendary Creature — Human Bard",
        "Whenever you attack, you may pay {2} and sacrifice an Aura attached to Three Dog. When you sacrifice "
        + "an Aura this way, for each other attacking creature you control, create a token that's a copy of "
        + "that Aura attached to that creature.")]
    [InlineData("True Polymorph", "Instant",
        "Target artifact or creature becomes a copy of another target artifact or creature.")]
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
    // …while the Incubator a creature leaves when it DIES only replaces it
    // (Furnace Gremlin, untagged by the human on 2026-09-29), and one a creature
    // makes once as it enters is the 2026-09-24 few-bodies-once rule (Converter
    // Beast).
    [InlineData("Furnace Gremlin", "Creature — Phyrexian Gremlin",
        "{1}{R}: This creature gets +1/+0 until end of turn.\nWhen this creature dies, incubate X, where X is "
        + "its power. (Create an Incubator token with X +1/+1 counters on it and \"{2}: Transform this token.\" "
        + "It transforms into a 0/0 Phyrexian artifact creature.)")]
    [InlineData("Converter Beast", "Creature — Phyrexian Beast",
        "When this creature enters, incubate 5. (Create an Incubator token with five +1/+1 counters on it and "
        + "\"{2}: Transform this token.\" It transforms into a 0/0 Phyrexian artifact creature.)")]
    // An amass once, as it enters, is the same one body once (2026-10-13).
    [InlineData("Dunland Crebain", "Creature — Bird Horror",
        "Flying\nWhen this creature enters, amass Orcs 2. (Put two +1/+1 counters on an Army you control. It's "
        + "also an Orc. If you don't control an Army, create a 0/0 black Orc Army creature token first.)")]
    [InlineData("Grishnákh, Brash Instigator", "Legendary Creature — Goblin Soldier",
        "When Grishnákh enters, amass Orcs 2. When you do, until end of turn, gain control of target "
        + "nonlegendary creature an opponent controls with power less than or equal to the amassed Army's "
        + "power. Untap that creature. It gains haste until end of turn.")]
    // A quote that ends its sentence keeps the full stop: Twisted Sewer-Witch
    // makes one Rat, and the Roles after it are no bodies. An artifact animated
    // once as the creature enters is one body once (Unctus's Retrofitter),
    // 2026-10-15.
    [InlineData("Twisted Sewer-Witch", "Creature — Human Warlock",
        "When this creature enters, create a 1/1 black Rat creature token with \"This creature can't block.\" "
        + "Then for each Rat you control, create a Wicked Role token attached to that Rat. (If you control "
        + "another Role on it, put that one into the graveyard. Enchanted creature gets +1/+1. When this token "
        + "is put into a graveyard, each opponent loses 1 life.)")]
    [InlineData("Unctus's Retrofitter", "Creature — Phyrexian Artificer",
        "Toxic 1 (Players dealt combat damage by this creature also get a poison counter.)\nWhen this creature "
        + "enters, up to one target artifact you control becomes an artifact creature with base power and "
        + "toughness 4/4 for as long as this creature remains on the battlefield.")]
    // A creature that manifests ONE card once, as it enters or dies, is one body
    // once like any other (2026-10-15, with the manifest ruling).
    [InlineData("Temur War Shaman", "Creature — Human Shaman",
        "When this creature enters, manifest the top card of your library. (Put that card onto the "
        + "battlefield face down as a 2/2 creature. Turn it face up any time for its mana cost if it's a "
        + "creature card.)\nWhenever a permanent you control is turned face up, if it's a creature, you may have "
        + "it fight target creature you don't control.")]
    [InlineData("Sultai Emissary", "Creature — Zombie Warrior",
        "When this creature dies, manifest the top card of your library. (Put that card onto the battlefield "
        + "face down as a 2/2 creature. Turn it face up any time for its mana cost if it's a creature card.)")]
    // Under the 2026-10-27 encore and unearth count: one body unearthed is two
    // (Scrapwork Cohort); the exiled creature's 4/3 is its controller's (Amphin
    // Mutineer); the encore copies themselves are no bodies, 'le copie non
    // contano' (Graywater's Fixer).
    [InlineData("Scrapwork Cohort", "Artifact Creature — Soldier",
        "When this creature enters, create a 1/1 colorless Soldier artifact creature token.\nUnearth {2}{W} "
        + "({2}{W}: Return this card from your graveyard to the battlefield. It gains haste. Exile it at the "
        + "beginning of the next end step or if it would leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Amphin Mutineer", "Creature — Salamander Pirate",
        "When this creature enters, exile up to one target non-Salamander creature. That creature's "
        + "controller creates a 4/3 blue Salamander Warrior creature token.\nEncore {4}{U}{U} ({4}{U}{U}, Exile "
        + "this card from your graveyard: For each opponent, create a token copy that attacks that opponent "
        + "this turn if able. They gain haste. Sacrifice them at the beginning of the next end step. Activate "
        + "only as a sorcery.)")]
    [InlineData("Graywater's Fixer", "Creature — Lizard Mercenary",
        "Each outlaw creature card in your graveyard has encore {X}, where X is its mana value. (Exile it and "
        + "pay its encore cost: For each opponent, create a token copy that attacks that opponent this turn if "
        + "able. They gain haste. Sacrifice them at the beginning of the next end step. Activate only as a "
        + "sorcery.)")]
    public void Classify_ACreatureMakingOneOrTwoBodiesOnce_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A Vehicle TOKEN needs crew like a Vehicle card, ruled 2026-10-27 ('No, serve
    // crew come le carte'): Pia Nalaar and Chandra, Spark Hunter untagged by name,
    // Lita untagged by hand.
    [InlineData("Pia Nalaar, Chief Mechanic", "Legendary Creature — Human Artificer",
        "Whenever one or more artifact creatures you control deal combat damage to a player, you get {E}{E} "
        + "(two energy counters).\nAt the beginning of your end step, you may pay one or more {E}. If you do, "
        + "create an X/X colorless Vehicle artifact token named Nalaar Aetherjet with flying and crew 2, where "
        + "X is the amount of {E} paid this way.")]
    [InlineData("Chandra, Spark Hunter", "Legendary Planeswalker — Chandra",
        "At the beginning of combat on your turn, choose up to one target Vehicle you control. Until end of "
        + "turn, it becomes an artifact creature and gains haste.\n+2: You may sacrifice an artifact or discard "
        + "a card. If you do, draw a card.\n0: Create a 3/2 colorless Vehicle artifact token with crew 1.\n−7: "
        + "You get an emblem with \"Whenever an artifact you control enters, this emblem deals 3 damage to any "
        + "target.\"")]
    [InlineData("Lita, Mechanical Engineer", "Legendary Artifact Creature — Artificer",
        "Vigilance\nAt the beginning of your end step, untap each other artifact creature you control.\n{3}{W}, "
        + "{T}: Create a 5/5 colorless Vehicle artifact token named Zeppelin with flying and crew 3. (It has "
        + "\"Tap any number of creatures you control with total power 3 or more: This token becomes an artifact "
        + "creature until end of turn.\")")]
    public void Classify_AVehicleToken_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // A body on a tribe's trigger counts only when the card sets it off by itself,
    // ruled 2026-10-27 ('i tribali contano solo se generano da soli'): another or
    // a nontoken member (Siege Veteran, Baboon Spirit, Deeproot Pilgrimage, Black
    // Panther with its modes), a tribe the card neither is nor makes (Bishop of
    // Wings), a tribe's spell cast (Urtet, Rin and Seri).
    [InlineData("Siege Veteran", "Creature — Human Soldier",
        "At the beginning of combat on your turn, put a +1/+1 counter on target creature you "
        + "control.\nWhenever another nontoken Soldier you control dies, create a 1/1 colorless Soldier artifact "
        + "creature token.")]
    [InlineData("Baboon Spirit", "Creature — Monkey Spirit",
        "Whenever another nontoken Spirit you control enters, create a 1/1 colorless Spirit creature token "
        + "with \"This token can't block or be blocked by non-Spirit creatures.\"\n{3}{U}: Exile another target "
        + "creature you control. Return it to the battlefield under its owner's control at the beginning of the "
        + "next end step.")]
    [InlineData("Deeproot Pilgrimage", "Enchantment",
        "Whenever one or more nontoken Merfolk you control become tapped, create a 1/1 blue Merfolk creature "
        + "token with hexproof.")]
    [InlineData("Black Panther, Vanguard", "Legendary Creature — Human Warrior Hero",
        "Whenever another nontoken Hero you control enters, choose one —\n• Create a 1/1 white Soldier "
        + "creature token.\n• Creatures you control get +1/+1 until end of turn.")]
    [InlineData("Bishop of Wings", "Creature — Human Cleric",
        "Whenever an Angel you control enters, you gain 4 life.\nWhenever an Angel you control dies, create a "
        + "1/1 white Spirit creature token with flying.")]
    [InlineData("Urtet, Remnant of Memnarch", "Legendary Artifact Creature — Myr",
        "Whenever you cast a Myr spell, create a 1/1 colorless Myr artifact creature token.\nAt the beginning "
        + "of combat on your turn, untap each Myr you control.\n{W}{U}{B}{R}{G}, {T}: Put three +1/+1 counters "
        + "on each Myr you control. Activate only during your turn.")]
    [InlineData("Rin and Seri, Inseparable", "Legendary Creature — Dog Cat",
        "Whenever you cast a Dog spell, create a 1/1 green Cat creature token.\nWhenever you cast a Cat spell, "
        + "create a 1/1 white Dog creature token.\n{R}{G}{W}, {T}: Rin and Seri deals damage to any target equal "
        + "to the number of Dogs you control. You gain life equal to the number of Cats you control.")]
    public void Classify_ABodyOnATribeItCannotSetOff_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // The other side of the 2026-10-27 tribal ruling: a card of the tribe saying
    // 'a' or 'one or more' (General Kreat, Zeriam), one making the tribe's tokens
    // itself (Origin of the Hidden Ones), and a land type, which is no tribe
    // (Hazezon, Titania).
    [InlineData("General Kreat, the Boltbringer", "Legendary Creature — Goblin Soldier",
        "Whenever one or more Goblins you control attack, create a 1/1 red Goblin creature token that's "
        + "tapped and attacking.\nWhenever another creature you control enters, General Kreat deals 1 damage to "
        + "each opponent.")]
    [InlineData("Zeriam, Golden Wind", "Legendary Creature — Griffin",
        "Flying\nWhenever a Griffin you control deals combat damage to a player, create a 2/2 white Griffin "
        + "creature token with flying.")]
    [InlineData("Origin of the Hidden Ones", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — This "
        + "Saga deals 4 damage to any target.\nII — Create two 1/1 black Assassin creature tokens with "
        + "menace.\nIII — Whenever an Assassin you control attacks this turn, create a 1/1 black Assassin "
        + "creature token with menace that's tapped and attacking.")]
    [InlineData("Hazezon, Shaper of Sand", "Legendary Creature — Human Warrior",
        "Desertwalk (This creature can't be blocked as long as defending player controls a Desert.)\nYou may "
        + "play Desert lands from your graveyard.\nWhenever a Desert you control enters, create two 1/1 red, "
        + "green, and white Sand Warrior creature tokens.")]
    [InlineData("Titania, Nature's Force", "Legendary Creature — Elemental",
        "You may play Forests from your graveyard.\nWhenever a Forest you control enters, create a 5/3 green "
        + "Elemental creature token.\nWhenever an Elemental you control dies, you may mill three cards.")]
    // A nontoken member that the card itself is (Crimson Cowl's Villains), and a
    // plural the lazy match cuts short, Elves made by an Elf maker (Elvish
    // Warmaster).
    [InlineData("Crimson Cowl, Master of Evil", "Legendary Creature — Human Villain",
        "Whenever one or more nontoken Villains you control attack a player, you create a 2/1 black Villain "
        + "creature token with menace. (It can't be blocked except by two or more creatures.)")]
    [InlineData("Elvish Warmaster", "Creature — Elf Warrior",
        "Whenever one or more other Elves you control enter, create a 1/1 green Elf Warrior creature token. "
        + "This ability triggers only once each turn.\n{5}{G}{G}: Elves you control get +2/+2 and gain "
        + "deathtouch until end of turn.")]
    public void Classify_ABodyOnATribeItSetsOffItself_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
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
    // Read 2026-10-13: DASH returns it to hand, so its enters trigger repeats
    // (Riders of Rohan); an incubate repeated X times; an amass on a trigger
    // that repeats, printed bare.
    [InlineData("Riders of Rohan", "Creature — Human Knight",
        "When this creature enters, create two 2/2 red Human Knight creature tokens with trample and "
        + "haste.\nDash {4}{R}{W} (You may cast this spell for its dash cost. If you do, it gains haste, and "
        + "it's returned from the battlefield to its owner's hand at the beginning of the next end step.)")]
    [InlineData("Progenitor Exarch", "Creature — Phyrexian Cat Cleric",
        "When this creature enters, incubate 3 X times. (To incubate 3, create an Incubator token with three "
        + "+1/+1 counters on it and \"{2}: Transform this token.\" It transforms into a 0/0 Phyrexian artifact "
        + "creature.)\n{T}: Transform target Incubator token you control.")]
    [InlineData("Sauron, the Dark Lord", "Legendary Creature — Avatar Horror",
        "Ward—Sacrifice a legendary artifact or legendary creature.\nWhenever an opponent casts a spell, amass "
        + "Orcs 1.\nWhenever an Army you control deals combat damage to a player, the Ring tempts you.\nWhenever "
        + "the Ring tempts you, you may discard your hand. If you do, draw four cards.")]
    // "Instead create X of those tokens" counts the creature tokens named before
    // it, and storm on a creature makes a token per spell cast before it
    // (2026-10-15).
    [InlineData("Adipose Offspring", "Creature — Alien",
        "Emerge {5}{W} (You may cast this spell by sacrificing a creature and paying the emerge cost reduced "
        + "by that creature's mana value.)\nWhen this creature enters, create a 2/2 white Alien creature token. "
        + "If this creature's emerge cost was paid, instead create X of those tokens, where X is the sacrificed "
        + "creature's toughness.")]
    [InlineData("Stormscale Scion", "Creature — Dragon",
        "Flying\nOther Dragons you control get +1/+1.\nStorm (When you cast this spell, copy it for each spell "
        + "cast before it this turn. Copies become tokens.)")]
    // Ruled 2026-10-27: ENCORE and UNEARTH bring the creature back, and the bodies
    // of every entry count together, three or more is Tokens. Encore at the 2vs2
    // table is the card plus two copies, entering and dying (Broodmate Tyrant's
    // Dragon, Exquisite Huntmaster's Elf on death); unearth enters twice (Royal
    // Warden's two Necrons are four). Read the same day: a Tarmogoyf token printed
    // with no reminder (Disa the Restless).
    [InlineData("Broodmate Tyrant", "Creature — Dragon",
        "Flying\nWhen this creature enters, create a 5/5 red Dragon creature token with flying.\nEncore "
        + "{5}{B}{R}{G}")]
    [InlineData("Exquisite Huntmaster", "Creature — Elf Warrior",
        "When this creature dies, create a 1/1 green Elf Warrior creature token.\nEncore {4}{B} ({4}{B}, Exile "
        + "this card from your graveyard: For each opponent, create a token copy that attacks that opponent "
        + "this turn if able. They gain haste. Sacrifice them at the beginning of the next end step. Activate "
        + "only as a sorcery.)")]
    [InlineData("Royal Warden", "Artifact Creature — Necron",
        "Phalanx Commander — When this creature enters, create two tapped 2/2 black Necron Warrior artifact "
        + "creature tokens.\nUnearth {3}{B} ({3}{B}: Return this card from your graveyard to the battlefield. It "
        + "gains haste. Exile it at the beginning of the next end step or if it would leave the battlefield. "
        + "Unearth only as a sorcery.)")]
    [InlineData("Disa the Restless", "Legendary Creature — Human Scout",
        "Whenever a Lhurgoyf permanent card is put into your graveyard from anywhere other than the "
        + "battlefield, put it onto the battlefield.\nWhenever one or more creatures you control deal combat "
        + "damage to a player, create a Tarmogoyf token.")]
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
    // A named token inside a quoted grant, its creature type in the reminder after
    // the quote (Tarmogoyf Nest, 2026-10-15).
    [InlineData("Tarmogoyf Nest", "Kindred Enchantment — Lhurgoyf Aura",
        "Enchant land\nEnchanted land has \"{1}{G}, {T}: Create a Tarmogoyf token.\" (It's a {1}{G} Lhurgoyf "
        + "creature with \"Tarmogoyf's power is equal to the number of card types among cards in all graveyards "
        + "and its toughness is equal to that number plus 1.\")")]
    // Cloak THOSE CARDS, a pile with library cards in it (Become Anonymous, read
    // 2026-10-27).
    [InlineData("Become Anonymous", "Instant",
        "Exile target nontoken creature you own and the top two cards of your library in a face-down pile, "
        + "shuffle that pile, then cloak those cards. They enter tapped. (To cloak a card, put it onto the "
        + "battlefield face down as a 2/2 creature with ward {2}. Turn it face up any time for its mana cost if "
        + "it's a creature card.)")]
    public void Classify_ANoncreatureMakingOneBody_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // The bodies Classify_ATribeOrABodyBeingMade_IsNotBuff opens with are
    // Tokens instead.
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
    // The counters put on the card itself, or on a permanent of yours named by
    // type, before "It becomes a 0/0" (2026-10-15).
    [InlineData("Haunted Screen", "Artifact",
        "{T}: Add {W} or {B}.\n{T}, Pay 1 life: Add {G}, {U}, or {R}.\n{7}: Put seven +1/+1 counters on this "
        + "artifact. It becomes a 0/0 Spirit creature in addition to its other types. Activate only once.")]
    [InlineData("Cosmium Confluence", "Sorcery",
        "Choose three. You may choose the same mode more than once.\n• Search your library for a Cave card, "
        + "put it onto the battlefield tapped, then shuffle.\n• Put three +1/+1 counters on a Cave you control. "
        + "It becomes a 0/0 Elemental creature with haste. It's still a land.\n• Destroy target enchantment.")]
    public void Classify_CountersThatMakeABody_AreTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Tokens means creature bodies on YOUR side. A Treasure, a Clue, a Food or a
    // Lander is a resource the card hands over in passing, and Afterlife's
    // Spirit is a body for the other player.
    [InlineData("Ichor Wellspring", "Artifact", "When this artifact enters, create a Treasure token.")]
    [InlineData("Tireless Tracker", "Creature — Human Scout", "Whenever a land you control enters, investigate. (Create a Clue token.)")]
    [InlineData("Afterlife", "Instant", "Destroy target creature. It can't be regenerated. Its controller creates a 1/1 white Spirit creature token with flying.")]
    [InlineData("Phelddagrif", "Legendary Creature — Hippo", "{G}: Phelddagrif gains trample until end of turn. Target opponent creates a 1/1 green Hippo creature token.")]
    // A NONCREATURE token named in passing makes nothing (2026-10-13), and
    // cards put face down out of EXILE are reanimated, not made.
    [InlineData("Bloodroot Apothecary", "Creature — Squirrel Druid",
        "Toxic 2 (Players dealt combat damage by this creature also get two poison counters. A player with "
        + "ten or more poison counters loses the game.)\nWhen this creature enters, you and target opponent each "
        + "create a Treasure token.\nWhenever an opponent sacrifices a noncreature token, that player gets two "
        + "poison counters.")]
    [InlineData("Death in Heaven", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Target player mills two cards, then exiles their graveyard.\nIII — Put all creature cards exiled with "
        + "this enchantment onto the battlefield face down under your control. They're 2/2 Cyberman artifact "
        + "creatures.")]
    // Read 2026-10-15: the consolation body of the creature just destroyed goes to
    // its controller (14 reviewed, none tagged); re-cloaking creatures already
    // yours, a trigger that only watches a manifest dread, cards manifested out of
    // a graveyard, and a non-creature token whose reminder mentions creatures make
    // no body.
    [InlineData("Ovinomancer", "Creature — Human Wizard",
        "When this creature enters, sacrifice it unless you return three basic lands you control to their "
        + "owner's hand.\n{T}, Return this creature to its owner's hand: Destroy target creature. It can't be "
        + "regenerated. That creature's controller creates a 0/1 green Sheep creature token.")]
    [InlineData("Rapid Hybridization", "Instant",
        "Destroy target creature. It can't be regenerated. That creature's controller creates a 3/3 green "
        + "Frog Lizard creature token.")]
    [InlineData("Expose the Culprit", "Instant",
        "Choose one or both —\n• Turn target face-down creature face up.\n• Exile any number of face-up "
        + "creatures you control with disguise in a face-down pile, shuffle that pile, then cloak them. (To "
        + "cloak a card, put it onto the battlefield face down as a 2/2 creature with ward {2}. Turn it face up "
        + "any time for its mana cost if it's a creature card.)")]
    [InlineData("Paranormal Analyst", "Creature — Human Detective",
        "Whenever you manifest dread, put a card you put into your graveyard this way into your hand.")]
    [InlineData("Ghastly Conscription", "Sorcery",
        "Exile all creature cards from target player's graveyard in a face-down pile, shuffle that pile, then "
        + "manifest those cards. (To manifest a card, put it onto the battlefield face down as a 2/2 creature. "
        + "Turn it face up any time for its mana cost if it's a creature card.)")]
    [InlineData("Sticky Fingers", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has menace and \"Whenever this creature deals combat damage to a "
        + "player, create a Treasure token.\" (It can't be blocked except by two or more creatures. The token is "
        + "an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")\nWhen enchanted creature "
        + "dies, draw a card.")]
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
    // EACH PLAYER includes you, read 2026-10-15: every reviewed card on which each
    // player creates a creature token is tagged.
    [InlineData("Waiting in the Weeds", "Sorcery",
        "Each player creates a 1/1 green Cat creature token for each untapped Forest they control.")]
    [InlineData("The War Games", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI — Each "
        + "player creates three tapped 1/1 white Warrior creature tokens. The tokens are goaded for as long as "
        + "this Saga remains on the battlefield.\nII, III — Put a +1/+1 counter on each Warrior creature.\nIV — "
        + "You may exile a nontoken creature you control. When you do, exile all Warriors.")]
    [InlineData("Tombstone Stairwell", "World Enchantment",
        "Cumulative upkeep {1}{B} (At the beginning of your upkeep, put an age counter on this permanent, "
        + "then sacrifice it unless you pay its upkeep cost for each age counter on it.)\nAt the beginning of "
        + "each upkeep, if this enchantment is on the battlefield, each player creates a 2/2 black Zombie "
        + "creature token with haste named Tombspawn for each creature card in their graveyard.\nAt the "
        + "beginning of each end step and when this enchantment leaves the battlefield, destroy all tokens "
        + "created with this enchantment. They can't be regenerated.")]
    [InlineData("Liege of the Hollows", "Creature — Spirit",
        "When this creature dies, each player may pay any amount of mana. Then each player creates a number "
        + "of 1/1 green Squirrel creature tokens equal to the amount of mana they paid this way.")]
    public void Classify_CreatureTokensForYou_IsTokens(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // An Adventure's or an Omen's spell face counts on its own, ruled 2026-10-30
    // ("l'avventura conta a sé"): Animating Faerie tagged by hand, and the four
    // untagged before the ruling handed back.
    [InlineData("Animating Faerie // Bring to Life", "Creature — Faerie // Sorcery — Adventure",
        "Flying\nTarget noncreature artifact you control becomes a 0/0 artifact creature. Put four +1/+1 "
        + "counters on it.")]
    [InlineData("Ratcatcher Trainee // Pest Problem", "Creature — Human Peasant // Instant — Adventure",
        "During your turn, this creature has first strike.\nCreate two 1/1 black Rat creature tokens with "
        + "\"This token can't block.\" (Then exile this card. You may cast the creature later from exile.)")]
    [InlineData("Riling Dawnbreaker // Signaling Roar", "Creature — Dragon // Sorcery — Omen",
        "Flying, vigilance\nAt the beginning of combat on your turn, another target creature you control gets "
        + "+1/+0 until end of turn.\nCreate a 2/2 white Soldier creature token. (Then shuffle this card into its "
        + "owner's library.)")]
    [InlineData("Hildibrand Manderville // Gentleman's Rise", "Legendary Creature — Human Detective // Instant — Adventure",
        "Creature tokens you control get +1/+1.\nWhen Hildibrand Manderville dies, you may cast it from your "
        + "graveyard as an Adventure until the end of your next turn.\nCreate a 2/2 black Zombie creature token. "
        + "(Then exile this card. You may cast the creature later from exile.)")]
    [InlineData("Imodane's Recruiter // Train Troops", "Creature — Human Knight // Sorcery — Adventure",
        "When this creature enters, creatures you control get +1/+0 and gain haste until end of turn.\nCreate "
        + "two 2/2 white Knight creature tokens with vigilance. (Then exile this card. You may cast the "
        + "creature later from exile.)")]
    [InlineData("Devouring Sugarmaw // Have for Dinner", "Creature — Horror // Instant — Adventure",
        "Menace, trample\nAt the beginning of your upkeep, you may sacrifice an artifact, enchantment, or "
        + "token. If you don't, tap this creature.\nCreate a 1/1 white Human creature token and a Food token. "
        + "(Then exile this card. You may cast the creature later from exile.)")]
    public void Classify_TheBodiesOfAnAdventuresSpellFace_AreTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
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
    // Any token of yours made a creature is a body like an animated artifact,
    // ruled 2026-10-27 on Halsin, Emerald Archdruid (Tokens, niente Buff).
    [InlineData("Halsin, Emerald Archdruid", "Legendary Creature — Elf Druid",
        "{1}: Until end of turn, target token you control becomes a green Bear creature with base power and "
        + "toughness 4/4 in addition to its other colors and types.\nChoose a Background (You can have a "
        + "Background as a second commander.)")]
    public void Classify_TheOctober27BuffPass_ATokenMadeACreature_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Ruled 2026-10-24. Any NONCREATURE card cast face down is a 2/2 body, like
    // the land (Gift of Doom tagged; Concealed Weapon and True Identity
    // realigned). You AND that player each creating is one for you too (Master of
    // Ceremonies, Sylvan Offering). Squad keeps its bodies (Sicarian Infiltrator,
    // a slip). A copy of a permanent on the battlefield is still a body: the
    // PERMANENT CARD veto must not reach Doppelgang or Extravagant Replication.
    [InlineData("Gift of Doom", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has deathtouch and indestructible.\nMorph—Sacrifice another "
        + "creature. (You may cast this card face down as a 2/2 creature for {3}. Turn it face up any time for "
        + "its morph cost.)\nAs this Aura is turned face up, you may attach it to a creature.")]
    [InlineData("Concealed Weapon", "Artifact — Equipment",
        "Equipped creature gets +3/+0.\nDisguise {2}{R} (You may cast this card face down for {3} as a 2/2 "
        + "creature with ward {2}. Turn it face up any time for its disguise cost.)\nWhen this Equipment is "
        + "turned face up, attach it to target creature you control.\nEquip {1}{R}")]
    [InlineData("True Identity", "Enchantment",
        "Whenever this enchantment or another permanent you control is turned face up, scry 1, then draw a "
        + "card. This ability triggers only once each turn.\nDisguise {W} (You may cast this card face down for "
        + "{3} as a 2/2 creature with ward {2}. Turn it face up any time for its disguise cost.)")]
    [InlineData("Master of Ceremonies", "Creature — Rhino Druid",
        "At the beginning of your upkeep, each opponent chooses money, friends, or secrets. For each player "
        + "who chose money, you and that player each create a Treasure token. For each player who chose "
        + "friends, you and that player each create a 1/1 green and white Citizen creature token. For each "
        + "player who chose secrets, you and that player each draw a card.")]
    [InlineData("Sylvan Offering", "Sorcery",
        "Choose an opponent. You and that player each create an X/X green Treefolk creature token.\nChoose an "
        + "opponent. You and that player each create X 1/1 green Elf Warrior creature tokens.")]
    [InlineData("Sicarian Infiltrator", "Artifact Creature — Human Soldier",
        "Flash\nSquad {2} (As an additional cost to cast this spell, you may pay {2} any number of times. When "
        + "this creature enters, create that many tokens that are copies of it.)\nBenediction of the Omnissiah — "
        + "When this creature enters, draw a card.")]
    [InlineData("Doppelgang", "Sorcery",
        "For each of X target permanents, create X tokens that are copies of that permanent.")]
    [InlineData("Extravagant Replication", "Enchantment",
        "At the beginning of your upkeep, create a token that's a copy of another target nonland permanent "
        + "you control.")]
    public void Classify_TheOctober24TokensPass_IsTokens(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Ruled 2026-10-24. A token copy of a nonland PERMANENT CARD from a graveyard
    // asks for no creature (Tamiyo, Compleated Sage, untagged by hand). A Vehicle
    // that becomes a creature on its own is not a body it makes, against 19
    // reviewed (Weatherlight Compleated a slip, War Balloon). The Treasure you and
    // an opponent each create is still no body (Bloodroot Apothecary).
    [InlineData("Tamiyo, Compleated Sage", "Legendary Planeswalker — Tamiyo",
        "Compleated ({G/U/P} can be paid with {G}, {U}, or 2 life. If life was paid, this planeswalker enters "
        + "with two fewer loyalty counters.)\n+1: Tap up to one target artifact or creature. It doesn't untap "
        + "during its controller's next untap step.\n−X: Exile target nonland permanent card with mana value X "
        + "from your graveyard. Create a token that's a copy of that card.\n−7: Create Tamiyo's Notebook, a "
        + "legendary colorless Book artifact token with \"Spells you cast cost {2} less to cast\" and \"{T}: Draw "
        + "a card.\"")]
    [InlineData("Weatherlight Compleated", "Legendary Artifact — Vehicle",
        "Flying\nAs long as Weatherlight Compleated has four or more phyresis counters on it, it's a Phyrexian "
        + "creature in addition to its other types.\nWhenever a creature you control dies, put a phyresis "
        + "counter on Weatherlight Compleated. Then draw a card if it has seven or more phyresis counters on "
        + "it. If it doesn't, scry 1.")]
    [InlineData("War Balloon", "Artifact — Vehicle",
        "Flying\n{1}: Put a fire counter on this Vehicle.\nAs long as this Vehicle has three or more fire "
        + "counters on it, it's an artifact creature.\nCrew 3 (Tap any number of creatures you control with "
        + "total power 3 or more: This Vehicle becomes an artifact creature until end of turn.)")]
    public void Classify_TheOctober24TokensPass_IsNotTokens(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tokens));
    }
}
