using System.Globalization;
using System.Text.RegularExpressions;

using ScatoloneDownloader.Json.Cards;
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
            + @"|discards? \w+ cards?\. if you do, draws?"
            // …and the halves joined by THEN across a full stop, which is the
            // same sentence pair with the condition left out. Ill-Timed
            // Explosion draws two and hands two back, and was reading as
            // advantage on top of the Filter it already had.
            + @"|draws? \w+ cards?\. then you may discards? \w+ cards?"
            // Two more named Filter by the human on 2026-09-27: a LAND may stand
            // in for the card (Highway Robbery), and N for N is still level
            // (Horrid Shadowspinner draws its power and discards as many).
            + @"|discards? a card or sacrifice (?:a|an) \w+\. if you do, draws?"
            + @"|draw cards equal to [^.\n]{1,40}\. if you do, discard that many cards");

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
        //   Professor Hojo). A draw bought by handing an opponent one of your
        //   permanents was read the same way (Stiltzkin) until 2026-10-14,
        //   when the human ruled it a card after all — see below.
        //
        //   The line this does NOT cross is Surrak, Elusive Hunter, which is
        //   tagged: it triggers on ANY spell or ability an opponent controls
        //   touching your creatures, which happens by itself. So the test is
        //   whether the trigger names something narrow, not whether it is a
        //   trigger. Ruled 2026-09-21, and it is four cards read one at a time
        //   rather than a principle — recorded as such.
        private static readonly Regex ADrawYouCannotCount = Rx(
            @"flip a coin[^\n]{0,80}draws? (?:a|one) card"
            // Mister Negative draws only when the life he swapped left him
            // WORSE OFF, which is the same "you cannot ask for this" the coin
            // flip is; he is hand-tagged Steal alone.
            + @"|exchange life totals[^\n]{0,80}draws? that many cards"
            + @"|becomes? the target of an (?:aura spell|activated ability)[^\n]{0,80}draws? (?:a|one) card"
            + @"|become the target of an activated ability, draws? (?:a|one) card"
            // A creature dealing combat damage to its OWNER needs a creature
            // that was handed to somebody else, a trigger the card cannot count
            // on (the trigger-difficulty family, one card at a time): The Beast,
            // Deathless Prince, the one reviewed card that says it, is Steal
            // alone by hand (read 2026-10-16).
            + @"|combat damage to its owner,[^\n]{0,40}draws? (?:a|one) card"
            // THE CARD ITSELF becoming the target of an opponent's spell or
            // ability is a tax they pay, not a card you gained: Altanak and
            // Cactarantula, 0 of 2 tagged (read 2026-10-16). "A creature you
            // control" is the other reading and stays (Surrak, Elusive Hunter
            // and Elrond, Master of Healing, 2 of 2 tagged): it happens by
            // itself, to any of a board of creatures. The subject may not open
            // with an article or a count, which is what separates the card
            // from the board.
            + @"|\bwhenever (?!an? |another |one or more |each |creatures? |you\b)[^,\n]{0,40}? becomes? the target of a spell or ability "
            + @"an opponent controls, (?:you may )?draws? (?:a|one) card"
            // Handing a permanent of yours to another player for the card left
            // this list on 2026-10-14 ("Tutti e due CA"): Discerning Financier
            // gives away a Treasure and Stiltzkin, Moogle Merchant any permanent,
            // and both draw. Yes Man, Personal Securitron, tagged, gives ITSELF.
            // Two more named difficulties, confirmed by the human on
            // 2026-09-22 ("Confermo la difficoltà di trigger"). BENDING is a
            // keyword action only a handful of cards in one set can perform, so
            // "whenever you waterbend, earthbend, firebend, or airbend, draw a
            // card" is a draw your deck has to be built to reach (Avatar Aang,
            // the only reviewed card that says it, untagged). And a CHOSEN CARD
            // NAME asks you to name a card and then meet it: The Clone Saga
            // draws only if a creature with the name you picked connects, and
            // that same turn. Two reviewed cards name a card this way and
            // neither is tagged. Still four-plus-two cards read one at a time
            // rather than a principle. Diviner's Lockbox guesses the top card
            // by name for three, and is the same difficulty ("No CA, è
            // difficoltà", 2026-10-23); its self-sacrifice already keeps it out.
            + @"|whenever you waterbend, earthbend, firebend, or airbend"
            + @"|choose a card name\.[^\n]{0,120}with the chosen name"
            // A draw an OPPONENT buys for you: Hold for Ransom hands the
            // creature it holds "{7}: Hold for Ransom's controller sacrifices
            // it and draws a card", the price of letting it go (Pacify alone
            // by hand, read 2026-10-23).
            + @"|'s controller sacrifices it and draws (?:a|one) card");

        //   And a draw the OTHER SIDE gets in the same breath and the same
        //   number. The parity ruling of 2026-09-20 was written for one player
        //   trading a card for a card; this is the two-player version, where
        //   the count is level across the table and neither side has gained on
        //   the other. Three reviewed cards say it and none is tagged: Tataru
        //   Taru draws one each, The Legend of Yangchen offers three for three,
        //   and Mog, Moogle Warrior runs a loot that every player may take.
        //
        //   This does NOT reach "TARGET player draws two cards" or "each player
        //   draws a card", for the same reason TheirDiscardYourDraw does not:
        //   you point Ancestral Recall at yourself, and a wheel that pays
        //   everybody is read by TheirDraw instead.
        private static readonly Regex EverybodyDrawsTheSame = Rx(
            @"you draws? (?:a|one|\w+) cards? and (?:target |an )?opponent may draw"
            + @"|you may have target opponent draw \w+ cards?\. if you do, draws?"
            + @"|each player[\w ,'-]{0,30}discards?[^\n]{0,60}"
            + @"each player who discarded[^\n]{0,40}draws?");

        // EXILE AND PLAY IT FROM THERE, in the five wordings the tag could not
        // read. Ruled 2026-09-21: the human confirmed the whole family is
        // CardAdvantage, and it was the single biggest hole left — twenty
        // reviewed cards, every one tagged, every one missed, because each
        // writes the same idea differently.
        //
        //   1. Dig until a nonland card turns up and play it (Solstice
        //      Revelations, Breaching Dragonstorm). DISCOVER spells the same
        //      thing out in its reminder text, and joined this pattern until
        //      2026-09-29 — see Discovers for why it is asked apart now, and
        //      why this pattern is asked with reminder text blanked.
        //   2. A standing permission over a growing pile: "you may play cards
        //      exiled with this creature" (Evendo Brushrazer, Urianger, Wheel of
        //      Potential, Triple Triad).
        //   3. The pile going to your HAND instead (Necropotence, Duplicity,
        //      Knowledge Vault).
        //   4. Casting from among the exiled cards for free (Kefka, Portent of
        //      Calamity, Etrata).
        //   5. The top of SOMEBODY ELSE'S library, which the several-cards rule
        //      already read for a count but not for one card (Vaan).
        private static readonly Regex ExileAndPlayFromThere = Rx(
            @"exiles? cards? from the top of your library until you exile a nonland card"
            + @"|(?:play|cast) (?:any number of |all |the )?(?:cards?|spells?|them)"
            + @"[\w ,'-]{0,30}exiled (?:with|this way|by)"
            // …and the same permission naming the pile in the singular, which
            // is how a card that exiles ONE card writes it: "you may cast the
            // exiled card without paying its mana cost" (Etrata).
            + @"|you may cast the exiled cards?"
            + @"|exiles? the top card of your library face down[^\n]{0,90}(?:put )?that card into your hand"
            // "Into their OWNERS' HANDS", the plural, and "exiled with IT":
            // Connecting the Dots and Expert-Level Safe, CardAdvantage by
            // hand, were missed on those two words (read 2026-10-16, 2 of 2).
            // Only "ALL cards exiled with it": Toluz, Clever Conductor returns
            // what its discards left behind when it dies, and is a Filter.
            + @"|cards?[\w ]{0,12}exiled with this [\w]+ into (?:your|their owners?'?s?'?) hands?"
            + @"|all cards exiled with it into (?:your|their owners?'?s?'?) hands?"
            + @"|(?:cast|play) (?:a |any number of )?(?:spells?|cards?)[\w ,'-]{0,40}"
            + @"from among (?:the |cards )?exiled"
            // The count is optional here too: Vaan exiles THE TOP CARD of the
            // player he hit and says "You may cast it", one card at a time and
            // every combat. "Their library" joins "that player's" because
            // Outrageous Robbery writes the same sentence that way, and the
            // permission is allowed a few words of its own ("You may look at
            // and play those cards").
            // The window is 40 and not 80, and the permission may not be for
            // ONE OF the cards. Both were measured: at 80 the pattern reaches
            // past a price into a permission on the far side of it (Reno and
            // Rude charges a creature or artifact for the card it exiled and is
            // Steal), and "you may play ONE of those cards" is the several-for-
            // one count that ExileSeveralAndChooseOne already rules is a Filter
            // (Fire Lord Ozai).
            + @"|exiles? the top (?:\w+ )?cards? of (?:that|target|each|their) ?(?:player|opponent)?'?s? ?library"
            + @"[^\n]{0,40}(?:you )?may [\w ]{0,25}(?:cast|play)(?! one of those)");

        // A one-card draw on a spell that can be CAST TWICE is two cards for
        // one, which is the same count Welcome the Dead's flashback makes.
        // Unending Whisper is "Draw a card." plus harmonize and nothing else.
        private static readonly Regex OneDrawButTwoCasts = Rx(@"^draw a card\.", RegexOptions.Multiline);

        // …unless the pile is THEIRS. The second wording above reads "you may
        // cast cards exiled with Azula", and Azula exiles a creature an
        // opponent controls and a card from their graveyard — which is Steal
        // and Removal, and is hand-tagged as exactly that. Crabomination is the
        // same card with three zones. What separates them from Vaan and
        // Outrageous Robbery, which ARE tagged CardAdvantage, is that those two
        // take the top of an opponent's LIBRARY: cards nobody had yet.
        private static readonly Regex ThePileIsTheirsAlready = Rx(
            @"exiles? an? [\w ]{0,20}(?:creature|permanent) they control"
            // Crabomination reaches three zones in one sentence and puts the
            // library first, so the graveyard half sits 32 characters in where
            // 25 were allowed; the hand is the same robbery in a third place.
            + @"|exiles? [\w ,]{0,60}card at random from their (?:graveyard|hand)"
            // Their GRAVEYARD said the long way round, which is the commonest
            // wording of all: "exile up to one card from that player's
            // graveyard" and then a permission to cast it (Hama, the
            // Bloodbender). Six reviewed cards reach into a named opponent's
            // graveyard and not one of them is CardAdvantage — they are
            // Reanimate and Steal, because the card was already somebody's.
            + @"|cards? from (?:that|target|an) (?:player|opponent)'?s graveyard"
            // …and their SPELL, exiled off the stack and cast for free:
            // Smirking Spelljacker, the one reviewed card written this way,
            // is Counter and Steal by hand and not CardAdvantage (read
            // 2026-10-16, 0 of 1 tagged). Any target spell too: Gale's
            // Redirection, Counter and Steal by hand (read 2026-10-23).
            + @"|exile target spell\b");

        //   An ability that sacrifices the permanent runs once, so it is not the
        //   repeatable draw the 2026-09-11 ruling asked for — the same reading
        //   that keeps a one-shot sacrifice out of the Sacrifice tag.
        //
        //   The sacrificing line has to be the line that DRAWS, added
        //   2026-09-21. Asked of the whole card it billed a draw to whatever
        //   ability happened to eat the permanent, which is a different ability:
        //   Cryogen Relic draws when it enters and again when it leaves, and
        //   separately sacrifices itself to stun a creature, and was losing the
        //   tag to the stun. Same correction RepeatableWording needed on
        //   2026-09-20, and for the same reason.
        private static readonly Regex DrawBySacrificingItself = Rx(
            @"^[^\n:]{0,60}sacrifice this [\w]+[^\n:]{0,30}:[^\n]{0,90}draw", RegexOptions.Multiline);

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
            // The card may be DESCRIBED between its noun and the zone: Seifer
            // Almasy casts "target instant or sorcery card WITH MANA VALUE 3 OR
            // LESS from your graveyard" on every hit, and the window stopped at
            // the noun.
            // A land may come first: Kagha, Shadow Archdruid's "Once during
            // each of your turns, you may play a land or cast a permanent
            // spell from among cards in your graveyard …" (read 2026-10-30;
            // 9 of 10 reviewed standing permissions to cast from the graveyard
            // are tagged, The Eighth Doctor the one that is not).
            @"you may (?:play a land or )?cast (?!this card|it\b)[\w ,'-]{0,40}(?:spells?|cards?)[\w ,'-]{0,40} "
            + @"from (?:among )?(?:cards in )?your graveyard"
            // Granting FLASHBACK to a graveyard wholesale is the same standing
            // permission written as a keyword — Iroh, Grand Lotus gives it to
            // every instant and sorcery you own during your turn.
            + @"|cards? in your graveyard ha(?:s|ve) flashback"
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
            + @"[^\n]{0,60}you may play (?:that card|it)\b"
            // The card CHOSEN in your graveyard first and cast after: Conduit
            // of Worlds, on every tap (tagged, 2026-10-14).
            + @"|choose target [\w ]{0,30}card in your graveyard[^\n]{0,80}you may cast that card"
            // Out of ANY graveyard, every turn (Coram, the Undertaker, tagged,
            // read 2026-10-27, 1 of 1: their graveyard on a repeat is this tag
            // by the ruling of 2026-10-16, Saruman of Many Colors).
            + @"|you may (?:play|cast) [\w ,'-]{0,40}(?:spells?|cards?) from among cards in graveyards");

        // "Once during each of your turns" and "During your turn, you may" are
        // permissions that keep standing, so they are repeatable with no trigger
        // word for RepeatableWording to find.
        private static readonly Regex StandingPermission = Rx(
            @"once during each of your turns|during your turn,|each of your turns");

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

        private static readonly Regex DrawsThenPutsBack = Rx(
            @"draws? (\w+) cards?, then put (\w+) cards? from your hand on top of your library");

        private static readonly Regex DiscardsThenDraws = Rx(
            @"discards? (\w+) cards?[.,]?(?: if you do,)?[^\n.]{0,30}draws? (\w+) cards?");

        // Hoisted for the same reason as MillPatterns: the guard below re-runs
        // them against the card with the un-aimable recursion blanked out.
        private static readonly Regex[] ReanimatePatterns =
        [
            // 90, not 70, as Regrowth's: Carmen, Cruel Skymarcher's "return up to
            // one target permanent card WITH MANA VALUE LESS THAN OR EQUAL TO
            // CARMEN'S POWER from your graveyard" runs to 86, and the human
            // tagged it Reanimate on 2026-09-29.
            // 130 and "OF your graveyard" read 2026-10-13, with every card
            // tagged by hand: Relive the Past lists three targets before its
            // "from" (Fiendish Panda describes one at length), Shallow Grave and
            // Corpse Dance take "the top creature card OF your graveyard", and
            // Bringer of the Last Gift's cards "that weren't put there this way"
            // stand between the graveyard and the battlefield.
            Rx(@"return[\w ,'\-/]{0,130}(?:from|of)[\w ,'\-/]{0,30}graveyards?(?: that [\w ']{0,40})? to the battlefield"),
            // The same effect written PUT instead of RETURN, with the graveyard
            // named any of the ways the game names it — "from a graveyard", "in
            // that player's graveyard", "from an opponent's graveyard". All six
            // reviewed cards written this way are tagged.
            // Read 2026-10-13: the card DESCRIBED before its zone (Coalstoke
            // Gearhulk's "with mana value 4 or less", Staff of Eden's "not
            // named …"), "creature or Vehicle card" (Fire Nation Salvagers), a
            // PERMANENT card (Nezumi Freewheeler, Staff of Eden) and "the top
            // creature card OF defending player's graveyard" (Bone Dancer) —
            // 6 of 6 reviewed, all tagged. Whole words, or the IN of
            // "defending" would stand in for the OF.
            Rx(@"put[\w ,'\-/]{0,60}\b(?:creature|permanent)\b[\w ,'\-/]{0,40}?\bcards?\b[\w ,'\-/]{0,50}?\b(?:from|in|of)\b"
                + @"[\w ,'\-/]{0,40}graveyards?[\w ,'\-/]{0,40}onto the battlefield"),
            // A token COPY of a creature card in a graveyard. Not the card
            // itself, but what lands on the table is the same. Ruled
            // 2026-09-19; 4 reviewed cards, 3 already tagged.
            // Widened 2026-10-13 the same way, and for a PERMANENT card copied
            // (Sin, Spira's Punishment, Lazotep Quarry) and the creature card
            // "exiled this way" out of a graveyard (Espers to Magicite).
            Rx(@"(?:creature|permanent) cards?\b[\w ,'\-/]{0,40}?(?:from|in)[\w ,'\-/]{0,40}graveyard"
                + @"[\s\S]{0,140}(?:token that's a copy|token cop|tokens that are copies)"
                + @"|graveyards?\b[\s\S]{0,80}creature cards? exiled this way[\s\S]{0,60}token that's a copy"),
            // The other wordings of the sitting of 10-06..10-11 and the older
            // ones it brought up, each tagged by hand on every reviewed card
            // that says it (2026-10-13): the Aura that brings back the card it
            // enchants (Animate Dead, Dance of the Dead); the verb first,
            // "return to the battlefield target … card in your graveyard"
            // (Squirming Emergence, Moira) and "put onto the battlefield … all
            // creature cards in all graveyards" (Myojin of Grim Betrayal); a
            // card returned to hand that may go to the battlefield instead
            // (Zuko's Conviction, Back for Seconds, Cosmic Rebirth); the mass
            // swap (Living Death); cards chosen in a graveyard and then put
            // (Afterlife from the Loam, Rejoin the Fight, Coin of Fate); a
            // library-and/or-graveyard search (Invasion of Ikoria); and every
            // creature of yours that dies (Valkyrie's Call).
            Rx(@"(?:return|put) enchanted creature card (?:on)?to the battlefield"),
            Rx(@"return to the battlefield (?:up to \w+ )?(?:another )?target[\w ,'\-/]{0,60}cards? (?:in|from)[\w ' ]{0,25}graveyard"
                + @"|put onto the battlefield[\w ,'\-/]{0,40}creature cards? (?:in|from)[\w ,'\-/]{0,20}graveyards?"),
            Rx(@"(?:target|up to \w+ target) [\w ,'\-/]{0,40}cards? (?:from|in) your graveyard\b[^\n]{0,160}?"
                + @"\bput (?:it|that card|one of those cards)\b[^.\n]{0,60}onto the battlefield"),
            Rx(@"exiles? all creature cards from their graveyards?[^.\n]{0,100}puts? all cards they exiled this way onto the battlefield"),
            Rx(@"creature cards? in [\w ']{0,30}graveyard\. put (?:those cards|that card|them|it) onto the battlefield"
                + @"|creature cards? (?:in|from) your graveyard[^\n]{0,160}\breturn (?:each card chosen this way|the other) to the battlefield"),
            Rx(@"search your library and/or graveyard for[^.\n]{0,60}creature card[^.\n]{0,80}put it onto the battlefield"),
            Rx(@"whenever (?:a|another) [^.\n]{0,40}creature you control dies, return that card to the battlefield"),
            // …and the one creature an Aura is on, "when enchanted creature dies,
            // return that card to the battlefield under your control" (Necrogen
            // Communion, False Demise), or any creature of yours, the enchantment
            // sacrificed for it (Angelic Renewal). Read 2026-10-16. Under YOUR
            // control: Abduction hands the stolen creature back to its owner.
            Rx(@"when(?:ever)? enchanted creature dies, return that card to the battlefield under your control"
                + @"|whenever a creature is put into your graveyard from the battlefield[^\n]{0,80}return that card to the battlefield"),
            // A FREE cast out of your graveyard of a card that may be a creature
            // or another permanent — Counterpoint's "creature, instant, sorcery,
            // or planeswalker spell", Shiko's "nonland card" and its copy — puts
            // a permanent on the battlefield for nothing, read 2026-10-16. The
            // instant-and-sorcery casts stay Regrowth alone (Daring Waverider,
            // Wishing Well, Seifer Almasy, Sword of Once and Future: 0 of 6).
            Rx(@"cast (?:a |an |target )?[\w ,'\-/]{0,40}\b(?:creature|permanent|nonland)\b[\w ,'\-/]{0,60}"
                + @"from your graveyard[^.\n]{0,80}without paying"
                + @"|\b(?:creature|permanent|nonland) card[\w ,'\-/]{0,40}from your graveyard\. copy it, then you may cast the copy without paying"),
        ];

        private static readonly Regex[] RegrowthPatterns =
        [
            // 90, not 70: Krile Baldesion's "return target creature card WITH MANA
            // VALUE EQUAL TO THAT SPELL'S MANA VALUE from your graveyard" runs to
            // 72 (2026-09-29).
            // 150 since 2026-10-13 for Rise from the Wreck's four targets, and
            // "graveyard OR exiled card with flashback you own" before the hand
            // (Sorceress's Schemes). A return that is the PRICE of keeping the
            // card is not this: Harvest Wurm is sacrificed "unless you return a
            // basic land card", untagged.
            Rx(@"(?<!unless you )return[\w ,'\-/]{0,150}from[\w ,'\-/]{0,30}graveyard(?: or [\w ]{0,40})? to[\w ',]{0,20}hand"),
            // Cards exiled out of a GRAVEYARD and later returned to their owners'
            // HANDS (Aurelia's Vindicator, The Spot — ruled Regrowth and Bounce
            // on 2026-09-29). Read 2026-10-13 for one card returned: "the
            // exiled card" (The Aesir Escape Valhalla), "target card exiled
            // with Nyla" (Nyla, Shirshu Sleuth).
            Rx(@"cards? from (?:a |your |their )?graveyards?[\s\S]{0,250}return [^.\n]{0,40}\bexiled\b[^.\n]{0,30} to (?:its|their) owners?'?s?'? hands?"),
            Rx(@"put[\w ,'\-/]{0,60}cards? from[\w ,'\-/]{0,30}graveyard into[\w ]{0,20}hand"),
            // The rest of the sitting of 10-06..10-11, each tagged by hand on
            // every reviewed card that says it (2026-10-13): one of the top two
            // cards of your graveyard to hand (Phyrexian Grimoire); a pile of
            // them (Split the Spoils); a card on top of your library without a
            // target (Halo-Charged Skaab).
            // Read 2026-10-16, each the one reviewed card that says it and
            // tagged: a card CHOSEN in your graveyard and then cast (Conduit of
            // Worlds) or put into your hand when it does not go to the
            // battlefield (Cosmic Rebirth); the top card of a graveyard put on
            // top of the library (Guiding Spirit).
            Rx(@"target [\w ,'\-/]{0,50}cards? in your graveyard\.[^\n]{0,80}\byou may cast that card"
                + @"|target [\w ,'\-/]{0,50}cards? in your graveyard\.[^\n]{0,200}\bput it into your hand"
                + @"|the top card of [\w' ]{0,25}graveyard is [^,\n]{0,30}, put that card on top of"),
            Rx(@"cards? of your graveyard\.[^.\n]{0,60}put the other (?:one )?into your hand"
                + @"|from your graveyard[^\n]{0,120}put that pile into your hand"
                + @"|put (?:a|an) [\w ,'\-/]{0,40}cards? from your graveyard on top of your library"),
            // A card EXILED out of your graveyard and then cast or played — the
            // card or its copy. Ruled 2026-10-13 ("Sì", 17 tagged: Kaervek,
            // Arcane Bombardment, Capricious Hellraiser, Scholar of Combustion,
            // Tersa, Quintorius Loremaster, The Tale of Tamiyo …); Nashi, Jacob
            // Frye, Storm of Memories and Containment Construct reached; Shiko,
            // Paragon of the Way named as the exception (its copy is a
            // Reanimate). Out of "A graveyard" only when the card is copied there
            // and then (Narset, Enlightened Exile, Psionic Ritual): Intrepid
            // Paleontologist and Boiling Rock Rioter exile from any graveyard as
            // hate, and cast from among what they exiled only later.
            Rx(@"exile[^.\n]{0,80}\bcards?\b[^.\n]{0,80}from (?:your|each) graveyards?\b[\s\S]{0,200}?"
                + @"(?:you may (?:cast|play) (?!this card)|cast the cop(?:y|ies))"
                + @"|exile[^.\n]{0,80}\bcards?\b[^.\n]{0,80}from a graveyard\b[^.\n]{0,10}(?:and )?copy (?:it|them)\b"
                // …and the card exiled as it reaches your graveyard: Mazzy,
                // Truesword Paladin's Auras (read 2026-10-30, 1 of 1).
                + @"|is put into your graveyard from the battlefield, exile it\.[^\n]{0,60}you may cast that card"),
            // A token COPY of a NONCREATURE card exiled out of your graveyard,
            // ruled 2026-10-30 on Anikthea, Hand of Erebos's "non-Aura
            // enchantment card" (Regrowth kept by name). A creature card's copy
            // stays Reanimate (Ardyn, Mordor on the March, Abyssal Harvester),
            // and so does "an artifact or creature card" (Tawnos, Lorehold
            // Archivist).
            Rx(@"exile (?:up to one )?(?:target )?(?:non-aura )?(?:enchantment|artifact|noncreature|instant|sorcery) card from your graveyard\. "
                + @"create a token that's a copy of (?:that card|it)\b"),
            // …and cast from among the cards a collect-evidence or escape cost
            // exiled from it (Kylox's Voltstrider, Skyway Robber).
            Rx(@"(?:collect evidence|escape)[\s\S]{0,300}cast [^.\n]{0,60}from among cards exiled with (?:it|this)"),
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
            //
            // The card may be DESCRIBED between its noun and "from", ruled
            // 2026-10-13 ("Regrowth sempre", once or every turn): Halo
            // Forager's "card with mana value X from a graveyard", Daring
            // Waverider, Wishing Well, Invoke Calamity; Seifer Almasy, Sword of
            // Once and Future and Gale, Waterdeep Prodigy, CardAdvantage alone
            // by hand, reached. A keyword's reminder is not a cast ("You may
            // cast cards with retrace from your graveyard": Deeproot
            // Historian, untagged).
            Rx(@"(?<!whenever you )(?:may )?cast (?!this card\b|this spell\b|it\b)"
                + @"(?!cards with (?:retrace|flashback|escape|harmonize|jump-start|mayhem)\b)"
                + @"(?!(?:[\w ,'\-/]{0,40})?for each)[\w ,'\-/]{0,50}(?:spells?|cards?)\b[\w ,'\-/]{0,80}? "
                + @"from (?:among )?(?:cards in )?(?:your|a|any) graveyard"),
            // A card cast out of an OPPONENT'S graveyard — "that player's", "their"
            // — is Steal alone and never Regrowth, ruled 2026-10-16 ("solo
            // Steal"): Tinybones, the Pickpocket by hand, Deluxe Dragster changed
            // by name. The two possessives were in the list above until then.
            //
            // A free cast of a card that can be a PERMANENT is Reanimate as well,
            // read 2026-10-16 from Counterpoint and Shiko, Paragon of the Way:
            // what lands may be a creature. See ReanimatePatterns.
            // "From ANY graveyard" is the same cast, ruled 2026-10-16: Urabrask //
            // The Great Work's chapter III ("Until end of turn, you may cast
            // instant and sorcery spells from any graveyard") is Regrowth like
            // Forgotten Cellar's one-turn "you may cast spells from your
            // graveyard this turn", and it is not CardAdvantage.
            //
            // MEASURED AND DROPPED 2026-10-13: the card described between
            // "graveyard" and "gains" (Desdemona, Freedom's Edge) and the keyword
            // given in quotes (The Grim Captain's Locker's "Escape—") reached no
            // card the cast patterns above do not already reach through the
            // escape cost and its reminder.
            Rx(@"target[\w ,'\-/]{0,50}cards? in (?:your|a) graveyard gains? "
                + @"(?:flashback|escape|harmonize|jump-start)"),
            // GAINS as well as HAS since 2026-10-13: Past in Flames, Will of the
            // Jeskai ("each instant and sorcery card in your graveyard gains
            // flashback"), reached.
            Rx(@"(?:each|all|any number of) [\w ,'\-/]{0,40}cards? in your graveyard (?:ha(?:s|ve)|gains?) "
                + @"(?:flashback|escape|harmonize|mayhem)"
                // …and in EVERY graveyard, yours among them (Otaria), which the
                // cast above read through the reminder's "their graveyard" until
                // that left its list on 2026-10-16.
                + @"|\b(?:instant|sorcery|instant and sorcery) cards in graveyards (?:ha(?:s|ve)|gains?) flashback"),
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
        //
        // Nor is a card handed back to its OWNER: Mysterious Limousine returns
        // the others "under its owner's control", Vault 13 "under their
        // owners' control" — a temporary removal undone, untagged by hand
        // (2026-10-13).
        private static readonly Regex ExiledCardOntoTheBattlefield = Rx(
            @"(?:put|return)[\w ,'\-/]{0,50}exiled with[\w ,'\-/]{0,40}(?:on)?to the battlefield"
            + @"(?! under (?:its owner's|their owners'?) control)");

        private static readonly Regex BlinksYourOwnCreature = Rx(
            @"exiles? target creature you control|exiles? (?:it|them|that creature)[\w ,']{0,30}return");

        // A land out of the graveyard is Ramp, ruled 2026-09-18 and read here
        // 2026-09-19: 7 reviewed cards put one back and only 1 is tagged
        // Reanimate. Summon: Titan and Will of the Sultai rebuild a mana base;
        // they do not reanimate anything.
        private static readonly Regex LandOutOfTheGraveyard = Rx(
            @"land cards? (?:from|in)[\w ,'\-/]{0,30}graveyard[\w ,'\-/]{0,20}(?:on)?to the battlefield[^\n.]{0,40}");

        // A card that brings ITSELF back from the graveyard reanimates nothing,
        // ruled 2026-10-13 ("mai Reanimate"): unearth, escape, sarcophagus,
        // "return this card from your graveyard to the battlefield", a death
        // trigger that returns it (Helbrute, Wave of Rats, Lunar Hatchling
        // named; 23 more reviewed cards realigned). It overturns the 09-19
        // measurement that rejected this veto. A card returning itself to
        // HAND is not Regrowth either, ruled 2026-10-13 ("Mai Regrowth (come
        // Reanimate)"): 16 reviewed cards tagged so (Hammer of Bogardan,
        // Furious Forebear, Ringwraiths …) reached and handed back, against the
        // 6 the hand had left untagged (Abzan Devotee, Whiteout, Curious
        // Cadaver …).
        private static readonly Regex SelfRecursionWording = Rx(
            @"[^.\n]*return (?:this card|this creature|this permanent|SELFNAME) from your graveyard to "
            + @"(?:the battlefield|your hand|its owner's hand)[^.\n]*"
            + @"|^(?:unearth|escape|sarcophagus|embalm|eternalize)\b[^\n]*"
            + @"|[^.\n]*you may cast (?:this card|SELFNAME) from your graveyard[^.\n]*"
            + @"|[^.\n]*when (?:this creature|SELFNAME) dies[^.\n]{0,80}return it to the battlefield[^.\n]*",
            RegexOptions.Multiline);

        // A creature put back for SOMEBODY ELSE reanimates nothing for you:
        // Immortal Obligation returns one "from an opponent's graveyard to the
        // battlefield under their control", Reincarnation "from its owner's
        // graveyard … under the control of that creature's owner", both
        // untagged (read 2026-10-13). Blanked with the self-recursion.
        private static readonly Regex ReanimatedForSomebodyElse = Rx(
            @"[^.\n]*(?:an opponent's|its owner's|that player's) graveyard to the battlefield under "
            + @"(?:their|its owner's|the control of)[^.\n]*");

        /// <summary>The text without the card's ways of bringing ITSELF back,
        /// its old wording naming itself. See SelfRecursionWording.</summary>
        private static string WithoutSelfRecursion(Card card, string text)
        {
            string name = Regex.Escape((card.Name ?? string.Empty).Split(" // ")[0]);
            string pattern = SelfRecursionWording.ToString().Replace("SELFNAME", name.Length > 0 ? name : "(?!)");
            return Regex.Replace(text, pattern, " ", SelfRecursionWording.Options);
        }

        // A reanimation or a cheat only a creature TYPE can use is neither tag,
        // ruled 2026-10-15 ("Reanimate e Cheat non dovrebbero contare" for the
        // tribal ones): a Pirate card back from the graveyard (Admiral Brass),
        // a Goblin card out of the hand (Goblin Wizard), Dragon spells cast free
        // (Dracogenesis), one that shares a creature type (Call to the
        // Kindred), and — asked with Kiora — one behind a tribal trigger: its
        // trigger and its look at the top are one sentence. Before it the hand
        // had tagged 7 such Reanimates and 6 such Cheats, all handed back. An
        // EXCLUSION is no tribe — Junji's "non-Dragon creature card" keeps
        // Reanimate. Nor, from 2026-10-30, is a type CHOSEN as the card enters
        // or resolves ("Se scegli tu il tipo quando gioco la carta è diverso da
        // un tribale fisso", in every tag): From the Rubble is Reanimate and
        // Belbe's Portal a Cheat again.
        // NB case-SENSITIVE: the type is what is capitalised, as for the tribal
        // pump and shield (NotATribe, NotACardType). The subtypes of the other
        // card types that these cards name are refused as well: an Aura
        // (Academy Researchers), an Equipment (Stoneforge Mystic), a Vehicle
        // (Fire Nation Salvagers), a Spacecraft — "creature or Spacecraft card"
        // is any creature (Scrounge for Eternity) — and a Cave (Glimpse the
        // Core). A "Saga card" is read as a type like a tribe, which is what the
        // hand had on Rydia, Summoner of Mist: no Reanimate.
        private const string NotAnotherKindOfSubtype =
            @"(?!(?:Aura|Equipment|Vehicle|Spacecraft|Cave)s?\b)";

        // Nor the VERB that opens a sentence: Sarevok's Tome's "{3}, {T}: Exile
        // cards from the top of your library until you exile a nonland card"
        // read as a tribe called Exile and lost its Cheat (2026-10-16).
        private const string NotAVerb =
            @"(?!(?:Exile|Reveal|Return|Discard|Cast|Play|Put|Mill)\b)";

        private const string ACreatureType = @"(?<![Nn]on-)\b" + NotATribe + NotACardType + NotAnotherKindOfSubtype + NotAVerb + @"[A-Z][\w'-]+";

        // An impulse whose card may be cast only when it is of the tribe —
        // "If it's a Goblin creature card, you may cast that card" (Rundvelt
        // Hordemaster, untagged, read 2026-10-23). Not TribalCard on the whole
        // ability: Harnesser of Storms triggers on an "Otter spell" and
        // Daredevil pumps on a "Hero card", and both play any card (tagged).
        private static readonly Regex ImpulseOnlyForATribe = new(
            @"\b[Ii]f it's an? " + ACreatureType + @" (?:creature )?card, you may (?:cast|play)\b",
            RegexOptions.CultureInvariant);

        // Case-sensitive inside a case-blind Rx, for SecondHandOnTop.
        private const string NotOnlyATribesSpells =
            @"(?! (?-i:" + ACreatureType + @") (?:creature )?spells from the top)";

        private static readonly Regex TribalCard = new(
            ACreatureType + @" (?:creature |permanent )?(?:cards?|spells?)\b"
            + @"|\bcreature cards? that shares? a creature type\b",
            RegexOptions.CultureInvariant);

        // …unless the same sentence names a card ANY creature could be: Thwart
        // the Grave returns "target creature card and up to one target Cleric,
        // Rogue, Warrior, or Wizard creature card", and the first is a
        // reanimation.
        private static readonly Regex UnrestrictedCard = Rx(@"\bcreature cards?\b");

        private static readonly Regex OneSentence = new(@"[^.\n]+", RegexOptions.CultureInvariant);

        // The same for an ability only OTHER members of a type pay for, ruled
        // with Kiora ("Vale per ogni Cheat o Reanimate dietro un trigger
        // tribale"): tap five untapped Zombies (Gravespawn Sovereign), sacrifice
        // another Cleric (Shadow-Rite Priest), tap another untapped Merfolk
        // (Meanders Guide). The whole ability line goes, its effect being the
        // next sentence. One creature of the type is no tribe — Slimefoot's
        // "Sacrifice a Saproling" — nor is a type the card makes tokens of
        // itself (Rat King, Verminister's "Sacrifice three Rats").
        private static readonly Regex TribalCost = new(
            @"\b(?:[Tt]ap|Sacrifice) (?:another|two|three|four|five|six|seven|eight|nine|ten|X) (?:untapped )?"
            + @"(?<type>" + ACreatureType + @")[^\n]*",
            RegexOptions.CultureInvariant);

        private static bool MakesTokensOf(string text, string type) =>
            Regex.IsMatch(text, @"\bcreate\b[^.\n]*\b" + Regex.Escape(type.TrimEnd('s')) + @" creature tokens?\b");

        /// <summary>The text without the sentences that reanimate or cheat only
        /// a creature type, and the abilities only that type pays for. See
        /// TribalCard and TribalCost.</summary>
        private static string WithoutTribalRecursion(string text) =>
            OneSentence.Replace(
                TribalCost.Replace(text, cost => MakesTokensOf(text, cost.Groups["type"].Value) ? cost.Value : " "),
                sentence => TribalCard.IsMatch(sentence.Value)
                    && !UnrestrictedCard.IsMatch(TribalCard.Replace(sentence.Value, " ")) ? " " : sentence.Value);

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
        //
        // An OMEN is not a second cast, ruled 2026-09-29: its "(Then shuffle
        // this card into its owner's library.)" sends the card away, it does
        // not hand it back, so Flush Out's "discard a card. If you do, draw two
        // cards" nets zero like Abandon Attachments and is Filter alone. The
        // alternative that read that reminder as a rebuy matched nothing in
        // the store but the five Omens, and was removed.
        private static readonly Regex CastsItselfASecondTime = Rx(
            @"\b(?:flashback|harmonize|escape|jump-start|aftermath|buyback|rebound|retrace|encore)\b");

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
        // The line has to DRAW, too, since 2026-10-14: Chivalric Alliance pays
        // a card for a Knight and Psychic Frog for a counter, and both lost the
        // draw on their combat triggers to it (tagged by hand).
        // Not a LAND card, read 2026-10-23: Soul of Windgrace pays one for its
        // draw and puts lands back from a graveyard every time it attacks
        // (tagged; the one other card in the store saying it is unreviewed).
        private static readonly Regex ActivationCostDiscard = Rx(
            @"^[^\n:]{0,50}discard (?!this card|a land card\b)(?:a|one|two|three|\w+) (?:\w+ )?cards?[^\n:]{0,30}:(?=[^\n]{0,100}\bdraw)",
            RegexOptions.Multiline);

        // Four shapes that name a player beside "discard" and attack nobody's
        // hand, found 2026-09-28, each untagged on every reviewed card:
        //   the MADNESS-style punishment — "when a spell or ability an opponent
        //     controls CAUSES YOU TO DISCARD THIS CARD" (Guerrilla Tactics, Psychic
        //     Purge, Mangara's Blessing, Sand Golem — 0 of 4);
        //   a LOOT handed to a player — "target player draws two cards, then
        //     discards a card" (Depth Defiler, Whirlwind Technique, Cait, Anvil of
        //     Bogardan — 0 of 4), which is Filter;
        //   a CONDITION — "whenever an opponent discards a card, …" (Lo and Li)
        //     watches a discard and causes none; Tinybones and Entropic
        //     Battlecruiser keep the tag through the discard they DO cause;
        //   an ALTERNATIVE COST — "its controller may discard a card that shares
        //     a color" (Dream Halls);
        //   and, ruled 2026-09-28, a SYMMETRIC CHOICE — "each player MAY discard"
        //     (Mind Bomb, Snort, Mog, Will of the Jeskai, and The Death of Gwen
        //     Stacy, whose hand tag the ruling corrected): everybody picks, you
        //     included, and nobody's hand is attacked. The mandatory "each player
        //     discards" of Wheel of Fortune stays Discard. Confirmed 2026-09-29
        //     for the SYMMETRIC PUNISHER too — "each player loses 2 life unless
        //     they discard a card" (Tomb of Annihilation, Possessed Portal) and
        //     Doom Foretold's "each player's upkeep … if that player can't, they
        //     discard" — while the one aimed at opponents only is Discard.
        // Blanked and the Discard patterns asked again, so a card that also
        // attacks a hand elsewhere keeps the tag.
        private static readonly Regex NotAnAttackOnAHand = Rx(
            @"[^.\n]*causes? you to discard this card[^.\n]*"
            + @"|(?:target player|defending player|each player|that player)[^.\n]{0,30}draws? [^.\n]{0,20}cards?,? then discards? (?:a|two) cards?"
            + @"|whenever (?:an opponent|a player|one or more (?:of your )?opponents?) discards? [^,\n]*,"
            + @"|its controller may discard[^.\n]*"
            + @"|each player may discard[^.\n]*"
            + @"|each player [^.\n]{0,60}\bunless they discard (?:a|two) cards?"
            + @"|at the beginning of each player's upkeep, that player [^\n]{0,200}?\bthey discard (?:a|two) cards?");

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
            + @"|at the beginning of each opponent'?s [\w ]{0,20}step, that player draws"
            // The player whose permanent was exiled shuffles and draws for what
            // it lost (The End, Deadly Cover-Up), untagged by hand 2026-10-14.
            + @"|that player shuffles, then draws"
            // "THAT PLAYER" is an opponent when the sentence before has just
            // named one: Red Death, Shipwrecker goads a creature "an opponent
            // controls. That player draws a card", and you add the mana. Read
            // 2026-10-16 on the one reviewed card written this way (untagged for
            // it, hand: Ramp). Howling Mine's "that player draws an additional
            // card" follows "each player's draw step" and is not this.
            + @"|an opponent controls\. that player draws (?:a|one) card");

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
        // "Whenever ENCHANTED PLAYER draws" is a Curse watching an opponent
        // (Curse of Fool's Wisdom, Burn alone by hand, read 2026-10-23).
        private static readonly Regex TriggersOffDrawing = Rx(
            @"whenever (?:you|an opponent|a player|another player|one or more players|enchanted player) draws?");

        // "Activate only once." closes an ability that runs once a GAME, so the
        // activated draw is a one-shot, not the repeatable draw of the
        // 2026-09-11 ruling: Thought Shucker ("Activate only if there are seven
        // or more cards in your graveyard and only once."), the one reviewed
        // card whose draw says it, untagged (read 2026-10-16). Asked of the
        // line that DRAWS: Duggan, Private Detective prints the same words on
        // its damage ability and keeps the tag from its investigate. "Only
        // once EACH TURN" does not end the line, so it is not this.
        private static readonly Regex DrawOnceOnly = Rx(
            @"^[^\n:]{1,70}:[^\n]*\bdraws? (?:a|one) card[^\n]*\bonly once\.?$", RegexOptions.Multiline);

        // A trigger that waits for a spell or ability to TARGET a creature, with
        // no opponent named, needs a second card of your own to pull it (the
        // reading ADrawYouCannotCount gives the Aura and the activated ability):
        // Nadu, Winged Wisdom hands "whenever this creature becomes the target
        // of a spell or ability, reveal the top card … put it into your hand" to
        // every creature and was tagged by the rule that reads a repeated
        // look-and-take. Read 2026-10-16; Fugitive Druid (untagged) is the
        // same difficulty said for an Aura, and Nadu is the one reviewed card
        // written without the opponent (hand: Ramp alone).
        private static readonly Regex TargetedByYourOwnSecondCard = Rx(
            @"\bbecomes? the target of a spell or ability,");

        // The sibling of DrawBySacrificingItself, and the same reading: a card
        // that exiles ITSELF out of your graveyard to draw runs exactly once.
        // The five identical Surveyors ("Max speed — {3}, Exile this card from
        // your graveyard: Draw a card") were four of the fifty over-fires.
        // 27 cards, 25 of them untagged.
        // Asked of the card with its REMINDER TEXT taken out, because encore
        // and flashback both spell their cost as "Exile this card from your
        // graveyard:" inside the brackets, and that is the keyword explaining
        // itself rather than an ability that draws. Eumidian Wastewaker draws a
        // card per land its attack buries and was losing the tag to its own
        // encore reminder (2026-09-21).
        private static bool SpendsItselfOutOfTheGraveyard(string text) =>
            DrawByExilingItselfFromGraveyard.IsMatch(Parenthetical.Replace(text, " "));

        private static readonly Regex DrawByExilingItselfFromGraveyard = Rx(
            @"exile this card from your graveyard[^\n:]{0,30}:");

        // The top of your library as a second hand, ruled 2026-09-16 — the rule
        // was written then and the window was too short for the very cards the
        // ruling named. Glarb says "play lands AND CAST SPELLS WITH MANA VALUE 4
        // OR GREATER from the top of your library", 44 characters where 40 were
        // allowed, and Fblthp plots rather than plays. Widened 2026-09-19:
        // 15 cards fire, 14 of them tagged.
        // Not CASCADE's reminder, "When you cast this spell, exile cards from
        // the top of your library until …", which this read as a second hand
        // and so tagged every cascade card: one cascade is one card, once, like
        // a discover, ruled 2026-10-23 (see CascadeMoreThanOnce). Only "this
        // spell": the same exile-until on EVERY legendary spell (Jodah, the
        // Unifier, tagged) or on every activation (Codie) is a stream.
        // Not when only a creature TYPE may be cast from the top: Hakoda, Selfless
        // Commander's "cast Ally spells from the top of your library" (untagged,
        // read 2026-10-27, the tribal ruling of 2026-10-16 as for Rundvelt
        // Hordemaster's impulse). A land beside the type is no tribe (Traveling
        // Chocobo, "play lands and cast Bird spells", tagged), nor a supertype
        // (Isu the Abominable's snow spells, tagged).
        private static readonly Regex SecondHandOnTop = Rx(
            @"(?:play|cast|plot)(?! this spell, exile cards from the top of your library until)" + NotOnlyATribesSpells
            + @"[\w ,'\d]{0,60}from the top of your library"
            // The bare LOOK ("you may look at the top card of your library any
            // time") is no second hand without a permission to play or cast
            // from it: Vesuvan Drifter, untagged (read 2026-10-16). The look
            // here stays only where the same card also plays from the top, or
            // explores (Glowcap Lantern, tagged: it looks and then keeps what
            // it finds); 23 of the 24 reviewed cards written this way have
            // the permission, and Glowcap Lantern is the 24th.
            + @"|look at the top card of your library any time(?=[\s\S]*(?:\b(?:play|cast)\b" + NotOnlyATribesSpells
            + @"[^\n]{0,60}\b(?:top card|from the top)\b|\bexplores\b))"
            + @"|you may (?:play|cast)[^\n]{0,60}top card of your library"
            // The same permission with the LOOK first and the cast after, which
            // is how a card that turns the top over as a trigger writes it:
            // Planetarium of Wan Shi Tong looks whenever you scry or surveil
            // and casts what it finds for free.
            //
            // The verb has to be LOOK or REVEAL, and that is the whole rule.
            // "Exile the top card of your library, you may play it" is a
            // one-card impulse, which the 2026-09-15 ruling calls a rider and
            // not a card, and a looser version of this pattern read three of
            // them as a second hand: Equilibrium Adept, Haste Magic and
            // Summon: Brynhildr, none of them tagged. Measured on the look:
            // one reviewed card is written this way and it is tagged.
            + @"|(?:looks? at|reveals?) the top card of your library"
            + @"[^\n]{0,60}you may (?:cast|play) (?:that card|it)\b");

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
            + @"|exiles? the top \w+ cards? of (?:target |that )?(?:opponent|player)"
            // THAT MANY off the top, the count carried in from the trigger
            // (Virtue of Courage, tagged, 2026-10-14).
            + @"|exiles? that many cards from the top of your library[^\n]{0,60}you may play"
            // …and A NUMBER of them, counted from the board (Neriv, Crackling
            // Vanguard, on every attack, tagged, read 2026-10-27, 1 of 1), all of
            // them: End-Blaze Epiphany chooses one, the count of a Filter.
            + @"|exiles? a number of cards from the top of your library[^\n]{0,160}you may play those cards");

        // …and the shape that takes several off the top and lets you play only
        // ONE of them. Ruled 2026-09-20: you looked at two and played one, which
        // is the count of a Filter and not of a card gained — the same answer
        // the repeatable put-into-hand rule gives for a one-shot. All three
        // reviewed cards say it the same way: Riverwheel Sweep, Heroes' Hangout
        // and Case of the Burning Masks, the last of which prints it behind
        // "Sacrifice this Case" and so cannot repeat either.
        // The same count LOOKED at first and one of them exiled to be cast,
        // once: Feral Encounter (Filter by hand, 2026-10-14). Anchored to the
        // line, so the attack trigger of Djeru and Hazoret, which looks again
        // every combat and is tagged, stays out. The one exiled may be written
        // "Exile one of them face down" (Discover the Impossible, Filter by
        // hand, read 2026-10-30); Ashiok, Wicked Manipulator's "Exile one of
        // them and put the other into your hand" casts nothing and stays out.
        private static readonly Regex ExileSeveralAndChooseOne = Rx(
            @"exiles? the top \w+ cards? of your library[^\n]{0,60}choose one of them"
            + @"|^look at the top \w+ cards? of your library\. (?:you may exile [^.\n]{0,40}\bcard from among them|exile one of them)"
            + @"[^\n]{0,160}you may cast the exiled card",
            RegexOptions.Multiline);

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
            // "Four times" and up are the same count as three: Carnage
            // Interpreter ("discard your hand, then investigate four times",
            // CardAdvantage by hand) was missed because only twice, three and
            // X were listed (read 2026-10-16; the family is 8 of 8 tagged).
            + @"|investigates? (?:twice|(?:three|four|five|six|seven|eight|nine|ten|x|\d+) times)"
            + @"|clue tokens?,? where x|investigate for each|clue token for each");

        // A draw AND a Clue in one spell is two cards for one: Deduce, "Draw a
        // card. Investigate.", is CardAdvantage by hand (read 2026-10-16, 1 of 1
        // reviewed card written this way). The Clue is the second card, which
        // is why the single Clue alone stays a rider.
        private static readonly Regex DrawAndInvestigate = Rx(@"\bdraw a card\. investigate\b");

        // "Draw that many cards" is a draw-for-each written the other way round,
        // and the count is never one: Niv-Mizzet, Starwinder, Voracious
        // Bibliophile. Restricted to YOUR draw — "each player draws that many"
        // is a wheel (Teferi's Puzzle Box, Winds of Change) and pays everybody.
        // 12 fire, 9 tagged; without the restriction 20 fire and 11 tagged.
        private static readonly Regex DrawThatMany = Rx(
            @"you (?:may )?draw that many cards|, draw that many cards");

        // "You may LOOK AT AND play that card" is the same permission (Headliner
        // Scarlett, every upkeep, tagged, read 2026-10-27; the four reviewed
        // cards that say it are all tagged).
        private static readonly Regex ImpulseDraw = Rx(
            @"exiles? the top [\w ]{0,20}(?:card|cards) of your library"
            + @"[^\n]{0,90}(?:you may (?:look at and )?(?:play|cast)|may play (?:it|them|that card))");

        // More than one card off the top is a draw two, whatever it costs to cast.
        // DISCOVER is ONE card off the top, cast for free or kept — the one-card
        // impulse, which is a card only when the ability comes back. Ruled
        // 2026-09-29 when the human took CardAdvantage off Geological Appraiser
        // (discover 3 as it enters) and Hidden Volcano (the land sacrificed for
        // discover 4), against Long-Range Sensor's charge counters and
        // Caparocti Sunborn's attack, which repeat and are tagged. Asked of the
        // ABILITY with the self-consuming costs subtracted, the same gate the
        // top-few-into-your-hand family uses. "Discover X" is the same keyword
        // (Monstrous Vortex, Pantlaza).
        private static readonly Regex Discovers = Rx(@"\bdiscover (?:\d+|x)\b");

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
        private static readonly Regex ChapterFiresTwice = Rx(@"^[ivx]+(?:, [ivx]+)+ ", RegexOptions.Multiline);

        private static bool AbilityRepeats(string ability) =>
            RepeatableWording.IsMatch(ability) || ChapterFiresTwice.IsMatch(ability);

        // The same question with the two self-consuming costs subtracted. An
        // ability that spends the card to pay for itself runs once however it
        // is worded — Morbius the Living Vampire exiles itself out of the
        // graveyard, Lupinflower Village sacrifices itself — and the impulse
        // family deliberately does NOT ask this, because there the cost is
        // often paid by something else: Junktown makes three Junk tokens and
        // each token sacrifices ITSELF for a card, which is three cards.
        private static bool AbilityRepeatsOutsideTheGraveyard(string ability) =>
            AbilityRepeats(ability) && !SpendsItselfOutOfTheGraveyard(ability);

        private static bool AbilityRepeatsAtNoCostToItself(string ability) =>
            AbilityRepeats(ability)
            && !DrawBySacrificingItself.IsMatch(ability)
            && !SpendsItselfWithoutACostLine.IsMatch(ability)
            && !TransformsItselfAway.IsMatch(ability)
            && !SpendsItselfOutOfTheGraveyard(ability);

        // A third way an ability runs exactly once: it TRANSFORMS the permanent
        // the moment it succeeds, and the other face does something else
        // entirely. This is the whole difference between Sidequest: Catch a
        // Fish and Traveling Botanist, which print the same sentence word for
        // word — the Botanist looks at the top every time it taps, while the
        // Sidequest turns into a land the first time it finds something. The
        // human ruled the pair Filter on 2026-09-22, which is what the
        // Sidequest is already tagged and what the repeat gate has to say for
        // itself. Six reviewed cards transform themselves this way and five are
        // untagged.
        private static readonly Regex TransformsItselfAway = Rx(
            @"transforms? this (?:enchantment|artifact|creature|permanent|land)");

        // A scry or a surveil made ONCE, beside what the card is really for, is
        // not selection — ruled 2026-09-27, overturning the "unconditionally" of
        // 2026-09-19: "Scry o Surveil come effetto secondario non li voglio come
        // Filter, generano troppo rumore". The hand tags had followed the old
        // ruling to the letter — every one of the 39 reviewed cards whose Filter
        // came from nothing but a one-shot scry or surveil carried it — which is
        // exactly the noise the human meant.
        //
        // ONCE is asked per ability, the way the repeatable draw is: Veteran
        // Guardmouse's valiant trigger and Clandestine Meddler's attack trigger
        // scry again and again and keep the tag (both named as slips the same
        // day). SECONDARY means a creature, artifact or enchantment — the body or
        // the Saga is what the card is for — or a spell that earns another tag;
        // Opt and Dreams of Laguna select and do nothing else, and keep it.
        // A LAND keeps it too, ruled the same day after the first cut had taken
        // it off the 20 surveil lands: "Surveil e scry land, teniamole. Mi serve
        // come filtro sulle terre doppie" — the tag is how the dual lands that
        // select are told apart from the ones that do not.
        // The reminder text goes with the keyword, because "(To scry 2, look at
        // the top two cards of your library …)" is itself a look at the top.
        private const string OneScryOrSurveil =
            @"\b(?:scry|scries|surveil|surveils) (?:\d+|x)\b\.?(?: ?\((?:to (?:scry|surveil) \w+, )?look at the top[^()]*\))?"
            // …and an EXPLORE, the same selection, since 2026-09-29 (see
            // Explores), with its reminder.
            // Not one explored X TIMES, which is a look at X (Jadelight
            // Spelunker, Filter by hand, 2026-10-14).
            + @"|(?<!\bwhenever [^,.\n]{0,60})\bexplores?\b(?! x times)(?:, then it explores again)?\.?(?: ?\((?:to have it explore, )?reveal the top card[^()]*\))?";

        // A LOOT tacked onto a SPELL that does something else is the same rider,
        // ruled 2026-09-27 on Refute — "non ha Filter, solo counter" — which is
        // Deny Entry, Broadside Barrage and Transpose written the same way.
        private const string OneLoot =
            @"draw (?:a|one) card,? then discard a card\.?|discard a card, then draw a card\.?";

        // …and a loot, a rummage or a CONNIVE made once by a PERMANENT, ruled
        // 2026-10-24 — "No Filter, come lo scry singolo" — on Revel Ruiner and
        // Echo Inspector, untagged on 10-23 the day Raffine's Informant kept
        // the tag on the same words. The body (or the artifact, the Saga) is
        // what the card is for, the scry ruling of 2026-09-27. Counted before
        // it was ruled: 6 of 10 reviewed connives on entering were tagged, 16
        // of 18 loots, 6 of 6 rummages, all from before the question was put.
        // A loot that COMES AGAIN keeps the tag (Dreadwing Scavenger's "enters
        // or attacks", Prowler's connive on every Villain). Any count, since
        // the scry ruling read any number: Kiora, the Rising Tide's "draw two
        // cards, then discard two cards". A connive selects only through its
        // reminder, which is a loot the first alternative blanks wherever it
        // sits on the line; blanking the keyword too was tried and moved
        // nothing.
        // Only on a TRIGGER that fires once (OnceTrigger): a bare loot line on
        // a creature card is its Adventure or Omen spell (Tempest Hart's Scan
        // the Clouds, tagged), an activated loot repeats (Oracle of Dust), and
        // a connive in quotes is one the card hands over to come again
        // (Copycrook, Security Bypass, both tagged). Nor a trigger on TWO
        // events, which loots twice: Oracle of Tragedy's "when this creature
        // enters or dies" (tagged 2026-09-29).
        private static readonly Regex OnceTrigger = Rx(
            @"^\s*(?:when\b(?![^,\n]* or (?:dies|leaves|attacks|is put into)\b)|[ivx]+ —)");

        private const string OneLootOnAPermanent =
            @"\bdraws? (?:a|one|two|three|x|\d+) cards?,? then discards? (?:a|one|two|three|x|\d+) cards?\.?"
            + @"|\bdiscards? (?:a|one|two|x|\d+) cards?, then draws? (?:a|one|two|three|x|\d+) cards?\.?"
            + @"|\byou may discard a card\. if you do, draw a card\.?";

        // Whatever sits in QUOTES belongs to something else — Transpose's Wizard
        // token carries its own "Whenever you cast …" — so it neither makes the
        // line repeatable nor gets blanked: the quoted alternative is matched
        // first and handed back untouched.
        private static readonly Regex QuotedOrOneScry = Rx("\"[^\"\n]*\"|" + OneScryOrSurveil);

        private static readonly Regex QuotedOrOneScryOrPermanentLoot = Rx(
            "\"[^\"\n]*\"|" + OneScryOrSurveil + "|" + OneLootOnAPermanent);

        private static readonly Regex QuotedOrOneScryOrLoot = Rx("\"[^\"\n]*\"|" + OneScryOrSurveil + "|" + OneLoot);

        private static string BlankOutsideQuotes(Regex quotedOrPhrase, string text) =>
            quotedOrPhrase.Replace(text, m => m.Value.StartsWith('"') ? m.Value : " ");

        private static bool SelectsOnlyInPassing(Card card, string text, CardEffect others)
        {
            bool spell = card.MacroType == MacroType.Spell;

            if (card.MacroType == MacroType.Land || (spell && others == CardEffect.None))
            {
                return false;
            }

            // A SPELL's MODE is an effect of its own and not a rider on the
            // others, ruled 2026-09-27 on Kozilek's Command — "è una spell,
            // quindi conta l'effetto secco (come Ponder)": its "scries X, then
            // draws a card" is a Ponder you can choose, so a bullet line of a
            // spell is left alone.
            // An ability that exiles the card out of the GRAVEYARD to pay for
            // itself runs once (Survivor of Korlis scries 2 that way, untagged
            // by hand, 2026-10-14). Not every self-spending cost: a scry the
            // card SACRIFICES itself for is Filter by hand (Grim Bauble, Aang's
            // Iceberg, the Omen cycle) — measured with AbilityRepeatsAtNoCost-
            // ToItself and dropped.
            string repeating = string.Join('\n', Abilities(text).Select(a =>
                AbilityRepeatsOutsideTheGraveyard(Quoted.Replace(Parenthetical.Replace(a, " "), " ")) ? a
                : spell ? string.Join('\n', a.Split('\n').Select(line =>
                    line.TrimStart().StartsWith('•') ? line : BlankOutsideQuotes(QuotedOrOneScryOrLoot, line)))
                : BlankOutsideQuotes(OnceTrigger.IsMatch(a) ? QuotedOrOneScryOrPermanentLoot : QuotedOrOneScry, a)));

            if (repeating == text)
            {
                return false;
            }

            // Re-asked through the same vetoes the tag already passed.
            string selecting = WithoutFalseSelection(repeating);

            return !FilterPatterns.Any(p => p.IsMatch(selecting))
                && !ExileSeveralAndChooseOne.IsMatch(selecting)
                && !PhasingLoot.IsMatch(selecting)
                && !Explores.IsMatch(Parenthetical.Replace(selecting, " "));
        }

        // CardAdvantage and Filter on DIFFERENT ABILITIES, ruled 2026-09-29 on
        // Qiqirn Merchant and Dimir Strandcatcher — "sono due abilità diverse.
        // Es. looter che se si sacrifica peschi 3": the loot selects and the
        // sacrifice draws, one ability each, so the card does both. That limits
        // the 2026-09-27 exclusivity to a card whose selecting and gaining are
        // the SAME ability (Overlord of the Floodpits draws two and discards
        // one in one breath). Each ability is classified ALONE, the one reading
        // that already knows every rule of both tags. And the ability that
        // selects has to REPEAT, like the looter the ruling names: a selection
        // made once beside what the card is for is the noise ruled out on
        // 2026-09-27 (Case of the Crimson Pulse's enters rummage, Kaho's
        // search, a hideaway), which the exclusivity had been hiding. That is
        // also what keeps a SPELL whole, since its lines neither trigger nor
        // cost: a separate "never split a spell" was measured and moved only
        // Quicksilver Sea, a Plane whose two abilities are really two.
        // An exception to that exclusivity, ruled 2026-10-13: a PLANESWALKER's
        // "draw, then discard unless" is a card when the
        // condition is met and a loot when it is not: Kaito Shizuki, Teferi
        // Akosa of Zhalfir, Tezzeret, Betrayer of Flesh (3 of 3 both tags).
        // A creature's or an enchantment's is still a loot (Katara, Seeking
        // Revenge, Chakra Meditation).
        private static readonly Regex PlaneswalkerDrawThenDiscardUnless = Rx(
            @"^[+−-]?\d+: draw [^.\n]*\. then discard [^.\n]* unless", RegexOptions.Multiline);

        // The same two abilities when the parity guard has already taken the
        // draw away: a loot on one ability and a draw of two or more of yours,
        // with no discard, on ANOTHER — The Royal Scions' "+1: Draw a card,
        // then discard a card" beside its "−8: Draw four cards" (read
        // 2026-10-30; 2 of 2 reviewed written so are tagged, Qiqirn Merchant
        // the other).
        private static readonly Regex YouDrawSeveral = Rx(@"\bdraw (?:two|three|four|five|six|seven|x) cards");

        // A selection written as its own verb inside the ability that gains
        // (a scry, a surveil) was measured the same day as both tags, as asked
        // of Rumor Gatherer and Sword of Once and Future: it reached 18
        // reviewed cards, 2 right and 16 against the hand (Mirror of
        // Galadriel's "Scry 1, then draw a card", Serum Sovereign, Saheeli,
        // Sludge Titan — CardAdvantage alone) and went back as a question:
        // ruled "solo CA", with Rumor Gatherer, Sword of Once and Future and
        // Kellan, Daring Traveler keeping Filter as NAMED cards, no rule.

        private static bool SelectsAndGainsOnDifferentAbilities(Card card, string text)
        {
            List<string> abilities = [.. Abilities(text).Where(a => a.Trim().Length > 0)];

            if (abilities.Count < 2)
            {
                return false;
            }

            CardEffect[] each = [.. abilities.Select(a => Classify(new OneAbility(card, a)))];

            return abilities.Where((a, i) => each[i].HasFlag(CardEffect.Filter) && !each[i].HasFlag(CardEffect.CardAdvantage))
                       .Any(a => AbilityRepeats(Quoted.Replace(Parenthetical.Replace(a, " "), " ")))
                && each.Any(e => e.HasFlag(CardEffect.CardAdvantage));
        }

        /// <summary>The card with the text of ONE of its abilities, for
        /// <see cref="SelectsAndGainsOnDifferentAbilities"/>.</summary>
        private sealed class OneAbility(Card card, string ability) : Card(new JsonCard
        {
            Name = card.Name,
            Layout = card.Layout,
            ReleasedAt = card.ReleasedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TypeLine = card.TypeLine,
            OracleText = ability,
            Keywords = card.Keywords,
            ManaCost = card.ManaCost,
        });

        // BLOOD, ruled 2026-09-27 under the scry ruling: "Blood Token rientra
        // nel punto F1, se ripetuti sì se singoli no". The token is a rummage —
        // discard a card, draw a card — so one is a rider and a stream of them
        // is selection. Both reviewed cards that make Blood again and again carry
        // the tag (Ivora, Moonstone Eulogist). And being a rummage it is never a
        // CARD: the reminder's "Draw a card" had been read as a repeatable draw,
        // which is what handed Moonstone Eulogist CardAdvantage.
        private static readonly Regex MakesBlood = Rx(@"\bcreate (?:a|one|two|three|x|\d+) blood tokens?");

        // Singular or plural: two made at once say "They're artifacts with",
        // and Falkenrath Celebrants was still reading as two cards.
        private static readonly Regex BloodReminder = Rx(
            @"\((?:it's an artifact|they're artifacts) with ""\{1\}, \{t\}, discard a card, sacrifice this token: draw a card\.""\)");

        // EXPLORE is selection, not a card, ruled 2026-09-29 ("explore =
        // Filter") on Subterranean Schooner, which the human moved from
        // CardAdvantage: a land off the top into your hand, or a counter and the
        // choice to bin the card — the look-and-keep-one of the top card. Its
        // reminder text says "Put that card into your hand", which the
        // repeated look-and-keep rule read as a card on every attack, so the
        // reminder is blanked before that rule is asked. Read outside brackets
        // (a Map token's explore is the token's), never in a PAYOFF's trigger
        // ("whenever a creature you control explores" — Wildgrowth Walker,
        // Lurking Chupacabra), and like scry and surveil subject to the one-shot
        // guard: an explore made once beside what the card does is noise. A
        // check in that guard for a REPEATED explore beside a one-shot scry was
        // written, turned nothing red and moved no card, and was removed.
        private static readonly Regex Explores = Rx(@"(?<!\bwhenever [^,.\n]{0,60})\bexplores?\b");

        // The reminder may open "To have this creature explore," (Deepfathom
        // Echo, Filter by hand, 2026-10-14).
        private static readonly Regex ExploreReminder = Rx(
            @"\((?:to have [^,)]{0,40}explore, )?reveal the top card of your library\. put that card into your hand if it's a land\.[^)]*\)");

        private static bool MakesBloodAgainAndAgain(string text) =>
            Abilities(text).Any(a => MakesBlood.IsMatch(a) && AbilityRepeats(Parenthetical.Replace(a, " ")));

        // A MAP token is an explore in a bottle, so the Blood reading holds for
        // it: one is a rider, a stream of them is selection — Sentinel of the
        // Nameless City and Restless Anchorage make one on every attack (both
        // Filter by hand, 2026-10-14). The Explores rule does not see them,
        // because the explore sits in the token's reminder.
        private static readonly Regex MakesMap = Rx(@"\bcreate (?:a|one) map token");

        private static bool MakesMapsAgainAndAgain(string text) =>
            Abilities(text).Any(a => MakesMap.IsMatch(a) && AbilityRepeats(Parenthetical.Replace(a, " ")));

        // HIDEAWAY is Filter on every reviewed card that has it, 5 of 5, and
        // that includes Wiretapping, which also draws a card every turn: the
        // card it hides is the one it means to play, so the look is not a
        // rider beside the draw. It is kept through the CardAdvantage
        // exclusivity (2026-10-14).
        private static readonly Regex Hideaway = Rx(@"^hideaway \d", RegexOptions.Multiline);

        // Three draws named CardAdvantage by the human on 2026-09-27, each
        // against a reading that had them as selection or as nothing:
        //   SACRIFICING a pile of permanents for as many cards turns board into
        //   hand (Pitiless Carnage) — the "sacrificing another permanent to draw
        //   is advantage" of 2026-09-22, counted;
        //   a DRAW MODE that comes back EVERY TURN, which overturns the "too
        //   card-specific to encode" of 2026-09-22: Monument to Endurance and
        //   Teval's Judgment both say "hasn't been chosen THIS TURN", Zuko,
        //   Conflicted has no reset and stays out;
        //   and looking at a few and KEEPING THEM ALL (Make Your Own Luck plots
        //   one and puts the rest into your hand), which chose nothing.
        // OFFSPRING makes a token copy that enters too, so an enters trigger
        // fires twice — the "one trigger, two events" of Cryogen Relic. Thundertrap
        // Trainer looks at four and keeps one each time, and the human ruled it
        // CardAdvantage alone on 2026-09-27.
        private static readonly Regex Offspring = Rx(@"^offspring\b", RegexOptions.Multiline);

        private static bool EntersTwiceWithOffspring(string text, string ability) =>
            Offspring.IsMatch(text) && EntersTrigger.IsMatch(ability);

        private static readonly Regex SacrificesAPileForCards = Rx(
            @"sacrifice (?:any number of|x) [^.\n]{1,40}, then draw that many cards");

        private static readonly Regex DrawModeEveryTurn = Rx(
            @"hasn't been chosen this turn[\s\S]{0,200}^\W*draw (?:a|one|two) cards?", RegexOptions.Multiline);

        // A DRAW MODE under any trigger that repeats is a card each time,
        // ruled 2026-10-13: Glissa Sunslayer, Hylda of the Icy Crown, Black
        // Market Connections (7 of 9 reviewed; Kastral, the Windcrested
        // realigned and handed back). Not when the mode is bought with a
        // permanent (Gorbag of Minas Morgul sacrifices the Orc), nor when each
        // mode runs once a GAME — "hasn't been chosen" without "this turn"
        // (Zuko, Conflicted, Silent Hallcreeper).
        private static readonly Regex DrawModeOnARepeatingTrigger = Rx(
            @"^(?:[\w ]+ — )?(?:whenever|at the beginning)(?![^\n]*sacrifice)(?![^\n]*hasn't been chosen(?! this turn))"
            + @"[^\n]*choose one(?: or more)?[^\n]*—(?:\n•[^\n]*)*?\n•\s*(?:[\w ]+ — )?(?:you )?(?:scry \d+, then )?"
            + @"draw (?:a|one|two) cards?"
            // A LOOT bullet is no draw mode: Pip-Boy 3000's "Sort Inventory — Draw
            // a card, then discard a card" on every attack (Filter by hand, read
            // 2026-10-27; Oracle of Tragedy's loot bullet is untagged too, 0 of 2).
            + @"(?!, then discard)",
            RegexOptions.Multiline);

        // …and a sacrifice that buys MORE THAN ONE mode, one of them the draw,
        // ruled 2026-10-16: Caesar, Legion's Emperor ("you may sacrifice another
        // creature. When you do, choose TWO —", a draw among the bullets) is
        // CardAdvantage where Gorbag of Minas Morgul's "choose ONE" is the
        // sacrifice for a card and stays out. The measure found no other card
        // written this way.
        private static readonly Regex SacrificeBuysSeveralModes = Rx(
            @"^(?:[\w ]+ — )?whenever[^\n]*you may sacrifice[^\n.]*\. when you do, choose (?:two|three)[^\n]*—"
            + @"(?:\n•[^\n]*)*?\n•\s*(?:you )?draws? (?:a|one|two) cards?",
            RegexOptions.Multiline);

        private static readonly Regex LooksAndKeepsTheRest = Rx(
            @"look at the top \w+ cards? of your library[^\n]{0,200}put the rest into your hand");

        // The self-sacrifice written as an EFFECT rather than as a cost, so
        // there is no colon for DrawBySacrificingItself to find: Preferred
        // Selection looks at two cards every upkeep but only keeps one by
        // saying "You may sacrifice this enchantment and pay {2}{G}{G}",
        // which it can do exactly once. Added 2026-09-21.
        private static readonly Regex SpendsItselfWithoutACostLine = Rx(
            @"sacrifice this (?:creature|permanent|artifact|enchantment|land|token|card)\b");

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
        // The COUNT is optional, added 2026-09-21. "Look at the top CARD of
        // your library and put it into your hand" every upkeep is the same
        // second hand the plural version is, and four of the tag's misses said
        // it that way: Darkstar Augur, Traveling Botanist, Bison Whistle and
        // Phyrexian Portal, the last of which digs ten and says "looks at"
        // because it makes an OPPONENT do the looking. Measured asked of one
        // ability and gated the same way: 13 reviewed cards are written this
        // way and 11 are tagged. The two that are not are Morbius, which the
        // exile-from-graveyard guard already takes out, and Sidequest: Catch a
        // Fish, which is Traveling Botanist word for word and is the human's
        // own one card of drift.
        // MILLING the few instead of looking at them is the same second hand,
        // added 2026-09-27 once milling and keeping one was ruled selection:
        // Ballad of the Black Flag's three chapters and Sludge Titan's attacks
        // mill and keep one again and again, and all three reviewed cards that
        // say it that way (Six too) are hand-tagged CardAdvantage.
        private static readonly Regex TopFewIntoYourHand = Rx(
            @"(?:(?:looks? at|reveals?) (?:the top (?:\w+ )?cards?|that many cards|twice \w+ cards) "
            // Read 2026-10-16: "mill A card" (Leyline Dowser, Sparring Dummy),
            // "mill THAT MANY cards" (Nashi, Searcher in the Dark) and the
            // keep said as "from among THOSE CARDS" (Ripples of Undeath) or
            // "a card milled this way" (Leyline Dowser), all repeating.
            + @"(?:of|from the top of) your library|\bmills? (?:(?:\w+|x|that many) cards?|a card))"
            // 200, not 160: Radagast the Brown qualifies the card at length ("a
            // creature card that doesn't share a creature type with a creature
            // you control from among those cards"), tagged by the human on
            // 2026-09-29.
            + @"[^\n]{0,200}put (?:one of them|it|that card|[\w /,'-]{0,80}from among (?:them|those cards|the cards milled this way)|[\w /,'-]{0,60} milled this way"
            // "Exile one of them and put THE OTHER into your hand" (Ashiok,
            // Wicked Manipulator's +1, tagged, 2026-10-14).
            + @"|\w+ of those cards|the other(?: one)?) into your hand");

        // The same second hand reached with a SEARCH rather than a look, and
        // only when the ability repeats. Land Tax puts three basic lands in
        // your hand every upkeep and was the tag's oldest miss; Gift of Estates
        // says the identical words on a one-shot sorcery and is Ramp alone,
        // which is what the repeat gate is for. 1 fires, 1 tagged.
        private static readonly Regex SearchesSeveralIntoYourHand = Rx(
            @"search your library for (?:up to )?(?:two|three|four|five|six|seven|x|\d+) "
            + @"[\w -]{0,30}cards?[^\n]{0,60}put them into your hand");

        // A card sacrificed for MORE THAN ONE card is a trade the card wins.
        // The self-sacrifice guard was written for the one-card case and reads
        // it correctly — Preferred Selection spends the enchantment to move one
        // card and gains nothing — but it was also taking out Blitzball,
        // Qiqirn Merchant and Reverberating Summons, which pay the same
        // permanent for two or three.
        //
        // Two exclusions, both measured rather than assumed. The count must be
        // READABLE: All-Fates Scroll draws "X cards, where X is the number of
        // differently named lands you control" and is Ramp alone, which is the
        // same answer LootDrawsMoreThanItPays gives an unreadable count. And
        // the line must not hand a card back: Conch Horn draws two and puts one
        // from your hand on top, which with the artifact itself is level, and
        // is Filter. With both, 3 fire and 3 are tagged.
        // The first exclusion is gone, 2026-10-23: the human tagged All-Fates
        // Scroll CardAdvantage on 2026-10-16, and Infernal Idol's "You draw two
        // cards" with it — 8 of the 10 reviewed cards that sacrifice
        // themselves for X or more are tagged, and the two that are not hand a
        // card back (Conch Horn puts one on top, Starting Column discards one).
        private static readonly Regex SelfSacrificeBuysSeveral = Rx(
            @"^[^\n:]{0,60}sacrifice this [\w]+[^\n:]{0,30}: ?"
            + @"(?:you )?draws? (?:two|three|four|five|six|seven|x|\d+) cards"
            + @"(?![^\n]{0,80}(?:discard|put (?:a|one|\w+) cards? from your hand))",
            RegexOptions.Multiline);

        // Emptying your hand and drawing a fixed number back is not a loot,
        // because the hand you paid can be empty — Case of the Crimson Pulse
        // solves itself on "You have no cards in hand" and then draws two for
        // nothing every upkeep. 3 reviewed cards say it, all 3 tagged.
        // …and the count read off the board, "draw cards equal to the number
        // of stash counters", even behind a self-sacrifice (Hoarder's Overflow,
        // read 2026-10-16; 9 of 9 cards in the family tagged).
        private static readonly Regex DiscardYourHandThenDrawSeveral = Rx(
            @"discard your hand, then draws? (?:two|three|four|five|six|seven|x|\d+) cards"
            + @"|discard your hand, then draws? cards equal to");

        // Read 2026-10-16, five shapes the human tagged CardAdvantage by hand
        // and that no rule read:
        //  - a reveal-until into the hand under an attack trigger the card
        //    fires itself: Songbirds' Blessing. Spinner of Souls (a creature
        //    dying), House Cartographer (a condition) and Nissa (a second
        //    resolution) are untagged and do not say "attacks".
        //  - "If you don't cast a spell this way, investigate" on an attack
        //    trigger: The Seventh Doctor.
        //  - an Aura that hands the enchanted permanent "{T}: Target player
        //    adds … The next spell they cast this turn has cascade": Bigger on
        //    the Inside, read since 2026-10-23 by CascadeMoreThanOnce.
        //  - gravestorm and a Clue on one spell: Follow the Bodies.
        //  - the controller of a damaged creature exiling that many off the top
        //    and playing them: Expedited Inheritance.
        private static readonly Regex AttackRevealsUntilIntoHand = Rx(
            @"^whenever [\w ]{0,30}\battacks, reveal cards from the top of your library until you reveal [^.\n]{0,80}\.[^\n]{0,80}\bput (?:that card|it) into your hand",
            RegexOptions.Multiline);

        private static readonly Regex AttackInvestigatesWhenNotCast = Rx(
            @"^whenever [\w ,]{0,40}\battacks,[^\n]*if you don't cast a spell this way, investigate",
            RegexOptions.Multiline);

        // CASCADE is one card, once — the discover of 2026-09-29 by another
        // name — so a card that cascades once is not this tag, ruled 2026-10-23
        // ("No CA, come discover"): the human had untagged 11 of the 14
        // reviewed cards that cascade once (Natural Reclamation, Volcanic
        // Torrent, Boarding Party …) and the three left (Forceful Denial,
        // Sakashima's Protege, Heralds of Tzeentch) were corrected by name.
        // It had been tagged by accident, by SecondHandOnTop reading the
        // reminder. Cascade that comes round again still is: printed more than
        // once (Apex Devastator), beside a second cast of the card (Into the
        // Time Vortex's rebound), or handed to your spells (Abaddon, Flamekin
        // Herald, Dark Apostle, Bigger on the Inside) — 11 reviewed, all 11
        // tagged. Not "THIS SPELL has cascade as long as" (Bloodbraid
        // Marauder, once), and not the Vehicles' attack (Smoldering
        // Stagecoach, TARDIS: untagged, 2026-10-16).
        private static readonly Regex CascadeMoreThanOnce = Rx(
            @"^cascade, cascade\b"
            + @"|^(?!whenever this vehicle attacks\b)[^\n]*?(?<!\bthis spell )\b(?:has|have|gains?) cascade\b",
            RegexOptions.Multiline);

        private static readonly Regex OwnCascade = Rx(@"^cascade\b", RegexOptions.Multiline);

        private static readonly Regex GravestormAndAClue = Rx(
            @"\bgravestorm\b[\s\S]{0,300}\binvestigate\b");

        private static readonly Regex ControllerExilesThatManyAndPlays = Rx(
            @"its controller may exile that many cards from the top of their library\. they may play those cards");

        // Mill a few and take TWO back, once: Greenhouse // Rickety Gazebo
        // ("return up to two permanent cards from among them"), tagged by hand.
        // Read 2026-10-16. A one-shot that keeps ONE (Cache Grab, Town Greeter,
        // Tomakul Scrapsmith) is selection and stays out.
        private static readonly Regex MillsAndTakesTwoBack = Rx(
            @"\bmills? \w+ cards?, then return up to (?:two|three|four|five) [\w -]{0,30}cards? from among them to your hand");

        // An enters trigger that lets you sacrifice something and, when you do,
        // pays a card on top of its other reward: Selfcraft Mechan (tagged by
        // hand, read 2026-10-16). Unscrupulous Contractor draws two and already
        // had the tag.
        private static readonly Regex EntersSacrificeThenDraws = Rx(
            @"\bwhen this [\w]+ enters, you may sacrifice an? [\w ]{1,20}\. when you do,[^\n]{0,100}\bdraw (?:a|one) card");

        // A triggered draw printed INSIDE QUOTES has been handed to a body that
        // keeps it — Thief's Knife writes it onto the Hero it makes, Herd
        // Heirloom lends it to a creature for the turn. Neither line carries a
        // trigger word of its own where the anchored rules look for one. Same
        // reading DrawGrantedToOtherPermanents gives the self-sacrifice guard;
        // 5 reviewed cards, 4 tagged.
        // The lookahead is the loot again: Ninja's Blades hands out "draw a
        // card, then discard a card", which is parity wherever it is printed.
        // The monarchy is a card at the end of every turn until somebody takes
        // it off you, which is the repeatable draw of the 2026-09-11 ruling
        // with no draw written down anywhere. Asked AFTER the guards, because
        // the crown outlives the permanent that granted it: Coin of Fate
        // sacrifices itself to hand it over, and the self-sacrifice guard was
        // reading that as an ability spent once. One reviewed card grants it
        // and it is tagged. "TARGET PLAYER becomes the monarch" is you as
        // often as not (Denethor, Stone Seer, Éomer, King of Rohan, both
        // tagged, 2026-10-14).
        private static readonly Regex BecomesTheMonarch = Rx(@"\b(?:you|target player) becomes? the monarch");

        // SEVERAL TOKENS THAT EACH DRAW, which is the Clue count in a costume
        // the Clue rules cannot read: Niko makes two Shards and each one cashes
        // for a card, so the card spent is one and the cards bought are two.
        // The human put the arithmetic exactly that way on 2026-09-22, "-1 +2 =
        // +1". Two reviewed cards are written this way and both are tagged.
        private static readonly Regex SeveralTokensThatDraw = Rx(
            "create (?:two|three|four|five|x|\\d+) [\\w ]{0,25}tokens[^\n]{0,80}"
            + "\"[^\"\n]{0,90}draws? (?:a|one) card");

        // WARP is a second cast, so a card that pays you on its way OUT pays you
        // twice: Anticausal Vestige draws when it leaves, and warp exiles it at
        // the end of the turn only to let you cast it again later. Ruled
        // 2026-09-22 with the human's own reason, "utilizzabile due volte, warp
        // prima e poi cast". Deliberately NOT folded into
        // CastsItselfASecondTime, which would hand the same reprieve to the 32
        // reviewed warp cards of which only 5 are tagged; this asks for the
        // leaves-trigger as well, and one card matches.
        private static readonly Regex WarpPaysOnTheWayOutTwice = Rx(
            @"leaves the battlefield, draws?[\s\S]{0,300}\bwarp \{");

        // COPYING YOUR OWN SPELL is a second copy of a card you only paid for
        // once — "è CA perché raddoppia la spell" (2026-09-22). Written to
        // require the copy IMMEDIATELY after the trigger, which is what
        // separates Taigam, whose second spell each turn is copied outright,
        // from Mendicant Core, Guidelight, where the copy is behind Max speed
        // and a further {1} and the card carries no tag. Max speed is NOT a
        // blanket difficulty: 34 reviewed cards print it and 4 are tagged.
        // A copied PERMANENT spell is a token, not a second card (Archmage of
        // Echoes, Tokens alone by hand, 2026-10-13) — see CopiesYourCreatureSpell.
        // So is a HISTORIC or COLORLESS spell, which is a permanent nearly every
        // time: ruled 2026-10-14 ("Dipende dal tipo copiato"), The Sixth Doctor
        // and Echoes of Eternity Redirect alone, while Storm of Saruman copies
        // any second spell, as Taigam does, and is CardAdvantage.
        private static readonly Regex CopiesYourOwnSpell = Rx(
            @"whenever you cast (?:your |a |an )?(?![\w' ]{0,40}\b(?:permanent|historic|colorless) spell)[\w' ]{0,40}, copy it");

        // A LOOT SPLIT ACROSS TWO TRIGGERS is still a loot, ruled 2026-09-22:
        // Teferi's Imp discards when it phases out and draws when it phases
        // back in, which is one card for one card on a timer. Filter, and not
        // CardAdvantage. One reviewed card is written this way.
        private static readonly Regex PhasingLoot = Rx(
            @"phases (?:out|in), discards? a card[\s\S]{0,120}phases (?:in|out), draws? a card");

        // MEASURED AND REJECTED, 2026-09-21, and the human explained the split
        // on 2026-09-22. A trigger whose draw is printed as a MODAL BULLET
        // further down reads as a bare draw to the anchored rules, because the
        // bullet is a line of its own with no trigger word on it — Teval's
        // Judgment says "Whenever one or more cards leave your graveyard,
        // choose one … • Draw a card". Asked of the stitched ability (which is
        // what Abilities() rejoins bullets for) it buys that one card and
        // costs two.
        //
        // The three cards do not disagree, and none of them turns on the
        // bullet. Each falls to a rule this tag ALREADY has:
        //
        //   Teval's Judgment  — "hasn't been chosen THIS TURN", so the choice
        //                       comes back every turn and the second iteration
        //                       is a card. Tagged, and it stays a known miss.
        //   Monument to End.  — the trigger IS a discard, so the draw is paid
        //                       for with a card. Parity, and it is Filter.
        //   Zuko, Conflicted  — "hasn't been chosen", full stop: the mode runs
        //                       once in the game and then you must pick
        //                       another. Not repeatable, and untagged.
        //
        // So the distinction is readable — "this turn" is right there in the
        // words — but the human ruled the family too card-specific to encode
        // ("sono super specifiche"), and no rule is written. Recorded with its
        // reasons rather than as a bare count, so the option is not re-derived
        // from scratch and is not mistaken for an inconsistency.

        private static readonly Regex GrantedTriggeredDraw = Rx(
            "\"[^\"\n]{0,80}(?:whenever|at the beginning of)[^\"\n]{0,140}"
            + "draws? (?:a|one) card(?![^\"\n]{0,30}discard)");

        // A draw the card spends ITSELF on, written without a colon so
        // DrawBySacrificingItself cannot see the cost line. Lim-Dûl's Paladin
        // sacrifices itself to draw one on an upkeep it will only reach once,
        // and Preferred Selection sacrifices itself to move one card into your
        // hand. Two reviewed cards say it; neither is tagged.
        private static readonly Regex SpendsItselfWithoutAColon = Rx(
            @"sacrifice this \w+ and (?:pay|draw)");

        // The CardAdvantage readings of the 10-06..10-11 sitting, each tagged by
        // hand on every reviewed card that says it (2026-10-14):
        //   a PILE off the top of your library, one pile into your hand (Curator
        //     of Destinies, Intrude on the Mind, Sauron's Ransom). Not a pile of
        //     graveyard cards (Split the Spoils), which is Regrowth;
        //   SEVERAL kept out of a look — "put two of those cards", "up to two
        //     … put them into your hand", a card of each type (Atraxa, Grand
        //     Unifier, Reality Heist, Sinuous Benthisaur, Hurkyl, Master Wizard).
        //     "Look at N, take 2" is CardAdvantage alone, ruled 2026-10-13. Not
        //     the second card a kicker buys "instead" (Consult the Star Charts);
        //     "three" is the same reading a fortiori (Skyclave Plunder). The
        //     other numbers, "of them", "of the revealed cards" and "either
        //     pile" were written first and MEASURED AND DROPPED: no card in the
        //     dump says them (2026-10-14).
        private static readonly Regex PileFromTheTopIntoYourHand = Rx(
            @"(?:top|library)[^\n]{0,200}put (?:that|one) pile into your hand");

        private static readonly Regex SeveralKeptFromALook = Rx(
            @"put (?:two|three) of those cards into your hand(?![^.\n]{0,20}instead)"
            + @"|reveal up to two [\w ]{0,30}cards from among them and put them into your hand"
            + @"|for each card type[^,\n]{0,60}, (?:you may )?put a card of that type from among [^.\n]{0,40}into your hand");

        //   a SAGA CHAPTER that fires more than once and hands you a card each
        //     time, or a loot that gains one each time (The Bath Song, An
        //     Unearthly Child, The Day of the Doctor);
        private static readonly Regex ChaptersRepeatACard = Rx(
            @"^[ivx]+(?:, [ivx]+)+ — [^\n]*(?:put that card into your hand|you may play that card|draw two cards, then discard a card)",
            RegexOptions.Multiline);

        //   JUNK tokens made again and again or several at once, the Clue count
        //     again (Rose, Cutthroat Raider at every end of combat, Mister
        //     Gutsy's X). One Junk made once is a rider (Break Down, Junk Jet).
        //     "Three Junk tokens" (Junktown), "where X" and "a Junk token for
        //     each" were MEASURED AND DROPPED: every card saying them is reached
        //     by another reading already;
        private static readonly Regex JunkAgainOrSeveral = Rx(
            @"create x junk tokens"
            + @"|(?:whenever|at the beginning of|at end of combat)[^\n]{0,200}junk token");

        //   a DRAW MODE you may choose more than once (Riveteers Confluence,
        //     Season of Weaving);
        //     "Target player draws a card" is the same mode, pointed at you
        //     (Unite the Coalition, read 2026-10-23).
        private static readonly Regex DrawModeChosenAgain = Rx(
            @"you may choose the same mode more than once[\s\S]{0,200}(?:—|•)\s*(?:you |target player )?draws? a card");

        //   and REBOUND handed to your spells, each cast twice (Ojer Pakpatiq).
        //     "Have rebound" (Cast Through Time) is reached already by the
        //     rebound reminder; MEASURED AND DROPPED.
        private static readonly Regex GrantsRebound = Rx(@"\bgains? rebound");

        // RUMMAGING for as many as you threw away is selection at no gain:
        // Fable of the Mirror-Breaker's chapter II, "discard up to two cards.
        // If you do, draw that many cards" (Filter by hand, 2026-10-14). The
        // DrawThatMany count is not asked of it. Fable is the only card in the
        // dump that says it, so it is read as Fable says it.
        private static readonly Regex RummagesThatMany = Rx(
            @"discard up to \w+ cards\. if you do, draw that many");

        // A card that sacrifices ITSELF BY NAME to draw one card runs once,
        // the same as "Sacrifice this artifact: Draw a card" — Stone of Erech
        // and Five Hundred Year Diary, untagged by hand (2026-10-14). Only a
        // bare "Draw a card." closing the line: Hylda's Crown of Winter draws
        // one for each tapped creature and ED-E, Lonesome Eyebot one per
        // counter more, and both are tagged.
        private static bool DrawsOnceBySacrificingItsName(Card card, string text)
        {
            string name = ShortName(card);
            if (name.Length == 0)
            {
                return false;
            }

            return Regex.IsMatch(text,
                @"^[^\n:]{0,60}sacrifice " + Regex.Escape(name) + @"[^\n:]{0,30}:[^\n]{0,60}\bdraw a card\.?$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        // "Until end of turn, WHENEVER …" on an instant or a sorcery is a window
        // of one turn, not an ability that comes back: Waltz of Rage exiles a
        // card for each creature of yours that dies this turn and lets you play
        // it, and it is a rider on a Wipe (hand: Wipe alone, read 2026-10-16).
        // The word is blanked for a SPELL before the repeat gate is asked. A
        // Saga's chapter II, III does repeat (Summon: Leviathan keeps the tag
        // by its own chapters), and so does a permanent's.
        private static readonly Regex ForOneTurnOnly = Rx(@"(?<=until end of turn, )whenever ");

        // A card type JOINED to a creature type — "an artifact or Human spell"
        // (Arcade Gannon, tagged) — is open to more than the tribe, so the
        // tribal strip is not asked of it. Ruled 2026-10-16 with the tribal
        // graveyard cast (C6); case-sensitive on the type, as TribalCard is.
        private static readonly Regex TribeOrACardType = new(
            @"\b(?:artifact|enchantment|instant|sorcery|planeswalker|land|permanent) (?:or|and/or) [A-Z]",
            RegexOptions.CultureInvariant);

        // Exiling a creature YOU CONTROL and playing that card back is the same
        // card returning, not a card gained: Norin, Swift Survivalist exiles
        // the creature that was blocked and lets you play it from exile this
        // turn (untagged, read 2026-10-16, 0 of 1).
        private static readonly Regex ExilesYourOwnAndPlaysItBack = Rx(
            @"\byou control\b[^.\n]{0,60}, you may exile it\. you may (?:play|cast) that card from exile");

        // A flashback (or a cast) bought by SACRIFICING A CLUE trades a card for
        // a card: The Fugitive Doctor, hand Regrowth alone (read 2026-10-16,
        // 0 of 1). Written for that wording only — "you may sacrifice a Clue.
        // When you do, … gains flashback" — and measured not to move Lazav,
        // Piper Wright, Alquist Proft, Lonis or Unshakable Tail.
        private static readonly Regex PaysAClueForTheCast = Rx(
            @"you may sacrifice a clue\. when you do,[^\n]{0,120}\b(?:flashback|cast)\b");

        // A cast out of a graveyard, or a keyword granted to the graveyard, that
        // CHARGES OTHER CARDS FROM THAT GRAVEYARD — forage ("exile three cards
        // from your graveyard or sacrifice a Food") or escape ("Exile four
        // other cards from your graveyard") — pays cards for the card it gets
        // back: 0 of 11 reviewed cards written this way are tagged CardAdvantage
        // (read 2026-10-16: The Grim Captain's Locker, Osteomancer Adept).
        // Tarrian's Journal // Tomb of Aclazotz casts for nothing and keeps the
        // tag from the stream it already had.
        private static readonly Regex ChargesOtherGraveyardCards = Rx(
            @"\bforag(?:e|ing)\b|\bescape—[^""\n]{0,40}exile \w+ other cards from your graveyard");

        // An ability paid by sacrificing the card BY ITS SHORT NAME runs once,
        // like "Sacrifice this artifact:" does — and the self-sacrifice veto
        // (DrawBySacrificingItself) reads only the words "this artifact",
        // so Vohar, Vodalian Desecrator's "{2}, Sacrifice Vohar: You may cast
        // target instant or sorcery card from your graveyard this turn" was
        // taken for a stream of casts. Hand: Filter, Burn, Regrowth, no
        // CardAdvantage (read 2026-10-16, 0 of 1 tagged). The line is blanked
        // and the cast stream asked again, so a card that casts on another
        // line keeps the tag.
        private static string WithoutSelfSacrificeByName(Card card, string text)
        {
            string name = ShortName(card);
            if (name.Length == 0)
            {
                return text;
            }

            return Regex.Replace(text,
                @"^[^\n:]{0,60}sacrifice " + Regex.Escape(name) + @"\b[^\n:]{0,30}:[^\n]*$", " ",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        // A card off the top of an OPPONENT's library that you may play or cast,
        // under an ability that REPEATS, is CardAdvantage next to Steal. Ruled
        // 2026-10-16 (C1): the trigger ("whenever", "at the beginning of") or the
        // activated cost can be paid again, so the pile keeps growing for you,
        // and the same ruling covers the two wordings the older impulse rules
        // could not read — "look at the top card of that player's library, then
        // exile it face down. You may play that card" (Rev, Tithe Extractor,
        // Blightwing Bandit, Edward Kenway, Dream-Thief's Bandana) and "that
        // player exiles cards from the top of their library until they exile …
        // You may cast that card" (Bismuth Mindrender, Gríma, Strago and Relm) —
        // plus the exile with a price or a count in between (Reno and Rude,
        // Fire Lord Ozai, Rakdos, the Muscle's mana value in cards).
        //
        // Out, by the gate rather than the wording: a one-shot single card
        // (Ensnared by the Mara; Crabomination, whose pile is theirs by the
        // graveyard half), a repeat that spends the card itself (Grinning Totem
        // sacrifices itself), and a repeat PAID WITH A CARD (Synthesis Pod:
        // "Exile a spell you control" is a card for a card, parity).
        private static readonly Regex TheirTopForYou = Rx(
            // The LOOK first, then the exile face down (the sentence about the
            // play may follow the exile by a sentence of its own).
            @"look at the top (?:\w+ )?cards? of (?:that|target|each|their) ?(?:player|opponent)?'?s? ?library"
            + @"[^\n]{0,60}\bexile (?:it|those cards|them)\b[^\n]{0,120}\byou may (?:play|cast)"
            // The EXILE of the top card, with a sentence or a price before the
            // permission: Reno and Rude's "Then you may sacrifice another
            // creature or artifact. If you do, you may play the exiled card",
            // Fire Lord Ozai's "Until end of turn, you may play one of those
            // cards".
            // The BOTTOM card, face down, is the same card (Arvinox, the Mind
            // Flail, every end step, tagged, read 2026-10-27, 1 of 1).
            + @"|\bexile the (?:top|bottom) card of (?:that|target|each) (?:player|opponent)'?s library(?: face down)?\.[^\n]{0,160}\byou may (?:play|cast)"
            // …as many as the mana value of what was sacrificed (Rakdos, the Muscle).
            + @"|\bexile cards equal to [^.\n]{0,40} from the top of (?:that|target|each) (?:player|opponent)'?s library\."
            + @"[^\n]{0,80}\byou may (?:play|cast) those cards"
            // THEY dig until they find the card, and you cast it. A sentence
            // of its own for the cast, not the villainous choice's ", then you
            // may cast" (Ensnared by the Mara, a one-shot in any case).
            + @"|\bexiles? cards from the top of their library until (?:they|that player) exile [^.\n]{0,80}\. "
            + @"you may (?:cast|play) (?:that card|it)\b"
            // The top of EACH of the opponents a trigger names (Breeches, Brazen
            // Plunderer, read 2026-10-23).
            + @"|\bexile the top card of each of (?:those|your) opponents'? libraries\.[^\n]{0,40}\byou may play those cards");

        // Their top several, taken in one go: a draw-two wearing a different coat
        // (the "take more than one" reading, as Petty Larceny's two cards and
        // Black Cat, Cunning Thief's two of nine). Needs no repeat.
        private static readonly Regex SeveralOffTheirTop = Rx(
            @"look at the top (?:\w+ )?cards of (?:that|target|each) (?:player|opponent)'?s library"
            + @"[^\n]{0,60}\bexile (?:those cards|them|(?:two|three|four|five|x|\d+) of them)\b"
            + @"[^\n]{0,200}\byou may (?:play|cast) (?:those|them|the exiled) cards");

        // A card out of an opponent's GRAVEYARD that you may then cast, under an
        // ability that repeats, ruled 2026-10-16 (C2). Saruman of Many Colors
        // copies a spell from their graveyard on every second spell; a
        // one-shot stays Steal alone (Hama, the Bloodbender; Shadow of the
        // Enemy), and so do the cards that put theirs onto the battlefield
        // (Ashen Powder, Fire Nation Salvagers: Reanimate) and the pile that is
        // theirs already (Azula, Cunning Usurper; Crabomination).
        private static readonly Regex TheirGraveyardCardForYou = Rx(
            @"from (?:an|that|target|each) (?:opponent|player)'?s graveyard\.(?: copy the exiled card\.)? "
            + @"you may (?:cast|play) (?:the copy|that card|it|the exiled card)\b");

        // Cards you do not own, exiled out of an opponent's graveyard and played
        // from exile on every one of your turns: Tinybones, Bauble Burglar's
        // stash counters. A standing permission, so it needs no repeat gate.
        private static readonly Regex TheirCardsFromExile = Rx(
            @"you may play cards you don't own with [\w ]{1,20} counters on them from exile");

        // A repeat that does not pay a CARD for the card: the ability repeats at
        // no cost to the permanent itself, and its cost does not exile a spell
        // or card or discard one (Synthesis Pod).
        private static readonly Regex PaysACardToRepeat = Rx(
            @"^[^\n:]{0,80}\b(?:exile an? (?:[\w ]{0,20} )?(?:spell|card)|discard (?:a|an|\w+) cards?)\b[^\n:]{0,40}:",
            RegexOptions.Multiline);

        // Reminder text and quoted abilities are not the card's own cost: Rev,
        // Tithe Extractor's Treasure reminder sacrifices "this token", and
        // Strago and Relm's creature is sacrificed at the end step.
        private static bool RepeatsWithoutPayingACard(string ability)
        {
            string own = Quoted.Replace(Parenthetical.Replace(ability, " "), " ");
            return AbilityRepeatsAtNoCostToItself(own) && !PaysACardToRepeat.IsMatch(own);
        }

        // A card searched out and EXILED to be cast is the tutor's one card,
        // not one more: Beseech the Mirror (Tutor alone by hand, 2026-10-14).
        // "Exile that card, then shuffle. You may cast the exiled card" in a
        // sentence of its own (Evolving Door, Sacrifice and Tutor by hand, read
        // 2026-10-23).
        private static readonly Regex TutoredIntoExile = Rx(
            @"search your library for [^.\n]{0,40}exile it"
            + @"|search your library for [^.\n]{0,120}\. exile that card, then shuffle\. you may cast the exiled card");

        // Read 2026-10-23, from the sitting of 2026-10-20..23: shapes the human
        // tagged CardAdvantage on the one or two reviewed cards that say them,
        // each a rule this tag already has in words no rule read.
        //  - the same impulse on entering AND on leaving, two cards (Cryogen
        //    Relic's reading): Experimental Synthesizer;
        //  - several revealed and kept, when a card TYPE is named rather than a
        //    creature type (Forging the Anchor, against Karumonix's Rats, Filter
        //    alone, the tribal reading of 2026-10-16);
        //  - a card back from a graveyard on every hit: Ghost of Ramirez
        //    DePietro;
        //  - a draw repeated by the spell itself: Invoke Despair;
        //  - the top four exiled every upkeep with a card played from them
        //    (Fireglass Mentor's reading): Journey to the Lost City;
        //  - the draw each player takes on a repeating trigger, Howling Mine's
        //    reading past the 160 window: Master of Ceremonies (Tenuous Truce
        //    was read already; Sergeant John Benton's "that many", untagged,
        //    is not reached);
        //  - squad copies that each draw as they enter (Niko and Nyla's
        //    reading): Sicarian Infiltrator;
        //  - a mill handed back to your hand on an activated ability: Sivriss,
        //    Nightmare Speaker;
        //  - one card off the top per target, all playable: Soulfire Eruption;
        //  - a token that taps to draw: Tamiyo's Notebook;
        //  - a death draw the encore copies repeat: Trove Tracker;
        //  - a planeswalker's reveal-until into the hand, the attack reading of
        //    Songbirds' Blessing: Vivien, Nature's Avenger;
        //  - a land's draw behind a life cost longer than the window: War Room;
        //  - a cantrip that splices, cast again on every spell: Everdream.
        // MEASURED AND REJECTED: a repeated MANIFEST of the top card as this
        // tag, 1 tagged of 9 (Cryptic Pursuit against Abhorrent Oculus,
        // Hauntwoods Shrieker and six more), left as a known disagreement.
        private static readonly Regex EntersOrLeavesImpulse = Rx(
            @"enters or leaves the battlefield, exile the top card of your library\. until end of turn, you may play that card");

        // The cards may be of a type CHOSEN first (For the Ancestors, kept
        // CardAdvantage on 2026-10-30: a chosen type is no tribe), where a
        // printed one is TribalCard's Filter (Karumonix's Rats).
        private static readonly Regex RevealsAnyNumberIntoHand = Rx(
            @"reveal any number of [\w ,/-]{0,40}cards (?:of the chosen (?:creature )?type )?from among them and put (?:them|the revealed cards) into your hand");

        private static readonly Regex GraveyardCardOnEveryHit = Rx(
            @"^whenever [^\n]{0,60}deals combat damage to a player, choose up to one target card in a graveyard\b"
            + @"[^\n]{0,80}\. put that card into its owner's hand",
            RegexOptions.Multiline);

        private static readonly Regex DrawThenRepeat = Rx(@"\byou draw (?:a|one) card\. then repeat this process\b");

        private static readonly Regex UpkeepExilesSeveralIntoPlay = Rx(
            @"^at the beginning of your upkeep, exile the top (?:two|three|four|five|\d+) cards of your library\b"
            + @"[\s\S]{0,120}from among those cards onto the battlefield",
            RegexOptions.Multiline);

        private static readonly Regex EachPlayerDrawsEveryTurn = Rx(
            @"^(?:whenever|at the beginning of)[^\n]*\byou and (?:that|each) player each draw (?:a|one) card",
            RegexOptions.Multiline);

        private static readonly Regex SquadCopiesDraw = Rx(
            @"\bsquad\b[\s\S]*\bwhen [^,\n]{1,60}\benters\b[^,\n]{0,20}, draw (?:a|one) card");

        private static readonly Regex MillHandedBack = Rx(
            @"^[^\n:]{1,70}:[^\n]{0,60}\byou mill (?:a|one) card, then return that card from your graveyard to your hand",
            RegexOptions.Multiline);

        private static readonly Regex ExilesOnePerTargetAndPlays = Rx(
            @"\bfor each of them, exile the top card of your library\b[^\n]{0,200}\byou may play the exiled cards");

        private static readonly Regex TokenThatTapsToDraw = Rx(@"\btoken with [^\n]{0,100}""\{t\}: draw (?:a|one) card");

        private static readonly Regex DeathDrawRepeatedByEncore = Rx(
            @"\bwhen this creature dies, draw (?:a|one) card\b[\s\S]*\bencore\b");

        // A dig until a card TYPE turns up, made ONCE, the one card into your
        // hand and the rest to the bottom: a card for a card, so Filter by the
        // count (Yuna's Whistle, a spell, and Artificer Class's level 2, tagged
        // 2026-09-10 and 2026-10-23 — 2 of 2 reviewed). The same dig AGAIN is a
        // card each time (Vivien, Nature's Avenger's −3 is CardAdvantage; see
        // LoyaltyRevealsUntilIntoHand), so the caller asks the ability whether
        // it repeats. That gate is also what keeps out the LAND dug for (House
        // Cartographer, ManaFixing) and the TRIBE's (Nissa, Resurgent Animist):
        // both repeat, and vetoes for a land and a chosen type were tried and
        // moved no card.
        private static readonly Regex RevealsUntilATypeIntoHand = Rx(
            @"reveal cards from the top of your library until you reveal an? "
            + @"[\w ]{0,30}card\. put (?:that|the revealed) card into your hand and the rest on the bottom of your library");

        private static readonly Regex LoyaltyRevealsUntilIntoHand = Rx(
            @"^[+−-]?\d+: reveal cards from the top of your library until you reveal [^.\n]{0,80}\.[^\n]{0,80}\bput (?:that card|it) into your hand",
            RegexOptions.Multiline);

        private static readonly Regex LifePaidForADraw = Rx(
            @"^[^\n:]{0,40}pay life equal to [^\n:]{0,80}: draw (?:a|one) card",
            RegexOptions.Multiline);

        private static readonly Regex SplicedCantrip = Rx(@"^draw (?:a|one) card\.\s*splice onto\b", RegexOptions.Multiline);

        // Read 2026-10-27, from the sitting of 2026-10-26: shapes the human
        // tagged on the one reviewed card that says them, each a rule this tag
        // already has in words no rule read (the others — Hakoda, Pip-Boy 3000,
        // Headliner Scarlett, Neriv, Arvinox, Coram, Aradesh — sit beside the
        // pattern they widen).
        //  - an activated draw closing a long line, "If one or more +1/+1
        //    counters are moved this way, you gain that much life and draw a
        //    card" (Black Panther, Wakandan King). Every activated draw past
        //    the 100 window was MEASURED AND REJECTED: 5 tagged, 4 not (Kogla
        //    and Yidaro, Aladdin's Lamp, Jeweled Bird, Vikya);
        //  - your spell exiled as it resolves to be cast again, every time
        //    (rebound's reading): Lilah, Undefeated Slickshot plots it; Forger's
        //    Foundry, tagged, is the other card written this way;
        //  - a d20 result that draws, on a trigger that repeats: Mathise, Surge
        //    Channeler;
        //  - "each draw half that many cards", granted on a tap (Master of
        //    Ceremonies' reading, each player drawing again): Sokrates,
        //    Athenian Teacher.
        private static readonly Regex DrawClosingAMoveThisWay = Rx(
            @"^[^\n:]{1,70}:[^\n]*\. if one or more [^.\n]{0,80} this way, [^.\n]{0,40}\band draw (?:a|one) card\.$",
            RegexOptions.Multiline);

        private static readonly Regex SpellExiledToCastAgain = Rx(
            @"^whenever you cast [^\n]{0,80}, exile that spell instead of putting it into your graveyard as it resolves",
            RegexOptions.Multiline);

        private static readonly Regex D20DrawOnARepeatingTrigger = Rx(
            @"^(?:whenever|at the beginning of)[^\n]*\broll a d20\.(?:\n[^\n]*)*?\n\d+(?:[–-]\d+)? \| (?:you )?draw (?:a|one) card",
            RegexOptions.Multiline);

        private static readonly Regex EachDrawsHalfThatMany = Rx(@"\beach draw half that many cards");

        // A draw counted by what the card made you DISCARD as it entered is the
        // rummage of Fable (RummagesThatMany): Indominus Rex, Alpha discards any
        // number of creature cards and draws one per keyword counter they gave
        // it (Filter by hand, read 2026-10-27, 1 of 1).
        private static readonly Regex DrawsForWhatItDiscarded = Rx(
            @"discard any number of [^.\n]{0,40}cards\b[\s\S]{0,400}\bdraw a card for each counter on it");
    }
}
