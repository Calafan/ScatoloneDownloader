using System;
using System.Collections.Generic;
using System.Linq;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Converts between <see cref="CardEffect"/> flags and their string names.
    /// Parsing accepts the exact enum member name (case-insensitive) plus a set
    /// of human aliases (e.g. "vindicate", "board wipe", "direct damage"), so raw
    /// tags authored by hand or carried over from the earlier free-text list still
    /// resolve. Unknown tags are skipped, never throw. Serialization always emits
    /// canonical enum names in declared (bit) order for stable git diffs.
    /// </summary>
    internal static class EffectResolver
    {
        /// <summary>Alias (case-insensitive) to flag. Enum member names are handled
        /// separately by <see cref="Enum.TryParse"/> and need no entry here.</summary>
        private static readonly Dictionary<string, CardEffect> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["token"]                       = CardEffect.Tokens,
            ["counterspell"]                = CardEffect.Counter,
            ["remove permanent"]            = CardEffect.RemovePermanent,
            ["vindicate"]                   = CardEffect.RemovePermanent,
            ["vindicate / remove permanent"] = CardEffect.RemovePermanent,
            ["board wipe"]                  = CardEffect.Wipe,
            ["boardwipe"]                   = CardEffect.Wipe,
            ["unsummon"]                    = CardEffect.Bounce,
            ["boomerang"]                   = CardEffect.Bounce,
            ["unsummon/boomerang"]          = CardEffect.Bounce,
            ["card advantage"]              = CardEffect.CardAdvantage,
            ["draw"]                        = CardEffect.CardAdvantage,
            ["reanimation"]                 = CardEffect.Reanimate,
            ["pump"]                        = CardEffect.Buff,
            ["direct damage"]               = CardEffect.Burn,
            ["directdamage"]                = CardEffect.Burn,
            ["sac"]                         = CardEffect.Sacrifice,
        };

        /// <summary>All real effect flags in declared (ascending bit) order, cached
        /// once. <see cref="ToNames"/> runs per-entry on every tagger save and
        /// per-card during view generation, so it must not re-allocate the values
        /// array (or box operands via <see cref="Enum.HasFlag"/>) each call.</summary>
        private static readonly CardEffect[] AllFlags =
            Enum.GetValues<CardEffect>().Where(f => f != CardEffect.None).ToArray();

        /// <summary>OR-combines a set of tag strings into a single flags value,
        /// silently skipping anything unrecognized.</summary>
        internal static CardEffect Parse(IEnumerable<string> names)
        {
            CardEffect result = CardEffect.None;
            if (names == null)
            {
                return result;
            }

            foreach (string raw in names)
            {
                if (TryParseSingle(raw, out CardEffect effect))
                {
                    result |= effect;
                }
            }

            return result;
        }

        /// <summary>Resolves one tag string to a single flag. Tries the exact enum
        /// member name first, then the alias table. Returns false for null/blank/
        /// unknown input.</summary>
        internal static bool TryParseSingle(string raw, out CardEffect effect)
        {
            effect = CardEffect.None;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string key = raw.Trim();

            // Reject pure numbers so "3" is not silently read as a bit combination.
            if (!int.TryParse(key, out _)
                && Enum.TryParse(key, ignoreCase: true, out CardEffect parsed)
                && parsed != CardEffect.None)
            {
                effect = parsed;
                return true;
            }

            if (Aliases.TryGetValue(key, out CardEffect aliased))
            {
                effect = aliased;
                return true;
            }

            return false;
        }

        /// <summary>Expands a flags value to its canonical member names in declared
        /// (ascending bit) order. Returns an empty list for <see cref="CardEffect.None"/>.</summary>
        internal static List<string> ToNames(CardEffect effects)
        {
            List<string> names = [];

            foreach (CardEffect flag in AllFlags)
            {
                // Non-boxing flag test (HasFlag boxes both operands).
                if ((effects & flag) == flag)
                {
                    names.Add(flag.ToString());
                }
            }

            return names;
        }
    }
}
