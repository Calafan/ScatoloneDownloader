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
        /// Every view-tree folder a single card belongs in. Empty when the card is
        /// excluded from all views (D7: rating 1-2). Banned/Token status routes
        /// exclusively to <c>0_Excluded</c> — never into the pool/effect/rating/
        /// color/type roots. Otherwise the card lands in every applicable root
        /// (P8: generate the full set); a card may be linked several times within
        /// a root when it carries multiple effects.
        /// </summary>
        private static List<string> BuildTargets(Card card, string root)
        {
            List<string> targets = [];

            // D7: never generate rating 1-2 views at all (rarely browsed; the
            // master folder + metadata directory remain the source of truth).
            if (card.Rating is 1 or 2)
            {
                return targets;
            }

            if (card.Status is CardStatus.Banned or CardStatus.Token)
            {
                targets.Add(Path.Combine(root, "0_Excluded", card.Status.ToString()));
                return targets;
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

            if (card.Rating == 0)
            {
                // VIEW: 0_Unrated -> Year / Set (B4). This is the ~26k-card
                // recovery-manifest backlog, not a curated pool, so it mirrors the
                // physical Source folder's year/expansion layout (easy to work
                // through set-by-set) instead of the color/type split every other
                // root uses — color/type isn't a useful axis for cards no one has
                // evaluated yet.
                string yearFolder = card.ReleasedAt.Year.ToString(CultureInfo.InvariantCulture);
                string setFolder = OutputPaths.Sanitize(card.SetName);
                targets.Add(Path.Combine(root, "0_Unrated", yearFolder, setFolder));
            }
            else
            {
                // Only 3/4/5 reach here (0 handled above, 1-2 excluded earlier).
                string ratingFolder = $"{card.Rating}_Stars";

                // VIEW: 2_ByRating -> {N}_Stars / Color.
                targets.Add(Path.Combine(root, "2_ByRating", ratingFolder, colorFolder));

                // VIEW 1 (both order variants, P7), rating>=3 only. Multi-link
                // per effect; untagged rated cards fall into "_Untagged".
                foreach (string effectName in effectNames)
                {
                    targets.Add(Path.Combine(root, "1_Deep_Effect", colorFolder, macroType, effectName, cmcFolder, ratingFolder));
                    targets.Add(Path.Combine(root, "1_Deep_Rating", colorFolder, ratingFolder, macroType, effectName, cmcFolder));
                }
            }

            // General organization views: not rating-gated (0/3/4/5 all included),
            // just excluded from Banned/Token (handled above) and rating 1-2 (D7).
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
