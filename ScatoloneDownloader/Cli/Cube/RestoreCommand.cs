using System;
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

namespace ScatoloneDownloader.Cli.Cube
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
        public sealed class Settings : MetadataSettings
        {
            [CommandOption("-i|--images <DIR>")]
            [Description("Destination folder for the restored .png images (created if missing).")]
            public string ImagesDirectory { get; set; }

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
            string metadataDir = settings.ResolveDirectory();

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
            int failed = 0;

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();

                // Index by printing id (scryfallId), and a fallback index by
                // oracle_id for entries saved before scryfallId existed. Printing
                // ids are unique; for the oracle_id fallback the first printing
                // encountered in the bulk download wins (matches the "first
                // matched printing wins" assumption used when entries are created).
                Dictionary<string, Card> cardsById = [];
                Dictionary<string, Card> cardsByOracleId = [];
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

                foreach ((string oracleId, CardMetadataEntry entry) in metadata.Cards)
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

                    try
                    {
                        // Write to a temp file then atomically move it into place, so
                        // an interrupted write (Ctrl-C, crash, disk full) never leaves
                        // a truncated .png that the skip-existing check above would
                        // then accept as "restored" forever.
                        byte[] png = await downloader.ComposeAsync(card);
                        string tempPath = destPath + ".tmp";
                        await File.WriteAllBytesAsync(tempPath, png, cancellationToken);
                        File.Move(tempPath, destPath, overwrite: true);
                        downloaded++;
                    }
                    catch (Exception ex)
                    {
                        // Isolate a single bad card (404/removed image, malformed
                        // face, network failure after retries) so it can't abort the
                        // whole disaster-recovery run and block every card after it.
                        // The .tmp is cleaned so a later re-run retries this card.
                        failed++;
                        TryDelete(destPath + ".tmp");
                        AnsiConsole.MarkupLineInterpolated($"[red]Failed:[/] {card.Name} — {ex.Message}");
                    }
                }
            }

            AnsiConsole.MarkupLine(
                $"[green]Restore complete:[/] {downloaded} downloaded, {alreadyPresent} already present, " +
                (failed > 0 ? $"[red]{failed} failed[/], " : "0 failed, ") +
                (unresolved > 0 ? $"[red]{unresolved} unresolved[/]." : "0 unresolved."));
            if (failed > 0 || unresolved > 0)
            {
                AnsiConsole.MarkupLine("[grey]Re-run restore to retry the failed/unresolved cards (already-present ones are skipped).[/]");
            }

            return 0;
        }

        /// <summary>Best-effort delete of a leftover temp file; never throws.</summary>
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore — a stray .tmp is harmless and overwritten on the next try.
            }
        }
    }
}
