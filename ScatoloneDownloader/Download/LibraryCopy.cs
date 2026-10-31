using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Download
{
    /// <summary>
    /// The pure part of <c>files</c> copying from the library: which cards of a list
    /// the library already holds an image of and so are copied, and which still have
    /// to be downloaded. Kept apart from the disk and network work so the decision is
    /// testable without either.
    /// </summary>
    internal static class LibraryCopy
    {
        /// <summary>
        /// The image file per oracle id, from the cards matched to the library's
        /// files. The first file per oracle id wins, so the order of
        /// <paramref name="matched"/> (the rules' picks first) decides which one.
        /// </summary>
        internal static Dictionary<string, string> FileByOracleId(IEnumerable<(Card Card, string FilePath)> matched)
        {
            Dictionary<string, string> files = new(StringComparer.Ordinal);

            foreach ((Card card, string filePath) in matched)
            {
                if (!string.IsNullOrEmpty(card.OracleId))
                {
                    files.TryAdd(card.OracleId, filePath);
                }
            }

            return files;
        }

        /// <summary>
        /// Splits the cards of a list. A card is copied when it is not a basic land
        /// and the library holds an image of its oracle id. A basic land (its
        /// printing is the point of asking for it with <c>--lands</c>, and the library
        /// holds at most one per name) and a card with no oracle id are downloaded, like
        /// every card the library lacks.
        /// <see cref="LibraryCopySplit.SyncWouldBring"/> is the part of the download
        /// the library rules select: missing from Source only because <c>sync</c> has
        /// not run yet.
        /// </summary>
        internal static LibraryCopySplit Split(
            IEnumerable<Card> cards, IReadOnlyDictionary<string, string> fileByOracleId, IReadOnlySet<string> selectedOracleIds)
        {
            List<(Card Card, string SourceFile)> toCopy = [];
            List<Card> toDownload = [];
            List<Card> syncWouldBring = [];

            foreach (Card card in cards)
            {
                if (!card.IsBasicLand
                    && !string.IsNullOrEmpty(card.OracleId)
                    && fileByOracleId.TryGetValue(card.OracleId, out string? sourceFile))
                {
                    toCopy.Add((card, sourceFile));
                    continue;
                }

                toDownload.Add(card);

                if (!string.IsNullOrEmpty(card.OracleId) && selectedOracleIds.Contains(card.OracleId))
                {
                    syncWouldBring.Add(card);
                }
            }

            return new LibraryCopySplit(toCopy, toDownload, syncWouldBring);
        }

        /// <summary>
        /// Copies a library image to <paramref name="target"/>, creating its folder.
        /// An existing file is replaced, never numbered: running the same list again
        /// refreshes the export. The copy goes through <see cref="AtomicFile.Copy"/>, so
        /// a crash never leaves a truncated PNG under the real name.
        /// </summary>
        internal static void Copy(string sourceFile, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            AtomicFile.Copy(sourceFile, target);
        }
    }

    /// <summary>What <see cref="LibraryCopy.Split"/> decided.</summary>
    /// <param name="ToCopy">Cards the library holds an image of, with that file.</param>
    /// <param name="ToDownload">Every other card.</param>
    /// <param name="SyncWouldBring">The cards of <paramref name="ToDownload"/> whose oracle
    /// id the library rules select.</param>
    internal sealed record LibraryCopySplit(
        IReadOnlyList<(Card Card, string SourceFile)> ToCopy, IReadOnlyList<Card> ToDownload, IReadOnlyList<Card> SyncWouldBring);
}
