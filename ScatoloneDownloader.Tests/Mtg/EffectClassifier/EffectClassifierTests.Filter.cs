using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // A card changing places at exactly no net gain, however the halves are
    // worded, is Filter and nothing else.
    [InlineData("Grab the Prize", "Sorcery",
        "As an additional cost to cast this spell, discard a card.\n"
        + "Draw two cards. If the discarded card wasn't a land card, Grab the Prize deals 2 damage to each opponent.")]
    [InlineData("Romantic Rendezvous", "Sorcery", "Discard a card, then draw two cards.")]
    [InlineData("Anvil of Bogardan", "Artifact",
        "Players have no maximum hand size.\n"
        + "At the beginning of each player's draw step, that player draws an additional card, then discards a card.")]
    public void Classify_ACardChangingPlaces_IsAlwaysFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The rummage: "you may discard a card. If you do, draw a card". The
    // plainest Filter shape printed and it had never been read — 14 cards fire
    // on it and 12 were already tagged by hand. Ruled 2026-09-19.
    // Yuyan Archers' rummage on entering moved to
    // Classify_TheOctober24FilterPass_IsNotFilter on 2026-10-24, when a
    // permanent's one-shot loot was ruled a rider like its one-shot scry.
    [InlineData("Rescue Leopard", "Creature — Cat",
        "Whenever this creature becomes tapped, you may discard a card. If you do, draw a card.")]
    // The activated version. The rule carries a lookahead for "this card"
    // because cycling reminder text reads identically and pays a card to
    // replace itself, which attacks nobody.
    [InlineData("Mesmeric Trance", "Enchantment",
        "Cumulative upkeep {1} (At the beginning of your upkeep, put an age counter on this permanent, "
        + "then sacrifice it unless you pay its upkeep cost for each age counter on it.)\n"
        + "{U}, Discard a card: Draw a card.")]
    public void Classify_Rummage_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A scry or surveil made ONCE beside what the card is for is not Filter,
    // ruled 2026-09-27 — "non li voglio come Filter, generano troppo rumore" —
    // which overturns the "unconditionally" these three pinned from 2026-09-19:
    // a bounce spell, a Vehicle and a creature. (Kozilek's Command was the
    // fourth, and moved out the same evening — a spell's MODE is its own effect.)
    [InlineData("Unauthorized Exit", "Instant",
        "Return target nonland permanent to its owner's hand. Surveil 1. "
        + "(Look at the top card of your library. You may put it into your graveyard.)")]
    [InlineData("Voyager Glidecar", "Artifact — Vehicle",
        "When this Vehicle enters, scry 1.\nTap three other untapped creatures you control: Until end of turn, "
        + "this Vehicle becomes an artifact creature and gains flying. Put a +1/+1 counter on it.\nCrew 1")]
    // Read in every printed form, "scry X" and "scries" included, so the veto
    // reaches every card the tag used to.
    [InlineData("Cascade Seer", "Creature — Merfolk Wizard",
        "When this creature enters, scry X, where X is the number of creatures in your party. "
        + "(Your party consists of up to one each of Cleric, Rogue, Warrior, and Wizard.)")]
    // The reminder text goes with the keyword: "(Look at the top two cards of
    // your library …)" is a look at the top in its own right.
    [InlineData("Consuming Ashes", "Instant",
        "Exile target creature. If it had mana value 3 or less, surveil 2. (Look at the top two cards of your "
        + "library, then put any number of them into your graveyard and the rest on top of your library in any "
        + "order.)")]
    // A LOOT tacked onto a spell that does something else is the same rider,
    // ruled on Refute: "non ha Filter, solo counter". Transpose's token carries
    // a quoted trigger of its own, which does not make the loot repeat.
    [InlineData("Refute", "Instant",
        "Counter target spell. Draw a card, then discard a card.")]
    [InlineData("Transpose", "Instant",
        "Draw a card, then discard a card. You lose 1 life. If this spell was cast from your hand, create a "
        + "0/1 black Wizard creature token with \"Whenever you cast a noncreature spell, this token deals 1 "
        + "damage to each opponent.\"\nRebound (If you cast this spell from your hand, exile it as it resolves. "
        + "At the beginning of your next upkeep, you may cast this card from exile without paying its mana "
        + "cost.)")]
    // …and a Blood token is the same rider: "se singoli no".
    [InlineData("Blood Servitor", "Artifact Creature — Construct",
        "When this creature enters, create a Blood token. (It's an artifact with \"{1}, {T}, Discard a card, "
        + "Sacrifice this token: Draw a card.\")")]
    // An explore made once as a creature enters is the same one-shot rider a scry
    // is, and a PAYOFF that watches others explore selects nothing.
    [InlineData("Merfolk Branchwalker", "Creature — Merfolk Scout",
        "When this creature enters, it explores. (Reveal the top card of your library. Put that card into "
        + "your hand if it's a land. Otherwise, put a +1/+1 counter on this creature, then put the card back or "
        + "put it into your graveyard.)")]
    [InlineData("Wildgrowth Walker", "Creature — Elemental",
        "Whenever a creature you control explores, put a +1/+1 counter on this creature and you gain 3 life.")]
    public void Classify_ASelectionMadeOnceInPassing_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // What keeps the tag: a spell that selects and does nothing else (Dreams of
    // Laguna), a scry that comes again (Veteran Guardmouse's valiant, Clandestine
    // Meddler's attacks — both named as slips on 2026-09-27), and Blood made
    // again and again ("se ripetuti sì").
    [InlineData("Dreams of Laguna", "Instant",
        "Surveil 1, then draw a card. (To surveil 1, look at the top card of your library. You may put it "
        + "into your graveyard.)\nFlashback {3}{U} (You may cast this card from your graveyard for its flashback "
        + "cost. Then exile it.)")]
    // A LAND's scry or surveil keeps the tag, ruled later the same day: "mi
    // serve come filtro sulle terre doppie".
    [InlineData("Undercity Sewers", "Land — Island Swamp",
        "({T}: Add {U} or {B}.)\nThis land enters tapped.\nWhen this land enters, surveil 1. (Look at the top "
        + "card of your library. You may put it into your graveyard.)")]
    [InlineData("Veteran Guardmouse", "Creature — Mouse Soldier",
        "Valiant — Whenever this creature becomes the target of a spell or ability you control for the first "
        + "time each turn, it gets +1/+0 and gains first strike until end of turn. Scry 1. (Look at the top "
        + "card of your library. You may put that card on the bottom.)")]
    [InlineData("Clandestine Meddler", "Creature — Vampire Rogue",
        "When this creature enters, suspect up to one other target creature you control. (A suspected "
        + "creature has menace and can't block.)\nWhenever one or more suspected creatures you control attack, "
        + "surveil 1. (Look at the top card of your library. You may put it into your graveyard.)")]
    [InlineData("Ivora, Insatiable Heir", "Legendary Creature — Vampire Warrior",
        "Trample\nWhen Ivora enters and whenever it deals combat damage to a player, create a Blood token. "
        + "(It's an artifact with \"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\nWhenever you "
        + "discard a card, put a +1/+1 counter on Ivora.")]
    // A spell's MODE is an effect of its own — "è una spell, quindi conta
    // l'effetto secco (come Ponder)" — so a Command's scry-and-draw and a
    // Charm's loot are selections you chose, not riders.
    [InlineData("Kozilek's Command", "Kindred Instant — Eldrazi",
        "Choose two —\n"
        + "• Target player creates X 0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this token: Add {C}.\"\n"
        + "• Target player scries X, then draws a card.\n"
        + "• Exile target creature with mana value X or less.\n"
        + "• Exile up to X target cards from graveyards.")]
    [InlineData("Treva's Charm", "Instant",
        "Choose one —\n• Destroy target enchantment.\n• Exile target attacking creature.\n• Draw a card, then "
        + "discard a card.")]
    // (Unassuming Sage was pinned here — a scry handed to something that
    // repeats it — until the Sorcerer Role's scry stopped being Filter on
    // 2026-10-13; see Classify_SelectionRuledOut_IsNotFilter.)
    // EXPLORE is selection, ruled 2026-09-29 ("explore = Filter"): Subterranean
    // Schooner, moved from CardAdvantage by the human, explores on every attack.
    [InlineData("Subterranean Schooner", "Artifact — Vehicle",
        "Whenever this Vehicle attacks, target creature that crewed it this turn explores. (Reveal the top "
        + "card of your library. Put that card into your hand if it's a land. Otherwise, put a +1/+1 counter on "
        + "that creature, then put the card back or put it into your graveyard.)\nCrew 1")]
    public void Classify_ASelectionThatIsTheCardOrComesAgain_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    public void Classify_BloodAgainAndAgain_IsARummageNotACard()
    {
        // The Blood reminder's "Draw a card" is half of a rummage, and had been
        // read as a repeatable draw. Ruled 2026-09-27 with the Blood token.
        CardEffect result = EffectClassifier.Classify(MakeCard("Moonstone Eulogist", "Creature — Bat Warlock",
            "Flying\nWhenever a creature an opponent controls dies, you create a Blood token. (It's an artifact "
            + "with \"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\nWhenever you sacrifice an "
            + "artifact, put a +1/+1 counter on this creature and you gain 1 life."));
        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    public void Classify_TwoBloodAtOnce_IsNeitherACardNorASelection()
    {
        // Two made once are two rummages made once: no card, and no stream.
        // The plural reminder ("They're artifacts with") had read as two cards.
        CardEffect result = EffectClassifier.Classify(MakeCard("Falkenrath Celebrants", "Creature — Vampire",
            "Menace (This creature can't be blocked except by two or more creatures.)\nWhen this creature enters, "
            + "create two Blood tokens. (They're artifacts with \"{1}, {T}, Discard a card, Sacrifice this token: "
            + "Draw a card.\")"));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // SOMEBODY ELSE'S TOP, rearranged or trimmed, ruled 2026-09-27: "sì,
    // guardare e basta no". Elemental Augury and Cruel Fate were untagged, Eye
    // Spy tagged, on the same act.
    [InlineData("Elemental Augury", "Enchantment",
        "{3}: Look at the top three cards of target player's library, then put them back in any order.")]
    // Cruel Fate stood here until 2026-11-06: only a top you could own is this
    // tag now ("solo le tue carte"), see Classify_TheNovember6Filter_OnlyAnOpponentsTop_IsNotFilter.
    [InlineData("Eye Spy", "Sorcery",
        "Look at the top card of target player's library. You may put that card into their graveyard.")]
    // MILL OR REVEAL A FEW AND KEEP ONE, the same day; Eerie Gravestone was the
    // human's slip.
    [InlineData("Cache Grab", "Instant",
        "Mill four cards. You may put a permanent card from among the cards milled this way into your hand. "
        + "If you control a Squirrel or returned a Squirrel card to your hand this way, create a Food token. "
        + "(To mill four cards, put the top four cards of your library into your graveyard. A Food token is an "
        + "artifact with \"{2}, {T}, Sacrifice this token: You gain 3 life.\")")]
    [InlineData("Eerie Gravestone", "Artifact",
        "When this artifact enters, draw a card.\n{1}{B}, Sacrifice this artifact: Mill four cards. You may "
        + "put a creature card from among them into your hand. (To mill four cards, put the top four cards of "
        + "your library into your graveyard.)")]
    [InlineData("Wood Sage", "Creature — Human Druid",
        "{T}: Choose a creature card name. Reveal the top four cards of your library and put all of them with "
        + "that name into your hand. Put the rest into your graveyard.")]
    // Five more named Filter by the human the same day.
    [InlineData("Winds of Change", "Sorcery",
        "Each player shuffles the cards from their hand into their library, then draws that many cards.")]
    [InlineData("Foresight", "Sorcery",
        "Search your library for three cards, exile them, then shuffle.\nDraw a card at the beginning of the "
        + "next turn's upkeep.")]
    [InlineData("Mana Severance", "Sorcery",
        "Search your library for any number of land cards, exile them, then shuffle.")]
    [InlineData("Scroll Rack", "Artifact",
        "{1}, {T}: Exile any number of cards from your hand face down. Put that many cards from the top of "
        + "your library into your hand. Then look at the exiled cards and put them on top of your library in "
        + "any order.")]
    public void Classify_SelectionRuledIn_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Only LOOKING at somebody's top is nothing, and manifest dread's reminder
    // has the victim look at their own.
    [InlineData("Orcish Spy", "Creature — Orc Rogue",
        "{T}: Look at the top three cards of target player's library.")]
    [InlineData("Fear of Impostors", "Enchantment Creature — Nightmare",
        "Flash\nWhen this creature enters, counter target spell. Its controller manifests dread. (That player "
        + "looks at the top two cards of their library, then puts one onto the battlefield face down as a 2/2 "
        + "creature and the other into their graveyard. If it's a creature card, it can be turned face up any "
        + "time for its mana cost.)")]
    // A pile you then PLAY from is Steal, however it was trimmed.
    [InlineData("Black Cat, Cunning Thief", "Legendary Creature — Human Rogue Villain",
        "When Black Cat enters, look at the top nine cards of target opponent's library, exile two of them "
        + "face down, then put the rest on the bottom of their library in a random order. You may play the "
        + "exiled cards for as long as they remain exiled. Mana of any type can be spent to cast spells this "
        + "way.")]
    // Perception Bobblehead stood here from 2026-09-23 ("cheated in, not chosen
    // between") until 2026-10-16: the 10-13 ruling made Kiora Cheat AND Filter,
    // so a look-and-cast-free is both, and the veto was withdrawn.
    // A SORCERER ROLE's scry was the Role's from 2026-10-13 (Unassuming Sage,
    // Spellbook Vendor here) until 2026-11-06, ruled the other way: see
    // Classify_TheNovember6Filter_ASorcererRolesScry_IsFilter.
    public void Classify_SelectionRuledOut_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Parity however it is worded, named Filter by the human on 2026-09-27:
    // a land may stand in for the discard, and N for N is still a rummage.
    [InlineData("Highway Robbery", "Sorcery",
        "You may discard a card or sacrifice a land. If you do, draw two cards.\nPlot {1}{R} (You may pay "
        + "{1}{R} and exile this card from your hand. Cast it as a sorcery on a later turn without paying its "
        + "mana cost. Plot only as a sorcery.)")]
    [InlineData("Horrid Shadowspinner", "Creature — Horror",
        "Lifelink\nWhenever this creature attacks, you may draw cards equal to its power. If you do, discard "
        + "that many cards.")]
    public void Classify_AnExchangeAtParity_IsFilterAndNotCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // EMPOWER JACE N makes a Jace planeswalker token whose whole job is
    // "[-1]: Surveil 1" and "[-3]: Draw a card", so the keyword filters by
    // itself. Ruled 2026-09-19. Thirty-five cards print it; 31 carry reminder
    // text that the surveil rule already reads, and these are the four that do
    // not.
    [InlineData("Sanctum Lurker", "Creature — Horror",
        "When this creature enters, empower Jace 1.\n"
        + "Planeswalkers you control aren't put into their owners' graveyards for having 0 loyalty.\n"
        + "Planeswalkers you control have \"[+2]: This planeswalker deals 1 damage to each opponent and you "
        + "gain 1 life.\"")]
    [InlineData("Theorist's Sanctum", "Land — Island",
        "({T}: Add {U}.)\n"
        + "As this land enters, you may behold a Jace. If you don't, this land enters tapped. (To behold a "
        + "Jace, choose a Jace you control or reveal a Jace card from your hand.)\n"
        + "{2}{U}, {T}: Empower Jace 2.")]
    [InlineData("Jace, Reality Sculptor", "Legendary Planeswalker — Jace",
        "+1: Empower Jace X, where X is the number of Islands you control.\n"
        + "−3: Until your next turn, whenever a creature attacks you or a planeswalker you control, it gets "
        + "-5/-0 until end of turn.\n"
        + "0: Exile all but the bottom card of each opponent's library. Activate only if there are twenty-five "
        + "or more loyalty counters among Jaces you control.")]
    [InlineData("Fatehold Charm", "Instant",
        "Choose one —\n• Draw a card. Empower Jace 2.\n• Return target spell or creature to its owner's hand.\n"
        + "• Creatures you control get +1/+2 until end of turn.")]
    public void Classify_EmpowerJace_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    // The one shape the count keeps out: a trigger that WATCHES a scry happen
    // is not a scry, the same reading that keeps "whenever you draw a card" out
    // of CardAdvantage. Planetarium of Wan Shi Tong still filters — it has its
    // own "{1}, {T}: Scry 2" — so the card is checked without that line.
    public void Classify_ATriggerThatWatchesAScry_IsNotItselfFilter()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(
            "Planetarium of Wan Shi Tong", "Legendary Artifact",
            "Whenever you scry or surveil, look at the top card of your library. You may cast that card without "
            + "paying its mana cost. Do this only once each turn. (Look at the card after you scry or surveil.)"));

        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-09-20: several cards off the top and only ONE of them playable
    // is two seen and one played, which is the count of a Filter. All three
    // reviewed cards say it in the same words, and the two that are not one-shot
    // spells still cannot repeat — Case of the Burning Masks sacrifices itself.
    [InlineData("Riverwheel Sweep", "Sorcery",
        "Tap target creature. Put three stun counters on it.\n"
        + "Exile the top two cards of your library. Choose one of them. Until the end of your next "
        + "turn, you may play that card.")]
    [InlineData("Heroes' Hangout", "Land",
        "Choose one —\n• Date Night — Exile the top two cards of your library. Choose one of them. "
        + "Until the end of your next turn, you may play that card.\n"
        + "• Patrol Night — One or two target creatures each get +1/+0 and gain first strike until "
        + "end of turn.")]
    [InlineData("Case of the Burning Masks", "Enchantment — Case",
        "When this Case enters, it deals 3 damage to target creature an opponent controls.\n"
        + "To solve — Three or more sources you controlled dealt damage this turn.\n"
        + "Solved — Sacrifice this Case: Exile the top three cards of your library. Choose one of "
        + "them. You may play that card this turn.")]
    public void Classify_SeveralOffTheTopAndOnePlayed_IsFilterNotCardAdvantage(
        string name, string typeLine, string oracle)
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(effects.HasFlag(CardEffect.CardAdvantage));
        Assert.True(effects.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-09-20, from the nine cards where the classifier said Filter and
    // the hand did not. Cloak and manifest dread turn what you picked FACE DOWN
    // into a 2/2, so what the ability gave you is a body and not a choice. A
    // LAND off the top is Ramp, the same rail that makes a land search Ramp
    // rather than Tutor. And "each opponent discards a card AND YOU DRAW a card"
    // is two players doing two different things, not one player trading.
    [InlineData("Curator Beastie", "Creature — Beast",
        "Reach\nColorless creatures you control enter with two additional +1/+1 counters on them.\n"
        + "Whenever this creature enters or attacks, manifest dread. (Look at the top two cards of "
        + "your library. Put one onto the battlefield face down as a 2/2 creature and the other into "
        + "your graveyard. Turn it face up any time for its mana cost if it's a creature card.)")]
    [InlineData("Hide in Plain Sight", "Sorcery",
        "Look at the top five cards of your library, cloak two of them, and put the rest on the bottom "
        + "of your library in a random order.")]
    [InlineData("Ignis Scientia", "Legendary Creature — Human Advisor",
        "When Ignis Scientia enters, look at the top six cards of your library. You may put a land card "
        + "from among them onto the battlefield tapped. Put the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Famished Worldsire", "Creature — Avatar",
        "Ward {3}\nDevour land 3\nWhen this creature enters, look at the top X cards of your library, "
        + "where X is this creature's power. Put any number of land cards from among them onto the "
        + "battlefield tapped, then shuffle.")]
    [InlineData("Jecht, Reluctant Guardian", "Legendary Enchantment Creature — Saga Human",
        "Menace\n(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I, II — Jecht Beam — Each opponent discards a card and you draw a card.\n"
        + "III — Ultimate Jecht Shot — Each opponent sacrifices two creatures of their choice.")]
    public void Classify_ThreeShapesThatOnlyLookLikeSelection_AreNotFilter(
        string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The carve-outs those three vetoes have to leave standing, each ruled the
    // same day. Planar Genesis takes a land if there is one and a CARD if there
    // is not, so it selects after all; hideaway looks at four and exiles one you
    // may play later, which is a hand you wait for; and "TARGET player" and
    // "EACH player" keep the loot, because you can point Forget at yourself and
    // because Flux loots everybody including you.
    [InlineData("Planar Genesis", "Sorcery",
        "Look at the top four cards of your library. You may put a land card from among them onto the "
        + "battlefield tapped. If you don't, put a card from among them into your hand. Put the rest on "
        + "the bottom of your library in a random order.")]
    [InlineData("Clive's Hideaway", "Land",
        "Hideaway 4 (When this land enters, look at the top four cards of your library, exile one face "
        + "down, then put the rest on the bottom in a random order.)\n{T}: Add {C}.\n"
        + "{2}, {T}: You may play the exiled card without paying its mana cost if you control four or "
        + "more legendary creatures.")]
    [InlineData("Forget", "Sorcery",
        "Target player discards two cards, then draws as many cards as they discarded this way.")]
    [InlineData("Flux", "Sorcery",
        "Each player discards any number of cards, then draws that many cards.\nDraw a card.")]
    public void Classify_WhatStillSelects_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    // The other half of the Jecht ruling: taking Filter away must not take the
    // three tags it really has. The edict on chapter III is Removal, the
    // opponents' discard is Discard, and the draw beside it is yours.
    public void Classify_ALootSplitBetweenTwoPlayers_KeepsItsOtherTags()
    {
        Card card = MakeCard("Jecht, Reluctant Guardian", "Legendary Enchantment Creature — Saga Human",
            "Menace\n(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
            + "I, II — Jecht Beam — Each opponent discards a card and you draw a card.\n"
            + "III — Ultimate Jecht Shot — Each opponent sacrifices two creatures of their choice.");

        CardEffect effects = EffectClassifier.Classify(card);

        Assert.True(effects.HasFlag(CardEffect.Discard));
        Assert.True(effects.HasFlag(CardEffect.CardAdvantage));
        Assert.True(effects.HasFlag(CardEffect.Removal));
        Assert.False(effects.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Look at the top cards, exile one and cast it is Filter by hand (Discover the
    // Impossible, 2026-10-27, read 10-30).
    [InlineData("Discover the Impossible", "Instant",
        "Look at the top five cards of your library. Exile one of them face down and put the rest on the "
        + "bottom of your library in a random order. You may cast the exiled card without paying its mana cost "
        + "if it's an instant spell with mana value 2 or less. If you don't, put that card into your hand.")]
    public void Classify_LookAtSeveralExileOneAndCastIt_IsFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Descendants' Fury sacrifices one of the creatures that dealt damage and digs
    // for a creature sharing its type: Filter and Sacrifice by hand (2026-10-29),
    // no Cheat, the type being TribalCard's.
    [InlineData("Descendants' Fury", "Enchantment",
        "Whenever one or more creatures you control deal combat damage to a player, you may sacrifice one of "
        + "them. If you do, reveal cards from the top of your library until you reveal a creature card that "
        + "shares a creature type with the sacrificed creature. Put that card onto the battlefield and the rest "
        + "on the bottom of your library in a random order.")]
    public void Classify_ADigForTheSacrificedCreaturesType_IsFilterAndSacrifice(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.True(result.HasFlag(CardEffect.Sacrifice));
        Assert.False(result.HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // One card kept out of a look, or a rummage for as many as were thrown
    // away, is Filter: explore spelled out in its long reminder (ruled 09-29
    // "explore = Filter"), a one-shot look that exiles one creature to cast
    // (Feral Encounter), and Fable's chapter II.
    [InlineData("Deepfathom Echo", "Creature — Merfolk Spirit",
        "At the beginning of combat on your turn, this creature explores. Then you may have it become a copy "
        + "of another creature you control until end of turn. (To have this creature explore, reveal the top "
        + "card of your library. Put that card into your hand if it's a land. Otherwise, put a +1/+1 counter "
        + "on this creature, then put the card back or put it into your graveyard.)")]
    [InlineData("Amalia Benavides Aguirre", "Legendary Creature — Vampire Scout",
        "Ward—Pay 3 life.\nWhenever you gain life, Amalia Benavides Aguirre explores. Then destroy all other "
        + "creatures if its power is exactly 20. (To have this creature explore, reveal the top card of your "
        + "library. Put that card into your hand if it's a land. Otherwise, put a +1/+1 counter on this "
        + "creature, then put the card back or put it into your graveyard.)")]
    [InlineData("Feral Encounter", "Sorcery",
        "Look at the top five cards of your library. You may exile a creature card from among them. Put the "
        + "rest on the bottom of your library in a random order. You may cast the exiled card this turn. At "
        + "the beginning of the next combat phase this turn, target creature you control deals damage equal to "
        + "its power to up to one target creature you don't control.")]
    [InlineData("Fable of the Mirror-Breaker", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter.)\nI — Create a 2/2 red Goblin "
        + "Shaman creature token with \"Whenever this token attacks, create a Treasure token.\"\nII — You may "
        + "discard up to two cards. If you do, draw that many cards.\nIII — Exile this Saga, then return it to "
        + "the battlefield transformed under your control.")]
    public void Classify_OneKeptOrAsManyAsThrownAway_IsFilterNotCardAdvantage(
        string name, string typeLine, string oracle)
    {
        var effect = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));
        Assert.True(effect.HasFlag(CardEffect.Filter));
        Assert.False(effect.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // The 2026-10-14 pass over the reviewed Filter misses. MILL N and keep one
    // "from among the milled cards".
    [InlineData("Seed of Hope", "Instant",
        "Mill two cards. You may put a permanent card from among the milled cards into your hand. You gain 2 "
        + "life. (To mill two cards, put the top two cards of your library into your graveyard.)")]
    [InlineData("Picklock Prankster", "Creature — Faerie Rogue",
        "Flying, vigilance\nMill four cards. Then put an instant, sorcery, or Faerie card from among the "
        + "milled cards into your hand.")]
    [InlineData("Wrenn and Realmbreaker", "Legendary Planeswalker — Wrenn",
        "Lands you control have \"{T}: Add one mana of any color.\"\n+1: Up to one target land you control "
        + "becomes a 3/3 Elemental creature with vigilance, hexproof, and haste until your next turn. It's "
        + "still a land.\n−2: Mill three cards. You may put a permanent card from among the milled cards into "
        + "your hand.\n−7: You get an emblem with \"You may play lands and cast permanent spells from your "
        + "graveyard.\"")]
    // Revealed and CHOSEN from among them.
    [InlineData("Green Sun's Twilight", "Sorcery",
        "Reveal the top X plus one cards of your library. Choose a creature card and/or a land card from "
        + "among them. Put those cards into your hand and the rest on the bottom of your library in a random "
        + "order. If X is 5 or more, instead put the chosen cards onto the battlefield or into your hand and "
        + "the rest on the bottom of your library in a random order.")]
    // A WHEEL of your own hand through the graveyard.
    [InlineData("Decaying Time Loop", "Instant",
        "Discard all the cards in your hand, then draw that many cards.\nRetrace (You may cast this card "
        + "from your graveyard by discarding a land card in addition to paying its other costs.)")]
    [InlineData("Path of the Pyromancer", "Sorcery",
        "Discard all the cards in your hand. Add {R} for each card discarded this way, then draw that many "
        + "cards plus one.\nWill of the Planeswalkers — Starting with you, each player votes for planeswalk or "
        + "chaos. If planeswalk gets more votes, planeswalk. If chaos gets more votes or the vote is tied, "
        + "chaos ensues.")]
    // MAP tokens made on every attack.
    [InlineData("Sentinel of the Nameless City", "Creature — Merfolk Warrior Scout",
        "Vigilance\nWhenever this creature enters or attacks, create a Map token. (It's an artifact with "
        + "\"{1}, {T}, Sacrifice this token: Target creature you control explores. Activate only as a "
        + "sorcery.\")")]
    [InlineData("Restless Anchorage", "Land",
        "This land enters tapped.\n{T}: Add {W} or {U}.\n{1}{W}{U}: Until end of turn, this land becomes a "
        + "2/3 white and blue Bird creature with flying. It's still a land.\nWhenever this land attacks, "
        + "create a Map token.")]
    // An explore made X TIMES is a look at X.
    [InlineData("Jadelight Spelunker", "Creature — Merfolk Scout",
        "When this creature enters, it explores X times. (To have it explore, reveal the top card of your "
        + "library. Put that card into your hand if it's a land. Otherwise, put a +1/+1 counter on that "
        + "creature, then put the card back or put it into your graveyard.)")]
    // Sentinel: a scry the card SACRIFICES itself for still counts (Grim Bauble,
    // Aang's Iceberg).
    [InlineData("Grim Bauble", "Artifact",
        "When this artifact enters, target creature an opponent controls gets -2/-2 until end of "
        + "turn.\n{2}{B}, {T}, Sacrifice this artifact: Surveil 2. (Look at the top two cards of your library, "
        + "then put any number of them into your graveyard and the rest on top of your library in any order.)")]
    [InlineData("Aang's Iceberg", "Enchantment",
        "Flash\nWhen this enchantment enters, exile up to one other target nonland permanent until this "
        + "enchantment leaves the battlefield.\nWaterbend {3}: Sacrifice this enchantment. If you do, scry 2. "
        + "(While paying a waterbend cost, you can tap your artifacts and creatures to help. Each one pays for "
        + "{1}.)")]
    public void Classify_TheOctoberFilterGaps_AreFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A LAND kept from the milled cards is ManaFixing (2026-09-27).
    [InlineData("Patient Naturalist", "Creature — Human Scout",
        "When this creature enters, mill three cards. Put a land card from among the milled cards into your "
        + "hand. If you can't, create a Treasure token. (To mill three cards, put the top three cards of your "
        + "library into your graveyard.)")]
    // THEIR wheel and THEIR loot are the opponent's, not selection of yours.
    [InlineData("Shocker", "Creature — Insect",
        "Whenever this creature deals damage to a player, that player discards all the cards in their hand, "
        + "then draws that many cards.")]
    [InlineData("Memory Worm", "Creature — Alien Worm",
        "Paradox — Whenever you cast a spell from anywhere other than your hand, this creature deals 2 "
        + "damage to target player. That player discards a card, then draws a card. Put a +1/+1 counter on "
        + "this creature.")]
    // ONE Map token made once is a rider, like one Blood.
    [InlineData("Cartographer's Companion", "Artifact Creature — Gnome",
        "When this creature enters, create a Map token. (It's an artifact with \"{1}, {T}, Sacrifice this "
        + "token: Target creature you control explores. Activate only as a sorcery.\")")]
    // Survivor of Korlis's scry, paid by exiling itself out of the graveyard,
    // stood here until 2026-11-06: paid with the card is what the card is for,
    // see Classify_TheNovember6Filter_ASelectionPaidWithTheCard_IsFilter.
    public void Classify_TheOctoberFilterGaps_AreNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Fact]
    public void Classify_HideawayBesideADrawEngine_IsFilterAndCardAdvantage()
    {
        // Hideaway is Filter on 5 of 5 reviewed cards, Wiretapping's draw
        // engine included (2026-10-14).
        var effect = EffectClassifier.Classify(MakeCard("Wiretapping", "Enchantment",
            "Hideaway 5 (When this enchantment enters, look at the top five cards of your library, exile one "
            + "face down, then put the rest on the bottom in a random order.)\nWhenever you draw your first card "
            + "during each of your draw steps, draw a card. Then if you have nine or more cards in hand, you may "
            + "play the exiled card without paying its mana cost."));
        Assert.True(effect.HasFlag(CardEffect.Filter));
        Assert.True(effect.HasFlag(CardEffect.CardAdvantage));
    }

    [Fact]
    // A loot the card spreads over TWO TRIGGERS is still one card for one card
    // on a timer, ruled 2026-09-22: Filter, and not CardAdvantage.
    public void Classify_ALootSpreadOverTwoTriggers_IsFilterAndNotCardAdvantage()
    {
        CardEffect effects = EffectClassifier.Classify(MakeCard(
            "Teferi's Imp", "Creature — Imp",
            "Flying\n"
            + "Phasing (This phases in or out before you untap during each of your untap steps. While it's "
            + "phased out, it's treated as though it doesn't exist.)\n"
            + "Whenever this creature phases out, discard a card.\n"
            + "Whenever this creature phases in, draw a card."));

        Assert.True(effects.HasFlag(CardEffect.Filter));
        Assert.False(effects.HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // M1/M5, 2026-10-16: draw-then-put-back that nets cards, and a repeating
    // exile-two-play-one, are CardAdvantage and no Filter.
    [InlineData("Brainsurge", "Instant",
        "Draw four cards, then put two cards from your hand on top of your library in any order.")]
    [InlineData("Fireglass Mentor", "Creature — Lizard Warlock",
        "At the beginning of your second main phase, if an opponent lost life this turn, exile the top two "
        + "cards of your library. Choose one of them. Until end of turn, you may play that card.")]
    public void Classify_TheOctober16Pass_ALootThatComesOutAhead_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // M5, 2026-10-16: one-shot and modal exile-two-play-one stay Filter.
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
    public void Classify_TheOctober16Pass_OneOfTwoOnce_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // C5, 2026-10-16: exile that many from the top, play one free. It was Cheat
    // too until 2026-11-03, when one card off the top cast once left Cheat
    // ("Mai Cheat"); see Classify_TheNovember3CheatPass_OneCardOffTheTopOnce_IsNotCheat.
    [InlineData("Vault 112: Sadistic Simulation", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — Tap "
        + "up to one target creature and put a stun counter on it. You get {E}{E} (two energy counters).\nIII — "
        + "Pay any amount of {E}. If you paid one or more {E} this way, shuffle your library, then exile that "
        + "many cards from the top. You may play one of those cards without paying its mana cost.")]
    public void Classify_TheOctober16Pass_ExileThatManyAndPlayOne_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-10-27, Filter by hand: a loot bullet on every attack is no draw
    // mode (Pip-Boy 3000), and a draw counted by what the card made you discard is
    // Fable's rummage (Indominus Rex, Alpha).
    [InlineData("Pip-Boy 3000", "Artifact — Equipment",
        "Whenever equipped creature attacks, choose one —\n• Sort Inventory — Draw a card, then discard a "
        + "card.\n• Pick a Perk — Put a +1/+1 counter on that creature.\n• Check Map — Untap up to two target "
        + "lands.\nEquip {2}")]
    [InlineData("Indominus Rex, Alpha", "Legendary Creature — Dinosaur Mutant",
        "As Indominus Rex enters, discard any number of creature cards. It enters with a flying counter on it "
        + "if a card discarded this way has flying. The same is true for first strike, double strike, "
        + "deathtouch, hexproof, haste, indestructible, lifelink, menace, reach, trample, and vigilance.\nWhen "
        + "Indominus Rex enters, draw a card for each counter on it.")]
    public void Classify_TheOctober27CardAdvantagePass_LootAndRummage_AreFilterNotCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // The Filter pass of 2026-10-24. A discard cost naming its card by a word
    // (Chandra's Regulator), the revealed put onto the battlefield without a may
    // (Smelting Vat), a creature card milled this way returned to hand (Monster
    // Manual), and a dig until a card type turns up made once (Artificer Class,
    // Yuna's Whistle) are read. The one-shot loot ruling of the same day stops at
    // a trigger that fires once: a loot on two events (Oracle of Tragedy), an
    // Adventure's spell (Tempest Hart), a connive handed over in quotes
    // (Copycrook, Security Bypass) and an activated loot (Oracle of Dust) keep the
    // tag.
    [InlineData("Chandra's Regulator", "Legendary Artifact",
        "Whenever you activate a loyalty ability of a Chandra planeswalker, you may pay {1}. If you do, copy "
        + "that ability. You may choose new targets for the copy.\n{1}, {T}, Discard a Mountain card or a red "
        + "card: Draw a card.")]
    [InlineData("Smelting Vat", "Artifact",
        "{1}, {T}, Sacrifice another artifact: Reveal the top eight cards of your library. Put up to two "
        + "noncreature artifact cards with total mana value less than or equal to the sacrificed artifact's "
        + "mana value from among them onto the battlefield and the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Monster Manual // Zoological Study", "Artifact — Book // Sorcery — Adventure",
        "{1}{G}, {T}: You may put a creature card from your hand onto the battlefield.\nMill five cards, then "
        + "return a creature card milled this way to your hand. (Then exile this card. You may cast the "
        + "artifact later from exile.)")]
    [InlineData("Artificer Class", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nThe first artifact spell you cast each turn "
        + "costs {1} less to cast.\n{1}{U}: Level 2\nWhen this Class becomes level 2, reveal cards from the top "
        + "of your library until you reveal an artifact card. Put that card into your hand and the rest on the "
        + "bottom of your library in a random order.\n{5}{U}: Level 3\nAt the beginning of your end step, create "
        + "a token that's a copy of target artifact you control.")]
    [InlineData("Yuna's Whistle", "Instant",
        "Reveal cards from the top of your library until you reveal a creature card. Put that card into your "
        + "hand and the rest on the bottom of your library in a random order. When you reveal a creature card "
        + "this way, put X +1/+1 counters on target creature you control, where X is the mana value of that "
        + "card.")]
    [InlineData("Oracle of Tragedy", "Creature — Human Wizard",
        "When this creature enters or dies, choose one —\n• Draw a card, then discard a card.\n• Shuffle up to "
        + "four target cards with mana value 3 or greater from your graveyard into your library.")]
    [InlineData("Tempest Hart // Scan the Clouds", "Creature — Elemental Elk // Instant — Adventure",
        "Trample\nWhenever you cast a spell with mana value 5 or greater, put a +1/+1 counter on this "
        + "creature.\nDraw two cards, then discard two cards. (Then exile this card. You may cast the creature "
        + "later from exile.)")]
    [InlineData("Copycrook", "Creature — Shapeshifter Rogue",
        "You may have this creature enter as a copy of any creature on the battlefield, except it has "
        + "\"Whenever this creature attacks, it connives.\" (Draw a card, then discard a card. If you discarded a "
        + "nonland card, put a +1/+1 counter on this creature.)")]
    [InlineData("Security Bypass", "Enchantment — Aura",
        "Enchant creature\nAs long as enchanted creature is attacking alone, it can't be blocked.\nEnchanted "
        + "creature has \"Whenever this creature deals combat damage to a player, it connives.\" (Its controller "
        + "draws a card, then discards a card. If they discarded a nonland card, they put a +1/+1 counter on "
        + "this creature.)")]
    [InlineData("Oracle of Dust", "Creature — Eldrazi Processor",
        "Devoid (This card has no color.)\n{2}, Put a card an opponent owns from exile into that player's "
        + "graveyard: Draw a card, then discard a card.")]
    public void Classify_TheOctober24FilterPass_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-10-24, No Filter, come lo scry singolo: a loot, a rummage or a
    // connive a permanent makes once is a rider, at any count and on a Saga's
    // chapter too (Revel Ruiner, Echo Inspector, Psychic Pickpocket, Raffine's
    // Informant, Kiora, Yuyan Archers, Founding of Omashu). Read the same day: an
    // opponent's loot (Balor), a scry only a tribe's spell pays out (Lapis Orb of
    // Dragonkind), two cards discarded for four (Sphinx of the Chimes), all of a
    // kind or a random one put onto the battlefield (Animist's Awakening, Getaway
    // Barrel), and a dig that repeats or is only a tribe's (Spinner of Souls,
    // Nissa, Resurgent Animist).
    [InlineData("Revel Ruiner", "Creature — Octopus Rogue",
        "Menace (This creature can't be blocked except by two or more creatures.)\nWhen this creature enters, "
        + "it connives. (Draw a card, then discard a card. If you discarded a nonland card, put a +1/+1 counter "
        + "on this creature.)")]
    [InlineData("Echo Inspector", "Creature — Bird Rogue",
        "Flying\nWhen this creature enters, it connives. (Draw a card, then discard a card. If you discarded a "
        + "nonland card, put a +1/+1 counter on this creature.)")]
    [InlineData("Psychic Pickpocket", "Creature — Octopus Rogue",
        "When this creature enters, it connives. When it connives this way, return up to one target nonland "
        + "permanent to its owner's hand. (To have a creature connive, draw a card, then discard a card. If you "
        + "discarded a nonland card, put a +1/+1 counter on that creature.)")]
    [InlineData("Raffine's Informant", "Creature — Human Wizard",
        "When this creature enters, it connives. (Draw a card, then discard a card. If you discarded a "
        + "nonland card, put a +1/+1 counter on this creature.)")]
    [InlineData("Kiora, the Rising Tide", "Legendary Creature — Merfolk Noble",
        "When Kiora enters, draw two cards, then discard two cards.\nThreshold — Whenever Kiora attacks, if "
        + "there are seven or more cards in your graveyard, you may create Scion of the Deep, a legendary 8/8 "
        + "blue Octopus creature token.")]
    [InlineData("Yuyan Archers", "Creature — Human Archer",
        "Reach\nWhen this creature enters, you may discard a card. If you do, draw a card.")]
    [InlineData("Founding of Omashu", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Create "
        + "two 1/1 white Ally creature tokens.\nII — You may discard a card. If you do, draw a card.\nIII — "
        + "Creatures you control get +1/+0 until end of turn.")]
    [InlineData("Balor", "Creature — Demon",
        "Flying\nWhenever this creature attacks or dies, choose one or more. Each mode must target a different "
        + "player.\n• Target opponent draws three cards, then discards three cards at random.\n• Target opponent "
        + "sacrifices a nontoken artifact of their choice.\n• This creature deals damage to target opponent "
        + "equal to the number of cards in their hand.")]
    [InlineData("Lapis Orb of Dragonkind", "Artifact",
        "{T}: Add {U}. When you spend this mana to cast a Dragon creature spell, scry 2. (Look at the top two "
        + "cards of your library, then put any number of them on the bottom and the rest on top in any order.)")]
    [InlineData("Sphinx of the Chimes", "Creature — Sphinx",
        "Flying\nDiscard two nonland cards with the same name: Draw four cards.")]
    [InlineData("Animist's Awakening", "Sorcery",
        "Reveal the top X cards of your library. Put all land cards from among them onto the battlefield "
        + "tapped and the rest on the bottom of your library in a random order.\nSpell mastery — If there are "
        + "two or more instant and/or sorcery cards in your graveyard, untap those lands.")]
    [InlineData("Getaway Barrel", "Artifact",
        "When this artifact is put into a graveyard from the battlefield, reveal the top thirteen cards of "
        + "your library. Put a random creature card from among them onto the battlefield. Put the rest on the "
        + "bottom of your library in a random order.")]
    [InlineData("Spinner of Souls", "Creature — Spider Spirit",
        "Reach\nWhenever another nontoken creature you control dies, you may reveal cards from the top of your "
        + "library until you reveal a creature card. Put that card into your hand and the rest on the bottom of "
        + "your library in a random order.")]
    [InlineData("Nissa, Resurgent Animist", "Legendary Creature — Elf Scout",
        "Landfall — Whenever a land you control enters, add one mana of any color. Then if this is the second "
        + "time this ability has resolved this turn, reveal cards from the top of your library until you reveal "
        + "an Elf or Elemental card. Put that card into your hand and the rest on the bottom of your library in "
        + "a random order.")]
    // A land card discarded for the draw (Soul of Windgrace, CardAdvantage alone),
    // a discard-first loot made once (Seasoned Pyromancer), and a dig for a land,
    // which is ManaFixing (House Cartographer).
    [InlineData("Soul of Windgrace", "Legendary Creature — Cat Avatar",
        "Whenever Soul of Windgrace enters or attacks, you may put a land card from a graveyard onto the "
        + "battlefield tapped under your control.\n{G}, Discard a land card: You gain 3 life.\n{1}{R}, Discard a "
        + "land card: Draw a card.\n{2}{B}, Discard a land card: Soul of Windgrace gains indestructible until "
        + "end of turn. Tap it.")]
    [InlineData("Seasoned Pyromancer", "Creature — Human Shaman",
        "When this creature enters, discard two cards, then draw two cards. For each nonland card discarded "
        + "this way, create a 1/1 red Elemental creature token.\n{3}{R}{R}, Exile this card from your graveyard: "
        + "Create two 1/1 red Elemental creature tokens.")]
    [InlineData("House Cartographer", "Creature — Human Scout Survivor",
        "Survival — At the beginning of your second main phase, if this creature is tapped, reveal cards from "
        + "the top of your library until you reveal a land card. Put that card into your hand and the rest on "
        + "the bottom of your library in a random order.")]
    public void Classify_TheOctober24FilterPass_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Readings of 2026-10-16: a look and a free cast is Filter beside its Cheat,
    // since the 10-13 ruling made Kiora both; a milled card put into your hand,
    // cast free or put onto the battlefield from among the milled; and a revealed
    // card put onto the battlefield from among them.
    [InlineData("Perception Bobblehead", "Artifact — Bobblehead",
        "{T}: Add one mana of any color.\n{3}, {T}: Look at the top X cards of your library, where X is the "
        + "number of Bobbleheads you control. You may cast a spell with mana value 3 or less from among them "
        + "without paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Terra, Magical Adept // Esper Terra", "Legendary Creature — Human Wizard Warrior // Legendary Enchantment Creature — Saga Wizard",
        "When Terra enters, mill five cards. Put up to one enchantment card milled this way into your "
        + "hand.\nTrance — {4}{R}{G}, {T}: Exile Terra, then return it to the battlefield transformed under its "
        + "owner's control. Activate only as a sorcery.\n(As this Saga enters and after your draw step, add a "
        + "lore counter.)\nI, II, III — Create a token that's a copy of target nonlegendary enchantment you "
        + "control. It gains haste. If it's a Saga, put up to three lore counters on it. Sacrifice it at the "
        + "beginning of your next end step.\nIV — Add {W}{W}, {U}{U}, {B}{B}, {R}{R}, and {G}{G}. Exile Esper "
        + "Terra, then return it to the battlefield (front face up).\nFlying")]
    [InlineData("Summons of Saruman", "Sorcery",
        "Amass Orcs X. Mill X cards. You may cast an instant or sorcery spell with mana value X or less from "
        + "among them without paying its mana cost. (To amass Orcs X, put X +1/+1 counters on an Army you "
        + "control. It's also an Orc. If you don't control an Army, create a 0/0 black Orc Army creature token "
        + "first.)\nFlashback—{3}{U}{R}, Exile X cards from your graveyard.")]
    [InlineData("Bramble Familiar // Fetch Quest", "Creature — Elemental Raccoon // Sorcery — Adventure",
        "{T}: Add {G}.\n{1}{G}, {T}, Discard a card: Return this creature to its owner's hand.\nMill seven "
        + "cards. Then put a creature, enchantment, or land card from among the milled cards onto the "
        + "battlefield.")]
    [InlineData("Knickknack Ouphe", "Creature — Ouphe",
        "This creature enters with X +1/+1 counters on it.\nWhen this creature enters, reveal the top X cards "
        + "of your library. You may put any number of Aura cards with mana value X or less from among them onto "
        + "the battlefield. Then put all cards revealed this way that weren't put onto the battlefield on the "
        + "bottom of your library in a random order.")]
    public void Classify_TheOctober16FilterPass_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // A connive beside a spell bounced is the rider of a counterspell, and a land
    // alone put onto the battlefield from the revealed is Ramp, not selection.
    [InlineData("Obscura Interceptor", "Creature — Octopus Wizard",
        "Flash\nLifelink\nWhen this creature enters, it connives. When it connives this way, return up to one "
        + "target spell to its owner's hand. (To have a creature connive, draw a card, then discard a card. If "
        + "you discarded a nonland card, put a +1/+1 counter on that creature.)")]
    [InlineData("Shigeki, Jukai Visionary", "Legendary Enchantment Creature — Snake Druid",
        "{1}{G}, {T}, Return Shigeki to its owner's hand: Reveal the top four cards of your library. You may "
        + "put a land card from among them onto the battlefield tapped. Put the rest into your "
        + "graveyard.\nChannel — {X}{X}{G}{G}, Discard this card: Return X target nonlegendary cards from your "
        + "graveyard to your hand.")]
    public void Classify_TheOctober16FilterPass_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // What the 2026-11-03 ruling does NOT reach: two cards paid for two or three
    // (Pumpkin Bombs, Falkenrath Pit Fighter, which also sacrifices a Vampire) and
    // a draw of two with one card back (Krovikan Sorcerer, 2026-09-19).
    [InlineData("Pumpkin Bombs", "Artifact",
        "{T}, Discard two cards: Draw three cards, then put a fuse counter on this artifact. It deals damage "
        + "equal to the number of fuse counters on it to target opponent. They gain control of this artifact.")]
    [InlineData("Falkenrath Pit Fighter", "Creature — Vampire Warrior",
        "{1}{R}, Discard a card, Sacrifice a Vampire: Draw two cards. Activate only if an opponent lost life "
        + "this turn.")]
    [InlineData("Krovikan Sorcerer", "Creature — Human Wizard Sorcerer",
        "{T}, Discard a nonblack card: Draw a card.\n{T}, Discard a black card: Draw two cards, then discard "
        + "one of them.")]
    public void Classify_TheNovember3CardAdvantagePass_APaidDrawThatNetsNothing_IsFilterNotCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // ONE card chosen from a MILL or an opponent's REVEAL put onto the battlefield
    // is selection, ruled 2026-11-03 ('3. Filter per le singole';
    // OneChosenFromAMillOrReveal): Vastlands Scavenger's Bind to Life and Guild
    // Feud, named, each Cheat before. A look was Filter by the older rules
    // already. Mole Module moved to CardAdvantage the same day, with Kinnan and
    // Owlbear Cub: one chosen again and again is a card ("Si ripete = CA").
    [InlineData("Vastlands Scavenger // Bind to Life", "Creature — Bear Druid // Instant",
        "Deathtouch\nThis creature enters prepared. (While it's prepared, you may cast a copy of its spell. "
        + "Doing so unprepares it.)\nMill seven cards. Then put a creature card from among them onto the "
        + "battlefield.")]
    // Guild Feud stood here until 2026-11-06: the opponent's reveal is their
    // choice, and "you do the same" every upkeep is a card — see
    // Classify_TheNovember6Filter_YouDoTheSameEveryUpkeep_IsCardAdvantage.
    public void Classify_TheNovember3Redefinition_OneChosenFromAMillOrAReveal_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-05: a spell whose own sentence is the connive, with X targets
    // (Change of Plans, Filter and Protection by hand), is X loots and not the
    // single selection made in passing of the 2026-09-27 rider ruling.
    [InlineData("Change of Plans", "Instant",
        "Each of X target creatures you control connive. You may have any number of them phase out. (To have "
        + "a creature connive, draw a card, then discard a card. If you discarded a nonland card, put a +1/+1 "
        + "counter on that creature. Treat phased-out permanents and anything attached to them as though they "
        + "don't exist until their controller's next turn.)")]
    public void Classify_TheNovember5Readings_ATargetedConniveOfTheSpell_IsFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Filter));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Ruled 2026-11-06 (Q6, "solo le tue carte"): a look at target opponent's or
    // each opponent's top chooses THEIR cards, not yours, so it is not this tag.
    // Overturns the 2026-09-27 reading that took an opponent's top in.
    [InlineData("Cruel Fate", "Sorcery",
        "Look at the top five cards of target opponent's library. Put one of those cards into that player's "
        + "graveyard and the rest on top of their library in any order.")]
    [InlineData("Sealed Fate", "Sorcery",
        "Look at the top X cards of target opponent's library. Exile one of those cards and put the rest back "
        + "on top of that player's library in any order.")]
    [InlineData("Precognition", "Enchantment",
        "At the beginning of your upkeep, you may look at the top card of target opponent's library. If you "
        + "do, you may put that card on the bottom of that player's library.")]
    public void Classify_TheNovember6Filter_OnlyAnOpponentsTop_IsNotFilter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Q6, 2026-11-06: target player's top can be your own, so it stays this tag.
    [InlineData("Ashnod's Cylix", "Artifact",
        "{3}, {T}: Target player looks at the top three cards of their library, puts one of them back on top "
        + "of their library, then exiles the rest.")]
    public void Classify_TheNovember6Filter_ATopThatCanBeYours_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-11-06 (Q7, "Role = Filter come Equipment"): a Sorcerer Role
    // grants a scry on every attack, the way an Equipment does, and the card that
    // makes the Role is the one that selects. Overturns the 2026-10-13 ruling that
    // the scry was the Role's.
    [InlineData("Unassuming Sage", "Creature — Human Peasant Wizard",
        "When this creature enters, you may pay {2}. If you do, create a Sorcerer Role token attached to it. "
        + "(Enchanted creature gets +1/+1 and has \"Whenever this creature attacks, scry 1.\")")]
    [InlineData("Spellbook Vendor", "Creature — Human Peasant",
        "Vigilance\nAt the beginning of combat on your turn, you may pay {1}. When you do, create a Sorcerer "
        + "Role token attached to target creature you control. (If you control another Role on it, put that one "
        + "into the graveyard. Enchanted creature gets +1/+1 and has \"Whenever this creature attacks, scry 1.\")")]
    [InlineData("Living Lectern", "Artifact Creature — Construct",
        "{1}, Sacrifice this creature: Draw a card. Create a Sorcerer Role token attached to up to one other "
        + "target creature you control. Activate only as a sorcery. (If you control another Role on it, put "
        + "that one into the graveyard. Enchanted creature gets +1/+1 and has \"Whenever this creature attacks, "
        + "scry 1.\")")]
    [InlineData("Splashy Spellcaster", "Creature — Elemental Wizard",
        "Whenever you cast an instant or sorcery spell, create a Sorcerer Role token attached to up to one "
        + "other target creature you control. (If you control another Role on it, put that one into the "
        + "graveyard. Enchanted creature gets +1/+1 and has \"Whenever this creature attacks, scry 1.\")")]
    public void Classify_TheNovember6Filter_ASorcererRolesScry_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-11-06 (Q8, "Pagata con la carta = Filter"): a scry the card pays
    // for by exiling itself out of the graveyard is what the card is for, not a
    // rider. Overturns the 2026-10-14 reading of a graveyard exile as once.
    [InlineData("Survivor of Korlis", "Creature — Human Soldier",
        "First strike\n{1}{W}, Exile this card from your graveyard: Scry 2.")]
    public void Classify_TheNovember6Filter_ASelectionPaidWithTheCard_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-11-06 (Q9, "Ovunque, come la Q5"): a draw and a discard unless a
    // condition is met gains a card when it is met and selects when it is not, so
    // it is both tags on any card, not only on a planeswalker (2026-10-13). The
    // planeswalkers keep both.
    [InlineData("Chakra Meditation", "Enchantment",
        "When this enchantment enters, return up to one target instant or sorcery card from your graveyard to "
        + "your hand.\nWhenever you cast an instant or sorcery spell, draw a card. Then discard a card unless "
        + "there are three or more Lesson cards in your graveyard.")]
    [InlineData("Shoreline Looter", "Creature — Rat Rogue",
        "This creature can't be blocked.\nThreshold — Whenever this creature deals combat damage to a player, "
        + "draw a card. Then discard a card unless there are seven or more cards in your graveyard.")]
    [InlineData("Wonderscape Sage", "Creature — Moonfolk Wizard",
        "Flying\n{T}, Return a land you control to its owner's hand: Draw a card. Then discard a card unless "
        + "that land had a nonbasic land type.")]
    [InlineData("Tainted Indulgence", "Instant",
        "Draw two cards. Then discard a card unless there are five or more mana values among cards in your "
        + "graveyard.")]
    [InlineData("Arm-Mounted Anchor", "Artifact — Equipment",
        "Equipped creature gets +2/+2 and has menace.\nWhenever equipped creature deals combat damage to a "
        + "player, draw two cards. Then discard two cards unless you discard a Pirate card.\nEquip {2}. This "
        + "ability costs {2} less to activate if you have one or fewer cards in hand.")]
    [InlineData("Winternight Stories", "Sorcery",
        "Draw three cards. Then discard two cards unless you discard a creature card.\nHarmonize {4}{U} (You "
        + "may cast this card from your graveyard for its harmonize cost. You may tap a creature you control to "
        + "reduce that cost by {X}, where X is its power. Then exile this spell.)")]
    [InlineData("Rush of Inspiration // Crackling Falls", "Instant // Land",
        "Draw two cards. Then discard a card at random unless you pay {E}{E} (two energy counters).\nThis land "
        + "enters tapped.\n{T}: Add {U} or {R}.")]
    [InlineData("Kaito Shizuki", "Legendary Planeswalker — Kaito",
        "At the beginning of your end step, if Kaito entered this turn, he phases out.\n+1: Draw a card. Then "
        + "discard a card unless you attacked this turn.\n−2: Create a 1/1 blue Ninja creature token with \"This "
        + "token can't be blocked.\"\n−7: You get an emblem with \"Whenever a creature you control deals combat "
        + "damage to a player, search your library for a blue or black creature card, put it onto the "
        + "battlefield, then shuffle.\"")]
    [InlineData("Tezzeret, Betrayer of Flesh", "Legendary Planeswalker — Tezzeret",
        "The first activated ability of an artifact you activate each turn costs {2} less to activate.\n+1: "
        + "Draw two cards. Then discard two cards unless you discard an artifact card.\n−2: Target artifact "
        + "becomes an artifact creature. If it isn't a Vehicle, it has base power and toughness 4/4.\n−6: You "
        + "get an emblem with \"Whenever an artifact you control becomes tapped, draw a card.\"")]
    public void Classify_TheNovember6Filter_DrawThenDiscardUnless_IsCardAdvantageAndFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Q9, 2026-11-06: when the failed condition still leaves more cards than it
    // took (draw three, discard one), the discard is a cost on a gain, not a
    // selection.
    [InlineData("Waterbending Lesson", "Sorcery — Lesson",
        "Draw three cards. Then discard a card unless you waterbend {2}. (While paying a waterbend cost, you "
        + "can tap your artifacts and creatures to help. Each one pays for {1}.)")]
    public void Classify_TheNovember6Filter_AnUnlessThatGainsEitherWay_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-11-06 (Q9 bis, "Conta + rumore"): an enters loot whose met
    // condition is draw two and discard one is parity on a creature (Racers'
    // Scoreboard, 2026-10-26), and the loot is a rider once, so neither tag.
    [InlineData("Steamcore Scholar", "Creature — Weird Detective",
        "Flying, vigilance\nWhen this creature enters, draw two cards. Then discard two cards unless you "
        + "discard an instant or sorcery card or a creature card with flying.")]
    [InlineData("Alpharael, Dreaming Acolyte", "Legendary Creature — Human Cleric",
        "When Alpharael enters, draw two cards. Then discard two cards unless you discard an artifact "
        + "card.\nDuring your turn, Alpharael has deathtouch.")]
    [InlineData("Splitskin Doll", "Artifact Creature — Toy",
        "When this creature enters, draw a card. Then discard a card unless you control another creature with "
        + "power 2 or less.")]
    public void Classify_TheNovember6Filter_AnUnlessMetAtParity_IsNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Ruled 2026-11-06 (Q5, "Guadagno condizionato = CA+Filter"): a selection made
    // every time and a card only sometimes is both tags, without naming the cards
    // (2026-10-13 named Kellan, Rumor Gatherer and the Sword).
    [InlineData("Kellan, Daring Traveler // Journey On", "Legendary Creature — Human Faerie Scout // Sorcery — Adventure",
        "Whenever Kellan attacks, reveal the top card of your library. If it's a creature card with mana "
        + "value 3 or less, put it into your hand. Otherwise, you may put it into your graveyard.\nCreate X Map "
        + "tokens, where X is one plus the number of opponents who control an artifact. (Then exile this card. "
        + "You may cast the creature later from exile.)")]
    [InlineData("Territory Culler", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nReach\nLandfall — Whenever a land you control enters, look at the "
        + "top card of your library. If it's a creature card, you may reveal it and put it into your hand. If "
        + "you don't put the card into your hand, you may put it into your graveyard.")]
    [InlineData("Rumor Gatherer", "Creature — Elf Wizard",
        "Alliance — Whenever another creature you control enters, scry 1. If this is the second time this "
        + "ability has resolved this turn, draw a card instead.")]
    [InlineData("Weatherlight Compleated", "Legendary Artifact — Vehicle",
        "Flying\nAs long as Weatherlight Compleated has four or more phyresis counters on it, it's a Phyrexian "
        + "creature in addition to its other types.\nWhenever a creature you control dies, put a phyresis "
        + "counter on Weatherlight Compleated. Then draw a card if it has seven or more phyresis counters on "
        + "it. If it doesn't, scry 1.")]
    [InlineData("Saheeli, Filigree Master", "Legendary Planeswalker — Saheeli",
        "+1: Scry 1. You may tap an untapped artifact you control. If you do, draw a card.\n−2: Create two 1/1 "
        + "colorless Thopter artifact creature tokens with flying. They gain haste until end of turn.\n−4: You "
        + "get an emblem with \"Artifact creatures you control get +1/+1\" and \"Artifact spells you cast cost {1} "
        + "less to cast.\"")]
    [InlineData("Sword of Once and Future", "Artifact — Equipment",
        "Equipped creature gets +2/+2 and has protection from blue and from black.\nWhenever equipped creature "
        + "deals combat damage to a player, surveil 2. Then you may cast an instant or sorcery spell with mana "
        + "value 2 or less from your graveyard without paying its mana cost. If that spell would be put into "
        + "your graveyard, exile it instead.\nEquip {2}")]
    [InlineData("Palantír of Orthanc", "Legendary Artifact",
        "At the beginning of your end step, put an influence counter on Palantír of Orthanc and scry 2. Then "
        + "target opponent may have you draw a card. If that player doesn't, you mill X cards, where X is the "
        + "number of influence counters on Palantír of Orthanc, and that player loses life equal to the total "
        + "mana value of those cards.")]
    public void Classify_TheNovember6Filter_AConditionalGain_IsCardAdvantageAndFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Q5, 2026-11-06: a top card that goes to the graveyard without a may chooses
    // nothing, and a scry then a draw on one activation is the draw's.
    [InlineData("Mirror of Galadriel", "Legendary Artifact",
        "{5}, {T}: Scry 1, then draw a card. This ability costs {1} less to activate for each legendary "
        + "creature you control.")]
    [InlineData("Bloodline Shaman", "Creature — Elf Wizard Shaman",
        "{T}: Choose a creature type. Reveal the top card of your library. If that card is a creature card of "
        + "the chosen type, put it into your hand. Otherwise, put it into your graveyard.")]
    public void Classify_TheNovember6Filter_AGainWithNoChoice_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-06: discard your hand (or up to X) then draw a card for each
    // card discarded hands back as many as it takes, the wheel the definition
    // names, so it is this tag and no card.
    [InlineData("Change of Fortune", "Sorcery",
        "Discard your hand, then draw a card for each card you've discarded this turn.")]
    [InlineData("Mishra's Command", "Sorcery",
        "Choose two —\n• Choose target player. They may discard up to X cards. Then they draw a card for each "
        + "card discarded this way.\n• This spell deals X damage to target creature.\n• This spell deals X damage "
        + "to target planeswalker.\n• Target creature gets +X/+0 and gains haste until end of turn.")]
    public void Classify_TheNovember6Filter_ADiscardThenADrawForEach_IsFilterNotCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.CardAdvantage));
        Assert.True(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // 2026-11-06: a draw for each card ANOTHER player discarded is still a card,
    // which the wheel reading above must not reach.
    [InlineData("Syphon Mind", "Sorcery",
        "Each other player discards a card. You draw a card for each card discarded this way.")]
    public void Classify_TheNovember6Filter_TheirDiscardsYourDraws_IsStillCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // Read 2026-11-06: Blood made again and again is a rummage (2026-10-24), and
    // "that many" Blood tokens is the same Blood.
    [InlineData("Jaws, Relentless Predator", "Legendary Creature — Shark",
        "Trample, haste\nWhenever Jaws deals combat damage to a player, create that many Blood tokens. "
        + "(They're artifacts with \"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\nWhenever a "
        + "noncreature artifact is sacrificed or destroyed, Jaws deals 1 damage to each opponent.")]
    [InlineData("Olivia's Attendants", "Creature — Vampire",
        "Menace\nWhenever this creature deals damage, create that many Blood tokens. (They're artifacts with "
        + "\"{1}, {T}, Discard a card, Sacrifice this token: Draw a card.\")\n{2}{R}: This creature deals 1 damage "
        + "to any target.")]
    public void Classify_TheNovember6Filter_ThatManyBloodAgainAndAgain_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-06: any number of a kind off the top onto the battlefield on
    // every attack keeps more than one, so it is a card and not a selection.
    [InlineData("Ayesha Tanaka, Armorer", "Legendary Creature — Human Artificer",
        "Whenever Ayesha Tanaka attacks, look at the top four cards of your library. You may put any number "
        + "of artifact cards with mana value less than or equal to Ayesha Tanaka's power from among them onto "
        + "the battlefield tapped. Put the rest on the bottom of your library in a random order.\nAyesha Tanaka "
        + "can't be blocked as long as defending player controls three or more artifacts.")]
    public void Classify_TheNovember6Filter_SeveralCappedOnEveryAttack_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // 2026-11-06: the same cap made once is still this tag; only the repeating
    // form left it.
    [InlineData("United Battlefront", "Sorcery",
        "Look at the top seven cards of your library. Put up to two noncreature, nonland permanent cards with "
        + "mana value 3 or less from among them onto the battlefield. Put the rest on the bottom of your "
        + "library in a random order.")]
    // A loyalty ultimate is once, so Ajani, Outland Chaperone's capped -8 is this
    // tag (unreviewed, read 2026-11-06).
    [InlineData("Ajani, Outland Chaperone", "Legendary Planeswalker — Ajani",
        "+1: Create a 1/1 green and white Kithkin creature token.\n−2: Ajani deals 4 damage to target tapped "
        + "creature.\n−8: Look at the top X cards of your library, where X is your life total. You may put any "
        + "number of nonland permanent cards with mana value 3 or less from among them onto the battlefield. "
        + "Then shuffle.")]
    public void Classify_TheNovember6Filter_SeveralCappedOnce_IsStillFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-06: up to two of the top X into your hand keeps more than one,
    // so it is a card.
    [InlineData("Shadow Prophecy", "Instant",
        "Domain — Look at the top X cards of your library, where X is the number of basic land types among "
        + "lands you control. Put up to two of them into your hand and the rest into your graveyard. You lose 2 "
        + "life.")]
    public void Classify_TheNovember6Filter_UpToTwoIntoYourHand_IsCardAdvantageNotFilter(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-06: the opponent's reveal is their choice (Q6), and "you do the
    // same" with your own top every upkeep puts a creature onto the battlefield
    // again and again, a card.
    [InlineData("Guild Feud", "Enchantment",
        "At the beginning of your upkeep, target opponent reveals the top three cards of their library, may "
        + "put a creature card from among them onto the battlefield, then puts the rest into their graveyard. "
        + "You do the same with the top three cards of your library. If two creatures are put onto the "
        + "battlefield this way, those creatures fight each other.")]
    public void Classify_TheNovember6Filter_YouDoTheSameEveryUpkeep_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-06: an upkeep look that keeps the card only if it is a kind,
    // and transforms once it does, selects every upkeep until it gains once: the
    // selection is the card's job.
    [InlineData("Sidequest: Catch a Fish // Cooking Campsite", "Enchantment // Land",
        "At the beginning of your upkeep, look at the top card of your library. If it's an artifact or "
        + "creature card, you may reveal it and put it into your hand. If you put a card into your hand this "
        + "way, create a Food token and transform this enchantment.\n{T}: Add {W}.\n{3}, {T}, Sacrifice an "
        + "artifact: Put a +1/+1 counter on each creature you control. Activate only as a sorcery.")]
    public void Classify_TheNovember6Filter_AKindIntoYourHandThenTransform_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }

    [Theory]
    // Read 2026-11-06: reveal until a kind or exile the top X, and you MAY put
    // that card onto the battlefield, is one chosen off the top (2026-11-03); the
    // must form (Aspiring Champion) is not.
    [InlineData("Hei Bai, Forest Guardian", "Legendary Creature — Bear Spirit",
        "When Hei Bai enters, reveal cards from the top of your library until you reveal a Shrine card. You "
        + "may put that card onto the battlefield. Then shuffle.\n{W}{U}{B}{R}{G}, {T}: For each legendary "
        + "enchantment you control, create a 1/1 colorless Spirit creature token with \"This token can't block "
        + "or be blocked by non-Spirit creatures.\"")]
    [InlineData("Anzrag's Rampage", "Sorcery",
        "Destroy all artifacts you don't control, then exile the top X cards of your library, where X is the "
        + "number of artifacts that were put into graveyards from the battlefield this turn. You may put a "
        + "creature card exiled this way onto the battlefield. It gains haste. Return it to your hand at the "
        + "beginning of the next end step.")]
    public void Classify_TheNovember6Filter_AMayOntoTheBattlefieldFromAReveal_IsFilter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Filter));
    }
}
