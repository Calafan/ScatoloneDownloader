using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// What the tagger's <c>/stats</c> page shows: sections 5-7 of
    /// <c>Cubo_Analysis.md</c> (effects by colour, answers against targets, bench by
    /// effect and colour), for the pool and for the bench. Built from the very same
    /// <see cref="CardAnalyzer"/> that <c>build-views</c> uses (<c>ForPool</c>, then
    /// <c>Analyze</c> / <c>AnalyzeBench</c>), so the page and the Markdown report
    /// cannot disagree; the only difference is that the tagger feeds it its live
    /// in-memory cards instead of the ones read from disk.
    /// </summary>
    /// <param name="Effects">Every single effect flag name, enum order.</param>
    /// <param name="Glossary">Tooltip text, same order as <paramref name="Effects"/>.</param>
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

            return new StatsTier(label, ratingParam, report.TotalCards, mono, identity, answers);
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
        IReadOnlyList<StatsAnswerGroup> Answers);

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
