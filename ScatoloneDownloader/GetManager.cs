using System;
using System.Collections.Generic;
using System.IO;
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
					return await scryfallClient.GetFromJsonAsync<List<Card>>(bulkData.DownloadUri, JsonSerializerOptions);
				}
			}

			throw new KeyNotFoundException(string.Format("Unable to found \"{0}\" bulk-data file reference. Request: {1}", name, url));
		}

		private async Task PopulateCardsByName(bool downloadLands)
		{
			CardsByName = [];

			List<Card> cards = await GetDefaultCards();

			foreach(Card card in cards)
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

						//Le carte sono in ordine casuale ma voglio che l'art originale abbia sempre il nome senza numero
						if (i != 1 && CardFilter.IsDownloadable(card, false, false, false))
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
			const string UniqueArtwork = "Default Cards";

			return await GetCardList(UniqueArtwork);
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

			List<Card> cards = [];

			foreach(Set set in sets.Sets)
			{
				int releasedYear = DateTime.Parse(set.ReleasedAt).Year;

				if (years.Contains(releasedYear) && set.CardCount > 0)
				{
					cards.AddRange(await GetCardSearch(set.SearchUri));
				}
			}

			return cards;
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
