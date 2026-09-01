using System.Globalization;
using System.Text;

namespace ScatoloneDownloader.Cli
{
    /// <summary>
    /// Collapses a card name to a punctuation-free lookup key, so a PNG filename
    /// can be matched against Scryfall bulk data even though a Windows filename
    /// cannot carry the characters the real name uses: the colon of
    /// <c>Summon: Choco/Mog</c>, the slash of <c>SP//dr, Piloted by Peni</c>, the
    /// quotes of <c>Henzie "Toolbox" Torre</c>, and the <c>//</c> of a
    /// double-faced card (written <c>_</c> in the filename).
    /// <para>
    /// Both sides of the comparison are collapsed, so a character the key drops is
    /// dropped identically in the filename and in the Scryfall name — only the
    /// characters the filesystem destroys actually differ, and those are exactly
    /// the ones this removes. This replaces the previous approach of rebuilding
    /// the exact Scryfall name from the filename with prefix and regex rules plus
    /// a hardcoded exception list, none of which could recover a stripped quote or
    /// slash.
    /// </para>
    /// </summary>
    internal static class CardNameKey
    {
        /// <summary>
        /// Returns <paramref name="name"/> lowercased, stripped of diacritics, and
        /// reduced to its ASCII letters and digits — every space, punctuation mark
        /// and separator is dropped. Measured over Scryfall's 38k oracle names,
        /// only 13 keys are shared by more than one card (mostly tokens and
        /// silver-border jokes, e.g. <c>Waste Land</c> / <c>Wasteland</c>), so
        /// callers must treat a shared key as ambiguous rather than a match.
        /// </summary>
        public static string Collapse(string name)
        {
            string decomposed = name.Normalize(NormalizationForm.FormD);
            StringBuilder key = new(decomposed.Length);

            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                char lowered = char.ToLowerInvariant(character);
                if (lowered is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
                {
                    key.Append(lowered);
                }
            }

            return key.ToString();
        }
    }
}
