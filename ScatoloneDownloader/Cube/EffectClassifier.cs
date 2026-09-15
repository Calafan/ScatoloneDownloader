using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// First-pass, rule-based auto-classifier: reads a card's rules text (plus its
    /// keyword abilities) and PROPOSES a set of <see cref="CardEffect"/> flags. It
    /// is deliberately heuristic and precision-leaning — a card can match several
    /// rules (the flags are OR-combined), and a miss is fine because a human
    /// confirms every suggestion in the tagger (the classifier never stamps
    /// <c>reviewedAt</c>). The rule table is a plain list of
    /// <c>(effect, regex...)</c> so it is easy to extend as gaps surface; each
    /// pattern is matched case-insensitively against the lower-cased oracle text.
    /// Not an oracle of truth — a starting point that turns "tag 30k from scratch"
    /// into "review suggestions".
    /// </summary>
    internal static class EffectClassifier
    {
        private static Regex Rx(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static Regex Rx(string pattern, RegexOptions extra) =>
            new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | extra);

        // Mill has to name SOMEBODY ELSE'S library. A bare "mill three cards" is
        // you filling your own graveyard, which is fuel for whatever the card
        // does next (Ooze Patrol's counters, Fell Gravship's recursion) rather
        // than an attack on anybody — the reading settled on 2026-09-15, and the
        // one that makes the tag mean "this is how the deck mills somebody out".
        // Measured against 5035 reviewed cards it is decisive: the old bare-verb
        // rule fired on 79 cards and was wrong on 51 of them; this one fires on
        // 28 and is wrong on none.
        //
        // The run between the player and the verb is what admits "an opponent
        // WOULD mill", "each player MAY mill" and "any number of target players
        // EACH mill". "Mill" only became keyword wording in 2021, so the
        // spelled-out pre-2021 form needs its own pattern or the whole old
        // library goes untagged.
        //
        // Naming the victim also SUBSUMES the old cost guard, which existed to
        // drop Deep Spawn's "unless you mill two cards" and Millikin's
        // "{T}, Mill a card:". Both are things a card does to itself, so neither
        // matches any more and the guard is gone.
        private const string OtherPlayer =
            @"(?:target player|target opponent|each opponent|an opponent|that player|each player"
            + @"|defending player|its controller|opponents?)";

        private static readonly Regex[] MillPatterns =
        [
            Rx(OtherPlayer + @"[\w ,]{0,40}mills?\b"),
            Rx(@"put(?:s)? the top [\w ]{0,25}" + OtherPlayer + @"[\w' ]{0,20}library into"),
            // Same sentence with the victim in front of the verb, which is how
            // Millstone and most pre-2021 mill is actually written.
            Rx(OtherPlayer + @"[\w ,]{0,30}puts? the top [\w ]{0,30}library into"),
        ];

        // The token has to be a CREATURE, and it has to be yours. A Treasure, a
        // Clue, a Food or a Lander is a resource the card hands you on the way
        // past — the tag is for the cards that put bodies on the board, which is
        // what a deck builds around. "Or a copy" covers the token that never
        // says "creature token" because it is a copy of one.
        private static readonly Regex[] TokenPatterns =
        [
            Rx(@"create[s]?\b.*\btoken"),
            Rx(@"put[s]?\b.*\btoken.*onto the battlefield"),
        ];

        private static readonly Regex CreatureTokenWording = Rx(@"creature token|token that's a copy|token copy");

        // Diabolic Edict and Flare of Malice make the OTHER player sacrifice,
        // which empties their board rather than giving you a place to put yours.
        private static readonly Regex SomebodyElseSacrifices = Rx(OtherPlayer + @"[\w ,]{0,40}sacrifices?\b");

        // An outlet has to be usable at will: a cost in front of a colon
        // ("Sacrifice a creature: ..."), or an optional sacrifice a trigger
        // offers you. "As an additional cost to cast this spell" is neither — it
        // is a price paid once, on the way to a different effect.
        private static readonly Regex SacrificeOutlet = Rx(
            @"^[^\n:]{0,60}sacrifices? (?:a|an|another|two|three|\d+)[\w ]*creature[^\n:]{0,40}:"
            + @"|you may sacrifice (?:a|an|another|two|three|\d+)[\w ]*creature",
            RegexOptions.Multiline);

        // "Its controller creates a 1/1 white Spirit" (Afterlife), "target
        // opponent creates a 1/1 green Hippo" (Phelddagrif): the body is real,
        // but it is not on your side of the table.
        private static readonly Regex SomebodyElseCreates = Rx(
            @"(?:its controller|that player|each player|each opponent|target opponent|an opponent"
            + @"|the exiled card's owner|they|opponents?)\s+(?:may have you |each )?creates?\b"
            + @"|have an opponent create|you and target opponent each create");

        // Hoisted for the same reason as MillPatterns: the beneficiary guard
        // re-walks their matches to see WHO the effect lands on.
        private static readonly Regex[] BuffPatterns =
        [
            Rx(@"gets? \+\d+/\+\d+"),
            Rx(@"creatures you control get \+"),
            Rx(@"\+\d+/\+\d+ until end of turn"),
        ];

        // NB: no bare "regenerate" — "can't be regenerated" (Wrath) would false-positive.
        private static readonly Regex[] ProtectionPatterns =
        [
            Rx(@"hexproof|indestructible|shroud|protection from"),
            Rx(@"can'?t be (countered|the target)"),
            Rx(@"\bward\b"),
        ];

        // Pacify split in two so the guard below can tell the shapes apart. An
        // untap lock is the one Pacify wording a card routinely aims at ITSELF —
        // Basalt Monolith, Mana Vault and Time Vault all read "this artifact
        // doesn't untap during your untap step", which is a price they pay, not a
        // lock on anybody. Everything in PacifyOutward names a victim by
        // construction and needs no such check.
        private static readonly Regex AnyUntapLock = Rx(@"does(n'?t| not) untap");

        private static readonly Regex[] PacifyOutward =
        [
            Rx(@"can'?t attack or block"),
            Rx(@"can'?t attack(\.|,| unless)"),
            Rx(@"tap target[\w ,]*creature"),
            Rx(@"detain"),
        ];

        private static readonly Regex[] PacifyPatterns = [AnyUntapLock, .. PacifyOutward];

        // The untap lock written about the card itself. "Target creature doesn't
        // untap during its controller's next untap step" (Frozen Solid) does NOT
        // match, so a real lock keeps the tag.
        private static readonly Regex SelfUntapClause = Rx(
            @"th(?:is|e) (?:artifact|creature|permanent|enchantment|land|vehicle)[\w ]{0,25}does(?:n'?t| not) untap");

        // The subjects that make an effect land on something OTHER than the card
        // writing it. Looked for in the text preceding a Buff/Protection match on
        // the same line — see GrantedToSomethingElse.
        //
        // "<noun> you control" has to stay open-ended: the beneficiary is any
        // type line the card cares to name (Wizards, Merfolk, creature tokens,
        // permanents). The stop-word list is what keeps that from swallowing the
        // subordinate clause in "As long as you control an artifact, this
        // creature gets +2/+0", where the card is still only pumping itself.
        // The card talking about itself. Modern oracle text says "this creature";
        // older printings repeat the card's name, which is handled separately
        // because it is per-card data rather than a pattern.
        private static readonly Regex SelfReference = Rx(
            @"\bthis (?:creature|permanent|card|artifact|enchantment|land|token|spell|planeswalker|vehicle|equipment)\b");

        // "X gains hexproof", "creatures with power 2 or less have shroud": a
        // grant verb means SOMETHING is being given the ability, which is enough
        // to keep the tag even when the subject is not vocabulary we recognise.
        private static readonly Regex GrantVerb = Rx(@"\b(?:gains?|have|has|becomes?)\b");

        private static readonly Regex Beneficiary = Rx(
            @"\b(?:target|another|other|each|all|enchanted|equipped|chosen|"
            // "that creature" is the beneficiary a second sentence refers back to:
            // "Gain control of target creature ... that creature gets +2/+0".
            + @"that (?:creature|permanent|player|token|card)|"
            // "attacking" has to MODIFY the beneficiary ("attacking red creatures
            // get +2/+0") — bare, it is just as often the card's own state, as in
            // "As long as this creature is attacking, it gets +2/+0".
            + @"(?:attacking|blocking) (?:[\w-]+ ){0,3}(?:creatures?|permanents?|tokens?)|"
            + @"(?!(?:as|if|unless|while|when|whenever|though|although|because|that|and|or|but|long)\b)\w+ you control)\b");

        // One entry per effect; a card gets the effect if ANY of its patterns hit.
        private static readonly (CardEffect Effect, Regex[] Patterns)[] Rules =
        [
            (CardEffect.Tokens, TokenPatterns),

            // Mass removal first (a wiper also reads as removal; both are fine).
            // Mass DAMAGE is the oldest wiper wording there is and was missing
            // entirely: Earthquake, Crypt Rats and Fire Magic sweep a board
            // without the word "destroy" appearing anywhere. [\dX] because the
            // interesting ones scale — "deals X damage to each creature".
            (CardEffect.Wipe, [Rx(@"destroy all (creatures|permanents|nonland)"), Rx(@"exile all creatures"),
                Rx(@"each player sacrifices"), Rx(@"all creatures get -\d+/-\d+"),
                Rx(@"deals? [\dX]+ damage to each creature")]),

            (CardEffect.RemovePermanent, [Rx(@"destroy target permanent"), Rx(@"exile target permanent")]),

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
            (CardEffect.LandDestruction, [
                Rx(@"destroy (?:[\w-]+ ){0,2}target (?:[\w-]+ ){0,2}lands?\b"),
                Rx(@"exile (?:[\w-]+ ){0,2}target (?:[\w-]+ ){0,2}lands?\b"),
                Rx(@"destroy all \blands?\b"),
                Rx(@"(?:target (?:player|opponent)|each player|that player) sacrifices? (?:[\w-]+ ){0,3}lands?\b")]),

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
            (CardEffect.Removal, [Rx(@"destroy target[\w ]*creature"), Rx(@"exile target[\w ]*creature"),
                Rx(@"destroy target[\w ]*(creature|planeswalker)"),
                Rx(@"deals? [\dX]+ damage to any target"),
                Rx(@"deals? [\dX]+ damage to [\w ]{0,20}target creature"),
                Rx(@"\bfights?\b"),
                Rx(@"target player sacrifices a creature")]),

            (CardEffect.Counter, [Rx(@"counter target[\w ]*spell")]),

            // The other way to answer something on the stack. "copy target" is
            // required rather than the bare word "copy": a token that enters "as a
            // copy OF target creature" is Tokens, not stack interaction.
            (CardEffect.Redirect, [Rx(@"change the targets? of"),
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

            (CardEffect.Disenchant, [Rx(@"destroy target[\w ]*(artifact|enchantment)"),
                Rx(@"exile target[\w ]*(artifact|enchantment)")]),

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
                Rx(@"^(?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card", RegexOptions.Multiline)]),

            (CardEffect.Filter, [Rx(@"scry \d"), Rx(@"surveil \d"),
                Rx(@"look at the top \w+ cards? of your library"),
                Rx(@"discard[\w ]* then draw"), Rx(@"draw \w+ cards?, then discard")]),

            (CardEffect.Reanimate, [Rx(@"return target[\w ]*creature card from[\w ]*graveyard to the battlefield"),
                Rx(@"return[\w ]*from (your|a) graveyard to the battlefield"),
                Rx(@"put[\w ]*creature card from[\w ]*graveyard onto the battlefield")]),

            // The sibling of Reanimate: same origin, different destination. The
            // [\w ] runs cannot cross a full stop, so a card that exiles from a
            // graveyard in one sentence and bounces a creature in the next does
            // not accidentally read as recursion.
            (CardEffect.Regrowth, [Rx(@"return[\w ]*from[\w ]*graveyard to[\w ']*hand"),
                Rx(@"put[\w ]*card from[\w ]*graveyard into[\w ]*hand")]),

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
                Rx(@"deals? [\dX]+ damage to (?:any target|target player|target opponent|each player|each opponent|that player)\b"),
                Rx(@"deals? [\dX]+ damage to [\w ,]{0,45}each (?:player|opponent)")]),

            // Sacrifice is a sacrifice OUTLET: somewhere to put your OWN creatures
            // on demand, which is what makes a stolen creature (see Steal) worth
            // taking. Ruled 2026-09-15. Three things fall out of that and are
            // handled by the guard below: an edict is somebody else sacrificing,
            // a land or an artifact is not a creature, and an additional cost to
            // cast is a one-shot price rather than an outlet you can point at
            // anything. Precision 56.5% -> 95.4%.
            (CardEffect.Sacrifice, [Rx(@"sacrifices? (?:a|an|another|two|three|\d+)[\w ]*creature")]),

            (CardEffect.Steal, [Rx(@"gains? control of"), Rx(@"you control (enchanted|target)"),
                Rx(@"untap target creature[\w ]*gain control")]),

            // "a card" (Demonic) or a typed non-land card (creature/instant/...);
            // deliberately NOT land searches, which are Ramp/ManaFixing, not Tutor.
            (CardEffect.Tutor, [Rx(@"search your library for an? card"),
                Rx(@"search your library for[\w ]*(creature|instant|sorcery|artifact|enchantment|planeswalker) card")]),

            (CardEffect.ManaFixing, [Rx(@"add one mana of any color"), Rx(@"mana of any (one )?color"),
                Rx(@"add \{[wubrg]\} or \{[wubrg]\}"), Rx(@"add \{[wubrg]\}, \{[wubrg]\}")]),

            // Mana ability (dork/rock) or a land-fetch to the battlefield. Lands
            // are stripped below — a land tapping for its own mana is not "ramp".
            (CardEffect.Ramp, [Rx(@"\{t\}: add "),
                Rx(@"search your library for[\w ]*(land|forest|plains|island|swamp|mountain)[\w ,]*put[\w ]*onto the battlefield")]),

            (CardEffect.Pacify, PacifyPatterns),
        ];

        /// <summary>Keyword abilities that map directly to an effect regardless of
        /// oracle wording (e.g. an "Indestructible" creature with no rules text).</summary>
        private static readonly Dictionary<string, CardEffect> KeywordEffects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hexproof"] = CardEffect.Protection,
            ["shroud"] = CardEffect.Protection,
            ["indestructible"] = CardEffect.Protection,
            ["ward"] = CardEffect.Protection,
            ["protection"] = CardEffect.Protection,
            ["detain"] = CardEffect.Pacify,
        };

        /// <summary>Proposes the effect flags for a card from its
        /// <see cref="Card.OracleText"/> and <see cref="Card.Keywords"/>. Returns
        /// <see cref="CardEffect.None"/> when nothing matches (leave it untagged).</summary>
        internal static CardEffect Classify(Card card)
        {
            CardEffect result = CardEffect.None;

            string text = card.OracleText ?? string.Empty;
            if (text.Length > 0)
            {
                foreach ((CardEffect effect, Regex[] patterns) in Rules)
                {
                    foreach (Regex pattern in patterns)
                    {
                        if (pattern.IsMatch(text))
                        {
                            result |= effect;
                            break;
                        }
                    }
                }
            }

            if (card.Keywords != null)
            {
                foreach (string keyword in card.Keywords)
                {
                    if (keyword != null && KeywordEffects.TryGetValue(keyword, out CardEffect kwEffect))
                    {
                        result |= kwEffect;
                    }
                }
            }

            // A land tapping for its own mana is not Ramp (it is just a land —
            // structural, tracked by MacroType). Its ManaFixing, if any, still stands.
            if (card.MacroType == MacroType.Land)
            {
                result &= ~CardEffect.Ramp;
            }

            // Protection is an INTERACTION you hold up, not a property a card
            // happens to have. A creature printed with hexproof protects only
            // itself, passively, and answers nothing; Mother of Runes protects
            // whatever needs it, in response, on the turn it matters. Only the
            // second is what the tag is for, so the vocabulary above has to clear
            // a timing gate before it counts.
            if (result.HasFlag(CardEffect.Protection) && !IsInstantSpeed(card))
            {
                result &= ~CardEffect.Protection;
            }

            // Tokens is the third reading of the same question the Mill rule
            // asks — what does this card do, and for whom. A Clue or a Treasure
            // is a resource handed over in passing, and a Spirit handed to the
            // creature's OWNER (Afterlife) is a body for the other side, so
            // neither earns the tag that means "this deck goes wide". Written as
            // a guard rather than as more regexes because the evidence sits
            // anywhere on the card, not next to the verb.
            if (result.HasFlag(CardEffect.Tokens)
                && (!CreatureTokenWording.IsMatch(text) || SomebodyElseCreates.IsMatch(text)))
            {
                result &= ~CardEffect.Tokens;
            }

            // Sacrifice, asked the same way: whose creature, and can you do it
            // when you want to? A card that only lets an OPPONENT sacrifice is an
            // edict, and one that charges a creature as an additional cost to cast
            // pays once and is gone — neither is an outlet.
            if (result.HasFlag(CardEffect.Sacrifice)
                && (SomebodyElseSacrifices.IsMatch(text) || !SacrificeOutlet.IsMatch(text)))
            {
                result &= ~CardEffect.Sacrifice;
            }

            // Buff and Protection need a beneficiary that is not the card itself.
            // A creature that pumps or shields ONLY itself has a stat line, not an
            // effect: it answers nothing for the rest of the board and changes no
            // deck's plan, whereas Giant Growth and Mother of Runes are cards you
            // hold up for whatever needs them. Same reading as the Mill guard —
            // ask what the text does for someone else.
            // The two differ in what SILENCE means, which is a fact about how the
            // two are written rather than a hedge. A P/T change always names who
            // gets it, so an unrecognised subject ("Nonartifact creatures get
            // +2/+2") is somebody else and the tag stands. Protection words are
            // keyword abilities a card can simply HAVE: a line reading "Ward {2}"
            // or "Protection from red" has no subject precisely because the
            // subject is the card itself.
            if (result.HasFlag(CardEffect.Buff) && AimsOnlyAtItself(text, BuffPatterns, card, bareIsSelf: false))
            {
                result &= ~CardEffect.Buff;
            }

            if (result.HasFlag(CardEffect.Protection) && AimsOnlyAtItself(text, ProtectionPatterns, card, bareIsSelf: true))
            {
                result &= ~CardEffect.Protection;
            }

            // Pacify is the third reading of the same question, and it is asked
            // the same way: a mana rock that will not untap has taxed ITSELF, and
            // taxing yourself neutralises nobody. Kept separate from
            // AimsOnlyAtItself because the evidence runs the other way round —
            // there is no subject in front of "doesn't untap" to inspect, so the
            // test is whether the clause names the card as the thing held down.
            if (result.HasFlag(CardEffect.Pacify) && OnlyLocksItself(text))
            {
                result &= ~CardEffect.Pacify;
            }

            return result;
        }

        /// <summary>True when the only thing that read as Pacify is an untap lock
        /// the card puts on itself. Any outward-aimed Pacify wording elsewhere on
        /// the card keeps the tag, so Time Vault's self-tax is dropped while a card
        /// that both taxes itself and taps an opponent's creature is not.</summary>
        private static bool OnlyLocksItself(string text)
        {
            foreach (Regex outward in PacifyOutward)
            {
                if (outward.IsMatch(text))
                {
                    return false;
                }
            }

            return SelfUntapClause.IsMatch(text);
        }

        /// <summary>Whether every match of <paramref name="patterns"/> lands on the
        /// card itself, judged by the subject written in front of it. The window is
        /// the current LINE only (oracle text puts one ability per line), so a
        /// "target" in an unrelated ability cannot vouch for a self-buff two lines
        /// down. One match aimed elsewhere is enough to keep the tag.</summary>
        private static bool AimsOnlyAtItself(string text, Regex[] patterns, Card card, bool bareIsSelf)
        {
            string ownName = ShortName(card);
            bool sawSelf = false;

            foreach (Regex pattern in patterns)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    int lineStart = text.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
                    int from = Math.Max(lineStart, match.Index - 80);
                    string before = text.Substring(from, match.Index - from);

                    if (Beneficiary.IsMatch(before) || Beneficiary.IsMatch(AfterCounterClause(text, match)))
                    {
                        return false;
                    }

                    if (SelfReference.IsMatch(before)
                        || (ownName.Length > 0 && before.Contains(ownName, StringComparison.OrdinalIgnoreCase)))
                    {
                        sawSelf = true;
                        continue;
                    }

                    if (GrantVerb.IsMatch(before) || !bareIsSelf)
                    {
                        return false;
                    }

                    sawSelf = true;
                }
            }

            return sawSelf;
        }

        /// <summary>The one wording that names its beneficiary AFTER the ability:
        /// "put an indestructible counter on target creature". Deliberately gated
        /// on the word "counter" rather than reading ahead in general — a general
        /// look-ahead reads "gets +1/+1 for each other creature you control" as a
        /// gift to those creatures, when it is a self-buff that merely counts
        /// them. Empty for every other shape, and stops at "." and "(" so
        /// reminder text ("Ward {2} (Whenever this creature becomes the target
        /// ...)") cannot vouch for itself.</summary>
        private static string AfterCounterClause(string text, Match match)
        {
            int start = match.Index + match.Length;
            int end = Math.Min(text.Length, start + 50);

            if (!text.AsSpan(start, end - start).StartsWith(" counter", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            for (int i = start; i < end; i++)
            {
                if (text[i] is '\n' or '(' or '.')
                {
                    return text.Substring(start, i - start);
                }
            }

            return text.Substring(start, end - start);
        }

        /// <summary>The name a card uses to refer to itself in its own rules text:
        /// the front face, cut at the comma ("Multani, Yavimaya's Avatar" writes
        /// "Multani gets +1/+1").</summary>
        private static string ShortName(Card card)
        {
            string name = card.Name ?? string.Empty;

            int slash = name.IndexOf(" //", StringComparison.Ordinal);
            if (slash > 0)
            {
                name = name.Substring(0, slash);
            }

            int comma = name.IndexOf(',');
            if (comma > 0)
            {
                name = name.Substring(0, comma);
            }

            return name.Trim();
        }

        /// <summary>Whether the card's effect can be deployed in response: an
        /// instant, a card with flash, or anything with an activated ability (a
        /// cost, then a colon — Mother of Runes' <c>{T}:</c>, a Circle of
        /// Protection's <c>{1}:</c>). Sorceries, static abilities and
        /// enter-the-battlefield triggers do not qualify.</summary>
        private static bool IsInstantSpeed(Card card)
        {
            if ((card.TypeLine ?? string.Empty).Contains("Instant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (string keyword in card.Keywords ?? [])
            {
                if (string.Equals(keyword, "Flash", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            string text = card.OracleText ?? string.Empty;

            foreach (Regex pattern in ActivatedAbility)
            {
                if (pattern.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }

        // The two shapes an activated ability's cost takes before its colon. Both
        // are bounded to a single line so an unrelated symbol cannot pair with a
        // later colon.
        //
        // Loyalty abilities ("+1:", "-2:") are deliberately NOT here: a
        // planeswalker activates at sorcery speed, so it fails the gate the same
        // way a sorcery does. That is also why this cannot simply look for "any
        // short prefix then a colon".
        private static readonly Regex[] ActivatedAbility =
        [
            Rx(@"\{[^}\n]+\}[^:\n]{0,40}:"),
            Rx(@"^(?:sacrifice|discard|pay|exile|tap|untap|remove|return|reveal)\b[^:\n]{0,50}:",
                RegexOptions.Multiline),
        ];
    }
}
