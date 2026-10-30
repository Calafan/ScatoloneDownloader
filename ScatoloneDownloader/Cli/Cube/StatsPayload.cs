using System.Globalization;
using System.Text.Json;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// What the tagger's <c>/stats</c> page shows, for the pool and for the bench: the
    /// pool's global distribution, category analysis and mana-value tables
    /// (<see cref="Overview"/>), then the effects by colour, the answers against
    /// targets, the bench by effect and colour and the creature profile. It is the
    /// one source of every stats file: <see cref="Build"/> reads the cube
    /// (<see cref="CardAnalyzer"/> <c>ForPool</c>, then <c>Analyze</c> /
    /// <c>AnalyzeBench</c>) and <see cref="BuildForList"/> a card list, which has
    /// no bench. Two renderers read it with the same sections and numbers: the page
    /// (live from the tagger, or frozen into a self-contained HTML file by
    /// <see cref="StatsReport"/>) and <see cref="StatsMarkdown"/>. The <c>report</c>
    /// command, the page's downloads and the <c>analyze</c> command all write that pair.
    /// </summary>
    /// <param name="Effects">Every single effect flag name, enum order.</param>
    /// <param name="Glossary">Tooltip text, same order as <paramref name="Effects"/>.</param>
    /// <param name="Pool">The active pool: <see cref="RatingTier.Pool"/> cards without a Banned/Token/Jolly status.</param>
    /// <param name="Bench">The <see cref="RatingTier.Bench"/> cards, shown beside the pool as the availability appendix; null for a card list, which has no bench.</param>
    /// <param name="Overview">The pool's distribution by type, cost and exact colour category.</param>
    /// <param name="BenchOverview">The same distribution for the bench; null when <paramref name="Bench"/> is.</param>
    /// <param name="Scope">What the tiers count: <see cref="CubeScope"/> or <see cref="ListScope"/>.</param>
    /// <param name="ViewNames">The names of the two column views, for the page's buttons and the Markdown sub-headings.</param>
    /// <param name="Buckets">The cost buckets in table order, with their labels.</param>
    /// <param name="Sections">The sections both renderers print, in order, with their titles and notes.</param>
    internal sealed record StatsPayload(
        IReadOnlyList<string> Effects,
        IReadOnlyList<string> Glossary,
        StatsTier Pool,
        StatsTier? Bench,
        StatsOverview Overview,
        StatsOverview? BenchOverview,
        string Scope,
        StatsViewNames ViewNames,
        IReadOnlyList<StatsBucket> Buckets,
        IReadOnlyList<StatsSection> Sections)
    {
        /// <summary>What the cube's tiers count (<see cref="Build"/>).</summary>
        internal const string CubeScope = "The pool is the cards rated 3–5 with no status; the bench is exactly ★2.";

        /// <summary>What a card list's "pool" counts (<see cref="BuildForList"/>).</summary>
        internal const string ListScope = "Every card of the list counts, whatever its rating or status in the cube.";

        private const string BenchLabel = "Bench (★2)";
        private const string CategoriesNote = "Exact colour identity; a land counts only under Lands.";

        /// <summary>
        /// How the payload is serialized: camelCase (the records are PascalCase), with
        /// dictionary keys (W, Multi, 1 White) left exactly as they are. Shared by
        /// <c>/api/stats</c> and by the frozen copy <see cref="StatsReport"/> embeds,
        /// so the page reads the same JSON live and frozen. The default encoder is
        /// kept: it escapes <c>&lt;</c>, so the JSON can sit inside a script tag.
        /// </summary>
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        // The colour columns of the two views. Mono is the analyzer's own one-column
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

        private static readonly StatsViewNames Views = new("mono + Multi", "colour incl. multi");

        internal static StatsPayload Build(IEnumerable<Card> cards)
        {
            CardAnalyzer analyzer = CardAnalyzer.ForPool(cards);
            return BuildFromReports(analyzer.Analyze(), analyzer.AnalyzeBench(), "Pool", CubeScope);
        }

        /// <summary>
        /// The payload of a card list (the <c>analyze</c> command): every card counts,
        /// whatever its rating or status, so the list is the whole "pool" under
        /// <paramref name="label"/>, and there is no bench.
        /// </summary>
        internal static StatsPayload BuildForList(IEnumerable<Card> cards, string label)
        {
            return BuildFromReports(new CardAnalyzer((cards ?? []).ToList()).Analyze(), null, label, ListScope);
        }

        private static StatsPayload BuildFromReports(AnalysisReport pool, AnalysisReport? bench, string label, string scope)
        {
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

            StatsTier poolTier = BuildTier(label, "pool", pool, order);
            StatsTier? benchTier = bench is null ? null : BuildTier(BenchLabel, "r2", bench, order);
            StatsOverview overview = BuildOverview(pool);

            return new StatsPayload(
                effects,
                glossary,
                poolTier,
                benchTier,
                overview,
                bench is null ? null : BuildOverview(bench),
                scope,
                Views,
                CostBucket.All.Select(b => new StatsBucket(b, CostBucket.Label(b))).ToList(),
                BuildSections(poolTier, benchTier, overview));
        }

        /// <summary>The sections in the order both renderers print them: the pool's
        /// tables, the bench's appendix when there is a bench (it sits between the
        /// pool's answers and its creature profile), then the creature profile and
        /// curve of each tier.</summary>
        private static List<StatsSection> BuildSections(StatsTier pool, StatsTier? bench, StatsOverview overview)
        {
            string p = pool.Label;
            List<StatsSection> sections =
            [
                new("types", "pool", p + " — global distribution", TypesNote(overview)),
                new("categories", "pool", p + " — by colour category", CategoriesNote),
                new("manaValue", "pool", p + " — mana value by colour category", null),
                new("creatureManaValue", "pool", p + " — creature mana value by colour category", null),
                new("effects", "pool", p + " — effects by colour", null),
                new("answers", "pool", p + " — answers vs targets", null),
            ];

            if (bench is not null)
            {
                sections.Add(new("categories", "bench", bench.Label + " — by colour category", CategoriesNote));
                sections.Add(new("manaValue", "bench", bench.Label + " — mana value by colour category", null));
                sections.Add(new("effects", "bench", bench.Label + " — available by effect and colour", null));
            }

            sections.Add(new("creatures", "pool", p + " — creatures by cost", CreatureSummary(pool.Creatures.Summary)));
            sections.Add(new("curve", "pool", p + " — creature curve by colour", null));

            if (bench is not null)
            {
                sections.Add(new("curve", "bench", bench.Label + " — creature curve by colour", null));
            }

            return sections;
        }

        // ------------------------------------------------------------- sentences

        private static string Plain(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        private static string OfNonLand(int n, int nonLand)
        {
            string percent = nonLand > 0 ? (n * 100.0 / nonLand).ToString("0.0", CultureInfo.InvariantCulture) : "0.0";
            return $"{Plain(n)} ({percent}%)";
        }

        private static string TypesNote(StatsOverview o)
        {
            string Pct(int n)
            {
                return o.NonLand > 0
                    ? Plain((int)Math.Round(n * 100.0 / o.NonLand, MidpointRounding.AwayFromZero))
                    : "0";
            }

            return $"{Plain(o.NonLand + o.Lands)} cards: {Plain(o.NonLand)} non-land + {Plain(o.Lands)} lands"
                + $" · permanents {Plain(o.Permanents)} ({Pct(o.Permanents)}%)"
                + $" · spells {Plain(o.Spells)} ({Pct(o.Spells)}%) of non-land"
                + $" · average mana value {o.AverageMv.ToString("0.00", CultureInfo.InvariantCulture)} (lands excluded)"
                + $" · pip density {o.PipDensity.ToString("0.00", CultureInfo.InvariantCulture)} (coloured pips per non-land card)"
                + $" · cards per colour: the Non-land row of “effects by colour”, with “{Views.Identity}”";
        }

        private static string CreatureSummary(StatsCreatureSummary s)
        {
            return "Creatures " + OfNonLand(s.Creatures, s.NonLand)
                + " · MV ≤ 2 " + OfNonLand(s.CreaturesMv2, s.NonLand)
                + " · aggressive 2-drops (MV ≤ 2, power ≥ 2) " + OfNonLand(s.AggressiveTwoDrops, s.NonLand)
                + " · MV ≤ 3 with power ≥ 3 " + OfNonLand(s.MvThreePowerThree, s.NonLand)
                + " · evasive " + OfNonLand(s.Evasive, s.NonLand)
                + " · haste " + OfNonLand(s.Haste, s.NonLand)
                + " · defender " + OfNonLand(s.Defender, s.NonLand)
                + " (percent of non-land)";
        }

        private static readonly (MacroType Macro, string Label)[] OverviewTypes =
        [
            (MacroType.Creature, "Creatures"),
            (MacroType.OtherPermanent, "Other permanents"),
            (MacroType.Spell, "Spells"),
        ];

        private static StatsOverview BuildOverview(AnalysisReport report)
        {
            List<StatsTypeRow> types = OverviewTypes
                .Select(t =>
                {
                    Dictionary<int, int> curve = report.CurveByMacroType.GetValueOrDefault(t.Macro) ?? [];
                    Dictionary<string, int> byCost = CostBucket.All.ToDictionary(
                        b => b,
                        b => curve.GetValueOrDefault(b == "6_Plus" ? 6 : int.Parse(b, CultureInfo.InvariantCulture), 0));
                    return new StatsTypeRow(
                        t.Macro.ToString(), t.Label, report.MacroTypeCounts.GetValueOrDefault(t.Macro, 0), byCost);
                })
                .ToList();

            Dictionary<string, int> curveTotal = CostBucket.All.ToDictionary(b => b, b => types.Sum(t => t.ByCost[b]));

            int spells = report.MacroTypeCounts.GetValueOrDefault(MacroType.Spell, 0);

            List<StatsCategoryGroup> groups = [];
            foreach ((string label, string[] categories) in CardAnalyzer.CategoryGroups)
            {
                List<StatsCategoryRow> rows = categories
                    .Where(c => report.ColorCategoryCounts.GetValueOrDefault(c, 0) > 0
                        || report.LandsByCategory.GetValueOrDefault(c, 0) > 0)
                    .Select(c => CategoryRow(report, c))
                    .ToList();
                if (rows.Count > 0)
                {
                    groups.Add(new StatsCategoryGroup(label, rows));
                }
            }

            // The creature average of the whole pool: the per-category creature cmc
            // distributions summed, over the creature count.
            double creatureCmcSum = report.CreatureCmcByCategory.Values
                .Sum(d => d.Sum(kv => kv.Key * kv.Value));
            StatsCategoryRow total = new(
                "",
                "Total",
                "",
                report.TotalCards,
                report.MacroTypeCounts.GetValueOrDefault(MacroType.Creature, 0),
                report.MacroTypeCounts.GetValueOrDefault(MacroType.OtherPermanent, 0),
                spells,
                report.AverageCmc,
                report.CreatureCount == 0 ? null : creatureCmcSum / report.CreatureCount,
                report.GlobalPipDensity,
                report.RatingTiers.Where(kv => kv.Key.Stars == 3).Sum(kv => kv.Value),
                report.RatingTiers.Where(kv => kv.Key.Stars == 4).Sum(kv => kv.Value),
                report.RatingTiers.Where(kv => kv.Key.Stars == 5).Sum(kv => kv.Value),
                report.LandCount,
                curveTotal,
                CostBucket.All.ToDictionary(b => b, b => report.CreatureByCost.GetValueOrDefault(b)?.Creatures ?? 0));

            return new StatsOverview(
                report.TotalCards,
                report.LandCount,
                report.TotalCards - spells,
                spells,
                report.AverageCmc,
                report.GlobalPipDensity,
                types,
                curveTotal,
                groups,
                total);
        }

        private static StatsCategoryRow CategoryRow(AnalysisReport report, string key)
        {
            Dictionary<MacroType, int> byType = report.MacroTypeByCategory.GetValueOrDefault(key) ?? [];
            int creatures = byType.GetValueOrDefault(MacroType.Creature, 0);

            return new StatsCategoryRow(
                key,
                ColorCategoryClassifier.Display(key),
                ColorCategoryClassifier.NumberedName(key),
                report.ColorCategoryCounts.GetValueOrDefault(key, 0),
                creatures,
                byType.GetValueOrDefault(MacroType.OtherPermanent, 0),
                byType.GetValueOrDefault(MacroType.Spell, 0),
                report.AverageCmcPerCategory.GetValueOrDefault(key, 0),
                creatures == 0 ? null : report.AverageCreatureCmcPerCategory.GetValueOrDefault(key, 0),
                report.PipDensityPerCategory.GetValueOrDefault(key, 0),
                report.RatingTiers.GetValueOrDefault((key, 3), 0),
                report.RatingTiers.GetValueOrDefault((key, 4), 0),
                report.RatingTiers.GetValueOrDefault((key, 5), 0),
                report.LandsByCategory.GetValueOrDefault(key, 0),
                FoldByCost(report.CmcByCategory.GetValueOrDefault(key)),
                FoldByCost(report.CreatureCmcByCategory.GetValueOrDefault(key)));
        }

        /// <summary>Folds raw (cmc, count) pairs into every <see cref="CostBucket"/>,
        /// zero included.</summary>
        private static Dictionary<string, int> FoldByCost(Dictionary<double, int>? byCmc)
        {
            Dictionary<string, int> folded = CostBucket.All.ToDictionary(b => b, _ => 0);
            foreach ((double cmc, int count) in byCmc ?? [])
            {
                folded[CostBucket.Of(cmc)] += count;
            }

            return folded;
        }

        private static StatsTier BuildTier(
            string label, string ratingParam, AnalysisReport report, List<(string Name, CardEffect Flag)> order)
        {
            StatsMatrix mono = BuildMatrix(
                report, MonoColumns, order,
                key => key == "Multi" ? "*multi" : ColorCategoryClassifier.NumberedName(key),
                report.CardsByColumn, report.ReviewedByColumn, report.UntaggedByColumn,
                report.EffectCountsByColumn);

            StatsMatrix identity = BuildMatrix(
                report, IdentityColumns, order,
                key => key == "Colorless" ? ColorCategoryClassifier.NumberedName(key) : "*has:" + key,
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
                BuildCurve(report, MonoColumns, key => key == "Multi" ? "*multi" : ColorCategoryClassifier.NumberedName(key), report.CreatureCurveByColumn),
                BuildCurve(report, IdentityColumns, key => key == "Colorless" ? ColorCategoryClassifier.NumberedName(key) : "*has:" + key, report.CreatureCurveByIdentity));

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

    /// <summary>The names of the two column views of a matrix: the analyzer's own
    /// one-column split, and the colour identity.</summary>
    internal sealed record StatsViewNames(string Mono, string Identity);

    /// <summary>One cost bucket: <paramref name="Key"/> is the tagger's <c>cost=</c>
    /// value, <paramref name="Label"/> its display form.</summary>
    internal sealed record StatsBucket(string Key, string Label);

    /// <summary>One section of the stats files. <paramref name="Kind"/> says which table
    /// it holds (types, categories, manaValue, creatureManaValue, effects, answers,
    /// creatures, curve), <paramref name="Tier"/> whose numbers (pool or bench),
    /// <paramref name="Note"/> the line above the table, if any.</summary>
    internal sealed record StatsSection(string Kind, string Tier, string Title, string? Note);

    /// <summary>The pool's overview: counts, the non-land curve by type, and the exact
    /// colour categories in their groups. Lands carry no mana value, so they appear
    /// only in <paramref name="Lands"/> and in each category's own Lands count.</summary>
    /// <param name="NonLand">Non-land pool cards.</param>
    /// <param name="Lands">Lands in the pool.</param>
    /// <param name="Permanents">Non-land cards that are not spells.</param>
    /// <param name="Spells">Non-land cards that are not permanents (instants and sorceries).</param>
    /// <param name="AverageMv">Average mana value, lands excluded.</param>
    /// <param name="PipDensity">Coloured pips per non-land card.</param>
    /// <param name="Types">Creature, OtherPermanent, Spell, in that order.</param>
    /// <param name="CurveTotal">The non-land curve: the sum of <paramref name="Types"/> per cost bucket.</param>
    /// <param name="Groups">The category groups that hold at least one row.</param>
    /// <param name="Total">The whole pool as one row; its Key and ColorParam are empty.</param>
    internal sealed record StatsOverview(
        int NonLand,
        int Lands,
        int Permanents,
        int Spells,
        double AverageMv,
        double PipDensity,
        IReadOnlyList<StatsTypeRow> Types,
        IReadOnlyDictionary<string, int> CurveTotal,
        IReadOnlyList<StatsCategoryGroup> Groups,
        StatsCategoryRow Total);

    /// <summary>One macro type. <paramref name="Macro"/> is the tagger's <c>type=</c>
    /// value; <paramref name="ByCost"/> has every <see cref="CostBucket"/> key.</summary>
    internal sealed record StatsTypeRow(
        string Macro,
        string Label,
        int Cards,
        IReadOnlyDictionary<string, int> ByCost);

    internal sealed record StatsCategoryGroup(string Label, IReadOnlyList<StatsCategoryRow> Rows);

    /// <summary>One exact colour category. <paramref name="ColorParam"/> is the tagger's
    /// <c>color=</c> value (e.g. "2 Azorius"). A row exists when the category has
    /// non-land cards or lands; <paramref name="CreatureAverageMv"/> is null without
    /// creatures. <paramref name="ByCost"/> and <paramref name="CreatureByCost"/> have
    /// every <see cref="CostBucket"/> key and leave lands out.</summary>
    internal sealed record StatsCategoryRow(
        string Key,
        string Name,
        string ColorParam,
        int Cards,
        int Creatures,
        int OtherPermanents,
        int Spells,
        double AverageMv,
        double? CreatureAverageMv,
        double PipDensity,
        int Rated3,
        int Rated4,
        int Rated5,
        int Lands,
        IReadOnlyDictionary<string, int> ByCost,
        IReadOnlyDictionary<string, int> CreatureByCost);

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
    /// <param name="Summary">The creature counts behind the profile (two-drops, evasive, haste, ...).</param>
    /// <param name="CurveMono">The cost-by-colour curve, columns by mono colour.</param>
    /// <param name="CurveIdentity">The cost-by-colour curve, columns by colour identity.</param>
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
    /// tagger's colour filter takes for it (a category name such as "2 Azorius", or a pseudo-value).</summary>
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
