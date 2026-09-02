using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Mtg;

using Spectre.Console;

namespace ScatoloneDownloader.Cube
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
        /// summary line: links created, cards excluded by D7 (rating 1), and any
        /// link failures (e.g. filesystem doesn't support symlinks or hard links).
        /// </summary>
        internal static void GenerateViews(IEnumerable<(Card Card, string FilePath)> cardFiles, string viewsRootDirectory)
        {
            var fileList = cardFiles as IList<(Card Card, string FilePath)> ?? cardFiles.ToList();
            int totalFiles = fileList.Count;

            // Nothing to link — do not even touch (let alone delete) the views root.
            if (totalFiles == 0) return;

            // SAFETY GUARD: this method deletes viewsRootDirectory wholesale, so
            // refuse to run if that directory contains any source image — deleting
            // it would destroy the irreplaceable master library. (BuildViewsCommand
            // adds a stronger master-vs-views overlap check with both paths in hand;
            // this catches any direct caller / views-root-is-master mistake.)
            string viewsFull = WithSeparator(Path.GetFullPath(viewsRootDirectory));
            foreach (var (_, sourcePath) in fileList)
            {
                if (Path.GetFullPath(sourcePath).StartsWith(viewsFull, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Refusing to generate views: the views directory '{viewsRootDirectory}' contains source image " +
                        $"'{sourcePath}'. Deleting the views root would destroy master images — point --views at a separate folder.");
                }
            }

            if (Directory.Exists(viewsRootDirectory))
            {
                Directory.Delete(viewsRootDirectory, true);
            }
            Directory.CreateDirectory(viewsRootDirectory);

            // Distinct target folders created so far, so CreateLink issues one
            // CreateDirectory per folder instead of once per link (tens of thousands
            // of links collapse onto a few hundred folders).
            HashSet<string> createdDirs = new(StringComparer.OrdinalIgnoreCase);

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

                        // Isolate each card: a bad target path (e.g. MAX_PATH from
                        // deep nesting, an invalid path char) must count as a link
                        // failure and let the run finish, never unwind the loop and
                        // abort AFTER the old tree was already deleted.
                        try
                        {
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
                                if (CreateLink(sourcePath, target, fileName, createdDirs))
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
                        }
                        catch (Exception)
                        {
                            failCount++;
                        }

                        task.Increment(1);
                    }

                    // Once done, set the final text.
                    task.Description = $"[green]Generation complete! [cyan][{totalFiles}/{totalFiles}][/][/]";
                });

            AnsiConsole.MarkupLine($"\n[green]Successfully created {createdLinks} links for {successCount} cards.[/]");
            if (ratingExcludedCount > 0)
            {
                AnsiConsole.MarkupLine($"[grey]{ratingExcludedCount} cards with rating 1 excluded from every view (D7) — master folder + metadata only.[/]");
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
        /// D7, so a status card is never dropped by the rating-1 exclusion.</description></item>
        /// <item><description>Normal rating 1 -> excluded from every view (D7).</description></item>
        /// <item><description>Normal rating 2 -> ONLY <c>6_Bench/{Color}/{MacroType}/{Effect}/Cost {N}</c>,
        /// the recovery root; multi-linked once per effect.</description></item>
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

            RatingTier tier = RatingTierClassifier.Classify(card.Rating);

            // D7: normal Fringe (rating 1) cards are excluded from every view —
            // rejected outright, so the master folder + metadata directory remain
            // the only trace. Rating 2 is NOT dropped here; it falls through to
            // the recovery root below.
            if (tier == RatingTier.Fringe)
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
            if (tier == RatingTier.Unrated)
            {
                string yearFolder = card.ReleasedAt.Year.ToString(CultureInfo.InvariantCulture);
                string setFolder = OutputPaths.Sanitize(card.SetName);
                return [Path.Combine(root, "0_Unrated", yearFolder, setFolder)];
            }

            // Rating 2 (Bench): its own recovery root, laid out like 1_Deep_Effect
            // minus the trailing rating leaf (every card here is a 2). Kept out of
            // roots 1-5 for the same reason D7 exists — the pool browse tree must
            // stay the curated pool. This is where a hole gets filled from: when
            // the analysis report says the curve is top-heavy, "Cost 2" under a
            // color shows exactly what is available to promote back.
            if (tier == RatingTier.Bench)
            {
                return effectNames
                    .Select(effectName => Path.Combine(root, "6_Bench", colorFolder, macroType, effectName, cmcFolder))
                    .ToList();
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

        /// <summary>True when two directory paths are identical or one nests inside
        /// the other, comparing full normalized paths case-insensitively (Windows).
        /// Used to refuse a views root that overlaps the master library.</summary>
        internal static bool PathsOverlap(string a, string b)
        {
            string na = WithSeparator(Path.GetFullPath(a));
            string nb = WithSeparator(Path.GetFullPath(b));
            return na.StartsWith(nb, StringComparison.OrdinalIgnoreCase)
                || nb.StartsWith(na, StringComparison.OrdinalIgnoreCase);
        }

        private static string WithSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
        }

        private static bool CreateLink(string sourcePath, string targetDirectory, string fileName, HashSet<string> createdDirs)
        {
            if (createdDirs.Add(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }
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
