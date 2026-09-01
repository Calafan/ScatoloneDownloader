using System;
using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Mtg;

using Spectre.Console;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Matches physical master image files to Scryfall cards by NAME, shared by
    /// <c>import</c>, <c>tag</c>, and <c>build-views</c> (<c>restore</c> matches in
    /// reverse — from metadata to bulk by id/oracle_id — and stays separate). The
    /// forward name match was previously copy-pasted into each of the three
    /// commands, which had already drifted apart; this is the single implementation.
    /// <para>
    /// Callers scan the <c>.png</c> list themselves and pass it in (rather than
    /// this method scanning the directory) so they can skip the expensive Scryfall
    /// bulk download when the folder is empty, before they have any cards to match.
    /// </para>
    /// </summary>
    internal static class CardImageMatcher
    {
        /// <summary>
        /// Returns one <c>(Card, filePath)</c> per image file that resolves to a
        /// Scryfall card. A file is matched on its exact name first; only if that
        /// fails does it fall back to the collapsed key of
        /// <see cref="CardNameKey"/>, which recovers the names a Windows filename
        /// cannot spell (<c>Summon: Choco/Mog</c>, <c>Henzie "Toolbox" Torre</c>,
        /// <c>Fire // Ice</c>). Cards are indexed first-wins (case-insensitive),
        /// mirroring the bulk's own printing order. When
        /// <paramref name="warnUnmatched"/> is set, files with no match are
        /// reported to the console (otherwise silently skipped).
        /// </summary>
        internal static List<(Card Card, string FilePath)> Match(
            IEnumerable<Card> cards, IEnumerable<string> pngFiles, bool warnUnmatched)
        {
            Dictionary<string, Card> cardsByName = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Card> cardsByKey = new(StringComparer.Ordinal);

            // A handful of distinct cards do collapse to the same key (tokens and
            // silver-border jokes such as "Waste Land" / "Wasteland"). Matching one
            // of those would silently pick the wrong card, so they are excluded
            // from the fallback and reported as unmatched instead.
            HashSet<string> ambiguousKeys = new(StringComparer.Ordinal);

            foreach (Card card in cards)
            {
                cardsByName.TryAdd(card.Name, card);

                string cardKey = CardNameKey.Collapse(card.Name);
                if (cardsByKey.TryGetValue(cardKey, out Card indexed))
                {
                    if (!string.Equals(indexed.Name, card.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        ambiguousKeys.Add(cardKey);
                    }
                }
                else
                {
                    cardsByKey.Add(cardKey, card);
                }
            }

            List<(Card Card, string FilePath)> matched = [];
            foreach (string file in pngFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string fileKey = CardNameKey.Collapse(fileName);

                if (!cardsByName.TryGetValue(fileName, out Card card) && !ambiguousKeys.Contains(fileKey))
                {
                    cardsByKey.TryGetValue(fileKey, out card);
                }

                if (card is not null)
                {
                    matched.Add((card, file));
                }
                else if (warnUnmatched)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Warning:[/] no Scryfall card found for file '{Path.GetFileName(file)}' (collapsed name: '{fileKey}')");
                }
            }

            return matched;
        }
    }
}
