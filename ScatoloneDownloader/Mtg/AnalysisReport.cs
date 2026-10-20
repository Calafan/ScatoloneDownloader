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

        /// <summary>Non-land cards per effect column (W, U, B, R, G, Multi, Colorless);
        /// see <see cref="CardAnalyzer.EffectColumn"/>.</summary>
        internal Dictionary<string, int> CardsByColumn { get; init; } = [];

        /// <summary>Of <see cref="CardsByColumn"/>, the cards a human reviewed
        /// (<see cref="Card.Reviewed"/>).</summary>
        internal Dictionary<string, int> ReviewedByColumn { get; init; } = [];

        /// <summary>Of <see cref="CardsByColumn"/>, the cards with no effect tagged.</summary>
        internal Dictionary<string, int> UntaggedByColumn { get; init; } = [];

        /// <summary>[column][flag] = cards carrying the flag. Overlapping, like
        /// <see cref="EffectCounts"/>.</summary>
        internal Dictionary<string, Dictionary<CardEffect, int>> EffectCountsByColumn { get; init; } = [];

        /// <summary>Non-land cards per colour IDENTITY (W, U, B, R, G, Colorless). Unlike
        /// <see cref="CardsByColumn"/> a card counts once in EVERY colour of its
        /// <see cref="Card.ColorIdentity"/> (a WU card is in W and in U), and a card
        /// with an empty identity counts in "Colorless". There is no "Multi" key, so
        /// the sum exceeds <see cref="TotalCards"/>. Answers the question "how much of
        /// this can a W drafter play" which the one-column split cannot.</summary>
        internal Dictionary<string, int> CardsByIdentity { get; init; } = [];

        /// <summary>Of <see cref="CardsByIdentity"/>, the cards a human reviewed.</summary>
        internal Dictionary<string, int> ReviewedByIdentity { get; init; } = [];

        /// <summary>Of <see cref="CardsByIdentity"/>, the cards with no effect tagged.</summary>
        internal Dictionary<string, int> UntaggedByIdentity { get; init; } = [];

        /// <summary>[identity colour][flag] = cards carrying the flag; a multicolour
        /// card adds to each of its colours. Overlapping, like <see cref="EffectCounts"/>.</summary>
        internal Dictionary<string, Dictionary<CardEffect, int>> EffectCountsByIdentity { get; init; } = [];

        /// <summary>Answers-vs-targets groups, in report order.</summary>
        internal List<AnswerGroupCount> AnswerGroups { get; init; } = [];

        /// <summary>Creature profile per <see cref="CostBucket"/> ("0".."5", "6_Plus"):
        /// every bucket is present, empty ones zeroed. Population: the non-land cards
        /// whose <see cref="MacroType"/> is Creature.</summary>
        internal Dictionary<string, CreatureCostRow> CreatureByCost { get; init; } = [];

        /// <summary>All creatures (the sum of <see cref="CreatureByCost"/>).</summary>
        internal int CreatureCount { get; init; }

        /// <summary>Creatures with mana value 2 or less.</summary>
        internal int CreaturesMv2 { get; init; }

        /// <summary>Creatures with mana value 2 or less and power 2 or more. A variable
        /// power ("*") counts as 0, so it is never aggressive.</summary>
        internal int AggressiveTwoDrops { get; init; }

        /// <summary>Creatures with mana value 3 or less and power 3 or more; a variable
        /// power counts as 0.</summary>
        internal int MvThreePowerThree { get; init; }

        /// <summary>Creature curve by effect column: [column][cost bucket] = creatures.
        /// Columns as <see cref="CardAnalyzer.EffectColumn"/>.</summary>
        internal Dictionary<string, Dictionary<string, int>> CreatureCurveByColumn { get; init; } = [];

        /// <summary>Creature curve by colour identity: [colour][cost bucket] = creatures.
        /// A multicolour creature counts in each of its colours, an empty identity in
        /// "Colorless"; same rule as <see cref="CardsByIdentity"/>.</summary>
        internal Dictionary<string, Dictionary<string, int>> CreatureCurveByIdentity { get; init; } = [];

        /// <summary>Total card count analyzed (excludes basic lands + tagged cards, per existing rule).</summary>
        internal int TotalCards { get; init; }
    }

    /// <summary>
    /// One cost bucket of the creature profile. Power and toughness are summed over
    /// the creatures whose value is a plain integer only; variable values ("*", "X")
    /// are left out of the sums and of their counts, so an average is
    /// Sum / Count (none when Count is 0).
    /// </summary>
    internal sealed class CreatureCostRow
    {
        internal int Creatures { get; set; }

        /// <summary>Sum of the numeric powers.</summary>
        internal int PowerSum { get; set; }

        /// <summary>Creatures with a numeric power.</summary>
        internal int PowerCount { get; set; }

        /// <summary>Sum of the numeric toughnesses.</summary>
        internal int ToughnessSum { get; set; }

        /// <summary>Creatures with a numeric toughness.</summary>
        internal int ToughnessCount { get; set; }

        /// <summary>Creatures with <see cref="CreatureStats.IsEvasive"/>.</summary>
        internal int Evasive { get; set; }

        internal int Haste { get; set; }

        internal int Defender { get; set; }

        /// <summary>The field-by-field sum of <paramref name="rows"/>: the Total row of
        /// the cost table, in the report and on /stats alike.</summary>
        internal static CreatureCostRow Sum(IEnumerable<CreatureCostRow> rows)
        {
            CreatureCostRow total = new();
            foreach (CreatureCostRow row in rows)
            {
                total.Creatures += row.Creatures;
                total.PowerSum += row.PowerSum;
                total.PowerCount += row.PowerCount;
                total.ToughnessSum += row.ToughnessSum;
                total.ToughnessCount += row.ToughnessCount;
                total.Evasive += row.Evasive;
                total.Haste += row.Haste;
                total.Defender += row.Defender;
            }

            return total;
        }
    }

    /// <summary>
    /// One row of the answers-vs-targets table: how many DISTINCT non-land cards
    /// carry any flag of the group (a card with two flags of the group counts
    /// once), split by effect column, and how many cards those answers can hit.
    /// </summary>
    internal sealed class AnswerGroupCount
    {
        internal required string Label { get; init; }

        /// <summary>The flags that put a card in the group.</summary>
        internal required CardEffect Effects { get; init; }

        /// <summary>Cards the group's answers can aim at; null when no meaningful
        /// target set exists (counterspells).</summary>
        internal int? Targets { get; init; }

        internal Dictionary<string, int> ByColumn { get; init; } = [];

        /// <summary>Distinct cards of the group per colour identity (a WU card counts
        /// in W and in U, once each); see <see cref="AnalysisReport.CardsByIdentity"/>.</summary>
        internal Dictionary<string, int> ByIdentity { get; init; } = [];

        internal int Total { get; init; }
    }
}
