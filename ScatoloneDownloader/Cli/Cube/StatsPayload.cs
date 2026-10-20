using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// What the tagger's <c>/stats</c> page shows: sections 5-7 of
    /// <c>Cubo_Analysis.md</c> (effects by colour, answers against targets, bench by
    /// effect and colour), for the pool and for the bench. Built from the very same
    /// <see cref="CardAnalyzer"/> that <c>report</c> uses (<c>ForPool</c>, then
    /// <c>Analyze</c> / <c>AnalyzeBench</c>), so the page and the Markdown report
    /// cannot disagree; the only difference is that the tagger feeds it its live
    /// in-memory cards instead of the ones read from disk.
    /// </summary>
    /// <param name="Effects">Every single effect flag name, enum order.</param>
    /// <param name="Glossary">Tooltip text, same order as <paramref name="Effects"/>.</param>
    /// <param name="Pool">The active pool: <see cref="RatingTier.Pool"/> cards without a Banned/Token/Jolly status.</param>
    /// <param name="Bench">The <see cref="RatingTier.Bench"/> cards, shown beside the pool as the availability appendix.</param>
    internal sealed record StatsPayload(
        IReadOnlyList<string> Effects,
        IReadOnlyList<string> Glossary,
        StatsTier Pool,
        StatsTier Bench)
    {
        // The colour columns of the two views. Mono is the report's own one-column
        // split (a gold card is "Multi"); identity counts a card in every colour it has.
        private static readonly (string Key, string Name)[] MonoColumns =
        [
            ("W", "White"), ("U", "Blue"), ("B", "Black"), ("R", "Red"), ("G", "Green"),
            ("Multi", "Multi"), ("Colorless", "Colorless"),
        ];

        private static readonly (string Key, string Name)[] IdentityColumns =
        [
            ("W", "White"), ("U", "Blue"), ("B", "Black"), ("R", "Red"), ("G", "Green"),
            ("Colorless", "Colorless"),
        ];

        internal static StatsPayload Build(IEnumerable<Card> cards)
        {
            CardAnalyzer analyzer = CardAnalyzer.ForPool(cards);
            AnalysisReport pool = analyzer.Analyze();
            AnalysisReport bench = analyzer.AnalyzeBench();

            List<string> effects = EffectResolver.ToNames((CardEffect)~0);
            string[] glossary = EffectGlossary.DescribeAll(effects);

            // One row order for both tiers: the pool's (total descending, then name),
            // so the pool and bench tables line up row for row even where the bench's
            // own totals would sort differently.
            List<(string Name, CardEffect Flag)> order = effects
                .Select(n => (Name: n, Flag: EffectResolver.Parse([n])))
                .OrderByDescending(e => pool.EffectCounts.GetValueOrDefault(e.Flag, 0))
                .ThenBy(e => e.Name, StringComparer.Ordinal)
                .ToList();

            return new StatsPayload(
                effects,
                glossary,
                BuildTier("Pool", "pool", pool, order),
                BuildTier("Bench", "r2", bench, order));
        }

        private static StatsTier BuildTier(
            string label, string ratingParam, AnalysisReport report, List<(string Name, CardEffect Flag)> order)
        {
            StatsMatrix mono = BuildMatrix(
                report, MonoColumns, order,
                key => key == "Multi" ? "*multi" : ColorCategoryClassifier.ViewFolderName(key),
                report.CardsByColumn, report.ReviewedByColumn, report.UntaggedByColumn,
                report.EffectCountsByColumn);

            StatsMatrix identity = BuildMatrix(
                report, IdentityColumns, order,
                key => key == "Colorless" ? ColorCategoryClassifier.ViewFolderName(key) : "*has:" + key,
                report.CardsByIdentity, report.ReviewedByIdentity, report.UntaggedByIdentity,
                report.EffectCountsByIdentity);

            List<StatsAnswerGroup> answers = report.AnswerGroups
                .Select(g => new StatsAnswerGroup(
                    g.Label,
                    EffectResolver.ToNames(g.Effects),
                    Fill(MonoColumns, g.ByColumn),
                    Fill(IdentityColumns, g.ByIdentity),
                    g.Total,
                    g.Targets))
                .ToList();

            CreatureCostRow creatureTotal = CreatureCostRow.Sum(report.CreatureByCost.Values);
            StatsCreatures creatures = new(
                BuildCreatureRows(report),
                CreatureRow("", "Total", creatureTotal),
                new StatsCreatureSummary(
                    report.TotalCards,
                    report.CreatureCount,
                    report.CreaturesMv2,
                    report.AggressiveTwoDrops,
                    report.MvThreePowerThree,
                    creatureTotal.Evasive,
                    creatureTotal.Haste,
                    creatureTotal.Defender),
                BuildCurve(report, MonoColumns, key => key == "Multi" ? "*multi" : ColorCategoryClassifier.ViewFolderName(key), report.CreatureCurveByColumn),
                BuildCurve(report, IdentityColumns, key => key == "Colorless" ? ColorCategoryClassifier.ViewFolderName(key) : "*has:" + key, report.CreatureCurveByIdentity));

            return new StatsTier(label, ratingParam, report.TotalCards, mono, identity, answers, creatures);
        }

        private static List<StatsCreatureRow> BuildCreatureRows(AnalysisReport report)
        {
            return CostBucket.All
                .Select(b => CreatureRow(b, CostBucket.Label(b), report.CreatureByCost.GetValueOrDefault(b) ?? new CreatureCostRow()))
                .ToList();
        }

        private static StatsCreatureRow CreatureRow(string cost, string label, CreatureCostRow row)
        {
            return new StatsCreatureRow(
                cost,
                label,
                row.Creatures,
                row.PowerCount == 0 ? null : (double)row.PowerSum / row.PowerCount,
                row.ToughnessCount == 0 ? null : (double)row.ToughnessSum / row.ToughnessCount,
                row.Evasive,
                row.Haste,
                row.Defender);
        }

        /// <summary>The cost-by-colour creature matrix: a row per cost bucket, the count
        /// per column, the row total and the per-column creature total. The row total is
        /// the bucket's distinct creatures, not the sum of the columns: in the identity
        /// view a WU creature sits in two columns but is one creature.</summary>
        private static StatsCreatureCurve BuildCurve(
            AnalysisReport report,
            (string Key, string Name)[] columns,
            Func<string, string> colorParam,
            Dictionary<string, Dictionary<string, int>> curve)
        {
            int Cell(string column, string bucket) =>
                curve.GetValueOrDefault(column)?.GetValueOrDefault(bucket, 0) ?? 0;

            List<StatsCurveRow> rows = CostBucket.All
                .Select(b => new StatsCurveRow(
                    b,
                    CostBucket.Label(b),
                    columns.ToDictionary(c => c.Key, c => Cell(c.Key, b)),
                    report.CreatureByCost.GetValueOrDefault(b)?.Creatures ?? 0))
                .ToList();

            return new StatsCreatureCurve(
                columns.Select(c => new StatsColumn(c.Key, c.Name, colorParam(c.Key))).ToList(),
                rows,
                columns.ToDictionary(c => c.Key, c => CostBucket.All.Sum(b => Cell(c.Key, b))));
        }

        private static StatsMatrix BuildMatrix(
            AnalysisReport report,
            (string Key, string Name)[] columns,
            List<(string Name, CardEffect Flag)> order,
            Func<string, string> colorParam,
            Dictionary<string, int> cards,
            Dictionary<string, int> reviewed,
            Dictionary<string, int> untagged,
            Dictionary<string, Dictionary<CardEffect, int>> effectCounts)
        {
            List<StatsEffectRow> rows = order
                .Select(e => new StatsEffectRow(
                    e.Name,
                    columns.ToDictionary(
                        c => c.Key,
                        c => effectCounts.TryGetValue(c.Key, out Dictionary<CardEffect, int>? byFlag)
                            ? byFlag.GetValueOrDefault(e.Flag, 0)
                            : 0),
                    report.EffectCounts.GetValueOrDefault(e.Flag, 0)))
                .ToList();

            // Distinct totals come from the report, not from summing the columns: in
            // the identity view a WU card sits in two columns but is one card.
            return new StatsMatrix(
                columns.Select(c => new StatsColumn(c.Key, c.Name, colorParam(c.Key))).ToList(),
                Fill(columns, cards),
                Fill(columns, reviewed),
                Fill(columns, untagged),
                report.TotalCards,
                report.ReviewedByColumn.Values.Sum(),
                report.UntaggedEffectCount,
                rows);
        }

        /// <summary>Every column key with its count, zero included, so the page never
        /// has to guess a missing key.</summary>
        private static Dictionary<string, int> Fill((string Key, string Name)[] columns, Dictionary<string, int> counts)
        {
            return columns.ToDictionary(c => c.Key, c => counts.GetValueOrDefault(c.Key, 0));
        }
    }

    internal sealed record StatsTier(
        string Label,
        string RatingParam,
        int NonLand,
        StatsMatrix Mono,
        StatsMatrix Identity,
        IReadOnlyList<StatsAnswerGroup> Answers,
        StatsCreatures Creatures);

    /// <summary>The creature profile of a tier: the cost table, its summary counts and
    /// the cost-by-colour curve in both column views.</summary>
    /// <param name="Rows">One row per cost bucket, 0 to 6_Plus.</param>
    /// <param name="Total">The sum over <paramref name="Rows"/>; its Cost is empty.</param>
    internal sealed record StatsCreatures(
        IReadOnlyList<StatsCreatureRow> Rows,
        StatsCreatureRow Total,
        StatsCreatureSummary Summary,
        StatsCreatureCurve CurveMono,
        StatsCreatureCurve CurveIdentity);

    /// <summary>One cost bucket. <paramref name="Cost"/> is the tagger's <c>cost=</c> value
    /// ("6_Plus" for 6 or more), <paramref name="Label"/> its display form. The averages
    /// are null when no creature has a numeric value.</summary>
    internal sealed record StatsCreatureRow(
        string Cost,
        string Label,
        int Creatures,
        double? PowerAvg,
        double? ToughnessAvg,
        int Evasive,
        int Haste,
        int Defender);

    internal sealed record StatsCreatureSummary(
        int NonLand,
        int Creatures,
        int CreaturesMv2,
        int AggressiveTwoDrops,
        int MvThreePowerThree,
        int Evasive,
        int Haste,
        int Defender);

    /// <summary>Creatures per cost bucket (rows) and colour column; <paramref name="Totals"/>
    /// is the creature count of each column.</summary>
    internal sealed record StatsCreatureCurve(
        IReadOnlyList<StatsColumn> Columns,
        IReadOnlyList<StatsCurveRow> Rows,
        IReadOnlyDictionary<string, int> Totals);

    internal sealed record StatsCurveRow(
        string Cost,
        string Label,
        IReadOnlyDictionary<string, int> ByColumn,
        int Total);

    internal sealed record StatsMatrix(
        IReadOnlyList<StatsColumn> Columns,
        IReadOnlyDictionary<string, int> Cards,
        IReadOnlyDictionary<string, int> Reviewed,
        IReadOnlyDictionary<string, int> Untagged,
        int TotalCards,
        int TotalReviewed,
        int TotalUntagged,
        IReadOnlyList<StatsEffectRow> Rows);

    /// <summary>One column of a matrix. <paramref name="ColorParam"/> is the value the
    /// tagger's colour filter takes for it (a view folder name, or a pseudo-value).</summary>
    internal sealed record StatsColumn(string Key, string Name, string ColorParam);

    internal sealed record StatsEffectRow(string Effect, IReadOnlyDictionary<string, int> ByColumn, int Total);

    internal sealed record StatsAnswerGroup(
        string Label,
        IReadOnlyList<string> Effects,
        IReadOnlyDictionary<string, int> ByMono,
        IReadOnlyDictionary<string, int> ByIdentity,
        int Total,
        int? Targets);
}
