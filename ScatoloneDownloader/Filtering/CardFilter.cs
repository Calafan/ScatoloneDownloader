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

        /// <summary>
        /// Promo categories that mark a *print treatment* — visually-special finish
        /// (surge foil, textured, halo foil, ...) or product treatment (thick stock, ...)
        /// — as opposed to categorical branding such as <c>universesbeyond</c>
        /// (Warhammer 40K, Lord of the Rings, ...). Art on a treated print is the
        /// same illustration as the plain printing, but the *frame/foil render*
        /// doesn't digitize cleanly, so the canonical-artwork rule excludes these
        /// from the un-numbered name slot. Add new Scryfall treatments here as
        /// they surface (the field is open-set; Scryfall does not publish a fixed
        /// enum — compare two prints of the same card to discover new values).
        /// </summary>
        private static readonly HashSet<string> SpecialPromoTreatments =
        [
            "surgefoil",
            "thick",
            "textured",
            "halofoil",
            "ripplefoil",
            "stepandcompleat",
            "gilded",
            "oil_slick",
            "neon",
            "liquid",
            "constellation",
            "immersive",
            "invisible",
            "vinyl",
            "firstspherefoil",
        ];


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

        /// <summary>
        /// Detects a promo *print treatment* — surge foil, textured, halo foil, thick
        /// stock, ... — as opposed to categorical branding like <c>universesbeyond</c>.
        /// Used by the canonical-artwork rule (the un-numbered name slot) to prefer
        /// the plain print whose PNG digitizes cleanly. A treated card is still
        /// downloadable (gated by <see cref="IsDownloadable"/>) — it just doesn't win
        /// the canonical slot, and a plain printing always displaces it.
        /// </summary>
        internal static bool IsPromoTreatment(Card card)
        {
            return card.PromoTypes != null && card.PromoTypes.Any(SpecialPromoTreatments.Contains);
        }

        /// <summary>
        /// The canonical-artwork selector: a card qualifies for the un-numbered name
        /// slot iff it would be downloaded under the strict (no-reprints, no-tokens,
        /// no-lands) filter AND is not a special print treatment. Both the
        /// <c>GetManager.PopulateCardsByName</c> dedup and the <c>CardDownloader</c>
        /// rename rule route through here, so the policy lives in one place.
        /// </summary>
        internal static bool IsCanonicalArtwork(Card card)
        {
            return IsDownloadable(card, downloadReprints: false, downloadTokens: false, downloadLands: false)
                && !IsPromoTreatment(card);
        }


        // --- Composite --------------------------------------------------------

        /// <summary>
        /// The ordered conjunction of the rules. Basic lands are excluded unless
        /// <paramref name="downloadLands"/> is set. Reprint-like cards are excluded
        /// unless <paramref name="downloadReprints"/> is set, except for basic lands
        /// which are always kept once lands are requested.
        /// </summary>
        internal static bool IsDownloadable(Card card, bool downloadReprints, bool downloadTokens, bool downloadLands)
        {
            if (card.IsBasicLand && !downloadLands)
            {
                return false;
            }

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
        internal static List<Card> Validate(IEnumerable<Card> cards, bool downloadReprints, bool downloadTokens, bool downloadLands)
        {
            List<Card> valid = [];

            foreach (Card card in cards)
            {
                if (card != null && IsDownloadable(card, downloadReprints, downloadTokens, downloadLands))
                {
                    valid.Add(card);
                }
            }

            return valid;
        }

        /// <summary>
        /// Collection rule for the <c>lands</c> command: keep every English paper basic
        /// land. Unlike <see cref="IsDownloadable"/> this applies no reprint/variant
        /// gate — the goal is the full set of printed basic-land artworks (one per
        /// unique illustration, as the Unique Artwork dataset already provides).
        /// </summary>
        internal static bool IsCollectibleBasicLand(Card card) => card.IsBasicLand && IsEnglish(card) && IsPaperGame(card);

        /// <summary>Keeps only the collectible basic lands from the given list.</summary>
        internal static List<Card> ValidateBasicLands(IEnumerable<Card> cards)
        {
            List<Card> valid = [];

            foreach (Card card in cards)
            {
                if (card != null && IsCollectibleBasicLand(card))
                {
                    valid.Add(card);
                }
            }

            return valid;
        }
    }
}
