using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;

using ScatoloneDownloader.Json.BulkData;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Json.Sets;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader
{
	internal class GetManager
	{
		private const string BaseUrl = "https://api.scryfall.com/";
		private const string SetsUrl = "sets/";

		private static readonly JsonSerializerOptions JsonSerializerOptions = new()
		{
			Converters = { new JsonCardConverter() }
		};

		private DateTime minNextRequestTime;
		private Dictionary<string, Card> CardsByName;


		private Stream Get(string url)
		{
			TimeSpan sleepingTime = minNextRequestTime - DateTime.Now;

			if (sleepingTime.TotalMilliseconds > 0)
			{
				Thread.Sleep(sleepingTime);
			}

			minNextRequestTime = DateTime.Now.AddMilliseconds(100);

			WebRequest request = WebRequest.Create(url);

			HttpWebResponse response = request.GetResponse() as HttpWebResponse;
			if (response.StatusCode == HttpStatusCode.OK)
			{
				return response.GetResponseStream();
			}
			else
			{
				throw new WebException(string.Format("Unable to contact: {0}. Status code: {1}", url, response.StatusCode));
			}
		}

		private string GetJson(string url)
		{
			string json;

			using (Stream stream = Get(url))
			{
				using StreamReader reader = new(stream);
				json = reader.ReadToEnd();
			}

			return json;
		}

		private List<Card> GetCardSearch(string searchUri)
		{
			List<Card> cards = new();

			CardSearch setSearch = null;
			bool firstTime = true;

			do
			{
				searchUri = firstTime ? searchUri : setSearch.NextPage;

				string json = GetJson(searchUri);
				setSearch = JsonSerializer.Deserialize<CardSearch>(json, JsonSerializerOptions);

				cards.AddRange(setSearch.Data);

				firstTime = false;
			}
			while (setSearch != null && setSearch.HasMore);

			return cards;
		}

		private List<Card> GetCardList(string name)
		{
			const string BulkDataUrl = "bulk-data";


			string url = BaseUrl + BulkDataUrl;

			string json = GetJson(url);

			BulkDataCollection bulkDataCollection = JsonSerializer.Deserialize<BulkDataCollection>(json);

			foreach (BulkData bulkData in bulkDataCollection.Data)
			{
				if (bulkData.Name == name)
				{
					json = GetJson(bulkData.DownloadUri);

					return JsonSerializer.Deserialize<List<Card>>(json, JsonSerializerOptions);
				}
			}

			throw new KeyNotFoundException(string.Format("Unable to found \"{0}\" bulk-data file reference. Request: {1}", name, url));
		}

		private void PopulateCardsByName(bool downloadLands)
		{
			CardsByName = new();

			List<Card> cards = GetDefaultCards();

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
						if (i != 1 && card.IsValid(false, false))
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
				catch
				{
					SimpleLogger.Instance.Error("Missing parameters: " + card.Name + " - " + card.Set);
				}
			}

			if (downloadLands)
			{
				cards = GetUniqueArtwork();

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


		internal List<Card> GetUniqueArtwork()
		{
			const string UniqueArtwork = "Unique Artwork";

			return GetCardList(UniqueArtwork);
		}

		internal List<Card> GetUniqueArtwork(string excludeFile)
		{
			List<Card> uniqueArtworkCards = GetUniqueArtwork();
			List<Card> cards = new();

			HashSet<string> cardNames = new();

			using (StreamReader reader = new(new FileStream(excludeFile, FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine().Trim();

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

			foreach (Card card in uniqueArtworkCards)
			{
				if (!card.IsBasicLand && !cardNames.Contains(card.Name))
				{
					cards.Add(card);
				}
			}

			return cards;
		}

		internal List<Card> GetDefaultCards()
		{
			const string UniqueArtwork = "Default Cards";

			return GetCardList(UniqueArtwork);
		}

		internal List<Card> GetSet(string setCode)
		{
			List<Card> cards = new();
			string url = BaseUrl + SetsUrl + setCode;

			string json = GetJson(url);
			Set set = JsonSerializer.Deserialize<Set>(json);

			if (set.CardCount > 0)
			{
				cards = GetCardSearch(set.SearchUri);
			}

			return cards;
		}

		internal List<Card> GetYears(List<int> years)
		{
			string url = BaseUrl + SetsUrl;

			string json = GetJson(url);
			SetSearch sets = JsonSerializer.Deserialize<SetSearch>(json);

			List<Card> cards = new();

			foreach(Set set in sets.Sets)
			{
				int releasedYear = DateTime.Parse(set.ReleasedAt).Year;

				if (years.Contains(releasedYear) && set.CardCount > 0)
				{
					cards.AddRange(GetCardSearch(set.SearchUri));
				}
			}

			return cards;
		}

		internal List<Card> GetCardList(string fileName, bool downloadLands)
		{
			HashSet<string> cardNames = new();
			List<Card> cards = new();

			if (CardsByName == null)
			{
				PopulateCardsByName(downloadLands);
			}

			using (StreamReader reader = new(new FileStream(fileName, FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine().Trim();

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

						if (CardsByName.ContainsKey(name))
						{
							if (cardNames.Contains(name))
							{
								SimpleLogger.Instance.Warning("Duplicate card: " + name);
							}
							else
							{
								Card card = CardsByName[name];

								card.Tag = tag;
								cards.Add(card);
								cardNames.Add(name);
							}
						}
						else
						{
							SimpleLogger.Instance.Warning("Missing card: " + name);
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

						if (basicLand.BorderColor != "white" && basicLand.BorderColor != "silver")
						{
							cards.Add(CardsByName[name]);
						}

						name = basicLandType + i++;
					}
				}
			}

			return cards;
		}

		internal Stream GetImageStream(string url)
		{
			return Get(url);
		}
	}
}
