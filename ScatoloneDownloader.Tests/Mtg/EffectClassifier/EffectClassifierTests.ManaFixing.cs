using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // A LAND is exempt from that, ruled 2026-10-13: the restricted lands are
    // ManaFixing by hand 8 of 8, the restricted non-lands 0 of 8. Great Hall of
    // the Citadel also needed "two mana in any combination of colors" read,
    // which Wizard's Rockets, paying {X} for it, shows on a non-land.
    [InlineData("Great Hall of the Citadel", "Land",
        "{T}: Add {C}.\n{1}, {T}: Add two mana in any combination of colors. Spend this mana only to cast "
        + "legendary spells.")]
    [InlineData("Ancient Ziggurat", "Land",
        "{T}: Add one mana of any color. Spend this mana only to cast a creature spell.")]
    [InlineData("Wizard's Rockets", "Artifact",
        "This artifact enters tapped.\n{X}, {T}, Sacrifice this artifact: Add X mana in any combination of "
        + "colors.\nWhen this artifact is put into a graveyard from the battlefield, draw a card.")]
    public void Classify_ARestrictedLandOrAnyCombinationOfColours_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
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
    // …but pay MANA on top of the tap and the card converts colour instead of
    // making it, which is the whole job. Any price converted until 2026-09-29;
    // since "solo il mana converte" only mana does (Gene Pollinator moved to
    // Classify_ANonManaPriceLeavesASource_IsRampNotManaFixing). A LAND is exempt
    // and never asked — 17 of 17.
    [InlineData("Mana Prism", "Artifact", "{T}: Add {C}.\n{1}, {T}: Add one mana of any color.")]
    [InlineData("Celestial Prism", "Artifact", "{2}, {T}: Add one mana of any color.")]
    [InlineData("Birds of Paradise, but a land", "Land", "{T}: Add one mana of any color.")]
    // The mana paid one line up for the token later cracked (Diamond
    // Kaleidoscope's {3} Prism), and one mana of any colour GRANTED TO LANDS, a
    // land's colour (Dune Chanter, ManaFixing by hand).
    [InlineData("Diamond Kaleidoscope", "Artifact",
        "{3}, {T}: Create a 0/1 colorless Prism artifact creature token.\nSacrifice a Prism token: Add one "
        + "mana of any color.")]
    [InlineData("Dune Chanter", "Creature — Plant Druid",
        "Reach\nLands you control and land cards you own that aren't on the battlefield are Deserts in "
        + "addition to their other types.\nLands you control have \"{T}: Add one mana of any color.\"\n{T}: Mill "
        + "two cards. You gain 1 life for each land card milled this way.")]
    // North Star's whole activation is the permission to spend mana as any type,
    // and it fixes.
    [InlineData("North Star", "Artifact",
        "{4}, {T}: For one spell this turn, you may spend mana as though it were mana of any type to pay that "
        + "spell's mana cost. (Additional costs are still paid normally.)")]
    public void Classify_ManaThatCostsSomething_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // A LAND that fetches a land onto the battlefield, sacrificing itself or for a
    // price, is a fetch land: 18 of 18 reviewed cards written this way are
    // ManaFixing (Shire Terrace the latest, 2026-09-29), and none is Ramp.
    [InlineData("Shire Terrace", "Land",
        "{T}: Add {C}.\n{1}, {T}, Sacrifice this land: Search your library for a basic land card, put it onto "
        + "the battlefield tapped, then shuffle.")]
    [InlineData("Thawing Glaciers", "Land",
        "This land enters tapped.\n{1}, {T}: Search your library for a basic land card, put that card onto the "
        + "battlefield tapped, then shuffle. Return this land to its owner's hand at the beginning of the next "
        + "cleanup step.")]
    [InlineData("Bountiful Landscape", "Land",
        "{T}: Add {C}.\n{T}, Sacrifice this land: Search your library for a basic Forest, Island, or Mountain "
        + "card, put it onto the battlefield tapped, then shuffle.\nCycling {G}{U}{R} ({G}{U}{R}, Discard this "
        + "card: Draw a card.)")]
    // A land of the colour, or the basic land type, chosen as it enters fixes too
    // (6 of 6 reviewed): Mirage Mesa, Multiversal Passage.
    [InlineData("Mirage Mesa", "Land — Desert",
        "This land enters tapped. As it enters, choose a color.\n{T}: Add one mana of the chosen color.")]
    [InlineData("Multiversal Passage", "Land",
        "As this land enters, choose a basic land type. Then you may pay 2 life. If you don't, it enters "
        + "tapped.\nThis land is the chosen type.")]
    // …and so does a land tapping for two colours at once: the Ravnica Karoos,
    // ruled 2026-10-13 once the Karoos lost Ramp.
    [InlineData("Azorius Chancery", "Land",
        "This land enters tapped.\nWhen this land enters, return a land you control to its owner's hand.\n{T}: "
        + "Add {W}{U}.")]
    public void Classify_ALandThatFetchesALand_IsManaFixing(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // "Spend mana as though it were mana of any type" riding on what the card does
    // fixes no mana base: 3 of the 4 reviewed riders are untagged (Abstruse
    // Appropriation, Outrageous Robbery, Case File Auditor), read 2026-09-29.
    // North Star, whose whole activation is the permission, is the exception.
    [InlineData("Abstruse Appropriation", "Instant",
        "Devoid (This card has no color.)\nExile target nonland permanent. You may cast that card for as long "
        + "as it remains exiled, and you may spend colorless mana as though it were mana of any color to cast "
        + "that spell.")]
    [InlineData("Case File Auditor", "Creature — Human Detective",
        "When this creature enters and whenever you solve a Case, look at the top six cards of your library. "
        + "You may reveal an enchantment card from among them and put it into your hand. Put the rest on the "
        + "bottom of your library in a random order.\nYou may spend mana as though it were mana of any color to "
        + "cast Case spells.")]
    public void Classify_APermissionToSpendManaAsAnyType_IsNotManaFixing(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
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

    [Theory]
    // …and a land face that taps for two colours fixes, as any land does (Drowned
    // Jungle, ManaFixing by hand).
    [InlineData("Drowner of Truth // Drowned Jungle", "Creature — Eldrazi // Land",
        "Devoid (This card has no color.)\nWhen you cast this spell, if {C} was spent to cast it, create two "
        + "0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this token: Add {C}.\"\nThis land enters "
        + "tapped.\n{T}: Add {G} or {U}.")]
    public void Classify_ADualLandFaceOfAModalCard_Fixes(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // A Treasure is a Lotus Petal in token form: it always fixes, and it ramps
    // when you get several at once — ruled 2026-09-18 — or when an ACTIVATED
    // ability makes it. One, once, is a rider, the line drawn for the Clue; and
    // ONE PER TRIGGER is never Ramp, ruled 2026-09-29 ("un Treasure per trigger
    // mai"), which moved Gilded Ghoda from true to false and reads "a Treasure
    // for each" on a trigger as one per trigger (Edward Kenway). Treasures
    // counted by a removal spell's EXCESS DAMAGE are its rider (Bottle-Cap
    // Blast, Hell to Pay).
    [InlineData("Professional Wrestler", "Creature — Human Warrior",
        "When this creature enters, create a Treasure token.", false)]
    [InlineData("Gilded Ghoda", "Creature — Frog Mount",
        "Whenever this creature attacks while saddled, create a Treasure token.", false)]
    [InlineData("Unexpected Windfall", "Instant",
        "As an additional cost to cast this spell, discard a card.\n"
        + "Draw two cards and create two Treasure tokens.", true)]
    [InlineData("Fountainport", "Land",
        "{T}: Add {C}.\n{2}, {T}, Sacrifice a token: Draw a card.\n{3}, {T}, Pay 1 life: Create a 1/1 blue Fish "
        + "creature token.\n{4}, {T}: Create a Treasure token.", true)]
    [InlineData("Prosperous Bandit", "Creature — Raccoon Rogue",
        "Offspring {1} (You may pay an additional {1} as you cast this spell. If you do, when this creature "
        + "enters, create a 1/1 token copy of it.)\nFirst strike\nWhenever this creature deals combat damage to a "
        + "player, create that many tapped Treasure tokens.", true)]
    [InlineData("Covetous Elegy", "Sorcery",
        "Each player chooses up to two creatures they control, then sacrifices the rest. Then you create a "
        + "tapped Treasure token for each creature your opponents control.", true)]
    [InlineData("Bootleggers' Stash", "Artifact",
        "Lands you control have \"{T}: Create a Treasure token.\"", true)]
    [InlineData("Edward Kenway", "Legendary Creature — Human Assassin Pirate",
        "At the beginning of your end step, create a Treasure token for each tapped Assassin, Pirate, and/or "
        + "Vehicle you control.\nWhenever a Vehicle you control deals combat damage to a player, look at the top "
        + "card of that player's library, then exile it face down. You may play that card for as long as it "
        + "remains exiled.", false)]
    [InlineData("Jolene, Plundering Pugilist", "Legendary Creature — Human Mercenary",
        "Whenever you attack with one or more creatures with power 4 or greater, create a Treasure "
        + "token.\n{1}{R}, Sacrifice a Treasure: Jolene deals 1 damage to any target.", false)]
    [InlineData("Bottle-Cap Blast", "Instant",
        "Improvise (Your artifacts can help cast this spell. Each artifact you tap after you're done "
        + "activating mana abilities pays for {1}.)\nBottle-Cap Blast deals 5 damage to any target. If excess "
        + "damage was dealt to a permanent this way, create that many tapped Treasure tokens. (They're "
        + "artifacts with \"{T}, Sacrifice this token: Add one mana of any color.\")", false)]
    [InlineData("Hell to Pay", "Sorcery",
        "Hell to Pay deals X damage to target creature. Create a number of tapped Treasure tokens equal to "
        + "the amount of excess damage dealt to that creature this way.", false)]
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
    // "Add two mana of different colors", ManaFixing by hand on both reviewed
    // cards (2026-10-29).
    [InlineData("Interplanar Beacon", "Land",
        "Whenever you cast a planeswalker spell, you gain 1 life.\n{T}: Add {C}.\n{1}, {T}: Add two mana of "
        + "different colors. Spend this mana only to cast planeswalker spells.")]
    [InlineData("Guild Globe", "Artifact",
        "When this artifact enters, draw a card.\n{2}, {T}, Sacrifice this artifact: Add two mana of different "
        + "colors.")]
    public void Classify_TwoManaOfDifferentColours_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // 'All lands have "{T}: Add one mana of any color"' is the one mana granted to
    // lands, which fixes (Toxicrene, ManaFixing alone by hand, read 2026-10-24).
    [InlineData("Toxicrene", "Creature — Tyranid",
        "Reach, deathtouch\nHypertoxic Miasma — All lands have \"{T}: Add one mana of any color\" and lose all "
        + "other abilities.")]
    public void Classify_TheOctober24RampPass_OneManaForAllLands_IsManaFixingNotRamp(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Ramp));
        Assert.True(result.HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Ruled 2026-10-16 (M1): a land made the basic type of your choice is
    // ManaFixing ("MF a entrambe", Mystic Compass and Jinx). Readings: lands found
    // and revealed into your hand, and a reveal until a land card goes to hand.
    [InlineData("Mystic Compass", "Artifact",
        "{1}, {T}: Target land becomes the basic land type of your choice until end of turn.")]
    [InlineData("Jinx", "Instant",
        "Target land becomes the basic land type of your choice until end of turn.\nDraw a card at the "
        + "beginning of the next turn's upkeep.")]
    [InlineData("Aang's Journey", "Sorcery — Lesson",
        "Kicker {2} (You may pay an additional {2} as you cast this spell.)\nSearch your library for a basic "
        + "land card. If this spell was kicked, instead search your library for a basic land card and a Shrine "
        + "card. Reveal those cards, put them into your hand, then shuffle.\nYou gain 2 life.")]
    [InlineData("Tithe", "Instant",
        "Search your library for a Plains card. If target opponent controls more lands than you, you may "
        + "search your library for an additional Plains card. Reveal those cards, put them into your hand, then "
        + "shuffle.")]
    [InlineData("Gift of Estates", "Sorcery",
        "If an opponent controls more lands than you, search your library for up to three Plains cards, "
        + "reveal them, put them into your hand, then shuffle.")]
    [InlineData("House Cartographer", "Creature — Human Scout Survivor",
        "Survival — At the beginning of your second main phase, if this creature is tapped, reveal cards from "
        + "the top of your library until you reveal a land card. Put that card into your hand and the rest on "
        + "the bottom of your library in a random order.")]
    public void Classify_TheOctober16ManaFixingPass_IsManaFixing(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Vetoes of 2026-10-16: a Treasure maker lent for a turn, and mana of the
    // colour the cost already paid, or of any colour only when the card returned
    // is green.
    [InlineData("Furnace Reins", "Sorcery",
        "Gain control of target creature until end of turn. Untap that creature. Until end of turn, it gains "
        + "haste and \"Whenever this creature deals combat damage to a player or battle, create a Treasure "
        + "token.\" (It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")")]
    [InlineData("Sunken Palace", "Land — Cave",
        "This land enters tapped.\n{T}: Add {U}.\n{1}{U}, {T}, Exile seven cards from your graveyard: Add {U}. "
        + "When you spend this mana to cast a spell or activate an ability, copy that spell or ability. You may "
        + "choose new targets for the copy. (Mana abilities can't be copied.)")]
    [InlineData("Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar", "Legendary Creature — Moonfolk Wizard // Legendary Planeswalker — Tamiyo",
        "Flying\nWhenever Tamiyo attacks, investigate. (Create a Clue token. It's an artifact with \"{2}, "
        + "Sacrifice this token: Draw a card.\")\nWhen you draw your third card in a turn, exile Tamiyo, then "
        + "return her to the battlefield transformed under her owner's control.\n+2: Until your next turn, "
        + "whenever a creature attacks you or a planeswalker you control, it gets -1/-0 until end of turn.\n−3: "
        + "Return target instant or sorcery card from your graveyard to your hand. If it's a green card, add "
        + "one mana of any color.\n−7: Draw cards equal to half the number of cards in your library, rounded up. "
        + "You get an emblem with \"You have no maximum hand size.\"")]
    public void Classify_TheOctober16ManaFixingPass_IsNotManaFixing(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }

    [Theory]
    // Ruled 2026-11-05 (Stoic Farmer, named): a land searched to hand is
    // ManaFixing, a basic or a Plains card alike, and the same card puts it onto
    // the battlefield on the other branch of the condition, which is Ramp.
    [InlineData("Stoic Farmer", "Creature — Dwarf Peasant",
        "When this creature enters, search your library for a basic Plains card and reveal it. If an opponent "
        + "controls more lands than you, put it onto the battlefield tapped. Otherwise, put it into your hand. "
        + "Then shuffle.\nForetell {1}{W} (During your turn, you may pay {2} and exile this card from your hand "
        + "face down. Cast it on a later turn for its foretell cost.)")]
    [InlineData("Scholar of New Horizons", "Creature — Human Scout",
        "This creature enters with a +1/+1 counter on it.\n{T}, Remove a counter from a permanent you control: "
        + "Search your library for a Plains card and reveal it. If an opponent controls more lands than you, "
        + "you may put that card onto the battlefield tapped. If you don't put the card onto the battlefield, "
        + "put it into your hand. Then shuffle.")]
    [InlineData("Ruin in Their Wake", "Sorcery",
        "Devoid (This card has no color.)\nSearch your library for a basic land card and reveal it. You may "
        + "put that card onto the battlefield tapped if you control a land named Wastes. Otherwise, put that "
        + "card into your hand. Then shuffle.")]
    public void Classify_TheNovember5Ruling_ALandToHandOnTheOtherBranch_IsManaFixing(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.True(result.HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // A creature or land card is a tutor, not a fix (Archdruid's Charm).
    [InlineData("Archdruid's Charm", "Instant",
        "Choose one —\n• Search your library for a creature or land card and reveal it. Put it onto the "
        + "battlefield tapped if it's a land card. Otherwise, put it into your hand. Then shuffle.\n• Put a "
        + "+1/+1 counter on target creature you control. It deals damage equal to its power to target creature "
        + "you don't control.\n• Exile target artifact or enchantment.")]
    public void Classify_TheNovember5Ruling_ACreatureOrLandSearch_IsNotManaFixing(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.ManaFixing));
    }
}
