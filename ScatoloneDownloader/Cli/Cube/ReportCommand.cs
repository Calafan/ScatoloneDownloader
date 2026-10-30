using System.ComponentModel;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Writes the cube statistics as two files with the same numbers: the Markdown
    /// analysis (<c>Cubo_Analysis.md</c>, <see cref="CardAnalyzer.RenderMarkdown"/>)
    /// and a self-contained HTML (<c>Cubo_Analysis.html</c>), the tagger's
    /// <c>/stats</c> page frozen with its data (<see cref="StatsReport"/>). The
    /// cards are the ones the tagger shows: the master folder scanned and matched,
    /// then the store's rating/status/effects applied (see
    /// <see cref="Metadata.CubeMetadataStore"/>). Read-only toward the library and
    /// the store: it writes the two report files and deletes nothing.
    /// </summary>
    internal sealed class ReportCommand : AsyncCommand<ReportCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            internal override bool NeedsSource => true;

            [CommandOption("-o|--output <FILE>")]
            [Description("Base path of the two files; the extension is replaced by .md and .html. Defaults to Cubo_Analysis in the current folder.")]
            public string? OutputFile { get; set; }
        }

        /// <summary>
        /// The two paths to write. A blank <paramref name="output"/> is
        /// <c>Cubo_Analysis</c> in <paramref name="currentDirectory"/>; otherwise it is
        /// the full path with any extension replaced, so <c>x\stats</c>,
        /// <c>x\stats.md</c> and <c>x\stats.html</c> all give <c>x\stats.md</c> and
        /// <c>x\stats.html</c>.
        /// </summary>
        internal static (string Markdown, string Html) ResolveOutputs(string? output, string currentDirectory)
        {
            string basePath = string.IsNullOrWhiteSpace(output)
                ? Path.Combine(currentDirectory, "Cubo_Analysis")
                : Path.GetFullPath(output, currentDirectory);

            return (Path.ChangeExtension(basePath, ".md"), Path.ChangeExtension(basePath, ".html"));
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string masterDir = settings.ResolveSourceDirectory();
            string metadataDir = settings.ResolveDirectory();
            (string markdownPath, string htmlPath) = ResolveOutputs(settings.OutputFile, Directory.GetCurrentDirectory());

            AnsiConsole.MarkupLineInterpolated($"[cyan]Master folder:[/] {masterDir}");
            AnsiConsole.MarkupLineInterpolated($"[cyan]Metadata     :[/] {metadataDir}");

            // Tuples of (Scryfall card, absolute file path).
            List<(Card Card, string FilePath)>? matchedCards = await CardImageMatcher.ScanAndMatchAsync(masterDir);
            if (matchedCards is null)
            {
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]Matched {matchedCards.Count} valid cards in the database.[/]");

            if (matchedCards.Count == 0) return 0;

            // The same load the tagger does, so both see the same cards.
            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);
            MetadataJsonSynchronizer.SyncFromJson(matchedCards.Select(m => m.Card), metadata);

            AnsiConsole.MarkupLine("[yellow]Generating the report...[/]");

            // The active pool only (rating 3-5, no Banned/Token/Jolly) so the unrated
            // backlog and excluded status cards cannot skew the distributions; the
            // bench (rating 2) rides along for its availability appendix.
            List<Card> cards = matchedCards.Select(m => m.Card).ToList();
            DateTime now = DateTime.Now;
            string subtitle = StatsReport.Subtitle(now);

            string markdown = CardAnalyzer.ForPool(cards).RenderMarkdown(subtitle);
            string html = StatsReport.RenderHtml(StatsPayload.Build(cards), subtitle, now);

            foreach (string path in new[] { markdownPath, htmlPath })
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            File.WriteAllText(markdownPath, markdown);
            File.WriteAllText(htmlPath, html);

            AnsiConsole.MarkupLineInterpolated($"[green]Markdown saved to: {markdownPath}[/]");
            AnsiConsole.MarkupLineInterpolated($"[green]HTML saved to    : {htmlPath}[/]");

            return 0;
        }
    }
}
