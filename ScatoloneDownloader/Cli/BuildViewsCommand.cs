using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
    /// <summary>
    /// Regenerates the multi-root <c>Views/</c> tree (see <see cref="ViewGenerator"/>
    /// and <c>docs/cube-metadata.md</c>) from the physical master folder plus the
    /// git-tracked metadata directory (see <see cref="CubeMetadataStore"/>), then
    /// writes the analysis report alongside it. Rating/status/effects are loaded
    /// from the metadata only — this command never reads XMP, so it is safe to
    /// run without Adobe Bridge installed.
    /// </summary>
    internal sealed class BuildViewsCommand : AsyncCommand<BuildViewsCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png).")]
            public string SourceDirectory { get; set; }

            [CommandOption("-v|--views")]
            [Description("Destination folder for the generated views.")]
            public string ViewsDirectory { get; set; }

            [CommandOption("-m|--metadata")]
            [Description("Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). Defaults to ./metadata.")]
            public string MetadataDirectory { get; set; }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.SourceDirectory) || !Directory.Exists(settings.SourceDirectory))
            {
                AnsiConsole.MarkupLine($"[red]Error: source folder '{settings.SourceDirectory}' does not exist.[/]");
                return 1;
            }

            string masterDir = Path.GetFullPath(settings.SourceDirectory);
            string viewsDir = settings.ViewsDirectory;

            if (string.IsNullOrWhiteSpace(viewsDir))
            {
                DirectoryInfo sourceInfo = new(masterDir);
                string parentDir = sourceInfo.Parent?.FullName ?? masterDir;
                viewsDir = Path.Combine(parentDir, "Views");
            }
            else
            {
                viewsDir = Path.GetFullPath(viewsDir);
            }

            AnsiConsole.MarkupLine($"[cyan]Master folder:[/] {masterDir}");
            AnsiConsole.MarkupLine($"[cyan]Views folder :[/] {viewsDir}");

            string[] pngFiles = Directory.GetFiles(masterDir, "*.png", SearchOption.AllDirectories);
            if (pngFiles.Length == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No .png files found in '{masterDir}'.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]Found {pngFiles.Length} files. Loading bulk data from Scryfall...[/]");

            // Tuples of (Scryfall card, absolute file path).
            List<(Card Card, string FilePath)> matchedCards = new();

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();

                Dictionary<string, Card> cardsByName = new(StringComparer.OrdinalIgnoreCase);
                foreach (Card c in allCards)
                {
                    if (!cardsByName.ContainsKey(c.Name))
                    {
                        cardsByName.Add(c.Name, c);
                    }
                }

                foreach (string file in pngFiles)
                {
                    string fileName = Path.GetFileName(file);
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(file);

                    // Apply the normalization rules to rebuild the Scryfall name.
                    string cardName = CardNameNormalizer.Normalize(nameWithoutExt);

                    if (cardsByName.TryGetValue(cardName, out Card card))
                    {
                        matchedCards.Add((card, file));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Warning:[/] no Scryfall card found for file '{fileName}' (searched name: '{cardName}')");
                    }
                }
            }

            AnsiConsole.MarkupLine($"[green]Matched {matchedCards.Count} valid cards in the database.[/]");

            if (matchedCards.Count == 0) return 0;

            string metadataDir = string.IsNullOrWhiteSpace(settings.MetadataDirectory)
                ? Path.GetFullPath("metadata")
                : Path.GetFullPath(settings.MetadataDirectory);

            // Rating/status/effects come from the metadata only — XMP is legacy
            // input, read once by `import`, never by view generation (P5).
            AnsiConsole.MarkupLine($"[yellow]Loading rating/status/effect tags from '{metadataDir}'...[/]");
            MetadataJsonSynchronizer.SyncFromJson(matchedCards.Select(m => m.Card), metadataDir);
            AnsiConsole.MarkupLine("[green]Metadata loaded.[/]");

            AnsiConsole.MarkupLine($"[yellow]Generating views in '{viewsDir}'...[/]");
            ViewGenerator.GenerateViews(matchedCards, viewsDir);
            AnsiConsole.MarkupLine("[green]Views generated successfully![/]");

            AnsiConsole.MarkupLine("[yellow]Generating analysis report...[/]");

            CardAnalyzer analyzer = new(matchedCards.Select(m => m.Card).ToList());

            if (!Directory.Exists(viewsDir))
            {
                Directory.CreateDirectory(viewsDir);
            }

            string reportPath = Path.Combine(viewsDir, "Cubo_Analysis.md");
            analyzer.SaveAnalysis(reportPath);

            AnsiConsole.MarkupLine($"[green]Report saved to: {reportPath}[/]");

            return 0;
        }
    }
}
