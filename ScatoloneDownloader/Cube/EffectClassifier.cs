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

        // "Mill" only became keyword wording in 2021; everything printed before
        // that spells the action out, so both forms are needed or the whole
        // pre-2021 library goes untagged. Hoisted out of the table below because
        // the cost guard re-runs them against a stripped copy of the text — and
        // declared BEFORE Rules, which captures the array in its own initializer.
        private static readonly Regex[] MillPatterns =
        [
            Rx(@"\bmills? (?:[\w-]+ ){0,2}cards?\b"),
            Rx(@"put(?:s)? the top [\w ]{0,25}library into [\w ]{0,25}graveyard"),
        ];

        // The mill you PAY, in the three shapes it is written in: a drawback you
        // meet ("sacrifice this creature unless you mill two cards"), an extra
        // cost on the way to the stack, and a cost before an activated ability's
        // colon ("{T}, Mill a card: Add {C}"). See the guard below for why.
        private static readonly Regex[] MillCosts =
        [
            Rx(@"unless (?:you|they|that player|its controller) mills? (?:[\w-]+ ){0,2}cards?"),
            Rx(@"as an additional cost to cast[^.\n]{0,80}mills? (?:[\w-]+ ){0,2}cards?"),
            Rx(@"\bmills? (?:[\w-]+ ){0,2}cards?[^:.\n]{0,30}:", RegexOptions.Multiline),
        ];

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
            (CardEffect.Tokens, [Rx(@"create[s]?\b.*\btoken"), Rx(@"put[s]?\b.*\btoken.*onto the battlefield")]),

            // Mass removal first (a wiper also reads as removal; both are fine).
            (CardEffect.Wipe, [Rx(@"destroy all (creatures|permanents|nonland)"), Rx(@"exile all creatures"),
                Rx(@"each player sacrifices"), Rx(@"all creatures get -\d+/-\d+")]),

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

            (CardEffect.Removal, [Rx(@"destroy target[\w ]*creature"), Rx(@"exile target[\w ]*creature"),
                Rx(@"destroy target[\w ]*(creature|planeswalker)")]),

            (CardEffect.Counter, [Rx(@"counter target[\w ]*spell")]),

            // The other way to answer something on the stack. "copy target" is
            // required rather than the bare word "copy": a token that enters "as a
            // copy OF target creature" is Tokens, not stack interaction.
            (CardEffect.Redirect, [Rx(@"change the targets? of"),
                Rx(@"cop(?:y|ies) target[\w ]*(?:spell|ability)")]),

            (CardEffect.Bounce, [Rx(@"return target[\w ,]*to (its|their) owner'?s? hand"),
                Rx(@"return[\w ,]*to (its|their) owner'?s? hand")]),

            (CardEffect.Disenchant, [Rx(@"destroy target[\w ]*(artifact|enchantment)"),
                Rx(@"exile target[\w ]*(artifact|enchantment)")]),

            (CardEffect.Discard, [Rx(@"(target (player|opponent)|each player|that player) discards"),
                Rx(@"discards? (a card|\d+ cards|two cards|three cards|their hand)")]),

            (CardEffect.CardAdvantage, [Rx(@"draw (two|three|four|\d+) cards"), Rx(@"draw a card")]),

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

            (CardEffect.Burn, [Rx(@"deals? \d+ damage to (any target|target creature|target player|target planeswalker|each|any|it|that)")]),

            (CardEffect.Sacrifice, [Rx(@"target player sacrifices"),
                Rx(@"sacrifice (a|another|two|three|\d+)[\w ]*(creature|permanent|artifact|land)")]),

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

            (CardEffect.Pacify, [Rx(@"does(n'?t| not) untap"), Rx(@"can'?t attack or block"), Rx(@"can'?t attack(\.|,| unless)"),
                Rx(@"tap target[\w ,]*creature"), Rx(@"detain")]),
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

            // Milling can be the effect or the PRICE, and only the first is what
            // the tag is for. Deep Spawn's "sacrifice this creature unless you
            // mill two cards" is an upkeep tax; Millikin's "{T}, Mill a card:"
            // buys mana. Neither card is doing anything to a library on purpose
            // — the same distinction LandDestruction already draws between Stone
            // Rain and Serendib Djinn's land sacrifice.
            if (result.HasFlag(CardEffect.Mill) && !MillsOutsideACost(card))
            {
                result &= ~CardEffect.Mill;
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

            return result;
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

        /// <summary>Whether anything is still milled once every cost clause is
        /// struck out — i.e. whether the card mills as an EFFECT rather than only
        /// as a price it pays. A card that does both keeps the tag, because the
        /// surviving text still matches.</summary>
        private static bool MillsOutsideACost(Card card)
        {
            string text = card.OracleText ?? string.Empty;

            foreach (Regex cost in MillCosts)
            {
                text = cost.Replace(text, " ");
            }

            foreach (Regex pattern in MillPatterns)
            {
                if (pattern.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
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
