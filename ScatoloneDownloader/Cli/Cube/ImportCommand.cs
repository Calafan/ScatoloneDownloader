using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// One-time seed command: migrates existing Adobe Bridge XMP ratings/labels
    /// into the git-tracked metadata directory (see <see cref="CubeMetadataStore"/>)
    /// so the git snapshot becomes complete and Bridge can be retired. This is
    /// the ONLY place XMP is read going forward (via <see cref="XmpManager"/>)
    /// — after running <c>import</c> once, the web tagger is authoritative and
    /// this command is rarely needed again.
    /// </summary>
    internal sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png) with XMP metadata.")]
            public string SourceDirectory { get; set; }

            internal override string MasterDirectory => SourceDirectory;

            [CommandOption("--overwrite")]
            [Description("Refresh label (and a REAL, non-zero XMP rating) on entries that already exist; off by default (XMP only fills entries that have none yet). Never demotes a stored rating to 0 and never repoints scryfallId unless it is empty.")]
            public bool Overwrite { get; set; }
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

            AnsiConsole.MarkupLine($"[yellow]Found {pngFiles.Length} files. Loading bulk data from Scryfall...[/]");

            List<(Card Card, string FilePath)> matched;

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();
                matched = CardImageMatcher.Match(allCards, pngFiles, warnUnmatched: true);
            }

            AnsiConsole.MarkupLine($"[green]Matched {matched.Count} cards.[/]");
            if (matched.Count == 0) return 0;

            // The only place XMP is still read: a one-time seed into the metadata.
            // SeedFromXmp reads each file's XMP once and reduces to one card per
            // oracle_id, so reprints that normalize to the same name don't clobber
            // each other last-file-wins (#8).
            matched = SeedFromXmp(matched);

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);

            int added = 0;
            int updated = 0;

            foreach (var (card, _) in matched)
            {
                if (string.IsNullOrEmpty(card.OracleId))
                {
                    continue; // cannot key an entry without an oracle_id
                }

                bool isNew = !metadata.Cards.TryGetValue(card.OracleId, out CardMetadataEntry existing);
                CardMetadataEntry entry = isNew ? new CardMetadataEntry() : existing;

                ApplyImportSeed(entry, card, isNew, settings.Overwrite);

                metadata.Cards[card.OracleId] = entry;

                if (isNew)
                {
                    added++;
                }
                else
                {
                    updated++;
                }
            }

            CubeMetadataStore.Save(metadataDir, metadata);

            AnsiConsole.MarkupLine($"[green]Import complete:[/] {added} added, {updated} updated. Saved to {metadataDir}.");

            return 0;
        }

        /// <summary>Reads each matched file's XMP once, then reduces to one card
        /// per oracle_id via <see cref="ReduceByOracle"/>. Separated so the pure
        /// reduction is unit-testable without Magick.NET / real image files.</summary>
        private static List<(Card Card, string FilePath)> SeedFromXmp(List<(Card Card, string FilePath)> matched)
        {
            List<(Card Card, string FilePath, int Rating, string Label)> perFile = [];

            // Every file is opened through Magick.NET to pull its XMP chunk, so on
            // a full library this is minutes of work — by far the longest pass in
            // the command. Report it the same way view generation does, otherwise
            // it looks indistinguishable from a hang.
            AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                })
                .Start(ctx =>
                {
                    ProgressTask task = ctx.AddTask(
                        $"[yellow]Reading XMP rating/label... {ProgressLabel.Counter(0, matched.Count)}[/]",
                        maxValue: matched.Count);

                    foreach (var (card, filePath) in matched)
                    {
                        task.Description = $"[yellow]Reading XMP rating/label... [cyan]{ProgressLabel.Counter(task.Value, matched.Count)}[/][/]";

                        int rating = 0;
                        string label = string.Empty;
                        if (File.Exists(filePath))
                        {
                            (rating, label) = XmpManager.ReadMetadata(filePath);
                            label ??= string.Empty;
                        }

                        perFile.Add((card, filePath, rating, label));
                        task.Increment(1);
                    }

                    task.Description = $"[green]XMP read complete! [cyan]{ProgressLabel.Counter(matched.Count, matched.Count)}[/][/]";
                });

            return ReduceByOracle(perFile);
        }

        /// <summary>
        /// Reduces per-file XMP reads to ONE tuple per <see cref="Card.OracleId"/>
        /// and stamps the winning rating/label onto each card. The Scryfall bulk
        /// gives one shared <see cref="Card"/> object per name, so a card printed
        /// in several sets (reprints) yields multiple files pointing at the SAME
        /// Card; seeding that object per file would be last-file-wins. Instead, for
        /// each oracle_id keep the file with the highest XMP rating (tiebreak: one
        /// that carries a color label), so the strongest existing evaluation seeds
        /// the entry. Cards with no oracle_id can't be keyed — they pass through
        /// unchanged (and are skipped later when the entry is written).
        /// </summary>
        internal static List<(Card Card, string FilePath)> ReduceByOracle(
            IEnumerable<(Card Card, string FilePath, int Rating, string Label)> perFile)
        {
            Dictionary<string, (Card Card, string FilePath, int Rating, string Label)> best = new();
            List<(Card Card, string FilePath)> passthrough = [];

            foreach (var (card, filePath, rating, label) in perFile)
            {
                string safeLabel = label ?? string.Empty;

                if (string.IsNullOrEmpty(card.OracleId))
                {
                    card.Rating = rating;
                    card.XmpLabel = safeLabel;
                    passthrough.Add((card, filePath));
                    continue;
                }

                bool better = !best.TryGetValue(card.OracleId, out var current)
                    || rating > current.Rating
                    || (rating == current.Rating
                        && string.IsNullOrEmpty(current.Label)
                        && !string.IsNullOrEmpty(safeLabel));

                if (better)
                {
                    best[card.OracleId] = (card, filePath, rating, safeLabel);
                }
            }

            List<(Card Card, string FilePath)> result = [];
            foreach (var (card, filePath, rating, label) in best.Values)
            {
                card.Rating = rating;
                card.XmpLabel = label;
                result.Add((card, filePath));
            }
            result.AddRange(passthrough);
            return result;
        }

        /// <summary>
        /// Applies the XMP seed onto one metadata entry with the import gating
        /// rules. <paramref name="isNew"/> entries are fully seeded; existing
        /// entries are protected against clobbering tagger-authored work:
        /// <list type="bullet">
        /// <item><description><c>scryfallId</c> is set only when new, empty, or
        /// <paramref name="overwrite"/> — never repoints a tagger-pinned printing
        /// otherwise (#6).</description></item>
        /// <item><description><c>rating</c> is refreshed on <paramref name="overwrite"/>
        /// only from a REAL XMP rating (&gt;0), so an XMP 0 never demotes a
        /// tagger-authored pool rating to unrated (#7).</description></item>
        /// <item><description><c>status</c> is only default-filled when absent —
        /// an explicit tagger status is never touched, even with
        /// <paramref name="overwrite"/>.</description></item>
        /// <item><description><c>effects</c> and <c>reviewedAt</c> are never
        /// touched — they belong to the tagger.</description></item>
        /// </list>
        /// </summary>
        internal static void ApplyImportSeed(CardMetadataEntry entry, Card card, bool isNew, bool overwrite)
        {
            entry.Name = card.Name;

            if (isNew || string.IsNullOrEmpty(entry.ScryfallId) || overwrite)
            {
                entry.ScryfallId = card.Id;
            }

            if (isNew || overwrite)
            {
                entry.Label = card.XmpLabel ?? string.Empty;
            }

            if (isNew)
            {
                entry.Rating = card.Rating;
            }
            else if (overwrite && card.Rating > 0)
            {
                entry.Rating = card.Rating;
            }

            if (string.IsNullOrEmpty(entry.Status))
            {
                CardStatus defaultStatus = StatusResolver.FromXmpLabel(card.XmpLabel);
                if (defaultStatus != CardStatus.None)
                {
                    entry.StatusValue = defaultStatus;
                }
            }
        }
    }
}
