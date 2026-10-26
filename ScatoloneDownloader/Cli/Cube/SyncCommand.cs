using System.ComponentModel;

using ScatoloneDownloader.Cli.Download;
using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Brings the library up to the library rules in one go: downloads the cards
    /// the rules select and <c>Source\</c> lacks, registers them in the metadata
    /// store as unrated entries and proposes their effects (never stamping
    /// <c>reviewedAt</c>, never touching an existing entry). Files no rule selects
    /// and store entries with no image are only LISTED: nothing here deletes or
    /// moves a file or removes an entry. The Scryfall bulk is downloaded once and
    /// reused by every step.
    /// </summary>
    internal sealed class SyncCommand : AsyncCommand<SyncCommand.Settings>
    {
        /// <summary>How many names are listed per section before the rest are summarised.</summary>
        private const int ListedNames = 50;

        public sealed class Settings : MetadataSettings
        {
            internal override bool NeedsSource => true;

            [CommandOption("--dry-run")]
            [Description("Report what would be downloaded and registered without downloading or writing anything.")]
            public bool DryRun { get; set; }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string root = settings.ResolveRoot();
            string source = settings.ResolveSourceDirectory();
            string metadataDir = settings.ResolveDirectory();

            AnsiConsole.MarkupLineInterpolated($"[cyan]Root    :[/] {root}");
            AnsiConsole.MarkupLineInterpolated($"[cyan]Source  :[/] {source}");
            AnsiConsole.MarkupLineInterpolated($"[cyan]Metadata:[/] {metadataDir}");
            AnsiConsole.MarkupLine("[yellow]Do not run this while `tag` is open: the tagger holds its own snapshot of the store. Restart `tag` afterwards.[/]");

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);

            using GetManager manager = new();
            AnsiConsole.MarkupLine("[yellow]Loading bulk data from Scryfall...[/]");
            List<Card> bulk = await manager.GetDefaultCards();

            // The same selection `all --library` and `years --library` make.
            List<Card> selected = LibrarySelector.Select(bulk, reprints: false, tokens: false, lands: false, releasedBy: DateTime.Today);

            // The expected file is the path `years --library` would write.
            OutputPaths.UseLibrary(source);
            List<(Card Card, string ExpectedPath)> expected = [.. selected.Select(card => (
                card,
                Path.Combine(OutputPaths.CardDirectory(card, Mode.All, null), OutputPaths.Sanitize(card.Name) + ".png")))];

            SyncPlan plan = LibrarySync.Plan(expected, Directory.GetFiles(source, "*.png", SearchOption.AllDirectories));

            AnsiConsole.MarkupLine(
                $"Selected {selected.Count} cards: [green]{plan.PresentCount} present[/], [yellow]{plan.Missing.Count} missing[/], {plan.Extra.Count} extra files.");

            ListLines(
                plan.Missing.Select(m => Path.GetRelativePath(source, m.ExpectedPath)).ToList(),
                "Missing — to download:");

            if (!settings.DryRun && plan.Missing.Count > 0)
            {
                CardDownloader downloader = new(manager);
                await CardService.DownloadAllAsync(
                    downloader, [.. plan.Missing.Select(m => m.Card)], Mode.All, null, "missing cards", skipExisting: true);
            }

            // Dry run still computes the registration, from the files now plus the
            // cards that would arrive, without touching the store.
            string[] files = Directory.GetFiles(source, "*.png", SearchOption.AllDirectories);
            // The rules' picks first, as CardImageMatcher.LibraryPicksFirst orders them.
            List<(Card Card, string FilePath)> matched = CardImageMatcher.Match([.. selected, .. bulk], files, warnUnmatched: false);
            Dictionary<string, Card> cardsByOracleId = CardIndex.ByOracleId(bulk);

            HashSet<string> presentOracleIds = new(
                matched.Select(m => m.Card.OracleId).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);

            if (settings.DryRun)
            {
                IEnumerable<string> arriving = plan.Missing.Select(m => m.Card.OracleId);
                presentOracleIds.UnionWith(arriving.Where(id => !string.IsNullOrEmpty(id)));

                int wouldRegister = LibrarySync.NewOracleIds(metadata, matched.Select(m => m.Card.OracleId).Concat(arriving)).Count;
                AnsiConsole.MarkupLine($"[green]Would register {wouldRegister} new cards as unrated.[/]");
            }
            else
            {
                SyncRegistration registration = LibrarySync.RegisterAndPropose(metadata, matched, cardsByOracleId);

                if (registration.AddedNames.Count > 0)
                {
                    CubeMetadataStore.Save(metadataDir, metadata);
                }

                AnsiConsole.MarkupLine(
                    $"[green]Registered {registration.AddedNames.Count} new cards as unrated;[/] proposed effects for {registration.Proposed} ({registration.NoProposal} with no proposal).");
                ListLines(registration.AddedNames, "Registered:");
            }

            ListLines(
                [.. plan.Extra.Select(path => Path.GetRelativePath(source, path))],
                "Extra files — not selected by the library rules, left in place:");

            ListLines(
                [.. LibrarySync.Orphans(metadata, presentOracleIds)
                    .Select(o => $"{o.Name} (rating {o.Rating}, {(o.Reviewed ? "reviewed" : "unreviewed")})")],
                "Store entries with no image — left in place:");

            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[grey]Dry run: nothing was downloaded or written.[/]");
            }

            return 0;
        }

        /// <summary>Prints a titled list, the first <see cref="ListedNames"/> lines and a count of the rest;
        /// prints nothing for an empty list.</summary>
        private static void ListLines(IReadOnlyList<string> lines, string title)
        {
            if (lines.Count == 0)
            {
                return;
            }

            AnsiConsole.MarkupLineInterpolated($"[yellow]{title}[/]");

            foreach (string line in lines.Take(ListedNames))
            {
                AnsiConsole.MarkupLineInterpolated($"  {line}");
            }

            if (lines.Count > ListedNames)
            {
                AnsiConsole.MarkupLine($"  … and {lines.Count - ListedNames} more");
            }
        }
    }
}
