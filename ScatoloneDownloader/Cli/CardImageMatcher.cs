using System;
using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Mtg;

using Spectre.Console;

namespace ScatoloneDownloader.Cli
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
        /// Returns one <c>(Card, filePath)</c> per image file whose normalized name
        /// resolves to a Scryfall card. Cards are indexed by name first-wins
        /// (case-insensitive), mirroring the bulk's own printing order. When
        /// <paramref name="warnUnmatched"/> is set, files with no match are
        /// reported to the console (otherwise silently skipped).
        /// </summary>
        internal static List<(Card Card, string FilePath)> Match(
            IEnumerable<Card> cards, IEnumerable<string> pngFiles, bool warnUnmatched)
        {
            Dictionary<string, Card> cardsByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (Card card in cards)
            {
                cardsByName.TryAdd(card.Name, card);
            }

            List<(Card Card, string FilePath)> matched = [];
            foreach (string file in pngFiles)
            {
                string cardName = CardNameNormalizer.Normalize(Path.GetFileNameWithoutExtension(file));
                if (cardsByName.TryGetValue(cardName, out Card card))
                {
                    matched.Add((card, file));
                }
                else if (warnUnmatched)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Warning:[/] no Scryfall card found for file '{Path.GetFileName(file)}' (searched name: '{cardName}')");
                }
            }

            return matched;
        }
    }
}
