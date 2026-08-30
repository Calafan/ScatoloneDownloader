using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ScatoloneDownloader.Cli
{
    /// <summary>
    /// Rebuilds the correct Scryfall card name from a PNG filename, working around
    /// Windows illegal-filename-character limits (e.g. the colon) and the double-
    /// faced <c>_</c> separator. Shared by <see cref="BuildViewsCommand"/> and the
    /// tagger so both match images to bulk data identically.
    /// </summary>
    internal static class CardNameNormalizer
    {
        public static string Normalize(string fileNameWithoutExt)
        {
            // 1. Unique cases (exact match).
            Dictionary<string, string> uniqueCases = new(StringComparer.OrdinalIgnoreCase)
            {
                // Add hardcoded exceptions here.
                // Example: { "File Name", "Scryfall Name" }
                // { "B.F.M. 1", "B.F.M. (Big Furry Monster)" },
            };

            if (uniqueCases.TryGetValue(fileNameWithoutExt, out string exactMatch))
            {
                return exactMatch;
            }

            // 2. Base rule for double-faced cards.
            string cardName = fileNameWithoutExt.Replace("_", " // ");

            // 3. Rules to restore the colon (:).
            string[] colonPrefixes =
            {
                "Circle of Protection ",
                "Rune of Protection ",
                "Sidequest ",
                "Summon ",
                "Ultimate Magic ",
            };

            foreach (string prefix in colonPrefixes)
            {
                if (cardName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cardName = $"{prefix.Remove(prefix.Length - 1)}: {cardName.Substring(prefix.Length)}";
                    break;
                }
            }

            // 4. Regex rule for "Vault <number>" (e.g. Fallout cards).
            // Matches "Vault " followed by one or more digits and a space at the
            // start of the string. Example: "Vault 101 Birthday Party" -> "Vault 101: Birthday Party".
            cardName = Regex.Replace(cardName, @"^(Vault \d+) ", "$1: ");

            return cardName;
        }
    }
}
