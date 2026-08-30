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

namespace ScatoloneDownloader.Cli
{
    /// <summary>
    /// One-time seed command: migrates existing Adobe Bridge XMP ratings/labels
    /// into the git-tracked metadata directory (see <see cref="CubeMetadataStore"/>)
    /// so the git snapshot becomes complete and Bridge can be retired. This is
    /// the ONLY place XMP is read going forward (see <see cref="MetadataSynchronizer"/>)
    /// — after running <c>import</c> once, the web tagger is authoritative and
    /// this command is rarely needed again.
    /// </summary>
    internal sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png) with XMP metadata.")]
            public string SourceDirectory { get; set; }

            [CommandOption("-m|--metadata")]
            [Description("Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). Defaults to ./metadata.")]
            public string MetadataDirectory { get; set; }

            [CommandOption("--overwrite")]
            [Description("Overwrite rating/label already present in the metadata with the XMP value (off by default: XMP only fills entries that have none yet).")]
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
            string metadataDir = string.IsNullOrWhiteSpace(settings.MetadataDirectory)
                ? Path.GetFullPath("metadata")
                : Path.GetFullPath(settings.MetadataDirectory);

            string[] pngFiles = Directory.GetFiles(masterDir, "*.png", SearchOption.AllDirectories);
            if (pngFiles.Length == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No .png files found in '{masterDir}'.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]Found {pngFiles.Length} files. Loading bulk data from Scryfall...[/]");

            List<(Card Card, string FilePath)> matched = [];

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
                    string cardName = CardNameNormalizer.Normalize(Path.GetFileNameWithoutExtension(file));
                    if (cardsByName.TryGetValue(cardName, out Card card))
                    {
                        matched.Add((card, file));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Warning:[/] no Scryfall card found for file '{Path.GetFileName(file)}' (searched name: '{cardName}')");
                    }
                }
            }

            AnsiConsole.MarkupLine($"[green]Matched {matched.Count} cards.[/]");
            if (matched.Count == 0) return 0;

            // The only place XMP is still read: a one-time seed into the metadata.
            AnsiConsole.MarkupLine("[yellow]Reading XMP rating/label from disk...[/]");
            MetadataSynchronizer.SyncCardsFromDisk(matched);

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

                entry.Name = card.Name;
                entry.ScryfallId = card.Id;

                // Rating/label: fill on a brand-new entry, or always when
                // --overwrite; otherwise leave the tagger-authored value alone.
                if (isNew || settings.Overwrite)
                {
                    entry.Rating = card.Rating;
                    entry.Label = card.XmpLabel ?? string.Empty;
                }

                // Status default: only fill when the JSON has no status yet — an
                // explicit status (set by the tagger) is never touched by import,
                // regardless of --overwrite (P1).
                if (string.IsNullOrEmpty(entry.Status))
                {
                    CardStatus defaultStatus = StatusResolver.FromXmpLabel(card.XmpLabel);
                    if (defaultStatus != CardStatus.None)
                    {
                        entry.StatusValue = defaultStatus;
                    }
                }

                // Effects and ReviewedAt are never touched here — they belong to
                // the tagger, and import must not clobber manual review work.
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
    }
}
