using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Json.BulkData;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Json.Sets;
using ScatoloneDownloader.Logging;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Scryfall;

using Spectre.Console;

namespace ScatoloneDownloader
{
    internal sealed class GetManager : IDisposable
    {
        private const string BaseUrl = "https://api.scryfall.com/";
        private const string SetsUrl = "sets/";

        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            Converters = { new JsonCardConverter() }
        };

        private static readonly ILogger Logger = AppLogger.CreateLogger<GetManager>();

        private readonly ScryfallClient scryfallClient = new();

        // Built lazily, once per GetManager. CardService creates a fresh manager
        // for every request (see GetCardsAsync), so this never outlives one file.
        private Dictionary<string, Card>? cardsByName;

        // Takes a nullable URI because it comes straight off a Scryfall Set,
        // whose search_uri is optional on the wire; a set advertising cards
        // without one is malformed and worth saying so.
        private async Task<List<Card>> GetCardSearch(string? searchUri)
        {
            if (string.IsNullOrEmpty(searchUri))
            {
                throw new InvalidDataException("Scryfall returned a set with no search_uri.");
            }

            List<Card> cards = [];
            string? nextUri = searchUri;

            while (nextUri != null)
            {
                CardSearch page = await scryfallClient.GetFromJsonAsync<CardSearch>(nextUri, JsonSerializerOptions);

                cards.AddRange(page.Data ?? []);

                // Stop on the last page, and also when Scryfall claims another
                // page but gives no link to it.
                nextUri = page.HasMore ? page.NextPage : null;
            }

            return cards;
        }

        /// <summary>
        /// Downloads and streams a Scryfall bulk-data file by its catalog name
        /// (e.g. "Default Cards", "Unique Artwork"). Distinct from the public
        /// <see cref="GetCardList(string, bool)"/>, which reads a local download-list
        /// FILE — this one fetches the whole bulk dataset over HTTP.
        /// </summary>
        private async Task<List<Card>> FetchBulkList(string bulkDataName)
        {
            const string BulkDataUrl = "bulk-data";

            // How many streamed cards between heartbeat lines. The Default Cards
            // export runs to six figures, so this yields a handful of lines —
            // enough to prove the transfer is alive without turning the log into
            // a progress bar.
            const int BulkProgressInterval = 25000;

            string url = BaseUrl + BulkDataUrl;

            BulkDataCollection bulkDataCollection = await scryfallClient.GetFromJsonAsync<BulkDataCollection>(url);

            foreach (BulkData bulkData in bulkDataCollection.Data ?? [])
            {
                if (bulkData.Name == bulkDataName)
                {
                    // Scryfall bulk files are gzipped JSONL — one card per line — not a
                    // single JSON array anymore. Stream them line by line.
                    List<Card> cards = [];

                    // Heartbeat. Every command that touches Scryfall starts here, and
                    // until it finishes there is nothing on screen: a stalled download
                    // and a slow one used to look identical, which is exactly how a
                    // hung fetch passed for a long import. A line every
                    // BulkProgressInterval cards costs nothing and survives being
                    // piped to a file, unlike a spinner.
                    Stopwatch elapsed = Stopwatch.StartNew();

                    await foreach (Card card in scryfallClient.GetJsonLinesAsync<Card>(
                        bulkData.JsonlDownloadUri
                            ?? throw new InvalidDataException($"Bulk-data entry \"{bulkDataName}\" has no jsonl_download_uri."),
                        JsonSerializerOptions))
                    {
                        cards.Add(card);

                        if (cards.Count % BulkProgressInterval == 0)
                        {
                            AnsiConsole.MarkupLineInterpolated(
                                $"[grey]  ...{cards.Count:N0} cards ({elapsed.Elapsed.TotalSeconds:N0}s)[/]");
                        }
                    }

                    AnsiConsole.MarkupLineInterpolated(
                        $"[green]Loaded {cards.Count:N0} cards[/] [grey]in {elapsed.Elapsed.TotalSeconds:N1}s.[/]");

                    return cards;
                }
            }

            throw new KeyNotFoundException(string.Format("Unable to find \"{0}\" bulk-data file reference. Request: {1}", bulkDataName, url));
        }

        /// <summary>Builds the name-to-card index used by the list path. Returns
        /// the map instead of assigning the field, so callers hold a value the
        /// compiler knows is not null.</summary>
        private async Task<Dictionary<string, Card>> BuildCardsByName(bool downloadLands)
        {
            Dictionary<string, Card> byName = [];

            List<Card> cards = await GetDefaultCards();

            foreach (Card card in cards)
            {
                try
                {
                    if (!card.IsBasicLand)
                    {
                        string name = NextFreeName(byName, card.Name);

                        // Cards arrive in random order, but the original artwork must always keep the un-numbered name.
                        if (name != card.Name && CardFilter.IsCanonicalArtwork(card))
                        {
                            Card notFirstArtCard = byName[card.Name];

                            byName[card.Name] = card;
                            byName.Add(name, notFirstArtCard);
                        }
                        else
                        {
                            byName.Add(name, card);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Missing parameters: {Name} - {Set}", card.Name, card.Set);
                }
            }

            if (downloadLands)
            {
                cards = await GetUniqueArtwork();

                foreach (Card card in cards)
                {
                    if (card.IsBasicLand)
                    {
                        string name = NextFreeName(byName, card.Name);

                        card.Tag = "Basic Lands";

                        byName.Add(name, card);
                    }
                }
            }

            return byName;
        }

        // Cards arrive in arbitrary order and names collide across printings, so
        // each collision spills onto a numbered slot ("Name", "Name1", "Name2", ...).
        // Returns the first free slot for baseName; a returned value != baseName
        // means the un-numbered slot was already taken.
        private static string NextFreeName(Dictionary<string, Card> map, string baseName)
        {
            string name = baseName;
            int i = 1;

            while (map.ContainsKey(name))
            {
                name = baseName + i++;
            }

            return name;
        }

        internal async Task<List<Card>> GetUniqueArtwork()
        {
            const string UniqueArtwork = "Unique Artwork";

            return await FetchBulkList(UniqueArtwork);
        }

        internal async Task<List<Card>> GetUniqueArtwork(string excludeFile)
        {
            List<Card> uniqueArtworkCards = await GetUniqueArtwork();
            List<Card> cards = [];

            HashSet<string> excludedNames = [];
            foreach (CardListEntry entry in await CardListFile.ReadAsync(excludeFile))
            {
                excludedNames.Add(entry.Name);
            }

            // Basic-land inclusion is decided centrally by CardFilter (via --lands);
            // here we only drop the names listed in the exclude file.
            foreach (Card card in uniqueArtworkCards)
            {
                if (!excludedNames.Contains(card.Name))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        internal async Task<List<Card>> GetDefaultCards()
        {
            const string AllArtworks = "Default Cards";

            return await FetchBulkList(AllArtworks);
        }

        internal async Task<List<Card>> GetSet(string setCode)
        {
            List<Card> cards = [];
            string url = BaseUrl + SetsUrl + setCode;

            Set set = await scryfallClient.GetFromJsonAsync<Set>(url);

            if (set.CardCount > 0)
            {
                cards = await GetCardSearch(set.SearchUri);
            }

            return cards;
        }

        internal async Task<List<Card>> GetYears(List<int> years)
        {
            string url = BaseUrl + SetsUrl;

            SetSearch sets = await scryfallClient.GetFromJsonAsync<SetSearch>(url);

            HashSet<int> yearSet = [.. years];

            // Matching sets with cards, for the threshold check.
            // A set with no released_at cannot match a requested year, so it is
            // filtered out before the date is parsed rather than blowing up.
            List<Set> matchingSets = (sets.Sets ?? [])
                .Where(s => s.ReleasedAt != null && yearSet.Contains(DateTime.Parse(s.ReleasedAt).Year) && s.CardCount > 0)
                .ToList();

            // Above the threshold the paginated search path generates too many
            // /cards/search calls (each at the stricter 2/s rate limit) and trips
            // 429. Switch to a single bulk "Default Cards" download (74 MB gz,
            // data endpoint = 10/s) and filter by year locally — one HTTP round
            // trip replaces hundreds.
            if (YearsBulkDecision(matchingSets, BulkYearsThreshold))
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Estimated {matchingSets.Sum(s => s.CardCount)} cards across {matchingSets.Count} sets — using bulk download to respect Scryfall's rate limit.[/]");

                List<Card> bulkCards = await GetDefaultCards();

                return FilterByYear(bulkCards, yearSet);
            }

            // Below threshold: paginate search as before (cheaper for small volumes).
            List<Card> cards = [];

            foreach (Set set in matchingSets)
            {
                cards.AddRange(await GetCardSearch(set.SearchUri));
            }

            return cards;
        }

        // --- bulk-vs-search decision + local year filter (pure logic, tested) --

        internal const int BulkYearsThreshold = 500;

        /// <summary>
        /// Returns true when the combined <see cref="Set.CardCount"/> of the
        /// matching sets exceeds the threshold, signalling that the paginated
        /// search path would make too many /cards/search calls and should be
        /// replaced by a single bulk-data download.
        /// </summary>
        internal static bool YearsBulkDecision(IReadOnlyList<Set> matchingSets, int threshold)
        {
            int total = matchingSets.Sum(s => s.CardCount);

            return total > threshold;
        }

        /// <summary>
        /// Keeps only the cards whose <see cref="Card.ReleasedAt"/> year is in
        /// the given set. Used by the bulk path of <see cref="GetYears"/> to
        /// replace the per-set search pagination with a local filter.
        /// </summary>
        internal static List<Card> FilterByYear(List<Card> cards, HashSet<int> years)
        {
            return cards.Where(c => years.Contains(c.ReleasedAt.Year)).ToList();
        }

        internal async Task<List<Card>> GetCardList(string fileName, bool downloadLands)
        {
            HashSet<string> seenNames = [];
            List<Card> cards = [];

            Dictionary<string, Card> index = cardsByName ??= await BuildCardsByName(downloadLands);

            foreach (CardListEntry entry in await CardListFile.ReadAsync(fileName))
            {
                if (index.TryGetValue(entry.Name, out Card? card))
                {
                    if (!seenNames.Add(entry.Name))
                    {
                        Logger.LogWarning("Duplicate card: {Name}", entry.Name);
                    }
                    else
                    {
                        card.Tag = entry.Tag;
                        cards.Add(card);
                    }
                }
                else
                {
                    Logger.LogWarning("Missing card: {Name}", entry.Name);
                }
            }

            if (downloadLands)
            {
                foreach (string basicLandType in Card.BasicLandTypes)
                {
                    string name = basicLandType;
                    int i = 1;

                    while (index.TryGetValue(name, out Card? basicLand))
                    {
                        if (CardFilter.IsBasicLandBorderAllowed(basicLand))
                        {
                            cards.Add(basicLand);
                        }

                        name = basicLandType + i++;
                    }
                }
            }

            return cards;
        }

        internal Task<Stream> GetImageStreamAsync(string url)
        {
            return scryfallClient.GetStreamAsync(url);
        }

        public void Dispose()
        {
            scryfallClient.Dispose();
        }
    }
}
