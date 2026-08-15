using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScatoloneDownloader.Mtg
{
    internal class CardAnalyzer
    {
        // Render order for the 4 MacroType rows in the .txt report.
        private static readonly MacroType[] MacroTypeOrder =
        [
            MacroType.Creature,
            MacroType.Land,
            MacroType.OtherPermanent,
            MacroType.Spell,
        ];

        // Printable names for the per-color section headers.
        private static readonly Dictionary<string, string> PrintableNames = new()
        {
            { "W", "White" },
            { "U", "Blue" },
            { "B", "Black" },
            { "R", "Red" },
            { "G", "Green" },
            { "WU", "Azorius" },
            { "UB", "Dimir" },
            { "BR", "Rakdos" },
            { "RG", "Gruul" },
            { "GW", "Selesnya" },
            { "WB", "Orzhov" },
            { "UR", "Izzet" },
            { "BG", "Golgari" },
            { "RW", "Boros" },
            { "GU", "Simic" },
            { "WUB", "Esper" },
            { "UBR", "Grixis" },
            { "BRG", "Jund" },
            { "RGW", "Naya" },
            { "GWU", "Bant" },
            { "WBG", "Abzan" },
            { "URW", "Jeskai" },
            { "BUG", "Sultai" },
            { "RWB", "Mardu" },
            { "GUR", "Temur" },
            { "4_5_Colors", "4-5 Colors" },
            { "Colorless", "Colorless" },
        };

        // Order in which per-color sections appear: monocolor, guilds, shards/wedges, 4-5, colorless.
        private static readonly List<string> CategoryOrder =
        [
            "W", "U", "B", "R", "G",
            "WU", "UB", "BR", "RG", "GW", "WB", "UR", "BG", "RW", "GU",
            "WUB", "UBR", "BRG", "RGW", "GWU", "WBG", "URW", "BUG", "RWB", "GUR",
            "4_5_Colors", "Colorless",
        ];

        private readonly List<Card> analyzedCards;

        internal CardAnalyzer(List<Card> cards)
        {
            analyzedCards = cards ?? [];
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
            Dictionary<int, int> globalCmc = [];
            for (int i = 0; i <= 6; i++) globalCmc[i] = 0;

            foreach (var macroCurve in report.CurveByMacroType.Values)
            {
                foreach (var (cmc, count) in macroCurve)
                {
                    int bucket = cmc >= 6 ? 6 : cmc;
                    globalCmc[bucket] += count;
                }
            }

            string cmcString = string.Join(" | ", globalCmc.OrderBy(k => k.Key)
                .Select(kvp => kvp.Key == 6 ? $"`6+: {kvp.Value}`" : $"`{kvp.Key}: {kvp.Value}`"));

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

                string name = PrintableNames.GetValueOrDefault(category, category);
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

            var groups = new Dictionary<string, string[]>()
            {
                { "### Monocolor", ["W", "U", "B", "R", "G"] },
                { "### Guilds (2-Color)", ["WU", "UB", "BR", "RG", "GW", "WB", "UR", "BG", "RW", "GU"] },
                { "### Shards & Wedges (3-Color)", ["WUB", "UBR", "BRG", "RGW", "GWU", "WBG", "URW", "BUG", "RWB", "GUR"] },
                { "### 4-5 Colors & Colorless", ["4_5_Colors", "Colorless"] }
            };

            foreach (var group in groups)
            {
                bool hasCardsInGroup = group.Value.Any(cat => report.ColorCategoryCounts.TryGetValue(cat, out int c) && c > 0);
                if (!hasCardsInGroup) continue;

                sb.AppendLine(group.Key);
                sb.AppendLine();
                sb.AppendLine("| Category | Cards | Perms % | Spells % | Creatures % | Avg CMC | Cr. Avg CMC | All CMC Dist (0-6+) | Cr. CMC Dist (0-6+) | Rating (3★/4★/5★) |");
                sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");

                foreach (string category in group.Value)
                {
                    if (!report.ColorCategoryCounts.TryGetValue(category, out int catTotal) || catTotal == 0)
                    {
                        continue;
                    }

                    string name = PrintableNames.GetValueOrDefault(category, category);
                    Dictionary<MacroType, int> typeCounts = report.MacroTypeByCategory[category];

                    int catSpells = typeCounts.GetValueOrDefault(MacroType.Spell, 0);
                    int catCreatures = typeCounts.GetValueOrDefault(MacroType.Creature, 0);
                    int catPermanents = catTotal - catSpells;

                    double catAvgCmc = report.AverageCmcPerCategory.GetValueOrDefault(category, 0);
                    double catAvgCreatureCmc = report.AverageCreatureCmcPerCategory.GetValueOrDefault(category, 0);

                    string cmcString = FormatCmcDistribution(report.CmcByCategory.GetValueOrDefault(category, []));
                    string creatureCmcString = catCreatures > 0
                        ? FormatCmcDistribution(report.CreatureCmcByCategory.GetValueOrDefault(category, []))
                        : "-";

                    int t3 = report.RatingTiers.GetValueOrDefault((category, 3), 0);
                    int t4 = report.RatingTiers.GetValueOrDefault((category, 4), 0);
                    int t5 = report.RatingTiers.GetValueOrDefault((category, 5), 0);
                    string tiers = (t3 > 0 || t4 > 0 || t5 > 0) ? $"{t3} / {t4} / {t5}" : "-";

                    string crCmcFormatted = catCreatures > 0 ? $"{catAvgCreatureCmc:0.##}" : "-";

                    sb.AppendLine($"| **{name}** | {catTotal} | {GetPercentage(catPermanents, catTotal)}% | {GetPercentage(catSpells, catTotal)}% | {GetPercentage(catCreatures, catTotal)}% | {catAvgCmc:0.##} | {crCmcFormatted} | {cmcString} | {creatureCmcString} | {tiers} |");
                }

                sb.AppendLine();
            }
        }

        private static string FormatCmcDistribution(Dictionary<double, int> rawCmc)
        {
            Dictionary<int, int> buckets = [];
            for (int i = 0; i <= 6; i++) buckets[i] = 0;

            foreach (var (cmc, count) in rawCmc)
            {
                int bucket = cmc >= 6 ? 6 : (int)cmc;
                buckets[bucket] += count;
            }

            var parts = buckets.OrderBy(k => k.Key)
                .Select(kvp => kvp.Key == 6 ? $"`6+: {kvp.Value}`" : $"`{kvp.Key}: {kvp.Value}`");

            return string.Join(" ", parts);
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
                var key = (c.ColorCategory, c.Rating);
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
                if (!curveByMacroType.TryGetValue(c.MacroType, out var macro))
                {
                    macro = [];
                    curveByMacroType[c.MacroType] = macro;
                }
                macro.TryGetValue(bucket, out int cur);
                macro[bucket] = cur + 1;
            }

            Dictionary<string, Dictionary<MacroType, int>> macroByCat = [];
            Dictionary<string, Dictionary<double, int>> cmcByCat = [];
            Dictionary<string, Dictionary<double, int>> creatureCmcByCat = []; // Distribuzione specifica creature
            Dictionary<string, double> avgCmcByCat = [];
            Dictionary<string, double> avgCreatureCmcByCat = [];

            foreach (var g in nonLands.GroupBy(c => c.ColorCategory))
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

                    // CMC totale della categoria
                    cmcCounts.TryGetValue(c.Cmc, out int v);
                    cmcCounts[c.Cmc] = v + 1;
                    sumCmc += c.Cmc;

                    // CMC specifico delle creature
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

            double globalPips = nonLands.Sum(c => (double)c.ManaPips);
            double globalPipDensity = total > 0 ? globalPips / total : 0;

            double totalManaCost = nonLands.Sum(c => c.Cmc);
            double avgCmc = total > 0 ? totalManaCost / total : 0;

            Dictionary<string, double> pipPerCat = [];
            foreach (var g in nonLands.GroupBy(c => c.ColorCategory))
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
                    if (individualColors.ContainsKey(color))
                    {
                        individualColors[color]++;
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
                TotalCards = total,
            };
        }

        internal void SaveAnalysisCsv(string directory)
        {
            Directory.CreateDirectory(directory);

            IEnumerable<Card> relevant = analyzedCards
                .Where(c => !c.IsBasicLand && string.IsNullOrEmpty(c.Tag) && c.MacroType != MacroType.Land);

            foreach (var group in relevant.GroupBy(c => c.ColorCategory))
            {
                string filePath = Path.Combine(directory, group.Key + ".csv");
                using StreamWriter writer = new(filePath);
                writer.WriteLine("Name,SetCode,CollectorNumber,ManaValue,MacroType,Rating,XmpLabel,ScryfallId,ColorIdentity");

                foreach (Card c in group)
                {
                    string colorIdentity = string.Join("/", c.ColorIdentity);
                    string line = $"{c.Name},{c.Set},{c.CollectorNumber},{c.Cmc},{c.MacroType},{c.Rating},{c.XmpLabel},{c.Id},{colorIdentity}";
                    writer.WriteLine(line);
                }
            }
        }
    }
}