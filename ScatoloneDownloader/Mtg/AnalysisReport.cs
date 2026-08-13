using System.Collections.Generic;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Immutable snapshot of all extended cube metrics computed by
    /// <see cref="CardAnalyzer.Analyze"/>. Each property corresponds to an R2
    /// metric from the cube management plan.
    /// </summary>
    public sealed class AnalysisReport
    {
        /// <summary>Total count per MacroType (Creature/Land/OtherPermanent/Spell).</summary>
        internal Dictionary<MacroType, int> MacroTypeCounts { get; init; } = new();

        /// <summary>Count per ColorCategory bucket (W, U, WU, WUB, 4_5_Colors, Colorless, ...).</summary>
        internal Dictionary<string, int> ColorCategoryCounts { get; init; } = new();

        /// <summary>Rating tier count keyed by (color letter, star rating 3-5).</summary>
        internal Dictionary<(string Color, int Stars), int> RatingTiers { get; init; } = new();

        /// <summary>CMC bucket (1-5 = exact, 6 = 6+) count per MacroType.</summary>
        internal Dictionary<MacroType, Dictionary<int, int>> CurveByMacroType { get; init; } = new();

        /// <summary>Average colored pips per card across the whole cube.</summary>
        internal double GlobalPipDensity { get; init; }

        /// <summary>Average CMC globally.</summary>
        internal double AverageCmc { get; init; }

        /// <summary>Average colored pips per card per ColorCategory.</summary>
        internal Dictionary<string, double> PipDensityPerCategory { get; init; } = new();

        /// <summary>Total card count analyzed (excludes basic lands + tagged cards, per existing rule).</summary>
        internal int TotalCards { get; init; }
    }
}