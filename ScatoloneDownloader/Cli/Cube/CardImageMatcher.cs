using ScatoloneDownloader.Download;
using ScatoloneDownloader.Mtg;

using Spectre.Console;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Matches physical master image files to Scryfall cards by NAME, shared by
    /// <c>register</c>, <c>tag</c>, and <c>report</c> (<c>restore</c> matches in
    /// reverse — from metadata to bulk by id/oracle_id — and stays separate). The
    /// forward name match was previously copy-pasted into each of the three
    /// commands, which had already drifted apart; this is the single implementation.
    /// <para>
    /// <see cref="ScanAndMatchAsync"/> is the whole scan-then-match sequence the
    /// three commands share: it lists the <c>.png</c> files first and returns
    /// before the expensive Scryfall bulk download when the folder is empty, since
    /// there are then no cards to match. <see cref="Match"/> alone is the pure
    /// matching step.
    /// </para>
    /// </summary>
    internal static class CardImageMatcher
    {
        /// <summary>
        /// Scans <paramref name="masterDir"/> recursively for <c>.png</c> files,
        /// loads the Scryfall bulk and matches the files to cards (reporting the
        /// unmatched ones). Prints the "No .png files" / "Found N files" lines the
        /// commands share. Returns <c>null</c> when the folder holds no <c>.png</c>
        /// — the caller then returns 0 — and the (possibly empty) match list
        /// otherwise; each caller prints its own "Matched ..." line.
        /// </summary>
        internal static async Task<List<(Card Card, string FilePath)>?> ScanAndMatchAsync(string masterDir)
        {
            string[] pngFiles = Directory.GetFiles(masterDir, "*.png", SearchOption.AllDirectories);
            if (pngFiles.Length == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No .png files found in '{masterDir}'.[/]");
                return null;
            }

            AnsiConsole.MarkupLine($"[yellow]Found {pngFiles.Length} files. Loading bulk data from Scryfall...[/]");

            using GetManager manager = new();
            List<Card> allCards = await manager.GetDefaultCards();
            return Match(allCards, pngFiles, warnUnmatched: true);
        }

        /// <summary>
        /// Returns one <c>(Card, filePath)</c> per image file that resolves to a
        /// Scryfall card. A file is matched on its exact name first; only if that
        /// fails does it fall back to the collapsed key of
        /// <see cref="CardNameKey"/>, which recovers the names a Windows filename
        /// cannot spell (<c>Summon: Choco/Mog</c>, <c>Henzie "Toolbox" Torre</c>,
        /// <c>Fire // Ice</c>). Cards are indexed first-wins (case-insensitive),
        /// mirroring the bulk's own printing order, but a printing from the set the
        /// file's folder is named after wins first: a library file sits in
        /// <c>&lt;year&gt;\&lt;set&gt;</c>, and the bulk lists tokens, front cards and
        /// planes of the same name before the card itself (the Coruscation Mage token,
        /// the Inferno front card — 26 library files went to the wrong object,
        /// measured 2026-10-20). When <paramref name="warnUnmatched"/> is set, files
        /// with no match are reported to the console (otherwise silently skipped).
        /// </summary>
        internal static List<(Card Card, string FilePath)> Match(
            IEnumerable<Card> cards, IEnumerable<string> pngFiles, bool warnUnmatched)
        {
            Dictionary<string, Card> cardsByName = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Card> cardsByKey = new(StringComparer.Ordinal);

            // Keyed "<set folder>\<name or key>", the shape of a library file's path.
            Dictionary<string, Card> cardsBySetName = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Card> cardsBySetKey = new(StringComparer.OrdinalIgnoreCase);

            // A handful of distinct cards do collapse to the same key (tokens and
            // silver-border jokes such as "Waste Land" / "Wasteland"). Matching one
            // of those would silently pick the wrong card, so they are excluded
            // from the fallback and reported as unmatched instead.
            HashSet<string> ambiguousKeys = new(StringComparer.Ordinal);

            foreach (Card card in cards)
            {
                cardsByName.TryAdd(card.Name, card);

                string setFolder = OutputPaths.Sanitize(card.SetName);
                string cardKey = CardNameKey.Collapse(card.Name);
                cardsBySetName.TryAdd(setFolder + @"\" + card.Name, card);
                cardsBySetKey.TryAdd(setFolder + @"\" + cardKey, card);

                if (cardsByKey.TryGetValue(cardKey, out Card? indexed))
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
                string folder = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;

                if (!cardsBySetName.TryGetValue(folder + @"\" + fileName, out Card? card)
                    && !cardsByName.TryGetValue(fileName, out card)
                    && !ambiguousKeys.Contains(fileKey)
                    && !cardsBySetKey.TryGetValue(folder + @"\" + fileKey, out card))
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
