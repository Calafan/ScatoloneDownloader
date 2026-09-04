using System.Diagnostics;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
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
        }

        internal static Task RunAllAsync(string? excludeFile, bool reprints, bool tokens, bool lands, bool printOnly)
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

        internal static Task RunYearsAsync(IEnumerable<int> years, bool reprints, bool tokens, bool lands, bool printOnly)
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

            List<Card> cards = await (req.Mode switch
            {
                Mode.All => string.IsNullOrEmpty(req.File) ? getManager.GetUniqueArtwork() : getManager.GetUniqueArtwork(req.File),
                Mode.Set => getManager.GetSet(Required(req.Set, req.Mode, "set code")),
                Mode.Years => getManager.GetYears(Required(req.Years, req.Mode, "years")),
                Mode.Files => getManager.GetCardList(Required(req.File, req.Mode, "list file"), req.Lands),
                Mode.Lands => getManager.GetUniqueArtwork(),
                _ => throw new ArgumentOutOfRangeException(nameof(req)),
            });

            if (req.Mode == Mode.Lands)
            {
                AnsiConsole.MarkupLine("Validating basic lands.");
                cards = CardFilter.ValidateBasicLands(cards);
            }
            else if (req.Mode != Mode.Files)
            {
                AnsiConsole.MarkupLine("Validating cards.");
                cards = CardFilter.Validate(cards, req.Reprints, req.Tokens, req.Lands);
            }

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
                await DownloadAllAsync(downloader, cards, req.Mode, req.File, specificText);
            }
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

        private static async Task DownloadAllAsync(CardDownloader downloader, List<Card> cards, Mode mode, string? file, string specificText)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(), new ElapsedTimeColumn())
                .StartAsync(async ctx =>
                {
                    ProgressTask task = ctx.AddTask($"Downloading {specificText}", maxValue: cards.Count);

                    foreach (Card card in cards)
                    {
                        await downloader.DownloadAsync(card, mode, file);
                        task.Increment(1);
                    }
                });

            stopwatch.Stop();

            ReportThroughput(cards.Count, stopwatch.Elapsed);
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
