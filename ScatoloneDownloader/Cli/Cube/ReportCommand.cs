using System.ComponentModel;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Writes the cube analysis report (<c>Cubo_Analysis.md</c>) from the physical
    /// master folder plus the git-tracked metadata directory (see
    /// <see cref="Metadata.CubeMetadataStore"/>). Rating/status/effects are loaded from the
    /// metadata only. Read-only toward the library and the store: it writes the
    /// one report file and deletes nothing.
    /// </summary>
    internal sealed class ReportCommand : AsyncCommand<ReportCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            internal override bool NeedsSource => true;

            [CommandOption("-o|--output")]
            [Description("Report file to write. Defaults to Views\\Cubo_Analysis.md in the library root.")]
            public string? OutputFile { get; set; }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string masterDir = settings.ResolveSourceDirectory();
            string reportPath;

            if (string.IsNullOrWhiteSpace(settings.OutputFile))
            {
                reportPath = Path.Combine(LibraryRoot.Views(settings.ResolveRoot()), "Cubo_Analysis.md");
            }
            else
            {
                reportPath = Path.GetFullPath(settings.OutputFile);
            }

            AnsiConsole.MarkupLineInterpolated($"[cyan]Master folder:[/] {masterDir}");
            AnsiConsole.MarkupLineInterpolated($"[cyan]Report       :[/] {reportPath}");

            // Tuples of (Scryfall card, absolute file path).
            List<(Card Card, string FilePath)>? matchedCards = await CardImageMatcher.ScanAndMatchAsync(masterDir);
            if (matchedCards is null)
            {
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]Matched {matchedCards.Count} valid cards in the database.[/]");

            if (matchedCards.Count == 0) return 0;

            string metadataDir = settings.ResolveDirectory();

            AnsiConsole.MarkupLineInterpolated($"[yellow]Loading rating/status/effect tags from '{metadataDir}'...[/]");
            MetadataJsonSynchronizer.SyncFromJson(matchedCards.Select(m => m.Card), metadataDir);
            AnsiConsole.MarkupLine("[green]Metadata loaded.[/]");

            AnsiConsole.MarkupLine("[yellow]Generating analysis report...[/]");

            // Analyse the active pool only (rating 3-5, no Banned/Token/Jolly) so
            // the unrated backlog and excluded status cards cannot skew the
            // distributions. Rating 2 is passed along too, but only for the
            // bench-availability appendix: it answers "what can fill this hole"
            // without entering any pool metric.
            CardAnalyzer analyzer = CardAnalyzer.ForPool(matchedCards.Select(m => m.Card));

            string? reportDir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDir) && !Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            analyzer.SaveAnalysis(reportPath);

            AnsiConsole.MarkupLineInterpolated($"[green]Report saved to: {reportPath}[/]");

            return 0;
        }
    }
}
