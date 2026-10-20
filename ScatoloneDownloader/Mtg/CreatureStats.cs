using System.Globalization;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// The creature facts the creature profile reads from a <see cref="Card"/>:
    /// numeric power/toughness and the evasion/haste/defender definitions of
    /// <c>docs/cube-targets.md</c> ("Definizioni"), so the profile's numbers stay
    /// comparable with the targets.
    /// </summary>
    internal static class CreatureStats
    {
        private static readonly string[] EvasionKeywords =
            ["Flying", "Menace", "Trample", "Shadow", "Horsemanship", "Fear", "Intimidate", "Skulk"];

        /// <summary>The integer of a power/toughness string: an optional leading minus
        /// and digits only. Variable values ("*", "1+*", "X"), empty, non-integers
        /// ("1.5") and a plus sign (an augment's "+1" is a modifier, not a stat) give
        /// null; the digit check is what refuses the plus, which TryParse accepts.</summary>
        internal static int? ParseStat(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            for (int i = value[0] == '-' ? 1 : 0; i < value.Length; i++)
            {
                if (value[i] is < '0' or > '9')
                {
                    return null;
                }
            }

            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : null;
        }

        /// <summary>One of the evasion keywords, or "can't be blocked" in the rules text
        /// (ordinal and case-sensitive, so "can't block" does not match).</summary>
        internal static bool IsEvasive(Card card)
        {
            return card.Keywords.Any(k => EvasionKeywords.Contains(k, StringComparer.Ordinal))
                || card.OracleText.Contains("can't be blocked", StringComparison.Ordinal);
        }

        internal static bool IsHaste(Card card)
        {
            return card.Keywords.Contains("Haste", StringComparer.Ordinal);
        }

        internal static bool IsDefender(Card card)
        {
            return card.Keywords.Contains("Defender", StringComparer.Ordinal);
        }
    }
}
