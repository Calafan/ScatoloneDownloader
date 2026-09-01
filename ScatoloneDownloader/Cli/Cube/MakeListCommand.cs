using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Generates a download list file (the exact format the <c>files</c> command
    /// reads) from the metadata directory: the active pool (rating 3-5), one card
    /// name per line, alphabetical. Cards carrying a status are pulled out of the
    /// flat list into their own <c>-- Banned</c> / <c>-- Token</c> / <c>-- Jolly</c>
    /// sections and written as <c>Name -- Status</c>, so <c>files</c> downloads
    /// them into a matching sub-folder (the inline tag after <c>--</c> becomes
    /// <see cref="Card.Tag"/>, which <see cref="OutputPaths.BuildCardDirectory"/>
    /// turns into an output sub-folder). Offline: reads only the metadata JSON,
    /// no Scryfall call.
    /// </summary>
    internal sealed class MakeListCommand : AsyncCommand<MakeListCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            [CommandOption("-o|--output <FILE>")]
            [Description("Output list file to write (the format the `files` command reads).")]
            public string OutputFile { get; set; }

            public override ValidationResult Validate()
            {
                if (string.IsNullOrWhiteSpace(OutputFile))
                {
                    return ValidationResult.Error("--output <FILE> is required.");
                }

                return ValidationResult.Success();
            }
        }

        protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string metadataDir = settings.ResolveDirectory();

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);

            List<CardMetadataEntry> pool = metadata.Cards.Values
                .Where(e => RatingTierClassifier.Classify(e.Rating) == RatingTier.Pool)
                .ToList();

            if (pool.Count == 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]No pool (rating 3-5) cards in '{metadataDir}'. Nothing to write.[/]");
                return Task.FromResult(0);
            }

            string text = BuildListText(pool, metadataDir);

            string outputPath = Path.GetFullPath(settings.OutputFile);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, text);

            AnsiConsole.MarkupLineInterpolated($"[green]Wrote {pool.Count} pool cards to[/] {outputPath}");
            AnsiConsole.MarkupLineInterpolated($"[grey]Download with:[/] ScatoloneDownloader files \"{outputPath}\"");
            return Task.FromResult(0);
        }

        /// <summary>
        /// Builds the list text: a header comment (lines starting with <c>--</c>,
        /// which the reader skips), then the plain pool cards (Status == None) one
        /// name per line alphabetically, then one section per status
        /// (Banned/Token/Jolly) whose cards are written as <c>Name -- Status</c>.
        /// Pure and DETERMINISTIC (no timestamp) so a committed list produces no
        /// needless git churn between runs.
        /// </summary>
        internal static string BuildListText(IEnumerable<CardMetadataEntry> poolEntries, string sourceLabel)
        {
            List<CardMetadataEntry> entries = poolEntries.ToList();

            StringBuilder sb = new();
            sb.Append("-- Cube download list: pool (rating 3-5), ").Append(entries.Count).Append(" cards\n");
            sb.Append("-- source: ").Append(sourceLabel).Append('\n');
            sb.Append("-- feed with: ScatoloneDownloader files <this file>\n");
            sb.Append('\n');

            foreach (CardMetadataEntry entry in SortedByName(entries.Where(e => e.StatusValue == CardStatus.None)))
            {
                sb.Append(entry.Name).Append('\n');
            }

            // Status cards to their own sections, in a fixed order, tagged inline so
            // the downloader routes each into a matching sub-folder.
            foreach (CardStatus status in new[] { CardStatus.Banned, CardStatus.Token, CardStatus.Jolly })
            {
                List<CardMetadataEntry> group = SortedByName(entries.Where(e => e.StatusValue == status)).ToList();
                if (group.Count == 0)
                {
                    continue;
                }

                string statusName = StatusResolver.ToName(status);
                sb.Append('\n').Append("-- ").Append(statusName).Append('\n');
                foreach (CardMetadataEntry entry in group)
                {
                    sb.Append(entry.Name).Append(" -- ").Append(statusName).Append('\n');
                }
            }

            return sb.ToString();
        }

        private static IEnumerable<CardMetadataEntry> SortedByName(IEnumerable<CardMetadataEntry> entries)
        {
            return entries
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.Ordinal);
        }
    }
}
