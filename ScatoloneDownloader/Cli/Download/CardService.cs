using System.Diagnostics;

using Microsoft.Extensions.Logging;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Logging;
using ScatoloneDownloader.Mtg;

using Spectre.Console;

namespace ScatoloneDownloader.Cli.Download
{
    /// <summary>
    /// Orchestrates a single download/analyze run. Replaces the old 11-parameter
    /// Program.GetCards dispatch; the subcommands call the mode-specific entry
    /// points below, each of which builds a <see cref="CardRequest"/> and hands it
    /// to <see cref="GetCardsAsync"/>.
    /// </summary>
    internal static class CardService
    {
        internal const int MinYear = 1993;
        internal const int MaxYear = 2050;

        private static readonly ILogger Logger = AppLogger.CreateLogger(nameof(CardService));

        /// <summary>
        /// One download/analyze request. Bundling the inputs replaces a
        /// 10-argument positional call where half the slots were <c>null</c>: each
        /// entry point sets only the fields its mode needs and leaves the rest at
        /// their defaults.
        /// </summary>
        private sealed record CardRequest
        {
            internal Mode Mode { get; init; }
            internal bool Reprints { get; init; }
            internal bool Tokens { get; init; }
            internal bool Lands { get; init; }

            /// <summary>Set code (Mode.Set only).</summary>
            internal string? Set { get; init; }

            /// <summary>Release years (Mode.Years only).</summary>
            internal List<int>? Years { get; init; }

            /// <summary>List file to read (Mode.Files) or exclude file (Mode.All).</summary>
            internal string? File { get; init; }

            internal bool Download { get; init; }
            internal bool Analyze { get; init; }
            internal bool PrintOnly { get; init; }

            /// <summary>Skip the library rules (Mode.All and Mode.Years only).</summary>
            internal bool NoRules { get; init; }
        }

        internal static Task RunAllAsync(string? excludeFile, bool reprints, bool tokens, bool lands, bool printOnly, bool noRules)
        {
            return GetCardsAsync(new CardRequest
            {
                Mode = Mode.All,
                Reprints = reprints,
                Tokens = tokens,
                Lands = lands,
                File = excludeFile,
                Download = true,
                PrintOnly = printOnly,
                NoRules = noRules,
            });
        }

        internal static async Task RunSetsAsync(IEnumerable<string> sets, bool reprints, bool tokens, bool lands, bool printOnly)
        {
            foreach (string set in sets)
            {
                await GetCardsAsync(new CardRequest
                {
                    Mode = Mode.Set,
                    Reprints = reprints,
                    Tokens = tokens,
                    Lands = lands,
                    Set = set,
                    Download = true,
                    PrintOnly = printOnly,
                });
            }
        }

        internal static Task RunYearsAsync(IEnumerable<int> years, bool reprints, bool tokens, bool lands, bool printOnly, bool noRules)
        {
            List<int> validYears = years.Where(year => year >= MinYear && year <= MaxYear).ToList();

            return GetCardsAsync(new CardRequest
            {
                Mode = Mode.Years,
                Reprints = reprints,
                Tokens = tokens,
                Lands = lands,
                Years = validYears,
                Download = true,
                PrintOnly = printOnly,
                NoRules = noRules,
            });
        }

        internal static Task RunLandsAsync(bool printOnly)
        {
            return GetCardsAsync(new CardRequest
            {
                Mode = Mode.Lands,
                Lands = true,
                Download = true,
                PrintOnly = printOnly,
            });
        }

        internal static async Task RunFilesAsync(IEnumerable<string> files, bool reprints, bool tokens, bool lands, bool printOnly)
        {
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    // A Files download also writes the stats file (Analyze = true), as before.
                    await GetCardsAsync(new CardRequest
                    {
                        Mode = Mode.Files,
                        Reprints = reprints,
                        Tokens = tokens,
                        Lands = lands,
                        File = file,
                        Download = true,
                        Analyze = true,
                        PrintOnly = printOnly,
                    });
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]File not found:[/] {file}");
                }
            }
        }

        internal static async Task RunAnalyzeAsync(IEnumerable<string> files, bool reprints, bool tokens, bool lands, bool printOnly)
        {
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    await GetCardsAsync(new CardRequest
                    {
                        Mode = Mode.Files,
                        Reprints = reprints,
                        Tokens = tokens,
                        Lands = lands,
                        File = file,
                        Analyze = true,
                        PrintOnly = printOnly,
                    });
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]File not found:[/] {file}");
                }
            }
        }

        private static async Task GetCardsAsync(CardRequest req)
        {
            using GetManager getManager = new();
            CardDownloader downloader = new(getManager);

            string specificText = DescribeRequest(req);

            AnsiConsole.MarkupLineInterpolated($"Getting {specificText} cards informations.");

            bool libraryRules = req.Mode is Mode.All or Mode.Years && !req.NoRules;

            List<Card> cards = await FetchCardsAsync(getManager, req, libraryRules);

            if (req.Mode == Mode.Lands)
            {
                AnsiConsole.MarkupLine("Validating basic lands.");
                cards = CardFilter.ValidateBasicLands(cards);
            }
            else if (req.Mode != Mode.Files && !libraryRules)
            {
                AnsiConsole.MarkupLine("Validating cards.");
                cards = LibrarySelector.WithFallback(cards, req.Reprints, req.Tokens, req.Lands);
            }

            // One printing per card: an existing <Name>.png means the card is already downloaded.
            bool skipExisting = libraryRules && !req.Reprints;

            if (req.Analyze)
            {
                AnsiConsole.MarkupLine("Analyzing cards.");

                Directory.CreateDirectory(OutputPaths.BasePath(Mode.Files));

                string path = Path.Combine(OutputPaths.BasePath(Mode.Files), Path.GetFileNameWithoutExtension(req.File) + "Stats.md");

                CardAnalyzer cardAnalyzer = new(cards);
                cardAnalyzer.SaveAnalysis(path);
            }

            if (req.PrintOnly)
            {
                AnsiConsole.MarkupLine("Writing list.");

                foreach (Card card in cards)
                {
                    CardDownloader.WriteToList(card);
                }
            }
            else if (req.Download)
            {
                await DownloadAllAsync(downloader, cards, req.Mode, req.File, specificText, skipExisting);
            }
        }

        /// <summary>Fetches the raw cards for a request and, when the library rules are on,
        /// selects them (globally, then by year for Mode.Years). Without the rules the
        /// caller validates the returned list.</summary>
        private static async Task<List<Card>> FetchCardsAsync(GetManager getManager, CardRequest req, bool libraryRules)
        {
            if (libraryRules && req.Mode == Mode.All)
            {
                List<Card> selected = LibrarySelector.Select(await getManager.GetDefaultCards(), req.Reprints, req.Tokens, req.Lands);

                return string.IsNullOrEmpty(req.File)
                    ? selected
                    : await GetManager.ExcludeListedNamesAsync(selected, req.File);
            }

            if (libraryRules && req.Mode == Mode.Years)
            {
                AnsiConsole.MarkupLine("Using the Default Cards bulk to apply the library rules.");

                List<Card> selected = LibrarySelector.Select(await getManager.GetDefaultCards(), req.Reprints, req.Tokens, req.Lands);

                return GetManager.FilterByYear(selected, [.. Required(req.Years, req.Mode, "years")]);
            }

            return await (req.Mode switch
            {
                Mode.All => string.IsNullOrEmpty(req.File) ? getManager.GetUniqueArtwork() : getManager.GetUniqueArtwork(req.File),
                Mode.Set => getManager.GetSet(Required(req.Set, req.Mode, "set code")),
                Mode.Years => getManager.GetYears(Required(req.Years, req.Mode, "years")),
                Mode.Files => getManager.GetCardList(Required(req.File, req.Mode, "list file"), req.Lands),
                Mode.Lands => getManager.GetUniqueArtwork(),
                _ => throw new ArgumentOutOfRangeException(nameof(req)),
            });
        }

        // Set/Years/File on a CardRequest are mode-dependent: each mode fills
        // exactly the ones it needs. This turns "the mode promised it" into a
        // checked assertion, so a malformed request fails by name here instead of
        // as a null somewhere inside GetManager.
        private static T Required<T>(T? value, Mode mode, string field)
            where T : class
        {
            return value ?? throw new InvalidOperationException($"A {mode} request carries no {field}.");
        }

        private static async Task DownloadAllAsync(CardDownloader downloader, List<Card> cards, Mode mode, string? file, string specificText, bool skipExisting)
        {
            int downloaded = 0;
            int skipped = 0;
            List<(string Name, string Set, string Message)> failures = [];

            Stopwatch stopwatch = Stopwatch.StartNew();

            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(), new ElapsedTimeColumn())
                .StartAsync(async ctx =>
                {
                    ProgressTask task = ctx.AddTask($"Downloading {specificText}", maxValue: cards.Count);

                    foreach (Card card in cards)
                    {
                        try
                        {
                            DownloadOutcome outcome = await downloader.DownloadAsync(card, mode, file, skipExisting);

                            if (outcome == DownloadOutcome.Downloaded)
                            {
                                downloaded++;
                            }
                            else
                            {
                                skipped++;
                            }
                        }
                        catch (Exception ex)
                        {
                            // One bad card (a missing image, a flaky response) must not abort the run.
                            // No token reaches the download, so an OperationCanceledException here is
                            // an HTTP timeout (TaskCanceledException), not a cancel: caught like the rest.
                            Logger.LogError(ex, "Download failed: {Name} - {Set}", card.Name, card.Set);
                            failures.Add((card.Name, card.Set, ex.Message));
                        }

                        task.Increment(1);
                    }
                });

            stopwatch.Stop();

            ReportThroughput(downloaded, stopwatch.Elapsed);

            AnsiConsole.MarkupLineInterpolated(
                $"Downloaded {downloaded}, skipped {skipped} (already present), failed {failures.Count}.");

            foreach ((string name, string set, string message) in failures)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Failed:[/] {name} ({set}): {message}");
            }
        }

        // Baseline measurement for the (currently sequential) download loop, so any
        // future parallelization can be compared against real numbers.
        private static void ReportThroughput(int cardCount, TimeSpan elapsed)
        {
            double seconds = elapsed.TotalSeconds;
            double perCardMs = cardCount > 0 ? elapsed.TotalMilliseconds / cardCount : 0;
            double cardsPerSecond = seconds > 0 ? cardCount / seconds : 0;

            AnsiConsole.MarkupLineInterpolated(
                $"Downloaded {cardCount} cards in {seconds:N1}s — {perCardMs:N0} ms/card, {cardsPerSecond:N2} cards/s.");
        }

        private static string DescribeRequest(CardRequest req)
        {
            switch (req.Mode)
            {
                case Mode.All:
                    return "Unique Artworks";
                case Mode.Set:
                    return req.Set + " set";
                case Mode.Years:
                    List<int> years = req.Years ?? [];
                    string joined = string.Join(", ", years);
                    return joined + (years.Count == 1 ? " year" : " years");
                case Mode.Files:
                    return req.File + " content";
                case Mode.Lands:
                    return "Basic Lands";
                default:
                    return string.Empty;
            }
        }
    }
}
