using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // Regrowth read 2026-10-13: a long list before "from", "graveyard or
    // exiled card", a card exiled from a graveyard and later returned to hand,
    // one of the top two, a pile, the top of the library untargeted, a card
    // exiled out of a graveyard and cast or copied ("Sì"), cast from among
    // what an evidence cost exiled, a cast card described before "from"
    // ("Regrowth sempre"), a keyword a graveyard card gains (escape described
    // or in quotes, read through its cost; flashback GAINED by each).
    [InlineData("Rise from the Wreck", "Sorcery",
        "Return up to one target creature card, up to one target Mount card, up to one target Vehicle card, "
        + "and up to one target creature card with no abilities from your graveyard to your hand.")]
    [InlineData("Sorceress's Schemes", "Sorcery",
        "Return target instant or sorcery card from your graveyard or exiled card with flashback you own to "
        + "your hand. Add {R}.\nFlashback {4}{R} (You may cast this card from your graveyard for its flashback "
        + "cost. Then exile it.)")]
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Mole Beast",
        "When Nyla enters, exile up to one target creature card from your graveyard. If you do, you lose X "
        + "life and create X Clue tokens, where X is that card's mana value. (A Clue token is an artifact with "
        + "\"{2}, Sacrifice this token: Draw a card.\")\nAt the beginning of your end step, if you control no "
        + "Clues, return target card exiled with Nyla to its owner's hand.")]
    [InlineData("Phyrexian Grimoire", "Artifact — Book",
        "{4}, {T}: Target opponent chooses one of the top two cards of your graveyard. Exile that card and "
        + "put the other one into your hand.")]
    [InlineData("Split the Spoils", "Sorcery",
        "Exile up to five target permanent cards from your graveyard and separate them into two piles. An "
        + "opponent chooses one of those piles. Put that pile into your hand and the other into your "
        + "graveyard. (Piles can be empty.)")]
    [InlineData("Halo-Charged Skaab", "Creature — Zombie",
        "When this creature enters, each player mills two cards. Then you may put an instant, sorcery, or "
        + "battle card from your graveyard on top of your library. (To mill two cards, a player puts the top "
        + "two cards of their library into their graveyard.)")]
    [InlineData("Kaervek, the Punisher", "Legendary Creature — Human Warlock",
        "Whenever you commit a crime, exile up to one target black card from your graveyard and copy it. You "
        + "may cast the copy. If you do, you lose 2 life. (Targeting opponents, anything they control, and/or "
        + "cards in their graveyards is a crime. Copies of permanent spells become tokens.)")]
    [InlineData("Psionic Ritual", "Sorcery",
        "Replicate—Tap an untapped Horror you control. (When you cast this spell, copy it for each time you "
        + "paid its replicate cost. You may choose new targets for the copies.)\nExile target instant or "
        + "sorcery card from a graveyard and copy it. You may cast the copy without paying its mana "
        + "cost.\nExile Psionic Ritual.")]
    [InlineData("Kylox's Voltstrider", "Artifact — Vehicle",
        "Collect evidence 6: This Vehicle becomes an artifact creature until end of turn.\nWhenever this "
        + "Vehicle attacks, you may cast an instant or sorcery spell from among cards exiled with it. If that "
        + "spell would be put into a graveyard, put it on the bottom of its owner's library instead.\nCrew 2")]
    [InlineData("Halo Forager", "Creature — Faerie Rogue",
        "Flying\nWhen this creature enters, you may pay {X}. When you do, you may cast target instant or "
        + "sorcery card with mana value X from a graveyard without paying its mana cost. If that spell would "
        + "be put into a graveyard, exile it instead.")]
    [InlineData("Desdemona, Freedom's Edge", "Legendary Creature — Human Rogue",
        "Vigilance\nWhenever Desdemona attacks, target creature card in your graveyard that's an artifact or "
        + "that has mana value 3 or less gains escape until end of turn. The escape cost is equal to its mana "
        + "cost plus exile two other cards from your graveyard. (You may cast it from your graveyard for its "
        + "escape cost this turn.)")]
    [InlineData("The Grim Captain's Locker", "Legendary Artifact",
        "{T}: Surveil 1. (Look at the top card of your library. You may put it into your graveyard.)\n{T}: "
        + "Until end of turn, each creature card in your graveyard gains \"Escape—{3}{B}, Exile four other "
        + "cards from your graveyard.\" (You may cast a card with escape from your graveyard for its escape "
        + "cost.)")]
    [InlineData("Past in Flames", "Sorcery",
        "Each instant and sorcery card in your graveyard gains flashback until end of turn. The flashback "
        + "cost is equal to its mana cost.\nFlashback {4}{R} (You may cast this card from your graveyard for "
        + "its flashback cost. Then exile it.)")]
    public void Classify_ACardBoughtBackFromAGraveyard_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // Not Regrowth: a card returning ITSELF to hand ("Mai Regrowth (come
    // Reanimate)", 2026-10-13), a return that is the price of keeping the
    // card, and a keyword's reminder text.
    [InlineData("Hammer of Bogardan", "Sorcery",
        "Hammer of Bogardan deals 3 damage to any target.\n{2}{R}{R}{R}: Return this card from your "
        + "graveyard to your hand. Activate only during your upkeep.")]
    [InlineData("Harvest Wurm", "Creature — Wurm",
        "When this creature enters, sacrifice it unless you return a basic land card from your graveyard to "
        + "your hand.")]
    [InlineData("Deeproot Historian", "Creature — Merfolk Druid",
        "Merfolk and Druid cards in your graveyard have retrace. (You may cast cards with retrace from your "
        + "graveyard by discarding a land card in addition to paying their other costs.)")]
    public void Classify_ACardBoughtBackForNothing_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
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

    [Theory]
    // A noncreature card comes back from the graveyard, read 2026-10-30:
    // Mazzy's Aura exiled from the graveyard and then cast. (Anikthea's token
    // copy of an enchantment card stood here until 2026-11-06, "Reanimate al
    // posto di Regrowth": moved to EffectClassifierTests.Reanimate.cs.)
    [InlineData("Mazzy, Truesword Paladin", "Legendary Creature — Halfling Knight",
        "Whenever an enchanted creature attacks one of your opponents, it gets +2/+0 and gains trample until "
        + "end of turn.\nWhenever an Aura you control is put into your graveyard from the battlefield, exile it. "
        + "Until the end of your next turn, you may cast that card.")]
    public void Classify_ACardExiledFromTheGraveyardAndCast_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // C4, 2026-10-16: a card cast from ANY graveyard counts for Regrowth, since
    // yours is among them. C4 also counted THEIR graveyard; G1 overturned that
    // half later the same day (Deluxe Dragster: "solo Steal").
    [InlineData("Urabrask // The Great Work", "Legendary Creature — Phyrexian Praetor // Enchantment — Saga",
        "First strike\nWhenever you cast an instant or sorcery spell, Urabrask deals 1 damage to target "
        + "opponent. Add {R}.\n{R}: Exile Urabrask, then return it to the battlefield transformed under its "
        + "owner's control. Activate only as a sorcery and only if you've cast three or more instant and/or "
        + "sorcery spells this turn.\n(As this Saga enters and after your draw step, add a lore counter.)\nI — "
        + "This Saga deals 3 damage to target opponent and each creature they control.\nII — Create three "
        + "Treasure tokens.\nIII — Until end of turn, you may cast instant and sorcery spells from any "
        + "graveyard. If a spell cast this way would be put into a graveyard, exile it instead. Exile this "
        + "Saga, then return it to the battlefield (front face up).")]
    public void Classify_TheOctober16Pass_AnyGraveyardCast_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // Ruled 2026-10-16 (G1): casting a card from an OPPONENT's graveyard is Steal
    // only, never Regrowth ("Solo Steal", named on Deluxe Dragster).
    [InlineData("Deluxe Dragster", "Artifact — Vehicle",
        "This Vehicle can't be blocked except by Vehicles.\nWhenever this Vehicle deals combat damage to a "
        + "player, you may cast target instant or sorcery card from that player's graveyard without paying its "
        + "mana cost. If that spell would be put into a graveyard, exile it instead.\nCrew 2")]
    [InlineData("Tinybones, Bauble Burglar", "Legendary Creature — Skeleton Rogue",
        "Whenever an opponent discards a card, exile it from their graveyard with a stash counter on "
        + "it.\nDuring your turn, you may play cards you don't own with stash counters on them from exile, and "
        + "mana of any type can be spent to cast those spells.\n{3}{B}, {T}: Each opponent discards a card. "
        + "Activate only as a sorcery.")]
    public void Classify_TheOctober16RegrowthPass_TheirGraveyardCast_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // Readings of 2026-10-16: a target card in your graveyard you may then cast, a
    // card in your graveyard put into your hand two sentences later, the top card
    // of your graveyard put on top of your library, and flashback granted to the
    // instants and sorceries in graveyards.
    [InlineData("Conduit of Worlds", "Artifact",
        "You may play lands from your graveyard.\n{T}: Choose target nonland permanent card in your graveyard. "
        + "If you haven't cast a spell this turn, you may cast that card. If you do, you can't cast additional "
        + "spells this turn. Activate only as a sorcery.")]
    [InlineData("Cosmic Rebirth", "Instant",
        "Choose target permanent card in your graveyard. If it has mana value 3 or less, you may put it onto "
        + "the battlefield. If you don't put it onto the battlefield, put it into your hand.\nYou gain 3 life.")]
    [InlineData("Guiding Spirit", "Creature — Angel Spirit",
        "Flying\n{T}: If the top card of target player's graveyard is a creature card, put that card on top of "
        + "that player's library.")]
    [InlineData("Otaria", "Plane — Dominaria",
        "Instant and sorcery cards in graveyards have flashback. The flashback cost is equal to the card's "
        + "mana cost. (Its owner may cast the card from their graveyard for its mana cost. Then they exile "
        + "it.)\nWhenever chaos ensues, take an extra turn after this one.")]
    public void Classify_TheOctober16RegrowthPass_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // Read 2026-11-06, each Regrowth by hand: a card chosen in the graveyard and
    // returned by a d20 table row (Aberrant Mind Sorcerer), an any-graveyard exile
    // whose copy comes in a sentence of its own, outside the line that exiles it
    // (Mnemonic Deluge, Wizard's Spellbook), and a graveyard card granted the
    // permission to be played in quotes (Kethis, the Hidden Hand; the flashback
    // Otaria grants).
    [InlineData("Aberrant Mind Sorcerer", "Creature — Human Elf Shaman Sorcerer",
        "Psionic Spells — When this creature enters, choose target instant or sorcery card in your graveyard, "
        + "then roll a d20.\n1—9 | You may put that card on top of your library.\n10—20 | Return that card to "
        + "your hand.")]
    [InlineData("Wizard's Spellbook", "Artifact — Book",
        "{T}: Exile target instant or sorcery card from a graveyard. Roll a d20. Activate only as a "
        + "sorcery.\n1—9 | Copy that card. You may cast the copy.\n10—19 | Copy that card. You may cast the copy "
        + "by paying {1} rather than paying its mana cost.\n20 | Copy each card exiled with this artifact. You "
        + "may cast any number of the copies without paying their mana costs.")]
    [InlineData("Mnemonic Deluge", "Sorcery",
        "Exile target instant or sorcery card from a graveyard. Copy that card three times. You may cast the "
        + "copies without paying their mana costs. Exile Mnemonic Deluge.")]
    [InlineData("Kethis, the Hidden Hand", "Legendary Creature — Elf Advisor",
        "Legendary spells you cast cost {1} less to cast.\nExile two legendary cards from your graveyard: "
        + "Until end of turn, each legendary card in your graveyard gains \"You may play this card from your "
        + "graveyard.\"")]
    public void Classify_TheNovember6Readings_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // Read 2026-11-06, both untagged for Regrowth by hand: an exile paid as a COST
    // in front of a colon, with the Adventure's reminder 'You may cast the
    // artifact later from exile' standing for the cast (Altar of Bhaal // Bone
    // Offering returns a creature to the battlefield, which is Reanimate), and a
    // draw that only WATCHES a cast from a graveyard happen with 'if you have
    // cast' (Laboratory Drudge, the shape that keeps 'whenever you cast' out).
    [InlineData("Altar of Bhaal // Bone Offering", "Artifact // Sorcery — Adventure",
        "{2}{B}, {T}, Exile a creature you control: Return target creature card from your graveyard to the "
        + "battlefield. Activate only as a sorcery.\nCreate a tapped 4/1 black Skeleton creature token with "
        + "menace. (Then exile this card. You may cast the artifact later from exile.)")]
    [InlineData("Laboratory Drudge", "Creature — Zombie Horror",
        "At the beginning of each end step, draw a card if you've cast a spell from a graveyard or activated "
        + "an ability of a card in a graveyard this turn.")]
    public void Classify_TheNovember6Readings_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // RULED 2026-11-06 (Tribale non è Regrowth): a return to HAND limited to a
    // creature type is not Regrowth; a tribe, a list of tribes, and Soulshift's
    // reminder all read the same. A tribal return to the battlefield stays
    // Reanimate.
    [InlineData("Ashcoat of the Shadow Swarm", "Legendary Creature — Rat Warlock",
        "Whenever Ashcoat attacks or blocks, other Rats you control get +X/+X until end of turn, where X is "
        + "the number of Rats you control.\nAt the beginning of your end step, you may mill four cards. If you "
        + "do, return up to two Rat creature cards from your graveyard to your hand. (To mill a card, put the "
        + "top card of your library into your graveyard.)")]
    [InlineData("Elderfang Ritualist", "Creature — Elf Cleric",
        "When this creature dies, return another target Elf card from your graveyard to your hand.")]
    [InlineData("Gallifrey Stands", "Legendary Enchantment",
        "When Gallifrey Stands enters, return all Doctor cards from your graveyard to your hand.\nAt the "
        + "beginning of your upkeep, you may put a Doctor creature card from your hand onto the battlefield. "
        + "Then if you control thirteen or more Doctors, you win the game.")]
    [InlineData("Mudflat Village", "Land",
        "{T}: Add {C}.\n{T}: Add {B}. Spend this mana only to cast a creature spell.\n{1}{B}, {T}, Sacrifice "
        + "this land: Return target Bat, Lizard, Rat, or Squirrel card from your graveyard to your hand.")]
    [InlineData("Deathknell Kami", "Creature — Spirit",
        "Flying\n{2}: This creature gets +1/+1 until end of turn. Sacrifice it at the beginning of the next "
        + "end step.\nSoulshift 1 (When this creature dies, you may return target Spirit card with mana value 1 "
        + "or less from your graveyard to your hand.)")]
    public void Classify_TheNovember6Ruling_ATribalReturnToHand_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // RULED 2026-11-08 (tribale mai Regrowth): a tribal card CAST out of the
    // graveyard, or exiled from there and copied, is not Regrowth either, as the
    // return to hand of 11-06. Named: Zul Ashur, Rivaz of the Claw, Zask, Jacob
    // Frye.
    [InlineData("Zul Ashur, Lich Lord", "Legendary Creature — Zombie Warlock",
        "Ward—Pay 2 life. (Whenever this creature becomes the target of a spell or ability an opponent "
        + "controls, counter it unless that player pays 2 life.)\n{T}: You may cast target Zombie creature card "
        + "from your graveyard this turn.")]
    [InlineData("Rivaz of the Claw", "Legendary Creature — Lizard Warlock",
        "Menace\n{T}: Add two mana in any combination of colors. Spend this mana only to cast Dragon creature "
        + "spells.\nOnce during each of your turns, you may cast a Dragon creature spell from your "
        + "graveyard.\nWhenever you cast a Dragon creature spell from your graveyard, it gains \"When this "
        + "creature dies, exile it.\"")]
    [InlineData("Zask, Skittering Swarmlord", "Legendary Creature — Insect",
        "You may play lands and cast Insect spells from your graveyard.\nWhenever another Insect you control "
        + "dies, put it on the bottom of its owner's library, then mill two cards. (Put the top two cards of "
        + "your library into your graveyard.)\n{1}{B/G}: Target Insect gets +1/+0 and gains deathtouch until end "
        + "of turn. ({B/G} can be paid with either {B} or {G}.)")]
    [InlineData("Jacob Frye", "Legendary Creature — Human Assassin",
        "Partner with Evie Frye (When this creature enters, target player may put Evie into their hand from "
        + "their library, then shuffle.)\nWhenever one or more Assassins you control deal combat damage to a "
        + "player, exile up to one target Assassin card or card with freerunning from your graveyard. If you "
        + "do, copy it. You may cast the copy.")]
    public void Classify_TheNovember8Ruling_ATribalCastFromTheGraveyard_IsNotRegrowth(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // ...but a tribe JOINED to an artifact or a legend is open to more than the
    // tribe and keeps the tag (Arcade Gannon's artifact or Human spell, Nashi's
    // legendary or Rat card), and Shiko's copy of a nonland card cast from the
    // graveyard is Regrowth as well as Reanimate since 2026-11-08 (+Regrowth, come
    // Counterpoint): the named exception of 10-13 is overturned.
    [InlineData("Arcade Gannon", "Legendary Creature — Human Doctor",
        "{T}: Draw a card, then discard a card. Put a quest counter on Arcade Gannon.\nFor Auld Lang Syne — "
        + "Once during each of your turns, you may cast an artifact or Human spell from your graveyard with "
        + "mana value less than or equal to the number of quest counters on Arcade Gannon.")]
    [InlineData("Nashi, Moon's Legacy", "Legendary Creature — Rat Shaman",
        "Menace, ward {1}\nWhenever Nashi attacks, exile up to one target legendary or Rat card from your "
        + "graveyard and copy it. You may cast the copy. (You still pay its costs. A copy of a permanent spell "
        + "becomes a token.)")]
    [InlineData("Shiko, Paragon of the Way", "Legendary Creature — Spirit Dragon",
        "Flying, vigilance\nWhen Shiko enters, exile target nonland card with mana value 3 or less from your "
        + "graveyard. Copy it, then you may cast the copy without paying its mana cost. (A copy of a permanent "
        + "spell becomes a token.)")]
    public void Classify_TheNovember8Ruling_ACastOpenToMoreThanTheTribe_IsRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // A card type, a supertype or the like is no tribe (Aura, Equipment, Vehicle,
    // Saga), and a card with another Regrowth on it keeps the tag when its tribal
    // return is blanked (Kami of Restless Shadows puts a creature card on top of
    // the library in its other mode).
    [InlineData("Satsuki, the Living Lore", "Legendary Creature — Human Druid",
        "{T}: Put a lore counter on each Saga you control. Activate only as a sorcery.\nWhen Satsuki dies, "
        + "choose up to one —\n• Return target Saga or enchantment creature you control to its owner's hand.\n• "
        + "Return target Saga card from your graveyard to your hand.")]
    [InlineData("Dogmeat, Ever Loyal", "Legendary Creature — Dog",
        "When Dogmeat enters, mill five cards, then return an Aura or Equipment card from your graveyard to "
        + "your hand.\nWhenever a creature you control that's enchanted or equipped attacks, create a Junk "
        + "token. (It's an artifact with \"{T}, Sacrifice this token: Exile the top card of your library. You "
        + "may play that card this turn. Activate only as a sorcery.\")")]
    [InlineData("Imperial Recovery Unit", "Artifact — Vehicle",
        "Whenever this Vehicle attacks, return target creature or Vehicle card with mana value 2 or less from "
        + "your graveyard to your hand.\nCrew 2 (Tap any number of creatures you control with total power 2 or "
        + "more: This Vehicle becomes an artifact creature until end of turn.)")]
    [InlineData("Kami of Restless Shadows", "Creature — Spirit",
        "When this creature enters, choose one —\n• Return up to one target Ninja or Rogue creature card from "
        + "your graveyard to your hand.\n• Put target creature card from your graveyard on top of your library.")]
    // An Arcane card is a spell type, no tribe (The Unspeakable, unreviewed);
    // 'creature card' beside a tribe is a plain type (Rise from the Wreck,
    // Regrowth by hand).
    [InlineData("The Unspeakable", "Legendary Creature — Spirit",
        "Flying, trample\nWhenever The Unspeakable deals combat damage to a player, you may return target "
        + "Arcane card from your graveyard to your hand.")]
    [InlineData("Rise from the Wreck", "Sorcery",
        "Return up to one target creature card, up to one target Mount card, up to one target Vehicle card, "
        + "and up to one target creature card with no abilities from your graveyard to your hand.")]
    public void Classify_TheNovember6Ruling_ANonTribalReturnToHand_IsStillRegrowth(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Regrowth));
    }
}
