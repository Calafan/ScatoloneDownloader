using System.Text;

namespace ScatoloneDownloader.Mtg
{
    internal class CardAnalyzer
    {
        // Render order for the 4 MacroType rows in the Markdown report.
        private static readonly MacroType[] MacroTypeOrder =
        [
            MacroType.Creature,
            MacroType.Land,
            MacroType.OtherPermanent,
            MacroType.Spell,
        ];

        // Order in which per-color sections appear: monocolor, guilds, shards/wedges, 4-5, colorless.
        private static readonly List<string> CategoryOrder =
        [
            "W", "U", "B", "R", "G",
            "WU", "UB", "BR", "RG", "GW", "WB", "UR", "BG", "RW", "GU",
            "WUB", "UBR", "BRG", "RGW", "GWU", "WBG", "URW", "BUG", "RWB", "GUR",
            "4_5_Colors", "Colorless",
        ];

        // Every CardEffect member in declared order, read once: the tallies below
        // walk it per card, and Enum.GetValues allocates a fresh array each call.
        private static readonly CardEffect[] AllEffects = Enum.GetValues<CardEffect>();

        // Column order of the effect matrix and of the answers table.
        private static readonly string[] EffectColumns =
            ["W", "U", "B", "R", "G", "Multi", "Colorless"];

        private static readonly string[] EffectColumnNames =
            ["White", "Blue", "Black", "Red", "Green", "Multi", "Colorless"];

        /// <summary>
        /// Answers-vs-targets groups. Each target set is what the group's flags can
        /// actually hit, not what the card happens to be: creature answers
        /// (Removal, RemovePermanent, Wipe, and the soft ones Pacify, Bounce, Steal)
        /// aim at the non-land creatures. Disenchant's own definition sends artifact
        /// CREATURES to Removal, so the artifact/enchantment group aims only at the
        /// NON-creature artifacts and enchantments. Counterspells aim at spells
        /// cast, which no card count can stand for, hence no target. Land
        /// destruction aims at the analysed non-basic lands.
        /// </summary>
        private static readonly (string Label, CardEffect Effects, AnswerTargets Targets)[] AnswerGroupDefinitions =
        [
            ("Creature removal", CardEffect.Removal | CardEffect.RemovePermanent | CardEffect.Wipe, AnswerTargets.Creatures),
            ("Soft creature answers", CardEffect.Pacify | CardEffect.Bounce | CardEffect.Steal, AnswerTargets.Creatures),
            ("Artifact/enchantment removal", CardEffect.Disenchant | CardEffect.RemovePermanent, AnswerTargets.NonCreatureArtifactsAndEnchantments),
            ("Counterspells", CardEffect.Counter, AnswerTargets.None),
            ("Land destruction", CardEffect.LandDestruction, AnswerTargets.Lands),
        ];

        private enum AnswerTargets
        {
            None,
            Creatures,
            NonCreatureArtifactsAndEnchantments,
            Lands,
        }

        private readonly List<Card> analyzedCards;

        // Rating-2 cards, reported as an appendix (section 7) and never mixed into
        // the metrics above it — they are candidates, not pool.
        private readonly List<Card> benchCards;

        internal CardAnalyzer(List<Card> cards)
            : this(cards, [])
        {
        }

        internal CardAnalyzer(List<Card> cards, List<Card> bench)
        {
            analyzedCards = cards ?? [];
            benchCards = bench ?? [];
        }

        /// <summary>Builds an analyzer over the active pool only — <see cref="RatingTier.Pool"/>
        /// and no Banned/Token/Jolly status — so the cube report matches what the
        /// views surface (and the <see cref="CardStatus"/> doc), plus the
        /// <see cref="RatingTier.Bench"/> cards for the availability appendix. The
        /// tier boundaries come from <see cref="RatingTierClassifier"/> rather than
        /// a literal, so the report can never disagree with view generation or
        /// metadata storage about where a rating belongs. The plain constructor
        /// analyzes whatever it is given, which the download <c>analyze</c>/<c>files</c>
        /// path relies on (those cards carry no rating).</summary>
        internal static CardAnalyzer ForPool(IEnumerable<Card> cards)
        {
            List<Card> normal = (cards ?? []).Where(c => c.Status == CardStatus.None).ToList();

            return new CardAnalyzer(
                normal.Where(c => RatingTierClassifier.Classify(c.Rating) == RatingTier.Pool).ToList(),
                normal.Where(c => RatingTierClassifier.Classify(c.Rating) == RatingTier.Bench).ToList());
        }

        /// <summary>Maps a <see cref="Card.ColorCategory"/> to its effect column:
        /// mono colours and Colorless map to themselves, every other bucket
        /// (guilds, shards, wedges, 4-5 colours) to "Multi".</summary>
        internal static string EffectColumn(string colorCategory)
        {
            return colorCategory switch
            {
                "W" or "U" or "B" or "R" or "G" or "Colorless" => colorCategory,
                _ => "Multi",
            };
        }

        /// <summary>The identity columns a card counts in: each of its W/U/B/R/G
        /// letters, or "Colorless" when it has none. Never "Multi".</summary>
        private static List<string> IdentityColumns(Card card)
        {
            List<string> colours = card.ColorIdentity
                .Where(c => c is "W" or "U" or "B" or "R" or "G")
                .Distinct()
                .ToList();

            return colours.Count > 0 ? colours : ["Colorless"];
        }

        /// <summary>The same analysis over the bench (rating-2) cards of this analyzer
        /// instead of the pool, so the live stats page and <c>report</c> report the
        /// bench with exactly the same code.</summary>
        internal AnalysisReport AnalyzeBench()
        {
            return new CardAnalyzer(benchCards).Analyze();
        }

        private static int GetPercentage(int value, int total)
        {
            return total != 0 ? (int)Math.Round(value * 100.0 / total) : 0;
        }

        /// <summary>
        /// Renders the full Markdown analysis from the <see cref="AnalysisReport"/>.
        /// </summary>
        internal void SaveAnalysis(string path)
        {
            AnalysisReport report = Analyze();

            StringBuilder sb = new();

            sb.AppendLine("# Scatolone Cube - Analysis Report");
            sb.AppendLine();

            AppendGlobalSection(sb, report);
            AppendColorDistribution(sb, report);
            AppendLandsSection(sb, report);
            AppendCategoryAnalysis(sb, report);
            AppendEffectDistribution(sb, report);
            AppendAnswersVsTargets(sb, report);
            AppendBenchSection(sb, benchCards.Count == 0 ? null : AnalyzeBench());

            using StreamWriter writer = new(path);
            writer.Write(sb.ToString());
        }

        private static void AppendGlobalSection(StringBuilder sb, AnalysisReport report)
        {
            int totalWithLands = report.TotalCards + report.LandCount;
            int totalSpells = report.MacroTypeCounts.GetValueOrDefault(MacroType.Spell, 0);
            int totalPermanents = report.TotalCards - totalSpells; // Non-land permanents

            sb.AppendLine($"## 1. Global Distribution ({totalWithLands} Cards)");
            sb.AppendLine($"*   **Permanents:** {totalPermanents} ({GetPercentage(totalPermanents, report.TotalCards)}%) | **Spells:** {totalSpells} ({GetPercentage(totalSpells, report.TotalCards)}%)");
            sb.AppendLine($"*   **Global Average CMC:** {report.AverageCmc:0.##} *(Excluding Lands)*");
            sb.AppendLine($"*   **Global Pip Density:** {report.GlobalPipDensity:0.##} *(Avg colored pips per non-land card)*");
            sb.AppendLine();

            sb.AppendLine("| MacroType | Count | Percentage |");
            sb.AppendLine("| :--- | :--- | :--- |");

            foreach (MacroType mt in MacroTypeOrder)
            {
                int count = report.MacroTypeCounts.GetValueOrDefault(mt, 0);
                sb.AppendLine($"| {mt}s | {count} | {GetPercentage(count, totalWithLands)}% |");
            }

            sb.AppendLine();

            // Aggregate all CMC buckets for global stats (0 through 6+)
            string cmcString = FormatCmcDistribution(
                report.CurveByMacroType.Values
                    .SelectMany(macroCurve => macroCurve)
                    .Select(kvp => new KeyValuePair<double, int>(kvp.Key, kvp.Value)),
                " | ");

            sb.AppendLine($"**Global CMC:** {cmcString}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void AppendColorDistribution(StringBuilder sb, AnalysisReport report)
        {
            sb.AppendLine("## 2. Color Distribution");
            sb.AppendLine("*Excludes Lands. Multicolored cards contribute to every color in their color identity.*");
            sb.AppendLine();

            sb.AppendLine("| Color | Count | Percentage |");
            sb.AppendLine("| :--- | :--- | :--- |");

            string[] colorOrder = ["W", "U", "B", "R", "G"];
            string[] colorNames = ["White", "Blue", "Black", "Red", "Green"];

            for (int i = 0; i < colorOrder.Length; i++)
            {
                report.IndividualColorCounts.TryGetValue(colorOrder[i], out int count);
                if (count == 0) continue;

                int pct = GetPercentage(count, report.TotalCards);
                sb.AppendLine($"| **{colorNames[i]}** | {count} | {pct}% |");
            }

            if (report.ColorlessCount > 0)
            {
                int pct = GetPercentage(report.ColorlessCount, report.TotalCards);
                sb.AppendLine($"| **Colorless** | {report.ColorlessCount} | {pct}% |");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void AppendLandsSection(StringBuilder sb, AnalysisReport report)
        {
            if (report.LandCount == 0) return;

            sb.AppendLine($"## 3. Lands Distribution ({report.LandCount} Cards)");
            sb.AppendLine("*Lands are grouped by Color Identity (e.g. dual lands). They are excluded from CMC statistics.*");
            sb.AppendLine();

            sb.AppendLine("| Category | Count |");
            sb.AppendLine("| :--- | :--- |");

            foreach (string category in CategoryOrder)
            {
                if (!report.LandsByCategory.TryGetValue(category, out int count) || count == 0)
                {
                    continue;
                }

                string name = ColorCategoryClassifier.Display(category);
                sb.AppendLine($"| {name} | {count} |");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void AppendCategoryAnalysis(StringBuilder sb, AnalysisReport report)
        {
            sb.AppendLine("## 4. Category Analysis (Excluding Lands)");
            sb.AppendLine();

            Dictionary<string, string[]> groups = new()
            {
                { "### Monocolor", ["W", "U", "B", "R", "G"] },
                { "### Guilds (2-Color)", ["WU", "UB", "BR", "RG", "GW", "WB", "UR", "BG", "RW", "GU"] },
                { "### Shards & Wedges (3-Color)", ["WUB", "UBR", "BRG", "RGW", "GWU", "WBG", "URW", "BUG", "RWB", "GUR"] },
                { "### 4-5 Colors & Colorless", ["4_5_Colors", "Colorless"] }
            };

            foreach (KeyValuePair<string, string[]> group in groups)
            {
                bool hasCardsInGroup = group.Value.Any(cat => report.ColorCategoryCounts.TryGetValue(cat, out int c) && c > 0);
                if (!hasCardsInGroup) continue;

                sb.AppendLine(group.Key);
                sb.AppendLine();
                sb.AppendLine("| Category | Cards | Perms % | Spells % | Creatures % | Avg CMC | Cr. Avg CMC | Pip Dens | All CMC Dist (0-6+) | Cr. CMC Dist (0-6+) | Rating (3★/4★/5★) |");
                sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");

                foreach (string category in group.Value)
                {
                    if (!report.ColorCategoryCounts.TryGetValue(category, out int catTotal) || catTotal == 0)
                    {
                        continue;
                    }

                    string name = ColorCategoryClassifier.Display(category);
                    Dictionary<MacroType, int> typeCounts = report.MacroTypeByCategory[category];

                    int catSpells = typeCounts.GetValueOrDefault(MacroType.Spell, 0);
                    int catCreatures = typeCounts.GetValueOrDefault(MacroType.Creature, 0);
                    int catPermanents = catTotal - catSpells;

                    double catAvgCmc = report.AverageCmcPerCategory.GetValueOrDefault(category, 0);
                    double catAvgCreatureCmc = report.AverageCreatureCmcPerCategory.GetValueOrDefault(category, 0);
                    double catPipDensity = report.PipDensityPerCategory.GetValueOrDefault(category, 0);

                    string cmcString = FormatCmcDistribution(report.CmcByCategory.GetValueOrDefault(category, []));
                    string creatureCmcString = catCreatures > 0
                        ? FormatCmcDistribution(report.CreatureCmcByCategory.GetValueOrDefault(category, []))
                        : "-";

                    int t3 = report.RatingTiers.GetValueOrDefault((category, 3), 0);
                    int t4 = report.RatingTiers.GetValueOrDefault((category, 4), 0);
                    int t5 = report.RatingTiers.GetValueOrDefault((category, 5), 0);
                    string tiers = (t3 > 0 || t4 > 0 || t5 > 0) ? $"{t3} / {t4} / {t5}" : "-";

                    string crCmcFormatted = catCreatures > 0 ? $"{catAvgCreatureCmc:0.##}" : "-";

                    sb.AppendLine($"| **{name}** | {catTotal} | {GetPercentage(catPermanents, catTotal)}% | {GetPercentage(catSpells, catTotal)}% | {GetPercentage(catCreatures, catTotal)}% | {catAvgCmc:0.##} | {crCmcFormatted} | {catPipDensity:0.##} | {cmcString} | {creatureCmcString} | {tiers} |");
                }

                sb.AppendLine();
            }
        }

        private static bool HasAnyEffect(AnalysisReport report)
        {
            return report.TotalCards - report.UntaggedEffectCount > 0;
        }

        private static void AppendEffectDistribution(StringBuilder sb, AnalysisReport report)
        {
            int tagged = report.TotalCards - report.UntaggedEffectCount;

            // Nothing tagged (the download analyze/files path): keep the flat
            // report exactly as it was before the colour matrix existed.
            if (!HasAnyEffect(report))
            {
                sb.AppendLine("## 5. Effect Distribution (Excluding Lands)");
                sb.AppendLine("*A card can carry several effects, so counts overlap and their sum exceeds the card total.*");
                sb.AppendLine($"*   **Tagged:** {tagged} / {report.TotalCards} ({GetPercentage(tagged, report.TotalCards)}%) | **Untagged:** {report.UntaggedEffectCount}");
                sb.AppendLine();
                sb.AppendLine("| Effect | Cards | % of Non-Land |");
                sb.AppendLine("| :--- | :--- | :--- |");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("## 5. Effects by Color (Excluding Lands)");
            sb.AppendLine("*A card can carry several effects, so counts overlap and their sum exceeds the card total.*");
            sb.AppendLine($"*   **Tagged:** {tagged} / {report.TotalCards} ({GetPercentage(tagged, report.TotalCards)}%) | **Untagged:** {report.UntaggedEffectCount}");
            sb.AppendLine("*Columns: mono-coloured cards by colour, Multi = two or more colours, Colorless = no colour identity; each card counts in exactly one column.*");
            sb.AppendLine();

            AppendEffectMatrix(sb, report);
        }

        /// <summary>
        /// Renders the effect-by-colour matrix: card totals per column, then how
        /// much of each column a human reviewed (the rest are the classifier's
        /// proposals, so a count weighs only as much as its reviewed share), the
        /// untagged remainder, and one row per effect that occurs at all. Shared by
        /// the pool section and the bench appendix so the two read identically.
        /// </summary>
        private static void AppendEffectMatrix(StringBuilder sb, AnalysisReport report)
        {
            sb.AppendLine("| Effect | " + string.Join(" | ", EffectColumnNames) + " | Total |");
            sb.AppendLine("| :--- | " + string.Concat(Enumerable.Repeat(":--- | ", EffectColumns.Length)) + ":--- |");

            sb.AppendLine("| **Non-land cards** | "
                + string.Join(" | ", EffectColumns.Select(c => report.CardsByColumn.GetValueOrDefault(c, 0).ToString()))
                + $" | {report.TotalCards} |");

            sb.AppendLine("| **Reviewed** | "
                + string.Join(" | ", EffectColumns.Select(c => FormatShare(report.ReviewedByColumn.GetValueOrDefault(c, 0), report.CardsByColumn.GetValueOrDefault(c, 0))))
                + $" | {FormatShare(report.ReviewedByColumn.Values.Sum(), report.TotalCards)} |");

            sb.AppendLine("| **Untagged** | "
                + string.Join(" | ", EffectColumns.Select(c => FormatShare(report.UntaggedByColumn.GetValueOrDefault(c, 0), report.CardsByColumn.GetValueOrDefault(c, 0))))
                + $" | {FormatShare(report.UntaggedEffectCount, report.TotalCards)} |");

            IEnumerable<KeyValuePair<CardEffect, int>> rows = report.EffectCounts
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => (int)kvp.Key);

            foreach ((CardEffect effect, int count) in rows)
            {
                IEnumerable<string> cells = EffectColumns.Select(c => FormatShare(
                    report.EffectCountsByColumn.GetValueOrDefault(c)?.GetValueOrDefault(effect, 0) ?? 0,
                    report.CardsByColumn.GetValueOrDefault(c, 0)));

                sb.AppendLine($"| {effect} | {string.Join(" | ", cells)} | {FormatShare(count, report.TotalCards)} |");
            }

            sb.AppendLine();
        }

        /// <summary>"n (p%)" of a column, or "-" when the count (or the column) is empty.</summary>
        private static string FormatShare(int count, int columnTotal)
        {
            return count == 0 || columnTotal == 0 ? "-" : $"{count} ({GetPercentage(count, columnTotal)}%)";
        }

        private static string FormatCount(int count)
        {
            return count == 0 ? "-" : count.ToString();
        }

        /// <summary>
        /// Section 6 — for each family of answers, how many distinct cards the pool
        /// holds per colour against how many cards they can hit. Omitted when
        /// nothing is tagged, since the table would be all dashes.
        /// </summary>
        private static void AppendAnswersVsTargets(StringBuilder sb, AnalysisReport report)
        {
            if (!HasAnyEffect(report))
            {
                return;
            }

            sb.AppendLine("## 6. Answers vs Targets");
            sb.AppendLine("*Distinct cards per group: a card is counted once even when it carries several flags of the group. \"Targets per answer\" = targets / answering cards.*");
            sb.AppendLine();
            sb.AppendLine("| Answers | Tags | " + string.Join(" | ", EffectColumnNames) + " | Total | Targets | Targets per answer |");
            sb.AppendLine("| :--- | :--- | " + string.Concat(Enumerable.Repeat(":--- | ", EffectColumns.Length)) + ":--- | :--- | :--- |");

            foreach (AnswerGroupCount group in report.AnswerGroups)
            {
                string tags = string.Join(", ", AllEffects
                    .Where(f => f != CardEffect.None && group.Effects.HasFlag(f))
                    .Select(f => f.ToString()));
                string cells = string.Join(" | ", EffectColumns.Select(c => FormatCount(group.ByColumn.GetValueOrDefault(c, 0))));
                string targets = group.Targets.HasValue ? group.Targets.Value.ToString() : "-";
                string ratio = group.Targets.HasValue && group.Total > 0
                    ? ((double)group.Targets.Value / group.Total).ToString("0.#")
                    : "-";

                sb.AppendLine($"| **{group.Label}** | {tags} | {cells} | {FormatCount(group.Total)} | {targets} | {ratio} |");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Section 7 — what is available on the bench (rating 2) to fill a hole the
        /// sections above just exposed. Rendered from a full
        /// <see cref="AnalysisReport"/> computed over the bench cards alone, so the
        /// counting rules (basic lands and legacy-tagged cards skipped, lands split
        /// out, effect counts overlapping) are exactly the pool's; only the three
        /// axes a hole is actually diagnosed on are shown — color, cost, effect.
        /// Omitted entirely when there is no bench, so the download
        /// <c>analyze</c>/<c>files</c> report is unchanged.
        /// </summary>
        private static void AppendBenchSection(StringBuilder sb, AnalysisReport? bench)
        {
            if (bench is null || (bench.TotalCards == 0 && bench.LandCount == 0))
            {
                return;
            }

            int tagged = bench.TotalCards - bench.UntaggedEffectCount;

            sb.AppendLine($"## 7. Bench Availability ({bench.TotalCards + bench.LandCount} Cards, Rating 2)");
            sb.AppendLine("*Cards cut by a hair. They are NOT counted in any section above — this is the");
            sb.AppendLine("shortlist to promote from when a section above shows a hole.");
            sb.AppendLine("Raise a card to 3 in the tagger and the next `report` counts it in the pool.*");
            sb.AppendLine($"*   **Non-Land:** {bench.TotalCards} | **Lands:** {bench.LandCount} | **Tagged:** {tagged} ({GetPercentage(tagged, bench.TotalCards)}%)");
            sb.AppendLine();

            sb.AppendLine("### Available by Color and Cost");
            sb.AppendLine();
            sb.AppendLine("| Category | Cards | Creatures | CMC Dist (0-6+) |");
            sb.AppendLine("| :--- | :--- | :--- | :--- |");

            foreach (string category in CategoryOrder)
            {
                if (!bench.ColorCategoryCounts.TryGetValue(category, out int count) || count == 0)
                {
                    continue;
                }

                string name = ColorCategoryClassifier.Display(category);
                int creatures = bench.MacroTypeByCategory.GetValueOrDefault(category, [])
                    .GetValueOrDefault(MacroType.Creature, 0);
                string cmcString = FormatCmcDistribution(bench.CmcByCategory.GetValueOrDefault(category, []));

                sb.AppendLine($"| **{name}** | {count} | {creatures} | {cmcString} |");
            }

            sb.AppendLine();

            // Lands are kept out of the cost table (their CMC says nothing) and out
            // of the effect counts, exactly as in the pool sections — so without
            // this they would be invisible past the header count, and a bench dual
            // is precisely what a "short on fixing" hole wants.
            if (bench.LandCount > 0)
            {
                sb.AppendLine("### Available Lands");
                sb.AppendLine();
                sb.AppendLine("| Category | Cards |");
                sb.AppendLine("| :--- | :--- |");

                foreach (string category in CategoryOrder)
                {
                    if (!bench.LandsByCategory.TryGetValue(category, out int landCount) || landCount == 0)
                    {
                        continue;
                    }

                    sb.AppendLine($"| {ColorCategoryClassifier.Display(category)} | {landCount} |");
                }

                sb.AppendLine();
            }

            if (!HasAnyEffect(bench))
            {
                return;
            }

            sb.AppendLine("### Available by Effect and Color");
            sb.AppendLine();
            AppendEffectMatrix(sb, bench);
        }

        /// <summary>Folds raw (cmc, count) pairs into the report's 0..6+ buckets and
        /// renders them in order, joined by <paramref name="separator"/>. The one
        /// bucketing both the global line and the per-category tables use.</summary>
        private static string FormatCmcDistribution(IEnumerable<KeyValuePair<double, int>> rawCmc, string separator = " ")
        {
            Dictionary<int, int> buckets = [];
            for (int i = 0; i <= 6; i++) buckets[i] = 0;

            foreach ((double cmc, int count) in rawCmc)
            {
                int bucket = cmc >= 6 ? 6 : (int)cmc;
                buckets[bucket] += count;
            }

            IEnumerable<string> parts = buckets.OrderBy(k => k.Key)
                .Select(kvp => kvp.Key == 6 ? $"`6+: {kvp.Value}`" : $"`{kvp.Key}: {kvp.Value}`");

            return string.Join(separator, parts);
        }

        internal AnalysisReport Analyze()
        {
            IEnumerable<Card> nonLands = analyzedCards
                .Where(c => !c.IsBasicLand && string.IsNullOrEmpty(c.Tag) && c.MacroType != MacroType.Land)
                .ToList();

            IEnumerable<Card> lands = analyzedCards
                .Where(c => !c.IsBasicLand && string.IsNullOrEmpty(c.Tag) && c.MacroType == MacroType.Land)
                .ToList();

            Dictionary<string, int> landsByCategory = [];
            foreach (Card c in lands)
            {
                landsByCategory.TryGetValue(c.ColorCategory, out int cur);
                landsByCategory[c.ColorCategory] = cur + 1;
            }

            int total = nonLands.Count();

            Dictionary<MacroType, int> macroTypeCounts = new()
            {
                { MacroType.Creature, 0 },
                { MacroType.Land, lands.Count() },
                { MacroType.OtherPermanent, 0 },
                { MacroType.Spell, 0 },
            };
            foreach (Card c in nonLands)
            {
                macroTypeCounts[c.MacroType]++;
            }

            Dictionary<string, int> colorCategoryCounts = [];
            foreach (Card c in nonLands)
            {
                string cat = c.ColorCategory;
                colorCategoryCounts.TryGetValue(cat, out int cur);
                colorCategoryCounts[cat] = cur + 1;
            }

            Dictionary<(string, int), int> ratingTiers = [];
            foreach (Card c in nonLands)
            {
                if (c.Rating < 3) continue;
                (string ColorCategory, int Rating) key = (c.ColorCategory, c.Rating);
                ratingTiers.TryGetValue(key, out int cur);
                ratingTiers[key] = cur + 1;
            }

            Dictionary<MacroType, Dictionary<int, int>> curveByMacroType = new()
            {
                { MacroType.Creature, [] },
                { MacroType.Spell, [] },
                { MacroType.OtherPermanent, [] },
                { MacroType.Land, [] },
            };
            foreach (Card c in nonLands)
            {
                int bucket = c.Cmc >= 6 ? 6 : (int)c.Cmc;
                if (!curveByMacroType.TryGetValue(c.MacroType, out Dictionary<int, int>? macro))
                {
                    macro = [];
                    curveByMacroType[c.MacroType] = macro;
                }
                macro.TryGetValue(bucket, out int cur);
                macro[bucket] = cur + 1;
            }

            Dictionary<string, Dictionary<MacroType, int>> macroByCat = [];
            Dictionary<string, Dictionary<double, int>> cmcByCat = [];
            Dictionary<string, Dictionary<double, int>> creatureCmcByCat = []; // Creature-specific CMC distribution
            Dictionary<string, double> avgCmcByCat = [];
            Dictionary<string, double> avgCreatureCmcByCat = [];

            foreach (IGrouping<string, Card> g in nonLands.GroupBy(c => c.ColorCategory))
            {
                Dictionary<MacroType, int> typeCounts = new()
                {
                    { MacroType.Creature, 0 },
                    { MacroType.Land, 0 },
                    { MacroType.OtherPermanent, 0 },
                    { MacroType.Spell, 0 },
                };
                Dictionary<double, int> cmcCounts = [];
                Dictionary<double, int> creatureCmcCounts = [];

                double sumCmc = 0;
                double sumCreatureCmc = 0;
                int catTotal = 0;

                foreach (Card c in g)
                {
                    typeCounts[c.MacroType]++;

                    // Total CMC for the category
                    cmcCounts.TryGetValue(c.Cmc, out int v);
                    cmcCounts[c.Cmc] = v + 1;
                    sumCmc += c.Cmc;

                    // Creature-specific CMC
                    if (c.MacroType == MacroType.Creature)
                    {
                        creatureCmcCounts.TryGetValue(c.Cmc, out int cv);
                        creatureCmcCounts[c.Cmc] = cv + 1;
                        sumCreatureCmc += c.Cmc;
                    }

                    catTotal++;
                }

                macroByCat[g.Key] = typeCounts;
                cmcByCat[g.Key] = cmcCounts;
                creatureCmcByCat[g.Key] = creatureCmcCounts;

                avgCmcByCat[g.Key] = catTotal > 0 ? sumCmc / catTotal : 0;

                int catCreatures = typeCounts[MacroType.Creature];
                avgCreatureCmcByCat[g.Key] = catCreatures > 0 ? sumCreatureCmc / catCreatures : 0;
            }

            // Effect distribution over non-land cards. Counts overlap: a card with
            // several effects is tallied under each flag it carries.
            Dictionary<CardEffect, int> effectCounts = [];
            foreach (CardEffect flag in AllEffects)
            {
                if (flag != CardEffect.None)
                {
                    effectCounts[flag] = 0;
                }
            }

            int untaggedEffects = 0;
            foreach (Card c in nonLands)
            {
                if (c.Effects == CardEffect.None)
                {
                    untaggedEffects++;
                    continue;
                }

                foreach (CardEffect flag in AllEffects)
                {
                    if (flag != CardEffect.None && c.Effects.HasFlag(flag))
                    {
                        effectCounts[flag]++;
                    }
                }
            }

            // Same effect tally, split by colour column (each card sits in exactly one).
            Dictionary<string, int> cardsByColumn = [];
            Dictionary<string, int> reviewedByColumn = [];
            Dictionary<string, int> untaggedByColumn = [];
            Dictionary<string, Dictionary<CardEffect, int>> effectsByColumn = [];
            foreach (Card c in nonLands)
            {
                string column = EffectColumn(c.ColorCategory);
                cardsByColumn[column] = cardsByColumn.GetValueOrDefault(column, 0) + 1;

                if (c.Reviewed)
                {
                    reviewedByColumn[column] = reviewedByColumn.GetValueOrDefault(column, 0) + 1;
                }

                if (c.Effects == CardEffect.None)
                {
                    untaggedByColumn[column] = untaggedByColumn.GetValueOrDefault(column, 0) + 1;
                    continue;
                }

                if (!effectsByColumn.TryGetValue(column, out Dictionary<CardEffect, int>? columnEffects))
                {
                    columnEffects = [];
                    effectsByColumn[column] = columnEffects;
                }

                foreach (CardEffect flag in AllEffects)
                {
                    if (flag != CardEffect.None && c.Effects.HasFlag(flag))
                    {
                        columnEffects[flag] = columnEffects.GetValueOrDefault(flag, 0) + 1;
                    }
                }
            }

            // Second tally, by colour identity: a card counts once in EVERY colour of
            // its identity (only the five letters), and an empty identity is Colorless.
            Dictionary<string, int> cardsByIdentity = [];
            Dictionary<string, int> reviewedByIdentity = [];
            Dictionary<string, int> untaggedByIdentity = [];
            Dictionary<string, Dictionary<CardEffect, int>> effectsByIdentity = [];
            foreach (Card c in nonLands)
            {
                foreach (string colour in IdentityColumns(c))
                {
                    cardsByIdentity[colour] = cardsByIdentity.GetValueOrDefault(colour, 0) + 1;

                    if (c.Reviewed)
                    {
                        reviewedByIdentity[colour] = reviewedByIdentity.GetValueOrDefault(colour, 0) + 1;
                    }

                    if (c.Effects == CardEffect.None)
                    {
                        untaggedByIdentity[colour] = untaggedByIdentity.GetValueOrDefault(colour, 0) + 1;
                        continue;
                    }

                    if (!effectsByIdentity.TryGetValue(colour, out Dictionary<CardEffect, int>? colourEffects))
                    {
                        colourEffects = [];
                        effectsByIdentity[colour] = colourEffects;
                    }

                    foreach (CardEffect flag in AllEffects)
                    {
                        if (flag != CardEffect.None && c.Effects.HasFlag(flag))
                        {
                            colourEffects[flag] = colourEffects.GetValueOrDefault(flag, 0) + 1;
                        }
                    }
                }
            }

            int creatureTargets = nonLands.Count(c => c.MacroType == MacroType.Creature);
            int artifactEnchantmentTargets = nonLands.Count(c => c.MacroType != MacroType.Creature
                && (c.TypeLine.Contains("Artifact", StringComparison.Ordinal)
                    || c.TypeLine.Contains("Enchantment", StringComparison.Ordinal)));

            List<AnswerGroupCount> answerGroups = [];
            foreach ((string label, CardEffect groupEffects, AnswerTargets targetKind) in AnswerGroupDefinitions)
            {
                Dictionary<string, int> byColumn = [];
                foreach (Card c in nonLands.Where(c => (c.Effects & groupEffects) != CardEffect.None))
                {
                    string column = EffectColumn(c.ColorCategory);
                    byColumn[column] = byColumn.GetValueOrDefault(column, 0) + 1;
                }

                Dictionary<string, int> byIdentity = [];
                foreach (Card c in nonLands.Where(c => (c.Effects & groupEffects) != CardEffect.None))
                {
                    foreach (string colour in IdentityColumns(c))
                    {
                        byIdentity[colour] = byIdentity.GetValueOrDefault(colour, 0) + 1;
                    }
                }

                int? targets = targetKind switch
                {
                    AnswerTargets.Creatures => creatureTargets,
                    AnswerTargets.NonCreatureArtifactsAndEnchantments => artifactEnchantmentTargets,
                    AnswerTargets.Lands => lands.Count(),
                    _ => null,
                };

                answerGroups.Add(new AnswerGroupCount
                {
                    Label = label,
                    Effects = groupEffects,
                    Targets = targets,
                    ByColumn = byColumn,
                    ByIdentity = byIdentity,
                    Total = byColumn.Values.Sum(),
                });
            }

            double globalPips = nonLands.Sum(c => (double)c.ManaPips);
            double globalPipDensity = total > 0 ? globalPips / total : 0;

            double totalManaCost = nonLands.Sum(c => c.Cmc);
            double avgCmc = total > 0 ? totalManaCost / total : 0;

            Dictionary<string, double> pipPerCat = [];
            foreach (IGrouping<string, Card> g in nonLands.GroupBy(c => c.ColorCategory))
            {
                int cnt = g.Count();
                if (cnt > 0)
                {
                    pipPerCat[g.Key] = g.Sum(c => (double)c.ManaPips) / cnt;
                }
            }

            Dictionary<string, int> individualColors = new()
            {
                { "W", 0 }, { "U", 0 }, { "B", 0 }, { "R", 0 }, { "G", 0 },
            };
            int colorlessCount = 0;
            int landCount = lands.Count();

            foreach (Card c in nonLands)
            {
                if (c.ColorIdentity.Count == 0)
                {
                    colorlessCount++;
                    continue;
                }

                foreach (string color in c.ColorIdentity)
                {
                    if (individualColors.TryGetValue(color, out int value))
                    {
                        individualColors[color] = ++value;
                    }
                }
            }

            return new AnalysisReport
            {
                MacroTypeCounts = macroTypeCounts,
                ColorCategoryCounts = colorCategoryCounts,
                MacroTypeByCategory = macroByCat,
                CmcByCategory = cmcByCat,
                CreatureCmcByCategory = creatureCmcByCat,
                AverageCmcPerCategory = avgCmcByCat,
                AverageCreatureCmcPerCategory = avgCreatureCmcByCat,
                RatingTiers = ratingTiers,
                CurveByMacroType = curveByMacroType,
                GlobalPipDensity = globalPipDensity,
                AverageCmc = avgCmc,
                PipDensityPerCategory = pipPerCat,
                IndividualColorCounts = individualColors,
                ColorlessCount = colorlessCount,
                LandCount = landCount,
                LandsByCategory = landsByCategory,
                EffectCounts = effectCounts,
                UntaggedEffectCount = untaggedEffects,
                CardsByColumn = cardsByColumn,
                ReviewedByColumn = reviewedByColumn,
                UntaggedByColumn = untaggedByColumn,
                EffectCountsByColumn = effectsByColumn,
                CardsByIdentity = cardsByIdentity,
                ReviewedByIdentity = reviewedByIdentity,
                UntaggedByIdentity = untaggedByIdentity,
                EffectCountsByIdentity = effectsByIdentity,
                AnswerGroups = answerGroups,
                TotalCards = total,
            };
        }
    }
}