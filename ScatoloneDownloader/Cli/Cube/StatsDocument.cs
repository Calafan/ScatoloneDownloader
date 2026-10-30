using System.Globalization;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// The words around a stats file's numbers: the title, the line under it and the
    /// note. The page (<see cref="StatsReport"/>) and the Markdown
    /// (<see cref="StatsMarkdown"/>) both print these three strings, so the two files
    /// of a pair open the same way.
    /// </summary>
    /// <param name="Title">The heading and, with the date, the HTML title.</param>
    /// <param name="Subtitle">When it was made and what it is about.</param>
    /// <param name="Note">What the numbers count.</param>
    internal sealed record StatsDocument(string Title, string Subtitle, string Note)
    {
        /// <summary>The cube's report (<c>report</c> command and the page's downloads).</summary>
        internal static StatsDocument ForCube(DateTime generatedAt)
        {
            return new StatsDocument(
                "Scatolone Cube Stats",
                StatsReport.Subtitle(generatedAt),
                "A snapshot of the cube's statistics. The pool is the cards rated 3–5 with no status; the bench is exactly ★2.");
        }

        /// <summary>A card list's report (<c>analyze</c> command): <paramref name="cards"/>
        /// counted cards, <paramref name="inStore"/> of them with an entry in the cube's
        /// store, and the <paramref name="taggedLeftOut"/> cards of <c>Name -- tag</c>
        /// lines, which the analysis does not count (named only when there are some).</summary>
        internal static StatsDocument ForList(string listFileName, DateTime generatedAt, int cards, int inStore, int taggedLeftOut)
        {
            string when = generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            string leftOut = taggedLeftOut > 0
                ? string.Create(CultureInfo.InvariantCulture, $" · tagged lines (Name -- tag) left out: {taggedLeftOut}")
                : "";

            return new StatsDocument(
                Path.GetFileNameWithoutExtension(listFileName) + " — List Stats",
                string.Create(CultureInfo.InvariantCulture,
                    $"Generated {when} · list: {listFileName} · {cards} cards, {inStore} in the cube's store{leftOut}"),
                "A snapshot of a card list's statistics. Every card of the list counts, whatever its rating or status in the cube. Effects, ★ and Reviewed come from the cube's store; a card not in the store carries the classifier's proposed effects.");
        }
    }
}
