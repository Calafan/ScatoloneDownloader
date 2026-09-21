using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// Patterns for the tags about CARDS moving: CardAdvantage, Filter, Reanimate
    /// and Regrowth. Part of <see cref="EffectClassifier"/>.
    /// </summary>
    internal static partial class EffectClassifier
    {
        // Three shapes that LOOK like repeatable or multi-card draw and are not.
        // None of them is a new ruling — each follows from one already made, and
        // together they took CardAdvantage from 135 wrong out of 355 fired to 39
        // out of 263 (precision 62.0% -> 85.2%).
        //
        //   A loot draws and discards in the same breath. "Draw then discard is
        //   Filter alone" was ruled on 2026-09-04 and never applied on this side,
        //   so Bazaar of Baghdad and every "draw a card, then discard a card"
        //   vehicle counted as advantage.
        private static readonly Regex Loot = Rx(
            @"draws? [\w]+ (?:additional )?cards?, then discards?"
            + @"|discards? [\w]+ cards?[^\n.]{0,20}(?:if you do, )?draws? [\w]+ cards?"
            + @"|you may discard a card\. if you do, draw"
            // The same loot with the halves in a sentence each, which the two
            // patterns above cannot cross because neither lets a full stop
            // through. Confirmed as parity on 2026-09-20 by the ruling that put
            // Brainstorm back in Filter: Rook Turret and Oblivious Bookworm say
            // "you may draw a card. If you do, discard a card" and neither is
            // tagged, and Reckless Detective charges the card with a "sacrifice
            // an artifact or" in front of it.
            + @"|draws? \w+ cards?\. if you do, discards?"
            + @"|discards? \w+ cards?\. if you do, draws?");

        //   Cycling pays a card to replace itself: exactly parity. It fired only
        //   because its REMINDER text spells out an activated ability that draws.
        private static readonly Regex Cycling = Rx(@"discard this card: draw a card");

        //   And a REPLACEMENT draw adds nothing at all — it spends the draw you
        //   were going to have anyway on something else. Ruled 2026-09-21: all
        //   three reviewed cards written this way are untagged, and each is
        //   tagged for what it swaps the draw FOR instead (Aladdin's Lamp
        //   Filter, Mangara's Tome and Ring of Ma'rûf Tutor).
        private static readonly Regex ReplacesTheDraw = Rx(
            @"the next time you would draw a card");

        //   A REPEATABLE draw whose trigger you cannot actually repeat. The
        //   human put it as "il punto è proprio la difficoltà di trigger", and
        //   asked how hard that is to make readable: in general it is not —
        //   there is no wording that means "this rarely happens". But the four
        //   cards it was asked about each NAME their own difficulty, and each
        //   naming has no counterexample in the reviewed set.
        //
        //   A COIN FLIP is chance said outright (Goblin Artisans). Becoming the
        //   target of an AURA SPELL or of an ACTIVATED ABILITY means you must
        //   supply a second card before the draw happens at all (Fugitive Druid,
        //   Professor Hojo). And a draw bought by handing an opponent one of
        //   your permanents is paid for, not free (Stiltzkin).
        //
        //   The line this does NOT cross is Surrak, Elusive Hunter, which is
        //   tagged: it triggers on ANY spell or ability an opponent controls
        //   touching your creatures, which happens by itself. So the test is
        //   whether the trigger names something narrow, not whether it is a
        //   trigger. Ruled 2026-09-21, and it is four cards read one at a time
        //   rather than a principle — recorded as such.
        private static readonly Regex ADrawYouCannotCount = Rx(
            @"flip a coin[^\n]{0,80}draws? (?:a|one) card"
            + @"|becomes? the target of an (?:aura spell|activated ability)[^\n]{0,80}draws? (?:a|one) card"
            + @"|become the target of an activated ability, draws? (?:a|one) card"
            + @"|(?:opponent|player) gains? control of[^\n]{0,80}draws? (?:a|one) card");

        //   An ability that sacrifices the permanent runs once, so it is not the
        //   repeatable draw the 2026-09-11 ruling asked for — the same reading
        //   that keeps a one-shot sacrifice out of the Sacrifice tag.
        private static readonly Regex DrawBySacrificingItself = Rx(
            @"^[^\n:]{0,60}sacrifice this [\w]+[^\n:]{0,30}:", RegexOptions.Multiline);

        //   …except when the ability is printed INSIDE QUOTES and handed to a
        //   whole class of permanents, where "this permanent" is a different
        //   body every time and the card spends none of itself. Mnemonic Sliver
        //   gives every Sliver "{2}, Sacrifice this permanent: Draw a card" and
        //   is the only reviewed card written that way; the Clue token's
        //   identical reminder text is read by the Clue rules instead, which
        //   already ask whether the card makes one Clue or many. Same reading
        //   as OnlyPumpsInsideQuotes gives Buff. Added 2026-09-20.
        private static readonly Regex DrawGrantedToOtherPermanents = Rx(
            @"(?:(?:all|each) [\w' -]{0,20}|[\w' -]{0,20} you control) ha(?:s|ve) "
            + @"""[^""]{0,40}sacrifice this [\w]+[^""\n:]{0,20}: ?draw");

        //   …and when the card BUYS ITSELF BACK. Esoteric Duplicator's draw
        //   sacrifices the artifact, and the trigger above it turns that
        //   sacrifice into a token copy of the same artifact, so the ability is
        //   on the table again and the card was never spent. Ruled 2026-09-20
        //   after it was put to the human as the one card standing against the
        //   self-sacrifice rule; it is the only reviewed card written this way.
        private static readonly Regex SacrificeRefundedByACopy = Rx(
            @"whenever you sacrifice this [\w]+[^\n]{0,120}create a token that's a copy of that");

        //   And four more ways the same card is handed back, which the loot
        //   pattern above does not cover because none of them says "discard":
        //   putting cards from your hand on top (Dream Cache), shuffling one in
        //   (Lat-Nam's Legacy), paying back the card you just drew (Jandor's
        //   Ring), and a discard split across two sentences (Green Goblin,
        //   Alpharael). Added 2026-09-16.
        private static readonly Regex DrawPaidForWithACard = Rx(
            @"put \w+ cards? from your hand[^\n]{0,30}on top"
            + @"|shuffle (?:a|\w+) cards? from your hand into your library\. if you do, draw"
            + @"|discard the last card you drew"
            + @"|then discard \w+ cards? unless"
            + @"|discards? a card\. then draws? a card");

        // Casting out of a graveyard or out of exile is only advantage when you
        // can go back to it, ruled 2026-09-16 — one flashback card is one card.
        // The negative lookahead is what keeps warp and flashback REMINDER text
        // out: those say "you may cast THIS CARD", and from your hand at that.
        private static readonly Regex CastsAStreamFromElsewhere = Rx(
            // "From AMONG CARDS IN your graveyard" is the same permission with
            // three more words in it, and the Regrowth patterns already read it
            // that way. Banon, the Returners' Leader casts one creature out of
            // the graveyard on each of your turns and was missing the tag for
            // no better reason than the wording (2026-09-20).
            @"you may cast (?!this card|it\b)[\w ,'-]{0,40}(?:spells?|cards?) "
            + @"from (?:among )?(?:cards in )?your graveyard"
            + @"|you may (?:cast|play) (?!this card|it\b)[\w ,'-]{0,40}(?:spells?|cards?) from exile"
            + @"|(?:spells?|cards?)[\w ,'-]{0,20}(?:can be cast|may be cast) from your graveyard"
            // The same stream written as an impulse out of the graveyard rather
            // than a permission to cast from it: Tersa Lightshatter exiles a
            // card at random from your graveyard on every attack and lets you
            // play it. Ruled 2026-09-20 with Tersa's own reason — the loot half
            // of that card nets nothing and it is this attack trigger that
            // earns the tag. One reviewed card is written this way and it is
            // tagged; the shape is kept narrow on purpose, because "exile cards
            // from your graveyard" is also how escape and delve charge a cost.
            + @"|exiles? [\w ,'-]{0,30}cards? [\w ,'-]{0,20}from your graveyard"
            + @"[^\n]{0,60}you may play (?:that card|it)\b");

        // "Once during each of your turns" and "During your turn, you may" are
        // permissions that keep standing, so they are repeatable with no trigger
        // word for RepeatableWording to find.
        private static readonly Regex StandingPermission = Rx(
            @"once during each of your turns|during your turn, you may|each of your turns");

        //   The counting exception. A loot that draws THREE and discards one is
        //   not parity, it is a Careful Study with a bonus — the guard was written
        //   for the 1-for-1 case and was swallowing Emmessi Tome and Casting of
        //   Bones with it. Only a PROVABLE gain rescues the tag: both counts have
        //   to parse and the draw has to be the bigger one, so anything unreadable
        //   stays parity.
        //   Ruled again 2026-09-19, and this time from BOTH sides: the net is
        //   the whole answer. Any net gain is CardAdvantage and stops being
        //   Filter; a net of zero — discard two draw two — is Filter and never
        //   CardAdvantage. Measured over the reviewed set before the ruling: of
        //   the 41 cards that net nothing, 38 are Filter and 3 are advantage,
        //   and of the 4 that net two, 3 are advantage and 1 is Filter.
        //
        //   The counts have to be read more loosely than they were. "Draw three
        //   cards, then discard ONE OF THEM" never parsed because it does not
        //   repeat the word "card" (Casting of Bones, Soldevi Sage), and a full
        //   stop between the two halves stopped the match dead (Waterbending
        //   Lesson, Alpharael). Both now parse; an unreadable count still stays
        //   parity, which is what keeps "any number of cards" out.
        //   "If you do," is a third way of joining the two halves, and it comes
        //   after a full stop, so both counters have to be told about it (added
        //   2026-09-20 alongside the Loot patterns that read the same shape).
        private static readonly Regex DrawsThenDiscards = Rx(
            @"draws? (\w+) cards?[.,]? (?:then |and |if you do, )?(?:you may )?discards? (\w+)(?: of them| cards?)");

        private static readonly Regex DiscardsThenDraws = Rx(
            @"discards? (\w+) cards?[.,]?(?: if you do,)?[^\n.]{0,30}draws? (\w+) cards?");

        // Hoisted for the same reason as MillPatterns: the guard below re-runs
        // them against the card with the un-aimable recursion blanked out.
        private static readonly Regex[] ReanimatePatterns =
        [
            Rx(@"return[\w ,'\-/]{0,70}from[\w ,'\-/]{0,30}graveyard to the battlefield"),
            // The same effect written PUT instead of RETURN, with the graveyard
            // named any of the ways the game names it — "from a graveyard", "in
            // that player's graveyard", "from an opponent's graveyard". All six
            // reviewed cards written this way are tagged.
            Rx(@"put[\w ,'\-/]{0,60}creature cards? (?:from|in)[\w ,'\-/]{0,40}graveyard"
                + @"[\w ,'\-/]{0,40}onto the battlefield"),
            // A token COPY of a creature card in a graveyard. Not the card
            // itself, but what lands on the table is the same. Ruled
            // 2026-09-19; 4 reviewed cards, 3 already tagged.
            Rx(@"creature cards? (?:from|in)[\w ,'\-/]{0,40}graveyard"
                + @"[\s\S]{0,140}(?:token that's a copy|token cop|tokens that are copies)"),
        ];

        private static readonly Regex[] RegrowthPatterns =
        [
            Rx(@"return[\w ,'\-/]{0,70}from[\w ,'\-/]{0,30}graveyard to[\w ',]{0,20}hand"),
            Rx(@"put[\w ,'\-/]{0,60}cards? from[\w ,'\-/]{0,30}graveyard into[\w ]{0,20}hand"),
            // The TOP OF A LIBRARY is a hand you have to wait one turn for, so
            // a card lifted out of a graveyard and put there is recursion.
            // Ruled 2026-09-19; all 5 reviewed cards written this way are
            // tagged. The BOTTOM is not in it — putting a card on the bottom of
            // a library is graveyard hate, which is why Barkform Harvester and
            // Chrome Companion stay out. Nor is an OPPONENT'S graveyard, which
            // is Misinformation shuffling their answers back in, not recursion
            // for you.
            Rx(@"put (?:up to |any number of )?(?:\w+ )?target[\w ,'\-/]{0,40}cards? (?:from|in)"
                + @"[\w ,'\-/]{0,30}(?<!an opponent's )graveyard[\w ,'\-/]{0,20}on top of"),
            // CASTING ANOTHER CARD out of a graveyard, ruled 2026-09-19. It is
            // reanimation for spells: the card never reaches your hand, but it
            // is bought back from the same place and for the same reason.
            //
            // The card that gives ITSELF a second cast is NOT this, which is
            // what splits a family that first measured at 10% and looked like
            // noise. Flashback, escape, harmonize, unearth and the rest are
            // printed ON the card they apply to: 54 reviewed cards carry one
            // and exactly one is tagged Regrowth — Sorceress's Schemes, which
            // earns it on a different line by returning a card to hand. Read
            // apart from that, the aimed family is 12 cards and 5 were tagged.
            //
            // Three shapes wrecked earlier drafts and each is excluded here.
            // "Costs {1} less to cast FOR EACH creature card IN your graveyard"
            // is cost reduction, so the cast must say FROM. "As an additional
            // cost to cast THIS SPELL, exile X cards from your graveyard" is a
            // price, so the pronoun list refuses "this spell" too. And
            // "WHENEVER YOU CAST a spell from your graveyard" only watches one
            // happen, the shape that keeps "whenever you draw a card" out of
            // CardAdvantage.
            Rx(@"(?<!whenever you )(?:may )?cast (?!this card\b|this spell\b|it\b)"
                + @"(?!(?:[\w ,'\-/]{0,40})?for each)[\w ,'\-/]{0,50}(?:spells?|cards?) "
                + @"from (?:among )?(?:cards in )?(?:your|a|their|that player's) graveyard"),
            Rx(@"target[\w ,'\-/]{0,50}cards? in (?:your|a) graveyard gains? "
                + @"(?:flashback|escape|harmonize|jump-start)"),
            Rx(@"(?:each|all|any number of) [\w ,'\-/]{0,40}cards? in your graveyard ha(?:s|ve) "
                + @"(?:flashback|escape|harmonize|mayhem)"),
            Rx(@"(?:spells?|cards?)[\w ,'\-/]{0,20}(?:can be cast|may be cast) from your graveyard"),
        ];

        // MEASURED AND REJECTED 2026-09-19, recorded so it is not tried again.
        // A card that claws ITSELF back out of the graveyard — unearth, "{B}:
        // Return this card from your graveyard to the battlefield", the
        // discard-me-and-I-come-back cycle — looks like the self-pump that Buff
        // refuses and the self-shield that Protection refuses, and 16 of the 21
        // reviewed cards written that way carry no recursion tag. Vetoing them
        // anyway makes things WORSE: it buys 9 false positives for 5 true ones
        // but costs 7 points of recall across the two tags, and the exact
        // tag-set match falls. Hammer of Bogardan is tagged Regrowth and the
        // veto would have taken it. The split is a ruling nobody has made, not
        // a rule waiting to be written.

        // A card THIS CARD exiled, put onto the battlefield under your control.
        // Ruled 2026-09-19: the graveyard is not the only place a creature
        // comes back from, and Purgatory, Sothera, The Darkness Crystal and
        // Ghost Vacuum all take one that died or was already dead.
        //
        // Kept OUT of ReanimatePatterns and asked separately, because the two
        // halves live on different lines and the strip-and-re-ask guard blanks
        // the wrong one: what matches is the RETURN, and what disqualifies it is
        // the EXILE a line earlier. Blinking your OWN creature is not a
        // reanimation — Cold Storage and Safe Haven exile a creature you control
        // and hand it back, which is a strange kind of protection and is
        // deliberately left untagged. Without the guard the rule fires on 6
        // cards and is right about 4.
        private static readonly Regex ExiledCardOntoTheBattlefield = Rx(
            @"(?:put|return)[\w ,'\-/]{0,50}exiled with[\w ,'\-/]{0,40}(?:on)?to the battlefield");

        private static readonly Regex BlinksYourOwnCreature = Rx(
            @"exiles? target creature you control|exiles? (?:it|them|that creature)[\w ,']{0,30}return");

        // A land out of the graveyard is Ramp, ruled 2026-09-18 and read here
        // 2026-09-19: 7 reviewed cards put one back and only 1 is tagged
        // Reanimate. Summon: Titan and Will of the Sultai rebuild a mana base;
        // they do not reanimate anything.
        private static readonly Regex LandOutOfTheGraveyard = Rx(
            @"land cards? (?:from|in)[\w ,'\-/]{0,30}graveyard[\w ,'\-/]{0,20}(?:on)?to the battlefield[^\n.]{0,40}");

        // THE CARD ITSELF IS A CARD. Ruled 2026-09-19 and it completes the
        // count: Ponder is -1 for the Ponder and +1 for the draw, which is
        // zero, and Grab the Prize is -1 for the spell, -1 for the discard it
        // charges and +2 for the draw, which is also zero. A one-shot therefore
        // has to draw TWO more than it pays before it has gained anything,
        // while a REPEATABLE ability paid for the card once and never again and
        // needs only one. That is the whole difference between Emmessi Tome
        // ("{5}, {T}: Draw two cards, then discard a card", a card every
        // activation) and Racers' Scoreboard, which prints the same words on an
        // enters trigger and nets nothing.
        //
        // Repeatable means an activated ability you can use again — so an
        // ability that SACRIFICES the permanent does not count, which is why
        // Starting Column is read as the one-shot it is.
        private static bool TheLootRepeats(string text) =>
            (AnyAbilityWithACost.IsMatch(text) && !DrawBySacrificingItself.IsMatch(text))
            || RecurringTrigger.IsMatch(text);

        // …and a card that gives ITSELF a second cast is not one card either.
        // Ruled 2026-09-21, and it is the count again rather than a new idea:
        // Welcome the Dead draws two and discards one, which is nothing once
        // the spell is paid for, but with flashback it is cast twice for one
        // card and the sum is -3 +4. Winternight Stories does the same behind
        // harmonize, Whispers of the Muse behind buyback.
        //
        // This is the OPPOSITE of the Regrowth ruling on the same keywords,
        // deliberately: there, a card that rebuys itself is not recursion
        // because nothing was fetched; here, it really is a second helping of
        // the same draw, so it stops the "the card itself is a card"
        // subtraction from being charged twice.
        private static readonly Regex CastsItselfASecondTime = Rx(
            @"\b(?:flashback|harmonize|escape|jump-start|aftermath|buyback|rebound|retrace|encore)\b"
            + @"|shuffles? this card into its owner'?s library");

        private static readonly Regex AnyAbilityWithACost = Rx(@"^[^\n:]{1,70}:", RegexOptions.Multiline);

        private static readonly Regex RecurringTrigger = Rx(@"\bwhenever\b|at the beginning of");

        // Cards paid that the paired draw/discard patterns cannot see, because
        // they are spent in a different sentence or by a different verb. A
        // discard charged AS A COST is still a card (ruled 2026-09-19, the
        // ruling that corrected Krovikan Sorcerer), and so are the lands
        // Soldevi Sage feeds itself.
        private static readonly Regex AdditionalCostDiscard = Rx(
            @"as an additional cost to cast this spell,[^\n.]{0,60}discard");

        // The same thing charged by an ACTIVATION cost rather than a casting
        // one — "{T}, Discard a black card: Draw two cards, then discard one of
        // them" pays two cards for two and gains nothing (Krovikan Sorcerer).
        // Asked of the line, so a discard ability elsewhere on the card cannot
        // be billed to this loot; the lookahead keeps cycling reminder text out.
        private static readonly Regex ActivationCostDiscard = Rx(
            @"^[^\n:]{0,50}discard (?!this card)(?:a|one|two|three|\w+) (?:\w+ )?cards?[^\n:]{0,30}:",
            RegexOptions.Multiline);

        private static readonly Regex SacrificedLands = Rx(
            @"sacrifices? (a|one|two|three|four|five|\d+) lands?\b");

        // Whose draw is it? A card that only ever draws for somebody ELSE gives
        // nothing away for free — Sibilant Spirit and Harbor Guardian pay the
        // defending player for the privilege of attacking, and Lord of
        // Tresserhorn hands two cards across the table. "TARGET player" and
        // "EACH player" are deliberately NOT in this list: you point Ancestral
        // Recall and Braingeyser at yourself, and 12 of the 21 cards written that
        // way are tagged. Ruled 2026-09-19 over 10 cards, 8 of them untagged.
        // The subject sits IMMEDIATELY in front of the verb, with nothing between
        // but an optional "may". Twenty characters of filler let the rule read
        // "a creature you control becomes the target of a spell AN OPPONENT
        // CONTROLS, DRAW a card" (Surrak) as somebody else's draw, when "an
        // opponent" there is a possessive clause and the drawing is yours —
        // the same trap GivesControlAway fell into on 2026-09-19.
        // NB "that player" keeps its "may". Without it the rule reads Howling
        // Mine's "that player draws an additional card" as somebody else's draw,
        // when the whole point of the card is that it draws for YOU too.
        private static readonly Regex TheirDraw = Rx(
            @"(?:(?:defending player|its controller|an opponent|each opponent|target opponent"
            + @"|another player)(?: may)?|that player may) draws? "
            + @"(?:a|one|two|three|four|five|x|\d+|that many|cards)"
            // "At the beginning of each OPPONENT'S draw step, that player
            // draws an additional card" is Malignant Growth handing the
            // cards to them. Howling Mine prints the same clause behind
            // "each PLAYER's draw step" and deals you in, which is why the
            // step's owner is what this reads rather than the pronoun.
            + @"|at the beginning of each opponent'?s [\w ]{0,20}step, that player draws");

        // Asked by STRIPPING, not by a lookbehind. "Defending player may draw a
        // card" puts "may " between the subject and the verb, so a lookbehind
        // sees only "may " and lets the whole phrase through — which is how
        // Sibilant Spirit and Harbor Guardian survived the first cut of this
        // rule. Blank out every draw that belongs to somebody else first, then
        // ask whether any draw is left standing.
        private static readonly Regex YouDraw = Rx(
            @"\bdraws? (?:a|one|two|three|four|five|six|seven|x|\d+|that many|cards)");

        // Triggering OFF a draw is not drawing. Underworld Dreams and Clinquant
        // Skymage wait for a card to be drawn and then do something else; 9 of
        // the 10 cards written this way carry no tag.
        private static readonly Regex TriggersOffDrawing = Rx(
            @"whenever (?:you|an opponent|a player|another player|one or more players) draws?");

        // The sibling of DrawBySacrificingItself, and the same reading: a card
        // that exiles ITSELF out of your graveyard to draw runs exactly once.
        // The five identical Surveyors ("Max speed — {3}, Exile this card from
        // your graveyard: Draw a card") were four of the fifty over-fires.
        // 27 cards, 25 of them untagged.
        private static readonly Regex DrawByExilingItselfFromGraveyard = Rx(
            @"exile this card from your graveyard[^\n:]{0,30}:");

        // The top of your library as a second hand, ruled 2026-09-16 — the rule
        // was written then and the window was too short for the very cards the
        // ruling named. Glarb says "play lands AND CAST SPELLS WITH MANA VALUE 4
        // OR GREATER from the top of your library", 44 characters where 40 were
        // allowed, and Fblthp plots rather than plays. Widened 2026-09-19:
        // 15 cards fire, 14 of them tagged.
        private static readonly Regex SecondHandOnTop = Rx(
            @"(?:play|cast|plot)[\w ,'\d]{0,60}from the top of your library"
            + @"|look at the top card of your library any time"
            + @"|you may (?:play|cast)[^\n]{0,60}top card of your library");

        // Several cards off the top, or off THEIR top, that you may then play.
        // The one-card version is the impulse rider and stays out; this is the
        // draw-two wearing the same coat, which ImpulseOfSeveralCards already
        // says, plus the version aimed at an opponent's library (Outrageous
        // Robbery, Laughing Jasper Flint, Kotis). 15 cards, 14 tagged.
        // The window was 80 characters and Three Wishes puts 108 between the two
        // halves ("face down. You may look at those cards for as long as they
        // remain exiled. Until your next turn, you may play those cards").
        // Widened to 160 on 2026-09-20 and measured: 18 reviewed cards are
        // written this way, 17 of them tagged, and the one that is not is
        // Riverwheel Sweep, which ExileSeveralAndChooseOne takes out below.
        private static readonly Regex ExileSeveralAndPlayThem = Rx(
            @"exiles? the top (?:two|three|four|five|six|seven|eight|nine|ten|x|\d+) cards?"
            + @"[^\n]{0,160}(?:you may (?:play|cast)|may play|may cast)"
            + @"|exiles? the top \w+ cards? of (?:target |that )?(?:opponent|player)");

        // …and the shape that takes several off the top and lets you play only
        // ONE of them. Ruled 2026-09-20: you looked at two and played one, which
        // is the count of a Filter and not of a card gained — the same answer
        // the repeatable put-into-hand rule gives for a one-shot. All three
        // reviewed cards say it the same way: Riverwheel Sweep, Heroes' Hangout
        // and Case of the Burning Masks, the last of which prints it behind
        // "Sacrifice this Case" and so cannot repeat either.
        private static readonly Regex ExileSeveralAndChooseOne = Rx(
            @"exiles? the top \w+ cards? of your library[^\n]{0,60}choose one of them");

        // A Clue and an impulse draw are both "a card the opponent does not get",
        // and neither is a draw, so both are asked the same question: is it one
        // card, once? A single Clue has to be CASHED for {2} and a single impulse
        // replaces the card that made it, so each is a rider rather than a card.
        // They earn the tag when the card makes them REPEATEDLY, and an impulse
        // earns it outright when it exiles more than one card — that is a draw
        // two wearing a different coat. Ruled 2026-09-15.
        private static readonly Regex ClueWording = Rx(@"\binvestigates?\b|\bclue token");

        // Read UNANCHORED, and measured that way 2026-09-19: the trigger word
        // sits mid-line on half these cards (Obsessive Pursuit says "When this
        // enchantment enters AND at the beginning of your upkeep"), and
        // Sharp-Eyed Rookie puts 180 characters of condition between "Whenever"
        // and "investigate". Loose costs one card — Sophia — and buys five.
        // 15 fire, 14 tagged; the anchored version found 9.
        private static readonly Regex RepeatableClue = Rx(
            @"^[^\n:]{1,70}:[^\n]{0,120}(?:investigate|clue token)"
            + @"|(?:whenever|at the beginning of)[^\n]{0,200}(?:investigate|clue token)",
            RegexOptions.Multiline);

        // Several Clues in one breath, the same reading the Treasure got on
        // 2026-09-18: one has to be cashed for {2} and is a rider, but X of them
        // is a draw X wearing a different coat. Nyla and Tamiyo Meets the Story
        // Circle, both tagged.
        private static readonly Regex SeveralClues = Rx(
            @"create (?:two|three|four|five|x|\d+) clue tokens"
            + @"|investigates? (?:twice|three times|x times)"
            + @"|clue tokens?,? where x|investigate for each|clue token for each");

        // "Draw that many cards" is a draw-for-each written the other way round,
        // and the count is never one: Niv-Mizzet, Starwinder, Voracious
        // Bibliophile. Restricted to YOUR draw — "each player draws that many"
        // is a wheel (Teferi's Puzzle Box, Winds of Change) and pays everybody.
        // 12 fire, 9 tagged; without the restriction 20 fire and 11 tagged.
        private static readonly Regex DrawThatMany = Rx(
            @"you (?:may )?draw that many cards|, draw that many cards");

        private static readonly Regex ImpulseDraw = Rx(
            @"exiles? the top [\w ]{0,20}(?:card|cards) of your library"
            + @"[^\n]{0,90}(?:you may (?:play|cast)|may play (?:it|them|that card))");

        // More than one card off the top is a draw two, whatever it costs to cast.
        private static readonly Regex ImpulseOfSeveralCards = Rx(
            @"exiles? the top (?:two|three|four|five|six|seven|eight|nine|ten|x|\d+) cards? of your library");

        // Repeatable, read loosely ON PURPOSE. The anchored version of this test
        // misses "Battalion — Whenever ...", "Start your engines!" and a trigger
        // written inside a modal bullet, which between them are three of the seven
        // one-card impulses in the reviewed set and all three are tagged. Used only
        // once an impulse has already matched, so the looseness costs nothing.
        private static readonly Regex RepeatableWording = Rx(
            @"\bwhenever\b|at the beginning of|^[^\n:]{1,70}:", RegexOptions.Multiline);

        // …and the reason the looseness is no longer free, found 2026-09-20.
        // Asked of the WHOLE CARD it reads repeatability off whichever ability
        // happens to carry a trigger word, and that is a different ability:
        // Equilibrium Adept exiles one card off an ENTERS trigger and has a
        // "Flurry — Whenever you cast your second spell" line underneath that
        // has nothing to do with it. Guru Pathik does the same thing to the
        // put-into-hand rule below. So the question is asked of ONE ABILITY.
        //
        // An ability is a line, except that a modal bullet belongs to the
        // trigger that introduced it — Parapet Thrasher puts "Whenever one or
        // more Dragons you control deal combat damage" on one line and the
        // impulse three lines down as "• Exile the top card of your library".
        // Splitting on newlines alone would read that as a one-shot, which is
        // the very failure the comment above warns about, so the bullets are
        // stitched back on.
        private static IEnumerable<string> Abilities(string text)
        {
            List<string> abilities = [];
            foreach (string line in text.Split('\n'))
            {
                if (line.TrimStart().StartsWith('•') && abilities.Count > 0)
                {
                    abilities[^1] += "\n" + line;
                }
                else
                {
                    abilities.Add(line);
                }
            }

            return abilities;
        }

        // A Saga chapter that names more than one number fires more than once:
        // Rediscover the Way's "I, II — Look at the top three cards of your
        // library" looks at six cards over two turns and keeps two of them.
        private static readonly Regex ChapterFiresTwice = Rx(@"^[ivx]+, [ivx]+ ", RegexOptions.Multiline);

        private static bool AbilityRepeats(string ability) =>
            RepeatableWording.IsMatch(ability) || ChapterFiresTwice.IsMatch(ability);

        // The same question with the two self-consuming costs subtracted. An
        // ability that spends the card to pay for itself runs once however it
        // is worded — Morbius the Living Vampire exiles itself out of the
        // graveyard, Lupinflower Village sacrifices itself — and the impulse
        // family deliberately does NOT ask this, because there the cost is
        // often paid by something else: Junktown makes three Junk tokens and
        // each token sacrifices ITSELF for a card, which is three cards.
        private static bool AbilityRepeatsAtNoCostToItself(string ability) =>
            AbilityRepeats(ability)
            && !DrawBySacrificingItself.IsMatch(ability)
            && !SpendsItselfWithoutACostLine.IsMatch(ability)
            && !DrawByExilingItselfFromGraveyard.IsMatch(ability);

        // The self-sacrifice written as an EFFECT rather than as a cost, so
        // there is no colon for DrawBySacrificingItself to find: Preferred
        // Selection looks at two cards every upkeep but only keeps one by
        // saying "You may sacrifice this enchantment and pay {2}{G}{G}",
        // which it can do exactly once. Added 2026-09-21.
        private static readonly Regex SpendsItselfWithoutACostLine = Rx(
            @"sacrifice this (?:creature|permanent|artifact|enchantment|land|token|card)");

        // "Look at the top few cards of your library and put one INTO YOUR
        // HAND." Ruled 2026-09-20, and the ruling is the repeatable one again:
        // the one-shot version pays a card to move a card and nets nothing, so
        // it is Filter, while the ability you can use every turn bought the
        // card once and hands you one more each time. The four cards asked
        // about split exactly on that line — Browse and Beastrider Vanguard and
        // Water Tribe Rallier all have a cost and a colon, and Morbius the
        // Living Vampire prints the same words behind "Exile this card from
        // your graveyard", which is once.
        //
        // This was measured and REJECTED a day earlier, on 8 tagged against 9
        // untagged, and the ruling is what changed: asked of one ability rather
        // than of the whole card, the split stops being 8/9 and becomes 9 cards
        // that repeat — 5 of them already tagged — against 11 one-shots, 10 of
        // them untagged. The only card the rule still misses is Memories
        // Returning, which is a one-shot that puts THREE cards in your hand and
        // is therefore advantage by the count, not by repetition.
        // "THAT MANY CARDS FROM THE TOP of your library" is the same look with
        // the count carried in from the trigger, and the rule could not read it:
        // Symbiote Spider-Man, Choco and Stargaze all say it. Added 2026-09-21.
        private static readonly Regex TopFewIntoYourHand = Rx(
            @"(?:look at|reveal) (?:the top \w+ cards?|that many cards|twice \w+ cards) "
            + @"(?:of|from the top of) your library"
            + @"[^\n]{0,120}put (?:one of them|it|that card|\w+ cards? from among them|\w+ of those cards) "
            + @"into your hand");
    }
}
