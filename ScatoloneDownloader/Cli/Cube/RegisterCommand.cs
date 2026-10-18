using System.ComponentModel;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Adds the library's new cards to the git-tracked metadata directory (see
    /// <see cref="CubeMetadataStore"/>) as unrated entries, keyed by
    /// <see cref="Card.OracleId"/>. This is the only command that creates entries
    /// for new PNGs; it never modifies an existing one, so it cannot undo anything
    /// the tagger or the classifier has recorded. The next step for the new cards
    /// is <c>classify</c>, which proposes their effects.
    /// </summary>
    internal sealed class RegisterCommand : AsyncCommand<RegisterCommand.Settings>
    {
        /// <summary>How many added names are listed before the rest are summarised.</summary>
        private const int ListedNames = 50;

        public sealed class Settings : MetadataSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png).")]
            public string SourceDirectory { get; set; } = string.Empty;

            internal override string MasterDirectory => SourceDirectory;

            [CommandOption("--dry-run")]
            [Description("Report what would be registered without writing the metadata directory.")]
            public bool DryRun { get; set; }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.SourceDirectory) || !Directory.Exists(settings.SourceDirectory))
            {
                AnsiConsole.MarkupLine($"[red]Error: source folder '{settings.SourceDirectory}' does not exist.[/]");
                return 1;
            }

            string masterDir = Path.GetFullPath(settings.SourceDirectory);
            string metadataDir = settings.ResolveDirectory();

            AnsiConsole.MarkupLineInterpolated($"[cyan]Master folder:[/] {masterDir}");
            AnsiConsole.MarkupLineInterpolated($"[cyan]Metadata     :[/] {metadataDir}");

            string[] pngFiles = Directory.GetFiles(masterDir, "*.png", SearchOption.AllDirectories);
            if (pngFiles.Length == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No .png files found in '{masterDir}'.[/]");
                return 0;
            }

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);

            AnsiConsole.MarkupLine($"[yellow]Found {pngFiles.Length} files. Loading bulk data from Scryfall...[/]");

            List<(Card Card, string FilePath)> matched;

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();
                matched = CardImageMatcher.Match(allCards, pngFiles, warnUnmatched: true);
            }

            AnsiConsole.MarkupLine($"[green]Matched {matched.Count} cards.[/]");

            RegisterResult result = RegisterNew(metadata, matched);

            if (result.Added > 0 && !settings.DryRun)
            {
                CubeMetadataStore.Save(metadataDir, metadata);
            }

            string verb = settings.DryRun ? "Would register" : "Registered";
            AnsiConsole.MarkupLineInterpolated(
                $"[green]{verb} {result.Added} new cards as unrated;[/] {result.AlreadyInStore} already in the store, {result.SkippedNoOracleId} skipped (no oracle id).");

            foreach (string name in result.AddedNames.Take(ListedNames))
            {
                AnsiConsole.MarkupLineInterpolated($"  {name}");
            }

            if (result.AddedNames.Count > ListedNames)
            {
                AnsiConsole.MarkupLine($"  … and {result.AddedNames.Count - ListedNames} more");
            }

            if (result.Added > 0)
            {
                if (settings.DryRun)
                {
                    AnsiConsole.MarkupLine("[grey]Dry run: nothing was written.[/]");
                }
                else
                {
                    // The tagger keeps the merged store in memory and writes entries
                    // back from it, so a register that ran underneath it would be
                    // overwritten on the next keystroke.
                    AnsiConsole.MarkupLine("[yellow]Do not run this while `tag` is open: the tagger holds its own snapshot of the store. Restart `tag` afterwards.[/]");
                }

                AnsiConsole.MarkupLineInterpolated($"[grey]Next: classify -m {metadataDir} to propose their effects.[/]");
            }

            return 0;
        }

        /// <summary>
        /// Adds an unrated entry (name and printing id only) for every matched
        /// card whose oracle id the store does not hold yet. An entry already in
        /// the store is never touched, whatever the matched card says, and several
        /// files of one oracle add one entry. Pure, so it is testable without
        /// image files or the Scryfall bulk.
        /// </summary>
        internal static RegisterResult RegisterNew(
            CubeMetadata metadata, IEnumerable<(Card Card, string FilePath)> matched)
        {
            List<string> addedNames = [];
            HashSet<string> addedIds = new(StringComparer.Ordinal);
            HashSet<string> alreadyInStore = new(StringComparer.Ordinal);
            int skippedNoOracleId = 0;

            foreach ((Card card, string _) in matched)
            {
                if (string.IsNullOrEmpty(card.OracleId))
                {
                    skippedNoOracleId++; // cannot key an entry without an oracle_id
                    continue;
                }

                if (metadata.Cards.ContainsKey(card.OracleId))
                {
                    // A second file of an oracle added a moment ago is not "already
                    // in the store": the first file wins and this one is ignored.
                    if (!addedIds.Contains(card.OracleId))
                    {
                        alreadyInStore.Add(card.OracleId);
                    }

                    continue;
                }

                metadata.Cards[card.OracleId] = new CardMetadataEntry
                {
                    Name = card.Name,
                    Rating = 0,
                    ScryfallId = card.Id,
                };
                addedIds.Add(card.OracleId);
                addedNames.Add(card.Name);
            }

            return new RegisterResult(addedNames, alreadyInStore.Count, skippedNoOracleId);
        }
    }

    /// <summary>What one <see cref="RegisterCommand.RegisterNew"/> call did.</summary>
    /// <param name="AddedNames">Names of the entries added, in match order.</param>
    /// <param name="AlreadyInStore">Distinct oracle ids that were already in the store
    /// before the call.</param>
    /// <param name="SkippedNoOracleId">Matched files whose card has no oracle id.</param>
    internal sealed record RegisterResult(IReadOnlyList<string> AddedNames, int AlreadyInStore, int SkippedNoOracleId)
    {
        internal int Added => AddedNames.Count;
    }
}
