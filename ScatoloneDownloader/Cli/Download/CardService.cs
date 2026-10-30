using System.Diagnostics;

using Microsoft.Extensions.Logging;

using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Logging;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Scryfall;

using Spectre.Console;

namespace ScatoloneDownloader.Cli.Download
{
    /// <summary>
    /// Orchestrates a single card-download run: the subcommands call the
    /// mode-specific entry points below, each of which builds a
    /// <see cref="CardRequest"/> and hands it to <see cref="GetCardsAsync"/>.
    /// </summary>
    internal static class CardService
    {
        internal const int MinYear = 1993;
        internal const int MaxYear = 2050;

        private static readonly ILogger Logger = AppLogger.CreateLogger(nameof(CardService));

        /// <summary>
        /// One card-download request: each entry point sets only the fields its
        /// mode needs and leaves the rest at their defaults.
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

            internal bool PrintOnly { get; init; }

            /// <summary>Skip the library rules (Mode.All and Mode.Years only).</summary>
            internal bool NoRules { get; init; }

            /// <summary>The library's <c>Source</c> folder to copy the cards it holds from
            /// (Mode.Files only); null downloads every card.</summary>
            internal string? LibrarySource { get; init; }
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
                PrintOnly = printOnly,
            });
        }

        internal static async Task RunFilesAsync(IEnumerable<string> files, bool lands, bool printOnly, string? librarySource)
        {
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    await GetCardsAsync(new CardRequest
                    {
                        Mode = Mode.Files,
                        Lands = lands,
                        File = file,
                        PrintOnly = printOnly,
                        LibrarySource = librarySource,
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

            if (req.PrintOnly)
            {
                AnsiConsole.MarkupLine("Writing list.");

                foreach (Card card in cards)
                {
                    CardDownloader.WriteToList(card);
                }
            }
            else if (req.LibrarySource is not null)
            {
                await CopyFromLibraryAsync(getManager, downloader, req, cards, specificText);
            }
            else
            {
                await DownloadAllAsync(downloader, cards, req.Mode, req.File, specificText, skipExisting);
            }
        }

        /// <summary>
        /// Exports a list from the library: the cards <c>Source\</c> already holds are
        /// copied, the rest go through <see cref="DownloadAllAsync"/> unchanged. The
        /// export therefore carries the LIBRARY's printing, the image the user rated in
        /// the tagger, not the canonical-artwork printing the name index picks. The
        /// files are matched exactly as <c>sync</c> matches them, so a card is found
        /// here whenever the store would find it there.
        /// </summary>
        private static async Task CopyFromLibraryAsync(
            GetManager getManager, CardDownloader downloader, CardRequest req, List<Card> cards, string specificText)
        {
            string source = req.LibrarySource!;

            // The same bulk the name index was built from: GetDefaultCards keeps it.
            List<Card> bulk = await getManager.GetDefaultCards();
            List<Card> selected = LibrarySelector.Select(bulk, reprints: false, tokens: false, lands: false, releasedBy: DateTime.Today);

            // The rules' picks first, as CardImageMatcher.LibraryPicksFirst orders them.
            List<(Card Card, string FilePath)> matched = CardImageMatcher.Match(
                [.. selected, .. bulk], Directory.GetFiles(source, "*.png", SearchOption.AllDirectories), warnUnmatched: false);

            HashSet<string> selectedOracleIds = new(
                selected.Select(card => card.OracleId).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);

            LibraryCopySplit split = LibraryCopy.Split(cards, LibraryCopy.FileByOracleId(matched), selectedOracleIds);

            int copied = 0;
            List<(string Name, string Message)> failures = [];

            foreach ((Card card, string sourceFile) in split.ToCopy)
            {
                try
                {
                    string target = Path.Combine(
                        OutputPaths.BuildCardDirectory(card, req.Mode, req.File), OutputPaths.Sanitize(card.Name) + ".png");

                    LibraryCopy.Copy(sourceFile, target);
                    copied++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.LogError(ex, "Copying {Name} from the library failed", card.Name);
                    failures.Add((card.Name, ex.Message));
                }
            }

            AnsiConsole.MarkupLineInterpolated($"Copied {copied} cards from the library ({source}).");

            foreach ((string name, string message) in failures)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Failed:[/] {name}: {message}");
            }

            try
            {
                if (split.ToDownload.Count > 0)
                {
                    await DownloadAllAsync(downloader, [.. split.ToDownload], req.Mode, req.File, specificText, skipExisting: false);
                }
            }
            finally
            {
                // After the download tally, and also when a 429 ends the run: the
                // list is what the user acts on next.
                SyncCommand.ListLines(
                    [.. split.SyncWouldBring.Select(card => card.Name)], "Missing from Source, `sync` would bring them:");
            }
        }

        /// <summary>Fetches the raw cards for a request and, when the library rules are on,
        /// selects them (globally, then by year for Mode.Years). Without the rules the
        /// caller validates the returned list.</summary>
        private static async Task<List<Card>> FetchCardsAsync(GetManager getManager, CardRequest req, bool libraryRules)
        {
            if (libraryRules && req.Mode == Mode.All)
            {
                List<Card> selected = LibrarySelector.Select(await getManager.GetDefaultCards(), req.Reprints, req.Tokens, req.Lands, DateTime.Today);

                return string.IsNullOrEmpty(req.File)
                    ? selected
                    : await GetManager.ExcludeListedNamesAsync(selected, req.File);
            }

            if (libraryRules && req.Mode == Mode.Years)
            {
                AnsiConsole.MarkupLine("Using the Default Cards bulk to apply the library rules.");

                List<Card> selected = LibrarySelector.Select(await getManager.GetDefaultCards(), req.Reprints, req.Tokens, req.Lands, DateTime.Today);

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

        /// <summary>What <see cref="DownloadEachAsync"/> did with a list of cards.</summary>
        internal sealed class DownloadTally
        {
            internal int Downloaded { get; set; }

            internal int Skipped { get; set; }

            internal List<(string Name, string Set, string Message)> Failures { get; } = [];

            /// <summary>The card Scryfall refused with a 429 that outlived every retry:
            /// the run stopped there, and it and every card after it were not attempted.</summary>
            internal Card? StoppedAt { get; set; }

            internal ScryfallRateLimitException? RateLimit { get; set; }
        }

        internal static async Task DownloadAllAsync(CardDownloader downloader, List<Card> cards, Mode mode, string? file, string specificText, bool skipExisting)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            DownloadTally tally = await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(), new ElapsedTimeColumn())
                .StartAsync(ctx =>
                {
                    ProgressTask task = ctx.AddTask($"Downloading {specificText}", maxValue: cards.Count);

                    return DownloadEachAsync(
                        cards, card => downloader.DownloadAsync(card, mode, file, skipExisting), () => task.Increment(1));
                });

            stopwatch.Stop();

            ReportTally(tally, cards.Count, skipExisting, stopwatch.Elapsed);
        }

        /// <summary>
        /// Prints how the run went and, when Scryfall stopped it with a 429, how far it
        /// got, then rethrows that 429 so the command ends in an error.
        /// </summary>
        internal static void ReportTally(DownloadTally tally, int cardCount, bool skipExisting, TimeSpan elapsed)
        {
            ReportThroughput(tally.Downloaded, elapsed);

            AnsiConsole.MarkupLineInterpolated(
                $"Downloaded {tally.Downloaded}, skipped {tally.Skipped} (already present), failed {tally.Failures.Count}.");

            foreach ((string name, string set, string message) in tally.Failures)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Failed:[/] {name} ({set}): {message}");
            }

            if (tally.RateLimit is { } rateLimit)
            {
                int notAttempted = cardCount - tally.Downloaded - tally.Skipped - tally.Failures.Count;

                AnsiConsole.MarkupLineInterpolated(
                    $"[red]Stopped:[/] Scryfall still answered 429 after every retry at {tally.StoppedAt!.Name} ({tally.StoppedAt.Set}); {notAttempted} cards not attempted, that one included.");

                if (skipExisting)
                {
                    AnsiConsole.MarkupLine("Wait a while, then run the same command again: it skips the images already present.");
                }

                throw rateLimit;
            }
        }

        /// <summary>
        /// Downloads the cards one by one. A failure is logged and recorded against its
        /// card and the loop goes on, except a <see cref="ScryfallRateLimitException"/>:
        /// that stops the loop at the card it came from.
        /// </summary>
        internal static async Task<DownloadTally> DownloadEachAsync(IReadOnlyList<Card> cards, Func<Card, Task<DownloadOutcome>> download, Action afterEach)
        {
            DownloadTally tally = new();

            foreach (Card card in cards)
            {
                try
                {
                    DownloadOutcome outcome = await download(card);

                    if (outcome == DownloadOutcome.Downloaded)
                    {
                        tally.Downloaded++;
                    }
                    else
                    {
                        tally.Skipped++;
                    }
                }
                catch (ScryfallRateLimitException ex)
                {
                    // Scryfall has asked us to stop: the next card would be refused too,
                    // and pressing on is what gets an IP banned.
                    Logger.LogError(ex, "Rate limited, run stopped: {Name} - {Set}", card.Name, card.Set);
                    tally.StoppedAt = card;
                    tally.RateLimit = ex;
                    break;
                }
                catch (Exception ex)
                {
                    // One bad card (a missing image, a flaky response) must not abort the run.
                    // No token reaches the download, so an OperationCanceledException here is
                    // an HTTP timeout (TaskCanceledException), not a cancel: caught like the rest.
                    Logger.LogError(ex, "Download failed: {Name} - {Set}", card.Name, card.Set);
                    tally.Failures.Add((card.Name, card.Set, ex.Message));
                }

                afterEach();
            }

            return tally;
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
                    return req.NoRules ? "Unique Artworks" : "library-rules";
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
