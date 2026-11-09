using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Mana a TRIGGER adds for nothing is a source, ruled 2026-10-13 ("free any
    // colour = source"): Ramp alone, like a bare tap. Nissa named; Muerra
    // untagged by hand.
    [InlineData("Nissa, Resurgent Animist", "Legendary Creature — Elf Scout",
        "Landfall — Whenever a land you control enters, add one mana of any color. Then if this is the second "
        + "time this ability has resolved this turn, reveal cards from the top of your library until you reveal "
        + "an Elf or Elemental card. Put that card into your hand and the rest on the bottom of your library in "
        + "a random order.")]
    [InlineData("Muerra, Trash Tactician", "Legendary Creature — Raccoon Warrior",
        "At the beginning of your first main phase, add {R} or {G} for each Raccoon you control.\nWhenever you "
        + "expend 4, you gain 3 life. (You expend 4 as you spend your fourth total mana to cast spells during a "
        + "turn.)\nWhenever you expend 8, exile the top two cards of your library. Until the end of your next "
        + "turn, you may play those cards.")]
    public void Classify_FreeManaFromATrigger_IsRampNotManaFixing(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Ramp));
        Assert.False(result.HasFlag(CardEffect.ManaFixing));
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
    // A land that makes more mana than it costs, every turn, is Ramp — the one
    // exception to "a land tapping for its own mana is just a land". Ruled
    // 2026-09-15 off Ancient Tomb.
    [InlineData("Ancient Tomb", "Land", "{T}: Add {C}{C}. This land deals 2 damage to you.")]
    [InlineData("Mishra's Workshop", "Land", "{T}: Add {C}{C}{C}. Spend this mana only to cast artifact spells.")]
    // (Karoo was pinned here, with invented text, until the Karoos lost Ramp on
    // 2026-10-13 — see Classify_ALandOfOneColour_IsJustALand.)
    // Three more shapes read 2026-09-29: two mana behind an ability word
    // (Muraganda Raceway), a station land paying one for one per creature
    // (Evendo), and a paid amount counted by the board (Three Tree City).
    [InlineData("Muraganda Raceway", "Land",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns "
        + "when an opponent loses life. Max speed is 4.)\n{T}: Add {C}.\nMax speed — {T}: Add {C}{C}.")]
    [InlineData("Evendo, Waking Haven", "Land — Planet",
        "This land enters tapped.\n{T}: Add {G}.\nStation (Tap another creature you control: Put charge "
        + "counters equal to its power on this Planet. Station only as a sorcery.)\n12+ | {G}, {T}: Add {G} for "
        + "each creature you control.")]
    [InlineData("Three Tree City", "Legendary Land",
        "As Three Tree City enters, choose a creature type.\n{T}: Add {C}.\n{2}, {T}: Choose a color. Add an "
        + "amount of mana of that color equal to the number of creatures you control of the chosen type.")]
    // "X mana of any one color" counted on a land, ruled 2026-10-13.
    [InlineData("Baldur's Gate", "Legendary Land — Gate",
        "{T}: Add {C}.\n{2}, {T}: Add X mana of any one color, where X is the number of other Gates you "
        + "control.")]
    public void Classify_ALandThatMakesExtraManaEveryTurn_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Lands out of a graveyard the card has just FILLED itself, ruled
    // 2026-10-13 (3 of 3 reviewed: Blossoming Tortoise, Deeproot Wayfinder,
    // Lumra). Insidious Roots, which makes the tokens it grants mana to, keeps
    // its Ramp beside the veto below.
    [InlineData("Blossoming Tortoise", "Creature — Turtle",
        "Whenever this creature enters or attacks, mill three cards, then return a land card from your "
        + "graveyard to the battlefield tapped.\nActivated abilities of lands you control cost {1} less to "
        + "activate.\nLand creatures you control get +1/+1.")]
    [InlineData("Lumra, Bellow of the Woods", "Legendary Creature — Elemental Bear",
        "Reach, vigilance\nLumra's power and toughness are each equal to the number of lands you control.\nWhen "
        + "Lumra enters, mill four cards. Then return all land cards from your graveyard to the battlefield "
        + "tapped.")]
    [InlineData("Insidious Roots", "Enchantment",
        "Creature tokens you control have \"{T}: Add one mana of any color.\"\nWhenever one or more creature "
        + "cards leave your graveyard, create a 0/1 green Plant creature token, then put a +1/+1 counter on "
        + "each Plant you control.")]
    public void Classify_LandsTheCardMilledOrTokensItMade_AreRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // "Dipendono da condizioni esterne, la carta di suo non è in grado di
    // generarle -> niente tag" (2026-10-13): a graveyard nobody filled
    // (Undergrowth Recon), tokens the card does not make (Jaheira), an explore
    // the card does not do (Nicanzil).
    [InlineData("Undergrowth Recon", "Enchantment",
        "At the beginning of your upkeep, return target land card from your graveyard to the battlefield "
        + "tapped.")]
    [InlineData("Jaheira, Friend of the Forest", "Legendary Creature — Human Elf Druid",
        "Tokens you control have \"{T}: Add {G}.\"\nChoose a Background (You can have a Background as a second "
        + "commander.)")]
    [InlineData("Nicanzil, Current Conductor", "Legendary Creature — Merfolk Scout",
        "Whenever a creature you control explores a land card, you may put a land card from your hand onto "
        + "the battlefield tapped.\nWhenever a creature you control explores a nonland card, put a +1/+1 counter "
        + "on Nicanzil.")]
    public void Classify_ManaOnAConditionTheCardCannotMeet_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // The two land shapes that stay out. A plain dual makes one mana; the
    // self-sacrificing family spends itself for the extra one, which is why
    // Dwarven Ruins is hand-tagged as nothing.
    [InlineData("Tropical Island", "Land — Forest Island", "({T}: Add {G} or {U}.)")]
    // A land that costs one untapped land to enter, or phases out every other
    // turn, does not make more than it costs every turn (Balduvian Trading Post,
    // Teferi's Isle — untagged, measured 2026-09-29).
    [InlineData("Balduvian Trading Post", "Land",
        "If this land would enter, sacrifice an untapped Mountain instead. If you do, put this land onto the "
        + "battlefield. If you don't, put it into its owner's graveyard.\n{T}: Add {C}{R}.\n{1}, {T}: This land "
        + "deals 1 damage to target attacking creature.")]
    [InlineData("Teferi's Isle", "Legendary Land",
        "Phasing (This phases in or out before you untap during each of your untap steps. While it's phased "
        + "out, it's treated as though it doesn't exist.)\nTeferi's Isle enters tapped.\n{T}: Add {U}{U}.")]
    public void Classify_OrdinaryLands_AreNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A KAROO, a land that hands a land back as it enters, is not Ramp: ruled
    // 2026-09-30 ("togliamo Ramp ai Karoo"), Karoo and Coral Atoll named. One
    // colour, or none, is no fixing either (Guildless Commons); the two-colour
    // Ravnica ones are ManaFixing — see Classify_ALandThatFetchesALand_IsManaFixing.
    [InlineData("Karoo", "Land",
        "This land enters tapped.\nWhen this land enters, sacrifice it unless you return an untapped Plains "
        + "you control to its owner's hand.\n{T}: Add {C}{W}.")]
    [InlineData("Coral Atoll", "Land",
        "This land enters tapped.\nWhen this land enters, sacrifice it unless you return an untapped Island "
        + "you control to its owner's hand.\n{T}: Add {C}{U}.")]
    [InlineData("Guildless Commons", "Land",
        "This land enters tapped.\nWhen this land enters, return a land you control to its owner's hand.\n{T}: "
        + "Add {C}{C}.")]
    // Two mana of ONE colour is not two colours (Teferi's Isle, untagged).
    [InlineData("Teferi's Isle", "Legendary Land",
        "Phasing (This phases in or out before you untap during each of your untap steps. While it's phased "
        + "out, it's treated as though it doesn't exist.)\nTeferi's Isle enters tapped.\n{T}: Add {U}{U}.")]
    public void Classify_ALandOfOneColour_IsJustALand(string name, string typeLine, string oracle)
    {
        Assert.Equal(CardEffect.None, EffectClassifier.Classify(MakeCard(name, typeLine, oracle)));
    }

    [Fact]
    // A POWERSTONE is Ramp however it is made, a land included (Hall of
    // Tagsin, 2026-10-13); not one handed to somebody else (Cityscape Leveler).
    public void Classify_APowerstoneForYou_IsRamp()
    {
        Assert.True(EffectClassifier.Classify(MakeCard("Hall of Tagsin", "Land",
            "{T}: Add {C}.\n{1}, {T}: Add one mana of any color.\n{4}, {T}: Create a tapped Powerstone token. (It's "
            + "an artifact with \"{T}: Add {C}. This mana can't be spent to cast a nonartifact spell.\")"))
            .HasFlag(CardEffect.Ramp));
        Assert.False(EffectClassifier.Classify(MakeCard("Cityscape Leveler", "Artifact Creature — Construct",
            "Trample\nWhen you cast this spell and whenever this creature attacks, destroy up to one target "
            + "nonland permanent. Its controller creates a tapped Powerstone token.\nUnearth {8}"))
            .HasFlag(CardEffect.Ramp));
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
        CardEffect effects = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(effects.HasFlag(CardEffect.Ramp));
        Assert.False(effects.HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // A price that is NOT mana — a life, energy, a sacrifice, the tap of other
    // permanents — leaves a mana SOURCE, which is Ramp and not a fixer, ruled
    // 2026-09-29 ("solo il mana converte") from the human's Ramp-only tags on
    // Supportive Parents, Haunted Screen, Solar Transformer and Lotus Ring,
    // overturning the 09-18 reading that pinned Gene Pollinator as a fixer.
    [InlineData("Supportive Parents", "Creature — Human Citizen",
        "Tap two untapped creatures you control: Add one mana of any color.")]
    [InlineData("Haunted Screen", "Artifact",
        "{T}: Add {W} or {B}.\n{T}, Pay 1 life: Add {G}, {U}, or {R}.\n{7}: Put seven +1/+1 counters on this "
        + "artifact. It becomes a 0/0 Spirit creature in addition to its other types. Activate only once.")]
    [InlineData("Solar Transformer", "Artifact",
        "This artifact enters tapped.\nWhen this artifact enters, you get {E}{E}{E} (three energy "
        + "counters).\n{T}: Add {C}.\n{T}, Pay {E}: Add one mana of any color.")]
    [InlineData("Lotus Ring", "Artifact — Equipment",
        "Indestructible\nEquipped creature gets +3/+3 and has vigilance and \"{T}, Sacrifice this creature: Add "
        + "three mana of any one color.\"\nEquip {3}")]
    [InlineData("Gene Pollinator", "Artifact Creature — Robot Insect",
        "{T}, Tap an untapped permanent you control: Add one mana of any color.")]
    public void Classify_ANonManaPriceLeavesASource_IsRampNotManaFixing(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Ramp));
        Assert.False(result.HasFlag(CardEffect.ManaFixing));
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
    // Mana a PERMANENT adds on a trigger, read since 2026-09-29 (10 of 12 reviewed
    // permanents written this way are tagged).
    [InlineData("Electro, Assaulting Battery", "Legendary Creature — Human Villain",
        "Flying\nYou don't lose unspent red mana as steps and phases end.\nWhenever you cast an instant or "
        + "sorcery spell, add {R}.\nWhen Electro leaves the battlefield, you may pay {X}. When you do, he deals "
        + "X damage to target player.")]
    [InlineData("Rodeo Pyromancers", "Creature — Human Mercenary",
        "Whenever you cast your first spell each turn, add {R}{R}.")]
    [InlineData("Karn, Legacy Reforged", "Legendary Artifact Creature — Golem",
        "Karn's power and toughness are each equal to the greatest mana value among artifacts you control.\nAt "
        + "the beginning of your upkeep, add {C} for each artifact you control. This mana can't be spent to "
        + "cast nonartifact spells. Until end of turn, you don't lose this mana as steps and phases end.")]
    // A modal card whose FRONT taps for mana keeps it: the land-face strip starts
    // at the land's own first line.
    [InlineData("Tangled Florahedron // Tangled Vale", "Creature — Elemental // Land",
        "{T}: Add {G}.\nThis land enters tapped.\n{T}: Add {G}.")]
    // A land onto the battlefield in three more wordings, read 2026-09-29: dug up
    // and put there in a second sentence, a search whose put comes after a full
    // stop, and a land token.
    [InlineData("Clifftop Lookout", "Creature — Frog Scout",
        "Reach\nWhen this creature enters, reveal cards from the top of your library until you reveal a land "
        + "card. Put that card onto the battlefield tapped and the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Point the Way", "Enchantment",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns "
        + "when an opponent loses life. Max speed is 4.)\n{3}{G}, Sacrifice this enchantment: Search your "
        + "library for up to X basic land cards, where X is your speed. Put those cards onto the battlefield "
        + "tapped, then shuffle.")]
    [InlineData("Overlord of the Hauntwoods", "Enchantment Creature — Avatar Horror",
        "Impending 4—{1}{G}{G} (If you cast this spell for its impending cost, it enters with four time "
        + "counters and isn't a creature until the last is removed. At the beginning of your end step, remove a "
        + "time counter from it.)\nWhenever this permanent enters or attacks, create a tapped colorless land "
        + "token named Everywhere that is every basic land type.")]
    // A spell's mana after a comma or counted before the symbol, read 2026-09-29:
    // Energy Tap, Burnt Offering.
    [InlineData("Energy Tap", "Sorcery",
        "Tap target untapped creature you control. If you do, add an amount of {C} equal to that creature's "
        + "mana value.")]
    [InlineData("Burnt Offering", "Instant",
        "As an additional cost to cast this spell, sacrifice a creature.\nAdd X mana in any combination of {B} "
        + "and/or {R}, where X is the sacrificed creature's mana value.")]
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
    // A mana ability in quotes that is a TOKEN's own (an Eldrazi Spawn's
    // "Sacrifice this token: Add {C}") or the victim's Treasure ("sacrifice this
    // artifact", Kitesail Larcenist) is not the card's, read with the quoted mana
    // abilities on 2026-09-29. Since 2026-11-09 a Spawn is read as a Treasure,
    // and Chittering Dispatcher's ONE, once, is still a rider.
    [InlineData("Chittering Dispatcher", "Creature — Eldrazi Drone",
        "Devoid (This card has no color.)\nMyriad\nWhen this creature leaves the battlefield, create a 0/1 "
        + "colorless Eldrazi Spawn creature token with \"Sacrifice this token: Add {C}.\"")]
    [InlineData("Kitesail Larcenist", "Creature — Human Pirate",
        "Flying, ward {1}\nWhen this creature enters, for each player, choose up to one other target artifact "
        + "or creature that player controls. For as long as this creature remains on the battlefield, the "
        + "chosen permanents become Treasure artifacts with \"{T}, Sacrifice this artifact: Add one mana of any "
        + "color\" and lose all other abilities.")]
    public void Classify_ManaOrLandThatIsNotYours_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A Saga chapter's burst of mana is not Ramp, ruled 2026-09-29 (4 of the 6
    // reviewed chapters that add mana are untagged, Maximum Carnage and Esper
    // Origins among them).
    [InlineData("The Bath Song", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Draw two cards, then discard a card.\nIII — Shuffle any number of target cards from your graveyard "
        + "into your library. Add {U}{U}.")]
    [InlineData("Maximum Carnage", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Until "
        + "your next turn, each creature attacks each combat if able and attacks a player other than you if "
        + "able.\nII — Add {R}{R}{R}.\nIII — This Saga deals 5 damage to each opponent.")]
    public void Classify_ASagaChaptersBurstOfMana_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // The LAND FACE of a modal double-faced card is a land, whose mana is just a
    // land's: 9 of the 10 reviewed pay-3-life MDFCs are untagged, and the human
    // left Boggart Bog without Ramp on 2026-09-23. (Drowner of Truth // Drowned
    // Jungle was pinned here too until 2026-11-09, when its front's two Eldrazi
    // Spawn became Ramp as Treasures; its land face still adds none.)
    [InlineData("Boggart Trawler // Boggart Bog", "Creature — Goblin // Land",
        "When this creature enters, exile target player's graveyard.\nAs this land enters, you may pay 3 life. "
        + "If you don't, it enters tapped.\n{T}: Add {B}.")]
    public void Classify_TheLandFaceOfAModalCard_IsJustALand(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Two land wordings the human does not count as Ramp: a land put onto the
    // battlefield only INSTEAD of into the hand (Embermouth Sentinel, ManaFixing),
    // and the lands of a graveyard returned to the battlefield (1 of 8 reviewed
    // tagged, measured 2026-09-29).
    [InlineData("Embermouth Sentinel", "Artifact Creature — Chimera",
        "When this creature enters, you may search your library for a basic land card, reveal it, then "
        + "shuffle and put that card on top. If you control a Dragon, put that card onto the battlefield tapped "
        + "instead.")]
    [InlineData("Aftermath Analyst", "Creature — Elf Detective",
        "When this creature enters, mill three cards. (Put the top three cards of your library into your "
        + "graveyard.)\n{3}{G}, Sacrifice this creature: Return all land cards from your graveyard to the "
        + "battlefield tapped.")]
    public void Classify_ALandThatIsNotRampByHand_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A spell's mana that rides on an answer — a kill, a counter, a sweep, a
    // regrowth — is not Ramp: 4 of 5 reviewed (Narset's Rebuke, Mana Drain,
    // Reckless Blaze, Sorceress's Schemes); The Last Agni Kai, the fifth, lost its
    // hand Ramp on 2026-11-09. And
    // mana in a keyword's reminder is not the spell's (Fire Nation Attacks'
    // firebending).
    [InlineData("Narset's Rebuke", "Instant",
        "Narset's Rebuke deals 5 damage to target creature. Add {U}{R}{W}. If that creature would die this "
        + "turn, exile it instead.")]
    [InlineData("Mana Drain", "Instant",
        "Counter target spell. At the beginning of your next main phase, add an amount of {C} equal to that "
        + "spell's mana value.")]
    [InlineData("Fire Nation Attacks", "Instant",
        "Create two 2/2 red Soldier creature tokens with firebending 1. (Whenever a creature with firebending "
        + "1 attacks, add {R}. This mana lasts until end of combat.)\nFlashback {8}{R} (You may cast this card "
        + "from your graveyard for its flashback cost. Then exile it.)")]
    public void Classify_ASpellsManaRidingOnAnAnswer_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // …but not mana from an ATTACK or combat damage, ruled 2026-09-29 ("mai Ramp
    // dall'attacco": Tesak), a SPELL's trigger that rides on what it does
    // (Reckless Blaze), or ONE Eldrazi Spawn per trigger (Basking Broodscale) —
    // read as the token's own mana until 2026-11-09, and since then as a
    // Treasure, which one per trigger never ramps.
    [InlineData("Tesak, Judith's Hellhound", "Legendary Creature — Elemental Dog",
        "Unleash (You may have this creature enter with a +1/+1 counter on it. It can't block as long as it "
        + "has a +1/+1 counter on it.)\nOther Dogs you control have unleash.\nCreatures you control with counters "
        + "on them have haste.\nWhenever Tesak attacks, add {R} for each attacking creature.")]
    [InlineData("Reckless Blaze", "Sorcery — Lesson",
        "Reckless Blaze deals 5 damage to each creature. Whenever a creature you control dealt damage this "
        + "way dies this turn, add {R}.")]
    [InlineData("Basking Broodscale", "Creature — Eldrazi Lizard",
        "Devoid (This card has no color.)\n{1}{G}: Adapt 1. (If this creature has no +1/+1 counters on it, put "
        + "a +1/+1 counter on it.)\nWhenever one or more +1/+1 counters are put on this creature, you may create "
        + "a 0/1 colorless Eldrazi Spawn creature token with \"Sacrifice this token: Add {C}.\"")]
    public void Classify_TriggeredManaThatIsNotTheCards_IsNotRamp(string name, string typeLine, string oracle)
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
    // Guild Globe pays {2} for two mana, which adds none, and Civic Gardener
    // untaps "target creature or land" (0 of 8 reviewed). Both untagged Ramp
    // 2026-10-29.
    [InlineData("Guild Globe", "Artifact",
        "When this artifact enters, draw a card.\n{2}, {T}, Sacrifice this artifact: Add two mana of different "
        + "colors.")]
    [InlineData("Civic Gardener", "Creature — Human Citizen",
        "Whenever this creature attacks, untap target creature or land.")]
    public void Classify_TwoManaPaidForWithTwoOrACreatureUntapped_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-10-24: mana KEPT from one step to the next for good is Ramp
    // (Horizon Stone, tagged by hand; Kruphix with it). Read the same day, one
    // reviewed card each, Ramp by hand: a spell's mode that adds mana (Jeska's
    // Will), lands an opponent searches put under your control (Rootweaver Druid),
    // mana for each player (Yurlok of Scorch Thrash), X mana a sentence after a
    // charge counter (Empowered Autogenerator), a mana token made by an activated
    // ability (Kibo, Uktabi Prince), lands searched before a d20 (Druid of the
    // Emerald Grove), and 'until you exile a land card' (Venture Forth). A spell's
    // own subtype is no tribe, and neither is face-down: Uncle Iroh and Dream
    // Chisel keep their cost reduction.
    [InlineData("Horizon Stone", "Artifact",
        "If you would lose unspent mana, that mana becomes colorless instead.")]
    [InlineData("Kruphix, God of Horizons", "Legendary Enchantment Creature — God",
        "Indestructible\nAs long as your devotion to green and blue is less than seven, Kruphix isn't a "
        + "creature.\nYou have no maximum hand size.\nIf you would lose unspent mana, that mana becomes colorless "
        + "instead.")]
    [InlineData("Jeska's Will", "Sorcery",
        "Choose one. If you control a commander as you cast this spell, you may choose both instead.\n• Add "
        + "{R} for each card in target opponent's hand.\n• Exile the top three cards of your library. You may "
        + "play them this turn.")]
    [InlineData("Rootweaver Druid", "Creature — Elf Druid",
        "When this creature enters, each opponent may search their library for up to three basic land cards. "
        + "They each put one of those cards onto the battlefield tapped under your control and the rest onto "
        + "the battlefield tapped under their control. Then each player who searched their library this way "
        + "shuffles.")]
    [InlineData("Yurlok of Scorch Thrash", "Legendary Creature — Lizard Shaman",
        "Vigilance\nA player losing unspent mana causes that player to lose that much life.\n{1}, {T}: Each "
        + "player adds {B}{R}{G}.")]
    [InlineData("Empowered Autogenerator", "Artifact",
        "This artifact enters tapped.\n{T}: Put a charge counter on this artifact. Add X mana of any one "
        + "color, where X is the number of charge counters on this artifact.")]
    [InlineData("Kibo, Uktabi Prince", "Legendary Creature — Monkey Noble",
        "{T}: Each player creates a colorless artifact token named Banana with \"{T}, Sacrifice this token: "
        + "Add {R} or {G}. You gain 2 life.\"\nWhenever an artifact an opponent controls is put into a graveyard "
        + "from the battlefield, put a +1/+1 counter on each creature you control that's an Ape or a "
        + "Monkey.\nWhenever Kibo attacks, defending player sacrifices an artifact of their choice.")]
    [InlineData("Druid of the Emerald Grove", "Creature — Dwarf Druid",
        "When this creature enters, search your library for up to two basic land cards and reveal them, then "
        + "roll a d20.\n9 or less | Put those cards into your hand, then shuffle.\n10—19 | Put one of those cards "
        + "onto the battlefield tapped and the other into your hand, then shuffle.\n20+ | Put those cards onto "
        + "the battlefield tapped, then shuffle.")]
    [InlineData("Venture Forth", "Sorcery",
        "Exile cards from the top of your library until you exile a land card. Put that card onto the "
        + "battlefield and the rest on the bottom of your library in a random order. Exile Venture Forth with "
        + "three time counters on it.\nSuspend 3—{1}{G}")]
    [InlineData("Uncle Iroh", "Legendary Creature — Human Noble Ally",
        "Firebending 1 (Whenever this creature attacks, add {R}. This mana lasts until end of combat.)\nLesson "
        + "spells you cast cost {1} less to cast.")]
    [InlineData("Dream Chisel", "Artifact",
        "Face-down creature spells you cast cost {1} less to cast.")]
    // The same ruling in its other wording, 'Players don't lose unspent mana as
    // steps and phases end'.
    [InlineData("Upwelling", "Enchantment",
        "Players don't lose unspent mana as steps and phases end.")]
    public void Classify_TheOctober24RampPass_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-10-24: a cost reduction for a TRIBE's spells is nothing (Gargos's
    // Hydras, Herald of Slaanesh's Demons, Nogi's Dragons), and kept mana is not
    // Ramp when what is kept comes from attacking (Fangorn, Tree Shepherd; Ozai,
    // the Phoenix King's firebending). Read the same day, untagged by hand: the
    // land the owner of a shuffled permanent finds (Audacious Swap), one land
    // sacrificed for one (Elvish Reclaimer), one Treasure from exiling the card
    // (Halo Scarab) or when its last counter is gone (Reckoner Bankbuster), and a
    // one-time mana mode on a permanent's trigger (Zuko, Conflicted). (Natural
    // Balance, the lands each player short of them searches for, was pinned here
    // until 2026-11-09, when it was ruled Ramp — see TheNovember9RampPass_IsRamp.)
    [InlineData("Gargos, Vicious Watcher", "Legendary Creature — Hydra",
        "Vigilance\nHydra spells you cast cost {4} less to cast.\nWhenever a creature you control becomes the "
        + "target of a spell, Gargos fights up to one target creature you don't control.")]
    [InlineData("Herald of Slaanesh", "Creature — Demon",
        "Locus of Slaanesh — Demon spells you cast cost {2} less to cast.\nOther Demons you control have "
        + "haste.")]
    [InlineData("Nogi, Draco-Zealot", "Legendary Creature — Kobold Shaman",
        "Dragon spells you cast cost {1} less to cast.\nWhenever Nogi attacks, if you control three or more "
        + "Dragons, until end of turn, Nogi becomes a Dragon with base power and toughness 5/5 and gains "
        + "flying.")]
    [InlineData("Fangorn, Tree Shepherd", "Legendary Creature — Treefolk",
        "Treefolk you control have vigilance.\nWhenever one or more Treefolk you control attack, add twice "
        + "that much {G}.\nYou don't lose unspent green mana as steps and phases end.")]
    [InlineData("Ozai, the Phoenix King", "Legendary Creature — Human Noble",
        "Trample, firebending 4, haste\nIf you would lose unspent mana, that mana becomes red instead.\nOzai "
        + "has flying and indestructible as long as you have six or more unspent mana.")]
    [InlineData("Audacious Swap", "Instant",
        "Casualty 2 (As you cast this spell, you may sacrifice a creature with power 2 or greater. When you "
        + "do, copy this spell and you may choose a new target for the copy.)\nThe owner of target "
        + "nonenchantment permanent shuffles it into their library, then exiles the top card of their library. "
        + "If it's a land card, they put it onto the battlefield. Otherwise, they may cast it without paying "
        + "its mana cost.")]
    [InlineData("Elvish Reclaimer", "Creature — Elf Warrior",
        "This creature gets +2/+2 as long as there are three or more land cards in your graveyard.\n{2}, {T}, "
        + "Sacrifice a land: Search your library for a land card, put it onto the battlefield tapped, then "
        + "shuffle.")]
    [InlineData("Halo Scarab", "Artifact Creature — Insect",
        "{2}, Exile this card from your graveyard: Create a Treasure token. (It's an artifact with \"{T}, "
        + "Sacrifice this token: Add one mana of any color.\")")]
    [InlineData("Reckoner Bankbuster", "Artifact — Vehicle",
        "This Vehicle enters with three charge counters on it.\n{2}, {T}, Remove a charge counter from this "
        + "Vehicle: Draw a card. Then if there are no charge counters on this Vehicle, create a Treasure token "
        + "and a 1/1 colorless Pilot creature token with \"This token crews Vehicles as though its power were 2 "
        + "greater.\"\nCrew 3")]
    [InlineData("Zuko, Conflicted", "Legendary Creature — Human Rogue",
        "At the beginning of your first main phase, choose one that hasn't been chosen and you lose 2 life "
        + "—\n• Draw a card.\n• Put a +1/+1 counter on Zuko.\n• Add {R}.\n• Exile Zuko, then return him to the "
        + "battlefield under an opponent's control.")]
    public void Classify_TheOctober24RampPass_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Treasures 'target opponent creates' are theirs (Vazi, Keen Negotiator,
    // CardAdvantage and Buff by hand, read 2026-10-24; Wanted Scoundrels with it).
    [InlineData("Vazi, Keen Negotiator", "Legendary Creature — Human Advisor",
        "Haste\n{T}: Target opponent creates X Treasure tokens, where X is the number of Treasure tokens you "
        + "created this turn.\nWhenever an opponent casts a spell or activates an ability, if mana from a "
        + "Treasure was spent to cast it or activate it, put a +1/+1 counter on target creature, then draw a "
        + "card.")]
    [InlineData("Wanted Scoundrels", "Creature — Human Pirate",
        "When this creature dies, target opponent creates two Treasure tokens. (They're artifacts with \"{T}, "
        + "Sacrifice this token: Add one mana of any color.\")")]
    public void Classify_TheOctober24RampPass_TreasuresForATargetOpponent_AreNeitherRampNorManaFixing(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Ramp));
        Assert.False(result.HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Readings of 2026-10-16: a chapter's Treasure for each, a land that puts
    // itself from your hand, mana added after an if-you-do, a tap ability that
    // adds mana a sentence later, and lands searched, exiled, then put onto the
    // battlefield.
    [InlineData("Vault 21: House Gambit", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Discard a card, then draw a card.\nIII — Reveal up to five nonland cards from your hand. For each of "
        + "those cards that has the same mana value as another card revealed this way, create a Treasure token.")]
    [InlineData("Talon Gates of Madara", "Land — Gate",
        "When this land enters, up to one target creature phases out.\n{T}: Add {C}.\n{1}, {T}: Add one mana of "
        + "any color.\n{4}: Put this card from your hand onto the battlefield.")]
    [InlineData("Omnath, Locus of All", "Legendary Creature — Phyrexian Elemental",
        "If you would lose unspent mana, that mana becomes black instead.\nAt the beginning of your first main "
        + "phase, look at the top card of your library. You may reveal that card if it has three or more "
        + "colored mana symbols in its mana cost. If you do, add three mana in any combination of its colors "
        + "and put it into your hand. If you don't reveal it, put it into your hand.")]
    [InlineData("Red Death, Shipwrecker", "Legendary Creature — Crab Mutant",
        "Alluring Eyes — {T}: Goad target creature an opponent controls. That player draws a card. You add "
        + "{R}. (Until your next turn, that creature attacks each combat if able and attacks a player other "
        + "than you if able.)")]
    [InlineData("Omenpath Journey", "Enchantment",
        "When this enchantment enters, search your library for up to five land cards that have different "
        + "names, exile them, then shuffle.\nAt the beginning of your end step, choose a card at random exiled "
        + "with this enchantment and put it onto the battlefield tapped.")]
    public void Classify_TheOctober16RampPass_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-10-16: Terra loses Ramp ("Togli Ramp"), and a free cast of a
    // tribe is nothing (C1, Dracogenesis and Charred Foyer). Vetoes: lands
    // untapped beside you can't cast spells, mana only for upkeep costs, a land
    // put from your hand for a creature returned, the mana an Aura gives an
    // opponent's creature, a Moon's basic, a land the dead card becomes, and a
    // Treasure for each outside a Saga's chapter.
    [InlineData("Terra, Magical Adept // Esper Terra", "Legendary Creature — Human Wizard Warrior // Legendary Enchantment Creature — Saga Wizard",
        "When Terra enters, mill five cards. Put up to one enchantment card milled this way into your "
        + "hand.\nTrance — {4}{R}{G}, {T}: Exile Terra, then return it to the battlefield transformed under its "
        + "owner's control. Activate only as a sorcery.\n(As this Saga enters and after your draw step, add a "
        + "lore counter.)\nI, II, III — Create a token that's a copy of target nonlegendary enchantment you "
        + "control. It gains haste. If it's a Saga, put up to three lore counters on it. Sacrifice it at the "
        + "beginning of your next end step.\nIV — Add {W}{W}, {U}{U}, {B}{B}, {R}{R}, and {G}{G}. Exile Esper "
        + "Terra, then return it to the battlefield (front face up).\nFlying")]
    [InlineData("Dracogenesis", "Enchantment",
        "You may cast Dragon spells without paying their mana costs.")]
    [InlineData("Charred Foyer // Warped Space", "Enchantment — Room // Enchantment — Room",
        "At the beginning of your upkeep, exile the top card of your library. You may play it this turn.\n(You "
        + "may cast either half. That door unlocks on the battlefield. As a sorcery, you may pay the mana cost "
        + "of a locked door to unlock it.)\nOnce each turn, you may pay {0} rather than pay the mana cost for a "
        + "spell you cast from exile.\n(You may cast either half. That door unlocks on the battlefield. As a "
        + "sorcery, you may pay the mana cost of a locked door to unlock it.)")]
    [InlineData("Innocuous Researcher", "Creature — Centaur Detective",
        "Parley — Whenever this creature attacks, each player reveals the top card of their library. For each "
        + "nonland card revealed this way, you investigate. Then each player draws a card.\nAt the beginning of "
        + "your end step, you may untap all lands you control. If you do, you can't cast spells until your next "
        + "turn.")]
    [InlineData("Snowfall", "Enchantment",
        "Cumulative upkeep {U} (At the beginning of your upkeep, put an age counter on this permanent, then "
        + "sacrifice it unless you pay its upkeep cost for each age counter on it.)\nWhenever an Island is "
        + "tapped for mana, its controller may add an additional {U}. If that Island is snow, its controller "
        + "may add an additional {U}{U} instead. Spend this mana only to pay cumulative upkeep costs.")]
    [InlineData("Urban Retreat", "Land",
        "This land enters tapped.\n{T}: Add {G}, {W}, or {U}.\n{2}, Return a tapped creature you control to its "
        + "owner's hand: Put this card from your hand onto the battlefield. Activate only as a sorcery.")]
    [InlineData("Honest Work", "Enchantment — Aura",
        "Enchant creature an opponent controls\nWhen this Aura enters, tap enchanted creature and remove all "
        + "counters from it.\nEnchanted creature loses all abilities and is a Citizen with base power and "
        + "toughness 1/1 and \"{T}: Add {C}\" named Humble Merchant. (It loses all other creature types and "
        + "names.)")]
    [InlineData("Zhao, the Moon Slayer", "Legendary Creature — Human Soldier",
        "Menace\nNonbasic lands enter tapped.\n{7}: Put a conqueror counter on Zhao.\nAs long as Zhao has a "
        + "conqueror counter on him, nonbasic lands are Mountains. (They lose all other land types and "
        + "abilities and have \"{T}: Add {R}.\")")]
    [InlineData("Princess Yue", "Legendary Creature — Human Noble Ally",
        "When Princess Yue dies, if she was a nonland creature, return this card to the battlefield tapped "
        + "under your control. She's a land named Moon. She gains \"{T}: Add {C}.\" (She's still legendary.)\n{T}: "
        + "Scry 2.")]
    [InlineData("Cataclysmic Prospecting", "Sorcery",
        "Cataclysmic Prospecting deals X damage to each creature. For each mana from a Desert spent to cast "
        + "this spell, create a tapped Treasure token.")]
    public void Classify_TheOctober16RampPass_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-11-03 (Morophon, the Boundless is Ramp): a cost reduction for
    // OTHER spells written with several coloured symbols is the same discount as
    // one symbol, and a chosen type is no tribe (2026-10-30). Avatar Aang is a
    // reviewed card (09-11, no Ramp) the rule reaches; it is handed back.
    [InlineData("Morophon, the Boundless", "Legendary Creature — Shapeshifter",
        "Changeling (This card is every creature type.)\nAs Morophon enters, choose a creature type.\nSpells of "
        + "the chosen type you cast cost {W}{U}{B}{R}{G} less to cast. This effect reduces only the amount of "
        + "colored mana you pay.\nOther creatures you control of the chosen type get +1/+1.")]
    [InlineData("Bard Class", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nLegendary creatures you control enter with an "
        + "additional +1/+1 counter on them.\n{R}{G}: Level 2\nLegendary spells you cast cost {R}{G} less to "
        + "cast. This effect reduces only the amount of colored mana you pay.\n{3}{R}{G}: Level 3\nWhenever you "
        + "cast a legendary spell, exile the top two cards of your library. You may play them this turn.")]
    [InlineData("Avatar Aang // Aang, Master of Elements", "Legendary Creature — Human Avatar Ally // Legendary Creature — Avatar Ally",
        "Flying, firebending 2\nWhenever you waterbend, earthbend, firebend, or airbend, draw a card. Then if "
        + "you've done all four this turn, transform Avatar Aang.\nFlying\nSpells you cast cost {W}{U}{B}{R}{G} "
        + "less to cast. (This can reduce generic costs.)\nAt the beginning of each upkeep, you may transform "
        + "Aang, Master of Elements. If you do, you gain 4 life, draw four cards, put four +1/+1 counters on "
        + "him, and he deals 4 damage to each opponent.")]
    public void Classify_TheNovember3RampPass_ARunOfColouredSymbolsLess_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A tribe's coloured discount stays nothing with several symbols (2026-10-24,
    // tribal): Edgewalker's Clerics and Ragemonger's Minotaurs.
    [InlineData("Edgewalker", "Creature — Human Cleric",
        "Cleric spells you cast cost {W}{B} less to cast. This effect reduces only the amount of colored mana "
        + "you pay. (For example, if you cast a Cleric spell with mana cost {1}{W}, it costs {1} to cast.)")]
    [InlineData("Ragemonger", "Creature — Minotaur Shaman",
        "Minotaur spells you cast cost {B}{R} less to cast. This effect reduces only the amount of colored "
        + "mana you pay. (For example, if you cast a Minotaur spell with mana cost {2}{R}, it costs {2} to "
        + "cast.)")]
    // A self-discount in a run of coloured symbols stays out too: Even the Score's
    // "this spell costs {U}{U}{U} less to cast" is a discount on itself.
    [InlineData("Even the Score", "Instant",
        "This spell costs {U}{U}{U} less to cast if an opponent has drawn four or more cards this turn.\nDraw "
        + "X cards.")]
    public void Classify_TheNovember3RampPass_ATribesRunOfSymbolsLess_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Read 2026-11-05: a land onto the battlefield by any wording is Ramp
    // (Ramp.md). The gap before onto the battlefield runs past 60 characters
    // (Jaheira's Respite, Harvest Season, Sylvan Primordial), and the put sits
    // behind a condition (Scholar of New Horizons, Stoic Farmer, Ruin in Their
    // Wake, Boreas Charger).
    [InlineData("Jaheira's Respite", "Instant",
        "Search your library for up to X basic land cards, where X is the number of creatures attacking you, "
        + "put those cards onto the battlefield tapped, then shuffle.\nPrevent all combat damage that would be "
        + "dealt this turn.")]
    [InlineData("Harvest Season", "Sorcery",
        "Search your library for up to X basic land cards, where X is the number of tapped creatures you "
        + "control, put those cards onto the battlefield tapped, then shuffle.")]
    [InlineData("Sylvan Primordial", "Creature — Avatar",
        "Reach\nWhen this creature enters, for each opponent, destroy target noncreature permanent that player "
        + "controls. Search your library for up to X Forest cards, where X is the number of permanents "
        + "destroyed this way, put those cards onto the battlefield tapped, then shuffle.")]
    [InlineData("Scholar of New Horizons", "Creature — Human Scout",
        "This creature enters with a +1/+1 counter on it.\n{T}, Remove a counter from a permanent you control: "
        + "Search your library for a Plains card and reveal it. If an opponent controls more lands than you, "
        + "you may put that card onto the battlefield tapped. If you don't put the card onto the battlefield, "
        + "put it into your hand. Then shuffle.")]
    [InlineData("Stoic Farmer", "Creature — Dwarf Peasant",
        "When this creature enters, search your library for a basic Plains card and reveal it. If an opponent "
        + "controls more lands than you, put it onto the battlefield tapped. Otherwise, put it into your hand. "
        + "Then shuffle.\nForetell {1}{W} (During your turn, you may pay {2} and exile this card from your hand "
        + "face down. Cast it on a later turn for its foretell cost.)")]
    [InlineData("Ruin in Their Wake", "Sorcery",
        "Devoid (This card has no color.)\nSearch your library for a basic land card and reveal it. You may "
        + "put that card onto the battlefield tapped if you control a land named Wastes. Otherwise, put that "
        + "card into your hand. Then shuffle.")]
    [InlineData("Boreas Charger", "Creature — Pegasus",
        "Flying\nWhen this creature leaves the battlefield, choose an opponent who controls more lands than "
        + "you. Search your library for a number of Plains cards equal to the difference, reveal those cards, "
        + "put one of them onto the battlefield tapped and the rest into your hand, then shuffle.")]
    public void Classify_TheNovember5Readings_ALandOntoTheBattlefield_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Embermouth Sentinel puts the land on top and the battlefield is only its
    // fallback with a Dragon: ManaFixing alone by hand, so the put that says
    // instead stays out.
    [InlineData("Embermouth Sentinel", "Artifact Creature — Chimera",
        "When this creature enters, you may search your library for a basic land card, reveal it, then "
        + "shuffle and put that card on top. If you control a Dragon, put that card onto the battlefield tapped "
        + "instead.")]
    public void Classify_TheNovember5Readings_AFallbackPutInsteadOfOnTop_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Read 2026-11-05 under the 2026-10-30 Guild Globe ruling: mana that costs
    // mana and adds the same amount adds none. Jenson Carthalion pays {5} for
    // WUBRG (reviewed 11-03, no Ramp); the Eggs and Kaleidostone pay as much as
    // they add.
    [InlineData("Jenson Carthalion, Druid Exile", "Legendary Creature — Human Druid",
        "Whenever you cast a multicolored spell, scry 1. If that spell was all colors, create a 4/4 white "
        + "Angel creature token with flying and vigilance.\n{5}, {T}: Add {W}{U}{B}{R}{G}.")]
    [InlineData("Darkwater Egg", "Artifact",
        "{2}, {T}, Sacrifice this artifact: Add {U}{B}. Draw a card. (Activate only as an instant.)")]
    [InlineData("Kaleidostone", "Artifact",
        "When this artifact enters, draw a card.\n{5}, {T}, Sacrifice this artifact: Add {W}{U}{B}{R}{G}.")]
    [InlineData("Coal Golem", "Artifact Creature — Golem",
        "{3}, Sacrifice this creature: Add {R}{R}{R}.")]
    public void Classify_TheNovember5Readings_APaidAbilityThatAddsAsMuchAsItCosts_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // ...while the ones that give back more than they took stay Ramp: the Signets
    // pay {1} for two, Codie and Cormela add more than the mana they cost.
    [InlineData("Azorius Signet", "Artifact",
        "{1}, {T}: Add {W}{U}.")]
    [InlineData("Codie, Vociferous Codex", "Legendary Artifact Creature — Book Construct",
        "You can't cast permanent spells.\n{4}, {T}: Add {W}{U}{B}{R}{G}. When you next cast a spell this "
        + "turn, exile cards from the top of your library until you exile an instant or sorcery card with "
        + "lesser mana value. Until end of turn, you may cast that card without paying its mana cost. Put each "
        + "other card exiled this way on the bottom of your library in a random order.")]
    [InlineData("Cormela, Glamour Thief", "Legendary Creature — Vampire Rogue",
        "Haste\n{1}, {T}: Add {U}{B}{R}. Spend this mana only to cast instant and/or sorcery spells.\nWhen "
        + "Cormela dies, return up to one target instant or sorcery card from your graveyard to your hand.")]
    public void Classify_TheNovember5Readings_APaidAbilityThatAddsMoreThanItCosts_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Read 2026-11-05 under the 2026-10-15 No Ramp ruling: one Treasure from an
    // ability that discards the card itself is a rider, like the sacrifice (Magma
    // Opus, untagged by hand).
    [InlineData("Magma Opus", "Instant",
        "Magma Opus deals 4 damage divided as you choose among any number of targets. Tap two target "
        + "permanents. Create a 4/4 blue and red Elemental creature token. Draw two cards.\n{U/R}{U/R}, Discard "
        + "this card: Create a Treasure token.")]
    [InlineData("Creative Outburst", "Instant",
        "Creative Outburst deals 5 damage to any target. Look at the top five cards of your library. Put one "
        + "of them into your hand and the rest on the bottom of your library in a random order.\n{U/R}{U/R}, "
        + "Discard this card: Create a Treasure token.")]
    [InlineData("Elemental Masterpiece", "Sorcery",
        "Create two 4/4 blue and red Elemental creature tokens.\n{U/R}{U/R}, Discard this card: Create a "
        + "Treasure token. (It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")")]
    public void Classify_TheNovember5Readings_ADiscardedCardsTreasure_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-11-09, the Ramp definition (mana YOURS beyond the land of the
    // turn, Rampant Growth): an Eldrazi Spawn or Scion is a Treasure ('Come i
    // Treasure'), so two Scions at every end step (Spawnbed Protector), three when
    // cast (Spawn-Gang Commander) and 'that many' from combat damage (Rapacious
    // One, as the 11 reviewed Treasure cards that make several from combat: 'Solo
    // i Treasure') are Ramp; the lands each player short of them searches for are
    // yours too (Natural Balance, '+Ramp', as Veteran Explorer); and a mana mode
    // on a trigger whose modes come back every turn (Galadriel, Light of Valinor,
    // read).
    [InlineData("Spawnbed Protector", "Creature — Eldrazi",
        "At the beginning of your end step, return up to one target Eldrazi creature card from your graveyard "
        + "to your hand. Create two 1/1 colorless Eldrazi Scion creature tokens with \"Sacrifice this token: Add "
        + "{C}.\"")]
    [InlineData("Spawn-Gang Commander", "Creature — Eldrazi Goblin",
        "Devoid (This card has no color.)\nWhen you cast this spell, create three 0/1 colorless Eldrazi Spawn "
        + "creature tokens with \"Sacrifice this token: Add {C}.\"\n{1}{C}, Sacrifice an Eldrazi: This creature "
        + "deals 2 damage to any target.")]
    [InlineData("Rapacious One", "Creature — Eldrazi Drone",
        "Trample\nWhenever this creature deals combat damage to a player, create that many 0/1 colorless "
        + "Eldrazi Spawn creature tokens. They have \"Sacrifice this token: Add {C}.\"")]
    [InlineData("Natural Balance", "Sorcery",
        "Each player who controls six or more lands chooses five lands they control and sacrifices the rest. "
        + "Each player who controls four or fewer lands may search their library for up to X basic land cards "
        + "and put them onto the battlefield, where X is five minus the number of lands they control. Then each "
        + "player who searched their library this way shuffles.")]
    [InlineData("Galadriel, Light of Valinor", "Legendary Creature — Elf Noble",
        "Alliance — Whenever another creature you control enters, choose one that hasn't been chosen this "
        + "turn —\n• Add {G}{G}{G}.\n• Put a +1/+1 counter on each creature you control.\n• Scry 2, then draw a "
        + "card.")]
    public void Classify_TheNovember9RampPass_IsRamp(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-11-09: lands untapped by an attack or by combat damage are mana
    // from the attack and never Ramp, only several Treasures last past the turn
    // ('Solo i Treasure': Pip-Boy 3000's mode, Bumi, Unleashed handed back); the
    // NEXT spell of a tribe costing less is the tribe's (Invasion of the Giants,
    // read); a creature typed Treasure cracking itself is one Treasure, colour
    // ('-Ramp +ManaFixing': Goldhound).
    [InlineData("Pip-Boy 3000", "Artifact — Equipment",
        "Whenever equipped creature attacks, choose one —\n• Sort Inventory — Draw a card, then discard a "
        + "card.\n• Pick a Perk — Put a +1/+1 counter on that creature.\n• Check Map — Untap up to two target "
        + "lands.\nEquip {2}")]
    [InlineData("Bumi, Unleashed", "Legendary Creature — Human Noble Ally",
        "Trample\nWhen Bumi enters, earthbend 4.\nWhenever Bumi deals combat damage to a player, untap all "
        + "lands you control. After this phase, there is an additional combat phase. Only land creatures can "
        + "attack during that combat phase.")]
    [InlineData("Invasion of the Giants", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Scry "
        + "2.\nII — Draw a card. Then you may reveal a Giant card from your hand. When you do, this Saga deals 2 "
        + "damage to target opponent or planeswalker.\nIII — The next Giant spell you cast this turn costs {2} "
        + "less to cast.")]
    [InlineData("Goldhound", "Artifact Creature — Treasure Dog",
        "First strike\nMenace (This creature can't be blocked except by two or more creatures.)\n{T}, Sacrifice "
        + "this creature: Add one mana of any color.")]
    public void Classify_TheNovember9RampPass_IsNotRamp(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Ruled 2026-11-09 ('-Ramp +ManaFixing'): the 2026-10-15 ruling on a card
    // typed Treasure cracking itself covers a creature too, so Goldhound fixes
    // once like Buried Treasure, while the untyped Lotus Petal and Blood Pet stay
    // Ramp.
    [InlineData("Goldhound", "Artifact Creature — Treasure Dog",
        "First strike\nMenace (This creature can't be blocked except by two or more creatures.)\n{T}, Sacrifice "
        + "this creature: Add one mana of any color.")]
    public void Classify_TheNovember9RampPass_ACreatureTypedTreasure_IsManaFixingNotRamp(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Ramp));
    }
}
