using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // A Clue and an impulse are asked the same question: one card, once? Made
    // REPEATEDLY, either is a card; and an impulse that takes more than one card
    // off the top is a draw two whatever else it does. Ruled 2026-09-15.
    [InlineData("Boros Strike-Captain", "Creature — Human Soldier",
        "Battalion — Whenever this creature and at least two other creatures attack, exile the top card of your library. You may play that card this turn.")]
    [InlineData("Charred Foyer", "Land",
        "At the beginning of your upkeep, exile the top card of your library. You may play it this turn.")]
    [InlineData("Zenith Festival", "Sorcery",
        "Exile the top X cards of your library. You may play them until the end of your next turn.")]
    [InlineData("Interdimensional Web Watch", "Artifact",
        "When this artifact enters, exile the top two cards of your library. Until the end of your next turn, you may play those cards.")]
    [InlineData("Morska, Undersea Sleuth", "Legendary Creature — Fish Detective",
        "At the beginning of your upkeep, investigate. (Create a Clue token.)")]
    [InlineData("June, Bounty Hunter", "Legendary Creature — Human Scout",
        "{1}, Sacrifice another creature: Create a Clue token.")]
    public void Classify_ImpulseAndRepeatableClues_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // DISCOVER is a one-card impulse, a card only when the ability comes back
    // (2026-09-29): on every creature spell, off charge counters, on every attack,
    // and granted to an equipped creature. Discover X and discover 10 are the same
    // keyword.
    [InlineData("Monstrous Vortex", "Enchantment",
        "Whenever you cast a creature spell with power 5 or greater, discover X, where X is that spell's mana "
        + "value. (Exile cards from the top of your library until you exile a nonland card with that mana value "
        + "or less. Cast it without paying its mana cost or put it into your hand. Put the rest on the bottom "
        + "in a random order.)")]
    [InlineData("Long-Range Sensor", "Artifact",
        "Whenever you attack a player, put a charge counter on this artifact.\n{1}, Remove two charge counters "
        + "from this artifact: Discover 4. Activate only as a sorcery. (Exile cards from the top of your "
        + "library until you exile a nonland card with mana value 4 or less. Cast it without paying its mana "
        + "cost or put it into your hand. Put the rest on the bottom in a random order.)")]
    [InlineData("Caparocti Sunborn", "Legendary Creature — Human Soldier",
        "Whenever Caparocti Sunborn attacks, you may tap two untapped artifacts and/or creatures you control. "
        + "If you do, discover 3. (Exile cards from the top of your library until you exile a nonland card with "
        + "mana value 3 or less. Cast it without paying its mana cost or put it into your hand. Put the rest on "
        + "the bottom in a random order.)")]
    [InlineData("Swashbuckler's Whip", "Artifact — Equipment",
        "Equipped creature has reach, \"{2}, {T}: Tap target artifact or creature,\" and \"{8}, {T}: Discover "
        + "10.\" (Exile cards from the top of your library until you exile a nonland card with mana value 10 or "
        + "less. Cast it without paying its mana cost or put it into your hand. Put the rest on the bottom in a "
        + "random order.)\nEquip {1}")]
    public void Classify_ARepeatedDiscover_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // …and a discover made ONCE is not, ruled 2026-09-29 when the human took
    // CardAdvantage off Geological Appraiser and Hidden Volcano: an enters
    // trigger, a land that sacrifices itself for it, a spell. The reminder text no
    // longer vouches for it either.
    [InlineData("Geological Appraiser", "Creature — Human Artificer",
        "When this creature enters, if you cast it, discover 3. (Exile cards from the top of your library "
        + "until you exile a nonland card with mana value 3 or less. Cast it without paying its mana cost or "
        + "put it into your hand. Put the rest on the bottom in a random order.)")]
    [InlineData("Hidden Volcano", "Land — Cave",
        "This land enters tapped.\n{T}: Add {R}.\n{4}{R}, {T}, Sacrifice this land: Discover 4. Activate only "
        + "as a sorcery. (Exile cards from the top of your library until you exile a nonland card with mana "
        + "value 4 or less. Cast it without paying its mana cost or put it into your hand. Put the rest on the "
        + "bottom in a random order.)")]
    [InlineData("Daring Discovery", "Sorcery",
        "Up to three target creatures can't block this turn.\nDiscover 4. (Exile cards from the top of your "
        + "library until you exile a nonland card with mana value 4 or less. Cast it without paying its mana "
        + "cost or put it into your hand. Put the rest on the bottom in a random order.)")]
    public void Classify_ADiscoverOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // One card, once, in either vocabulary: the Clue still has to be cashed for
    // {2}, and the one-shot impulse just replaces the card that cast it.
    [InlineData("Cunning Maneuver", "Instant",
        "Create a Clue token. (It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    [InlineData("Equilibrium Adept", "Creature — Human Wizard",
        "When this creature enters, exile the top card of your library. Until the end of your next turn, you may play that card.")]
    [InlineData("Haste Magic", "Instant",
        "Target creature gets +3/+1 and gains haste until end of turn. Exile the top card of your library. You may play that card this turn.")]
    public void Classify_OneCardOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Card parity dressed as card advantage. A loot draws and discards in the
    // same breath (ruled Filter alone on 2026-09-04); cycling pays a card to
    // replace itself and fired only through its reminder text; and an ability
    // that sacrifices the permanent runs once, so it is not the repeatable draw
    // the 2026-09-11 ruling asked for.
    [InlineData("Bazaar of Baghdad", "Land", "{T}: Draw two cards, then discard three cards.")]
    [InlineData("Boosted Sloop", "Artifact — Vehicle", "Menace\nWhenever you attack, draw a card, then discard a card.\nCrew 1")]
    [InlineData("Airship Crash", "Sorcery",
        "Destroy target artifact, enchantment, or creature with flying.\nCycling {2} ({2}, Discard this card: Draw a card.)")]
    [InlineData("Airship Engine Room", "Land",
        "This land enters tapped.\n{T}: Add {U} or {R}.\n{4}, {T}, Sacrifice this land: Draw a card.")]
    public void Classify_CardParity_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // THE CARD ITSELF IS A CARD, ruled 2026-09-19, and it completes a count
    // that had been half-done since 2026-09-16. Ponder is -1 for the Ponder and
    // +1 for the draw, which is nothing; Grab the Prize is -1 for the spell, -1
    // for the discard it charges and +2, which is also nothing and makes it
    // Abandon Attachments with the discard moved into the cost line. So a
    // ONE-SHOT has to draw two more than it pays before it has gained anything.
    //
    // Racers' Scoreboard is the case that proves it: it prints the same words
    // as Emmessi Tome ("draw two cards, then discard a card") on an ENTERS
    // trigger instead of an activated ability, and nets nothing.
    [InlineData("Ponder", "Sorcery",
        "Look at the top three cards of your library, then put them back in any order. You may shuffle.\n"
        + "Draw a card.")]
    [InlineData("Grab the Prize", "Sorcery",
        "As an additional cost to cast this spell, discard a card.\n"
        + "Draw two cards. If the discarded card wasn't a land card, Grab the Prize deals 2 damage to each opponent.")]
    [InlineData("Racers' Scoreboard", "Artifact",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns when "
        + "an opponent loses life. Max speed is 4.)\n"
        + "When this artifact enters, draw two cards, then discard a card.\n"
        + "Max speed — Spells you cast cost {1} less to cast.")]
    [InlineData("Romantic Rendezvous", "Sorcery", "Discard a card, then draw two cards.")]
    [InlineData("Careful Study", "Sorcery", "Draw two cards, then discard two cards.")]
    [InlineData("Alpharael, Dreaming Acolyte", "Legendary Creature — Human Cleric",
        "When Alpharael enters, draw two cards. Then discard two cards unless you discard an artifact card.\n"
        + "During your turn, Alpharael has deathtouch.")]
    // An OMEN shuffles itself away, it is not cast twice (2026-09-29).
    [InlineData("Stormshriek Feral // Flush Out", "Creature — Dragon // Sorcery — Omen",
        "Flying, haste\n{1}{R}: This creature gets +1/+0 until end of turn.\nDiscard a card. If you do, draw "
        + "two cards. (Then shuffle this card into its owner's library.)")]
    public void Classify_AOneShotThatPaysForItself_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A REPEATABLE ability paid for the card once and never again, so one more
    // than it hands back is already a card: that is the whole difference
    // between Emmessi Tome and Racers' Scoreboard above. A one-shot qualifies
    // at two, which Casting of Bones reaches — and which never parsed, because
    // "discard ONE OF THEM" does not repeat the word "card".
    [InlineData("Emmessi Tome", "Artifact — Book", "{5}, {T}: Draw two cards, then discard a card.")]
    [InlineData("Casting of Bones", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, draw three cards, then discard one of them.")]
    [InlineData("Focus the Mind", "Instant",
        "This spell costs {2} less to cast if you've cast another spell this turn.\n"
        + "Draw three cards, then discard a card.")]
    public void Classify_ALootThatGainsAfterPayingForTheCard_IsCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The two tags ARE exclusive, ruled 2026-09-27 — "se ha CardAdvantage non ha
    // Filter" — which overturns 2026-09-19. These three had pinned "both" until
    // then: Casting of Bones draws three and keeps two, Emmessi Tome loots two
    // for one every turn, and Plan the Heist surveils beside a draw three.
    [InlineData("Casting of Bones", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, draw three cards, then discard one of them.")]
    [InlineData("Emmessi Tome", "Artifact — Book", "{5}, {T}: Draw two cards, then discard a card.")]
    [InlineData("Plan the Heist", "Sorcery",
        "Surveil 3 if you have no cards in hand. Then draw three cards. (To surveil 3, look at the top three "
        + "cards of your library, then put any number of them into your graveyard and the rest on top of your "
        + "library in any order.)\nPlot {3}{U} (You may pay {3}{U} and exile this card from your hand. Cast it "
        + "as a sorcery on a later turn without paying its mana cost. Plot only as a sorcery.)")]
    public void Classify_ASelectionThatComesOutAhead_IsCardAdvantageAndNotFilter(
        string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // CardAdvantage and Filter on DIFFERENT abilities, ruled 2026-09-29 ("sono due
    // abilità diverse"): a repeatable selection beside a separate draw does both.
    [InlineData("Qiqirn Merchant", "Creature — Beast Citizen",
        "{1}, {T}: Draw a card, then discard a card.\n{7}, {T}, Sacrifice this creature: Draw three cards. "
        + "This ability costs {1} less to activate for each Town you control.")]
    [InlineData("Dimir Strandcatcher", "Creature — Faerie Rogue",
        "Flying\nWhenever you attack, surveil X, where X is the number of opponents being attacked.\nAt the "
        + "beginning of each end step, if three or more cards were put into your graveyard from anywhere other "
        + "than the battlefield this turn, draw a card.")]
    [InlineData("Arcade Gannon", "Legendary Creature — Human Doctor",
        "{T}: Draw a card, then discard a card. Put a quest counter on Arcade Gannon.\nFor Auld Lang Syne — "
        + "Once during each of your turns, you may cast an artifact or Human spell from your graveyard with "
        + "mana value less than or equal to the number of quest counters on Arcade Gannon.")]
    [InlineData("Veronica, Dissident Scribe", "Legendary Creature — Human Artificer Rogue",
        "Menace\nWhenever Veronica attacks, you may discard a card. If you do, draw a card.\nWhenever you "
        + "discard one or more nonland cards for the first time each turn, create a Junk token. (It's an "
        + "artifact with \"{T}, Sacrifice this token: Exile the top card of your library. You may play that card "
        + "this turn. Activate only as a sorcery.\")")]
    public void Classify_SelectingAndGainingOnDifferentAbilities_IsBoth(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // …but not a selection in the SAME ability as the draw (True Identity,
    // Champions from Beyond), one made ONCE beside a draw engine (Case of the
    // Crimson Pulse, Kaho), nor cards searched out and exiled to be CAST (Ugin,
    // 2026-09-29).
    [InlineData("True Identity", "Enchantment",
        "Whenever this enchantment or another permanent you control is turned face up, scry 1, then draw a "
        + "card. This ability triggers only once each turn.\nDisguise {W} (You may cast this card face down for "
        + "{3} as a 2/2 creature with ward {2}. Turn it face up any time for its disguise cost.)")]
    [InlineData("Champions from Beyond", "Enchantment",
        "When this enchantment enters, create X 1/1 colorless Hero creature tokens.\nLight Party — Whenever "
        + "you attack with four or more creatures, scry 2, then draw a card.\nFull Party — Whenever you attack "
        + "with eight or more creatures, those creatures get +4/+4 until end of turn.")]
    [InlineData("Case of the Crimson Pulse", "Enchantment — Case",
        "When this Case enters, discard a card, then draw two cards.\nTo solve — You have no cards in hand. "
        + "(If unsolved, solve at the beginning of your end step.)\nSolved — At the beginning of your upkeep, "
        + "discard your hand, then draw two cards.")]
    [InlineData("Kaho, Minamo Historian", "Legendary Creature — Human Wizard",
        "When Kaho enters, search your library for up to three instant cards, exile them, then shuffle.\n{X}, "
        + "{T}: You may cast a spell with mana value X from among cards exiled with Kaho without paying its "
        + "mana cost.")]
    [InlineData("Ugin, Eye of the Storms", "Legendary Planeswalker — Ugin",
        "When you cast this spell, exile up to one target permanent that's one or more colors.\nWhenever you "
        + "cast a colorless spell, exile up to one target permanent that's one or more colors.\n+2: You gain 3 "
        + "life and draw a card.\n0: Add {C}{C}{C}.\n−11: Search your library for any number of colorless nonland "
        + "cards, exile them, then shuffle. Until end of turn, you may cast those cards without paying their "
        + "mana costs.")]
    public void Classify_SelectingOnceOrInTheSameAbility_IsCardAdvantageAlone(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Named CardAdvantage by the human on 2026-09-27: a pile of permanents
    // traded for as many cards, a draw mode back every turn, and a look that
    // keeps every card it saw.
    [InlineData("Pitiless Carnage", "Sorcery",
        "Sacrifice any number of permanents you control, then draw that many cards.\nPlot {1}{B}{B} (You may "
        + "pay {1}{B}{B} and exile this card from your hand. Cast it as a sorcery on a later turn without "
        + "paying its mana cost. Plot only as a sorcery.)")]
    [InlineData("Monument to Endurance", "Artifact",
        "Whenever you discard a card, choose one that hasn't been chosen this turn —\n• Draw a card.\n• Create "
        + "a Treasure token.\n• Each opponent loses 3 life.")]
    [InlineData("Make Your Own Luck", "Sorcery",
        "Look at the top three cards of your library. You may exile a nonland card from among them. If you "
        + "do, it becomes plotted. Put the rest into your hand. (You may cast it as a sorcery on a later turn "
        + "without paying its mana cost.)")]
    // Keeping ALL of a kind, and milling to keep one over and over, are the
    // several-cards reading — both hand-tagged CardAdvantage and read once
    // keeping one became Filter.
    [InlineData("Marina Vendrell", "Legendary Creature — Human Warlock",
        "When Marina Vendrell enters, reveal the top seven cards of your library. Put all enchantment cards "
        + "from among them into your hand and the rest on the bottom of your library in a random order.\n{T}: "
        + "Lock or unlock a door of target Room you control. Activate only as a sorcery.")]
    [InlineData("Sludge Titan", "Creature — Zombie Giant",
        "Trample\nWhenever this creature enters or attacks, mill five cards. You may put a creature card "
        + "and/or a land card from among them into your hand.")]
    [InlineData("Szarekh, the Silent King", "Legendary Artifact Creature — Necron",
        "Flying\nMy Will Be Done — Whenever Szarekh attacks, mill three cards. You may put an artifact "
        + "creature card or Vehicle card from among the cards milled this way into your hand.")]
    // "Up to two", revealed: more than one, whatever the verb.
    [InlineData("Pieces of the Puzzle", "Sorcery",
        "Reveal the top five cards of your library. Put up to two instant and/or sorcery cards from among "
        + "them into your hand and the rest into your graveyard.")]
    // OFFSPRING makes the enters trigger fire twice: "solo CA", ruled the same day.
    [InlineData("Thundertrap Trainer", "Creature — Otter Wizard",
        "Offspring {4} (You may pay an additional {4} as you cast this spell. If you do, when this creature "
        + "enters, create a 1/1 token copy of it.)\nWhen this creature enters, look at the top four cards of "
        + "your library. You may reveal a noncreature, nonland card from among them and put it into your hand. "
        + "Put the rest on the bottom of your library in a random order.")]
    public void Classify_ADrawRuledIn_IsCardAdvantageAndNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Whose draw is it? A card that only ever draws for somebody ELSE gives
    // nothing away for free. Ruled 2026-09-19 over 10 cards, 8 untagged. The
    // test is done by STRIPPING rather than a lookbehind, because "defending
    // player MAY draw a card" puts a word between the subject and the verb.
    [InlineData("Sibilant Spirit", "Creature — Spirit",
        "Flying\nWhenever this creature attacks, defending player may draw a card.")]
    [InlineData("Phelddagrif", "Legendary Creature — Phelddagrif",
        "{G}: Phelddagrif gains trample until end of turn. Target opponent creates a 1/1 green Hippo creature token.\n"
        + "{W}: Phelddagrif gains flying until end of turn. Target opponent gains 2 life.\n"
        + "{U}: Return Phelddagrif to its owner's hand. Target opponent may draw a card.")]
    // Triggering OFF a draw is not drawing: 9 of the 10 cards written this way
    // carry no tag at all.
    [InlineData("Underworld Dreams", "Enchantment",
        "Whenever an opponent draws a card, this enchantment deals 1 damage to that player.")]
    [InlineData("Clinquant Skymage", "Creature — Bird Wizard",
        "Flying\nWhenever you draw a card, put a +1/+1 counter on this creature.")]
    // And a draw that exiles the card ITSELF out of your graveyard runs once,
    // the same reading that keeps a one-shot sacrifice out. Four of the fifty
    // over-fires were identical Surveyors printed with this line.
    [InlineData("Goblin Surveyor", "Creature — Goblin Scout",
        "Trample\nStart your engines! (If you have no speed, it starts at 1. It increases once on each of your "
        + "turns when an opponent loses life. Max speed is 4.)\n"
        + "Max speed — {3}, Exile this card from your graveyard: Draw a card.")]
    public void Classify_ADrawThatIsNotYoursOrRunsOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    // The victim has to be the one DRAWING. "A creature you control becomes the
    // target of a spell AN OPPONENT CONTROLS, draw a card" names an opponent in a
    // possessive clause and hands the card to you; twenty characters of filler
    // between the subject and the verb read that as somebody else's draw. Ruled
    // 2026-09-20 — the same trap GivesControlAway fell into the day before.
    public void Classify_ADrawNamingAnOpponentInPassing_IsStillCardAdvantage()
    {
        Card card = MakeCard("Surrak, Elusive Hunter", "Legendary Creature — Human Warrior",
            "This spell can't be countered.\nTrample\nWhenever a creature you control or a creature spell "
            + "you control becomes the target of a spell or ability an opponent controls, draw a card.");

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-09-20. Every one of these hands you a card you would otherwise
    // have had to draw, and hands you another one next turn: the ability has a
    // cost you can pay again, or a trigger that comes round again, so the card
    // it cost was paid once. Browse and Water Tribe Rallier take one off the top
    // of the library, Rediscover the Way does it on two Saga chapters, Elkin
    // Bottle and Parapet Thrasher exile the top card and let you play it, Tersa
    // Lightshatter does the same out of the graveyard, and Banon casts one
    // creature out of it on each of your turns.
    [InlineData("Browse", "Enchantment",
        "{2}{U}{U}: Look at the top five cards of your library, put one of them into your hand, "
        + "and exile the rest.")]
    [InlineData("Water Tribe Rallier", "Creature — Human Soldier Ally",
        "Waterbend {5}: Look at the top four cards of your library. You may reveal a creature card "
        + "with power 3 or less from among them and put it into your hand. Put the rest on the bottom "
        + "of your library in a random order.")]
    [InlineData("Rediscover the Way", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I, II — Look at the top three cards of your library. Put one of them into your hand and the "
        + "rest on the bottom of your library in any order.\n"
        + "III — Whenever you cast a noncreature spell this turn, target creature you control gains "
        + "double strike until end of turn.")]
    [InlineData("Elkin Bottle", "Artifact",
        "{3}, {T}: Exile the top card of your library. Until the beginning of your next upkeep, "
        + "you may play that card.")]
    [InlineData("Parapet Thrasher", "Creature — Dragon",
        "Flying\nWhenever one or more Dragons you control deal combat damage to an opponent, choose "
        + "one that hasn't been chosen this turn —\n• Destroy target artifact that opponent controls.\n"
        + "• This creature deals 4 damage to each other opponent.\n"
        + "• Exile the top card of your library. You may play it this turn.")]
    [InlineData("Tersa Lightshatter", "Legendary Creature — Orc Wizard",
        "Haste\nWhen Tersa Lightshatter enters, discard up to two cards, then draw that many cards.\n"
        + "Whenever Tersa Lightshatter attacks, if there are seven or more cards in your graveyard, "
        + "exile a card at random from your graveyard. You may play that card this turn.")]
    [InlineData("Banon, the Returners' Leader", "Legendary Creature — Human Rebel",
        "Pray — Once during each of your turns, you may cast a creature spell from among cards in your "
        + "graveyard that were put there from anywhere other than the battlefield this turn.\n"
        + "Whenever you attack, you may pay {1} and discard a card. If you do, draw a card.")]
    // The kept card qualified at length before "put it into your hand" (read up to
    // 200 characters since 2026-09-29): Radagast the Brown, tagged by the human,
    // and Whiskervale Forerunner.
    [InlineData("Radagast the Brown", "Legendary Creature — Avatar Wizard",
        "Whenever Radagast or another nontoken creature you control enters, look at the top X cards of your "
        + "library, where X is that creature's mana value. You may reveal a creature card that doesn't share a "
        + "creature type with a creature you control from among those cards and put it into your hand. Put the "
        + "rest on the bottom of your library in a random order.")]
    [InlineData("Whiskervale Forerunner", "Creature — Mouse Bard",
        "Valiant — Whenever this creature becomes the target of a spell or ability you control for the first "
        + "time each turn, look at the top five cards of your library. You may reveal a creature card with mana "
        + "value 3 or less from among them. You may put it onto the battlefield if it's your turn. If you don't "
        + "put it onto the battlefield, put it into your hand. Put the rest on the bottom of your library in a "
        + "random order.")]
    // Mill a few and keep ALL of a kind from among them, a card even once: Tazri
    // and Beluna Grandsquall, tagged by the human on 2026-09-29.
    [InlineData("Tazri, Stalwart Survivor", "Legendary Creature — Human Warrior",
        "Each creature you control has \"{T}: Add one mana of any of this creature's colors. Spend this mana "
        + "only to activate an ability of a creature. Activate only if this creature has another activated "
        + "ability.\"\n{W}{U}{B}{R}{G}, {T}: Mill five cards. Put all creature cards with activated abilities "
        + "that aren't mana abilities from among the milled cards into your hand.")]
    [InlineData("Beluna Grandsquall // Seek Thrills", "Legendary Creature — Giant Noble // Instant — Adventure",
        "Trample\nPermanent spells you cast that have an Adventure cost {1} less to cast.\nMill seven cards. "
        + "Then put all cards that have an Adventure from among the milled cards into your hand.")]
    // A draw of yours past the 160 characters a long trigger allows, in the two
    // wordings the human tagged on 2026-09-29: "Otherwise, you draw a card"
    // (Gandalf) and a villainous choice whose first branch is your draw (Davros).
    [InlineData("Gandalf, Westward Voyager", "Legendary Creature — Avatar Wizard",
        "Whenever you cast a spell with mana value 5 or greater, each opponent reveals the top card of their "
        + "library. If any of those cards shares a card type with that spell, copy that spell, you may choose "
        + "new targets for the copy, and each opponent draws a card. Otherwise, you draw a card. (A copy of a "
        + "permanent spell becomes a token.)")]
    [InlineData("Davros, Dalek Creator", "Legendary Artifact Creature — Alien Scientist",
        "Menace\nAt the beginning of your end step, create a 3/3 black Dalek artifact creature token with "
        + "menace if an opponent lost 3 or more life this turn. Then each opponent who lost 3 or more life this "
        + "turn faces a villainous choice — You draw a card, or that player discards a card.")]
    // A DRAW MODE under a trigger that repeats, ruled 2026-10-13.
    [InlineData("Glissa Sunslayer", "Legendary Creature — Phyrexian Zombie Elf",
        "First strike, deathtouch\nWhenever Glissa Sunslayer deals combat damage to a player, choose one —\n• "
        + "You draw a card and lose 1 life.\n• Destroy target enchantment.\n• Remove up to three counters from "
        + "target permanent.")]
    public void Classify_ACardYouCanGoBackForEveryTurn_IsCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // And the one-shots that print the same words. Repeatability belongs to ONE
    // ABILITY: Morbius the Living Vampire and Lupinflower Village spend
    // themselves to pay for theirs, and Guru Pathik and Equilibrium Adept run
    // off an ENTERS trigger while carrying an unrelated "Whenever" underneath —
    // reading the card as a whole billed them for somebody else's repetition.
    // Rook Turret and Brainstorm hand back exactly what they took.
    [InlineData("Morbius the Living Vampire", "Legendary Creature — Vampire Scientist Villain",
        "Flying, vigilance, lifelink\n{U}{B}, Exile this card from your graveyard: Look at the top "
        + "three cards of your library. Put one of them into your hand and the rest on the bottom of "
        + "your library in any order.")]
    [InlineData("Lupinflower Village", "Land",
        "{T}: Add {C}.\n{T}: Add {W}. Spend this mana only to cast a creature spell.\n"
        + "{1}{W}, {T}, Sacrifice this land: Look at the top six cards of your library. You may reveal "
        + "a Bat, Bird, Mouse, or Rabbit card from among them and put it into your hand. Put the rest "
        + "on the bottom of your library in a random order.")]
    [InlineData("Guru Pathik", "Legendary Creature — Human Monk Ally",
        "When Guru Pathik enters, look at the top five cards of your library. You may reveal a Lesson, "
        + "Saga, or Shrine card from among them and put it into your hand. Put the rest on the bottom "
        + "of your library in a random order.\n"
        + "Whenever you cast a Lesson, Saga, or Shrine spell, put a +1/+1 counter on another target "
        + "creature you control.")]
    [InlineData("Equilibrium Adept", "Creature — Dog Monk",
        "When this creature enters, exile the top card of your library. Until the end of your next "
        + "turn, you may play that card.\n"
        + "Flurry — Whenever you cast your second spell each turn, this creature gains double strike "
        + "until end of turn.")]
    [InlineData("Rook Turret", "Artifact Creature — Construct",
        "Flying\nWhenever another artifact you control enters, you may draw a card. If you do, "
        + "discard a card.")]
    [InlineData("Brainstorm", "Instant",
        "Draw three cards, then put two cards from your hand on top of your library in any order.")]
    public void Classify_TheSameWordsRunOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A draw mode bought with a PERMANENT, or one each mode runs once a GAME,
    // is not a card every turn (2026-10-13: Gorbag, Silent Hallcreeper).
    [InlineData("Gorbag of Minas Morgul", "Legendary Creature — Orc Soldier",
        "Whenever a Goblin or Orc you control deals combat damage to a player, you may sacrifice it. When you "
        + "do, choose one —\n• Draw a card.\n• Create a Treasure token. (It's an artifact with \"{T}, Sacrifice "
        + "this token: Add one mana of any color.\")")]
    [InlineData("Silent Hallcreeper", "Enchantment Creature — Horror",
        "This creature can't be blocked.\nWhenever this creature deals combat damage to a player, choose one "
        + "that hasn't been chosen —\n• Put two +1/+1 counters on this creature.\n• Draw a card.\n• This creature "
        + "becomes a copy of another target creature you control.")]
    public void Classify_ADrawModeBoughtOrOnceAGame_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    // A planeswalker's "draw, then discard unless" is both: a card when the
    // condition holds, a loot when it does not (Kaito Shizuki, 2026-10-13).
    public void Classify_APlaneswalkerLootUnless_IsCardAdvantageAndFilter()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Kaito Shizuki", "Legendary Planeswalker — Kaito",
            "At the beginning of your end step, if Kaito entered this turn, he phases out.\n+1: Draw a card. Then "
            + "discard a card unless you attacked this turn.\n−2: Create a 1/1 blue Ninja creature token with \"This "
            + "token can't be blocked.\"\n−7: You get an emblem with \"Whenever a creature you control deals combat "
            + "damage to a player, search your library for a blue or black creature card, put it onto the "
            + "battlefield, then shuffle.\""));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Three one-offs, each recovered 2026-09-20 rather than left as a known
    // miss. Three Wishes puts 108 characters between exiling three cards and
    // letting you play them, where the rule allowed 80. Memories Returning
    // reaches into your hand three times, one card at a time. And Mnemonic
    // Sliver's draw sacrifices "this permanent" inside QUOTES, handed to every
    // Sliver — the body that pays is a different one each time, so the card
    // spends none of itself and the one-shot veto does not apply.
    [InlineData("Three Wishes", "Instant",
        "Exile the top three cards of your library face down. You may look at those cards for as long "
        + "as they remain exiled. Until your next turn, you may play those cards. At the beginning of "
        + "your next upkeep, put any of those cards you didn't play into your graveyard.")]
    [InlineData("Memories Returning", "Sorcery",
        "Reveal the top five cards of your library. Put one of them into your hand. Then choose an "
        + "opponent. They put one on the bottom of your library. Then you put one into your hand. Then "
        + "they put one on the bottom of your library. Put the other into your hand.\nFlashback {7}{U}{U}")]
    [InlineData("Mnemonic Sliver", "Creature — Sliver",
        "All Slivers have \"{2}, Sacrifice this permanent: Draw a card.\"")]
    // Ruled 2026-09-20 after it was put to the human as the one card standing
    // against the self-sacrifice rule: the sacrifice is refunded by the trigger
    // above it, which hands back a token copy of the artifact just spent, so the
    // ability is on the table again and the card was never really paid.
    [InlineData("Esoteric Duplicator", "Artifact",
        "Whenever you sacrifice this artifact or another artifact, you may pay {2}. If you do, at "
        + "the beginning of the next end step, create a token that's a copy of that artifact.\n"
        + "{2}, Sacrifice this artifact: Draw a card.")]
    public void Classify_TheOneOffsThatStillGainACard_AreCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-09-21. A REPLACEMENT draw adds nothing — it spends the draw you
    // were going to have anyway (0 of 3 reviewed cards tagged). A repeatable
    // draw whose trigger names something NARROW is not one you can count on: a
    // coin flip, or becoming the target of an Aura spell or an activated
    // ability. (Paying an opponent a permanent for it was on this list until
    // 2026-10-14 — see Classify_ADrawPaidForWithAPermanentGivenAway_IsCardAdvantage.)
    // The line this does not cross is Surrak, whose trigger is any spell an
    // opponent aims at your creatures — that happens by itself, and Surrak
    // keeps the tag.
    [InlineData("Aladdin's Lamp", "Artifact",
        "{X}, {T}: The next time you would draw a card this turn, instead look at the top X cards of "
        + "your library, put all but one of them on the bottom of your library in a random order, then "
        + "draw a card. X can't be 0.")]
    [InlineData("Goblin Artisans", "Creature — Goblin Artificer",
        "{T}: Flip a coin. If you win the flip, draw a card. If you lose the flip, counter target "
        + "artifact spell you control.")]
    [InlineData("Fugitive Druid", "Creature — Human Druid",
        "Whenever this creature becomes the target of an Aura spell, you draw a card.")]
    public void Classify_ADrawYouCannotCountOn_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // And the four wordings the tag could not read, all ruled the same day. An
    // ADDITIONAL card is a card (Howling Mine, Sylvan Library). A trigger can
    // have one sentence in front of it (Rowen). "That many cards FROM THE TOP"
    // is the same look with the count carried in from the trigger (Symbiote
    // Spider-Man). And a card that gives ITSELF a second cast is not one card,
    // so the loot that nets zero on the first cast comes out ahead over both —
    // Welcome the Dead is -3 +4 with its flashback, which is how the human read
    // it.
    [InlineData("Howling Mine", "Artifact",
        "At the beginning of each player's draw step, if this artifact is untapped, that player draws "
        + "an additional card.")]
    [InlineData("Rowen", "Enchantment",
        "Reveal the first card you draw each turn. Whenever you reveal a basic land card this way, "
        + "draw a card.")]
    [InlineData("Symbiote Spider-Man", "Legendary Creature — Symbiote Human Hero",
        "Whenever this creature deals combat damage to a player, look at that many cards from the top "
        + "of your library. Put one of them into your hand and the rest into your graveyard.")]
    [InlineData("Welcome the Dead", "Sorcery",
        "Draw two cards, then discard a card and you lose 2 life.\nFlashback {4}{B}")]
    public void Classify_TheWordingsTheTagCouldNotRead_AreCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The carve-out that keeps the rule above honest: "TARGET player" and
    // "EACH player" are NOT somebody else's draw, because you point these at
    // yourself. 12 of the 21 cards written that way are tagged.
    [InlineData("Ancestral Recall", "Instant", "Target player draws three cards.")]
    [InlineData("Braingeyser", "Sorcery", "Target player draws X cards.")]
    public void Classify_TargetPlayerDraws_IsStillCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The 2026-09-16 second-hand ruling, widened 2026-09-19 because its window
    // was too short for the very cards it named: Glarb puts 44 characters
    // between "play lands" and "from the top of your library" where 40 were
    // allowed, and Assemble the Players never says "play" at all.
    [InlineData("Glarb, Calamity's Augur", "Legendary Creature — Frog Wizard Noble",
        "Deathtouch\nYou may look at the top card of your library any time.\n"
        + "You may play lands and cast spells with mana value 4 or greater from the top of your library.\n"
        + "{T}: Surveil 2.")]
    [InlineData("Assemble the Players", "Enchantment",
        "You may look at the top card of your library any time.\n"
        + "Once each turn, you may cast a creature spell with power 2 or less from the top of your library.")]
    // Several Clues in one breath, the reading the Treasure got on 2026-09-18:
    // one has to be cashed for {2} and is a rider, X of them is a draw X.
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Mole Beast",
        "When Nyla enters, exile up to one target creature card from your graveyard. If you do, you lose X life "
        + "and create X Clue tokens, where X is that card's mana value. (A Clue token is an artifact with "
        + "\"{2}, Sacrifice this token: Draw a card.\")\n"
        + "At the beginning of your end step, if you control no Clues, return target card exiled with Nyla to "
        + "its owner's hand.")]
    [InlineData("Tamiyo Meets the Story Circle", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Until your next turn, whenever a creature attacks you or a planeswalker you control, it gets "
        + "-2/-0 until end of turn.\n"
        + "II — Discard any number of cards, then investigate twice for each card discarded this way.\n"
        + "III — Shuffle up to three target cards from your graveyard into your library.")]
    // "Draw that many cards" is a draw-for-each written the other way round,
    // and the count is never one. Restricted to YOUR draw, because "each player
    // draws that many" is a wheel and pays everybody.
    [InlineData("Niv-Mizzet, Visionary", "Legendary Creature — Dragon Wizard",
        "Flying\nYou have no maximum hand size.\n"
        + "Whenever a source you control deals noncombat damage to an opponent, you draw that many cards.")]
    public void Classify_MoreWaysToGetACard_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The top of your library is a second hand, ruled 2026-09-16: these never
    // run out of cards to play even though they never draw one.
    [InlineData("Fblthp, Lost on the Range", "Legendary Creature — Homunculus",
        "Ward {2}\nYou may look at the top card of your library any time.\nYou may play the top card of your library.")]
    [InlineData("Traveling Chocobo", "Creature — Bird",
        "You may look at the top card of your library any time.\nYou may play lands and cast Bird spells from the top of your library.")]
    // Looking at N and taking MORE THAN ONE is a draw with selection; taking
    // exactly one is Filter, and Stock Up is both.
    [InlineData("Stock Up", "Sorcery",
        "Look at the top five cards of your library. Put two of them into your hand and the rest on the bottom of your library in any order.")]
    // Casting out of a graveyard, when you can go back to it every turn.
    [InlineData("Festival of Embers", "Enchantment",
        "During your turn, you may cast instant and sorcery spells from your graveyard by paying 1 life in addition to their other costs.")]
    [InlineData("Edgar, Master Machinist", "Legendary Creature — Human Artificer",
        "Once during each of your turns, you may cast an artifact spell from your graveyard. If you cast a spell this way, that artifact enters tapped.")]
    public void Classify_ASecondHand_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The edges of those three. One card out of the graveyard is one card, and
    // warp and flashback reminder text says "cast THIS CARD" — from your hand at
    // that, which is why the rule refuses the pronoun.
    //
    // Browse used to stand here, on the reading that taking ONE card off the top
    // is selection and not a card gained. The 2026-09-20 ruling overturned that
    // for the repeatable case and Browse is the card it was ruled on, so it has
    // moved to Classify_ACardYouCanGoBackForEveryTurn_IsCardAdvantage.
    [InlineData("Bygone Colossus", "Creature — Giant",
        "Warp {3} (You may cast this card from your hand for its warp cost. Exile it as it resolves.)")]
    public void Classify_OneCardFromElsewhere_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Four more ways the card is handed straight back, none of which says
    // "discard" in the shape the loot pattern reads.
    [InlineData("Dream Cache", "Sorcery",
        "Draw three cards, then put two cards from your hand both on top of your library or both on the bottom.")]
    [InlineData("Lat-Nam's Legacy", "Instant",
        "Shuffle a card from your hand into your library. If you do, draw two cards at the beginning of the next turn's upkeep.")]
    [InlineData("Jandor's Ring", "Artifact",
        "{2}, {T}, Discard the last card you drew this turn: Draw a card.")]
    [InlineData("Green Goblin, Revenant", "Legendary Creature — Goblin",
        "Flying, deathtouch\nWhenever Green Goblin attacks, discard a card. Then draw a card.")]
    public void Classify_ADrawPaidForWithACard_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A repeating draw hiding behind a label. The anchored rule wants "whenever"
    // at the start of the line; modern cards put an ability word or a Siege
    // bullet in front of it.
    [InlineData("Entity Tracker", "Creature — Spirit",
        "Flash\nEerie — Whenever an enchantment you control enters and whenever you fully unlock a Room, draw a card.")]
    [InlineData("G'raha Tia", "Legendary Creature — Cat Scholar",
        "Reach\nThe Allagan Eye — Whenever one or more other creatures and/or artifacts you control enter, draw a card.")]
    [InlineData("Frostcliff Siege", "Enchantment",
        "As this enchantment enters, choose Jeskai or Temur.\n• Jeskai — Whenever a creature you control attacks alone, draw a card.")]
    public void Classify_ATriggerBehindALabel_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // "Draw a card for each X" is multi-card draw written the other way round,
    // and the count-first wording was missed entirely.
    [InlineData("Balance of Power", "Sorcery", "If target opponent has more cards in hand than you, draw cards equal to the difference.")]
    [InlineData("Become the Avalanche", "Sorcery", "Draw a card for each creature you control with power 4 or greater.")]
    public void Classify_CountFirstDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The Lieutenant ruling of 2026-10-15 holds for every tag, not only Tokens: a
    // draw behind it never happens in a cube.
    [InlineData("Loyal Drake", "Creature — Drake",
        "Flying\nLieutenant — At the beginning of combat on your turn, if you control your commander, draw a "
        + "card.")]
    [InlineData("Stormsurge Kraken", "Creature — Kraken",
        "Hexproof\nLieutenant — As long as you control your commander, this creature gets +2/+2 and has "
        + "\"Whenever this creature becomes blocked, you may draw two cards.\"")]
    public void Classify_ALieutenantAbility_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Two or more cards, or a draw you can come back to.
    [InlineData("Divination", "Sorcery", "Draw two cards.")]
    // The user's own example: an activated draw on a body is card advantage
    // because nothing stops you doing it again.
    [InlineData("Azure Drake", "Creature — Drake", "Flying\n{3}{U}: Draw a card.")]
    [InlineData("Ophidian", "Creature — Serpent", "Whenever this creature deals combat damage to a player, you may draw a card.")]
    [InlineData("Howling Mine", "Artifact", "At the beginning of each player's draw step, that player draws a card.")]
    public void Classify_RepeatableOrMultipleDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // One card off a spell you cast replaces the spell: parity, not advantage.
    [InlineData("Eject", "Instant", "This spell can't be countered.\nReturn target nonland permanent to its owner's hand.\nDraw a card.")]
    [InlineData("Airbending Lesson", "Sorcery", "Airbend target nonland permanent.\nDraw a card.")]
    // An enters-the-battlefield draw fires once, so it is the same one-shot
    // replacement as a cantrip — "whenever" would be a different matter.
    [InlineData("Wall of Omens", "Creature — Wall", "Defender\nWhen this creature enters, draw a card.")]
    public void Classify_SingleOneShotDraw_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Any number of cards of a type CHOSEN first is CardAdvantage, ruled with the
    // chosen type on 2026-10-30 (For the Ancestors, kept by name); a printed type
    // is TribalCard's Filter (Karumonix).
    [InlineData("For the Ancestors", "Instant",
        "Choose a creature type. Look at the top six cards of your library. You may reveal any number of "
        + "cards of the chosen type from among them and put the revealed cards into your hand. Put the rest on "
        + "the bottom of your library in a random order.\nFlashback {3}{G} (You may cast this card from your "
        + "graveyard for its flashback cost. Then exile it.)")]
    public void Classify_AnyNumberOfAChosenType_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A loot on one ability and a draw of several on another is CardAdvantage,
    // read 2026-10-30: The Royal Scions' +1 beside its -8 draw four, tagged by
    // hand on 2026-10-29 (2 of 2 with Qiqirn).
    [InlineData("The Royal Scions", "Legendary Planeswalker — Will Rowan",
        "+1: Draw a card, then discard a card.\n+1: Target creature gets +2/+0 and gains first strike and "
        + "trample until end of turn.\n−8: Draw four cards. When you do, The Royal Scions deals damage to any "
        + "target equal to the number of cards in your hand.")]
    public void Classify_ALootBesideADrawOfSeveral_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A standing permission to cast from the graveyard is CardAdvantage, read
    // 2026-10-30 (Kagha, tagged 2026-10-27; 9 of 10 reviewed, The Eighth Doctor
    // the exception).
    [InlineData("Kagha, Shadow Archdruid", "Legendary Creature — Elf Druid",
        "Whenever Kagha attacks, it gains deathtouch until end of turn. Mill two cards. (Put the top two "
        + "cards of your library into your graveyard.)\nOnce during each of your turns, you may play a land or "
        + "cast a permanent spell from among cards in your graveyard that were put there from your library this "
        + "turn.")]
    public void Classify_AStandingPermissionToCastFromElsewhere_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    // A copied PERMANENT spell is a token, not a second card: Archmage of
    // Echoes is Tokens alone by hand (2026-10-13).
    public void Classify_CopyingYourPermanentSpell_IsNotCardAdvantage()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Archmage of Echoes", "Creature — Faerie Wizard",
            "Flying, ward {2}\nWhenever you cast a Faerie or Wizard permanent spell, copy it. (The copy becomes a "
            + "token.)"));

        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // ONE card off the top, into your hand, every turn. The put-into-hand rule
    // wanted a count where these cards write none, and it is the same second
    // hand either way (2026-09-21). 13 reviewed cards, 11 tagged.
    [InlineData("Darkstar Augur", "Creature — Bat Warlock",
        "Flying\nAt the beginning of your upkeep, reveal the top card of your library and put that card "
        + "into your hand. You lose life equal to its mana value.")]
    [InlineData("Traveling Botanist", "Creature — Dog Scout",
        "Whenever this creature becomes tapped, look at the top card of your library. If it's a land card, "
        + "you may reveal it and put it into your hand. If you don't put the card into your hand, you may "
        + "put it into your graveyard.")]
    // …and the same hand dug for with a SEARCH, which only counts when the
    // ability repeats: Gift of Estates says the identical words on a one-shot
    // sorcery and is Ramp alone.
    [InlineData("Land Tax", "Enchantment",
        "At the beginning of your upkeep, if an opponent controls more lands than you, you may search your "
        + "library for up to three basic land cards, reveal them, put them into your hand, then shuffle.")]
    // The opponent may be the one doing the looking.
    [InlineData("Phyrexian Portal", "Artifact",
        "{3}: If your library has ten or more cards in it, target opponent looks at the top ten cards of "
        + "your library and separates them into two face-down piles. Exile one of those piles. Search the "
        + "other pile for a card, put it into your hand, then shuffle the rest of that pile into your library.")]
    // A permanent sold for MORE THAN ONE card is a trade it wins. The one-shot
    // sacrifice rule was written for the one-card case and still reads that.
    [InlineData("Blitzball", "Artifact",
        "{T}: Add one mana of any color.\n"
        + "GOOOOAAAALLL! — {T}, Sacrifice this artifact: Draw two cards. Activate only if an opponent was "
        + "dealt combat damage by a legendary creature this turn.")]
    // A triggered draw printed INSIDE QUOTES was handed to a body that keeps
    // it, the reading Mnemonic Sliver already got.
    [InlineData("Thief's Knife", "Artifact — Equipment",
        "Job select (When this Equipment enters, create a 1/1 colorless Hero creature token, then attach "
        + "this to it.)\nEquipped creature gets +1/+1, has \"Whenever this creature deals combat damage to "
        + "a player, draw a card,\" and is a Rogue in addition to its other types.\nEquip {4}")]
    // The monarchy is a card every turn with no draw written down, and it
    // outlives the permanent that handed it over.
    [InlineData("Coin of Fate", "Artifact",
        "When this artifact enters, surveil 1.\n"
        + "{3}{W}, {T}, Exile two creature cards from your graveyard, Sacrifice this artifact: An opponent "
        + "chooses one of the exiled cards. You put that card on the bottom of your library and return the "
        + "other to the battlefield tapped. You become the monarch.")]
    // One trigger word covering TWO events is two cards. The stun ability is a
    // different line and may not be billed for this draw.
    [InlineData("Cryogen Relic", "Artifact",
        "When this artifact enters or leaves the battlefield, draw a card.\n"
        + "{1}{U}, Sacrifice this artifact: Put a stun counter on up to one target tapped creature.")]
    // The count-first sentence, and a graveyard handed flashback wholesale.
    [InlineData("Mob Verdict", "Sorcery",
        "Secret council — Each player secretly votes for another player, then those votes are revealed. "
        + "For each vote an opponent received, Mob Verdict deals 2 damage to that player and each creature "
        + "that player controls. For each vote you received, draw a card.")]
    [InlineData("Iroh, Grand Lotus", "Legendary Creature — Human Noble Ally",
        "Firebending 2\nDuring your turn, each non-Lesson instant and sorcery card in your graveyard has "
        + "flashback. The flashback cost is equal to that card's mana cost.\n"
        + "During your turn, each Lesson card in your graveyard has flashback {1}.")]
    // A Saga chapter naming more than one number fires more than once. Any
    // number of numbers, and the draw need not say "you".
    [InlineData("Summon: Anima", "Enchantment Creature — Saga Horror",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\n"
        + "I, II, III — Pain — You draw a card and you lose 1 life.\n"
        + "IV — Oblivion — Each opponent sacrifices a creature of their choice and loses 3 life.\nMenace")]
    [InlineData("Summon: Leviathan", "Enchantment Creature — Saga Leviathan",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Return each creature that isn't a Kraken, Leviathan, Merfolk, Octopus, or Serpent to its "
        + "owner's hand.\n"
        + "II, III — Until end of turn, whenever a Kraken, Leviathan, Merfolk, Octopus, or Serpent attacks, "
        + "draw a card.\nWard {2}")]
    // One card off SOMEBODY ELSE'S top, cast from where it lies.
    [InlineData("Vaan, Street Thief", "Legendary Creature — Human Scout",
        "Whenever one or more Scouts, Pirates, and/or Rogues you control deal combat damage to a player, "
        + "exile the top card of that player's library. You may cast it. If you don't, create a Treasure "
        + "token.\nWhenever you cast a spell you don't own, put a +1/+1 counter on each Scout, Pirate, and "
        + "Rogue you control.")]
    public void Classify_TheSecondPassWordings_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // ONLY YOUR PART COUNTS (2026-11-07, "conta solo la tua parte"): Tataru
    // draws you one card for itself, Mog trades one for one. Until 2026-11-07
    // this read "parity across the table", the opponent's draw cancelling
    // yours; that reading is overturned, see The Legend of Yangchen in
    // Classify_TheNovember7CardAdvantage_OnlyYourPartCounts_IsCardAdvantage.
    [InlineData("Tataru Taru", "Legendary Creature — Dwarf Advisor",
        "When Tataru Taru enters, you draw a card and target opponent may draw a card.\n"
        + "Scions' Secretary — Whenever an opponent draws a card, if it isn't that player's turn, create a "
        + "tapped Treasure token. This ability triggers only once each turn.")]
    [InlineData("Mog, Moogle Warrior", "Legendary Creature — Moogle Warrior",
        "Lifelink\nDance — At the beginning of your end step, each player may discard a card. Each player "
        + "who discarded a card this way draws a card. If a creature card was discarded this way, you "
        + "create a 1/2 white Moogle creature token with lifelink.")]
    // The self-sacrifice trade only wins when the line hands nothing back:
    // Conch Horn puts one from your hand back on top, and is untagged.
    // All-Fates Scroll was here too, for its "X cards", until 2026-10-23: the
    // human had tagged it CardAdvantage on 2026-10-16, see
    // Classify_TheOctober23CardAdvantagePass_IsCardAdvantage.
    [InlineData("Conch Horn", "Artifact",
        "{1}, {T}, Sacrifice this artifact: Draw two cards, then put a card from your hand on top of your "
        + "library.")]
    // A quoted draw that is a LOOT is parity wherever it is printed.
    [InlineData("Ninja's Blades", "Artifact — Equipment",
        "Job select\nEquipped creature gets +1/+1, is a Ninja in addition to its other types, and has "
        + "\"Whenever this creature deals combat damage to a player, draw a card, then discard a card. "
        + "That player loses life equal to the discarded card's mana value.\"\nMutsunokami — Equip {2}")]
    // THE PILE IS THEIRS reaches their graveyard too: six reviewed cards exile
    // from a named opponent's graveyard and none is CardAdvantage.
    [InlineData("Hama, the Bloodbender", "Legendary Creature — Human Warlock",
        "When Hama enters, target opponent mills three cards. Exile up to one noncreature, nonland card "
        + "from that player's graveyard. For as long as you control Hama, you may cast the exiled card "
        + "during your turn by waterbending {X} rather than paying its mana cost, where X is its mana value.")]
    // A chapter that draws only when something else went its way is the
    // conditional rider this tag has no ruling on.
    [InlineData("The Tale of Tamiyo", "Legendary Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\n"
        + "I, II, III — Mill two cards. If two cards that share a card type were milled this way, draw a "
        + "card and repeat this process.\n"
        + "IV — Exile any number of target instant, sorcery, and/or Tamiyo planeswalker cards from your "
        + "graveyard. Copy them. You may cast any number of the copies.")]
    // "Exile the top card, you may play it" is the one-card impulse, which is a
    // rider — the second-hand rule has to say LOOK or REVEAL, never EXILE.
    [InlineData("Haste Magic", "Instant",
        "Target creature gets +3/+1 and gains haste until end of turn. Exile the top card of your library. "
        + "You may play it until your next end step.")]
    // A draw the card spends ITSELF on, written without a colon.
    [InlineData("Lim-Dûl's Paladin", "Creature — Human Knight",
        "Trample\nAt the beginning of your upkeep, you may discard a card. If you don't, sacrifice this "
        + "creature and draw a card.\n"
        + "Whenever this creature becomes blocked, it gets +6/+3 until end of turn.")]
    public void Classify_TheSecondPassNonWordings_AreNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-10-14 ("Tutti e due CA"): a draw bought by handing another
    // player a permanent of yours is a card. It overturns the 09-21 reading of
    // Stiltzkin, and Kain — who hands over HIMSELF, like Yes Man — follows.
    [InlineData("Stiltzkin, Moogle Merchant", "Legendary Creature — Moogle",
        "Lifelink\n{2}, {T}: Target opponent gains control of another target permanent you control. If they "
        + "do, you draw a card.")]
    [InlineData("Discerning Financier", "Creature — Human Noble",
        "At the beginning of your upkeep, if an opponent controls more lands than you, create a Treasure "
        + "token. (It's an artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")\n{2}{W}: "
        + "Choose another player. That player gains control of target Treasure you control. You draw a card.")]
    [InlineData("Kain, Traitorous Dragoon", "Legendary Creature — Human Knight",
        "Jump — During your turn, Kain has flying.\nWhenever Kain deals combat damage to a player, that "
        + "player gains control of Kain. If they do, you draw that many cards, create that many tapped "
        + "Treasure tokens, then lose that much life.")]
    [InlineData("Yes Man, Personal Securitron", "Legendary Artifact Creature — Robot",
        "{T}: Target opponent gains control of Yes Man. When they do, you draw two cards and put a quest "
        + "counter on Yes Man. Activate only during your turn.\nWild Card — When Yes Man leaves the "
        + "battlefield, its owner creates a tapped 1/1 white Soldier creature token for each quest counter on "
        + "it.")]
    public void Classify_ADrawPaidForWithAPermanentGivenAway_IsCardAdvantage(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The 2026-10-14 pass over the reviewed CardAdvantage misses.
    // A COPY of your own spell is a card when the spell can be anything
    // (Storm of Saruman, ruled "Dipende dal tipo copiato"), not when it is a
    // historic or colorless one — those are permanents as often as not, and a
    // copied permanent is a token (The Sixth Doctor, Echoes of Eternity).
    [InlineData("Storm of Saruman", "Enchantment",
        "Ward {3}\nWhenever you cast your second spell each turn, copy it, except the copy isn't legendary. "
        + "You may choose new targets for the copy. (A copy of a permanent spell becomes a token.)")]
    // The MONARCH given to any player, not only to you.
    [InlineData("Denethor, Stone Seer", "Legendary Creature — Human Noble",
        "When Denethor enters, scry 2.\n{3}{R}, {T}, Sacrifice Denethor: Target player becomes the monarch. "
        + "Denethor deals 3 damage to any target.")]
    [InlineData("Éomer, King of Rohan", "Legendary Creature — Human Noble",
        "Double strike\nÉomer enters with a +1/+1 counter on it for each other Human you control.\nWhen "
        + "Éomer enters, target player becomes the monarch. Éomer deals damage equal to its power to any "
        + "target.")]
    // A PILE out of your own library put into your hand — Fact or Fiction.
    [InlineData("Intrude on the Mind", "Instant",
        "Reveal the top five cards of your library and separate them into two piles. An opponent chooses one "
        + "of those piles. Put that pile into your hand and the other into your graveyard. Create a 0/0 "
        + "colorless Thopter artifact creature token with flying, then put a +1/+1 counter on it for each card "
        + "put into your graveyard this way.")]
    [InlineData("Curator of Destinies", "Creature — Sphinx",
        "This spell can't be countered.\nFlying\nWhen this creature enters, look at the top five cards of "
        + "your library and separate them into a face-down pile and a face-up pile. An opponent chooses one of "
        + "those piles. Put that pile into your hand and the other into your graveyard.")]
    [InlineData("Sauron's Ransom", "Instant",
        "Choose an opponent. They look at the top four cards of your library and separate them into a "
        + "face-down pile and a face-up pile. Put one pile into your hand and the other into your graveyard. "
        + "The Ring tempts you.")]
    // SEVERAL cards kept from one look (ruling 13: "take two" is a card on its
    // own), however the number is said.
    [InlineData("Reality Heist", "Instant",
        "Affinity for artifacts (This spell costs {1} less to cast for each artifact you control.)\nLook at "
        + "the top seven cards of your library. You may reveal up to two artifact cards from among them and "
        + "put them into your hand. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Pictures of Spider-Man", "Artifact",
        "When this artifact enters, look at the top five cards of your library. You may reveal up to two "
        + "creature cards from among them and put them into your hand. Put the rest on the bottom of your "
        + "library in a random order.\n{1}, {T}, Sacrifice this artifact: Create a Treasure token. (It's an "
        + "artifact with \"{T}, Sacrifice this token: Add one mana of any color.\")")]
    [InlineData("Sinuous Benthisaur", "Creature — Dinosaur",
        "When this creature enters, look at the top X cards of your library, where X is the number of Caves "
        + "you control plus the number of Cave cards in your graveyard. Put two of those cards into your hand "
        + "and the rest on the bottom of your library in a random order.")]
    [InlineData("Skyclave Plunder", "Sorcery",
        "Look at the top X cards of your library, where X is three plus the number of creatures in your "
        + "party. Put three of those cards into your hand and the rest on the bottom of your library in a "
        + "random order. (Your party consists of up to one each of Cleric, Rogue, Warrior, and Wizard.)")]
    [InlineData("Atraxa, Grand Unifier", "Legendary Creature — Phyrexian Angel",
        "Flying, vigilance, deathtouch, lifelink\nWhen Atraxa enters, reveal the top ten cards of your "
        + "library. For each card type, you may put a card of that type from among the revealed cards into "
        + "your hand. Put the rest on the bottom of your library in a random order. (Artifact, battle, "
        + "creature, enchantment, instant, land, planeswalker, and sorcery are card types.)")]
    [InlineData("Hurkyl, Master Wizard", "Legendary Creature — Human Wizard Advisor",
        "At the beginning of your end step, if you've cast a noncreature spell this turn, reveal the top "
        + "five cards of your library. For each card type among noncreature spells you've cast this turn, you "
        + "may put a card of that type from among the revealed cards into your hand. Put the rest on the "
        + "bottom of your library in a random order.")]
    // A loyalty ability that looks at two and keeps "the other" — Ashiok's +1
    // is a card every turn, which the one-shot look is not.
    [InlineData("Ashiok, Wicked Manipulator", "Legendary Planeswalker — Ashiok",
        "If you would pay life while your library has at least that many cards in it, exile that many cards "
        + "from the top of your library instead.\n+1: Look at the top two cards of your library. Exile one of "
        + "them and put the other into your hand.\n−2: Create two 1/1 black Nightmare creature tokens with "
        + "\"At the beginning of combat on your turn, if a card was put into exile this turn, put a +1/+1 "
        + "counter on this token.\"\n−7: Target player exiles the top X cards of their library, where X is the "
        + "total mana value of cards you own in exile.")]
    // SAGA chapters that repeat a card.
    [InlineData("An Unearthly Child", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II, "
        + "III — Reveal cards from the top of your library until you reveal a Doctor card, a card with "
        + "doctor's companion, or a Vehicle card. Put that card into your hand and the rest on the bottom of "
        + "your library in a random order.")]
    [InlineData("The Day of the Doctor", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI, II, III "
        + "— Exile cards from the top of your library until you exile a legendary card. You may play that card "
        + "for as long as this Saga remains on the battlefield. Put the rest of those exiled cards on the "
        + "bottom of your library in a random order.\nIV — Choose up to three Doctors. You may exile all other "
        + "creatures. If you do, this Saga deals 13 damage to you.")]
    [InlineData("The Bath Song", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Draw two cards, then discard a card.\nIII — Shuffle any number of target cards from your graveyard "
        + "into your library. Add {U}{U}.")]
    // JUNK made several at a time or again and again.
    [InlineData("Mister Gutsy", "Artifact Creature — Robot Soldier",
        "Whenever you cast an Aura or Equipment spell, put a +1/+1 counter on this creature.\nWhen this "
        + "creature dies, create X Junk tokens, where X is the number of +1/+1 counters on it. (They're "
        + "artifacts with \"{T}, Sacrifice this token: Exile the top card of your library. You may play that "
        + "card this turn. Activate only as a sorcery.\")")]
    [InlineData("Rose, Cutthroat Raider", "Legendary Artifact Creature — Robot",
        "First strike\nRaid — At end of combat on your turn, if you attacked this turn, create a Junk token "
        + "for each opponent you attacked. (It's an artifact with \"{T}, Sacrifice this token: Exile the top "
        + "card of your library. You may play that card this turn. Activate only as a sorcery.\")\nWhenever "
        + "you sacrifice a Junk, add {R}.")]
    // A DRAW MODE you may choose more than once.
    [InlineData("Riveteers Confluence", "Sorcery",
        "Choose three. You may choose the same mode more than once.\n• You draw a card and you lose 1 "
        + "life.\n• Riveteers Confluence deals 1 damage to each creature and planeswalker you don't "
        + "control.\n• You may put a land card from your hand or graveyard onto the battlefield tapped.")]
    [InlineData("Season of Weaving", "Sorcery",
        "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Draw a "
        + "card.\n{P}{P} — Choose an artifact or creature you control. Create a token that's a copy of "
        + "it.\n{P}{P}{P} — Return each nonland, nontoken permanent to its owner's hand.")]
    // REBOUND granted to your spells casts each of them twice.
    [InlineData("Ojer Pakpatiq, Deepest Epoch", "Legendary Creature — God",
        "Flying\nWhenever you cast an instant spell from your hand, it gains rebound. (Exile it as it "
        + "resolves. At the beginning of your next upkeep, you may cast it from exile without paying its mana "
        + "cost.)\nWhen Ojer Pakpatiq dies, return it to the battlefield tapped and transformed under its "
        + "owner's control with three time counters on it.")]
    // THAT MANY cards exiled from the top, and all of them yours to play.
    [InlineData("Virtue of Courage", "Enchantment",
        "Whenever a source you control deals noncombat damage to an opponent, you may exile that many cards "
        + "from the top of your library. You may play those cards this turn.\nEmbereth Blaze deals 2 damage to "
        + "any target. (Then exile this card. You may cast the enchantment later from exile.)")]
    // A card out of your graveyard cast again and again from a tap.
    [InlineData("Conduit of Worlds", "Artifact",
        "You may play lands from your graveyard.\n{T}: Choose target nonland permanent card in your "
        + "graveyard. If you haven't cast a spell this turn, you may cast that card. If you do, you can't cast "
        + "additional spells this turn. Activate only as a sorcery.")]
    // A recurring draw written where the trigger reading did not look: an
    // enters-AND-whenever, or a closing "draw a card" after a long trigger.
    [InlineData("Up the Beanstalk", "Enchantment",
        "When this enchantment enters and whenever you cast a spell with mana value 5 or greater, draw a "
        + "card.")]
    [InlineData("The Toymaker's Trap", "Enchantment",
        "At the beginning of your upkeep, secretly choose a number between 1 and 5 that hasn't been chosen. "
        + "If you do, an opponent guesses which number you chose, then you reveal the number you chose. If "
        + "they guessed wrong, they lose life equal to the number they guessed and you draw a card. If they "
        + "guessed right, sacrifice this enchantment.")]
    [InlineData("Erestor of the Council", "Legendary Creature — Elf Noble",
        "Whenever players finish voting, each opponent who voted for a choice you voted for creates a "
        + "Treasure token. You scry X, where X is the number of opponents who voted for a choice you didn't "
        + "vote for. Draw a card.")]
    // A discard that PAYS for an ability is not a rummage unless that ability
    // draws: Chivalric Alliance and Psychic Frog draw on another line.
    [InlineData("Chivalric Alliance", "Enchantment",
        "Whenever you attack with two or more creatures, draw a card.\n{2}, Discard a card: Create a 2/2 "
        + "white and blue Knight creature token with vigilance.")]
    [InlineData("Psychic Frog", "Creature — Frog",
        "Whenever this creature deals combat damage to a player or planeswalker, draw a card.\nDiscard a "
        + "card: Put a +1/+1 counter on this creature.\nExile three cards from your graveyard: This creature "
        + "gains flying until end of turn.")]
    // Sentinels for the guards below: a look that starts with WHENEVER is not
    // the one-shot Feral Encounter (Djeru and Hazoret), and a by-name self-
    // sacrifice that draws MORE than one is still a card (Hylda's Crown, ED-E).
    [InlineData("Djeru and Hazoret", "Legendary Creature — Human God",
        "As long as you have one or fewer cards in hand, Djeru and Hazoret has vigilance and "
        + "haste.\nWhenever Djeru and Hazoret attacks, look at the top six cards of your library. You may "
        + "exile a legendary creature card from among them. Put the rest on the bottom of your library in a "
        + "random order. Until end of turn, you may cast the exiled card without paying its mana cost.")]
    [InlineData("Hylda's Crown of Winter", "Legendary Artifact",
        "{1}, {T}: Tap target creature. This ability costs {1} less to activate during your turn.\n{3}, "
        + "Sacrifice Hylda's Crown of Winter: Draw a card for each tapped creature your opponents control.")]
    [InlineData("ED-E, Lonesome Eyebot", "Legendary Artifact Creature — Robot",
        "Flying\nED-E My Love — Whenever you attack, if the number of attacking creatures is greater than "
        + "the number of quest counters on ED-E, put a quest counter on it.\n{2}, Sacrifice ED-E: Draw a card, "
        + "then draw an additional card for each quest counter on ED-E.")]
    public void Classify_TheOctoberCardAdvantageGaps_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Copies of a historic or colorless spell — see Storm of Saruman above.
    [InlineData("Echoes of Eternity", "Kindred Enchantment — Eldrazi",
        "If a triggered ability of a colorless spell you control or another colorless permanent you control "
        + "triggers, that ability triggers an additional time.\nWhenever you cast a colorless spell, copy it. "
        + "You may choose new targets for the copy. (A copy of a permanent spell becomes a token.)")]
    [InlineData("The Sixth Doctor", "Legendary Creature — Time Lord Doctor",
        "Time Lord's Prerogative — Whenever you cast a historic spell, copy it, except the copy isn't "
        + "legendary. This ability triggers only once each turn. (Artifacts, legendaries, and Sagas are "
        + "historic. A copy of a permanent spell becomes a token.)")]
    // "That player shuffles, then draws" — THEIR draw, after the search that
    // emptied their hand of a name.
    [InlineData("Deadly Cover-Up", "Sorcery",
        "As an additional cost to cast this spell, you may collect evidence 6.\nDestroy all creatures. If "
        + "evidence was collected, exile a card from an opponent's graveyard. Then search its owner's "
        + "graveyard, hand, and library for any number of cards with that name and exile them. That player "
        + "shuffles, then draws a card for each card exiled from their hand this way.")]
    [InlineData("The End", "Instant",
        "This spell costs {2} less to cast if your life total is 5 or less.\nExile target creature or "
        + "planeswalker. Search its controller's graveyard, hand, and library for any number of cards with the "
        + "same name as that permanent and exile them. That player shuffles, then draws a card for each card "
        + "exiled from their hand this way.")]
    // A card TUTORED into exile and cast from there is a tutor, not a card.
    [InlineData("Beseech the Mirror", "Sorcery",
        "Bargain (You may sacrifice an artifact, enchantment, or token as you cast this spell.)\nSearch your "
        + "library for a card, exile it face down, then shuffle. If this spell was bargained, you may cast the "
        + "exiled card without paying its mana cost if that spell's mana value is 4 or less. Put the exiled "
        + "card into your hand if it wasn't cast this way.")]
    // A card that sacrifices ITSELF BY NAME for one card replaces itself —
    // the parity rule, written with the name where "this" usually goes.
    [InlineData("Five Hundred Year Diary", "Legendary Artifact — Book Clue",
        "Five Hundred Year Diary enters tapped.\n{T}: Add {U} for each Clue you control.\n{2}, Sacrifice "
        + "Five Hundred Year Diary: Draw a card.")]
    [InlineData("Stone of Erech", "Legendary Artifact",
        "If a creature an opponent controls would die, exile it instead.\n{2}, {T}, Sacrifice Stone of "
        + "Erech: Exile target player's graveyard. Draw a card.")]
    // Sentinels: a pile out of your GRAVEYARD is Regrowth (Split the Spoils);
    // one kept, two only when kicked "instead", is a Filter (Consult the Star
    // Charts); an enters-and-whenever that is not a draw of yours (Ivora's
    // Blood, Bowmasters' ping); a discard that pays for a rummage on its own
    // line (Invasion of Mercadia).
    [InlineData("Split the Spoils", "Sorcery",
        "Exile up to five target permanent cards from your graveyard and separate them into two piles. An "
        + "opponent chooses one of those piles. Put that pile into your hand and the other into your "
        + "graveyard. (Piles can be empty.)")]
    [InlineData("Consult the Star Charts", "Instant",
        "Kicker {1}{U} (You may pay an additional {1}{U} as you cast this spell.)\nLook at the top X cards "
        + "of your library, where X is the number of lands you control. Put one of those cards into your hand. "
        + "If this spell was kicked, put two of those cards into your hand instead. Put the rest on the bottom "
        + "of your library in a random order.")]
    [InlineData("Ivora, Insatiable Heir", "Legendary Creature — Vampire Warrior",
        "Trample\nWhen Ivora enters and whenever it deals combat damage to a player, create a Blood token. "
        + "(It's an artifact with \"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\nWhenever "
        + "you discard a card, put a +1/+1 counter on Ivora.")]
    [InlineData("Orcish Bowmasters", "Creature — Orc Archer",
        "Flash\nWhen this creature enters and whenever an opponent draws a card except the first one they "
        + "draw in each of their draw steps, this creature deals 1 damage to any target. Then amass Orcs 1.")]
    [InlineData("Invasion of Mercadia", "Battle — Siege",
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's "
        + "defeated, exile it, then cast it transformed.)\nWhen this Siege enters, you may discard a card. If "
        + "you do, draw two cards.\n{2}{R}, {T}, Discard a card: Create two 1/1 blue and red Elemental "
        + "creature tokens. Creatures you control get +1/+0 and gain haste until end of turn.")]
    public void Classify_TheOctoberCardAdvantageGaps_AreNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // CUMULATIVE UPKEEP paid in CARDS draws one more every turn it survives,
    // and it is written where a cost goes, so no rule was looking there.
    [InlineData("Psychic Vortex", "Enchantment",
        "Cumulative upkeep—Draw a card. (At the beginning of your upkeep, put an age counter on this "
        + "permanent, then sacrifice it unless you pay its upkeep cost for each age counter on it.)\n"
        + "At the beginning of your end step, sacrifice a land and discard your hand.")]
    // Symmetry is not parity when EVERYBODY gains — Howling Mine's reading,
    // given by name on 2026-09-22.
    [InlineData("Parker Luck", "Enchantment",
        "At the beginning of your end step, two target players each reveal the top card of their library. "
        + "They each lose life equal to the mana value of the card revealed by the other player. Then they "
        + "each put the card they revealed into their hand.")]
    // Several tokens that EACH draw: one card spent, two bought. "-1 +2 = +1".
    [InlineData("Niko, Light of Hope", "Legendary Creature — Human Wizard",
        "When Niko enters, create two Shard tokens. (They're enchantments with \"{2}, Sacrifice this "
        + "token: Scry 1, then draw a card.\")\n"
        + "{2}, {T}: Exile target nonlegendary creature you control. Shards you control become copies of "
        + "it until the next end step. Return it to the battlefield under its owner's control at the "
        + "beginning of the next end step.")]
    // WARP is a second cast, so a card that pays you on the way OUT pays twice.
    [InlineData("Anticausal Vestige", "Creature — Eldrazi",
        "When this creature leaves the battlefield, draw a card, then you may put a permanent card with "
        + "mana value less than or equal to the number of lands you control from your hand onto the "
        + "battlefield tapped.\n"
        + "Warp {4} (You may cast this card from your hand for its warp cost. Exile this creature at the "
        + "beginning of the next end step, then you may cast it from exile on a later turn.)")]
    // Copying your own spell is a second copy of a card you paid for once.
    [InlineData("Taigam, Master Opportunist", "Legendary Creature — Human Monk",
        "Flurry — Whenever you cast your second spell each turn, copy it, then exile the spell you cast "
        + "with four time counters on it. If it doesn't have suspend, it gains suspend.")]
    // Casting from the top of your library is the second hand, whatever you
    // pay for the card — ruled 2026-09-22, overturning the hand tag.
    [InlineData("Madame Web, Clairvoyant", "Legendary Creature — Mutant Advisor",
        "You may look at the top card of your library any time.\n"
        + "You may cast Spider spells and noncreature spells from the top of your library.\n"
        + "Whenever you attack, you may mill a card.")]
    public void Classify_TheThirdPassWordings_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The copy has to follow the trigger IMMEDIATELY. Mendicant Core puts Max
    // speed and a further {1} in front of its copy and carries no tag; Max
    // speed is NOT a blanket difficulty, since 34 reviewed cards print it and
    // 4 are tagged.
    [InlineData("Mendicant Core, Guidelight", "Legendary Artifact Creature — Robot",
        "Mendicant Core's power is equal to the number of artifacts you control.\n"
        + "Start your engines!\n"
        + "Max speed — Whenever you cast an artifact spell, you may pay {1}. If you do, copy it. "
        + "(The copy becomes a token.)")]
    // A trigger that NAMES its own difficulty, confirmed 2026-09-22: a CHOSEN
    // CARD NAME asks you to name a card and then meet it, that same turn.
    // Avatar Aang stood here beside it (bending as a difficulty) until
    // 2026-11-07, overturned ("Aang: CA"): its own firebending 2 meets the
    // trigger on every attack, see
    // Classify_TheNovember7CardAdvantage_TheDifficultyIsInsideTheCount_IsCardAdvantage.
    [InlineData("The Clone Saga", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Surveil 3.\n"
        + "II — When you next cast a creature spell this turn, copy it, except the copy isn't legendary.\n"
        + "III — Choose a card name. Whenever a creature with the chosen name deals combat damage to a "
        + "player this turn, draw a card.")]
    // An ability that TRANSFORMS the permanent the moment it succeeds runs
    // once. This is the whole difference between the Sidequest and Traveling
    // Botanist, which print the same sentence word for word.
    [InlineData("Sidequest: Catch a Fish", "Enchantment",
        "At the beginning of your upkeep, look at the top card of your library. If it's an artifact or "
        + "creature card, you may reveal it and put it into your hand. If you put a card into your hand "
        + "this way, create a Food token and transform this enchantment.")]
    // A discard charged as an ADDITIONAL COST, with the spell itself counted,
    // is parity however many the card draws: Grab the Prize's reading, applied
    // 2026-09-22 to the two cards that were hand-tagged the other way.
    [InlineData("Laughing Mad", "Instant",
        "As an additional cost to cast this spell, discard a card.\nDraw two cards.\n"
        + "Flashback {3}{R}")]
    [InlineData("Sazacap's Brew", "Instant",
        "Gift a tapped Fish\nAs an additional cost to cast this spell, discard a card.\n"
        + "Target player draws two cards. If the gift was promised, target creature you control gets "
        + "+2/+0 until end of turn.")]
    // Chakra Meditation stood here until 2026-11-06 ("a draw for one card handed
    // straight back is parity even when the discard is conditional"): a
    // conditional gain is both tags now, see
    // Classify_TheNovember6Filter_DrawThenDiscardUnless_IsCardAdvantageAndFilter.
    public void Classify_TheThirdPassNonWordings_AreNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // CardAdvantage pass 2026-10-16: the readings (C1-C3, M1-M18) and the cards
    // that keep the tag beside the removals.
    [InlineData("Dream-Thief's Bandana", "Artifact — Equipment",
        "Whenever equipped creature deals combat damage to a player, look at the top card of their library, "
        + "then exile it face down. For as long as it remains exiled, you may play it, and mana of any type can "
        + "be spent to cast that spell.\nEquip {1}")]
    [InlineData("Petty Larceny", "Sorcery",
        "Freerunning {1}{B} (You may cast this spell for its freerunning cost if you dealt combat damage to a "
        + "player this turn with an Assassin or commander.)\nLook at the top two cards of target opponent's "
        + "library and exile those cards face down. You may play those cards for as long as they remain exiled, "
        + "and mana of any type can be spent to cast them. Create a Treasure token.")]
    [InlineData("Rakdos, the Muscle", "Legendary Creature — Demon Mercenary",
        "Flying, trample\nWhenever you sacrifice another creature, exile cards equal to its mana value from "
        + "the top of target player's library. Until your next end step, you may play those cards, and mana of "
        + "any type can be spent to cast those spells.\nSacrifice another creature: Rakdos gains indestructible "
        + "until end of turn. Tap it. Activate only once each turn.")]
    [InlineData("Tinybones, Bauble Burglar", "Legendary Creature — Skeleton Rogue",
        "Whenever an opponent discards a card, exile it from their graveyard with a stash counter on "
        + "it.\nDuring your turn, you may play cards you don't own with stash counters on them from exile, and "
        + "mana of any type can be spent to cast those spells.\n{3}{B}, {T}: Each opponent discards a card. "
        + "Activate only as a sorcery.")]
    [InlineData("Bismuth Mindrender", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nMenace\nWhenever this creature deals combat damage to a player, that "
        + "player exiles cards from the top of their library until they exile a nonland card. You may cast that "
        + "card by paying life equal to the spell's mana value rather than paying its mana cost.")]
    [InlineData("Black Cat, Cunning Thief", "Legendary Creature — Human Rogue Villain",
        "When Black Cat enters, look at the top nine cards of target opponent's library, exile two of them "
        + "face down, then put the rest on the bottom of their library in a random order. You may play the "
        + "exiled cards for as long as they remain exiled. Mana of any type can be spent to cast spells this "
        + "way.")]
    [InlineData("Blightwing Bandit", "Creature — Faerie Rogue",
        "Flying, deathtouch\nWhenever you cast your first spell during each opponent's turn, look at the top "
        + "card of that player's library, then exile it face down. You may play that card for as long as it "
        + "remains exiled, and mana of any type can be spent to cast it.")]
    [InlineData("Edward Kenway", "Legendary Creature — Human Assassin Pirate",
        "At the beginning of your end step, create a Treasure token for each tapped Assassin, Pirate, and/or "
        + "Vehicle you control.\nWhenever a Vehicle you control deals combat damage to a player, look at the top "
        + "card of that player's library, then exile it face down. You may play that card for as long as it "
        + "remains exiled.")]
    [InlineData("Fire Lord Ozai", "Legendary Creature — Human Noble",
        "Whenever Fire Lord Ozai attacks, you may sacrifice another creature. If you do, add an amount of {R} "
        + "equal to the sacrificed creature's power. Until end of combat, you don't lose this mana as steps "
        + "end.\n{6}: Exile the top card of each opponent's library. Until end of turn, you may play one of "
        + "those cards without paying its mana cost.")]
    [InlineData("Gríma, Saruman's Footman", "Legendary Creature — Human Advisor",
        "Gríma can't be blocked.\nWhenever Gríma deals combat damage to a player, that player exiles cards "
        + "from the top of their library until they exile an instant or sorcery card. You may cast that card "
        + "without paying its mana cost. Then that player puts the exiled cards that weren't cast this way on "
        + "the bottom of their library in a random order.")]
    [InlineData("Reno and Rude", "Legendary Creature — Human Assassin",
        "Menace\nWhenever Reno and Rude deals combat damage to a player, exile the top card of that player's "
        + "library. Then you may sacrifice another creature or artifact. If you do, you may play the exiled "
        + "card this turn, and mana of any type can be spent to cast it.")]
    [InlineData("Rev, Tithe Extractor", "Legendary Creature — Human Rogue",
        "Whenever you attack, target creature gains deathtouch until end of turn.\nWhenever one or more "
        + "creatures you control deal combat damage to a player, create a Treasure token, then look at the top "
        + "card of that player's library and exile it face down. You may cast that card for as long as it "
        + "remains exiled. (A Treasure token is an artifact with \"{T}, Sacrifice this token: Add one mana of "
        + "any color.\")")]
    [InlineData("Saruman of Many Colors", "Legendary Creature — Avatar Wizard",
        "Ward—Discard an enchantment, instant, or sorcery card.\nWhenever you cast your second spell each "
        + "turn, each opponent mills two cards. When one or more cards are milled this way, exile target "
        + "enchantment, instant, or sorcery card with equal or lesser mana value than that spell from an "
        + "opponent's graveyard. Copy the exiled card. You may cast the copy without paying its mana cost.")]
    [InlineData("Strago and Relm", "Legendary Creature — Human Wizard",
        "Sketch and Lore — {2}{R}, {T}: Target opponent exiles cards from the top of their library until they "
        + "exile an instant, sorcery, or creature card. You may cast that card without paying its mana cost. If "
        + "you cast a creature spell this way, it gains haste and \"At the beginning of the end step, sacrifice "
        + "this creature.\" Activate only as a sorcery.")]
    [InlineData("Caesar, Legion's Emperor", "Legendary Creature — Human Soldier",
        "Whenever you attack, you may sacrifice another creature. When you do, choose two —\n• Create two 1/1 "
        + "red and white Soldier creature tokens with haste that are tapped and attacking.\n• You draw a card "
        + "and you lose 1 life.\n• Caesar deals damage equal to the number of creature tokens you control to "
        + "target opponent.")]
    [InlineData("Brainsurge", "Instant",
        "Draw four cards, then put two cards from your hand on top of your library in any order.")]
    [InlineData("Deduce", "Instant",
        "Draw a card. Investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this token: "
        + "Draw a card.\")")]
    [InlineData("Carnage Interpreter", "Creature — Devil Detective",
        "When this creature enters, discard your hand, then investigate four times. (To investigate, create a "
        + "Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")\nAs long as you have one "
        + "or fewer cards in hand, this creature gets +2/+2 and has menace.")]
    [InlineData("Connecting the Dots", "Enchantment",
        "Whenever a creature you control attacks, exile the top card of your library face down. (You can't "
        + "look at it.)\n{1}{R}, Discard your hand, Sacrifice this enchantment: Put all cards exiled with this "
        + "enchantment into their owners' hands.")]
    [InlineData("Expert-Level Safe", "Artifact",
        "When this artifact enters, exile the top two cards of your library face down.\n{1}, {T}: You and "
        + "target opponent each secretly choose 1, 2, or 3. Then those choices are revealed. If they match, "
        + "sacrifice this artifact and put all cards exiled with it into their owners' hands. Otherwise, exile "
        + "the top card of your library face down.")]
    [InlineData("Fireglass Mentor", "Creature — Lizard Warlock",
        "At the beginning of your second main phase, if an opponent lost life this turn, exile the top two "
        + "cards of your library. Choose one of them. Until end of turn, you may play that card.")]
    [InlineData("Fisher's Talent", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nAt the beginning of your upkeep, look at the "
        + "top card of your library. You may reveal it if it's a land card. Create a 1/1 blue Fish creature "
        + "token if you revealed it this way. Then draw a card.\n{G}{U}: Level 2\nIf you would create a Fish "
        + "token, create a 3/3 blue Shark creature token instead.\n{2}{G}{U}: Level 3\nIf you would create a "
        + "Shark token, create an 8/8 blue Octopus creature token instead.")]
    [InlineData("Hoarder's Overflow", "Enchantment",
        "When this enchantment enters and whenever you expend 4, put a stash counter on it. (You expend 4 as "
        + "you spend your fourth total mana to cast spells during a turn.)\n{1}{R}, Sacrifice this enchantment: "
        + "Discard your hand, then draw cards equal to the number of stash counters on this enchantment.")]
    [InlineData("Selfcraft Mechan", "Artifact Creature — Robot Artificer",
        "When this creature enters, you may sacrifice an artifact. When you do, put a +1/+1 counter on target "
        + "creature and draw a card.")]
    [InlineData("Shadow of the Second Sun", "Enchantment — Aura",
        "Enchant player\nAt the beginning of each of enchanted player's postcombat main phases, there is an "
        + "additional beginning phase after this phase. (The end step happens after the added untap, upkeep, "
        + "and draw steps.)")]
    [InlineData("Leyline Dowser", "Artifact",
        "{1}, {T}: Mill a card. You may put an instant or sorcery card milled this way into your hand.\nTap an "
        + "untapped legendary creature you control: Untap this artifact.")]
    [InlineData("Nashi, Searcher in the Dark", "Legendary Creature — Rat Ninja Wizard",
        "Menace\nWhenever Nashi deals combat damage to a player, you mill that many cards. You may put any "
        + "number of legendary and/or enchantment cards from among them into your hand. If you put no cards "
        + "into your hand this way, put a +1/+1 counter on Nashi.")]
    [InlineData("Ripples of Undeath", "Enchantment",
        "At the beginning of your first main phase, mill three cards. Then you may pay {1} and 3 life. If you "
        + "do, put a card from among those cards into your hand.")]
    [InlineData("Greenhouse // Rickety Gazebo", "Enchantment — Room // Enchantment — Room",
        "Lands you control have \"{T}: Add one mana of any color.\"\n(You may cast either half. That door "
        + "unlocks on the battlefield. As a sorcery, you may pay the mana cost of a locked door to unlock "
        + "it.)\nWhen you unlock this door, mill four cards, then return up to two permanent cards from among "
        + "them to your hand.\n(You may cast either half. That door unlocks on the battlefield. As a sorcery, "
        + "you may pay the mana cost of a locked door to unlock it.)")]
    [InlineData("Sparring Dummy", "Artifact Creature — Scarecrow",
        "Defender\n{T}: Mill a card. You may put a land card milled this way into your hand. You gain 2 life "
        + "if a Lesson card is milled this way. (To mill a card, put the top card of your library into your "
        + "graveyard.)")]
    [InlineData("Songbirds' Blessing", "Enchantment — Aura",
        "Enchant creature\nWhenever enchanted creature attacks, reveal cards from the top of your library "
        + "until you reveal an Aura card. You may put that card onto the battlefield. If you don't, put it into "
        + "your hand. Put the rest on the bottom of your library in a random order.")]
    [InlineData("The Seventh Doctor", "Legendary Creature — Time Lord Doctor",
        "Whenever The Seventh Doctor attacks, choose a card in your hand. Defending player guesses whether "
        + "that card's mana value is greater than the number of artifacts you control. If they guessed wrong, "
        + "you may cast it without paying its mana cost. If you don't cast a spell this way, investigate. "
        + "(Create a Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    [InlineData("Bigger on the Inside", "Enchantment — Aura",
        "Enchant artifact or land\nEnchanted permanent has \"{T}: Target player adds two mana of any one color. "
        + "The next spell they cast this turn has cascade.\" (When they cast their next spell, they exile cards "
        + "from the top of their library until they exile a nonland card that costs less. They may cast it "
        + "without paying its mana cost. They put the exiled cards on the bottom in a random order.)")]
    [InlineData("Follow the Bodies", "Sorcery",
        "Gravestorm (When you cast this spell, copy it for each permanent put into a graveyard from the "
        + "battlefield this turn.)\nInvestigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice "
        + "this token: Draw a card.\")")]
    [InlineData("Expedited Inheritance", "Enchantment",
        "Whenever a creature is dealt damage, its controller may exile that many cards from the top of their "
        + "library. They may play those cards until the end of their next turn.")]
    [InlineData("Éowyn, Shieldmaiden", "Legendary Creature — Human Knight",
        "First strike\nAt the beginning of combat on your turn, if another Human entered the battlefield under "
        + "your control this turn, create two 2/2 red Human Knight creature tokens with trample and haste. Then "
        + "if you control six or more Humans, draw a card.")]
    [InlineData("Galadriel, Elven-Queen", "Legendary Creature — Elf Noble",
        "Will of the council — At the beginning of combat on your turn, if another Elf entered the "
        + "battlefield under your control this turn, starting with you, each player votes for dominion or "
        + "guidance. If dominion gets more votes, the Ring tempts you, then you put a +1/+1 counter on your "
        + "Ring-bearer. If guidance gets more votes or the vote is tied, draw a card.")]
    [InlineData("Arcane Bombardment", "Enchantment",
        "Whenever you cast your first instant or sorcery spell each turn, exile an instant or sorcery card at "
        + "random from your graveyard. Then copy each card exiled with this enchantment. You may cast any "
        + "number of the copies without paying their mana costs.")]
    [InlineData("Tarrian's Journal // The Tomb of Aclazotz", "Legendary Artifact — Book // Legendary Land — Cave",
        "{T}, Sacrifice another artifact or creature: Draw a card. Activate only as a sorcery.\n{2}, {T}, "
        + "Discard your hand: Transform Tarrian's Journal.\n(Transforms from Tarrian's Journal.)\n{T}: Add "
        + "{B}.\n{T}: You may cast a creature spell from your graveyard this turn. If you do, it enters with a "
        + "finality counter on it and is a Vampire in addition to its other types. (If a creature with a "
        + "finality counter on it would die, exile it instead.)")]
    [InlineData("Chandra, Flameshaper", "Legendary Planeswalker — Chandra",
        "+2: Add {R}{R}{R}. Exile the top three cards of your library. Choose one. You may play that card "
        + "this turn.\n+1: Create a token that's a copy of target creature you control, except it has haste and "
        + "\"At the beginning of the end step, sacrifice this token.\"\n−4: Chandra deals 8 damage divided as you "
        + "choose among any number of target creatures and/or planeswalkers.")]
    [InlineData("Jaya, Fiery Negotiator", "Legendary Planeswalker — Jaya",
        "+1: Create a 1/1 red Monk creature token with prowess.\n−1: Exile the top two cards of your library. "
        + "Choose one of them. You may play that card this turn.\n−2: Choose target creature an opponent "
        + "controls. Whenever you attack this turn, Jaya deals damage equal to the number of attacking "
        + "creatures to that creature.\n−8: You get an emblem with \"Whenever you cast a red instant or sorcery "
        + "spell, copy it twice. You may choose new targets for the copies.\"")]
    [InlineData("Bomat Courier", "Artifact Creature — Construct",
        "Haste\nWhenever this creature attacks, exile the top card of your library face down. (You can't look "
        + "at it.)\n{R}, Discard your hand, Sacrifice this creature: Put all cards exiled with this creature "
        + "into their owners' hands.")]
    [InlineData("Jace, the Mind Sculptor", "Legendary Planeswalker — Jace",
        "+2: Look at the top card of target player's library. You may put that card on the bottom of that "
        + "player's library.\n0: Draw three cards, then put two cards from your hand on top of your library in "
        + "any order.\n−1: Return target creature to its owner's hand.\n−12: Exile all cards from target player's "
        + "library, then that player shuffles their hand into their library.")]
    [InlineData("Survivor of the Unseen", "Creature — Human Wizard",
        "Cumulative upkeep {2} (At the beginning of your upkeep, put an age counter on this permanent, then "
        + "sacrifice it unless you pay its upkeep cost for each age counter on it.)\n{T}: Draw two cards, then "
        + "put a card from your hand on top of your library.")]
    [InlineData("Vivien on the Hunt", "Legendary Planeswalker — Vivien",
        "+2: You may sacrifice a creature. If you do, search your library for a creature card with mana value "
        + "equal to 1 plus the sacrificed creature's mana value, put it onto the battlefield, then shuffle.\n+1: "
        + "Mill five cards, then put any number of creature cards milled this way into your hand.\n−1: Create a "
        + "4/4 green Rhino Warrior creature token.")]
    [InlineData("Surrak, Elusive Hunter", "Legendary Creature — Human Warrior",
        "This spell can't be countered.\nTrample\nWhenever a creature you control or a creature spell you "
        + "control becomes the target of a spell or ability an opponent controls, draw a card.")]
    [InlineData("Elrond, Master of Healing", "Legendary Creature — Elf Noble",
        "Whenever you scry, put a +1/+1 counter on each of up to X target creatures, where X is the number of "
        + "cards looked at while scrying this way.\nWhenever a creature you control with a +1/+1 counter on it "
        + "becomes the target of a spell or ability an opponent controls, you may draw a card.")]
    [InlineData("Lazav, Wearer of Faces", "Legendary Creature — Shapeshifter Detective",
        "Whenever Lazav attacks, exile target card from a graveyard, then investigate. (Create a Clue token. "
        + "It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")\nWhenever you sacrifice a Clue, you "
        + "may have Lazav become a copy of a creature card exiled with it until end of turn.")]
    [InlineData("Piper Wright, Publick Reporter", "Legendary Creature — Human Detective",
        "Whenever Piper Wright deals combat damage to a player, investigate that many times. (To investigate, "
        + "create a Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")\nWhenever you "
        + "sacrifice a Clue, put a +1/+1 counter on target creature you control.")]
    [InlineData("Alquist Proft, Master Sleuth", "Legendary Creature — Human Detective",
        "Vigilance\nWhen Alquist Proft enters, investigate. (Create a Clue token. It's an artifact with \"{2}, "
        + "Sacrifice this token: Draw a card.\")\n{X}{W}{U}{U}, {T}, Sacrifice a Clue: You draw X cards and gain "
        + "X life.")]
    [InlineData("Lonis, Cryptozoologist", "Legendary Creature — Snake Elf Scout",
        "Whenever another nontoken creature you control enters, investigate.\n{T}, Sacrifice X Clues: Target "
        + "opponent reveals the top X cards of their library. You may put a nonland permanent card with mana "
        + "value X or less from among them onto the battlefield under your control. That player puts the rest "
        + "on the bottom of their library in a random order.")]
    [InlineData("Unshakable Tail", "Creature — Zombie Detective",
        "When this creature enters and at the beginning of your upkeep, surveil 1.\nWhenever one or more "
        + "creature cards are put into your graveyard from your library, investigate. (Create a Clue token. "
        + "It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")\n{2}, Sacrifice a Clue: Return this "
        + "card from your graveyard to your hand.")]
    [InlineData("Duggan, Private Detective", "Legendary Creature — Human Detective",
        "Duggan's power and toughness are each equal to the number of cards in your hand.\nWhenever Duggan "
        + "enters or attacks, investigate.\nThe Most Important Punch in History — {1}{G}, {T}: Duggan deals "
        + "damage equal to twice its power to another target creature. Activate only once.")]
    [InlineData("Kaito, Bane of Nightmares", "Legendary Planeswalker — Kaito",
        "Ninjutsu {1}{U}{B} ({1}{U}{B}, Return an unblocked attacker you control to hand: Put this card onto "
        + "the battlefield from your hand tapped and attacking.)\nDuring your turn, as long as Kaito has one or "
        + "more loyalty counters on him, he's a 3/4 Ninja creature and has hexproof.\n+1: You get an emblem with "
        + "\"Ninjas you control get +1/+1.\"\n0: Surveil 2. Then draw a card for each opponent who lost life this "
        + "turn.\n−2: Tap target creature. Put two stun counters on it.")]
    [InlineData("Glowcap Lantern", "Artifact — Equipment",
        "Equipped creature has \"You may look at the top card of your library any time\" and \"Whenever this "
        + "creature attacks, it explores.\" (Reveal the top card of your library. Put that card into your hand "
        + "if it's a land. Otherwise, put a +1/+1 counter on that creature, then put the card back or put it "
        + "into your graveyard.)\nEquip {2}")]
    public void Classify_TheOctober16CardAdvantagePassGains_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // CardAdvantage pass 2026-10-16: V1-V12 and C6 removals, plus the one-shots
    // every reading must leave out.
    [InlineData("Altanak, the Thrice-Called", "Legendary Creature — Insect Beast",
        "Trample\nWhenever Altanak becomes the target of a spell or ability an opponent controls, draw a "
        + "card.\n{1}{G}, Discard this card: Return target land card from your graveyard to the battlefield "
        + "tapped.")]
    [InlineData("Cactarantula", "Creature — Plant Spider",
        "This spell costs {1} less to cast if you control a Desert.\nReach\nWhenever this creature becomes the "
        + "target of a spell or ability an opponent controls, you may draw a card.")]
    [InlineData("Nadu, Winged Wisdom", "Legendary Creature — Bird Wizard",
        "Flying\nCreatures you control have \"Whenever this creature becomes the target of a spell or ability, "
        + "reveal the top card of your library. If it's a land card, put it onto the battlefield. Otherwise, "
        + "put it into your hand. This ability triggers only twice each turn.\"")]
    [InlineData("Red Death, Shipwrecker", "Legendary Creature — Crab Mutant",
        "Alluring Eyes — {T}: Goad target creature an opponent controls. That player draws a card. You add "
        + "{R}. (Until your next turn, that creature attacks each combat if able and attacks a player other "
        + "than you if able.)")]
    [InlineData("Smirking Spelljacker", "Creature — Djinn Wizard Rogue",
        "Flash\nFlying\nWhen this creature enters, exile target spell an opponent controls.\nWhenever this "
        + "creature attacks, if a card is exiled with it, you may cast the exiled card without paying its mana "
        + "cost.")]
    [InlineData("Thought Shucker", "Creature — Rat Rogue",
        "Threshold — {1}{U}: Put a +1/+1 counter on this creature and draw a card. Activate only if there are "
        + "seven or more cards in your graveyard and only once.")]
    [InlineData("Vesuvan Drifter", "Creature — Shapeshifter",
        "Flying\nYou may look at the top card of your library any time.\nAt the beginning of each combat, you "
        + "may reveal the top card of your library. If you reveal a creature card this way, this creature "
        + "becomes a copy of that card until end of turn, except it has flying.")]
    [InlineData("Vohar, Vodalian Desecrator", "Legendary Creature — Phyrexian Merfolk Wizard",
        "{T}: Draw a card, then discard a card. If you discarded an instant or sorcery card this way, each "
        + "opponent loses 1 life and you gain 1 life.\n{2}, Sacrifice Vohar: You may cast target instant or "
        + "sorcery card from your graveyard this turn. If that spell would be put into your graveyard, exile it "
        + "instead. Activate only as a sorcery.")]
    // Osteomancer Adept and The Grim Captain's Locker stood here (the cards
    // they charge from the graveyard spent the gain) until 2026-11-07,
    // overturned ("Tutti CA"): a graveyard card paid is no card of the hand, see
    // Classify_TheNovember7CardAdvantage_AGraveyardCastEveryTurn_IsCardAdvantage.
    [InlineData("Waltz of Rage", "Sorcery",
        "Target creature you control deals damage equal to its power to each other creature. Until end of "
        + "turn, whenever a creature you control dies, exile the top card of your library. You may play it "
        + "until the end of your next turn.")]
    [InlineData("Norin, Swift Survivalist", "Legendary Creature — Human Coward",
        "Norin can't block.\nWhenever a creature you control becomes blocked, you may exile it. You may play "
        + "that card from exile this turn.")]
    [InlineData("Refurbished Familiar", "Artifact Creature — Zombie Rat",
        "Affinity for artifacts (This spell costs {1} less to cast for each artifact you "
        + "control.)\nFlying\nWhen this creature enters, each opponent discards a card. For each opponent who "
        + "can't, you draw a card.")]
    [InlineData("The Beast, Deathless Prince", "Legendary Creature — Demon",
        "When you cast this spell, gain control of target creature until end of turn. Untap it. It gains "
        + "menace and haste until end of turn.\nThe Beast enters tapped with six stun counters on it. (If it "
        + "would become untapped, remove a stun counter from it instead.)\nWhenever a creature deals combat "
        + "damage to its owner, untap The Beast and draw a card.")]
    [InlineData("The Fugitive Doctor", "Legendary Creature — Time Lord Doctor",
        "When The Fugitive Doctor enters, investigate.\nWhenever The Fugitive Doctor attacks, you may "
        + "sacrifice a Clue. When you do, target instant or sorcery card in your graveyard gains flashback "
        + "{2}{R}{G} until end of turn. (You may cast that card from your graveyard for its flashback cost. "
        + "Then exile it.)")]
    [InlineData("Zul Ashur, Lich Lord", "Legendary Creature — Zombie Warlock",
        "Ward—Pay 2 life. (Whenever this creature becomes the target of a spell or ability an opponent "
        + "controls, counter it unless that player pays 2 life.)\n{T}: You may cast target Zombie creature card "
        + "from your graveyard this turn.")]
    [InlineData("Solemn Doomguide", "Creature — Tiefling Cleric",
        "Flying\nEach creature card in your graveyard that's a Cleric, Rogue, Warrior, and/or Wizard has "
        + "unearth {1}{B}. ({1}{B}: Return the card to the battlefield. The creature gains haste. Exile it at "
        + "the beginning of the next end step or if it would leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Deeproot Historian", "Creature — Merfolk Druid",
        "Merfolk and Druid cards in your graveyard have retrace. (You may cast cards with retrace from your "
        + "graveyard by discarding a land card in addition to paying their other costs.)")]
    [InlineData("Ensnared by the Mara", "Sorcery",
        "Each opponent faces a villainous choice — They exile cards from the top of their library until they "
        + "exile a nonland card, then you may cast that card without paying its mana cost, or that player "
        + "exiles the top four cards of their library and Ensnared by the Mara deals damage equal to the total "
        + "mana value of those exiled cards to that player.")]
    [InlineData("Grinning Totem", "Artifact",
        "{2}, {T}, Sacrifice this artifact: Search target opponent's library for a card and exile it. Then "
        + "that player shuffles. Until the beginning of your next upkeep, you may play that card. At the "
        + "beginning of your next upkeep, if you haven't played it, put it into its owner's graveyard.")]
    [InlineData("Synthesis Pod", "Artifact",
        "({U/P} can be paid with either {U} or 2 life.)\n{1}{U/P}, {T}, Exile a spell you control: Target "
        + "opponent reveals cards from the top of their library until they reveal a card with mana value equal "
        + "to 1 plus the exiled spell's mana value. Exile that card, then that player shuffles. You may cast "
        + "that exiled card without paying its mana cost.")]
    [InlineData("Crabomination", "Creature — Crab Demon",
        "Emerge from artifact {5}{B}{B} (You may cast this spell by sacrificing an artifact and paying the "
        + "emerge cost reduced by that artifact's mana value.)\nWhen this creature enters, target opponent "
        + "exiles the top card of their library, a card at random from their graveyard, and a card at random "
        + "from their hand. You may cast a spell from among cards exiled this way without paying its mana cost.")]
    [InlineData("Azula, Cunning Usurper", "Legendary Creature — Human Noble Rogue",
        "Firebending 2 (Whenever this creature attacks, add {R}{R}. This mana lasts until end of "
        + "combat.)\nWhen Azula enters, target opponent exiles a nontoken creature they control, then they exile "
        + "a nonland card from their graveyard.\nDuring your turn, you may cast cards exiled with Azula and you "
        + "may cast them as though they had flash. Mana of any type can be spent to cast those spells.")]
    [InlineData("Ashen Powder", "Sorcery",
        "Put target creature card from an opponent's graveyard onto the battlefield under your control.")]
    [InlineData("Fire Nation Salvagers", "Creature — Human Soldier",
        "Menace\nWhen this creature enters, put a +1/+1 counter on target creature or Vehicle you "
        + "control.\nWhenever one or more creatures you control with counters on them deal combat damage to a "
        + "player, put target creature or Vehicle card from that player's graveyard onto the battlefield under "
        + "your control.")]
    [InlineData("Riverwheel Sweep", "Sorcery",
        "Tap target creature. Put three stun counters on it. (If a permanent with a stun counter would become "
        + "untapped, remove one from it instead.)\nExile the top two cards of your library. Choose one of them. "
        + "Until the end of your next turn, you may play that card.")]
    [InlineData("Heroes' Hangout", "Sorcery",
        "Choose one —\n• Date Night — Exile the top two cards of your library. Choose one of them. Until the "
        + "end of your next turn, you may play that card.\n• Patrol Night — One or two target creatures each get "
        + "+1/+0 and gain first strike until end of turn.")]
    [InlineData("Case of the Burning Masks", "Enchantment — Case",
        "When this Case enters, it deals 3 damage to target creature an opponent controls.\nTo solve — Three "
        + "or more sources you controlled dealt damage this turn. (If unsolved, solve at the beginning of your "
        + "end step.)\nSolved — Sacrifice this Case: Exile the top three cards of your library. Choose one of "
        + "them. You may play that card this turn.")]
    [InlineData("Mishra's Research Desk", "Artifact",
        "{1}, {T}, Sacrifice this artifact: Exile the top two cards of your library. Choose one of them. "
        + "Until the end of your next turn, you may play that card.\nUnearth {1}{R} ({1}{R}: Return this card "
        + "from your graveyard to the battlefield. Exile it at the beginning of the next end step or if it "
        + "would leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Strongbox Raider", "Creature — Orc Pirate",
        "Raid — When this creature enters, if you attacked this turn, exile the top two cards of your "
        + "library. Choose one of them. Until the end of your next turn, you may play that card.")]
    [InlineData("Tectonic Giant", "Creature — Elemental Giant",
        "Whenever this creature attacks or becomes the target of a spell an opponent controls, choose one —\n• "
        + "This creature deals 3 damage to each opponent.\n• Exile the top two cards of your library. Choose one "
        + "of them. Until the end of your next turn, you may play that card.")]
    [InlineData("Party Thrasher", "Creature — Lizard Wizard",
        "Noncreature spells you cast from exile have convoke. (Each creature you tap while casting a "
        + "noncreature spell from exile pays for {1} or one mana of that creature's color.)\nAt the beginning of "
        + "your first main phase, you may discard a card. If you do, exile the top two cards of your library, "
        + "then choose one of them. You may play that card this turn.")]
    [InlineData("Magmatic Channeler", "Creature — Human Wizard",
        "As long as there are four or more instant and/or sorcery cards in your graveyard, this creature gets "
        + "+3/+1.\n{T}, Discard a card: Exile the top two cards of your library, then choose one of them. You "
        + "may play that card this turn.")]
    [InlineData("Cache Grab", "Instant",
        "Mill four cards. You may put a permanent card from among the cards milled this way into your hand. "
        + "If you control a Squirrel or returned a Squirrel card to your hand this way, create a Food token. "
        + "(To mill four cards, put the top four cards of your library into your graveyard. A Food token is an "
        + "artifact with \"{2}, {T}, Sacrifice this token: You gain 3 life.\")")]
    [InlineData("Dredger's Insight", "Enchantment",
        "Whenever one or more artifact and/or creature cards leave your graveyard, you gain 1 life.\nWhen this "
        + "enchantment enters, mill four cards. You may put an artifact, creature, or land card from among the "
        + "milled cards into your hand. (To mill four cards, put the top four cards of your library into your "
        + "graveyard.)")]
    [InlineData("Town Greeter", "Creature — Human Citizen",
        "When this creature enters, mill four cards. You may put a land card from among them into your hand. "
        + "If you put a Town card into your hand this way, you gain 2 life. (To mill four cards, a player puts "
        + "the top four cards of their library into their graveyard.)")]
    [InlineData("Tomakul Scrapsmith", "Creature — Human Artificer",
        "When this creature enters, mill three cards. You may put an artifact card from among the cards "
        + "milled this way into your hand. If you don't, put a +1/+1 counter on this creature. (To mill a card, "
        + "put the top card of your library into your graveyard.)")]
    [InlineData("Ainok Wayfarer", "Creature — Dog Scout",
        "When this creature enters, mill three cards. You may put a land card from among them into your hand. "
        + "If you don't, put a +1/+1 counter on this creature. (To mill three cards, put the top three cards of "
        + "your library into your graveyard.)")]
    [InlineData("Rosheen, Roaring Prophet", "Legendary Creature — Giant Shaman",
        "When Rosheen enters, mill six cards. You may put a card with {X} in its mana cost from among them "
        + "into your hand.\n{T}: Reveal any number of cards with {X} in their mana cost in your hand. Add {C}{C} "
        + "for each card revealed this way. Spend this mana only on costs that contain {X}.")]
    [InlineData("Picklock Prankster // Free the Fae", "Creature — Faerie Rogue // Instant — Adventure",
        "Flying, vigilance\nMill four cards. Then put an instant, sorcery, or Faerie card from among the "
        + "milled cards into your hand.")]
    [InlineData("Scout the City", "Sorcery",
        "Choose one —\n• Look Around — Mill three cards. You may put a permanent card from among them into "
        + "your hand. You gain 3 life. (To mill three cards, put the top three cards of your library into your "
        + "graveyard.)\n• Bring Down — Destroy target creature with flying.")]
    [InlineData("Wrenn and Realmbreaker", "Legendary Planeswalker — Wrenn",
        "Lands you control have \"{T}: Add one mana of any color.\"\n+1: Up to one target land you control "
        + "becomes a 3/3 Elemental creature with vigilance, hexproof, and haste until your next turn. It's "
        + "still a land.\n−2: Mill three cards. You may put a permanent card from among the milled cards into "
        + "your hand.\n−7: You get an emblem with \"You may play lands and cast permanent spells from your "
        + "graveyard.\"")]
    [InlineData("Spinner of Souls", "Creature — Spider Spirit",
        "Reach\nWhenever another nontoken creature you control dies, you may reveal cards from the top of your "
        + "library until you reveal a creature card. Put that card into your hand and the rest on the bottom of "
        + "your library in a random order.")]
    [InlineData("Nissa, Resurgent Animist", "Legendary Creature — Elf Scout",
        "Landfall — Whenever a land you control enters, add one mana of any color. Then if this is the second "
        + "time this ability has resolved this turn, reveal cards from the top of your library until you reveal "
        + "an Elf or Elemental card. Put that card into your hand and the rest on the bottom of your library in "
        + "a random order.")]
    [InlineData("House Cartographer", "Creature — Human Scout Survivor",
        "Survival — At the beginning of your second main phase, if this creature is tapped, reveal cards from "
        + "the top of your library until you reveal a land card. Put that card into your hand and the rest on "
        + "the bottom of your library in a random order.")]
    [InlineData("Sacred Guide", "Creature — Human Cleric",
        "{1}{W}, Sacrifice this creature: Reveal cards from the top of your library until you reveal a white "
        + "card. Put that card into your hand and exile all other cards revealed this way.")]
    [InlineData("Yuna's Whistle", "Instant",
        "Reveal cards from the top of your library until you reveal a creature card. Put that card into your "
        + "hand and the rest on the bottom of your library in a random order. When you reveal a creature card "
        + "this way, put X +1/+1 counters on target creature you control, where X is the mana value of that "
        + "card.")]
    [InlineData("Demonic Consultation", "Instant",
        "Choose a card name. Exile the top six cards of your library, then reveal cards from the top of your "
        + "library until you reveal a card with the chosen name. Put that card into your hand and exile all "
        + "other cards revealed this way.")]
    [InlineData("Smoldering Stagecoach", "Artifact — Vehicle",
        "Smoldering Stagecoach's power is equal to the number of instant and sorcery cards in your "
        + "graveyard.\nWhenever this Vehicle attacks, the next instant spell and the next sorcery spell you cast "
        + "this turn each have cascade.\nCrew 2")]
    [InlineData("TARDIS", "Artifact — Vehicle",
        "Flying\nWhenever this Vehicle attacks, if you control a Time Lord, the next spell you cast this turn "
        + "has cascade and you may planeswalk.\nCrew 2 (Tap any number of creatures you control with total power "
        + "2 or more: This Vehicle becomes an artifact creature until end of turn.)")]
    [InlineData("Oil-Gorger Troll", "Creature — Phyrexian Troll Warrior",
        "When this creature enters, you gain 3 life. Then if you control a permanent with an oil counter on "
        + "it, draw a card.")]
    [InlineData("Dream Cache", "Sorcery",
        "Draw three cards, then put two cards from your hand both on top of your library or both on the "
        + "bottom of your library.")]
    [InlineData("Hidetsugu and Kairi", "Legendary Creature — Ogre Demon Dragon",
        "Flying\nWhen Hidetsugu and Kairi enters, draw three cards, then put two cards from your hand on top "
        + "of your library in any order.\nWhen Hidetsugu and Kairi dies, exile the top card of your library. "
        + "Target opponent loses life equal to its mana value. If it's an instant or sorcery card, you may cast "
        + "it without paying its mana cost.")]
    // C2 is a REPEAT: a card out of an opponent's graveyard cast ONCE, on
    // entering, stays Steal alone. Without the repeat gate in
    // RepeatsWithoutPayingACard this one gains CardAdvantage (measured
    // 2026-10-16).
    [InlineData("Dire Fleet Daredevil", "Creature — Human Pirate",
        "First strike\nWhen this creature enters, exile target instant or sorcery card from an opponent's "
        + "graveyard. You may cast it this turn, and mana of any type can be spent to cast that spell. If that "
        + "spell would be put into a graveyard, exile it instead.")]
    public void Classify_TheOctober16CardAdvantagePassRemovals_AreNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // What stopped being Cheat on 2026-11-03 is a CARD ('1. CA', '2. CA', '3. CA
    // per le multiple', '4. CA se si ripetono'), in the shapes this tag never read
    // because Cheat had them. A PILE off the top cast free (FreePileOffTheTop):
    // Dance with Calamity, Plargg and Nassari and Wand of Wonder, named (Wand
    // pinned the other way from 2026-10-16, moved here), and Fevered Suspicion.
    [InlineData("Dance with Calamity", "Sorcery",
        "Shuffle your library. As many times as you choose, you may exile the top card of your library. If "
        + "the total mana value of the cards exiled this way is 13 or less, you may cast any number of spells "
        + "from among those cards without paying their mana costs.")]
    [InlineData("Plargg and Nassari", "Legendary Creature — Orc Efreet",
        "At the beginning of your upkeep, each player exiles cards from the top of their library until they "
        + "exile a nonland card. An opponent chooses a nonland card exiled this way. You may cast up to two "
        + "spells from among the other cards exiled this way without paying their mana costs.")]
    [InlineData("Wand of Wonder", "Artifact",
        "{4}, {T}: Roll a d20. Each opponent exiles cards from the top of their library until they exile an "
        + "instant or sorcery card, then shuffles the rest into their library. You may cast up to X instant "
        + "and/or sorcery spells from among cards exiled this way without paying their mana costs.\n1—9 | X is "
        + "one.\n10—19 | X is two.\n20 | X is three.")]
    [InlineData("Fevered Suspicion", "Sorcery",
        "Each opponent exiles cards from the top of their library until they exile a nonland card. You may "
        + "cast any number of spells from among those nonland cards without paying their mana costs.\nRebound "
        + "(If you cast this spell from your hand, exile it as it resolves. At the beginning of your next "
        + "upkeep, you may cast this card from exile without paying its mana cost.)")]
    // An exile or a reveal UNTIL a nonland card cast free (FreeCastUntilANonland),
    // off an opponent's library too: Neera, Wild Mage, and Nicol Bolas, God-
    // Pharaoh's +2, named.
    [InlineData("Neera, Wild Mage", "Legendary Creature — Human Elf Shaman",
        "Whenever you cast a spell, you may put it on the bottom of its owner's library. If you do, reveal "
        + "cards from the top of your library until you reveal a nonland card. You may cast that card without "
        + "paying its mana cost. Then put all revealed cards not cast this way on the bottom of your library in "
        + "a random order. This ability triggers only once each turn.")]
    [InlineData("Nicol Bolas, God-Pharaoh", "Legendary Planeswalker — Bolas",
        "+2: Target opponent exiles cards from the top of their library until they exile a nonland card. "
        + "Until end of turn, you may cast that card without paying its mana cost.\n+1: Each opponent exiles two "
        + "cards from their hand.\n−4: Nicol Bolas deals 7 damage to target opponent, creature an opponent "
        + "controls, or planeswalker an opponent controls.\n−12: Exile each nonland permanent your opponents "
        + "control.")]
    // A card off the top SUSPENDED for you (SuspendsACardOffTheTop): Sibylline
    // Soothsayer and The Parting of the Ways, named.
    [InlineData("Sibylline Soothsayer", "Creature — Human Warlock",
        "Temporal Foresight — When this creature enters, reveal cards from the top of your library until you "
        + "reveal a nonland card with mana value 3 or greater. Exile that card with three time counters on it. "
        + "If it doesn't have suspend, it gains suspend. Put the rest of the revealed cards on the bottom of "
        + "your library in a random order.")]
    [InlineData("The Parting of the Ways", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Exile "
        + "the top five cards of your library. For each nonland card exiled this way, put a number of time "
        + "counters on that card equal to its mana value. If it doesn't have suspend, it gains suspend.\nII — "
        + "Time travel, then time travel.\nIII — For each opponent, destroy up to one target artifact that "
        + "player controls.")]
    // ONE card off the top cast or played free by an ability that repeats
    // (FreeCardOffTheTop, CastsItFree): Temporal Aperture, named; Urza, Lord High
    // Artificer's shuffle-then-exile.
    [InlineData("Temporal Aperture", "Artifact",
        "{5}, {T}: Shuffle your library, then reveal the top card. Until end of turn, for as long as that "
        + "card remains on top of your library, play with the top card of your library revealed and you may "
        + "play that card without paying its mana cost. (If it has X in its mana cost, X is 0.)")]
    [InlineData("Urza, Lord High Artificer", "Legendary Creature — Human Artificer",
        "When Urza enters, create a 0/0 colorless Construct artifact creature token with \"This token gets "
        + "+1/+1 for each artifact you control.\"\nTap an untapped artifact you control: Add {U}.\n{5}: Shuffle "
        + "your library, then exile the top card. Until end of turn, you may play that card without paying its "
        + "mana cost.")]
    // MORE THAN ONE card off the top put onto the battlefield
    // (SeveralPutFromAmongThem): Majestic Genesis, Over the Top, Hew the Entwood,
    // Vorinclex's milled pair and Tezzeret, Master of the Bridge's -8, named;
    // Gilgamesh, Master-at-Arms' Equipment, reached.
    [InlineData("Majestic Genesis", "Sorcery",
        "Reveal the top X cards of your library, where X is the greatest mana value of a commander you own on "
        + "the battlefield or in the command zone. You may put any number of permanent cards from among them "
        + "onto the battlefield. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Over the Top", "Sorcery",
        "Each player reveals a number of cards from the top of their library equal to the number of nonland "
        + "permanents they control, puts all permanent cards they revealed this way onto the battlefield, and "
        + "puts the rest into their graveyard.")]
    [InlineData("Hew the Entwood", "Sorcery",
        "Sacrifice any number of lands. Reveal the top X cards of your library, where X is the number of "
        + "lands sacrificed this way. Choose any number of artifact and/or land cards revealed this way. Put "
        + "all nonland cards chosen this way onto the battlefield, then put all land cards chosen this way onto "
        + "the battlefield tapped, then put the rest on the bottom of your library in a random order.")]
    [InlineData("Vorinclex // The Grand Evolution", "Legendary Creature — Phyrexian Praetor // Enchantment — Saga",
        "Reach, trample\nWhen Vorinclex enters, search your library for up to two Forest cards, reveal them, "
        + "put them into your hand, then shuffle.\n{6}{G}{G}: Exile Vorinclex, then return it to the battlefield "
        + "transformed under its owner's control. Activate only as a sorcery.\n(As this Saga enters and after "
        + "your draw step, add a lore counter.)\nI — Mill ten cards. Put up to two creature cards from among the "
        + "milled cards onto the battlefield.\nII — Distribute seven +1/+1 counters among any number of target "
        + "creatures you control.\nIII — Until end of turn, creatures you control gain \"{1}: This creature "
        + "fights target creature you don't control.\" Exile this Saga, then return it to the battlefield (front "
        + "face up).")]
    [InlineData("Tezzeret, Master of the Bridge", "Legendary Planeswalker — Tezzeret",
        "Creature and planeswalker spells you cast have affinity for artifacts. (They cost {1} less to cast "
        + "for each artifact you control.)\n+2: Tezzeret deals X damage to each opponent, where X is the number "
        + "of artifacts you control. You gain X life.\n−3: Return target artifact card from your graveyard to "
        + "your hand.\n−8: Exile the top ten cards of your library. Put all artifact cards from among them onto "
        + "the battlefield.")]
    [InlineData("Gilgamesh, Master-at-Arms", "Legendary Creature — Human Samurai",
        "Whenever Gilgamesh enters or attacks, look at the top six cards of your library. You may put any "
        + "number of Equipment cards from among them onto the battlefield. Put the rest on the bottom of your "
        + "library in a random order. When you put one or more Equipment onto the battlefield this way, you may "
        + "attach one of them to a Samurai you control.")]
    // A creature or another permanent off the top put onto the battlefield by an
    // ability that repeats (RevealedOntoTheBattlefield): Call of the Wild and Atla
    // Palani, named; Doors of Durin's 'revealed this way', reached; Zoologist.
    [InlineData("Call of the Wild", "Enchantment",
        "{2}{G}{G}: Reveal the top card of your library. If it's a creature card, put it onto the "
        + "battlefield. Otherwise, put it into your graveyard.")]
    [InlineData("Atla Palani, Nest Tender", "Legendary Creature — Human Shaman",
        "{2}, {T}: Create a 0/1 green Egg creature token with defender.\nWhenever an Egg you control dies, "
        + "reveal cards from the top of your library until you reveal a creature card. Put that card onto the "
        + "battlefield and the rest on the bottom of your library in a random order.")]
    [InlineData("Doors of Durin", "Legendary Artifact",
        "Whenever you attack, scry 2, then you may reveal the top card of your library. If a creature card is "
        + "revealed this way, put it onto the battlefield tapped and attacking. Until your next turn, it gains "
        + "trample if you control a Dwarf and hexproof if you control an Elf.")]
    [InlineData("Zoologist", "Creature — Human Druid",
        "{3}{G}, {T}: Reveal the top card of your library. If it's a creature card, put it onto the "
        + "battlefield. Otherwise, put it into your graveyard.")]
    public void Classify_TheNovember3Redefinition_ACardOffTheTop_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-11-03 ('Si ripete = CA', asked with Arthur, whose creature also
    // comes back to the hand): ONE card chosen off the top and put onto the
    // battlefield by an ability that repeats is CardAdvantage, chosen as well as
    // random (OneChosenOffTheTop). Web of Life and Destiny, Owlbear Cub and
    // Armored Skyhunter named, Filter by hand; Ureni of the Unwritten reached;
    // Mole Module, Muzzio and Winota unreviewed.
    [InlineData("Arthur, Marigold Knight", "Legendary Creature — Mouse Knight",
        "Haste\nWhenever Arthur and at least one other creature attack, look at the top six cards of your "
        + "library. You may put a creature card from among them onto the battlefield tapped and attacking. Put "
        + "the rest on the bottom of your library in a random order. Return that creature to its owner's hand "
        + "at end of combat.")]
    [InlineData("Web of Life and Destiny", "Enchantment",
        "Convoke (Your creatures can help cast this spell. Each creature you tap while casting this spell "
        + "pays for {1} or one mana of that creature's color.)\nAt the beginning of combat on your turn, look at "
        + "the top five cards of your library. You may put a creature card from among them onto the "
        + "battlefield. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Owlbear Cub", "Creature — Bird Bear",
        "Mama's Coming — Whenever this creature attacks a player who controls eight or more lands, look at "
        + "the top eight cards of your library. You may put a creature card from among them onto the "
        + "battlefield tapped and attacking that player. Put the rest on the bottom of your library in a random "
        + "order.")]
    [InlineData("Armored Skyhunter", "Creature — Cat Knight",
        "Flying\nWhenever this creature attacks, look at the top six cards of your library. You may put an "
        + "Aura or Equipment card from among them onto the battlefield. If an Equipment is put onto the "
        + "battlefield this way, you may attach it to a creature you control. Put the rest of those cards on "
        + "the bottom of your library in a random order.")]
    [InlineData("Ureni of the Unwritten", "Legendary Creature — Spirit Dragon",
        "Flying, trample\nWhenever Ureni enters or attacks, look at the top eight cards of your library. You "
        + "may put a Dragon creature card from among them onto the battlefield. Put the rest on the bottom of "
        + "your library in a random order.")]
    [InlineData("Mole Module", "Artifact — Vehicle",
        "Menace\nWhenever this Vehicle deals combat damage to a player, mill four cards. You may put a "
        + "permanent card from among them onto the battlefield.\nCrew 2")]
    [InlineData("Muzzio, Visionary Architect", "Legendary Creature — Human Artificer",
        "{3}{U}, {T}: Look at the top X cards of your library, where X is the greatest mana value among "
        + "artifacts you control. You may put an artifact card from among them onto the battlefield. Put the "
        + "rest on the bottom of your library in any order.")]
    [InlineData("Winota, Joiner of Forces", "Legendary Creature — Human Warrior",
        "Whenever a non-Human creature you control attacks, look at the top six cards of your library. You "
        + "may put a Human creature card from among them onto the battlefield tapped and attacking. It gains "
        + "indestructible until end of turn. Put the rest of the cards on the bottom of your library in a "
        + "random order.")]
    public void Classify_TheNovember3Redefinition_OneChosenAgainAndAgain_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The edges of the same 2026-11-03 reading. A MASS SWAP of your own permanents
    // for as many off the top is a card for a card (MassSwap): Mass Polymorph,
    // Synthetic Destiny, Glimpse of Tomorrow.
    [InlineData("Mass Polymorph", "Sorcery",
        "Exile all creatures you control, then reveal cards from the top of your library until you reveal "
        + "that many creature cards. Put all creature cards revealed this way onto the battlefield, then "
        + "shuffle the rest of the revealed cards into your library.")]
    [InlineData("Synthetic Destiny", "Instant",
        "Exile all creatures you control. At the beginning of the next end step, reveal cards from the top of "
        + "your library until you reveal that many creature cards, put all creature cards revealed this way "
        + "onto the battlefield, then shuffle the rest of the revealed cards into your library.")]
    [InlineData("Glimpse of Tomorrow", "Sorcery",
        "Suspend 3—{R}{R}\nShuffle all permanents you own into your library, then reveal that many cards from "
        + "the top of your library. Put all non-Aura permanent cards revealed this way onto the battlefield, "
        + "then do the same for Aura cards, then put the rest on the bottom of your library in a random order.")]
    // So is a swap of ONE of your own for the creature revealed, however often
    // (SwapsYourOwn): Identity Echo, Fireflux Squad, Audacious Reshapers.
    [InlineData("Identity Echo", "Enchantment",
        "{3}{R}: Exile target creature or planeswalker you control. Reveal cards from the top of your library "
        + "until you reveal a creature or planeswalker card. Put that card onto the battlefield and the rest on "
        + "the bottom of your library in a random order. Activate only as a sorcery.")]
    [InlineData("Fireflux Squad", "Creature — Human Soldier",
        "Haste\nWhenever this creature attacks, you may exile another target attacking creature you control. "
        + "If you do, reveal cards from the top of your library until you reveal a creature card. Put that card "
        + "onto the battlefield tapped and attacking and the rest on the bottom of your library in a random "
        + "order.")]
    [InlineData("Audacious Reshapers", "Creature — Human Artificer",
        "{T}, Sacrifice an artifact: Reveal cards from the top of your library until you reveal an artifact "
        + "card. Put that card onto the battlefield and the rest on the bottom of your library in a random "
        + "order. This creature deals damage to you equal to the number of cards revealed this way.")]
    // A free card only a creature TYPE may cast is the tribal impulse of
    // 2026-10-23: Descendants' Path, Leaf-Crowned Elder.
    // Cards from OUTSIDE THE GAME are a wish, not the top (Spawnsire of Ulamog).
    [InlineData("Descendants' Path", "Enchantment",
        "At the beginning of your upkeep, reveal the top card of your library. If it's a creature card that "
        + "shares a creature type with a creature you control, you may cast it without paying its mana cost. If "
        + "you don't cast it, put it on the bottom of your library.")]
    [InlineData("Leaf-Crowned Elder", "Creature — Treefolk Shaman",
        "Kinship — At the beginning of your upkeep, you may look at the top card of your library. If it "
        + "shares a creature type with this creature, you may reveal it. If you do, you may play that card "
        + "without paying its mana cost.")]
    [InlineData("Spawnsire of Ulamog", "Creature — Eldrazi",
        "Annihilator 1 (Whenever this creature attacks, defending player sacrifices a permanent of their "
        + "choice.)\n{4}: Create two 0/1 colorless Eldrazi Spawn creature tokens. They have \"Sacrifice this "
        + "token: Add {C}.\"\n{20}: Cast any number of Eldrazi spells from among cards you own outside the game "
        + "without paying their mana costs.")]
    // A creature revealed ONCE is one card, not a stream: Aspiring Champion
    // sacrifices itself, Gamekeeper exiles itself, Madcap Experiment is a sorcery.
    // LANDS are Ramp (Expand the Sphere), and a CAP is Filter (Knickknack Ouphe).
    [InlineData("Aspiring Champion", "Creature — Astartes Warrior",
        "Menace\nRuinous Ascension — When this creature deals combat damage to a player, sacrifice it. If you "
        + "do, reveal cards from the top of your library until you reveal a creature card. Put that card onto "
        + "the battlefield, then shuffle the rest into your library. If that creature is a Demon, it deals "
        + "damage equal to its power to each opponent.")]
    [InlineData("Gamekeeper", "Creature — Elf",
        "When this creature dies, you may exile it. If you do, reveal cards from the top of your library "
        + "until you reveal a creature card. Put that card onto the battlefield and put all other cards "
        + "revealed this way into your graveyard.")]
    [InlineData("Madcap Experiment", "Sorcery",
        "Reveal cards from the top of your library until you reveal an artifact card. Put that card onto the "
        + "battlefield and the rest on the bottom of your library in a random order. Madcap Experiment deals "
        + "damage to you equal to the number of cards revealed this way.")]
    [InlineData("Expand the Sphere", "Sorcery",
        "Look at the top six cards of your library. Put up to two land cards from among them onto the "
        + "battlefield tapped and the rest on the bottom of your library in a random order. If you put fewer "
        + "than two lands onto the battlefield this way, proliferate a number of times equal to the difference. "
        + "(Choose any number of permanents and/or players, then give each another counter of each kind already "
        + "there.)")]
    [InlineData("Knickknack Ouphe", "Creature — Ouphe",
        "This creature enters with X +1/+1 counters on it.\nWhen this creature enters, reveal the top X cards "
        + "of your library. You may put any number of Aura cards with mana value X or less from among them onto "
        + "the battlefield. Then put all cards revealed this way that weren't put onto the battlefield on the "
        + "bottom of your library in a random order.")]
    // A ONE-SHOT shuffle and free top card is no stream (Unexpected Results, a
    // sorcery): the repeat gate on FreeCardOffTheTop.
    [InlineData("Unexpected Results", "Sorcery",
        "Shuffle your library, then reveal the top card. If it's a nonland card, you may cast it without "
        + "paying its mana cost. If it's a land card, you may put it onto the battlefield and return Unexpected "
        + "Results to its owner's hand.")]
    // Nor, ruled 2026-11-03 on Killer Instinct ('Solo Burn'), a creature off the
    // top SACRIFICED at the next end step, which leaves no card behind
    // (SacrificedAtTheEndStep); nor one chosen after a sacrifice (Industrial
    // Advancement), a land (Freestrider Lookout, Ramp), or Guild Feud's card the
    // opponent chooses first (not read).
    [InlineData("Killer Instinct", "Enchantment",
        "At the beginning of your upkeep, reveal the top card of your library. If it's a creature card, put "
        + "it onto the battlefield. That creature gains haste until end of turn. Sacrifice it at the beginning "
        + "of the next end step.")]
    [InlineData("Industrial Advancement", "Enchantment",
        "At the beginning of your end step, you may sacrifice a creature. If you do, look at the top X cards "
        + "of your library, where X is that creature's mana value. You may put a creature card from among them "
        + "onto the battlefield. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Freestrider Lookout", "Creature — Human Rogue",
        "Reach\nWhenever you commit a crime, look at the top five cards of your library. You may put a land "
        + "card from among them onto the battlefield tapped. Put the rest on the bottom of your library in a "
        + "random order. This ability triggers only once each turn. (Targeting opponents, anything they "
        + "control, and/or cards in their graveyards is a crime.)")]
    // Guild Feud stood here until 2026-11-06, see
    // Classify_TheNovember6Filter_YouDoTheSameEveryUpkeep_IsCardAdvantage.
    public void Classify_TheNovember3Redefinition_ACardForACardOrOnce_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Read and ruled 2026-10-23 from the sitting of 2026-10-20..23. Cascade is a
    // card only when it comes round again: granted to other spells (Abaddon), cast
    // twice (Bloodbraid Challenger's escape), or the same exile-until on every
    // legendary spell (Jodah); one cascade is a discover. Then one card per
    // wording the rules could not read: a self-sacrifice for X or for 'you draw
    // two' (All-Fates Scroll, Infernal Idol), a mana symbol in 'for each'
    // (Bladecoil Serpent), each opponent's top card (Breeches), a spliced cantrip
    // (Everdream), an impulse on entering and on leaving (Experimental
    // Synthesizer), every card of a type kept from a look (Forging the Anchor), a
    // graveyard card on every hit (Ghost of Ramirez), a draw repeated by the spell
    // (Invoke Despair), four exiled every upkeep with one played (Journey to the
    // Lost City), each player drawing on a repeating trigger (Master of
    // Ceremonies), squad copies that draw (Sicarian Infiltrator), a mill handed
    // back (Sivriss), a land card paid for a draw by a card that brings lands back
    // (Soul of Windgrace), one card per target (Soulfire Eruption), a token that
    // taps to draw (Tamiyo, Compleated Sage), up to two kept from a look (The Mana
    // Rig), a death draw encore repeats (Trove Tracker), the same mode for a
    // target player (Unite the Coalition), a loyalty reveal-until into the hand
    // (Vivien), a life cost before a draw (War Room). Harnesser of Storms casts on
    // an Otter spell and plays any card: the tribal veto is not its.
    [InlineData("Abaddon the Despoiler", "Legendary Creature — Astartes Warrior",
        "Trample\nMark of Chaos Ascendant — During your turn, spells you cast from your hand with mana value X "
        + "or less have cascade, where X is the total amount of life your opponents have lost this turn.")]
    [InlineData("Bloodbraid Challenger", "Creature — Elf Berserker",
        "Cascade\nHaste\nEscape—{3}{R}{G}, Exile three other cards from your graveyard. (You may cast this card "
        + "from your graveyard for its escape cost.)")]
    [InlineData("Jodah, the Unifier", "Legendary Creature — Human Wizard",
        "Legendary creatures you control get +X/+X, where X is the number of legendary creatures you "
        + "control.\nWhenever you cast a legendary spell from your hand, exile cards from the top of your "
        + "library until you exile a legendary nonland card with lesser mana value. You may cast that card "
        + "without paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    [InlineData("All-Fates Scroll", "Artifact",
        "{T}: Add one mana of any color.\n{7}, {T}, Sacrifice this artifact: Draw X cards, where X is the "
        + "number of differently named lands you control.")]
    [InlineData("Infernal Idol", "Artifact",
        "{T}: Add {B}.\n{1}{B}{B}, {T}, Sacrifice this artifact: You draw two cards and lose 2 life.")]
    [InlineData("Bladecoil Serpent", "Artifact Creature — Serpent",
        "When this creature enters, for each {U}{U} spent to cast it, draw a card.\nWhen this creature enters, "
        + "for each {B}{B} spent to cast it, each opponent discards a card.\nWhen this creature enters, for each "
        + "{R}{R} spent to cast it, it gets +1/+0 and gains trample and haste until end of turn.")]
    [InlineData("Breeches, Brazen Plunderer", "Legendary Creature — Goblin Pirate",
        "Menace\nWhenever one or more Pirates you control deal damage to your opponents, exile the top card of "
        + "each of those opponents' libraries. You may play those cards this turn, and you may spend mana as "
        + "though it were mana of any color to cast those spells.\nPartner (You can have two commanders if both "
        + "have partner.)")]
    [InlineData("Everdream", "Instant",
        "Draw a card.\nSplice onto instant or sorcery {2}{U} (As you cast an instant or sorcery spell, you may "
        + "reveal this card from your hand and pay its splice cost. If you do, add this card's effects to that "
        + "spell.)")]
    [InlineData("Experimental Synthesizer", "Artifact",
        "When this artifact enters or leaves the battlefield, exile the top card of your library. Until end "
        + "of turn, you may play that card.\n{2}{R}, Sacrifice this artifact: Create a 2/2 white Samurai "
        + "creature token with vigilance. Activate only as a sorcery.")]
    [InlineData("Forging the Anchor", "Sorcery",
        "Look at the top five cards of your library. You may reveal any number of artifact cards from among "
        + "them and put the revealed cards into your hand. Put the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Ghost of Ramirez DePietro", "Legendary Creature — Spirit Pirate",
        "Ghost of Ramirez DePietro can't be blocked by creatures with toughness 3 or greater.\nWhenever Ghost "
        + "of Ramirez DePietro deals combat damage to a player, choose up to one target card in a graveyard "
        + "that was discarded or put there from a library this turn. Put that card into its owner's "
        + "hand.\nPartner (You can have two commanders if both have partner.)")]
    [InlineData("Invoke Despair", "Sorcery",
        "Target opponent sacrifices a creature of their choice. If they can't, they lose 2 life and you draw "
        + "a card. Then repeat this process for an enchantment and a planeswalker.")]
    [InlineData("Journey to the Lost City", "Enchantment",
        "At the beginning of your upkeep, exile the top four cards of your library, then roll a d20.\n1—9 | "
        + "You may put a land card from among those cards onto the battlefield.\n10—19 | Create a 2/2 green Wolf "
        + "creature token, then put a +1/+1 counter on it for each creature card among those cards.\n20 | Put "
        + "all permanent cards exiled with this enchantment onto the battlefield, then sacrifice it.")]
    [InlineData("Master of Ceremonies", "Creature — Rhino Druid",
        "At the beginning of your upkeep, each opponent chooses money, friends, or secrets. For each player "
        + "who chose money, you and that player each create a Treasure token. For each player who chose "
        + "friends, you and that player each create a 1/1 green and white Citizen creature token. For each "
        + "player who chose secrets, you and that player each draw a card.")]
    [InlineData("Sicarian Infiltrator", "Artifact Creature — Human Soldier",
        "Flash\nSquad {2} (As an additional cost to cast this spell, you may pay {2} any number of times. When "
        + "this creature enters, create that many tokens that are copies of it.)\nBenediction of the Omnissiah — "
        + "When this creature enters, draw a card.")]
    [InlineData("Sivriss, Nightmare Speaker", "Legendary Creature — Snake Cleric Warlock",
        "{T}, Sacrifice another creature or an artifact: For each opponent, you mill a card, then return that "
        + "card from your graveyard to your hand unless that player pays 3 life. (To mill a card, put the top "
        + "card of your library into your graveyard.)\nChoose a Background (You can have a Background as a "
        + "second commander.)")]
    [InlineData("Soul of Windgrace", "Legendary Creature — Cat Avatar",
        "Whenever Soul of Windgrace enters or attacks, you may put a land card from a graveyard onto the "
        + "battlefield tapped under your control.\n{G}, Discard a land card: You gain 3 life.\n{1}{R}, Discard a "
        + "land card: Draw a card.\n{2}{B}, Discard a land card: Soul of Windgrace gains indestructible until "
        + "end of turn. Tap it.")]
    [InlineData("Soulfire Eruption", "Sorcery",
        "Choose any number of target creatures, planeswalkers, and/or players. For each of them, exile the "
        + "top card of your library, then Soulfire Eruption deals damage equal to that card's mana value to "
        + "that permanent or player. You may play the exiled cards until the end of your next turn.")]
    [InlineData("Tamiyo, Compleated Sage", "Legendary Planeswalker — Tamiyo",
        "Compleated ({G/U/P} can be paid with {G}, {U}, or 2 life. If life was paid, this planeswalker enters "
        + "with two fewer loyalty counters.)\n+1: Tap up to one target artifact or creature. It doesn't untap "
        + "during its controller's next untap step.\n−X: Exile target nonland permanent card with mana value X "
        + "from your graveyard. Create a token that's a copy of that card.\n−7: Create Tamiyo's Notebook, a "
        + "legendary colorless Book artifact token with \"Spells you cast cost {2} less to cast\" and \"{T}: Draw "
        + "a card.\"")]
    [InlineData("The Mana Rig", "Legendary Artifact",
        "Whenever you cast a multicolored spell, create a tapped Powerstone token. (It's an artifact with "
        + "\"{T}: Add {C}. This mana can't be spent to cast a nonartifact spell.\")\n{X}{X}{X}, {T}: Look at the "
        + "top X cards of your library. Put up to two of them into your hand and the rest on the bottom of your "
        + "library in a random order.")]
    [InlineData("Trove Tracker", "Creature — Human Pirate",
        "When this creature dies, draw a card.\nEncore {5}{U}{U} ({5}{U}{U}, Exile this card from your "
        + "graveyard: For each opponent, create a token copy that attacks that opponent this turn if able. They "
        + "gain haste. Sacrifice them at the beginning of the next end step. Activate only as a sorcery.)")]
    [InlineData("Unite the Coalition", "Instant",
        "Choose five. You may choose the same mode more than once.\n• Target permanent phases out.\n• Target "
        + "player draws a card.\n• Exile target player's graveyard.\n• Unite the Coalition deals 2 damage to any "
        + "target.\n• Destroy target artifact or enchantment.")]
    [InlineData("Vivien, Nature's Avenger", "Legendary Planeswalker — Vivien",
        "+1: Put three +1/+1 counters on up to one target creature.\n−1: Reveal cards from the top of your "
        + "library until you reveal a creature card. Put that card into your hand and the rest on the bottom of "
        + "your library in a random order.\n−6: Target creature gets +10/+10 and gains trample until end of "
        + "turn.")]
    [InlineData("War Room", "Land",
        "{T}: Add {C}.\n{3}, {T}, Pay life equal to the number of colors in your commanders' color identity: "
        + "Draw a card.")]
    [InlineData("Harnesser of Storms", "Creature — Otter Wizard",
        "Whenever you cast a noncreature or Otter spell, you may exile the top card of your library. Until "
        + "end of turn, you may play that card. This ability triggers only once each turn.")]
    // Cascade printed twice (Apex Devastator) and cascade beside a rebound (Into
    // the Time Vortex), both tagged.
    [InlineData("Apex Devastator", "Creature — Chimera Hydra",
        "Cascade, cascade, cascade, cascade (When you cast this spell, exile cards from the top of your "
        + "library until you exile a nonland card that costs less. You may cast it without paying its mana "
        + "cost. Put the exiled cards on the bottom in a random order. Multiple instances of cascade each "
        + "trigger separately.)")]
    [InlineData("Into the Time Vortex", "Sorcery",
        "Cascade (When you cast this spell, exile cards from the top of your library until you exile a "
        + "nonland card that costs less. You may cast it without paying its mana cost. Put the exiled cards on "
        + "the bottom in a random order.)\nRebound (If you cast this spell from your hand, exile it as it "
        + "resolves. At the beginning of your next upkeep, you may cast this card from exile without paying its "
        + "mana cost.)")]
    public void Classify_TheOctober23CardAdvantagePass_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-10-23: one cascade is one card, once, like a discover (Forceful
    // Denial, Sakashima's Protege, Heralds of Tzeentch; Bloodbraid Marauder's
    // 'this spell has cascade' is its own), and Diviner's Lockbox guessing the top
    // card by name is difficulty, not a card. Read with them: a curse watching the
    // enchanted player draw (Curse of Fool's Wisdom), a tutor into exile cast from
    // there (Evolving Door), any target spell exiled and cast (Gale's
    // Redirection), a draw an opponent buys to free their creature (Hold for
    // Ransom), an impulse only a Goblin may be cast from (Rundvelt Hordemaster),
    // Rat cards kept from a look (Karumonix), and a self-sacrifice that discards
    // one of the two (Starting Column).
    [InlineData("Forceful Denial", "Instant",
        "Cascade (When you cast this spell, exile cards from the top of your library until you exile a "
        + "nonland card that costs less. You may cast it without paying its mana cost. Put the exiled cards on "
        + "the bottom in a random order.)\nCounter target spell.")]
    [InlineData("Sakashima's Protege", "Creature — Shapeshifter",
        "Flash\nCascade (When you cast this spell, exile cards from the top of your library until you exile a "
        + "nonland card that costs less. You may cast it without paying its mana cost. Put the exiled cards on "
        + "the bottom in a random order.)\nYou may have this creature enter as a copy of any permanent that "
        + "entered this turn.")]
    [InlineData("Heralds of Tzeentch", "Creature — Demon",
        "Flying\nCascade (When you cast this spell, exile cards from the top of your library until you exile a "
        + "nonland card that costs less. You may cast it without paying its mana cost. Put the exiled cards on "
        + "the bottom in a random order.)")]
    [InlineData("Bloodbraid Marauder", "Creature — Human Berserker",
        "This creature can't block.\nDelirium — This spell has cascade as long as there are four or more card "
        + "types among cards in your graveyard. (When you cast this spell, exile cards from the top of your "
        + "library until you exile a nonland card that costs less. You may cast it without paying its mana "
        + "cost. Put the exiled cards on the bottom in a random order.)")]
    [InlineData("Diviner's Lockbox", "Artifact",
        "{1}, {T}: Choose a card name, then reveal the top card of your library. If that card has the chosen "
        + "name, sacrifice this artifact and draw three cards. Activate only as a sorcery.")]
    [InlineData("Curse of Fool's Wisdom", "Enchantment — Aura Curse",
        "Enchant player\nWhenever enchanted player draws a card, they lose 2 life and you gain 2 life.\nMadness "
        + "{3}{B} (If you discard this card, discard it into exile. When you do, cast it for its madness cost "
        + "or put it into your graveyard.)")]
    [InlineData("Evolving Door", "Artifact",
        "{1}, {T}, Sacrifice a creature: Count the colors of the sacrificed creature, then search your "
        + "library for a creature card that's exactly that many colors plus one. Exile that card, then shuffle. "
        + "You may cast the exiled card. Activate only as a sorcery.")]
    [InlineData("Gale's Redirection", "Instant",
        "Exile target spell, then roll a d20 and add that spell's mana value.\n1—14 | You may cast the exiled "
        + "card for as long as it remains exiled, and you may spend mana as though it were mana of any color to "
        + "cast that spell.\n15+ | You may cast the exiled card without paying its mana cost for as long as it "
        + "remains exiled.")]
    [InlineData("Hold for Ransom", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature can't attack or block and has \"{7}: Hold for Ransom's controller "
        + "sacrifices it and draws a card. Activate only as a sorcery.\"")]
    [InlineData("Rundvelt Hordemaster", "Creature — Goblin Warrior",
        "Other Goblins you control get +1/+1.\nWhenever this creature or another Goblin you control dies, "
        + "exile the top card of your library. If it's a Goblin creature card, you may cast that card until the "
        + "end of your next turn.")]
    [InlineData("Karumonix, the Rat King", "Legendary Creature — Phyrexian Rat",
        "Toxic 1 (Players dealt combat damage by this creature also get a poison counter.)\nOther Rats you "
        + "control have toxic 1.\nWhen Karumonix enters, look at the top five cards of your library. You may "
        + "reveal any number of Rat cards from among them and put the revealed cards into your hand. Put the "
        + "rest on the bottom of your library in a random order.")]
    [InlineData("Starting Column", "Artifact",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns "
        + "when an opponent loses life. Max speed is 4.)\n{T}: Add one mana of any color.\nMax speed — {T}, "
        + "Sacrifice this artifact: Draw two cards, then discard a card.")]
    public void Classify_TheOctober23CardAdvantagePass_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Read 2026-10-27 from the sitting of 2026-10-26: each card is the one
    // reviewed card that writes a shape this tag already has, tagged by hand. A
    // closing conditional draw on an attack (Aradesh); the bottom of their library
    // every end step (Arvinox); an activated draw closing a move (Black Panther);
    // any graveyard every turn (Coram); 'look at and play' every upkeep (Headliner
    // Scarlett); a spell exiled to be plotted (Lilah); a d20 draw on every cast
    // (Mathise); a number of cards off the top, all playable (Neriv); 'each draw
    // half that many' on a tap (Sokrates); and a land beside the tribe keeps the
    // second hand (Traveling Chocobo).
    [InlineData("Aradesh, the Founder", "Legendary Creature — Human Soldier",
        "Enlist (As this creature attacks, you may tap a nonattacking creature you control without summoning "
        + "sickness. When you do, add its power to this creature's until end of turn.)\nWhenever a creature you "
        + "control attacks, if it enlisted a creature this combat, the creature that attacked gains double "
        + "strike until end of turn. If that creature's power is 4 or greater, draw a card.")]
    [InlineData("Arvinox, the Mind Flail", "Legendary Enchantment Creature — Horror",
        "Arvinox isn't a creature unless you control three or more permanents you don't own.\nAt the beginning "
        + "of your end step, exile the bottom card of each opponent's library face down. For as long as those "
        + "cards remain exiled, you may look at them, you may cast permanent spells from among them, and you "
        + "may spend mana as though it were mana of any color to cast those spells.")]
    [InlineData("Black Panther, Wakandan King", "Legendary Creature — Human Noble Hero",
        "First strike\nSurvey the Realm — Whenever Black Panther or another creature you control enters, put a "
        + "+1/+1 counter on target land you control.\nMine Vibranium — {3}: Move all +1/+1 counters from target "
        + "land you control onto target creature. If one or more +1/+1 counters are moved this way, you gain "
        + "that much life and draw a card.")]
    [InlineData("Coram, the Undertaker", "Legendary Creature — Human Warrior",
        "Coram gets +X/+0, where X is the greatest power among creature cards in all graveyards.\nWhenever "
        + "Coram attacks, each player mills a card.\nDuring each of your turns, you may play a land and cast a "
        + "spell from among cards in graveyards that were put there from libraries this turn.")]
    [InlineData("Headliner Scarlett", "Legendary Creature — Human Warlock",
        "Haste\nWhen Headliner Scarlett enters, creatures target player controls can't block this turn.\nAt the "
        + "beginning of your upkeep, exile the top card of your library face down. You may look at and play "
        + "that card this turn.")]
    [InlineData("Lilah, Undefeated Slickshot", "Legendary Creature — Human Rogue",
        "Prowess (Whenever you cast a noncreature spell, this creature gets +1/+1 until end of "
        + "turn.)\nWhenever you cast a multicolored instant or sorcery spell from your hand, exile that spell "
        + "instead of putting it into your graveyard as it resolves. If you do, it becomes plotted. (You may "
        + "cast it as a sorcery on a later turn without paying its mana cost.)")]
    [InlineData("Mathise, Surge Channeler", "Legendary Creature — Human Elf Shaman Sorcerer",
        "Whenever you cast an instant or sorcery spell with mana value 3 or greater, roll a d20.\n1-9 | Each "
        + "player draws a card.\n10-19 | You draw a card.\n20 | Copy that spell. You may choose new targets for "
        + "the copy.")]
    [InlineData("Neriv, Crackling Vanguard", "Legendary Creature — Spirit Dragon",
        "Flying, deathtouch\nWhen Neriv enters, create two 1/1 red Goblin creature tokens.\nWhenever Neriv "
        + "attacks, exile a number of cards from the top of your library equal to the number of differently "
        + "named tokens you control. During any turn you attacked with a commander, you may play those cards.")]
    [InlineData("Sokrates, Athenian Teacher", "Legendary Creature — Human Advisor",
        "Defender\nSokrates has hexproof as long as it's untapped.\nSokratic Dialogue — {T}: Until end of turn, "
        + "target creature gains \"If this creature would deal combat damage to a player, prevent that damage. "
        + "This creature's controller and that player each draw half that many cards, rounded down.\"")]
    [InlineData("Traveling Chocobo", "Creature — Bird",
        "You may look at the top card of your library any time.\nYou may play lands and cast Bird spells from "
        + "the top of your library.\nIf a land or Bird you control entering the battlefield causes a triggered "
        + "ability of a permanent you control to trigger, that ability triggers an additional time.")]
    public void Classify_TheOctober27CardAdvantagePass_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Read 2026-10-27: a second hand on top that only a creature TYPE may cast
    // from (Hakoda, the tribal ruling of 2026-10-16); a number exiled and ONE
    // chosen (End-Blaze Epiphany, the count of a Filter); a draw consoling a copy
    // not made, 'if you don't' (Shiko and Narset, untagged).
    [InlineData("Hakoda, Selfless Commander", "Legendary Creature — Human Warrior Ally",
        "Vigilance\nYou may look at the top card of your library any time.\nYou may cast Ally spells from the "
        + "top of your library.\nSacrifice Hakoda: Creatures you control get +0/+5 and gain indestructible until "
        + "end of turn.")]
    [InlineData("End-Blaze Epiphany", "Instant",
        "End-Blaze Epiphany deals X damage to target creature. When that creature dies this turn, exile a "
        + "number of cards from the top of your library equal to its power, then choose a card exiled this way. "
        + "Until the end of your next turn, you may play that card.")]
    [InlineData("Shiko and Narset, Unified", "Legendary Creature — Human Spirit Dragon",
        "Flying, vigilance\nFlurry — Whenever you cast your second spell each turn, copy that spell if it "
        + "targets a permanent or player, and you may choose new targets for the copy. If you don't copy a "
        + "spell this way, draw a card.")]
    public void Classify_TheOctober27CardAdvantagePass_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-11-03 (CA): a repeatable ability that discards ONE card and draws
    // TWO nets a card every time. Niambi, Esteemed Speaker; Key to the Side-Door
    // is the same ability with a longer noun and was CardAdvantage already.
    [InlineData("Niambi, Esteemed Speaker", "Legendary Creature — Human Cleric",
        "Flash\nWhen Niambi enters, you may return another target creature you control to its owner's hand. If "
        + "you do, you gain life equal to that creature's mana value.\n{1}{W}{U}, {T}, Discard a legendary card: "
        + "Draw two cards.")]
    [InlineData("Key to the Side-Door", "Artifact",
        "{2}, {T}: Target creature can't be blocked this turn.\n{1}, {T}, Discard a legendary card with the "
        + "same name as a legendary permanent you control: Draw two cards.")]
    // Control: Reverberating Summons discards its whole hand and sacrifices itself
    // for two cards, a one-shot that was CardAdvantage already (reviewed
    // 2026-09-10).
    [InlineData("Reverberating Summons", "Enchantment",
        "At the beginning of each combat, if you've cast two or more spells this turn, this enchantment "
        + "becomes a 3/3 Monk creature with haste in addition to its other types until end of turn.\n{1}{R}, "
        + "Discard your hand, Sacrifice this enchantment: Draw two cards.")]
    public void Classify_TheNovember3CardAdvantagePass_DiscardOneDrawTwoEachTime_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The free card is Cheat AND CardAdvantage: Caves of Chaos Adventurer keeps
    // the tag it was reviewed with (2026-10-23), and Powerbalance (reviewed
    // 2026-09-23) did not move.
    [InlineData("Caves of Chaos Adventurer", "Creature — Human Barbarian",
        "Trample\nWhen this creature enters, you take the initiative.\nWhenever this creature attacks, exile "
        + "the top card of your library. If you've completed a dungeon, you may play that card this turn "
        + "without paying its mana cost. Otherwise, you may play that card this turn.")]
    [InlineData("Powerbalance", "Enchantment",
        "Whenever an opponent casts a spell, you may reveal the top card of your library. If you do, you may "
        + "cast that card without paying its mana cost if the two spells have the same mana value.")]
    public void Classify_TheNovember3CheatPass_TheImpulseRiderStaysCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A window opened by a one-shot, 'whenever X this turn, draw a card' or 'until
    // end of turn, whenever X, draw a card', draws on every X, ruled 2026-11-03
    // ('CA, si ripete'): Warhost's Frenzy named, Captain Howler reached and handed
    // back, Benefactor's Draught unreviewed.
    [InlineData("Warhost's Frenzy", "Instant",
        "Kicker {B} (You may pay an additional {B} as you cast this spell.)\nCreatures you control get +2/+0 "
        + "until end of turn. If this spell was kicked, whenever a creature you control dies this turn, draw a "
        + "card.")]
    [InlineData("Captain Howler, Sea Scourge", "Legendary Creature — Shark Pirate",
        "Ward—{2}, Pay 2 life.\nWhenever you discard one or more cards, target creature gets +2/+0 until end "
        + "of turn for each card discarded this way. Whenever that creature deals combat damage to a player "
        + "this turn, you draw a card.")]
    [InlineData("Benefactor's Draught", "Instant",
        "Untap all creatures. Until end of turn, whenever a creature an opponent controls blocks, draw a "
        + "card.\nDraw a card.")]
    public void Classify_TheNovember3Window_WheneverThisTurnDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Ruled 2026-11-05: unearth and encore SUM the card's entries for
    // CardAdvantage, as for Tokens (2026-10-27) and Buff. One draw on entry or on
    // a sacrifice is two or three draws with the keyword: Scrapwork Rager and
    // Combat Courier are CardAdvantage and Burn by hand; First-Sphere Gargantua is
    // reached and handed back.
    [InlineData("Scrapwork Rager", "Artifact Creature — Phyrexian Horror",
        "When this creature enters, you draw a card and you lose 1 life.\nUnearth {3}{B} ({3}{B}: Return this "
        + "card from your graveyard to the battlefield. It gains haste. Exile it at the beginning of the next "
        + "end step or if it would leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Combat Courier", "Artifact Creature — Construct",
        "{2}, Sacrifice this creature: Draw a card.\nUnearth {U} ({U}: Return this card from your graveyard to "
        + "the battlefield. It gains haste. Exile it at the beginning of the next end step or if it would leave "
        + "the battlefield. Unearth only as a sorcery.)")]
    [InlineData("First-Sphere Gargantua", "Creature — Phyrexian Horror",
        "When this creature enters, you draw a card and you lose 1 life.\nUnearth {2}{B} ({2}{B}: Return this "
        + "card from your graveyard to the battlefield. It gains haste. Exile it at the beginning of the next "
        + "end step or if it would leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Eumidian Wastewaker", "Creature — Insect Cleric",
        "Whenever this creature attacks, you and defending player each discard a card or sacrifice a "
        + "permanent. You draw a card for each land card put into a graveyard this way.\nEncore {6}{B}{B} "
        + "({6}{B}{B}, Exile this card from your graveyard: For each opponent, create a token copy that attacks "
        + "that opponent this turn if able. They gain haste. Sacrifice them at the beginning of the next end "
        + "step. Activate only as a sorcery.)")]
    [InlineData("Trove Tracker", "Creature — Human Pirate",
        "When this creature dies, draw a card.\nEncore {5}{U}{U} ({5}{U}{U}, Exile this card from your "
        + "graveyard: For each opponent, create a token copy that attacks that opponent this turn if able. They "
        + "gain haste. Sacrifice them at the beginning of the next end step. Activate only as a sorcery.)")]
    public void Classify_TheNovember5UnearthAndEncoreSumTheDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A loot stays card parity however many times it enters: Scrapwork Mutt
    // discards a card for the one it draws, so the summed entries have no surplus.
    [InlineData("Scrapwork Mutt", "Artifact Creature — Dog",
        "When this creature enters, you may discard a card. If you do, draw a card.\nUnearth {1}{R} ({1}{R}: "
        + "Return this card from your graveyard to the battlefield. It gains haste. Exile it at the beginning "
        + "of the next end step or if it would leave the battlefield. Unearth only as a sorcery.)")]
    public void Classify_TheNovember5ALootWithUnearth_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Read 2026-11-05: the card that exiles itself (Lantern of the Lost) or
    // sacrifices itself, even as an optional second sentence with no colon (Curse
    // of Silence, Dreamcatcher), pays for the card it draws: parity. Lost Isle
    // Calling's draw is for each, so it is not caught.
    [InlineData("Lantern of the Lost", "Artifact",
        "When this artifact enters, exile target card from a graveyard.\n{1}, {T}, Exile this artifact: Exile "
        + "all cards from all graveyards, then draw a card.")]
    [InlineData("Curse of Silence", "Enchantment — Aura Curse",
        "Enchant player\nAs this Aura enters, choose a card name.\nSpells with the chosen name enchanted player "
        + "casts cost {2} more to cast.\nWhenever enchanted player casts a spell with the chosen name, you may "
        + "sacrifice this Aura. If you do, draw a card.")]
    [InlineData("Dreamcatcher", "Creature — Spirit",
        "Whenever you cast a Spirit or Arcane spell, you may sacrifice this creature. If you do, draw a card.")]
    public void Classify_TheNovember5ADrawPaidByTheCardItself_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The self-exile guard leaves a draw for each alone: Lost Isle Calling exiles
    // itself but draws for each permanent it has, tagged by hand.
    [InlineData("Lost Isle Calling", "Enchantment",
        "Whenever you scry, put a verse counter on this enchantment.\n{4}{U}{U}, Exile this enchantment: Draw "
        + "a card for each verse counter on this enchantment. If it had seven or more verse counters on it, "
        + "take an extra turn after this one. Activate only as a sorcery.")]
    public void Classify_TheNovember5ADrawForEachBesideExilingItself_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // CONTO NETTO, ruled 2026-11-07 ('No, conta la mano'): a permanent paid is no
    // card of the hand, so only the discard is charged. Falkenrath Pit Fighter
    // discards one and sacrifices a Vampire for two, Soldevi Sage sacrifices two
    // lands for three and puts one back: both net a card, and a certain gain on
    // the ability is CardAdvantage alone (the 2026-09-27 exclusivity). Overturns
    // 2026-09-19 (Soldevi) and 2026-11-03 (Falkenrath).
    [InlineData("Falkenrath Pit Fighter", "Creature — Vampire Warrior",
        "{1}{R}, Discard a card, Sacrifice a Vampire: Draw two cards. Activate only if an opponent lost life "
        + "this turn.")]
    [InlineData("Soldevi Sage", "Creature — Human Wizard",
        "{T}, Sacrifice two lands: Draw three cards, then discard one of them.")]
    public void Classify_TheNovember7CardAdvantage_APermanentPaidIsNoCard_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A permanent OR a card for the draw (2026-11-07): paid with the permanent the
    // draw nets a card, paid with the card it is parity, so the gain is
    // conditional and the card is both tags (the 2026-11-06 reading). Highway
    // Robbery was Filter alone from 2026-09-27.
    [InlineData("Highway Robbery", "Sorcery",
        "You may discard a card or sacrifice a land. If you do, draw two cards.\nPlot {1}{R} (You may pay "
        + "{1}{R} and exile this card from your hand. Cast it as a sorcery on a later turn without paying its "
        + "mana cost. Plot only as a sorcery.)")]
    [InlineData("Demand Answers", "Instant",
        "As an additional cost to cast this spell, sacrifice an artifact or discard a card.\nDraw two cards.")]
    [InlineData("Reckless Detective", "Creature — Devil Detective",
        "Whenever this creature attacks, you may sacrifice an artifact or discard a card. If you do, draw a "
        + "card and this creature gets +2/+0 until end of turn.")]
    [InlineData("Chandra, Spark Hunter", "Legendary Planeswalker — Chandra",
        "At the beginning of combat on your turn, choose up to one target Vehicle you control. Until end of "
        + "turn, it becomes an artifact creature and gains haste.\n+2: You may sacrifice an artifact or discard "
        + "a card. If you do, draw a card.\n0: Create a 3/2 colorless Vehicle artifact token with crew 1.\n−7: "
        + "You get an emblem with \"Whenever an artifact you control enters, this emblem deals 3 damage to any "
        + "target.\"")]
    public void Classify_TheNovember7CardAdvantage_APermanentOrACard_IsCardAdvantageAndFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A card cast from the graveyard turn after turn is a card gained each time
    // ('Tutti CA', 2026-11-07), and the graveyard cards it charges cost nothing,
    // since they are no cards of the hand: forage and escape no longer veto the
    // tag. Serra Paragon and The Eighth Doctor play a land OR cast a spell from
    // the graveyard.
    [InlineData("Serra Paragon", "Creature — Angel",
        "Flying\nOnce during each of your turns, you may play a land from your graveyard or cast a permanent "
        + "spell with mana value 3 or less from your graveyard. If you do, it gains \"When this permanent is put "
        + "into a graveyard from the battlefield, exile it and you gain 2 life.\"")]
    [InlineData("The Eighth Doctor", "Legendary Creature — Time Lord Doctor",
        "When The Eighth Doctor enters, mill three cards.\nOnce during each of your turns, you may play a "
        + "historic land or cast a historic permanent spell from your graveyard. If you do, it gains \"If this "
        + "permanent would leave the battlefield, exile it instead of putting it anywhere else.\"")]
    [InlineData("Osteomancer Adept", "Creature — Squirrel Warlock",
        "Deathtouch\n{T}: Until end of turn, you may cast creature spells from your graveyard by foraging in "
        + "addition to paying their other costs. If you cast a spell this way, that creature enters with a "
        + "finality counter on it. (To forage, exile three cards from your graveyard or sacrifice a Food. If a "
        + "creature with a finality counter on it would die, exile it instead.)")]
    [InlineData("The Grim Captain's Locker", "Legendary Artifact",
        "{T}: Surveil 1. (Look at the top card of your library. You may put it into your graveyard.)\n{T}: "
        + "Until end of turn, each creature card in your graveyard gains \"Escape—{3}{B}, Exile four other cards "
        + "from your graveyard.\" (You may cast a card with escape from your graveyard for its escape cost.)")]
    public void Classify_TheNovember7CardAdvantage_AGraveyardCastEveryTurn_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A window of one turn on the graveyard is CardAdvantage like the stream ('CA
    // come la finestra', 2026-11-07): every spell cast through it is a card gained
    // (GraveyardWindowForOneTurn). Backdraft Hellkite is reached, not named, and
    // was handed back.
    [InlineData("Gaea's Will", "Sorcery",
        "Suspend 4—{G}\nUntil end of turn, you may play lands and cast spells from your graveyard.\nIf a card "
        + "would be put into your graveyard from anywhere this turn, exile that card instead.")]
    [InlineData("Urabrask // The Great Work", "Legendary Creature — Phyrexian Praetor // Enchantment — Saga",
        "First strike\nWhenever you cast an instant or sorcery spell, Urabrask deals 1 damage to target "
        + "opponent. Add {R}.\n{R}: Exile Urabrask, then return it to the battlefield transformed under its "
        + "owner's control. Activate only as a sorcery and only if you've cast three or more instant and/or "
        + "sorcery spells this turn.\n(As this Saga enters and after your draw step, add a lore counter.)\nI — "
        + "This Saga deals 3 damage to target opponent and each creature they control.\nII — Create three "
        + "Treasure tokens.\nIII — Until end of turn, you may cast instant and sorcery spells from any "
        + "graveyard. If a spell cast this way would be put into a graveyard, exile it instead. Exile this "
        + "Saga, then return it to the battlefield (front face up).")]
    [InlineData("Backdraft Hellkite", "Creature — Dragon",
        "Flying\nWhenever this creature attacks, each instant and sorcery card in your graveyard gains "
        + "flashback until end of turn. The flashback cost is equal to its mana cost.")]
    // Reached, unreviewed: Mizzix's Mastery copies every card exiled from the
    // graveyard on overload, the window's third wording.
    [InlineData("Mizzix's Mastery", "Sorcery",
        "Exile target card that's an instant or sorcery from your graveyard. For each card exiled this way, "
        + "copy it, and you may cast the copy without paying its mana cost. Exile Mizzix's Mastery.\nOverload "
        + "{5}{R}{R}{R} (You may cast this spell for its overload cost. If you do, change \"target\" in its text "
        + "to \"each.\")")]
    public void Classify_TheNovember7CardAdvantage_AGraveyardWindowForOneTurn_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Only your part of a symmetric draw counts (2026-11-07): Yangchen's chapter
    // II draws you three for one, whatever the opponent draws. It was parity
    // across the table before (EverybodyDrawsTheSame, withdrawn).
    [InlineData("The Legend of Yangchen // Avatar Yangchen", "Enchantment — Saga // Legendary Creature — Avatar",
        "(As this Saga enters and after your draw step, add a lore counter.)\nI — Starting with you, each "
        + "player chooses up to one permanent with mana value 3 or greater from among permanents your opponents "
        + "control. Exile those permanents.\nII — You may have target opponent draw three cards. If you do, draw "
        + "three cards.\nIII — Exile this Saga, then return it to the battlefield transformed under your "
        + "control.\nFlying\nWhenever you cast your second spell each turn, airbend up to one other target "
        + "nonland permanent. (Exile it. While it's exiled, its owner may cast it for {2} rather than its mana "
        + "cost.)")]
    public void Classify_TheNovember7CardAdvantage_OnlyYourPartCounts_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // How hard a draw is to set up sits inside the count, not outside it ('Dentro
    // il conto', 2026-11-07). Avatar Aang meets its own bending trigger with
    // firebending 2 on every attack ('Aang: CA'), and Lich turns every life gained
    // into cards ('Lich: CA').
    [InlineData("Avatar Aang // Aang, Master of Elements", "Legendary Creature — Human Avatar Ally // Legendary Creature — Avatar Ally",
        "Flying, firebending 2\nWhenever you waterbend, earthbend, firebend, or airbend, draw a card. Then if "
        + "you've done all four this turn, transform Avatar Aang.\nFlying\nSpells you cast cost {W}{U}{B}{R}{G} "
        + "less to cast. (This can reduce generic costs.)\nAt the beginning of each upkeep, you may transform "
        + "Aang, Master of Elements. If you do, you gain 4 life, draw four cards, put four +1/+1 counters on "
        + "him, and he deals 4 damage to each opponent.")]
    [InlineData("Lich", "Enchantment",
        "As this enchantment enters, you lose life equal to your life total.\nYou don't lose the game for "
        + "having 0 or less life.\nIf you would gain life, draw that many cards instead.\nWhenever you're dealt "
        + "damage, sacrifice that many nontoken permanents. If you can't, you lose the game.\nWhen this "
        + "enchantment is put into a graveyard from the battlefield, you lose the game.")]
    public void Classify_TheNovember7CardAdvantage_TheDifficultyIsInsideTheCount_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // A scry or surveil in front of a self-sacrifice that draws several is the
    // draw's, not a selection of its own ('CA, via Filter', 2026-11-07): Cryptex
    // nets cards and loses Filter by the 2026-09-27 exclusivity.
    [InlineData("Cryptex", "Artifact",
        "{T}, Collect evidence 3: Add one mana of any color. Put an unlock counter on this artifact. (To "
        + "collect evidence 3, exile cards with total mana value 3 or greater from your graveyard.)\nSacrifice "
        + "this artifact: Surveil 3, then draw three cards. Activate only if this artifact has five or more "
        + "unlock counters on it.")]
    public void Classify_TheNovember7CardAdvantage_AScryBeforeTheSelfSacrificeDraw_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Readings of 2026-11-07, each a wording the rules could not read: Galvanic
    // Relay's storm copies an impulse draw, Wall of Mourning puts a card it exiled
    // into the hand each turn, Investigator's Journal draws again from another
    // ability besides the self-sacrifice, Xanathar plays the opponent's top card,
    // Random Encounter returns the milled creatures to hand, Cryptic Pursuit casts
    // the face-down card that died.
    [InlineData("Galvanic Relay", "Sorcery",
        "Exile the top card of your library. During your next turn, you may play that card.\nStorm (When you "
        + "cast this spell, copy it for each spell cast before it this turn.)")]
    [InlineData("Investigator's Journal", "Artifact — Book Clue",
        "This artifact enters with a number of suspect counters on it equal to the greatest number of "
        + "creatures a player controls.\n{2}, {T}, Remove a suspect counter from this artifact: Draw a "
        + "card.\n{2}, Sacrifice this artifact: Draw a card.")]
    [InlineData("Wall of Mourning", "Creature — Wall",
        "Defender\nWhen this creature enters, exile a card from the top of your library face down for each "
        + "opponent you have.\nCoven — At the beginning of your end step, if you control three or more creatures "
        + "with different powers, put a card exiled with this creature into its owner's hand.")]
    [InlineData("Xanathar, Guild Kingpin", "Legendary Creature — Beholder",
        "At the beginning of your upkeep, choose target opponent. Until end of turn, that player can't cast "
        + "spells, you may look at the top card of their library any time, you may play the top card of their "
        + "library, and you may spend mana as though it were mana of any color to cast spells this way.")]
    [InlineData("Random Encounter", "Sorcery",
        "Shuffle your library, then mill four cards. Put each creature card milled this way onto the "
        + "battlefield. They gain haste. At the beginning of the next end step, return those creatures to their "
        + "owner's hand.\nFlashback {6}{R}{R} (You may cast this card from your graveyard for its flashback "
        + "cost. Then exile it.)")]
    [InlineData("Cryptic Pursuit", "Enchantment",
        "Whenever you cast an instant or sorcery spell from your hand, manifest the top card of your library. "
        + "(Put that card onto the battlefield face down as a 2/2 creature. Turn it face up any time for its "
        + "mana cost if it's a creature card.)\nWhenever a face-down creature you control dies, exile it if it's "
        + "an instant or sorcery card. You may cast that card until the end of your next turn.")]
    public void Classify_TheNovember7CardAdvantage_Readings_AreCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Not CardAdvantage (2026-11-07). Karazikar's draw waits for an opponent to
    // attack another opponent, and in the 2vs2 cube the two opponents are a team
    // ('In 2vs2 non scatta'). Ecstatic Awakener draws once and transforms, so the
    // draw never repeats and pays for itself.
    [InlineData("Karazikar, the Eye Tyrant", "Legendary Creature — Beholder",
        "Whenever you attack a player, tap target creature that player controls and goad it. (Until your next "
        + "turn, that creature attacks each combat if able and attacks a player other than you if "
        + "able.)\nWhenever an opponent attacks another one of your opponents, you and the attacking player each "
        + "draw a card and lose 1 life.")]
    [InlineData("Ecstatic Awakener // Awoken Demon", "Creature — Human Wizard // Creature — Demon",
        "{2}{B}, Sacrifice another creature: Draw a card, then transform this creature. Activate only once "
        + "each turn.")]
    public void Classify_TheNovember7CardAdvantage_ADrawTheCardCannotMakeHappen_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }
}
