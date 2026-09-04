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

        // One entry per effect; a card gets the effect if ANY of its patterns hit.
        private static readonly (CardEffect Effect, Regex[] Patterns)[] Rules =
        [
            (CardEffect.Tokens, [Rx(@"create[s]?\b.*\btoken"), Rx(@"put[s]?\b.*\btoken.*onto the battlefield")]),

            // Mass removal first (a wiper also reads as removal; both are fine).
            (CardEffect.Wipe, [Rx(@"destroy all (creatures|permanents|nonland)"), Rx(@"exile all creatures"),
                Rx(@"each player sacrifices"), Rx(@"all creatures get -\d+/-\d+")]),

            (CardEffect.RemovePermanent, [Rx(@"destroy target permanent"), Rx(@"exile target permanent")]),

            (CardEffect.Removal, [Rx(@"destroy target[\w ]*creature"), Rx(@"exile target[\w ]*creature"),
                Rx(@"destroy target[\w ]*(creature|planeswalker)")]),

            (CardEffect.Counter, [Rx(@"counter target[\w ]*spell")]),

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

            return result;
        }
    }
}
