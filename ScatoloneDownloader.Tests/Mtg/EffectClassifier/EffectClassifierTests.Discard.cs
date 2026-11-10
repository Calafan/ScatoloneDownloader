using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Taking a card out of their revealed hand into exile is Discard (2026-09-25),
    // Intimidation Tactics by name the same day.
    [InlineData("Aggressive Negotiations", "Sorcery",
        "Target opponent reveals their hand. You choose a nonland card from it and exile that card. Put a "
        + "+1/+1 counter on up to one target creature you control.")]
    [InlineData("Intimidation Tactics", "Sorcery",
        "Target opponent reveals their hand. You choose an artifact or creature card from it. Exile that "
        + "card.\nCycling {3} ({3}, Discard this card: Draw a card.)")]
    public void Classify_ExilingFromTheirRevealedHand_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Five wordings read 2026-09-28, each tagged on every reviewed card using
    // it: the punisher's "unless that player … discards a card", the chosen card
    // discarded by a pronoun ("they discard it", "the player discards that
    // card"), "they discard a card" after a player is named, a list ("sacrifices
    // a creature, discards a card"), and the name search through a hand. Tinybones
    // pins that a card watching a discard AND causing one keeps the tag.
    [InlineData("Perforating Artist", "Creature — Devil",
        "Deathtouch (Any amount of damage this deals to a creature is enough to destroy it.)\nRaid — At the "
        + "beginning of your end step, if you attacked this turn, each opponent loses 3 life unless that player "
        + "sacrifices a nonland permanent of their choice or discards a card.")]
    [InlineData("Binding Negotiation", "Sorcery",
        "Target opponent reveals their hand. You may choose a nonland card from it. If you do, they discard "
        + "it. Otherwise, you may put a face-up exiled card they own into their graveyard.")]
    [InlineData("Leshrac's Sigil", "Enchantment",
        "Whenever an opponent casts a green spell, you may pay {B}{B}. If you do, look at that player's hand "
        + "and choose a card from it. The player discards that card.\n{B}{B}: Return this enchantment to its "
        + "owner's hand.")]
    [InlineData("Sonic Shrieker", "Creature — Dragon",
        "Flying\nWhen this creature enters, it deals 2 damage to any target and you gain 2 life. If a player "
        + "is dealt damage this way, they discard a card.")]
    [InlineData("Scarring Memories", "Sorcery — Lesson",
        "You may cast this spell as though it had flash if you control an attacking legendary "
        + "creature.\nTarget opponent sacrifices a creature of their choice, discards a card, and loses 3 life.")]
    [InlineData("Lobotomy", "Sorcery",
        "Target player reveals their hand, then you choose a card other than a basic land card from it. "
        + "Search that player's graveyard, hand, and library for all cards with the same name as the chosen "
        + "card and exile them. Then that player shuffles.")]
    [InlineData("Tinybones, Bauble Burglar", "Legendary Creature — Skeleton Rogue",
        "Whenever an opponent discards a card, exile it from their graveyard with a stash counter on "
        + "it.\nDuring your turn, you may play cards you don't own with stash counters on them from exile, and "
        + "mana of any type can be spent to cast those spells.\n{3}{B}, {T}: Each opponent discards a card. "
        + "Activate only as a sorcery.")]
    public void Classify_AnAttackOnAHandInAnyWording_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Four shapes that name a player beside "discard" and attack nobody's hand,
    // each untagged on every reviewed card: the madness-style punishment, a loot
    // handed to a player (Filter), a condition that watches a discard, and an
    // alternative cost.
    [InlineData("Guerrilla Tactics", "Instant",
        "Guerrilla Tactics deals 2 damage to any target.\nWhen a spell or ability an opponent controls causes "
        + "you to discard this card, it deals 4 damage to any target.")]
    [InlineData("Depth Defiler", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nKicker {C} (You may pay an additional {C} as you cast this "
        + "spell.)\nWhen you cast this spell, choose one. If it was kicked, choose both instead.\n• Return target "
        + "creature to its owner's hand.\n• Target player draws two cards, then discards a card.")]
    [InlineData("Cait, Cage Brawler", "Legendary Creature — Human Warrior",
        "During your turn, Cait has indestructible.\nWhenever Cait attacks, you and defending player each draw "
        + "a card, then discard a card. Put two +1/+1 counters on Cait if you discarded the card with the "
        + "greatest mana value among those cards or tied for greatest.")]
    [InlineData("Lo and Li, Royal Advisors", "Legendary Creature — Human Advisor",
        "Whenever an opponent discards a card or mills one or more cards, put a +1/+1 counter on each Advisor "
        + "you control.\n{2}{U/B}: Target player mills four cards. (They put the top four cards of their library "
        + "into their graveyard.)")]
    [InlineData("Dream Halls", "Enchantment",
        "Rather than pay the mana cost for a spell, its controller may discard a card that shares a color "
        + "with that spell.")]
    public void Classify_DiscardWordsThatAttackNoHand_AreNotDiscard(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Ruled 2026-09-28, each settling a split: a card EXILED from their hand
    // (Unscrupulous Agent, the same sentence as Ruthless Negotiation;
    // Crabomination's card at random), a card chosen from their hand and put on
    // their LIBRARY (Painful Memories, as Agonizing Memories), the whole hand
    // looked at and EXILED (Apple of Eden, "come Wheel of Fortune e sorelle"), a
    // symmetric REPLACEMENT of the draw (Chains of Mephistopheles), and Nicol
    // Bolas's "that player discards their hand", whose missing tag was a slip.
    [InlineData("Unscrupulous Agent", "Creature — Elf Detective",
        "When this creature enters, target opponent exiles a card from their hand.")]
    [InlineData("Crabomination", "Creature — Crab Demon",
        "Emerge from artifact {5}{B}{B} (You may cast this spell by sacrificing an artifact and paying the "
        + "emerge cost reduced by that artifact's mana value.)\nWhen this creature enters, target opponent "
        + "exiles the top card of their library, a card at random from their graveyard, and a card at random "
        + "from their hand. You may cast a spell from among cards exiled this way without paying its mana cost.")]
    [InlineData("Painful Memories", "Sorcery",
        "Look at target opponent's hand and choose a card from it. Put that card on top of that player's "
        + "library.")]
    [InlineData("Apple of Eden, Isu Relic", "Legendary Artifact",
        "{T}, Pay 4 life, Sacrifice Apple of Eden: Look at target opponent's hand and exile those cards face "
        + "down. You may play those cards this turn, and mana of any type can be spent to cast them. Until end "
        + "of turn, whenever you play a land or cast a spell this way, its owner draws a card. At the beginning "
        + "of the next end step, return the exiled cards to their owner's hand. Activate only as a sorcery.")]
    [InlineData("Chains of Mephistopheles", "Enchantment",
        "If a player would draw a card except the first one they draw in each of their draw steps, that "
        + "player discards a card instead. If the player discards a card this way, they draw a card. If the "
        + "player doesn't discard a card this way, they mill a card.")]
    [InlineData("Nicol Bolas", "Legendary Creature — Elder Dragon",
        "Flying\nAt the beginning of your upkeep, sacrifice Nicol Bolas unless you pay {U}{B}{R}.\nWhenever "
        + "Nicol Bolas deals damage to an opponent, that player discards their hand.")]
    // Confirmed 2026-09-29: a punisher that offers the OPPONENTS the choice
    // outright, "each opponent may sacrifice … or discard a card".
    [InlineData("Osseous Sticktwister", "Artifact Creature — Scarecrow",
        "Lifelink\nDelirium — At the beginning of your end step, if there are four or more card types among "
        + "cards in your graveyard, each opponent may sacrifice a nonland permanent of their choice or discard "
        + "a card. Then this creature deals damage equal to its power to each opponent who didn't sacrifice a "
        + "permanent or discard a card this way.")]
    // The hand looked at and a card exiled from it in a second sentence: Deep-
    // Cavern Bat, tagged by the human on 2026-09-29.
    [InlineData("Deep-Cavern Bat", "Creature — Bat",
        "Flying, lifelink\nWhen this creature enters, look at target opponent's hand. You may exile a nonland "
        + "card from it until this creature leaves the battlefield.")]
    // Read 2026-11-10 (the second sitting): the exile from their hand written as
    // YOURS, 'exile all cards from that player's hand' (Elder Brain, Thought
    // Distortion; 3 of 3 reviewed with Isildur's Fateful Strike).
    [InlineData("Elder Brain", "Creature — Horror",
        "Menace\nWhenever this creature attacks a player, exile all cards from that player's hand, then they "
        + "draw that many cards. You may play lands and cast spells from among the exiled cards for as long as "
        + "they remain exiled. If you cast a spell this way, you may spend mana as though it were mana of any "
        + "color to cast it.")]
    [InlineData("Thought Distortion", "Sorcery",
        "This spell can't be countered.\nTarget opponent reveals their hand. Exile all noncreature, nonland "
        + "cards from that player's hand and graveyard.")]
    public void Classify_AHandAttackRuledIn_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Ruled 2026-09-28: "each player MAY discard" is a choice everybody makes,
    // you included, and attacks nobody's hand (Mind Bomb; The Death of Gwen
    // Stacy, whose hand tag was corrected); a card exiled that they may still
    // PLAY is not lost (Lightstall Inquisitor, Elkin Lair); and a discard of
    // YOUR OWN is a cost — the four hand tags that said otherwise were ruled
    // errors (Professor Zei, Sabin, Stormbind, Summon: G.F. Ifrit).
    [InlineData("Mind Bomb", "Sorcery",
        "Each player may discard up to three cards. Mind Bomb deals damage to each player equal to 3 minus "
        + "the number of cards they discarded this way.")]
    [InlineData("The Death of Gwen Stacy", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Destroy "
        + "target creature.\nII — Each player may discard a card. Each player who doesn't loses 3 life.\nIII — "
        + "Exile any number of target players' graveyards.")]
    [InlineData("Lightstall Inquisitor", "Creature — Angel Wizard",
        "Vigilance\nWhen this creature enters, each opponent exiles a card from their hand and may play that "
        + "card for as long as it remains exiled. Each spell cast this way costs {1} more to cast. Each land "
        + "played this way enters tapped.")]
    [InlineData("Elkin Lair", "World Enchantment",
        "At the beginning of each player's upkeep, that player exiles a card at random from their hand. The "
        + "player may play that card this turn. At the beginning of the next end step, if the player hasn't "
        + "played the card, they put it into their graveyard.")]
    [InlineData("Professor Zei, Anthropologist", "Legendary Creature — Human Advisor Ally",
        "{T}, Discard a card: Draw a card.\n{1}, {T}, Sacrifice Professor Zei: Return target instant or "
        + "sorcery card from your graveyard to your hand. Activate only during your turn.")]
    [InlineData("Sabin, Master Monk", "Legendary Creature — Human Noble Monk",
        "Double strike\nBlitz—{2}{R}{R}, Discard a card. (If you cast this spell for its blitz cost, it gains "
        + "haste and \"When this creature dies, draw a card.\" Sacrifice it at the beginning of the next end "
        + "step.)\nYou may cast this card from your graveyard using its blitz ability.")]
    [InlineData("Stormbind", "Enchantment",
        "{2}, Discard a card at random: This enchantment deals 2 damage to any target.")]
    [InlineData("Summon: G.F. Ifrit", "Enchantment Creature — Saga Demon",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI, II — You "
        + "may discard a card. If you do, draw a card.\nIII, IV — Add {R}.")]
    // Confirmed 2026-09-29: a SYMMETRIC punisher follows "each player may
    // discard" — everybody chooses, you included.
    [InlineData("Possessed Portal", "Artifact",
        "If a player would draw a card, that player skips that draw instead.\nAt the beginning of each end "
        + "step, each player sacrifices a permanent of their choice unless they discard a card.")]
    [InlineData("Doom Foretold", "Enchantment",
        "At the beginning of each player's upkeep, that player sacrifices a nonland, nontoken permanent of "
        + "their choice. If that player can't, they discard a card, they lose 2 life, you draw a card, you gain "
        + "2 life, you create a 2/2 white Knight creature token with vigilance, then you sacrifice this "
        + "enchantment.")]
    // Elite Spellbinder sat here until 2026-10-15, when the card YOU choose
    // from a hand you looked at was ruled Discard even though its owner may
    // still play it (Invasion of Gobakhan, in
    // Classify_TheOctoberSmallGaps_CarryTheTag).
    // Read 2026-11-10 (the second sitting): a name search that starts from a card
    // in a GRAVEYARD (Kotose, untagged; Deadly Cover-Up the same).
    [InlineData("Kotose, the Silent Spider", "Legendary Creature — Human Ninja",
        "When Kotose enters, exile target card other than a basic land card from an opponent's graveyard. "
        + "Search that player's graveyard, hand, and library for any number of cards with the same name as that "
        + "card and exile them. Then that player shuffles. For as long as you control Kotose, you may play one "
        + "of the exiled cards, and you may spend mana as though it were mana of any color to cast it.")]
    public void Classify_AHandAttackRuledOut_IsNotDiscard(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
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
    // Ruled 2026-11-10 with the definition (Hymn to Tourach): a hand shuffled into
    // its library when the card makes everybody, or a player it names, do it
    // ('anche Timetwister o Wheel of Fortune lo sono perché scegli tu quando
    // farlo'; Winds of Change reached, Sway of the Stars past the graveyard and
    // the permanents, Jace's -12 on 'that player'), and a card of their hand put
    // back on their library: Jester's Mask by name, where you then pick their new
    // hand, Chittering Rats and Sadistic Augermage, where they pick the card and
    // lose the draw ('perde una pescata').
    [InlineData("Timetwister", "Sorcery",
        "Each player shuffles their hand and graveyard into their library, then draws seven cards. (Then put "
        + "Timetwister into its owner's graveyard.)")]
    [InlineData("Winds of Change", "Sorcery",
        "Each player shuffles the cards from their hand into their library, then draws that many cards.")]
    [InlineData("Sway of the Stars", "Sorcery",
        "Each player shuffles their hand, graveyard, and all permanents they own into their library, then "
        + "draws seven cards. Each player's life total becomes 7.")]
    [InlineData("Jace, the Mind Sculptor", "Legendary Planeswalker — Jace",
        "+2: Look at the top card of target player's library. You may put that card on the bottom of that "
        + "player's library.\n0: Draw three cards, then put two cards from your hand on top of your library in "
        + "any order.\n−1: Return target creature to its owner's hand.\n−12: Exile all cards from target player's "
        + "library, then that player shuffles their hand into their library.")]
    [InlineData("Jester's Mask", "Artifact",
        "This artifact enters tapped.\n{1}, {T}, Sacrifice this artifact: Target opponent puts the cards from "
        + "their hand on top of their library. Search that player's library for that many cards. That player "
        + "puts those cards into their hand, then shuffles.")]
    [InlineData("Chittering Rats", "Creature — Rat",
        "When this creature enters, target opponent puts a card from their hand on top of their library.")]
    [InlineData("Sadistic Augermage", "Creature — Human Wizard",
        "When this creature dies, each player puts a card from their hand on top of their library.")]
    public void Classify_TheNovember10Definition_AHandShuffledOrPutBack_IsDiscard(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Ruled 2026-11-10, the other side of the same definition: Teferi's Puzzle Box
    // resets every hand every draw step by itself, nobody chooses when, and
    // Widespread Panic waits on THEIR shuffle; Step Between Worlds lets each
    // player choose and Weftwalking shuffles your own hand; 'discards ANY NUMBER
    // of cards, then draws that many' is the same choice as 'each player may
    // discard', a loot given away (Flux -Discard by name, Steal the Show and
    // Fervent Mastery the same words).
    [InlineData("Teferi's Puzzle Box", "Artifact",
        "At the beginning of each player's draw step, that player puts the cards in their hand on the bottom "
        + "of their library in any order, then draws that many cards.")]
    [InlineData("Widespread Panic", "Enchantment",
        "Whenever a spell or ability causes its controller to shuffle their library, that player puts a card "
        + "from their hand on top of their library.")]
    [InlineData("Step Between Worlds", "Sorcery",
        "Each player may shuffle their hand and graveyard into their library. Each player who does draws "
        + "seven cards. Exile Step Between Worlds.\nPlot {4}{U}{U} (You may pay {4}{U}{U} and exile this card "
        + "from your hand. Cast it as a sorcery on a later turn without paying its mana cost. Plot only as a "
        + "sorcery.)")]
    [InlineData("Weftwalking", "Enchantment",
        "When this enchantment enters, if you cast it, shuffle your hand and graveyard into your library, "
        + "then draw seven cards.\nThe first spell each player casts during each of their turns may be cast "
        + "without paying its mana cost.")]
    [InlineData("Flux", "Sorcery",
        "Each player discards any number of cards, then draws that many cards.\nDraw a card.")]
    [InlineData("Steal the Show", "Sorcery",
        "Choose one or both —\n• Target player discards any number of cards, then draws that many cards.\n• "
        + "Steal the Show deals damage equal to the number of instant and sorcery cards in your graveyard to "
        + "target creature or planeswalker.")]
    [InlineData("Fervent Mastery", "Sorcery",
        "You may pay {2}{R}{R} rather than pay this spell's mana cost.\nIf the {2}{R}{R} cost was paid, an "
        + "opponent discards any number of cards, then draws that many cards.\nSearch your library for up to "
        + "three cards, put them into your hand, shuffle, then discard three cards at random.")]
    public void Classify_TheNovember10Definition_NotChosenWhenOrTheirOwnChoice_IsNotDiscard(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Discard));
    }
}
