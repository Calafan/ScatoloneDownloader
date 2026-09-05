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

            (CardEffect.Buff, [Rx(@"gets? \+\d+/\+\d+"), Rx(@"creatures you control get \+"), Rx(@"\+\d+/\+\d+ until end of turn")]),

            // NB: no bare "regenerate" — "can't be regenerated" (Wrath) would false-positive.
            (CardEffect.Protection, [Rx(@"hexproof|indestructible|shroud|protection from"),
                Rx(@"can'?t be (countered|the target)"), Rx(@"\bward\b")]),

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

            return result;
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
