using System.Text.RegularExpressions;

using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// A Scryfall card as data. Filtering, imaging, and download/output behavior
    /// live in their own components; this type only holds the card's fields and
    /// the small derived facts (<see cref="IsBasicLand"/>) read across the app.
    /// </summary>
    public abstract partial class Card
    {
        internal static readonly List<string> BasicLandTypes =
        [
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
        ];

        internal string Name { get; init; }
        internal string Id { get; init; }

        /// <summary>Scryfall <c>oracle_id</c>: stable across printings, unlike
        /// <see cref="Id"/> (printing-specific). Used as the key for cube
        /// evaluation metadata so re-downloads that match a different printing
        /// still resolve to the same card.</summary>
        internal string OracleId { get; init; }
        internal string Language { get; init; }
        internal string Layout { get; init; }

        internal DateTime ReleasedAt { get; init; }

        internal string TypeLine { get; init; }

        /// <summary>Rules text. For a double-faced card the per-face texts are
        /// joined (top-level <c>oracle_text</c> is usually empty there). Input for
        /// the future effect auto-classifier; empty when Scryfall omits it.</summary>
        internal string OracleText { get; init; }

        /// <summary>Scryfall keyword abilities (e.g. "Flash", "Hexproof",
        /// "Flying"). Cheap structured signal alongside <see cref="OracleText"/>
        /// for classification. Never null (empty when absent).</summary>
        internal List<string> Keywords { get; init; }

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
        internal List<string> ColorIdentity { get; init; }
        internal string ManaCost { get; init; }

        internal List<string> PromoTypes { get; init; }

        /// <summary>Scryfall <c>collector_number</c> (may carry letters or a star, e.g. "12a", "★").</summary>
        internal string CollectorNumber { get; init; }

        /// <summary><c>oracle_id</c> of the first card face; reversible cards carry the
        /// oracle id only on their faces, so <see cref="OracleId"/> is empty for them.
        /// Empty when there are no faces.</summary>
        internal string FirstFaceOracleId { get; init; }

        /// <summary>True when the printing is legal in no format: it has legalities and every
        /// one is "not_legal". Missing or empty legalities are not "never legal".</summary>
        internal bool NeverLegal { get; init; }

        /// <summary>True when the front type line (before " // " and before the " — " subtype
        /// dash) contains the word Dungeon.</summary>
        internal bool IsDungeon { get; init; }

        /// <summary>True when the front type line (before " // " and before the " — " subtype
        /// dash) starts with "Token".</summary>
        internal bool IsTokenType { get; init; }

        // --- Cube management fields ------------------------------------------
        // Rating/Status/Effects are the tagger-authored evaluation: loaded from
        // the git-tracked metadata directory's rating-tier files by
        // MetadataJsonSynchronizer (see property docs below), never from image
        // files. MacroType and ColorCategory are derived from Scryfall fields at
        // construction.
        // ManaPips is parsed from ManaCost.

        /// <summary>Cube rating (0 = unrated, 1-5 stars). Loaded from the
        /// metadata directory by <see cref="Cube.MetadataJsonSynchronizer"/>; authored
        /// in the web tagger. Also the field that decides which rating-tier
        /// file (<see cref="Metadata.CubeMetadataStore"/>) the card is stored in.</summary>
        internal int Rating { get; set; }

        /// <summary>Ban/Token/Jolly pool status. Loaded from the metadata
        /// directory by <see cref="Cube.MetadataJsonSynchronizer"/>; defaults to
        /// <see cref="CardStatus.None"/> (normal pool card).</summary>
        internal CardStatus Status { get; set; } = CardStatus.None;

        /// <summary>Functional effect tags (multi-valued bitset). Loaded from
        /// the metadata directory by <see cref="Cube.MetadataJsonSynchronizer"/>;
        /// defaults to <see cref="CardEffect.None"/> when the card is untagged.</summary>
        internal CardEffect Effects { get; set; } = CardEffect.None;

        /// <summary>True when the metadata entry carries a <c>reviewedAt</c>, i.e. a
        /// human confirmed its tags; false when the effects are only the
        /// classifier's proposal or the card has no entry. Loaded by
        /// <see cref="Cube.MetadataJsonSynchronizer"/>.</summary>
        internal bool Reviewed { get; set; }

        internal MacroType MacroType { get; init; }
        internal string ColorCategory { get; init; }
        internal int ManaPips { get; init; }

        /// <summary>Download sub-folder assigned by the list file being processed
        /// (see <see cref="Cli.Cube.MakeListCommand"/>); null for cards that came
        /// from a plain year/set download.</summary>
        internal string? Tag { get; set; }

        // Scryfall may omit any of these JSON fields, so every one is coalesced:
        // the wire DTO (JsonCard) is nullable by nature, this domain type is not.
        // Text fields fall back to string.Empty and lists to [], matching how the
        // filters already read them (e.g. CardFilter.IsPaperGame counts Games).
        internal Card(JsonCard jsonCard)
        {
            Name = jsonCard.Name ?? string.Empty;
            Id = jsonCard.Id ?? string.Empty;
            OracleId = jsonCard.OracleId ?? string.Empty;
            Language = jsonCard.Language ?? string.Empty;
            Layout = jsonCard.Layout ?? string.Empty;

            // The one field with no sensible fallback: a card without a release
            // date cannot be foldered by year, so fail loudly and name the card.
            ReleasedAt = DateTime.Parse(jsonCard.ReleasedAt
                ?? throw new ArgumentException(
                    $"Card '{jsonCard.Name}' has no released_at.", nameof(jsonCard)));

            TypeLine = jsonCard.TypeLine ?? string.Empty;
            OracleText = ResolveOracleText(jsonCard);
            Keywords = jsonCard.Keywords ?? [];

            Games = jsonCard.Games ?? [];

            Reprint = jsonCard.Reprint;
            Variation = jsonCard.Variation;
            Textless = jsonCard.Textless;

            Set = jsonCard.Set ?? string.Empty;
            SetName = jsonCard.SetName ?? string.Empty;
            SetType = jsonCard.SetType ?? string.Empty;

            BorderColor = jsonCard.BorderColor ?? string.Empty;
            FrameEffects = jsonCard.FrameEffects ?? [];

            Cmc = jsonCard.Cmc;
            ColorIdentity = jsonCard.ColorIdentity ?? [];
            ManaCost = jsonCard.ManaCost ?? string.Empty;
            PromoTypes = jsonCard.PromoTypes ?? [];

            CollectorNumber = jsonCard.CollectorNumber ?? string.Empty;
            FirstFaceOracleId = jsonCard.CardFaces is { Count: > 0 } faces ? faces[0].OracleId ?? string.Empty : string.Empty;
            NeverLegal = jsonCard.Legalities is { Count: > 0 } legalities
                && legalities.Values.All(value => string.Equals(value, "not_legal", StringComparison.Ordinal));

            string frontType = FrontType(TypeLine);
            IsDungeon = DungeonWord().IsMatch(frontType);
            IsTokenType = frontType.StartsWith("Token", StringComparison.Ordinal);

            // Derived cube-design fields.
            MacroType = MacroTypeResolver.Resolve(TypeLine);
            ColorCategory = ColorCategoryClassifier.Classify(ColorIdentity);
            ManaPips = ManaPipsParser.CountColoredPips(ManaCost);
        }

        [GeneratedRegex(@"\bDungeon\b")]
        private static partial Regex DungeonWord();

        /// <summary>The type line cut before the first " // " (back face) and then before
        /// the first " — " (subtypes).</summary>
        private static string FrontType(string typeLine)
        {
            int slash = typeLine.IndexOf(" // ", StringComparison.Ordinal);
            string front = slash >= 0 ? typeLine[..slash] : typeLine;
            int dash = front.IndexOf(" — ", StringComparison.Ordinal);

            return dash >= 0 ? front[..dash] : front;
        }

        /// <summary>Rules text for classification: the top-level
        /// <c>oracle_text</c> when present, otherwise the per-face texts joined
        /// (double-faced cards carry their text on the faces, not the top level).</summary>
        private static string ResolveOracleText(JsonCard jsonCard)
        {
            if (!string.IsNullOrEmpty(jsonCard.OracleText))
            {
                return jsonCard.OracleText;
            }

            if (jsonCard.CardFaces == null)
            {
                return string.Empty;
            }

            List<string> faceTexts = [];
            foreach (JsonCardFace face in jsonCard.CardFaces)
            {
                if (!string.IsNullOrEmpty(face.OracleText))
                {
                    faceTexts.Add(face.OracleText);
                }
            }

            return string.Join("\n", faceTexts);
        }

        internal static Card CreateCard(JsonCard jsonCard)
        {
            return jsonCard.ImageUris != null ? new SingleFaceCard(jsonCard) : new DoubleFaceCard(jsonCard);
        }
    }
}
