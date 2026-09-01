using System.Collections.Generic;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Immutable snapshot of all cube metrics computed by
    /// <see cref="CardAnalyzer.Analyze"/>. Each property corresponds to an R2
    /// metric from the cube management plan. Every metric here is derived from the
    /// <see cref="MacroType"/> (from type_line) and <see cref="Card.ColorCategory"/>
    /// (from color_identity) classifications, so the Markdown report renders from a
    /// single source of truth.
    /// </summary>
    public sealed class AnalysisReport
    {
        /// <summary>Total count per MacroType (Creature/Land/OtherPermanent/Spell).</summary>
        internal Dictionary<MacroType, int> MacroTypeCounts { get; init; } = [];

        /// <summary>Count per ColorCategory bucket (W, U, WU, WUB, 4_5_Colors, Colorless, ...).</summary>
        internal Dictionary<string, int> ColorCategoryCounts { get; init; } = [];

        /// <summary>MacroType breakdown per ColorCategory: [category][macroType] = count.
        /// Drives the per-color section of the Markdown report.</summary>
        internal Dictionary<string, Dictionary<MacroType, int>> MacroTypeByCategory { get; init; } = [];

        /// <summary>CMC distribution per ColorCategory: [category][cmc] = count.</summary>
        internal Dictionary<string, Dictionary<double, int>> CmcByCategory { get; init; } = [];

        /// <summary>Creature-only CMC distribution per ColorCategory: [category][cmc] = count.</summary>
        internal Dictionary<string, Dictionary<double, int>> CreatureCmcByCategory { get; init; } = [];

        /// <summary>Rating tier count keyed by (ColorCategory, star rating 3-5).</summary>
        internal Dictionary<(string Color, int Stars), int> RatingTiers { get; init; } = [];

        /// <summary>CMC bucket (1-5 = exact, 6 = 6+) count per MacroType.</summary>
        internal Dictionary<MacroType, Dictionary<int, int>> CurveByMacroType { get; init; } = [];

        /// <summary>Average colored pips per card across the whole cube.</summary>
        internal double GlobalPipDensity { get; init; }

        /// <summary>Average CMC globally.</summary>
        internal double AverageCmc { get; init; }

        /// <summary>Average colored pips per card per ColorCategory.</summary>
        internal Dictionary<string, double> PipDensityPerCategory { get; init; } = [];

        /// <summary>Average CMC per ColorCategory.</summary>
        internal Dictionary<string, double> AverageCmcPerCategory { get; init; } = [];

        /// <summary>Average creature CMC per ColorCategory.</summary>
        internal Dictionary<string, double> AverageCreatureCmcPerCategory { get; init; } = [];

        /// <summary>
        /// Per-single-color card count: multicolor cards contribute to EVERY
        /// color in their color_identity. Lands are excluded from this count and
        /// tracked separately in <see cref="LandCount"/>.
        /// </summary>
        internal Dictionary<string, int> IndividualColorCounts { get; init; } = [];

        /// <summary>Non-land cards with an empty color_identity (artifacts, eldrazi, ...).</summary>
        internal int ColorlessCount { get; init; }

        /// <summary>Cards whose MacroType is Land, regardless of color_identity.</summary>
        internal int LandCount { get; init; }

        /// <summary>Lands grouped by ColorCategory — for the bottom Lands section.</summary>
        internal Dictionary<string, int> LandsByCategory { get; init; } = [];

        /// <summary>Card count per <see cref="CardEffect"/> flag across non-land cards.
        /// Counts OVERLAP: a card with N effects is counted under each, so the sum
        /// exceeds <see cref="TotalCards"/>.</summary>
        internal Dictionary<CardEffect, int> EffectCounts { get; init; } = [];

        /// <summary>Non-land cards with no effect tagged yet.</summary>
        internal int UntaggedEffectCount { get; init; }

        /// <summary>Total card count analyzed (excludes basic lands + tagged cards, per existing rule).</summary>
        internal int TotalCards { get; init; }
    }
}