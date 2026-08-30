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
        internal string CollectorNumber { get; init; }
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
        internal List<string> ColorIdentity { get; init; }
        internal string ManaCost { get; init; }

        internal List<string> PromoTypes { get; init; }

        // --- Cube management fields ------------------------------------------
        // Rating/Status/Effects are the tagger-authored evaluation: loaded from
        // cube-metadata.json by MetadataJsonSynchronizer (see property docs
        // below), never from XMP. XmpLabel is legacy Adobe Bridge color-label
        // data, read only once by the `import` seed command (MetadataSynchronizer)
        // to migrate an existing Bridge label into cube-metadata.json; no other
        // code path reads it. MacroType and ColorCategory are derived from
        // Scryfall fields at construction. ManaPips is parsed from ManaCost.

        /// <summary>Cube rating (0 = unrated, 1-5 stars). Loaded from
        /// <c>cube-metadata.json</c> by <see cref="MetadataJsonSynchronizer"/>;
        /// authored in the web tagger, never derived from XMP after the initial
        /// <c>import</c> seed.</summary>
        internal int Rating { get; set; }

        /// <summary>Legacy Adobe Bridge color-label text, read once by the
        /// <c>import</c> command's <see cref="MetadataSynchronizer"/> and used
        /// only to default <see cref="Status"/> when a JSON entry has none yet.
        /// Not read by the tagger or view generation.</summary>
        internal string XmpLabel { get; set; } = string.Empty;

        /// <summary>Ban/Token/Jolly pool status. Loaded from
        /// <c>cube-metadata.json</c> by <see cref="MetadataJsonSynchronizer"/>;
        /// defaults to <see cref="CardStatus.None"/> (normal pool card).</summary>
        internal CardStatus Status { get; set; } = CardStatus.None;

        /// <summary>Functional effect tags (multi-valued bitset). Loaded from
        /// <c>cube-metadata.json</c> by <see cref="MetadataJsonSynchronizer"/>;
        /// defaults to <see cref="CardEffect.None"/> when the card is untagged.</summary>
        internal CardEffect Effects { get; set; } = CardEffect.None;

        internal MacroType MacroType { get; init; }
        internal string ColorCategory { get; init; }
        internal int ManaPips { get; init; }

        internal string Tag { get; set; }


        internal Card(JsonCard jsonCard)
        {
            Name = jsonCard.Name;
            Id = jsonCard.Id;
            OracleId = jsonCard.OracleId;
            CollectorNumber = jsonCard.CollectorNumber;
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
            ColorIdentity = jsonCard.ColorIdentity ?? [];
            ManaCost = jsonCard.ManaCost ?? string.Empty;
            PromoTypes = jsonCard.PromoTypes;

            // Derived cube-design fields.
            MacroType = MacroTypeResolver.Resolve(TypeLine);
            ColorCategory = ColorCategoryClassifier.Classify(ColorIdentity);
            ManaPips = ManaPipsParser.CountColoredPips(ManaCost);
        }

        internal static Card CreateCard(JsonCard jsonCard)
        {
            return jsonCard.ImageUris != null ? new SingleFaceCard(jsonCard) : new DoubleFaceCard(jsonCard);
        }
    }
}
