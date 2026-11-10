using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // A BLINK at SORCERY SPEED saves nothing (2026-09-29), and since 2026-11-10
    // no shield does (Classify_EnduranceBobblehead_IsBuffNotProtection).
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
    // An activation whose cost runs past 40 characters is still held up.
    [InlineData("Arwen, Mortal Queen", "Legendary Creature — Elf Noble", null,
        "Arwen enters with an indestructible counter on her.\n{1}, Remove an indestructible counter from Arwen: "
        + "Another target creature gains indestructible until end of turn. Put a +1/+1 counter and a lifelink "
        + "counter on that creature and a +1/+1 counter and a lifelink counter on Arwen.")]
    // Backup hands the hexproof printed below it to the creature it lands on.
    [InlineData("Saiba Cryptomancer", "Creature — Moonfolk Ninja", "Flash",
        "Flash\nBackup 1 (When this creature enters, put a +1/+1 counter on target creature. If that's another "
        + "creature, it gains the following ability until end of turn.)\nHexproof")]
    // Your own permanents phased out, the ones you choose.
    [InlineData("Clever Concealment", "Instant", null,
        "Convoke (Your creatures can help cast this spell. Each creature you tap while casting this spell pays "
        + "for {1} or one mana of that creature's color.)\nAny number of target nonland permanents you control "
        + "phase out. (Treat them and anything attached to them as though they don't exist until your next "
        + "turn.)")]
    [InlineData("Guardian of Faith", "Creature — Spirit Knight", "Flash",
        "Flash\nFlying\nWhen this creature enters, any number of other target creatures you control phase out. "
        + "(Treat them and anything attached to them as though they don't exist until your next turn.)")]
    [InlineData("Ripples of Potential", "Instant", null,
        "Proliferate, then choose any number of permanents you control that had a counter put on them this "
        + "way. Those permanents phase out. (To proliferate, choose any number of permanents and/or players, "
        + "then give each another counter of each kind already there. Treat phased-out permanents and anything "
        + "attached to them as though they don't exist until their controller's next turn.)")]
    // A blink of your own named in a list of types.
    [InlineData("Scrollshift", "Instant", null,
        "Exile up to one target artifact, creature, or enchantment you control, then return it to the "
        + "battlefield under its owner's control.\nDraw a card.")]
    // What died this turn, brought back from your graveyard in response.
    [InlineData("Samwise the Stouthearted", "Legendary Creature — Halfling Peasant", "Flash",
        "Flash\nWhen Samwise enters, choose up to one target permanent card in your graveyard that was put "
        + "there from the battlefield this turn. Return it to your hand. Then the Ring tempts you.")]
    [InlineData("Gerrard's Hourglass Pendant", "Legendary Artifact", "Flash",
        "Flash\nIf a player would begin an extra turn, that player skips that turn instead.\n{4}, {T}, Exile "
        + "Gerrard's Hourglass Pendant: Return to the battlefield tapped all artifact, creature, enchantment, "
        + "and land cards in your graveyard that were put there from the battlefield this turn.")]
    public void Classify_TheOctoberProtectionGaps_AreProtection(string name, string typeLine, string? keyword, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle, keyword is null ? null : [keyword]);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A Dragon spell's hexproof is a tribe's shield.
    [InlineData("Jade Orb of Dragonkind", "Artifact", null,
        "{T}: Add {G}. When you spend this mana to cast a Dragon creature spell, it enters with an additional "
        + "+1/+1 counter on it and gains hexproof until your next turn. (It can't be the target of spells or "
        + "abilities your opponents control.)")]
    // An opponent's graveyard saves nothing of yours.
    [InlineData("Lobelia Sackville-Baggins", "Legendary Creature — Halfling Citizen", "Flash",
        "Flash\nMenace\nWhen Lobelia enters, exile target creature card from an opponent's graveyard that was "
        + "put there from the battlefield this turn, then create X Treasure tokens, where X is the exiled "
        + "card's power.")]
    // Your own lands phased out every upkeep are a cost.
    [InlineData("Taniwha", "Legendary Creature — Serpent", null,
        "Trample\nPhasing (This phases in or out before you untap during each of your untap steps. While it's "
        + "phased out, it's treated as though it doesn't exist.)\nAt the beginning of your upkeep, all lands "
        + "you control phase out. (They phase in before you untap during your next untap step.)")]
    public void Classify_TheOctoberProtectionMisreadings_AreNotProtection(string name, string typeLine, string? keyword, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle, keyword is null ? null : [keyword]);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // "All abilities" lost is not "all" creatures shielded.
    [InlineData("Monument to Perfection", "Artifact",
        "{3}, {T}: Search your library for a basic, Sphere, or Locus land card, reveal it, put it into your "
        + "hand, then shuffle.\n{3}: This artifact becomes a 9/9 Phyrexian Construct artifact creature, loses "
        + "all abilities, and gains indestructible and toxic 9. Activate only if there are nine or more lands "
        + "with different names among the basic, Sphere, and Locus lands you control.")]
    public void Classify_TheOctober16Pass_AllAbilities_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Readings of 2026-10-24, each under a ruling docs/ontology/Protection.md
    // already holds: a granted return from death that also comes back from exile,
    // to the hand; a morph Aura attached as it is turned face up; a blink whose
    // return opens a d20 table; an exile its controller may accept, brought back
    // by a second activation; target creatures phased out in the plural, and a
    // target permanent; ward over you and your permanents written out in full.
    [InlineData("Ashnod's Intervention", "Instant",
        "Until end of turn, target creature gets +2/+0 and gains \"When this creature dies or is put into "
        + "exile from the battlefield, return it to its owner's hand.\"")]
    [InlineData("Gift of Doom", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has deathtouch and indestructible.\nMorph—Sacrifice another "
        + "creature. (You may cast this card face down as a 2/2 creature for {3}. Turn it face up any time for "
        + "its morph cost.)\nAs this Aura is turned face up, you may attach it to a creature.")]
    [InlineData("Lae'zel's Acrobatics", "Instant",
        "Exile all nontoken creatures you control, then roll a d20.\n1—9 | Return those cards to the "
        + "battlefield under their owner's control at the beginning of the next end step.\n10—20 | Return those "
        + "cards to the battlefield under their owner's control, then exile them again. Return those cards to "
        + "the battlefield under their owner's control at the beginning of the next end step.")]
    [InlineData("Livio, Oathsworn Sentinel", "Legendary Creature — Human Knight",
        "{1}{W}: Choose another target creature. Its controller may exile it with an aegis counter on "
        + "it.\n{2}{W}, {T}: Return all exiled cards with aegis counters on them to the battlefield under their "
        + "owners' control.\nPartner (You can have two commanders if both have partner.)")]
    [InlineData("March of Swirling Mist", "Instant",
        "As an additional cost to cast this spell, you may exile any number of blue cards from your hand. "
        + "This spell costs {2} less to cast for each card exiled this way.\nUp to X target creatures phase out. "
        + "(While they're phased out, they're treated as though they don't exist. Each one phases in before its "
        + "controller untaps during their next untap step.)")]
    [InlineData("Unite the Coalition", "Instant",
        "Choose five. You may choose the same mode more than once.\n• Target permanent phases out.\n• Target "
        + "player draws a card.\n• Exile target player's graveyard.\n• Unite the Coalition deals 2 damage to any "
        + "target.\n• Destroy target artifact or enchantment.")]
    [InlineData("Parnesse, the Subtle Brush", "Legendary Creature — Vampire Wizard",
        "Whenever you or a permanent you control becomes the target of a spell or ability an opponent "
        + "controls, counter that spell or ability unless that player pays 4 life.\nWhenever you copy a spell, "
        + "up to one target opponent may also copy that spell. They may choose new targets for that copy.")]
    public void Classify_TheOctober24ProtectionReadings_AreProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Misreadings of 2026-10-24: IT opening a sentence after an activation cost
    // that names a land you control is still the card itself; a tribe's prevention
    // is a tribe's shield; a permanent that cannot phase in for as long as
    // something holds it is the O-ring shape, not a save.
    [InlineData("Trench Behemoth", "Creature — Kraken",
        "Return a land you control to its owner's hand: Untap this creature. It gains hexproof until end of "
        + "turn.\nLandfall — Whenever a land you control enters, target creature an opponent controls attacks "
        + "during its controller's next combat phase if able.")]
    [InlineData("Rescue Retriever", "Creature — Dog Soldier",
        "Flash\nWhen this creature enters, put a +1/+1 counter on each other Soldier you control.\nPrevent all "
        + "damage that would be dealt to other attacking Soldiers you control.")]
    [InlineData("The Pandorica", "Legendary Artifact",
        "You may choose not to untap The Pandorica during your untap step.\n{1}{W}, {T}: Untap another target "
        + "nonland permanent, then it phases out. It can't phase in for as long as The Pandorica remains "
        + "tapped. When The Pandorica becomes untapped or leaves the battlefield, that permanent phases in. "
        + "Activate only as a sorcery.")]
    [InlineData("The Phasing of Zhalfir", "Enchantment — Saga",
        "Read ahead (Choose a chapter and start with that many lore counters. Add one after your draw step. "
        + "Skipped chapters don't trigger. Sacrifice after III.)\nI, II — Another target nonland permanent "
        + "phases out. It can't phase in for as long as you control this Saga.\nIII — Destroy all creatures. For "
        + "each creature destroyed this way, its controller creates a 2/2 black Phyrexian creature token.")]
    public void Classify_TheOctober24ProtectionMisreadings_AreNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Ruled 2026-10-25: a condition the card makes true itself as it enters is no
    // condition, so Dawnglade Regent's monarch shield is static; a return held up
    // for one creature that dies this turn is a save (Cryptek).
    [InlineData("Dawnglade Regent", "Creature — Elk",
        "When this creature enters, you become the monarch.\nAs long as you're the monarch, permanents you "
        + "control have hexproof.")]
    [InlineData("Cryptek", "Artifact Creature — Necron Wizard",
        "{1}{B}, {T}: Choose another target artifact creature you control. When that creature dies this turn, "
        + "return it to the battlefield tapped under your control.")]
    public void Classify_TheOctober25ProtectionRulings_AreProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Ruled 2026-10-25: protection from a creature type over the area is no shield
    // of this tag (Gor Muldrak's Salamanders), and a death trigger that returns
    // your creature is not one either (Magnanimous Magistrate). The second half
    // was overturned 2026-11-10 for an area with no condition (Valkyrie's Call,
    // Classify_TheNovember10Ruling_AReturnFromDeathOverAnArea_IsProtection);
    // Magistrate stays out on its condition, a budget of reprieve counters.
    [InlineData("Gor Muldrak, Amphinologist", "Legendary Creature — Human Scout",
        "You and permanents you control have protection from Salamanders.\nAt the beginning of your end step, "
        + "each player who controls the fewest creatures creates a 4/3 blue Salamander Warrior creature token.")]
    [InlineData("Magnanimous Magistrate", "Creature — Human Advisor",
        "This creature enters with five reprieve counters on it.\nWhenever another nontoken creature you "
        + "control dies, if its mana value was 1 or greater, you may remove that many reprieve counters from "
        + "this creature. If you do, return that card to the battlefield under its owner's control.")]
    public void Classify_TheOctober25ProtectionRulings_AreNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // The plural you control is a save of your own and not Pacify (2026-10-25).
    [InlineData("Gallifrey Falls // No More", "Instant // Instant",
        "Gallifrey Falls deals 4 damage to each creature. If a creature dealt damage this way would die this "
        + "turn, exile it instead.\nFuse (You may cast one or both halves of this card from your hand.)\nAny "
        + "number of target creatures you control phase out.\nFuse (You may cast one or both halves of this card "
        + "from your hand.)")]
    [InlineData("Guardian of Faith", "Creature — Spirit Knight",
        "Flash\nVigilance\nWhen this creature enters, any number of other target creatures you control phase "
        + "out. (Treat them and anything attached to them as though they don't exist until their controller's "
        + "next turn.)")]
    public void Classify_TheOctober25PhasingRuling_YourOwnIsProtectionAlone(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Pacify));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Read 2026-11-05 under the phasing ruling: Slip Out the Back's Put a +1/+1
    // counter on target creature. It phases out. is a shield with the counter as
    // its rider (Protection alone by hand, 2026-11-03). The it phases out of the
    // sentence before is read for Protection only, and the until and can't phase
    // in guards stay.
    [InlineData("Slip Out the Back", "Instant",
        "Put a +1/+1 counter on target creature. It phases out. (Treat it and anything attached to it as "
        + "though they don't exist until its controller's next turn.)")]
    public void Classify_TheNovember5ItPhasesOutAfterACounter_IsProtectionNotBuff(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Protection));
        Assert.False(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-05: an instant that exiles a target permanent and brings it
    // straight back is a save when you can hold it up, whoever's it is (Planar
    // Incision, Protection by hand, with Twining Twins and Parting Gust already
    // tagged), the anybody's-blink of Protection.md 2026-09-28 without the wait.
    // Eldrazi Confluence's blink mode is the reviewed card this reading reaches
    // against its hand tag.
    [InlineData("Planar Incision", "Instant",
        "Exile target artifact or creature, then return it to the battlefield under its owner's control with "
        + "a +1/+1 counter on it.")]
    [InlineData("Twining Twins // Swift Spiral", "Creature — Faerie Wizard // Instant — Adventure",
        "Flying, vigilance, ward {1}\nExile target nontoken creature. Return it to the battlefield under its "
        + "owner's control at the beginning of the next end step.")]
    [InlineData("Parting Gust", "Instant",
        "Gift a tapped Fish (You may promise an opponent a gift as you cast this spell. If you do, they "
        + "create a tapped 1/1 blue Fish creature token before its other effects.)\nExile target nontoken "
        + "creature. If the gift wasn't promised, return that card to the battlefield under its owner's control "
        + "with a +1/+1 counter on it at the beginning of the next end step.")]
    public void Classify_TheNovember5InstantBlinkOfAnyonesPermanent_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Read 2026-11-05: target creatures phased out by choice are a save, whether
    // the targets are listed (Spectral Adversary's artifacts, creatures, and/or
    // enchantments) or the phasing is an option of the next sentence (Change of
    // Plans: connive, then you may have any number of them phase out).
    [InlineData("Spectral Adversary", "Creature — Spirit",
        "Flash\nFlying\nWhen this creature enters, you may pay {1}{U} any number of times. When you pay this "
        + "cost one or more times, put that many +1/+1 counters on this creature, then up to that many other "
        + "target artifacts, creatures, and/or enchantments phase out.")]
    [InlineData("Change of Plans", "Instant",
        "Each of X target creatures you control connive. You may have any number of them phase out. (To have "
        + "a creature connive, draw a card, then discard a card. If you discarded a nonland card, put a +1/+1 "
        + "counter on that creature. Treat phased-out permanents and anything attached to them as though they "
        + "don't exist until their controller's next turn.)")]
    public void Classify_TheNovember5PhasingOutTargetCreatures_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Read 2026-11-05: Semester's End exiles any number of target creatures and/or
    // planeswalkers YOU control and returns each at the end step, a blink of your
    // own and no answer to anything (Protection alone by hand).
    [InlineData("Semester's End", "Instant",
        "Exile any number of target creatures and/or planeswalkers you control. At the beginning of the next "
        + "end step, return each of them to the battlefield under its owner's control. Each of them enters with "
        + "an additional +1/+1 counter on it if it's a creature and an additional loyalty counter on it if it's "
        + "a planeswalker.")]
    public void Classify_TheNovember5ProtectionPass_ExileYoursAndBringThemBack_IsProtectionNotRemoval(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Protection));
        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Read 2026-11-05: Guardian Naga's "during your turn, prevent all damage that
    // would be dealt to this creature" shields the creature itself, not a save
    // for anyone else, and a target on the Adventure face (Disenchant by hand)
    // is no reason to read it as one.
    [InlineData("Guardian Naga // Banishing Coils", "Creature — Snake // Instant — Adventure",
        "Vigilance\nDuring your turn, prevent all damage that would be dealt to this creature.\nExile target "
        + "artifact or enchantment. (Then exile this card. You may cast the creature later from exile.)")]
    public void Classify_TheNovember5ProtectionPass_AnEffectThatSpendsItselfOnTheCreature_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // RULED 2026-11-05 (Solo Buff): a toughness-only bonus is Buff and not
    // Protection, flash or not. Paladin's Shield is named (flash, attaches on
    // entering, +0/+2); Great Defender's instant +0/+X is beside it.
    [InlineData("Paladin's Shield", "Artifact — Equipment",
        "Flash\nWhen this Equipment enters, attach it to target creature you control.\nEquipped creature gets "
        + "+0/+2.\nEquip {3} ({3}: Attach to target creature you control. Equip only as a sorcery.)")]
    [InlineData("Great Defender", "Instant",
        "Target creature gets +0/+X until end of turn, where X is its mana value.")]
    public void Classify_TheNovember5Ruling_AToughnessOnlyBonus_IsBuffNotProtection(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Buff));
        Assert.False(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Ruled 2026-11-10 (Protection.md, Q1): a shield whose recipient is chosen at
    // sorcery speed answers nothing, whatever it is - indestructible until end of
    // turn, an indestructible counter - as Lilysplash Mentor's blink already did
    // not (Endurance Bobblehead is pinned in the Buff file).
    [InlineData("Burdened Stoneback", "Creature — Giant Warrior",
        "This creature enters with two -1/-1 counters on it.\n{1}{W}, Remove a counter from this creature: "
        + "Target creature gains indestructible until end of turn. Activate only as a sorcery. (Damage and "
        + "effects that say \"destroy\" don't destroy it. If its toughness is 0 or less, it still dies.)")]
    [InlineData("Daring Fiendbonder", "Creature — Human Warlock",
        "Haste\nThis creature attacks each combat if able.\n{1}{B}, Exile this card from your graveyard: Put an "
        + "indestructible counter on target creature. Activate only as a sorcery.")]
    public void Classify_TheNovember10Ruling_AShieldChosenAtSorcerySpeed_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Ruled 2026-11-10 (Protection.md, Q2), overturning 2026-10-25: a return from
    // death over an area of your creatures with no condition is the static area
    // shield. Valkyrie's Call and Yarus named; persist and undying handed to the
    // same area (Isilu, Mikaeus), Avacyn and Luminous Broodmoth reached.
    [InlineData("Valkyrie's Call", "Enchantment",
        "Whenever a nontoken, non-Angel creature you control dies, return that card to the battlefield under "
        + "its owner's control with a +1/+1 counter on it. It has flying and is an Angel in addition to its "
        + "other types.")]
    [InlineData("Yarus, Roar of the Old Gods", "Legendary Creature — Centaur Druid",
        "Other creatures you control have haste.\nWhenever one or more face-down creatures you control deal "
        + "combat damage to a player, draw a card.\nWhenever a face-down creature you control dies, return it to "
        + "the battlefield face down under its owner's control if it's a permanent card, then turn it face up.")]
    [InlineData("Eirdu, Carrier of Dawn // Isilu, Carrier of Twilight", "Legendary Creature — Elemental God // Legendary Creature — Elemental God",
        "Flying, lifelink\nCreature spells you cast have convoke. (Your creatures can help cast those spells. "
        + "Each creature you tap while casting a creature spell pays for {1} or one mana of that creature's "
        + "color.)\nAt the beginning of your first main phase, you may pay {B}. If you do, transform "
        + "Eirdu.\nFlying, lifelink\nEach other nontoken creature you control has persist. (When it dies, if it "
        + "had no -1/-1 counters on it, return it to the battlefield under its owner's control with a -1/-1 "
        + "counter on it.)\nAt the beginning of your first main phase, you may pay {W}. If you do, transform "
        + "Isilu.")]
    [InlineData("Mikaeus, the Unhallowed", "Legendary Creature — Zombie Cleric",
        "Intimidate (This creature can't be blocked except by artifact creatures and/or creatures that share "
        + "a color with it.)\nWhenever a Human deals damage to you, destroy it.\nOther non-Human creatures you "
        + "control get +1/+1 and have undying. (When a creature with undying dies, if it had no +1/+1 counters "
        + "on it, return it to the battlefield under its owner's control with a +1/+1 counter on it.)")]
    [InlineData("Avacyn, Angel of Horror", "Legendary Creature — Angel",
        "Flying, deathtouch\nWhenever Avacyn or another nontoken creature you control dies, return that card "
        + "to the battlefield under your control at the beginning of the next end step.")]
    [InlineData("Luminous Broodmoth", "Creature — Insect",
        "Flying\nWhenever a creature you control without flying dies, return it to the battlefield under its "
        + "owner's control with a flying counter on it.")]
    public void Classify_TheNovember10Ruling_AReturnFromDeathOverAnArea_IsProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Under the same ruling, out: a counter the creature must have (Marchesa), one
    // tribe (Thunderbolts Conspiracy), a creature you do not own (Edea), anybody's
    // (Missy), and the one an Equipment is on (Resurrection Orb).
    [InlineData("Marchesa, the Black Rose", "Legendary Creature — Human Wizard",
        "Dethrone (Whenever this creature attacks the player with the most life or tied for most life, put a "
        + "+1/+1 counter on it.)\nOther creatures you control have dethrone.\nWhenever a creature you control "
        + "with a +1/+1 counter on it dies, return that card to the battlefield under your control at the "
        + "beginning of the next end step.")]
    [InlineData("Thunderbolts Conspiracy", "Enchantment",
        "Flash\nWhenever a Villain you control dies, return it to the battlefield under its owner's control "
        + "with a finality counter on it. That creature is a Hero in addition to its other types. (If a "
        + "creature with a finality counter on it would die, exile it instead.)")]
    [InlineData("Edea, Possessed Sorceress", "Legendary Creature — Human Warlock",
        "Ward {2}\nAt the beginning of combat on your turn, gain control of target creature an opponent "
        + "controls until end of turn. Untap that creature. It gains haste until end of turn.\nWhenever a "
        + "creature you control but don't own dies, return it to the battlefield under its owner's control and "
        + "you draw a card.")]
    [InlineData("Missy", "Legendary Creature — Time Lord Rogue",
        "Whenever another nonartifact creature dies, return it to the battlefield under your control face "
        + "down and tapped. It's a 2/2 Cyberman artifact creature.\nAt the beginning of your end step, each "
        + "opponent faces a villainous choice — Each artifact creature you control deals 1 damage to that "
        + "opponent, or you draw a card and chaos ensues.")]
    [InlineData("Resurrection Orb", "Artifact — Equipment",
        "Equipped creature has lifelink.\nWhenever equipped creature dies, return that card to the battlefield "
        + "under its owner's control at the beginning of the next end step.\nEquip {4} ({4}: Attach to target "
        + "creature you control. Equip only as a sorcery.)")]
    public void Classify_TheNovember10Ruling_AReturnFromDeathThatIsNoArea_IsNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Read 2026-11-10: your own creature exiled and CLOAKED back, at instant
    // speed, is the blink that saves (Become Anonymous, Tokens and Protection by
    // hand).
    [InlineData("Become Anonymous", "Instant",
        "Exile target nontoken creature you own and the top two cards of your library in a face-down pile, "
        + "shuffle that pile, then cloak those cards. They enter tapped. (To cloak a card, put it onto the "
        + "battlefield face down as a 2/2 creature with ward {2}. Turn it face up any time for its mana cost if "
        + "it's a creature card.)")]
    public void Classify_TheNovember10Readings_AreProtection(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Read 2026-11-10: the target of the damage the card deals is not who the
    // grant after and goes to (Ellie, Vengeful Hunter shields herself); and a
    // spell kept from a counter is no permanent saved, 0 of 7 reviewed tagged
    // (Mistrise Village, held up; Vexing Shusher, Surrak Dragonclaw, Cavern of
    // Souls).
    [InlineData("Ellie, Vengeful Hunter", "Legendary Creature — Human Survivor",
        "Pay 2 life, Sacrifice another creature: Ellie deals 2 damage to target player and gains "
        + "indestructible until end of turn.\nPartner—Survivors (You can have two commanders if both have this "
        + "ability.)")]
    [InlineData("Mistrise Village", "Land",
        "This land enters tapped unless you control a Mountain or a Forest.\n{T}: Add {U}.\n{U}, {T}: The next "
        + "spell you cast this turn can't be countered.")]
    [InlineData("Vexing Shusher", "Creature — Goblin Shaman",
        "This spell can't be countered.\n{R/G}: Target spell can't be countered.")]
    [InlineData("Surrak Dragonclaw", "Legendary Creature — Human Warrior",
        "Flash\nThis spell can't be countered.\nCreature spells you control can't be countered.\nOther creatures "
        + "you control have trample.")]
    [InlineData("Cavern of Souls", "Land",
        "As this land enters, choose a creature type.\n{T}: Add {C}.\n{T}: Add one mana of any color. Spend "
        + "this mana only to cast a creature spell of the chosen type, and that spell can't be countered.")]
    public void Classify_TheNovember10Readings_AreNotProtection(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Protection));
    }
}
