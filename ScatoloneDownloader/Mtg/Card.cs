using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
	public abstract class Card
	{
		private const double CardWidthMm = 63d;
		private const double CardHeightMm = 88d;
		private const double AdditionalBorderMm = 3d;

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
		internal bool Textless { get; init; }
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
            Textless = jsonCard.Textless;

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

      private static Color GetBorderColor(Image image)
		{
			int x = Math.Clamp(20, 0, image.Width - 1);
			int y = Math.Clamp(20, 0, image.Height - 1);

			if (image is Bitmap bitmap)
			{
				return bitmap.GetPixel(x, y);
			}

			using Bitmap sampledBitmap = new(image);
			return sampledBitmap.GetPixel(x, y);
		}

      private static void NormalizeBorders(Image image)
		{
            Color borderColor = GetBorderColor(image);      //TODO Usare il nero, quale?

           using (Graphics g = Graphics.FromImage(image))
           using (SolidBrush brush = new(borderColor))
			{
               const int borderThickness = 25;

				g.FillRectangle(brush, 0, 0, borderThickness, image.Height);
				g.FillRectangle(brush, image.Width - borderThickness, 0, borderThickness, image.Height);
				g.FillRectangle(brush, 0, 0, image.Width, borderThickness);
				g.FillRectangle(brush, 0, image.Height - borderThickness, image.Width, borderThickness);
			}
		}

		private static Image AddOuterBorder(Image image)
		{
			Color borderColor = GetBorderColor(image);

			int horizontalBorder = (int)Math.Round(image.Width * (AdditionalBorderMm / CardWidthMm));
			int verticalBorder = (int)Math.Round(image.Height * (AdditionalBorderMm / CardHeightMm));

			Bitmap borderedImage = new(image.Width + (horizontalBorder * 2), image.Height + (verticalBorder * 2));

			using (Graphics g = Graphics.FromImage(borderedImage))
			{
				g.Clear(borderColor);
				g.DrawImage(image, horizontalBorder, verticalBorder, image.Width, image.Height);
			}

			return borderedImage;
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
			if (i != 1 && !IsBasicLand && CardFilter.IsDownloadable(this, false, false))
			{
				File.Move(Path.Combine(basePath, validName) + ".png", path + ".png");
				path = Path.Combine(basePath, validName);
			}

            using Image sourceImage = GetImage(getManager);
			NormalizeBorders(sourceImage);

			using Image image = AddOuterBorder(sourceImage);
			image.Save(path + ".png");
		}

		internal void Print()
		{
			string basePath = GetPath(Mode.Files, string.Empty);

			File.AppendAllText(Path.Combine(basePath, "List.txt"), Name + "\n");
		}
	}
}
