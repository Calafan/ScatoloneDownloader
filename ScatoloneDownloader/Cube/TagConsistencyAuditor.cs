using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// Finds cards that say the SAME THING and were tagged differently.
    /// <para>
    /// Tagging 30k cards by hand is repetitive, and repetitive work produces
    /// slips: Red Mana Battery went untagged while its four siblings are Ramp,
    /// Prodigal Sorcerer lost the Removal its two identical twins kept. Neither
    /// is a taxonomy problem — the rule was clear, the hand slipped — so neither
    /// can be found by arguing about definitions. They can be found by asking a
    /// much narrower question: did two cards with the same rules text get the
    /// same tags?
    /// </para>
    /// <para>
    /// Two cards collapse to the same <see cref="Signature"/> when their oracle
    /// text matches after the things that do not change what a card DOES are
    /// stripped: reminder text, the card's own name, mana symbols, and the size
    /// of every number. Three things are deliberately KEPT, each because dropping
    /// it reported correct cards as contradictions: the sign, because "+3/+3" and
    /// "-3/-3" are opposite cards; the activation cost, because "Sacrifice a
    /// creature:" and "{T}:" in front of the same effect are different cards; and
    /// zero, because "-2/-0" only shrinks power while "-1/-1" can kill.
    /// </para>
    /// <para>
    /// A group that disagrees is not automatically a mistake. It is either a slip
    /// (one card out of many) or a boundary the user has not settled (a family
    /// split down the middle) — the shape of the group says which, which is why
    /// this reports and never edits.
    /// </para>
    /// </summary>
    internal static class TagConsistencyAuditor
    {
        private static Regex Rx(string pattern) =>
            new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ReminderText = Rx(@"\([^)]*\)");
        private static readonly Regex ManaSymbols = Rx(@"\{[^}]*\}");
        private static readonly Regex Digits = Rx(@"\b\d+\b|\bX\b");

        private static readonly Regex NumberWords =
            Rx(@"\b(?:one|two|three|four|five|six|seven|eight|nine|ten)\b");
        private static readonly Regex Whitespace = Rx(@"\s+");

        /// <summary>Zero is not a size, it is a kind. "-2/-0" only shrinks power
        /// while "-1/-1" can kill, and the user tags exactly that difference
        /// (Grandmother Sengir is Removal, Pradesh Gypsies is not) — so erasing
        /// every number alike reported three correct cards as a contradiction.
        /// Keeping zero and flattening everything else still groups Giant Growth
        /// with Monstrous Growth, which is the grouping that earns its keep.</summary>
        private static string Flatten(Match number) => number.Value == "0" ? "0" : "#";

        /// <summary>Below this a signature is too generic to mean anything — a
        /// bare keyword line would group every vanilla flier together.</summary>
        private const int MinimumSignatureLength = 25;

        /// <summary>One set of cards whose rules text reduces to the same
        /// signature, where the human did not give them all the same tags.</summary>
        internal sealed record Disagreement(string Signature, IReadOnlyList<AuditedCard> Cards)
        {
            /// <summary>The tag set most of the group shares, or <c>null</c> when
            /// the group splits evenly. A lone card differing from a clear
            /// majority is the shape a slip takes; an even split is a boundary.</summary>
            internal CardEffect? Majority
            {
                get
                {
                    var ranked = Cards.GroupBy(c => c.Effects)
                        .Select(g => (Tags: g.Key, Count: g.Count()))
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    return ranked.Count > 1 && ranked[0].Count > ranked[1].Count ? ranked[0].Tags : null;
                }
            }
        }

        internal sealed record AuditedCard(string OracleId, string Name, string OracleText, CardEffect Effects);

        /// <summary>Groups the supplied cards by signature and returns only the
        /// groups that were tagged more than one way, largest first.</summary>
        internal static IReadOnlyList<Disagreement> Audit(IEnumerable<AuditedCard> cards)
        {
            Dictionary<string, List<AuditedCard>> bySignature = [];

            foreach (AuditedCard card in cards)
            {
                string signature = Signature(card.Name, card.OracleText);
                if (signature.Length < MinimumSignatureLength)
                {
                    continue;
                }

                if (!bySignature.TryGetValue(signature, out List<AuditedCard>? group))
                {
                    bySignature[signature] = group = [];
                }

                group.Add(card);
            }

            List<Disagreement> disagreements = [];
            foreach ((string signature, List<AuditedCard> group) in bySignature)
            {
                if (group.Count > 1 && group.Select(c => c.Effects).Distinct().Count() > 1)
                {
                    disagreements.Add(new Disagreement(signature, group.OrderBy(c => c.Name, StringComparer.Ordinal).ToList()));
                }
            }

            return [.. disagreements.OrderByDescending(d => d.Cards.Count).ThenBy(d => d.Cards[0].Name, StringComparer.Ordinal)];
        }

        /// <summary>What a card DOES, with everything that only changes how it is
        /// written removed. See the class doc for what is kept and why.</summary>
        internal static string Signature(string? name, string? oracleText)
        {
            string text = oracleText ?? string.Empty;
            text = ReminderText.Replace(text, " ");

            // A card refers to itself by name, and the name is the one part of the
            // text that is guaranteed to differ between two otherwise identical
            // cards. Fragments of three letters or fewer are left alone so a name
            // like "The" or "of" cannot eat ordinary words — the cost is that a
            // short word IN a name survives too ("Red Mana Battery" keeps "Red"),
            // which can split a group that should have matched. Missing a group
            // is the safe direction: this tool only ever suggests things to look
            // at, so a false negative is silence and a false positive is a chore.
            foreach (string part in (name ?? string.Empty)
                .Split([' ', ',', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length > 3)
                {
                    text = text.Replace(part, " ", StringComparison.OrdinalIgnoreCase);
                }
            }

            text = ManaSymbols.Replace(text, " ");
            text = Digits.Replace(text, Flatten);
            text = NumberWords.Replace(text, "#");

            return Whitespace.Replace(text, " ").Trim().ToLowerInvariant();
        }
    }
}
