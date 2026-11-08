using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // Tower Winder left this theory on 2026-11-08: Command Tower is a LAND, and
    // a land is never Tutor (Q1, untagged by name); the classifier cannot read
    // that from the name and still proposes it, a disagreement recorded in
    // Tutor.md.
    [InlineData("Llanowar Sentinel", "Creature — Elf",
        "When this creature enters, you may pay {1}{G}. If you do, search your library for a card "
        + "named Llanowar Sentinel, put that card onto the battlefield, then shuffle.")]
    [InlineData("Dragonstorm Forecaster", "Creature — Human Scout",
        "{2}, {T}: Search your library for a card named Dragonstorm Globe or Boulderborn Dragon, reveal it, "
        + "put it into your hand, then shuffle.")]
    // Demonic Consultation never searches — it names a card and digs for the NAME.
    [InlineData("Demonic Consultation", "Instant",
        "Choose a card name. Exile the top six cards of your library, then reveal cards from the top "
        + "of your library until you reveal a card with the chosen name. Put that card into your hand "
        + "and exile all other cards revealed this way.")]
    // The zones listed with commas, "your library, graveyard, and/or outside the
    // game": Invasion of Arcavios, tagged Tutor by the human on 2026-09-29.
    [InlineData("Invasion of Arcavios // Invocation of the Founders", "Battle — Siege // Enchantment",
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's "
        + "defeated, exile it, then cast it transformed.)\nWhen this Siege enters, search your library, "
        + "graveyard, and/or outside the game for an instant or sorcery card you own, reveal it, and put it "
        + "into your hand. If you search your library this way, shuffle.\nWhenever you cast an instant or "
        + "sorcery spell from your hand, you may copy that spell. You may choose new targets for the copy.")]
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
    // A search for a creature or land card is still a creature found.
    [InlineData("Analyze the Pollen", "Sorcery",
        "As an additional cost to cast this spell, you may collect evidence 8. (Exile cards with total mana "
        + "value 8 or greater from your graveyard.)\nSearch your library for a basic land card. If evidence was "
        + "collected, instead search your library for a creature or land card. Reveal that card, put it into "
        + "your hand, then shuffle.")]
    public void Classify_TheOctober16Pass_ASearchForACreatureOrLand_IsTutor(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }

    [Theory]
    // Ruled 2026-11-05 (learn is not Tutor): the Lesson card of learn's reminder
    // text is revealed from outside the game, or a card is rummaged; it searches
    // no library. Gnarled Professor was untagged by hand, Igneous Inspiration is
    // handed back without the tag.
    [InlineData("Gnarled Professor", "Creature — Treefolk Druid",
        "Trample\nWhen this creature enters, learn. (You may reveal a Lesson card you own from outside the "
        + "game and put it into your hand, or discard a card to draw a card.)")]
    [InlineData("Igneous Inspiration", "Sorcery",
        "Igneous Inspiration deals 3 damage to any target.\nLearn. (You may reveal a Lesson card you own from "
        + "outside the game and put it into your hand, or discard a card to draw a card.)")]
    [InlineData("Academic Dispute", "Instant",
        "Target creature blocks this turn if able. You may have it gain reach until end of turn.\nLearn. (You "
        + "may reveal a Lesson card you own from outside the game and put it into your hand, or discard a card "
        + "to draw a card.)")]
    [InlineData("Cram Session", "Sorcery",
        "You gain 4 life.\nLearn. (You may reveal a Lesson card you own from outside the game and put it into "
        + "your hand, or discard a card to draw a card.)")]
    [InlineData("Field Trip", "Sorcery",
        "Search your library for a basic Forest card, put that card onto the battlefield tapped, then "
        + "shuffle.\nLearn. (You may reveal a Lesson card you own from outside the game and put it into your "
        + "hand, or discard a card to draw a card.)")]
    public void Classify_TheNovember5Ruling_LearnIsNotTutor(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }

    [Theory]
    // The veto is the word Lesson: the other cards that fetch from outside the
    // game never name one and stay Tutor (Tutor.md, the sideboard is a tutor).
    [InlineData("Ring of Ma'rûf", "Artifact",
        "{5}, {T}, Exile this artifact: The next time you would draw a card this turn, instead put a card you "
        + "own from outside the game into your hand.")]
    [InlineData("Burning Wish", "Sorcery",
        "You may reveal a sorcery card you own from outside the game and put it into your hand. Exile Burning "
        + "Wish.")]
    [InlineData("Living Wish", "Sorcery",
        "You may reveal a creature or land card you own from outside the game and put it into your hand. "
        + "Exile Living Wish.")]
    [InlineData("Vivien, Arkbow Ranger", "Legendary Planeswalker — Vivien",
        "+1: Distribute two +1/+1 counters among up to two target creatures. They gain trample until end of "
        + "turn.\n−3: Target creature you control deals damage equal to its power to target creature or "
        + "planeswalker.\n−5: You may reveal a creature card you own from outside the game and put it into your "
        + "hand.")]
    public void Classify_TheNovember5Ruling_TheSideboardWishesStayTutor(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }

    [Theory]
    // Redefined 2026-11-08: the player who searches chooses a card from their own
    // library. 'Searches their library' is that search said of another player
    // (Scheming Symmetry, Varragoth, tagged by hand); Auditore Ambush names the
    // card, reached and handed back with the tag.
    [InlineData("Scheming Symmetry", "Sorcery",
        "Choose two target players. Each of them searches their library for a card, then shuffles and puts "
        + "that card on top.")]
    [InlineData("Varragoth, Bloodsky Sire", "Legendary Creature — Demon Rogue",
        "Deathtouch\nBoast — {1}{B}: Target player searches their library for a card, then shuffles and puts "
        + "that card on top. (Activate only if this creature attacked this turn and only once each turn.)")]
    [InlineData("Auditore Ambush", "Sorcery",
        "Choose one or both —\n• Return target creature to its owner's hand.\n• Target player searches their "
        + "library and/or graveyard for a card named Ezio, Blade of Vengeance, reveals it, and puts it into "
        + "their hand. If they search their library this way, they shuffle.")]
    public void Classify_TheNovember8Redefinition_AnotherPlayersOwnSearch_IsTutor(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }

    [Theory]
    // Ruled 2026-11-08 (terra mai Tutor): a search for ANY land is Ramp or
    // ManaFixing. 'A land card' (Elvish Reclaimer, Golos, untagged by name) and a
    // nonbasic subtype, 'a Desert card' or 'a Cave card' (Colossal Rattlewurm,
    // Sand Scout, Cosmium Confluence, handed back without the tag).
    [InlineData("Elvish Reclaimer", "Creature — Elf Warrior",
        "This creature gets +2/+2 as long as there are three or more land cards in your graveyard.\n{2}, {T}, "
        + "Sacrifice a land: Search your library for a land card, put it onto the battlefield tapped, then "
        + "shuffle.")]
    [InlineData("Golos, Tireless Pilgrim", "Legendary Artifact Creature — Scout",
        "When Golos enters, you may search your library for a land card, put that card onto the battlefield "
        + "tapped, then shuffle.\n{2}{W}{U}{B}{R}{G}: Exile the top three cards of your library. You may play "
        + "them this turn without paying their mana costs.")]
    [InlineData("Colossal Rattlewurm", "Creature — Wurm",
        "Colossal Rattlewurm has flash as long as you control a Desert.\nTrample\n{1}{G}, Exile this card from "
        + "your graveyard: Search your library for a Desert card, put it onto the battlefield tapped, then "
        + "shuffle.")]
    [InlineData("Sand Scout", "Creature — Human Scout",
        "When this creature enters, if an opponent controls more lands than you, search your library for a "
        + "Desert card, put it onto the battlefield tapped, then shuffle.\nWhenever one or more land cards are "
        + "put into your graveyard from anywhere, create a 1/1 red, green, and white Sand Warrior creature "
        + "token. This ability triggers only once each turn.")]
    [InlineData("Cosmium Confluence", "Sorcery",
        "Choose three. You may choose the same mode more than once.\n• Search your library for a Cave card, "
        + "put it onto the battlefield tapped, then shuffle.\n• Put three +1/+1 counters on a Cave you control. "
        + "It becomes a 0/0 Elemental creature with haste. It's still a land.\n• Destroy target enchantment.")]
    // 'A Gate card' (Maze's End, unreviewed) is the third subtype in the store.
    [InlineData("Maze's End", "Land",
        "This land enters tapped.\n{T}: Add {C}.\n{3}, {T}, Return this land to its owner's hand: Search your "
        + "library for a Gate card, put it onto the battlefield, then shuffle. If you control ten or more Gates "
        + "with different names, you win the game.")]
    public void Classify_TheNovember8Redefinition_ALandOfAnyKind_IsNotTutor(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Tutor));
    }
}
