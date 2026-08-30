using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
    /// <summary>
    /// Closes the disaster-recovery loop: rebuilds an image folder from nothing
    /// but the git-tracked metadata directory (see <see cref="CubeMetadataStore"/>
    /// — <see cref="CubeMetadataStore.Load"/> merges the UNION of all rating-tier
    /// files, so every card in the library is restored, not just the pool) and
    /// the Scryfall bulk download. No XMP is written — rating/status/effects live
    /// only in the metadata and are edited in the web tagger, never on the images
    /// themselves. Idempotent: existing files are left untouched.
    /// </summary>
    internal sealed class RestoreCommand : AsyncCommand<RestoreCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-i|--images <DIR>")]
            [Description("Destination folder for the restored .png images (created if missing).")]
            public string ImagesDirectory { get; set; }

            [CommandOption("-m|--metadata")]
            [Description("Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). Defaults to ./metadata.")]
            public string MetadataDirectory { get; set; }

            public override ValidationResult Validate()
            {
                if (string.IsNullOrWhiteSpace(ImagesDirectory))
                {
                    return ValidationResult.Error("--images <DIR> is required.");
                }

                return ValidationResult.Success();
            }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string imagesDir = Path.GetFullPath(settings.ImagesDirectory);
            string metadataDir = string.IsNullOrWhiteSpace(settings.MetadataDirectory)
                ? Path.GetFullPath("metadata")
                : Path.GetFullPath(settings.MetadataDirectory);

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);
            if (metadata.Cards.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No entries found in '{metadataDir}'. Nothing to restore.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[cyan]Metadata  :[/] {metadataDir} ({metadata.Cards.Count} entries)");
            AnsiConsole.MarkupLine($"[cyan]Images dir:[/] {imagesDir}");

            Directory.CreateDirectory(imagesDir);

            AnsiConsole.MarkupLine("[yellow]Loading bulk data from Scryfall...[/]");

            int downloaded = 0;
            int alreadyPresent = 0;
            int unresolved = 0;

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();

                // Index by printing id (scryfallId), and a fallback index by
                // oracle_id for entries saved before scryfallId existed. Printing
                // ids are unique; for the oracle_id fallback the first printing
                // encountered in the bulk download wins (matches the "first
                // matched printing wins" assumption used when entries are created).
                Dictionary<string, Card> cardsById = new();
                Dictionary<string, Card> cardsByOracleId = new();
                foreach (Card card in allCards)
                {
                    if (!string.IsNullOrEmpty(card.Id))
                    {
                        cardsById.TryAdd(card.Id, card);
                    }

                    if (!string.IsNullOrEmpty(card.OracleId))
                    {
                        cardsByOracleId.TryAdd(card.OracleId, card);
                    }
                }

                CardDownloader downloader = new(manager);

                foreach (var (oracleId, entry) in metadata.Cards)
                {
                    Card card = null;
                    if (!string.IsNullOrEmpty(entry.ScryfallId))
                    {
                        cardsById.TryGetValue(entry.ScryfallId, out card);
                    }

                    card ??= cardsByOracleId.GetValueOrDefault(oracleId);

                    if (card == null)
                    {
                        unresolved++;
                        AnsiConsole.MarkupLine($"[red]Warning:[/] no Scryfall printing found for '{entry.Name}' (oracle_id {oracleId}).");
                        continue;
                    }

                    string destPath = Path.Combine(imagesDir, OutputPaths.Sanitize(card.Name) + ".png");

                    if (File.Exists(destPath))
                    {
                        alreadyPresent++;
                        continue;
                    }

                    byte[] png = await downloader.ComposeAsync(card);
                    await File.WriteAllBytesAsync(destPath, png);
                    downloaded++;
                }
            }

            AnsiConsole.MarkupLine(
                $"[green]Restore complete:[/] {downloaded} downloaded, {alreadyPresent} already present, " +
                (unresolved > 0 ? $"[red]{unresolved} unresolved[/]." : "0 unresolved."));

            return 0;
        }
    }
}
