using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// The rule table — one entry per effect, a card gets the effect if ANY of its
    /// patterns hit — and the guards that are asked outside it. Part of
    /// <see cref="EffectClassifier"/>.
    /// </summary>
    internal static partial class EffectClassifier
    {
        // One entry per effect; a card gets the effect if ANY of its patterns hit.
        private static readonly (CardEffect Effect, Regex[] Patterns)[] Rules;

        // Both of these are the SAME ARRAY the table holds, looked up once —
        // hoisted out of it for the same reason as MillPatterns and
        // ReanimatePatterns: the vetoes that strip a tag re-ask its own patterns
        // against the text with the offending shape blanked out, so a card that
        // selects on one line and makes bodies on another keeps what it earned.
        private static readonly Regex[] FilterPatterns;

        private static readonly Regex[] BurnPatterns;

        // A STATIC CONSTRUCTOR rather than three field initialisers, and that is
        // forced by the split: initialisers run in textual order within a file but
        // in UNSPECIFIED order across the files of a partial class, so this table —
        // which reads two dozen pattern arrays that now live in the other files —
        // could be built from nulls. Every field initialiser is guaranteed to have
        // run before a static constructor body, so this is the one place the order
        // is safe.
        static EffectClassifier()
        {
            Rules =
            [
                (CardEffect.Tokens, TokenPatterns),

                // Mass removal first (a wiper also reads as removal; both are fine).
                // Mass DAMAGE is the oldest wiper wording there is and was missing
                // entirely: Earthquake, Crypt Rats and Fire Magic sweep a board
                // without the word "destroy" appearing anywhere. [\dX] because the
                // interesting ones scale — "deals X damage to each creature".
                // Every one of these used to stop at the first adjective, which is
                // where half the sweepers in the game put one: "destroy all WHITE
                // permanents" (Anarchy), "destroy all GREEN creatures" (Perish),
                // "1 damage to each WHITE AND/OR BLUE creature" (Evaporate). Widened
                // 2026-09-15, together with the two other ways a board gets emptied —
                // returning it all to hand, and making everybody sacrifice.
                (CardEffect.Wipe, [
                    Rx(@"(?:destroy|exile) all(?: [\w-]{1,15}){0,3} (?:creature|permanent|nonland)"),
                    Rx(@"(?:all|each)(?: [\w-]{1,15}){0,3} creatures get -[\dX]+/-[1-9X]"),
                    Rx(@"deals? [\dX]+ damage to each(?: [\w/-]{1,20}){0,4} creature"),
                    Rx(@"return (?:all|each)[\w -]{0,30}(?:permanent|creature)s?[\w ,'-]{0,40}to (?:their owners'|its owner's) hands?"),
                    Rx(@"each player[\w ,'-]{0,80}sacrifices the rest|each player sacrifices[\w ]{0,20}(?:creature|permanent)")]),

                // "Nonland permanent" is how the whole modern O-ring family is worded
                // (Stormplain Detainment, Web Up, Emergency Eject), and reading only
                // the bare "target permanent" left 22 of 31 hand-tagged cards
                // untouched. Ruled 2026-09-15: an answer that can point at ANY
                // permanent is RemovePermanent, not Removal, even when the card that
                // exiles it happens to be aimed at a creature in practice.
                // Recall 29.0% -> 83.9%, 24 wrong -> 12.
                (CardEffect.RemovePermanent, [
                    Rx(@"(?:destroy|exile) (?:[\w ]{0,15})?target nonland permanent"),
                    Rx(@"(?:destroy|exile) (?:[\w ]{0,15})?target permanent"),
                    // A COLOUR-restricted permanent kill belongs here rather than to
                    // Removal, ruled 2026-09-20. It was tried as Removal the day
                    // before, on the reading that "destroy target red permanent" is
                    // aimed at a creature in practice — but the BREADTH is what this
                    // tag is for, and it is the same reading that keeps Vindicate
                    // here even though you usually point it at a creature. Active
                    // Volcano, Flash Flood and both Paladins.
                    //
                    // NB the colours are listed one by one on purpose. A "non\w+"
                    // alternation written to catch "nonblack permanent" also catches
                    // "NONLAND permanent", which the first rule above already reads —
                    // 26 false positives in one measurement while it sat on Removal.
                    Rx(@"(?:destroy|exile) target (?:white|blue|black|red|green|colorless|multicolored"
                        + @"|nonwhite|nonblue|nonblack|nonred|nongreen)[\w ]{0,15}permanent")]),

                // Three deliberate tightenings, each from a card that fooled a looser
                // version of these rules:
                //   \b before "land"   — "islandwalk" and "an Island" contain the
                //                        letters but not the word.
                //   {0,2} filler words — "destroy target Aura attached to a land"
                //                        (Pyramids) puts four words in between, and
                //                        it PROTECTS lands rather than killing them.
                //   a named victim     — "at the beginning of your upkeep, sacrifice
                //                        a land" (Serendib Djinn) is a drawback you
                //                        pay, not an effect you aim at someone.
                //
                // Widened 2026-09-19, where all 23 of the misses were: the land is
                // almost never alone in the sentence. It sits in a TYPE LIST
                // ("destroy target artifact, creature, or land" — Aftershock, Creeping
                // Mold, Amulet of Unmaking), or behind a count ("destroy UP TO ONE
                // target artifact, up to one target creature, and up to one target
                // land" — Boom Box), or beside a second target ("destroy target
                // creature AND target land" — Fumarole), and the {0,2} filler words
                // could not cross a comma to reach any of them. The mass form has the
                // same problem: "destroy all CREATURES AND lands" (Devastation).
                //
                // A BASIC LAND TYPE is a land by another name, and the only wording
                // Boil, Boiling Seas, Acid Rain and Reign of Chaos ever use. The \b
                // that keeps "islandwalk" out is still doing its job — the letters of
                // "land" inside "Island" have a word character in front of them.
                // Four shapes match the words and are not this tag; they are read line
                // by line in AnswersALand below.
                (CardEffect.LandDestruction, LandDestructionPatterns),

                // Damage pointed at something is how most of the game kills a
                // creature, and reading it as Burn alone left 231 of 291 human-tagged
                // Removal cards untouched — by far the widest gap measured. Blaze and
                // Broadside Barrage answer a threat exactly the way Doom Blade does;
                // that the wording says "damage" rather than "destroy" is a detail of
                // the era a card was printed in, not a difference in what it does.
                //
                // "any target" is included deliberately, though it also covers a
                // player. The 2026-09-04 ruling drew the line at target CREATURE, but
                // measured against 3664 reviewed cards the human tags "deals X damage
                // to any target" as Removal in 68 of 75 cases, and the ruling was
                // widened to match that on 2026-09-11 rather than the other way round.
                //
                // Fight is the same act with the damage delegated to a creature you
                // already control, and an edict removes without ever saying "target".
                // The lookbehind is load-bearing in every one of these: NONCREATURE
                // ends in "creature", so "destroy target noncreature artifact"
                // (Gorilla Shaman, Joven) read as a kill. Same shape as tap/untap.
                (CardEffect.Removal, [
                    Rx(@"destroy target[\w ]*(?<!non)creature"), Rx(@"exile target[\w ]*(?<!non)creature"),
                    Rx(@"destroy target[\w ]*((?<!non)creature|planeswalker)"),
                    // The AMOUNT is written as freely as the target: "half X damage,
                    // rounded down" (Banshee), "X plus 1 damage" (Meteor Shower). And
                    // "any target" has two more spellings — "any OTHER target"
                    // (Self-Destruct) and "EACH OF X targets" (Firestorm).
                    Rx(@"deals? (?:half )?[\dX]+(?: plus \d+)? damage(?:, rounded (?:down|up),)? "
                        + @"to (?:any (?:other )?target|that creature(?!'s)|each of [\dX]+ targets)"),
                    // The damage rules could not read a QUALIFIED target — "damage to
                    // target ATTACKING creature", "to target creature AN OPPONENT
                    // CONTROLS" — which was 23 of the misses on its own, and the
                    // amount is allowed to come after the target as well.
                    // NB the qualifier deliberately cannot cross a COMMA. Letting it
                    // reach "4 damage to target ATTACKING, BLOCKING, OR TAPPED
                    // creature" (Sonar Strike) also lets it run past a sentence into
                    // the next clause: measured 2026-09-19, two right for two wrong.
                    Rx(@"deals? [\dX]+ damage to [\w ]{0,20}target [\w -]{0,28}(?<!non)creature"),
                    // "That much damage" is an amount too — Screaming Nemesis hands
                    // back whatever it was dealt.
                    Rx(@"deals? that much damage to any (?:other )?target"),
                    Rx(@"deals damage to [\w ]{0,20}target [\w -]{0,28}(?<!non)creature equal to"),
                    // A fireball split between several things still kills one of them.
                    Rx(@"deals? (?:half )?[\dX]+(?: plus \d+)? damage divided "
                        + @"(?:evenly, rounded down, |as you choose )?among"),
                    // A fight has to name what it fights. The bare word also appears
                    // in the NAME of a mode — School Daze offers "Fight Crime", which
                    // counters a spell and draws a card.
                    Rx(@"\bfights? [\w ,'-]{0,30}target|\bfights? (?:each|it)\b"),
                    // An edict, in every wording — but aimed at THEM. "Each player
                    // sacrifices" costs you a creature too, and the hand-tagging
                    // declines those (Abyssal Gatekeeper, Pillar Tombs of Aku).
                    Rx(@"(?:target player|target opponent|each opponent)[\w ,]{0,30}sacrifices? (?:a|an|one|two|\d+)[\w ]{0,25}(?<!non)creature"),
                    // A creature that ends up in a LIBRARY is as answered as one that
                    // is destroyed, and the Auras are the only place this wording
                    // appears: The Spot's Portal puts it on the bottom, Dramatic
                    // Accusation, Stay Hidden Stay Silent and Watery Grasp shuffle it
                    // in. Added 2026-09-19.
                    Rx(@"put target[\w ,'-]{0,30}(?<!non)creature[\w ,'-]{0,25}on the bottom of[\w ']{0,25}library"),
                    Rx(@"shuffles? enchanted creature into[\w ']{0,25}library"
                        + @"|enchanted creature'?s owner shuffles it into")]),

                (CardEffect.Counter, [Rx(@"counter target[\w ]*spell")]),

                // The other way to answer something on the stack. "copy target" is
                // required rather than the bare word "copy": a token that enters "as a
                // copy OF target creature" is Tokens, not stack interaction.
                // Widened 2026-09-19 for the possessive: Meddle says "change THAT
                // SPELL'S TARGET to another creature" where Deflection says "change
                // the target of", and no rule reading the second could see the first.
                //
                // The DELAYED COPY was tried the same day and ruled out: "when you
                // next cast an instant or sorcery spell this turn, copy it … you may
                // choose new targets for the copy" is a card doubling a spell of YOUR
                // OWN, which is not acting on somebody else's spell on the stack. It
                // matched 8 reviewed cards, 4 tagged and 4 not, and Ether reads word
                // for word like Jeong Jeong; the ruling took the tag off all eight
                // rather than keep a rule that could not tell them apart. What stays
                // is the aimed copy — "copy TARGET instant or sorcery spell" — which
                // is Fork, Reverberate and Twincast.
                (CardEffect.Redirect, [Rx(@"change the targets? of"),
                    Rx(@"change (?:that|target) spell's targets?"),
                    Rx(@"cop(?:y|ies) target[\w ]*(?:spell|ability)")]),

                // Bounce has to say WHICH permanent goes back, because the bare
                // sentence is just as often the price the card pays: Ovinomancer's
                // "{T}, Return this creature to its owner's hand:" is a cost and
                // Fleeting Effigy's end-step return is a drawback. Requiring
                // "target" (or a mass "return each/all") took the tag from 74 wrong
                // out of 114 fired down to 8 wrong out of 56.
                //
                // The filler before "target" is what admits "return UP TO ONE OTHER
                // target nonland permanent"; the possessive alternation is what
                // admits the plural "to their owners' hands".
                (CardEffect.Bounce, [
                    Rx(@"return (?:[\w' ]{0,30})?target[\w ,']*to (?:its|their) owner(?:'s|s'|s)? hands?"),
                    Rx(@"return (?:each|all|every)[\w ,']*to (?:its|their) owner(?:'s|s'|s)? hands?")]),

                // The old rule was two lines of `destroy target[\w ]*(artifact|
                // enchantment)`, and that `[\w ]*` was free to run the length of the
                // sentence. It is where 11 of the 15 false positives came from, in two
                // shapes: "destroy target NONartifact, nonblack creature" (Terror,
                // Nekrataal, The Abyss) reaches the noun straight through "non", and
                // "exile target nonland permanent an opponent controls until this
                // ENCHANTMENT leaves the battlefield" (Web Up, Seam Rip, White
                // Auracite) reaches it through the reminder of where the card comes
                // back from. The filler is bounded now, and "non"/"non-" cannot be
                // crossed to get to the noun.
                //
                // The misses were counting words. The rule wanted "destroy target
                // <noun>" and nothing else, so "destroy UP TO ONE target artifact",
                // "destroy X target artifacts", "exile TWO target artifacts",
                // "destroy ANOTHER target artifact" and every mass "destroy all
                // enchantments" walked past it — 20 of the 28 misses between them.
                // AURA is the third noun: Serene Heart and Hope Charm answer the same
                // cards this tag exists for.
                //
                // An ARTIFACT CREATURE is a creature, and the human tags it Removal:
                // Chandler and Hearth Charm both read "destroy target artifact
                // creature" and neither is tagged here, so the noun refuses to be
                // followed by "creature".
                //
                // An AURA ATTACHED TO something named is about that thing, not about
                // the Aura: Savaen Elves and Pyramids ("destroy target Aura attached
                // to a land") are land protection, Miracle Worker pulls an Aura off
                // your own creature, and Hakim and Gauntlets of Chaos strip the ones
                // on a permanent the card itself just touched. A bare "destroy target
                // Aura" (Hope Charm) or "destroy all Auras" (Serene Heart) still
                // counts, which is why the noun tests only for the words that follow.
                //
                // Three more shapes match the words and are not this tag; they are
                // read line by line in AnswersAnArtifactOrEnchantment below.
                (CardEffect.Disenchant, DisenchantPatterns),

                // Same reading as Mill, and the same result. A bare "discard a card"
                // is overwhelmingly a COST — madness, blitz, cycling reminder text,
                // the second half of looting — rather than an attack on a hand.
                // Naming the victim took Discard from 129 wrong out of 188 fired to
                // 16 wrong out of 69, the largest precision gain on the board.
                (CardEffect.Discard, [Rx(OtherPlayer + @"[\w ,]{0,30}discards?\b")]),

                // Drawing ONE card off a spell you cast replaces the spell — that is
                // card parity, not advantage, which is why Eject and Broadside
                // Barrage do not earn the tag for their trailing "Draw a card". The
                // tag needs a card the opponent does not get: draw two or more, or a
                // draw you can go back to. Decided 2026-09-11 after measuring that
                // firing on any bare "draw a card" cost 179 false positives, the
                // worst precision on the board at 42%.
                //
                // Repeatable means an activated ability (a cost, then a colon) or a
                // recurring trigger. "Whenever" and "At the beginning of" qualify;
                // plain "When this creature enters" does not, because an ETB fires
                // once and is therefore the same one-shot replacement as a cantrip.
                (CardEffect.CardAdvantage, [
                    Rx(@"draws? (?:two|three|four|five|six|seven|eight|nine|ten|x|\d+) cards"),
                    Rx(@"^[^\n:]{1,70}:[^\n]{0,100}draws? (?:a|one) card", RegexOptions.Multiline),
                    Rx(@"^(?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card", RegexOptions.Multiline),
                    // The same trigger with ONE SENTENCE in front of it. Rowen
                    // says "Reveal the first card you draw each turn. Whenever
                    // you reveal a basic land card this way, draw a card", and
                    // the anchor above wants the trigger word first. Reading the
                    // trigger word ANYWHERE was measured on 2026-09-18 and loses
                    // 11, so this allows exactly one short sentence and no more.
                    Rx(@"^[^\n]{0,70}\. (?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card",
                        RegexOptions.Multiline),
                    // "An ADDITIONAL card" is a card, ruled 2026-09-21. Howling
                    // Mine and Sylvan Library say it and are tagged; the one
                    // card that says it and is not — Anvil of Bogardan — takes
                    // the card straight back ("then discards a card") and the
                    // parity guard already reads that.
                    //
                    // "Draws UP TO n cards" joins it without the "may": that
                    // word is what separates Diminishing Returns, which is
                    // tagged, from Truce and Temporary Truce, where every player
                    // MAY decline and the two of them are not.
                    Rx(@"draws? (?:an|\w+) additional cards?|(?<!may )draws? up to \w+ cards"),
                    // "Draw a card for each creature you control" is multi-card draw
                    // written the other way round, and the count-first wording was
                    // missed entirely: Balance of Power, Baleful Stare, Become the
                    // Avalanche. Worth 24 recovered for 2 wrongly fired.
                    Rx(@"draws? a card for each|draws? cards equal to"),
                    // A trigger hiding behind a LABEL. The anchored rule above wants
                    // "whenever" at the start of the line, and modern cards put an
                    // ability word or a Siege bullet in front of it — "Eerie —
                    // Whenever ...", "• Jeskai — Whenever ...". Same bug as the
                    // impulse had, and the same fix; reading the trigger word ANYWHERE
                    // instead was measured and loses 11.
                    Rx(@"^(?:• )?[\w' ]{1,28}— ?(?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card",
                        RegexOptions.Multiline),
                    // A Saga chapter that names TWO numbers fires twice, so the card
                    // it draws was bought once and drawn again — the same reading
                    // ChapterFiresTwice gives the put-into-hand rule. Found
                    // 2026-09-20 on Jecht, Reluctant Guardian, whose "I, II — Jecht
                    // Beam — Each opponent discards a card and you draw a card" is
                    // hand-tagged CardAdvantage and was being missed.
                    Rx(@"^[ivx]+, [ivx]+ [^\n]{0,140}\byou draw (?:a|one) card", RegexOptions.Multiline),
                    // The top of your library is a second hand, ruled 2026-09-16:
                    // Fblthp, Glarb and the Traveling Chocobo never run out of cards
                    // to play even though they never draw one.
                    Rx(@"you may (?:play|cast)[^\n]{0,60}top card of your library"
                        + @"|play (?:lands|cards|the top card)[^\n]{0,40}from the top of your library"),
                    // Looking at N and taking MORE THAN ONE is a draw with selection.
                    // Taking exactly one is Filter, which the Filter rules say and
                    // this deliberately does not contradict. Ruled 2026-09-16.
                    // Widened 2026-09-21: the count can be X, the cards can be
                    // named "from among them", and the look can be written the
                    // other way round — Stargaze says "Look at TWICE X cards
                    // FROM THE TOP of your library. Put X cards from among them
                    // into your hand".
                    Rx(@"look at (?:the top \w+ cards?|twice \w+ cards?) (?:of|from the top of) your library"
                        + @"[^\n]{0,60}put (?:two|three|four|five|x|\d+) "
                        + @"(?:of them|of those cards|cards? from among them) into your hand"),
                    // The same thing counted one card at a time. Memories Returning
                    // says "Put one of them into your hand", then "Then you put one
                    // into your hand", then "Put the other into your hand" — three
                    // cards for a spell, which is advantage by the count and not by
                    // repetition. Written narrowly against the second "Then YOU":
                    // every other reviewed card that reaches into its hand twice
                    // does it CONDITIONALLY ("put two of those cards into your hand
                    // INSTEAD if this spell was kicked" — Consult the Star Charts,
                    // Accumulate Wisdom), and those are Filter and stay Filter.
                    Rx(@"into your hand[^\n]{0,120}then you put one into your hand"),
                    // The top of your library kept as a second hand, and several
                    // cards off a top that you may then play. See SecondHandOnTop
                    // and ExileSeveralAndPlayThem for the measurements.
                    SecondHandOnTop]),

                // Filter is selection at NO net gain: look at some and take one, or
                // hand back exactly what you drew. The rummage family — "you may
                // discard a card. If you do, draw a card" — is the plainest shape
                // there is and had never been read: 14 cards fire on the first
                // pattern below and 12 of them were already tagged by hand. The
                // third pattern is the activated version ("Discard a card: Draw a
                // card"); the lookahead is what keeps CYCLING reminder text out,
                // because cycling pays a card to replace itself and attacks nobody.
                // Added 2026-09-19.
                // Scry and surveil ARE Filter, unconditionally, ruled 2026-09-19 —
                // it does not matter what else the card does or how small the
                // number is. Read across every printed form, which "scry \d" was
                // not: the bulk carries "scry X" on 17 cards and "surveil X" on 6,
                // and a handful say "scries"/"surveils" because the subject is a
                // player. The count is required so that "whenever you scry OR
                // surveil" — a trigger that watches one happen — stays out.
                // EMPOWER JACE N makes a Jace planeswalker token whose whole job is
                // "[-1]: Surveil 1" and "[-3]: Draw a card", so the keyword is a
                // Filter by itself. Ruled 2026-09-19. Thirty-five cards print it and
                // 31 carry the reminder text, which the surveil rule below already
                // reads; this is for the four that do not — Sanctum Lurker,
                // Theorist's Sanctum, Jace Reality Sculptor and Fatehold Charm.
                // The token is a PLANESWALKER, so Tokens stays out of it.
                (CardEffect.Filter, [Rx(@"\bempowers? jace\b"),
                    Rx(@"\b(?:scry|scries|surveil|surveils) (?:\d+|x)\b"),
                    Rx(@"look at the top \w+ cards? of your library"),
                    Rx(@"discard[\w ]* then draw"), Rx(@"draws? [\w ]{0,20}cards?[.,] ?(?:then |and )?(?:you may )?discards?"),
                    // The same exchange written discard-first WITH a count:
                    // "Discard a card, then draw two cards" (Romantic Rendezvous,
                    // Summon: Kujata's third chapter), which the two patterns above
                    // both miss — one wants the draw first, the other cannot cross
                    // the comma. The lookahead keeps CYCLING out, which is what an
                    // earlier and looser version of this pattern swallowed: it fired
                    // on 45 cards of which only 4 were tagged.
                    Rx(@"discards? (?!this card)(?:a|one|two|three|four|five|x|\d+) cards?,? (?:then |and )(?:you )?draws?"),
                    Rx(@"you may discard (?:a|one|up to \w+|any number of) cards?\.? ?(?:if you do, )?draws?"),
                    Rx(@"discards? (?:a|one|up to \w+|any number of|that many) cards?, then draws? that many"),
                    Rx(@"^[^\n:]{0,50}discard (?!this card)(?:a|one|\w+) cards?[^\n:]{0,30}: ?draw",
                        RegexOptions.Multiline),
                    // The two halves change hands without the word "discard": you may
                    // draw and then hand one back (Oblivious Bookworm, Rook Turret),
                    // or pay the card by some other verb — Brainstorm and Dream Cache
                    // put cards back on top, Lat-Nam's Legacy shuffles one in,
                    // Jandor's Ring hands back exactly what it just drew. Each is a
                    // card changing places at no net gain, which is Filter exactly.
                    //
                    // These are spelled out rather than reusing DrawPaidForWithACard
                    // and Loot wholesale, which was tried and measured: Loot's middle
                    // alternative reads CYCLING reminder text as a loot and fires on
                    // 45 cards of which 4 are tagged, costing 42 false positives on
                    // its own. The alternatives kept here fire on 3, 1, 1, 5 and 1.
                    Rx(@"(?:you may )?draws? (?:a|one|\w+) cards?\. if you do, discard"),
                    Rx(@"(?:sacrifice[\w ]{0,25}or )?discard (?:a|one|\w+) cards?\. if you do, draw"),
                    Rx(@"put \w+ cards? from your hand[^\n]{0,30}on top"),
                    Rx(@"shuffle (?:a|\w+) cards? from your hand into your library\. if you do, draw"),
                    Rx(@"discard the last card you drew"),
                    Rx(@"then discard \w+ cards? unless"),
                    Rx(@"discards? a card\. then draws? a card"),
                    // A discard charged as an ADDITIONAL COST is still a card
                    // changing places — Grab the Prize pays one to draw two, which
                    // with the spell itself is exactly level, and is functionally
                    // Abandon Attachments with the discard moved into the cost line.
                    // Ruled 2026-09-19.
                    Rx(@"as an additional cost to cast this spell,[^\n.]{0,60}discard"
                        + @"[\s\S]{0,120}draws? (?:a|one|two|three|four|five|x|\d+) cards?")]),

                // The [\w ] runs still cannot cross a FULL STOP, which is what keeps
                // a card that exiles from a graveyard in one sentence and bounces a
                // creature in the next from reading as recursion. But they could not
                // cross a COMMA or a SLASH either, and that is where modern cards
                // put their card-type lists: "one or two target creature AND/OR
                // planeswalker cards", "non-Assassin historic card", "up to one
                // target creature card, up to one target Mount card, …". Widened
                // 2026-09-19 to allow punctuation inside the run but not a stop —
                // worth 4 more Regrowth and 2 more Reanimate, at no cost.
                (CardEffect.Reanimate, ReanimatePatterns),

                // The sibling of Reanimate: same origin, different destination.
                (CardEffect.Regrowth, RegrowthPatterns),

                (CardEffect.Mill, MillPatterns),

                (CardEffect.Buff, BuffPatterns),

                (CardEffect.Protection, ProtectionPatterns),

                // [\dX] rather than \d so a scaling burn spell counts: Blaze and Fireball
                // say "deals X damage", and matching only literal numbers left them as
                // Removal without being Burn once damage started reading as Removal —
                // an ontology that contradicted itself on the same sentence.
                //
                // Worth 15 recovered against 9 wrongly fired on the reviewed set, which
                // is thin: it is here for the consistency, not for the score. The wider
                // Burn problem is elsewhere and NOT fixed — "each" in the object list
                // below tags mass damage as Burn, which accounts for 37 false positives,
                // but the same wording is hand-tagged Burn on 22 other cards, so the
                // question of whether a sweeper burns is the user's to settle, not a
                // rule to quietly change.
                // Burn is damage aimed at a FACE. Damage pointed at a creature is how
                // the game kills creatures and is already Removal; reading it as Burn
                // as well is what gave the tag 129 false positives, Explosive Shot and
                // Fanged Flames among them. Two shapes qualify: a player named as the
                // target, and the sweeper that catches players on its way past —
                // Inferno's "each creature and each player", which is Wipe AND Burn,
                // while a sweeper that only hits creatures is Wipe alone. Ruled
                // 2026-09-15.
                //
                // "damage to you" is deliberately absent: a painland, Ancient Tomb and
                // Juzam Djinn charge themselves, and self-damage is a price the same
                // way a self-mill or a sacrifice cost is. Precision 58.7% -> 86.3%.
                (CardEffect.Burn, [
                    // The AMOUNT and the FACE, both read as widely as the game
                    // writes them. Every widening here was a card the rule already
                    // agreed with and simply could not read: "half X damage,
                    // rounded down" (Banshee), "each of X targets" (Firestorm),
                    // "any OTHER target" (Screaming Nemesis, Self-Destruct), "each
                    // OTHER opponent" (Parapet Thrasher), "defending player"
                    // (Ghost-Spider), "the player" (Monsoon), "them" (Vexing
                    // Arcanix) and "the controller of" (Suffocation).
                    Rx(@"deals? (?:half )?[\dX]+(?: plus \d+)? damage(?:, rounded (?:down|up),)? to " + BurnTarget + @"\b"),
                    Rx(@"deals? that much damage to " + BurnTarget + @"\b"),
                    Rx(@"deals? [\dX]+ damage to [\w ,]{0,45}each (?:player|opponent)"),
                    // "Each opponent loses 2 life" is a Lava Spike at every face at
                    // once; the game just declined to call it damage. 18 of the 27
                    // reviewed cards that say it are hand-tagged Burn, and every one
                    // of them was being missed. Ruled 2026-09-15.
                    //
                    // ONE point of life is a rider, not a burn. Ruled 2026-09-20
                    // after measuring the family: eleven reviewed cards drain
                    // exactly one and carry no Burn tag (Agate-Blade Assassin,
                    // Al Bhed Salvagers, Ebony Charm, Nafs Asp, Sanguine Syphoner,
                    // Venerated Stormsinger and five more), against two that do —
                    // and one of those two, Susurian Voidborn, is word for word
                    // Al Bhed Salvagers. From TWO the tag stands, and X counts
                    // because X is not one (Northern Air Temple). The subject list
                    // is spelled out so that "you lose 2 life" stays a price.
                    Rx(LosesLifeSubject + @" ?loses? (?:[2-9]|\d\d+|x) life"),
                    // The same life loss sized by a COUNT. Ruled 2026-09-20 and it
                    // splits cleanly on WHOSE life it is: four reviewed cards say
                    // "YOU lose life equal to" (Reanimate, Lich, Teval, Darkstar
                    // Augur) and none is tagged, because that is the price the card
                    // charges; seven say somebody else does and six are tagged.
                    Rx(LosesLifeSubject + @" ?loses? life equal to"),
                    // Damage DIVIDED among "targets" — bare, so a face can be one of
                    // them. Ruled 2026-09-20: five of the six reviewed cards written
                    // that way are tagged (Fireball, Rolling Thunder, Pyrotechnics,
                    // Meteor Shower, Mogg Mob), while all seven that say "among
                    // target CREATURES" are not, because those can never reach a
                    // player. The lookahead is Fiery Justice, the sixth, which the
                    // human ruled an exception ON PURPOSE: it hands the opponent
                    // back exactly the five life it dealt, so the count is zero.
                    Rx(@"damage divided (?:evenly,? )?(?:rounded down,? )?(?:as you choose )?"
                        + @"among (?:any number of|one, two, or three|\w+) targets\b"
                        + @"(?![^\n]{0,40}gains? [\dX]+ life)"),
                    // The same damage with the word order the other way round —
                    // "deals damage TO that player EQUAL TO the number of artifacts
                    // they control" — which the rule below could not read at all.
                    // Twelve of the eighteen reviewed cards written this way were
                    // already tagged and the human ruled the other six in on
                    // 2026-09-20, the upkeep punishers among them.
                    Rx(@"deals? damage to [\w ,'-]{0,40}" + BurnTarget + @"[\w ,'-]{0,20} equal to"),
                    // Damage sized by a COUNT rather than a digit. The rule for this
                    // already existed and fed Removal alone, so Cat-Gator's "damage
                    // equal to the number of Swamps to any target" was read as a kill
                    // and not as a burn. 11 of 12 such cards are tagged. The window
                    // was 45 characters and Summon: Bahamut puts 52 between the two
                    // halves ("the total mana value of other permanents you
                    // control"), and Cyclone names the creatures before the players.
                    Rx(@"deals damage equal to [\w' ,]{0,70}to [\w ,'-]{0,30}" + BurnTarget + @"\b"),
                    // Life paid to keep something from happening is life lost.
                    // Ruled 2026-09-20 with the rest of Burn: Breathstealer's Crypt,
                    // Sirocco and Cleansing all charge a player life to stop the
                    // card, and all three are tagged. Restricted to SOMEBODY ELSE
                    // paying, so an additional cost you pay stays a price.
                    // The threshold applies here too, with one escape: a single
                    // point charged FOR EACH of something is not one point at all
                    // (Cleansing charges one per land destroyed).
                    Rx(@"unless (?:that player|they|any player|its controller) pays? (?:[2-9]|\d\d+|x) life"
                        + @"|for each[^\n.]{0,70}unless (?:that player|they|any player|its controller) pays? [\dX]+ life"),
                    // "Loses 1 life FOR EACH card type" is not the one-point rider
                    // the threshold above refuses — the count is what it is
                    // multiplied by (Polluted Cistern, Fandaniel). The subject is
                    // asked for the same reason as everywhere else in this tag:
                    // Reign of Terror charges YOU two life for each creature it
                    // killed, and that is the price of a Wrath.
                    Rx(LosesLifeSubject + @" ?loses? [\dX]+ life for each")]),

                // Sacrifice is a sacrifice OUTLET: somewhere to put your OWN permanents
                // on demand, which is what makes a stolen creature (see Steal) worth
                // taking. Ruled 2026-09-15. Two things fall out of that and are handled
                // by the guard below: an edict is somebody else sacrificing, and an
                // additional cost to cast is a one-shot price rather than an outlet you
                // can point at anything. Precision 56.5% -> 88.1%, 84 wrong -> 44.
                //
                // The fuel can be a creature, an artifact, or a permanent named as such
                // ("Sacrifice a nontoken permanent:") — Atog, Dwarven Weaponsmith and
                // Infernal Tribute are all Ashnod's Altar with a different thing going
                // in. Artifacts were worth 12 recovered against 7 wrongly fired;
                // "permanent" a further 3 against 0.
                //
                // A LAND is deliberately not on the list, and does not need to be
                // excluded either: "sacrifice a land" is Harrow paying for a fetch and
                // never says "permanent", so it simply does not match.
                (CardEffect.Sacrifice, [Rx(@"sacrifices? (?:a|an|another|two|three|\d+)[\w ]*(?:creature|artifact|permanent)")]),

                // Three patterns of "gain control" saw one third of this tag. The rest
                // is in StealPatterns above: an EXCHANGE is a theft you paid for
                // (Juxtapose, Legerdemain, Political Trickery, Trade the Helm,
                // Gauntlets of Chaos), a card taken out of somebody else's LIBRARY,
                // HAND or GRAVEYARD and played is a theft of a card rather than of a
                // permanent (15 of the 28 misses, from Outrageous Robbery to Gonti),
                // and "onto the battlefield UNDER YOUR CONTROL" is how Bone Dancer,
                // Helm of Obedience and Desertion say it.
                (CardEffect.Steal, StealPatterns),

                // Widened 2026-09-19. The old pair read "search your library for a
                // card" and a list of five card TYPES, and the game names what it
                // fetches in every other way there is: a SUBTYPE (Equipment, Demon,
                // Vehicle, Dragon), a COLOUR ("a black card, a green card, and a blue
                // card"), a COUNT ("three cards", "five cards"), a NEGATION ("a
                // nonland permanent card"), a second ZONE ("your library and/or
                // graveyard"), and every one of those behind an optional "up to two".
                // Twenty-one of the 48 hand-tagged cards went untouched.
                //
                // Still deliberately NOT a land search, which is Ramp or ManaFixing —
                // that reading costs Elemental Teachings, whose four land cards end up
                // split between a graveyard and the battlefield by an opponent, and it
                // is one card against the family.
                (CardEffect.Tutor, TutorPatterns),

                (CardEffect.ManaFixing, [Rx(@"add one mana of any color"), Rx(@"mana of any (one )?color"),
                    Rx(@"add \{[wubrg]\} or \{[wubrg]\}"), Rx(@"add \{[wubrg]\}, \{[wubrg]\}"),
                    // Typecycling fetches the colour you are short of, which is the
                    // whole job. Added 2026-09-15; worth 11 on its own.
                    TypeCycling,
                    // A land fetched to HAND fixes the colour you are short of and
                    // ramps nothing — the land still has to be played, off your one
                    // land drop. The same search that puts it onto the BATTLEFIELD is
                    // Ramp instead, and deliberately stays out: measured 2026-09-18
                    // over the 5,035 reviewed cards, to-hand was tagged 28 of 30
                    // (93.3%) and to-battlefield 8 of 51 (15.7%). Worth 17 on its own.
                    LandSearchToHand, LandSearchToTop,
                    // A colour filter: you feed it mana and it hands back a colour
                    // you did not have. Ruled 2026-09-18, the mirror of the Ramp
                    // ruling — the same ability that adds no mana converts colour,
                    // and that IS the job. Two shapes, and the difference matters:
                    //
                    //   GENERIC in, colour out  — Farrelite Priest "{1}: Add {W}",
                    //   Sea Scryer, Coal Golem. Always a filter.
                    //
                    //   COLOUR in, a DIFFERENT colour out — Fire Sprites "{G}, {T}:
                    //   Add {R}", Agent of Stromgald. The backreference is the whole
                    //   point: Evendo's "{G}, {T}: Add {G} for each creature you
                    //   control" pays green for more green, which is Ramp and not a
                    //   filter, and it was the only thing the first pattern got
                    //   wrong.
                    //
                    // {C} is deliberately not an output colour — colourless fixes
                    // nothing.
                    Rx(@"^[^\n:]*\{\d+\}[^\n:]*: add [\w ]{0,20}\{[wubrg]\}", RegexOptions.Multiline),
                    Rx(@"^[^\n:]*\{([wubrg])\}[^\n:]*: add [\w ]{0,20}\{(?!\1)[wubrg]\}", RegexOptions.Multiline),
                    // A Treasure is one mana of whatever colour you were short of.
                    // It fixes however many you get; whether it also RAMPS is the
                    // separate question TreasureAlsoRamps asks. Ruled 2026-09-18.
                    MakesATreasure,
                    // A land with two mana abilities in different colours fixes even
                    // though no single line says "or" — Bleachbone Verge, and the
                    // whole modern "{T}: Add {B}. / {T}: Add {W}." cycle.
                    Rx(@"^\{t\}: add \{([wubrg])\}\.?$[\s\S]{0,120}?^\{t\}: add \{(?!\1)([wubrg])\}", RegexOptions.Multiline)]),

                // Mana ability (dork/rock) or a land-fetch to the battlefield. Lands
                // are stripped below — a land tapping for its own mana is not "ramp".
                // The land-search rule that used to sit here moved into the guarded
                // branch in Classify, where LandOntoTheBattlefield says the same
                // thing more widely AND asks the two questions this one could not:
                // whose land is it, and did you pay a land for it.
                (CardEffect.Ramp, [Rx(@"\{t\}: add ")]),

                (CardEffect.Pacify, PacifyPatterns),

                // Cheat: the permanent arrives without being cast. Ruled 2026-09-18;
                // see CardEffect.Cheat for the three edges and the numbers.
                (CardEffect.Cheat, [
                    // Out of hand. The lookahead keeps LANDS out — "put a basic
                    // Forest card from your hand onto the battlefield" is Gaea's
                    // Touch and is Ramp — and "their hand" is in because Show and
                    // Tell is the card the whole family is named after.
                    // The window is 60 and not 40 because Show and Tell lists four
                    // card types before it gets to the noun. The land lookahead is
                    // narrower on purpose: it uses [\w ], which stops at the first
                    // comma, so a LIST that happens to mention lands (Show and Tell
                    // again) still counts while "put a basic Forest card" does not.
                    Rx(@"put an? (?![\w ]{0,25}(?:land|forest|island|swamp|mountain|plains) card)"
                        + @"[\w ,]{0,60}card from (?:your|their) hand onto the battlefield"),
                    // Out of the library. Natural Order, Eldritch Evolution's cousins.
                    Rx(@"search your library for an? [\w ]{0,30}creature card"
                        + @"[\w ,']{0,40}put (?:it|that card|them) onto the battlefield"),
                    // A STANDING permission to cast free, and the discriminator is
                    // GRAMMATICAL NUMBER. A standing permission covers a class of
                    // spells, so its wording is plural — "Dragon spells … without
                    // paying THEIR MANA COSTS" (Dracogenesis), "creature spells with
                    // mana value 3 or less" (Aluren), "spells from your hand"
                    // (Omniscience). The impulse rider covers the one card this card
                    // just exiled and is always singular: "you may cast a spell from
                    // among them without paying ITS MANA COST". Reading the object
                    // phrase instead was tried first and let ten riders through,
                    // because they name the card ten different ways.
                    // The plural alone still let the MULTI-card rider through — Ugin
                    // and Arcane Bombardment exile several and say "cast them
                    // without paying their mana costs" — so the object phrase is
                    // tempered as well: anything pointing back at cards this card
                    // exiled is the rider, not a permission.
                    Rx(@"(?:you|any player|each player|players) may cast "
                        + @"(?!this card\b|this spell\b)"
                        + @"(?:(?!among them|those cards|exiled|from exile|\bthem\b)[\w ,'-]){0,60}"
                        + @"without paying their mana costs")]),
            ];

            FilterPatterns = Rules.First(rule => rule.Effect == CardEffect.Filter).Patterns;
            BurnPatterns = Rules.First(rule => rule.Effect == CardEffect.Burn).Patterns;
        }

        // Damage aimed at THAT PERMANENT'S CONTROLLER, which is how the old
        // punishers name a face: Psychic Venom, Ankh of Mishra, Dingus Egg and
        // Staff, Haunting Wind, Seizures, Orcish Mine, Artifact Possession,
        // Stinging Licid. Ten of the seventeen reviewed cards written this way
        // are tagged.
        // "IS DEALT TO that spell's controller" is the passive voice of the
        // same thing: Reverberation turns a sorcery round on the player who
        // cast it, which is a Lava Spike they wrote themselves.
        private static readonly Regex DamageToTheirController = Rx(
            @"(?:deals?|is dealt) (?:[\dX]+|that much|half [\dX]+)? ?damage[\w ,'-]{0,40}to (?:that|the) "
            + @"(?:creature|land|artifact|permanent|spell)'s controller"
            + @"|damage[\w ,'-]{0,80}is dealt to that (?:creature|land|artifact|permanent|spell)'s controller");

        // …and the other seven, which are the same words hanging off an ANSWER.
        // Ruled 2026-09-20: a card that kills the permanent and then charges its
        // controller for it is a removal spell with a bonus, not a burn spell —
        // Detonate, Icequake, Cinder Cloud, Stench of Evil all destroy first,
        // and Misthios's Fury and Wisecrack shoot the creature first. Read
        // across sentences, because the kill and the charge are rarely in the
        // same one.
        // The verb is often ELIDED on the second half — Judgment Bolt says
        // "deals 5 damage to target creature AND X damage to that creature's
        // controller" with no second "deals" — so it is optional here. The
        // opening alternative has already established that a kill came first.
        //
        // A LAND is not in the list, ruled 2026-09-20: on the land-destroyers
        // the human reads the damage as a second effect worth counting, which
        // also settles the one split this family had — Orcish Mine charges the
        // controller two for the land it just destroyed and is tagged, Icequake
        // and Stench of Evil do the same and were not.
        private static readonly Regex ChargesForAKillItJustMade = Rx(
            @"(?:destroy|exile|deals? [\dX]+ damage to target creature"
            + @"|damage equal to [\w' ]{0,25}to itself)"
            + @"[\s\S]{0,160}(?:deals? )?(?:[\dX]+|that much|half [\dX]+)? ?damage"
            + @"[\w ,'-]{0,40}to (?:that|the) (?:creature|artifact|permanent|spell)'s controller");

        // Damage a TOKEN does, printed inside the quotes the token is created
        // with. Ruled 2026-09-20 with the opposite answer to the one Tokens
        // gives: the card makes a body, and what the body then does is the
        // body's. Eight reviewed cards create the 0/1 Wizard that pings on every
        // noncreature spell and none of the eight carries Burn. Keyed on "THIS
        // TOKEN" (and the emblem, which is the same thing a planeswalker makes),
        // so an ability granted to a creature you already control keeps the tag:
        // Black Mage's Rod says "this creature" and Fire Whip burns on a line of
        // its own.
        private static readonly Regex DamageFromSomethingItMade = Rx(
            @"""[^""]{0,160}\bthis (?:token|emblem)[^""]{0,60}deals? [\dX]+ damage");

        // A creature with HASTE that is gone at the end of the turn it arrived
        // is a burn spell with legs. Ruled 2026-09-20: it attacks once and then
        // it is not there any more, so what it really did was put its power on
        // a face — Ball Lightning, Spark Elemental, Blistering Firecat,
        // Groundbreaker, Lightning Skelemental. The human extended the ruling
        // the same day to the ones that BOUNCE rather than die, which is the
        // same turn and the same attack: Viashino Sandstalker, Archwing Dragon,
        // Glitterfang and the rest of the Viashinos.
        //
        // NB the reviewed set can barely test this — Ball Lightning is tagged,
        // Viashino Sandstalker was not and is realigned by the ruling, and the
        // other thirteen cards written this way have not been reviewed yet. It
        // is recorded here as a ruling applied forward rather than a rule
        // measured against ground truth, which is unusual for this file.
        //
        // Asked of the card with QUOTED text blanked out, because Strago and
        // Relm, Skirk Alarmist and Apprentice Necromancer all hand the drawback
        // to somebody else's creature.
        private static readonly Regex GoneAtEndOfTheTurn = Rx(
            @"at the beginning of the (?:next )?end step, (?:sacrifice this creature"
            + @"|return this creature to (?:its|their) owner'?s hand)");

        private static readonly Regex Hasty = Rx(@"\bhaste\b");

        // Three shapes that look like selection and are not, all ruled
        // 2026-09-20 off the nine cards where the classifier said Filter and the
        // hand did not.
        //
        //   CLOAK AND MANIFEST DREAD turn what you picked FACE DOWN into a 2/2.
        //   Nothing is selected as a card — what the ability gave you is a body,
        //   which is why Curator Beastie and Hide in Plain Sight are Tokens
        //   alone. Named by the keyword rather than by "onto the battlefield",
        //   because a card put onto the battlefield AS ITSELF is still a choice
        //   made: Aang, Gilgamesh, Jet, United Battlefront and Web of Life and
        //   Destiny all do that and all five are tagged Filter.
        //   Both this veto and the land one refuse to fire when the SAME LINE
        //   also puts a card into your hand, because then the card really did
        //   select: Planar Genesis looks at four, takes a land if there is one
        //   and a CARD if there is not, and it is tagged Ramp and Filter both.
        private static readonly Regex LooksAtTopAndMakesBodies = Rx(
            @"look at the top [\w ,']{0,40} of your library(?![^\n]{0,200}into your hand)[^\n]{0,80}"
            + @"(?:\bcloaks?\b|onto the battlefield face down)"
            + @"|manifest dread\.? \(look at the top");

        //   A LAND out of the top few is Ramp, ruled 2026-09-20 on Famished
        //   Worldsire and Ignis Scientia — the same rail that makes a land
        //   search Ramp or ManaFixing rather than Tutor. What you looked at
        //   bought mana, not a choice.
        private static readonly Regex LooksAtTopForALand = Rx(
            @"look at the top [\w ,']{0,40} of your library(?![^\n]{0,200}into your hand)[^\n]{0,80}"
            + @"put (?:a|any number of|up to \w+|\w+) (?:basic |snow )*land cards? from among them");

        //   AND A LOOT THAT IS NOT YOURS. "Each opponent discards a card AND YOU
        //   DRAW a card" is two different players doing two different things,
        //   not one player trading. Jecht, Reluctant Guardian is Discard and
        //   CardAdvantage and nothing else. The carve-out is deliberate and
        //   matches the one CardAdvantage already makes: "TARGET player" and
        //   "EACH player" stay in, because you can point Forget at yourself and
        //   because Flux loots everybody including you — both were put to the
        //   human on 2026-09-20 and both keep the tag.
        private static readonly Regex TheirDiscardYourDraw = Rx(
            @"(?:each opponent|target opponent|an opponent|each other player|each player other than you)"
            + @"[\w ,'-]{0,30}discards?[\w ,'-]{0,40}(?:and|then) you draw");
    }
}
