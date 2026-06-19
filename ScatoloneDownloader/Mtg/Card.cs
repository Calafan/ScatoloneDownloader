using System;
using System.Collections.Generic;

using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
	/// <summary>
	/// A Scryfall card as data. Filtering, imaging, and download/output behavior
	/// live in their own components; this type only holds the card's fields and
	/// the small derived facts (<see cref="IsBasicLand"/>) read across the app.
	/// </summary>
	public abstract class Card
	{
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
	}
}
