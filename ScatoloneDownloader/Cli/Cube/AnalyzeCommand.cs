using System.ComponentModel;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Writes the statistics of list file(s) as the same two files <c>report</c>
    /// writes for the cube: Markdown and the <c>/stats</c> page frozen with its data,
    /// both from one <see cref="StatsPayload"/> (<see cref="StatsPayload.BuildForList"/>).
    /// Every card of a list counts, whatever its rating or status in the cube. Effects,
    /// rating and Reviewed come from the cube's store; a card the store does not know
    /// carries the classifier's proposed effects
    /// (<see cref="MetadataJsonSynchronizer.SyncFromJsonOrClassify"/>). No image is
    /// downloaded and nothing in the library or the store is touched.
    /// </summary>
    internal sealed class AnalyzeCommand : AsyncCommand<AnalyzeCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            internal override bool NeedsSource => false;

            [CommandArgument(0, "<FILES>")]
            [Description("One or more list files to analyze.")]
            public string[] Files { get; set; } = [];

            [CommandOption("-o|--output <FILE>")]
            [Description("Base path of the two files; the extension is replaced by .md and .html. Defaults to <list>_Analysis in the current folder. Only with a single list.")]
            public string? OutputFile { get; set; }

            public override ValidationResult Validate()
            {
                ValidationResult baseResult = base.Validate();
                if (!baseResult.Successful)
                {
                    return baseResult;
                }

                if (!string.IsNullOrWhiteSpace(OutputFile) && Files.Length > 1)
                {
                    return ValidationResult.Error("--output <FILE> works with a single list; with several lists each gets its own <list>_Analysis files.");
                }

                return ValidationResult.Success();
            }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string metadataDir = settings.ResolveDirectory();
            AnsiConsole.MarkupLineInterpolated($"[cyan]Metadata     :[/] {metadataDir}");

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);

            // One manager for every list: the Scryfall bulk is fetched once.
            using GetManager getManager = new();

            foreach (string file in settings.Files)
            {
                if (!File.Exists(file))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]File not found:[/] {file}");
                    continue;
                }

                // A "Name -- tag" line is not counted by the analysis (CardAnalyzer.Analyze
                // skips a card with a Tag), so the cards it carries are not counted here either.
                List<Card> listed = await getManager.GetCardList(file, downloadLands: false);
                List<Card> cards = listed.Where(c => string.IsNullOrEmpty(c.Tag)).ToList();
                int taggedLeftOut = listed.Count - cards.Count;
                int inStore = MetadataJsonSynchronizer.SyncFromJsonOrClassify(cards, metadata);

                AnsiConsole.MarkupLineInterpolated($"[green]{Path.GetFileName(file)}: {cards.Count} cards, {inStore} in the cube's store.[/]");
                if (taggedLeftOut > 0)
                {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]Tagged lines (Name -- tag) left out: {taggedLeftOut}.[/]");
                }
                AnsiConsole.MarkupLine("[yellow]Generating the report...[/]");

                DateTime now = DateTime.Now;
                string stem = Path.GetFileNameWithoutExtension(file);
                StatsPayload payload = StatsPayload.BuildForList(cards, stem);
                StatsDocument document = StatsDocument.ForList(Path.GetFileName(file), now, cards.Count, inStore, taggedLeftOut);

                (string markdownPath, string htmlPath) = ReportCommand.ResolveOutputs(
                    settings.OutputFile, Directory.GetCurrentDirectory(), stem + "_Analysis");

                ReportCommand.WriteStatsFiles(payload, document, now, markdownPath, htmlPath);
            }

            return 0;
        }
    }
}
