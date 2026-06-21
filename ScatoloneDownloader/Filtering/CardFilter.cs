using System.Collections.Generic;
using System.Linq;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Filtering
{
	/// <summary>
	/// Decides whether a card should be downloaded. Each rule is a named predicate;
	/// <see cref="IsDownloadable"/> is the ordered conjunction of them. The
	/// reprints/tokens flags toggle the two rules that depend on them. All filter
	/// data lives here, so there is one place to change what gets excluded (R1-R3).
	/// </summary>
	internal static class CardFilter
	{
		private static readonly HashSet<string> InvalidSetTypes =
        [
            "masters", "masterpiece", "from_the_vault", "spellbook", "premium_deck", "memorabilia"
		];

		private static readonly HashSet<string> InvalidFrameEffects =
        [
            "inverted", "showcase", "extendedart"
		];

		private static readonly HashSet<string> WhiteBorderSets = ["ptk", "s99"];

		private static readonly HashSet<string> ValidBorderColors = ["black", "silver", "borderless"];


		// --- Named rules ------------------------------------------------------

		/// <summary>Excludes supplementary set types (masters, from-the-vault, ...).</summary>
		internal static bool HasValidSetType(Card card) => !InvalidSetTypes.Contains(card.SetType);

		/// <summary>Only English printings.</summary>
		internal static bool IsEnglish(Card card) => card.Language == "en";

		/// <summary>Excludes emblems, schemes, and — unless tokens are requested — tokens.</summary>
		internal static bool HasValidLayout(Card card, bool downloadTokens)
		{
			bool isToken = card.Layout.Contains("token");
			bool isEmblem = card.Layout.Contains("emblem");
			bool isScheme = card.Layout.Contains("scheme");

			return !((isToken && !downloadTokens) || isEmblem || isScheme);
		}

		/// <summary>Paper printings only (or cards with no game listed).</summary>
		internal static bool IsPaperGame(Card card) => card.Games.Count == 0 || card.Games.Contains("paper");

		/// <summary>Black, silver, or borderless borders, plus the known white-border sets.</summary>
		internal static bool HasValidBorder(Card card)
		{
			return ValidBorderColors.Contains(card.BorderColor) || WhiteBorderSets.Contains(card.Set);
		}

		/// <summary>Excludes etched-foil frame treatments.</summary>
		internal static bool IsEtched(Card card) => card.FrameEffects != null && card.FrameEffects.Contains("etched");

		/// <summary>Reprints, variations, textless, borderless, or alternate frame treatments.</summary>
		internal static bool IsReprintLike(Card card)
		{
			bool hasInvalidFrameEffect = card.FrameEffects != null && card.FrameEffects.Any(InvalidFrameEffects.Contains);

			return card.Reprint || card.Variation || hasInvalidFrameEffect || card.Textless || card.BorderColor == "borderless";
		}

		/// <summary>Basic lands keep their own border gate: no white or silver borders.</summary>
		internal static bool IsBasicLandBorderAllowed(Card card) => card.BorderColor != "white" && card.BorderColor != "silver";


		// --- Composite --------------------------------------------------------

		/// <summary>
		/// The ordered conjunction of the rules. Reprint-like cards are excluded
		/// unless <paramref name="downloadReprints"/> is set, except for basic lands
		/// which are always kept.
		/// </summary>
		internal static bool IsDownloadable(Card card, bool downloadReprints, bool downloadTokens)
		{
			bool reprintExcluded = !downloadReprints && IsReprintLike(card);

			return HasValidSetType(card)
				&& IsEnglish(card)
				&& (!reprintExcluded || card.IsBasicLand)
				&& HasValidLayout(card, downloadTokens)
				&& IsPaperGame(card)
				&& HasValidBorder(card)
				&& !IsEtched(card);
		}

		/// <summary>Keeps only the downloadable cards from the given list.</summary>
		internal static List<Card> Validate(IEnumerable<Card> cards, bool downloadReprints, bool downloadTokens)
		{
			List<Card> valid = [];

			foreach (Card card in cards)
			{
				if (card != null && IsDownloadable(card, downloadReprints, downloadTokens))
				{
					valid.Add(card);
				}
			}

			return valid;
		}
	}
}
