namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Converts between <see cref="CardEffect"/> flags and their string names.
    /// Parsing accepts the exact enum member name (case-insensitive), which is
    /// what the tagger sends and the tier files hold. Unknown tags are skipped,
    /// never throw. Serialization always emits canonical enum names in declared
    /// (bit) order for stable git diffs.
    /// </summary>
    internal static class EffectResolver
    {
        /// <summary>All real effect flags in declared (ascending bit) order, cached
        /// once. <see cref="ToNames"/> runs per-entry on every tagger save, per
        /// card in the tagger's card list and per effect group in the stats payload, so it must not re-allocate the values
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

        /// <summary>Resolves one tag string to a single flag by its exact enum member
        /// name (case-insensitive). Returns false for null/blank/numeric/unknown
        /// input.</summary>
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
