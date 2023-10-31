using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
	public abstract class Card
	{
		private static readonly List<string> InvalidSetsType = new() { "masters", "masterpiece", "from_the_vault", "spellbook", "premium_deck", "memorabilia" };
		private static readonly List<string> InvalidFrameEffects = new() { "inverted", "showcase", "extendedart" };

		private readonly List<string> WhiteBorderSets = new(){ "ptk", "s99" };
		private protected readonly List<string> ForbiddenCharacters = new() { "\\", "/", ":", "*", "?", "\"", "<", ">", "|" };

		internal static readonly Dictionary<Mode, string> BasePaths = new(){ { Mode.All, @".\All" } , { Mode.Set, @".\Sets" }, { Mode.Years, @".\Years" }, { Mode.Files, @".\Lists" } };
		internal static readonly List<string> BasicLandTypes = new()
		{
			"Plains",
			"Island",
			"Swamp",
			"Mountain",
			"Forest",
			"Wastes",
			"Snow-Covered Plains",
			"Snow-Covered Island",
			"Snow-Covered Swamp",
			"Snow-Covered Mountain",
			"Snow-Covered Forest"
		};


		internal string Name { get; init; }
		internal string Language { get; init; }
		internal string Layout { get; init; }

		internal DateTime ReleasedAt { get; init; }

		internal string TypeLine { get; init; }

		internal List<string> Games { get; init; }
		internal List<string> FrameEffects { get; init; }

		internal bool Reprint { get; init; }
		internal bool Variation { get; init; }
		internal bool IsBasicLand { get { return !string.IsNullOrEmpty(TypeLine) && TypeLine.Contains("Basic") && TypeLine.Contains("Land"); } }

		internal string Set { get; init; }
		internal string SetName { get; init; }
		internal string SetType { get; init; }

		internal string BorderColor { get; init; }

		internal double Cmc { get; init; }
		internal List<string> Colors { get; init; }

		internal string Tag { get; set; }


		internal Card(JsonCard jsonCard)
		{
			Name = jsonCard.Name;
			Language = jsonCard.Language;
			Layout = jsonCard.Layout;

			ReleasedAt = DateTime.Parse(jsonCard.ReleasedAt);

			TypeLine = jsonCard.TypeLine;

			Games = jsonCard.Games;

			Reprint = jsonCard.Reprint;
			Variation = jsonCard.Variation;

			Set = jsonCard.Set;
			SetName = jsonCard.SetName;
			SetType = jsonCard.SetType;

			BorderColor = jsonCard.BorderColor;
			FrameEffects = jsonCard.FrameEffects;

			Cmc = jsonCard.Cmc;
			Colors = jsonCard.Colors;
		}

		internal static Card CreateCard(JsonCard jsonCard)
		{
			return jsonCard.ImageUris != null ? new SingleFaceCard(jsonCard) : new DoubleFaceCard(jsonCard);
		}

		static internal List<Card> ValidateCards(List<Card> allCards, bool downloadReprints, bool downloadTokens)
		{
			List<Card> cards = new();

			foreach(Card card in allCards)
			{
				if (card != null && card.IsValid(downloadReprints, downloadTokens))
				{
					cards.Add(card);
				}
			}

			return cards;
		}


		private bool IsSetValid()
		{
			return !InvalidSetsType.Contains(SetType);
		}

		private bool IsLayoutValid(bool downloadTokens)
		{
			bool isToken = Layout.Contains("token");
			bool isEmblem = Layout.Contains("emblem");
			bool isScheme = Layout.Contains("scheme");

			return !((isToken && !downloadTokens) || isEmblem || isScheme);
		}

		private bool IsGameValid()
		{
			return Games.Count == 0 || Games.Contains("paper");
		}

		private bool IsBorderValid()
		{
			return (BorderColor == "black") || (BorderColor == "silver") || (BorderColor == "borderless") || WhiteBorderSets.Contains(Set);
		}

		private bool IsDifferentFrameVariation()
		{
			if (FrameEffects != null)
			{
				foreach (string frameEffect in FrameEffects)
				{
					if (InvalidFrameEffects.Contains(frameEffect))
					{
						return true;
					}
				}
			}

			return false;
		}


		private static Image NormalizeBorders(Image image)
		{
			Color borderColor = ((Bitmap)image).GetPixel(20, 20);		//TODO Usare il nero, quale?

			using (Graphics g = Graphics.FromImage(image))
			{
				Brush brush = new Pen(borderColor).Brush;

				Size size = new(25, image.Height);

				g.FillRectangle(brush, new Rectangle(new Point(0, 0), size));
				g.FillRectangle(brush, new Rectangle(new Point(image.Width - size.Width, 0), size));

				size = new Size(image.Width, 25);

				g.FillRectangle(brush, new Rectangle(new Point(0, 0), size));
				g.FillRectangle(brush, new Rectangle(new Point(0, image.Height - size.Height), size));
			}

			return image;
		}


		private protected abstract Image GetImage(GetManager getManager);

		private protected string RemoveInvalidCharacters(string path)
		{
			if (path.Contains(" // "))
			{
				path = path.Replace(" // ", "_");
			}

			foreach (string character in ForbiddenCharacters)
			{
				if (path.Contains(character))
				{
					path = path.Replace(character, string.Empty);
				}
			}

			return path;
		}

		private protected string GetPath(Mode mode, string fileName)
		{
			string path = BasePaths[mode];

			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}

			switch (mode)
			{
				case Mode.All:
				case Mode.Years:
				{
					path = Path.Combine(path, ReleasedAt.Year.ToString());

					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
					}

					path = Path.Combine(path, RemoveInvalidCharacters(SetName));
					break;
				}
				case Mode.Set:
				{
					path = Path.Combine(path, RemoveInvalidCharacters(SetName));
					break;
				}
				case Mode.Files:
				{
					path = Path.Combine(path, Path.GetFileNameWithoutExtension(fileName));

					if (!string.IsNullOrEmpty(Tag))
					{
						path = Path.Combine(path, Tag);
					}

					break;
				}
			}

			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}

			return path;
		}


		internal bool IsValid(bool downloadReprints, bool downloadTokens)
		{
			bool isLanguageValid = Language == "en";
			bool isReprint = (Reprint || Variation || IsDifferentFrameVariation() || BorderColor == "borderless") && !downloadReprints;
			bool isEtched = FrameEffects != null && FrameEffects.Contains("etched");

			bool isValid = IsSetValid() && isLanguageValid && (!isReprint || IsBasicLand) && IsLayoutValid(downloadTokens) && IsGameValid() && IsBorderValid() && !isEtched;

			return isValid;
		}

		internal void Download(GetManager getManager, Mode mode, string fileName)
		{
			string basePath = GetPath(mode, fileName);

			int i = 1;
			string validName = RemoveInvalidCharacters(Name);
			string path = Path.Combine(basePath, validName);

			while (File.Exists(path + ".png"))
			{
				path = Path.Combine(basePath, validName + i++.ToString());
			}

			//Le carte sono in ordine casuale ma voglio che l'art originale abbia sempre il nome senza numero
			if (i != 1 && !IsBasicLand && IsValid(false, false))
			{
				File.Move(Path.Combine(basePath, validName) + ".png", path + ".png");
				path = Path.Combine(basePath, validName);
			}

			Image image = NormalizeBorders(GetImage(getManager));
			image.Save(path + ".png");

			image.Dispose();
		}

		internal void Print()
		{
			string basePath = GetPath(Mode.Files, string.Empty);

			File.AppendAllText(Path.Combine(basePath, "List.txt"), Name + "\n");
		}
	}
}
