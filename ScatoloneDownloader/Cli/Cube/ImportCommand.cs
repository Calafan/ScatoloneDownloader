using System.ComponentModel;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Folds Adobe Bridge XMP ratings/labels into the git-tracked metadata
    /// directory (see <see cref="CubeMetadataStore"/>) so the git snapshot stays
    /// complete. This is the ONLY place XMP is read (via <see cref="XmpManager"/>):
    /// the store is the authoring authority and the web tagger writes straight to
    /// it, but Bridge is still in use, so this command is meant to be re-run after
    /// every Bridge session rather than once — see <c>--incremental</c>, which
    /// rescans only the files touched since the previous run.
    /// </summary>
    internal sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png) with XMP metadata.")]
            public string SourceDirectory { get; set; } = string.Empty;

            internal override string MasterDirectory => SourceDirectory;

            [CommandOption("--overwrite")]
            [Description("Refresh label (and a REAL, non-zero XMP rating) on entries that already exist; off by default (XMP only fills entries that have none yet). Never demotes a stored rating to 0 and never repoints scryfallId unless it is empty.")]
            public bool Overwrite { get; set; }

            [CommandOption("--incremental")]
            [Description("Only read the XMP of files modified since the last import (see import-state.json). Cards not yet in the store are always read. Off by default: a full scan is the safe answer.")]
            public bool Incremental { get; set; }
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

            // Taken before any file is touched: this becomes the next run's
            // watermark, so anything written while this one is scanning is caught
            // next time rather than falling through the gap.
            DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
            DateTimeOffset? incrementalSince = settings.Incremental
                ? ImportState.Load(metadataDir).LastImportUtc
                : null;

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

            // Loaded before the XMP pass because incremental mode needs to know
            // which oracle_ids the store already knows about — a card that has
            // never been imported must be read no matter how old its file is.
            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);

            if (settings.Incremental)
            {
                int before = matched.Count;
                matched = SelectForRescan(matched, incrementalSince, [.. metadata.Cards.Keys], path => File.GetLastWriteTimeUtc(path));

                if (incrementalSince == null)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]No previous import recorded in {ImportState.FileName} — scanning all {before} files this once.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"Incremental: {matched.Count} of {before} files changed since {incrementalSince:u} ({before - matched.Count} skipped).");
                }

                if (matched.Count == 0)
                {
                    AnsiConsole.MarkupLine("[green]Nothing has changed since the last import.[/]");
                    ImportState.Save(metadataDir, startedUtc);
                    return 0;
                }
            }

            // The only place XMP is still read: a one-time seed into the metadata.
            // SeedFromXmp reads each file's XMP once and reduces to one card per
            // oracle_id, so reprints that normalize to the same name don't clobber
            // each other last-file-wins (#8).
            matched = SeedFromXmp(matched);

            int added = 0;
            int updated = 0;
            int ratingsChanged = 0;
            int labelsChanged = 0;
            int statusesFilled = 0;

            foreach ((Card card, string _) in matched)
            {
                if (string.IsNullOrEmpty(card.OracleId))
                {
                    continue; // cannot key an entry without an oracle_id
                }

                bool isNew = !metadata.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? existing);
                CardMetadataEntry entry = existing ?? new CardMetadataEntry();

                ImportSeedResult changes = ApplyImportSeed(entry, card, isNew, settings.Overwrite);

                metadata.Cards[card.OracleId] = entry;

                if (isNew)
                {
                    added++;
                }
                else
                {
                    updated++;
                }

                if (changes.RatingChanged) ratingsChanged++;
                if (changes.LabelChanged) labelsChanged++;
                if (changes.StatusFilled) statusesFilled++;
            }

            CubeMetadataStore.Save(metadataDir, metadata);

            // Stamped on every run, incremental or not, so the FIRST --incremental
            // after a plain import already has a watermark to work from.
            ImportState.Save(metadataDir, startedUtc);

            AnsiConsole.MarkupLine($"[green]Import complete:[/] {added} added, {updated} updated. Saved to {metadataDir}.");

            // "Updated" counts entries rewritten, not entries that moved: the seed
            // touches every matched card whether or not its XMP differed. Report
            // what actually changed, so importing a folder Bridge never wrote to
            // is obvious on the spot instead of looking like a successful run.
            AnsiConsole.MarkupLine(
                $"[cyan]Changed:[/] {ratingsChanged} ratings, {labelsChanged} labels, {statusesFilled} statuses.");

            // Only meaningful after a FULL scan. In incremental mode "nothing
            // changed" is the expected, healthy answer for a subset of files and
            // says nothing about whether the right folder was imported.
            if (!settings.Incremental && added == 0 && ratingsChanged == 0 && labelsChanged == 0 && statusesFilled == 0)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Nothing differed from the store.[/] [grey]If you expected a Bridge session to land here, check you pointed at the folder Bridge actually wrote to.[/]");

                if (!settings.Overwrite)
                {
                    AnsiConsole.MarkupLine(
                        "[grey]Note: without --overwrite an existing entry never takes a new XMP rating.[/]");
                }
            }

            return 0;
        }

        /// <summary>
        /// Narrows the files whose XMP is worth reading to the ones that can still
        /// carry news, for <c>--incremental</c>. The unit is the <b>oracle_id
        /// group</b>, never the single file, and that is the whole subtlety:
        /// <see cref="ReduceByOracle"/> keeps the HIGHEST rating among the printings
        /// sharing an oracle_id, so reading one changed printing while silently
        /// dropping its unchanged siblings would let a 5-star printing vanish from
        /// the comparison and quietly demote the card. If any file in a group
        /// changed, every file in that group is re-read.
        /// <para>
        /// A group whose oracle_id the store has never seen is always read,
        /// whatever its timestamps: images that arrived through <c>restore</c> can
        /// carry a modification time older than the last import and would otherwise
        /// never be picked up. A file with no oracle_id cannot be grouped or keyed,
        /// so it is always read too. Anything whose timestamp cannot be read is
        /// treated as changed — this filter may only ever do redundant work, never
        /// skip something it should have looked at.
        /// </para>
        /// </summary>
        /// <param name="since">Watermark; <c>null</c> (no import recorded yet) selects everything.</param>
        /// <param name="knownOracleIds">oracle_ids already present in the store.</param>
        /// <param name="lastWriteUtc">Injected for testability — <see cref="File.GetLastWriteTimeUtc"/> in production.</param>
        internal static List<(Card Card, string FilePath)> SelectForRescan(
            IEnumerable<(Card Card, string FilePath)> matched,
            DateTimeOffset? since,
            HashSet<string> knownOracleIds,
            Func<string, DateTimeOffset> lastWriteUtc)
        {
            if (since == null)
            {
                return [.. matched];
            }

            List<(Card Card, string FilePath)> ungrouped = [];
            Dictionary<string, List<(Card Card, string FilePath)>> groups = [];

            foreach ((Card card, string filePath) in matched)
            {
                if (string.IsNullOrEmpty(card.OracleId))
                {
                    ungrouped.Add((card, filePath));
                    continue;
                }

                if (!groups.TryGetValue(card.OracleId, out List<(Card, string)>? group))
                {
                    group = [];
                    groups[card.OracleId] = group;
                }

                group.Add((card, filePath));
            }

            List<(Card Card, string FilePath)> selected = [.. ungrouped];

            foreach ((string oracleId, List<(Card Card, string FilePath)> group) in groups)
            {
                bool mustRead = !knownOracleIds.Contains(oracleId)
                    || group.Any(file => ChangedSince(file.FilePath, since.Value, lastWriteUtc));

                if (mustRead)
                {
                    selected.AddRange(group);
                }
            }

            return selected;
        }

        /// <summary>Whether one file counts as touched since the watermark. An
        /// unreadable timestamp answers "yes": this filter is only ever allowed to
        /// err towards extra work.</summary>
        private static bool ChangedSince(string filePath, DateTimeOffset since, Func<string, DateTimeOffset> lastWriteUtc)
        {
            try
            {
                return lastWriteUtc(filePath) >= since;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>Reads each matched file's XMP once, then reduces to one card
        /// per oracle_id via <see cref="ReduceByOracle"/>. Separated so the pure
        /// reduction is unit-testable without Magick.NET / real image files.</summary>
        private static List<(Card Card, string FilePath)> SeedFromXmp(List<(Card Card, string FilePath)> matched)
        {
            List<(Card Card, string FilePath, int Rating, string Label)> perFile = [];

            // Every file's XMP is read here. XmpManager walks the PNG chunk table
            // rather than decoding the image, so a full 30k library is seconds of
            // CPU rather than the ~17 minutes the decode used to cost — but a cold
            // file cache still puts a disk-bound wait in front of the user, so keep
            // reporting progress the way view generation does.
            AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                [
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                ])
                .Start(ctx =>
                {
                    ProgressTask task = ctx.AddTask(
                        $"[yellow]Reading XMP rating/label... {ProgressLabel.Counter(0, matched.Count)}[/]",
                        maxValue: matched.Count);

                    foreach ((Card card, string filePath) in matched)
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
            Dictionary<string, (Card Card, string FilePath, int Rating, string Label)> best = [];
            List<(Card Card, string FilePath)> passthrough = [];

            foreach ((Card card, string filePath, int rating, string label) in perFile)
            {
                string safeLabel = label ?? string.Empty;

                if (string.IsNullOrEmpty(card.OracleId))
                {
                    card.Rating = rating;
                    card.XmpLabel = safeLabel;
                    passthrough.Add((card, filePath));
                    continue;
                }

                bool better = !best.TryGetValue(card.OracleId, out (Card Card, string FilePath, int Rating, string Label) current)
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
            foreach ((Card card, string filePath, int rating, string label) in best.Values)
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
        internal static ImportSeedResult ApplyImportSeed(CardMetadataEntry entry, Card card, bool isNew, bool overwrite)
        {
            int previousRating = entry.Rating;
            string previousLabel = entry.Label ?? string.Empty;
            string previousStatus = entry.Status ?? string.Empty;

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

            // Default-fill the status from the Bridge color label, but only for an
            // entry no human has reviewed. `StatusResolver.ToName(None)` is null, so
            // "explicitly cleared to None in the tagger" and "never set" look
            // identical on disk; without the reviewedAt gate, a card demoted from
            // Banned back to None would be re-banned by the still-red XMP label on
            // the next import — silently undoing tagger work.
            if (string.IsNullOrEmpty(entry.Status) && entry.ReviewedAt == null)
            {
                CardStatus defaultStatus = StatusResolver.FromXmpLabel(card.XmpLabel);
                if (defaultStatus != CardStatus.None)
                {
                    entry.StatusValue = defaultStatus;
                }
            }

            return new ImportSeedResult(
                RatingChanged: entry.Rating != previousRating,
                LabelChanged: (entry.Label ?? string.Empty) != previousLabel,
                StatusFilled: (entry.Status ?? string.Empty) != previousStatus);
        }
    }

    /// <summary>
    /// What one <see cref="ImportCommand.ApplyImportSeed"/> call actually altered.
    /// Counting entries *written* says nothing — the seed rewrites every matched
    /// entry whether or not the XMP differed, so a run that imported the wrong
    /// folder still reports thousands "updated". These flags are what the summary
    /// turns into "n ratings changed", the line that tells a real Bridge session
    /// apart from a no-op.
    /// </summary>
    internal readonly record struct ImportSeedResult(bool RatingChanged, bool LabelChanged, bool StatusFilled);
}
