using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ScatoloneDownloader.Extensions;

namespace ScatoloneDownloader.Mtg
{
	internal class CardAnalyzer
	{
		private static readonly List<string> CardColors = new() { "W", "U", "B", "R", "G", "Multicolor", "Colorless" };
		private static readonly List<string> CardTypes = new() { "creature", "land", "artifact", "enchantment", "planeswalker", "instant", "sorcery" };

		private static readonly Dictionary<string, string> ColorPrintableNames = new() { 
			{ "W", "White" }, 
			{ "U", "Blue" }, 
			{ "B", "Black" }, 
			{ "R", "Red" }, 
			{ "G", "Green" }, 
			{ "Multicolor", "Multicolor" }, 
			{ "Colorless", "Colorless" } };

		private static readonly Dictionary<string, string> Tabs = new() { { "creature", "\t\t" }, { "land", "\t\t\t" }, { "artifact", "\t\t" }, { "enchantment", "\t" }, { "planeswalker", "\t" }, { "instant", "\t\t" }, { "sorcery", "\t\t" } };

		private readonly Dictionary<string, Dictionary<string, int>> CardsByColorAndType;
		private readonly Dictionary<string, Dictionary<double, int>> CardsByColorAndCmc;
		private readonly Dictionary<string, int> MulticolorColorDistribution;

		internal CardAnalyzer(List<Card> cards)
		{
			CardsByColorAndType = new();
			CardsByColorAndCmc = new();
			MulticolorColorDistribution = new();

			foreach (string color in CardColors)
			{
				CardsByColorAndType.Add(color, new Dictionary<string, int>());
				CardsByColorAndCmc.Add(color, new Dictionary<double, int>());

				if (color != "Multicolor" && color != "Colorless")
				{
					MulticolorColorDistribution.Add(color, 0);
				}
				

				foreach (string type in CardTypes)
				{
					CardsByColorAndType[color].Add(type, 0);
				}
			}


			foreach (Card card in cards)
			{
				if (!card.IsBasicLand && string.IsNullOrEmpty(card.Tag))
				{
					string color;

					if (card.Colors.Count == 0 ||
						card.TypeLine.Contains("land", StringComparison.CurrentCultureIgnoreCase) ||
						card.TypeLine.Contains("conspiracy", StringComparison.CurrentCultureIgnoreCase))
					{
						color = "Colorless";
					}
					else if (card.Colors.Count > 1)
					{
						color = "Multicolor";
						foreach (string c in card.Colors)
						{
							MulticolorColorDistribution[c]++;
						}

					}
					else
					{
						color = card.Colors[0];
					}

					foreach (string type in CardTypes)
					{
						if (card.TypeLine.Contains(type, StringComparison.CurrentCultureIgnoreCase))
						{
							CardsByColorAndType[color][type]++;

							if (!CardsByColorAndCmc[color].ContainsKey(card.Cmc))
							{
								CardsByColorAndCmc[color].Add(card.Cmc, 0);
							}

							CardsByColorAndCmc[color][card.Cmc]++;
							break;
						}
					}
				}
			}
		}


		private static int GetPercentage(int value, int total)
		{
			return total != 0 ? value * 100 / total : 0;
		}

		private static string GetOutput(string header, int totalCards, int totalPermanents, int totalSpells, Dictionary<string, int> cardCountByType, Dictionary<double, int> cardCountByCmc, double totalManaCost)
		{
			const string Header = "Cards: {0} - Permanents: {1} ({2}%) Spells: {3} ({4}%)";
			const string CMCHeader = "CMC distribution:";
			const string AverageCMC = "Average CMC: {0:0.##}";

			StringBuilder stringBuilder = new();

			string tab = string.Empty;

			if (!string.IsNullOrEmpty(header))
			{
				stringBuilder.AppendLine();
				stringBuilder.AppendLine();
				stringBuilder.AppendLine(header);

				tab = "\t";
			}

			stringBuilder.AppendLine(string.Format(tab + Header, totalCards, totalPermanents, GetPercentage(totalPermanents, totalCards), totalSpells, GetPercentage(totalSpells, totalCards)));
			stringBuilder.AppendLine();

			foreach (string type in CardTypes)
			{
				string pluralizedType = type != "sorcery" ? type + "s" : "sorceries";

				stringBuilder.AppendLine(tab + pluralizedType.Capitalize() + Tabs[type] + cardCountByType[type] + " (" + GetPercentage(cardCountByType[type], totalCards) + "%)");
			}

			stringBuilder.AppendLine();
			stringBuilder.AppendLine(tab + CMCHeader);

			foreach (double cmc in cardCountByCmc.Keys.OrderBy(k => k))
			{
				stringBuilder.AppendLine(tab + "\t" + Convert.ToInt32(cmc) + ":\t" + cardCountByCmc[cmc]);
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(string.Format(tab + AverageCMC, totalCards != 0 ? totalManaCost / totalCards : 0));

			return stringBuilder.ToString();
		}


		internal void SaveAnalysis(string path)
		{
			StringBuilder stringBuilder = new();

			Dictionary<string, int> cardsByType = new();
			Dictionary<double, int> cardsByCmc = new();

			foreach (string type in CardTypes)
			{
				cardsByType.Add(type, 0);
			}

			int totalCards = 0;
			double totalManaCost = 0;

			//Complete list
			foreach (string color in CardColors)
			{
				foreach(string type in CardTypes)
				{
					totalCards += CardsByColorAndType[color][type];
					cardsByType[type] += CardsByColorAndType[color][type];
				}

				foreach(double cmc in CardsByColorAndCmc[color].Keys)
				{
					totalManaCost += cmc * CardsByColorAndCmc[color][cmc];

					if (!cardsByCmc.ContainsKey(cmc))
					{
						cardsByCmc.Add(cmc, 0);
					}

					cardsByCmc[cmc] += CardsByColorAndCmc[color][cmc];
				}
			}

			int totalSpells = cardsByType["instant"] + cardsByType["sorcery"];
			int totalPermanents = totalCards - totalSpells;

			stringBuilder.Append(GetOutput(null, totalCards, totalPermanents, totalSpells, cardsByType, cardsByCmc, totalManaCost));

			//Colors
			foreach(string color in CardColors)
			{
				totalCards = 0;
				totalManaCost = 0;

				foreach (string type in CardTypes)
				{
					totalCards += CardsByColorAndType[color][type];
				}

				foreach (double cmc in CardsByColorAndCmc[color].Keys)
				{
					totalManaCost += cmc * CardsByColorAndCmc[color][cmc];
				}

				totalSpells = CardsByColorAndType[color]["instant"] + CardsByColorAndType[color]["sorcery"];
				totalPermanents = totalCards - totalSpells;

				stringBuilder.Append(GetOutput(ColorPrintableNames[color], totalCards, totalPermanents, totalSpells, CardsByColorAndType[color], CardsByColorAndCmc[color], totalManaCost));

				if (color == "Multicolor")
				{
					stringBuilder.AppendLine();
					stringBuilder.AppendLine("\tColor distribution:");

					foreach(string c in MulticolorColorDistribution.Keys)
					{
						stringBuilder.AppendLine("\t\t" + ColorPrintableNames[c] + ":\t" + MulticolorColorDistribution[c] + "(" + GetPercentage(MulticolorColorDistribution[c], totalCards) + "%)");
					}
					stringBuilder.AppendLine();
				}
			}

			using StreamWriter writer = new(path);
			writer.Write(stringBuilder.ToString());
		}
	}
}
