using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

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

        private Dictionary<string, Card> CardsByName;


        private async Task<List<Card>> GetCardSearch(string searchUri)
        {
            List<Card> cards = [];

            CardSearch setSearch = null;
            bool firstTime = true;

            do
            {
                searchUri = firstTime ? searchUri : setSearch.NextPage;

                setSearch = await scryfallClient.GetFromJsonAsync<CardSearch>(searchUri, JsonSerializerOptions);

                cards.AddRange(setSearch.Data);

                firstTime = false;
            }
            while (setSearch != null && setSearch.HasMore);

            return cards;
        }

        private async Task<List<Card>> GetCardList(string name)
        {
            const string BulkDataUrl = "bulk-data";


            string url = BaseUrl + BulkDataUrl;

            BulkDataCollection bulkDataCollection = await scryfallClient.GetFromJsonAsync<BulkDataCollection>(url);

            foreach (BulkData bulkData in bulkDataCollection.Data)
            {
                if (bulkData.Name == name)
                {
                    // Scryfall bulk files are gzipped JSONL — one card per line — not a
                    // single JSON array anymore. Stream them line by line.
                    List<Card> cards = [];
                    await foreach (Card card in scryfallClient.GetJsonLinesAsync<Card>(bulkData.JsonlDownloadUri, JsonSerializerOptions))
                    {
                        cards.Add(card);
                    }
                    return cards;
                }
            }

            throw new KeyNotFoundException(string.Format("Unable to found \"{0}\" bulk-data file reference. Request: {1}", name, url));
        }

        private async Task PopulateCardsByName(bool downloadLands)
        {
            CardsByName = [];

            List<Card> cards = await GetDefaultCards();

            foreach (Card card in cards)
            {
                try
                {
                    if (!card.IsBasicLand)
                    {

                        string name = card.Name;
                        int i = 1;

                        while (CardsByName.ContainsKey(name))
                        {
                            name = card.Name + i++;
                        }

                        // Cards arrive in random order, but the original artwork must always keep the un-numbered name.
                        if (i != 1 && CardFilter.IsCanonicalArtwork(card))
                        {
                            Card notFirstArtCard = CardsByName[card.Name];

                            CardsByName[card.Name] = card;
                            CardsByName.Add(name, notFirstArtCard);
                        }
                        else
                        {
                            CardsByName.Add(name, card);
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

                        string name = card.Name;
                        int i = 1;

                        while (CardsByName.ContainsKey(name))
                        {
                            name = card.Name + i++;
                        }

                        card.Tag = "Basic Lands";

                        CardsByName.Add(name, card);
                    }
                }
            }
        }


        internal async Task<List<Card>> GetUniqueArtwork()
        {
            const string UniqueArtwork = "Unique Artwork";

            return await GetCardList(UniqueArtwork);
        }

        internal async Task<List<Card>> GetUniqueArtwork(string excludeFile)
        {
            List<Card> uniqueArtworkCards = await GetUniqueArtwork();
            List<Card> cards = [];

            HashSet<string> cardNames = [];

            using (StreamReader reader = new(new FileStream(excludeFile, FileMode.Open)))
            {
                string rawLine;
                while ((rawLine = await reader.ReadLineAsync()) is not null)
                {
                    string line = rawLine.Trim();

                    if (!(string.IsNullOrEmpty(line) || line.StartsWith("--")))
                    {
                        string name;

                        if (line.Contains("--"))
                        {
                            string[] parts = line.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                            name = parts[0];
                        }
                        else
                        {
                            name = line;
                        }

                        cardNames.Add(name);
                    }
                }
            }

            // Basic-land inclusion is decided centrally by CardFilter (via --lands);
            // here we only drop the names listed in the exclude file.
            foreach (Card card in uniqueArtworkCards)
            {
                if (!cardNames.Contains(card.Name))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        internal async Task<List<Card>> GetDefaultCards()
        {
            const string AllArtworks = "Default Cards";

            return await GetCardList(AllArtworks);
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
            List<Set> matchingSets = sets.Sets
                .Where(s => yearSet.Contains(DateTime.Parse(s.ReleasedAt).Year) && s.CardCount > 0)
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
            HashSet<string> cardNames = [];
            List<Card> cards = [];

            if (CardsByName == null)
            {
                await PopulateCardsByName(downloadLands);
            }

            using (StreamReader reader = new(new FileStream(fileName, FileMode.Open)))
            {
                string rawLine;
                while ((rawLine = await reader.ReadLineAsync()) is not null)
                {
                    string line = rawLine.Trim();

                    if (!(string.IsNullOrEmpty(line) || line.StartsWith("--")))
                    {
                        string tag = string.Empty;

                        string name;
                        if (line.Contains("--"))
                        {
                            string[] parts = line.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                            name = parts[0];

                            if (parts.Length > 1)
                            {
                                tag = parts[1];
                            }
                        }
                        else
                        {
                            name = line;
                        }

                        if (CardsByName.TryGetValue(name, out Card card))
                        {
                            if (cardNames.Contains(name))
                            {
                                Logger.LogWarning("Duplicate card: {Name}", name);
                            }
                            else
                            {
                                card.Tag = tag;
                                cards.Add(card);
                                cardNames.Add(name);
                            }
                        }
                        else
                        {
                            Logger.LogWarning("Missing card: {Name}", name);
                        }
                    }
                }
            }

            if (downloadLands)
            {
                foreach (string basicLandType in Card.BasicLandTypes)
                {
                    string name = basicLandType;
                    int i = 1;

                    while (CardsByName.ContainsKey(name))
                    {
                        Card basicLand = CardsByName[name];

                        if (CardFilter.IsBasicLandBorderAllowed(basicLand))
                        {
                            cards.Add(CardsByName[name]);
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
