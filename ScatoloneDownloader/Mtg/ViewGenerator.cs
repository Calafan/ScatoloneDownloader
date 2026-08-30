using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using ScatoloneDownloader.Download;

using Spectre.Console;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Builds the multi-root cube view tree from rating/status/effects loaded
    /// (by <see cref="MetadataJsonSynchronizer"/>) from the metadata directory.
    /// Every card is linked (never copied) into zero or more folders — see
    /// <see cref="BuildTargets"/> for the exact root/exclusion rules (Plan
    /// decisions D7, P6, P7, P8, B4).
    /// </summary>
    internal static class ViewGenerator
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        /// <summary>
        /// Rebuilds the entire view tree under <paramref name="viewsRootDirectory"/>
        /// from scratch (the directory is deleted first, so this is always a clean
        /// regenerate, never an incremental patch — cheap because links, not
        /// copies, are what's being (re)created). Delegates the per-card
        /// root/exclusion decisions to <see cref="BuildTargets"/> and reports a
        /// summary line: links created, cards excluded by D7 (rating 1-2), and any
        /// link failures (e.g. filesystem doesn't support symlinks or hard links).
        /// </summary>
        internal static void GenerateViews(IEnumerable<(Card Card, string FilePath)> cardFiles, string viewsRootDirectory)
        {
            if (Directory.Exists(viewsRootDirectory))
            {
                Directory.Delete(viewsRootDirectory, true);
            }
            Directory.CreateDirectory(viewsRootDirectory);

            var fileList = cardFiles as IList<(Card Card, string FilePath)> ?? cardFiles.ToList();
            int totalFiles = fileList.Count;

            if (totalFiles == 0) return;

            int failCount = 0;
            int successCount = 0;
            int createdLinks = 0;
            int ratingExcludedCount = 0;

            AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),    // Shows both the text and the numeric counter
                    new ProgressBarColumn(),        // Visual completion bar
                    new PercentageColumn(),         // Percentage %
                    new SpinnerColumn(),            // Animated spinner
                })
                .Start(ctx =>
                {
                    // Initialize the task with the counter at 0.
                    var task = ctx.AddTask($"[yellow]Generating views... [0/{totalFiles}][/]", maxValue: totalFiles);

                    foreach (var (card, sourcePath) in fileList)
                    {
                        // Dynamically update the description to show exact progress.
                        task.Description = $"[yellow]Generating views... [cyan][{task.Value}/{totalFiles}][/][/]";

                        if (!File.Exists(sourcePath))
                        {
                            task.Increment(1);
                            continue;
                        }

                        string fileName = Path.GetFileName(sourcePath);
                        List<string> targets = BuildTargets(card, viewsRootDirectory);

                        if (targets.Count == 0)
                        {
                            // D7: rating 1-2 is deliberately excluded from every root.
                            ratingExcludedCount++;
                            task.Increment(1);
                            continue;
                        }

                        bool cardOk = true;
                        foreach (string target in targets)
                        {
                            if (CreateLink(sourcePath, target, fileName))
                            {
                                createdLinks++;
                            }
                            else
                            {
                                cardOk = false;
                            }
                        }

                        if (cardOk) successCount++;
                        else failCount++;

                        task.Increment(1);
                    }

                    // Once done, set the final text.
                    task.Description = $"[green]Generation complete! [cyan][{totalFiles}/{totalFiles}][/][/]";
                });

            AnsiConsole.MarkupLine($"\n[green]Successfully created {createdLinks} links for {successCount} cards.[/]");
            if (ratingExcludedCount > 0)
            {
                AnsiConsole.MarkupLine($"[grey]{ratingExcludedCount} cards with rating 1-2 excluded from every view (D7) — master folder + metadata only.[/]");
            }
            if (failCount > 0)
            {
                AnsiConsole.MarkupLine($"[red]Warning: could not create all links for {failCount} cards.[/]");
            }
        }

        /// <summary>
        /// Every view-tree folder a single card belongs in.
        /// <list type="bullet">
        /// <item><description>Any status (Banned/Token/Jolly) -> a single flat
        /// top-level folder per tag (<c>0_Banned</c>/<c>0_Token</c>/<c>0_Jolly</c>),
        /// with the card directly inside — no color/type/cost split. Checked before
        /// D7, so a status card is never dropped by the rating-1-2 exclusion.</description></item>
        /// <item><description>Normal rating 1-2 -> excluded from every view (D7).</description></item>
        /// <item><description>Normal rating 0 -> ONLY <c>0_Unrated/{Year}/{Set}</c>
        /// (B4). Kept out of the browse views so the ~26k-card library backlog can
        /// never flood and choke them.</description></item>
        /// <item><description>Normal rating 3-5 (the curated pool) -> the full
        /// multi-root browse tree; multi-linked once per effect.</description></item>
        /// </list>
        /// </summary>
        private static List<string> BuildTargets(Card card, string root)
        {
            // Status cards (Banned/Token/Jolly) go to a single flat folder per tag
            // at the top level, at ANY rating — so a tagged card never hides and is
            // not split by color/cost. Before D7, so rating 1-2 can't drop it.
            if (card.Status != CardStatus.None)
            {
                return [Path.Combine(root, "0_" + card.Status)];
            }

            // D7: normal rating 1-2 cards are excluded from every view (rarely
            // browsed; the master folder + metadata directory remain the truth).
            if (card.Rating is 1 or 2)
            {
                return [];
            }

            // "Supertipo" (P6) = MacroType: Creature/Land/OtherPermanent/Spell.
            string colorFolder = ColorCategoryClassifier.ViewFolderName(card.ColorCategory);
            string macroType = card.MacroType.ToString();
            string cmcFolder = $"Cost {CmcBucket(card.Cmc)}";

            List<string> effectNames = EffectResolver.ToNames(card.Effects);
            if (effectNames.Count == 0)
            {
                effectNames = ["_Untagged"];
            }

            // Unrated (rating 0): ONLY the year/set backlog view (B4 + #1). This is
            // the recovery-manifest backlog, mirroring the physical Source layout so
            // it can be worked through set-by-set; it is deliberately kept out of
            // every browse root (color/type isn't a useful axis for cards no one has
            // evaluated yet, and ~26k of them would choke Bridge).
            if (card.Rating == 0)
            {
                string yearFolder = card.ReleasedAt.Year.ToString(CultureInfo.InvariantCulture);
                string setFolder = OutputPaths.Sanitize(card.SetName);
                return [Path.Combine(root, "0_Unrated", yearFolder, setFolder)];
            }

            // Rating 3-5 (the curated pool): the full multi-root browse tree.
            List<string> targets = [];
            string ratingFolder = $"{card.Rating}_Stars";

            // VIEW: 2_ByRating -> {N}_Stars / Color.
            targets.Add(Path.Combine(root, "2_ByRating", ratingFolder, colorFolder));

            // VIEW 1 (both order variants, P7). Multi-link per effect; untagged
            // rated cards fall into "_Untagged".
            foreach (string effectName in effectNames)
            {
                targets.Add(Path.Combine(root, "1_Deep_Effect", colorFolder, macroType, effectName, cmcFolder, ratingFolder));
                targets.Add(Path.Combine(root, "1_Deep_Rating", colorFolder, ratingFolder, macroType, effectName, cmcFolder));
            }

            foreach (string effectName in effectNames)
            {
                targets.Add(Path.Combine(root, "3_ByEffect", effectName, colorFolder));
            }

            targets.Add(Path.Combine(root, "4_ByColor", colorFolder, macroType, cmcFolder));
            targets.Add(Path.Combine(root, "5_ByType", macroType));

            return targets;
        }

        /// <summary>CMC folder bucket: exact 0-5, "6_Plus" for 6 or more. Invariant
        /// culture so the decimal never leaks into the folder name.</summary>
        private static string CmcBucket(double cmc)
        {
            return cmc >= 6
                ? "6_Plus"
                : ((int)cmc).ToString(CultureInfo.InvariantCulture);
        }

        private static bool CreateLink(string sourcePath, string targetDirectory, string fileName)
        {
            Directory.CreateDirectory(targetDirectory);
            string targetFile = Path.Combine(targetDirectory, fileName);

            if (!File.Exists(targetFile))
            {
                try
                {
                    // 1. Try the .NET symbolic link first.
                    File.CreateSymbolicLink(targetFile, sourcePath);
                    return true;
                }
                catch (Exception)
                {
                    // 2. Fall back to the native Windows hard link.
                    try
                    {
                        bool success = CreateHardLink(targetFile, sourcePath, IntPtr.Zero);
                        return success;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
