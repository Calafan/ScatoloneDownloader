namespace ScatoloneDownloader.Download
{
    /// <summary>One parsed line of a download-list file: the card name plus its
    /// optional inline tag (the text after <c>--</c>, which the downloader turns
    /// into an output sub-folder). The read-side counterpart to
    /// <c>MakeListCommand</c>'s writer.</summary>
    internal readonly record struct CardListEntry(string Name, string Tag);

    /// <summary>
    /// Reads the hand-written / generated download-list format shared by the
    /// <c>files</c> command (via <see cref="GetManager"/>) and <c>make-list</c>:
    /// one card per line, an optional <c>Name -- tag</c> suffix, with blank lines
    /// and comment lines (the whole line starting with <c>--</c>) skipped.
    /// Centralizes the parsing that used to be copy-pasted across GetManager's
    /// exclude-file and list-file readers.
    /// </summary>
    internal static class CardListFile
    {
        /// <summary>
        /// Parses every card line of <paramref name="path"/> in file order. A card
        /// line is either <c>Name</c> or <c>Name -- tag</c> (the tag is empty when
        /// absent); both name and tag are trimmed. Blank lines and comment lines
        /// (the trimmed line starting with <c>--</c>) are skipped. Throws the usual
        /// <see cref="IO.IOException"/>s if the file cannot be read — callers that
        /// tolerate a missing file check for it first.
        /// </summary>
        internal static async Task<List<CardListEntry>> ReadAsync(string path)
        {
            List<CardListEntry> entries = [];

            foreach (string rawLine in await File.ReadAllLinesAsync(path))
            {
                string line = rawLine.Trim();

                // Skip blank lines and comment lines (make-list writes its header
                // and per-status separators as "-- ..." lines the reader ignores).
                if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.Contains("--", StringComparison.Ordinal))
                {
                    string[] parts = line.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string name = parts.Length > 0 ? parts[0] : string.Empty;
                    string tag = parts.Length > 1 ? parts[1] : string.Empty;

                    if (name.Length > 0)
                    {
                        entries.Add(new CardListEntry(name, tag));
                    }
                }
                else
                {
                    entries.Add(new CardListEntry(line, string.Empty));
                }
            }

            return entries;
        }
    }
}
