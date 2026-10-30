using System.Globalization;
using System.Text.Json;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// The <c>/stats</c> page frozen with its data: a self-contained HTML file for
    /// people who do not run the tagger. It is the live page itself
    /// (<see cref="TagCommand.GetStatsPageHtml"/>) with the payload the page would
    /// fetch from <c>/api/stats</c> embedded before its script; the page sees
    /// <c>window.STATS_SNAPSHOT</c> and renders from it without fetching. Used by
    /// the <c>report</c> command and by the page's download links, next to the
    /// Markdown twin (<c>CardAnalyzer.RenderMarkdown</c>) with the same numbers.
    /// </summary>
    internal static class StatsReport
    {
        private const string TitleTag = "<title>Scatolone Cube Stats</title>";
        private const string ScriptTag = "<script>";

        /// <summary>
        /// The line under the report's title: when it was made and what pool and bench
        /// mean. No file paths: the files go to other people.
        /// </summary>
        internal static string Subtitle(DateTime generatedAt)
        {
            return $"Generated {generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"
                + " · pool: rating 3-5, no status · bench: exactly ★2";
        }

        /// <summary>The download name without extension, <c>Cubo_Analysis_yyyy-MM-dd</c>.</summary>
        internal static string FileStem(DateTime generatedAt)
        {
            return "Cubo_Analysis_" + generatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The stats page with <paramref name="payload"/> frozen into it, inserted right
        /// before the page's own script. The JSON is the one <c>/api/stats</c> serves
        /// (<see cref="StatsPayload.JsonOptions"/>); the default JSON encoder escapes
        /// <c>&lt;</c>, so a card name cannot close the script tag.
        /// </summary>
        internal static string RenderHtml(StatsPayload payload, string subtitle, DateTime generatedAt)
        {
            string page = TagCommand.GetStatsPageHtml();

            int script = page.IndexOf(ScriptTag, StringComparison.Ordinal);
            if (script < 0 || !page.Contains(TitleTag, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The stats page has no title or script tag to freeze the data into.");
            }

            string json = JsonSerializer.Serialize(payload, StatsPayload.JsonOptions);
            string info = JsonSerializer.Serialize(subtitle);

            string snapshot = $"<script>window.STATS_SNAPSHOT = {json}; window.STATS_SNAPSHOT_INFO = {info};</script>\n";

            string title = "<title>Scatolone Cube Stats — "
                + generatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "</title>";

            return page.Insert(script, snapshot).Replace(TitleTag, title, StringComparison.Ordinal);
        }
    }
}
