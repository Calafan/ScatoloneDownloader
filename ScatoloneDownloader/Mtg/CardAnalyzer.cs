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
            return total != 0 ? value * 100 / total : 0;
        }


        /// <summary>
        /// Renders the full .txt analysis from the <see cref="AnalysisReport"/> —
        /// a global section followed by one section per ColorCategory. Each section
        /// shows total cards, permanents vs spells percentages, the 4 MacroType
        /// counts, CMC distribution, and average CMC. Replaces the old
        /// CardsByColorAndType/CardsByColorAndCmc rendering that used the Colors
        /// field and 7 literal type strings.
        /// </summary>
        internal void SaveAnalysis(string path)
        {
            AnalysisReport report = Analyze();

            StringBuilder sb = new();

            // --- Global section ---------------------------------------------------
            int totalSpells = report.MacroTypeCounts[MacroType.Spell];
            int totalPermanents = report.TotalCards - totalSpells;

            AppendSection(sb, header: null, report, report.MacroTypeCounts, report.TotalCards,
                totalPermanents, totalSpells, report.AverageCmc, report.TotalCards);

            // --- Per-ColorCategory sections ---------------------------------------
            foreach (string category in CategoryOrder)
            {
                if (!report.ColorCategoryCounts.TryGetValue(category, out int catTotal) || catTotal == 0)
                {
                    continue;
                }

                Dictionary<MacroType, int> typeCounts = report.MacroTypeByCategory[category];
                int catSpells = typeCounts[MacroType.Spell];
                int catPermanents = catTotal - catSpells;
                double catAvgCmc = report.AverageCmcPerCategory[category];

                AppendSection(sb, PrintableNames.GetValueOrDefault(category, category),
                    report, typeCounts, catTotal, catPermanents, catSpells, catAvgCmc, catTotal,
                    category, report.CmcByCategory[category]);
            }

            using StreamWriter writer = new(path);
            writer.Write(sb.ToString());
        }

        private static void AppendSection(
            StringBuilder sb,
            string? header,
            AnalysisReport report,
            Dictionary<MacroType, int> typeCounts,
            int totalCards,
            int totalPermanents,
            int totalSpells,
            double avgCmc,
            int sectionTotal,
            string? category = null,
            Dictionary<double, int>? cmcDistribution = null)
        {
            string tab = string.Empty;

            if (!string.IsNullOrEmpty(header))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine(header);
                tab = "\t";
            }

            sb.AppendLine(string.Format(
                tab + "Cards: {0} - Permanents: {1} ({2}%) Spells: {3} ({4}%)",
                totalCards, totalPermanents, GetPercentage(totalPermanents, totalCards),
                totalSpells, GetPercentage(totalSpells, totalCards)));
            sb.AppendLine();

            // 4 MacroType rows.
            foreach (MacroType mt in MacroTypeOrder)
            {
                int count = typeCounts.GetValueOrDefault(mt, 0);
                sb.AppendLine(tab + mt + "s:\t" + count + " (" + GetPercentage(count, totalCards) + "%)");
            }

            sb.AppendLine();
            sb.AppendLine(tab + "CMC distribution:");

            // Global section uses CurveByMacroType aggregated; per-category uses CmcByCategory.
            // For the global section we compute aggregate from curveByMacroType.
            if (cmcDistribution != null)
            {
                foreach (double cmc in cmcDistribution.Keys.OrderBy(k => k))
                {
                    sb.AppendLine(tab + "\t" + Convert.ToInt32(cmc) + ":\t" + cmcDistribution[cmc]);
                }
            }
            else
            {
                // Global: aggregate all CMC buckets across MacroType curves.
                Dictionary<double, int> globalCmc = [];
                foreach (var macroCurve in report.CurveByMacroType.Values)
                {
                    foreach (var (cmc, count) in macroCurve)
                    {
                        globalCmc.TryGetValue(cmc, out int v);
                        globalCmc[cmc] = v + count;
                    }
                }
                foreach (double cmc in globalCmc.Keys.OrderBy(k => k))
                {
                    sb.AppendLine(tab + "\t" + Convert.ToInt32(cmc) + ":\t" + globalCmc[cmc]);
                }
            }

            sb.AppendLine();
            sb.AppendLine(string.Format(tab + "Average CMC: {0:0.##}", totalCards != 0 ? avgCmc : 0));

            // Rating tiers for this category (3★/4★/5★).
            if (category != null)
            {
                bool anyTier = false;
                for (int stars = 3; stars <= 5; stars++)
                {
                    if (report.RatingTiers.TryGetValue((category, stars), out int tier) && tier > 0)
                    {
                        if (!anyTier)
                        {
                            sb.AppendLine();
                            sb.AppendLine(tab + "Rating tiers:");
                            anyTier = true;
                        }
                        sb.AppendLine(tab + "\t" + stars + "★:\t" + tier);
                    }
                }
            }
        }


        // --- Phase 1: extended metrics + CSV export ---------------------------


        /// <summary>
        /// Computes all extended cube metrics (R2) from the cards passed to the
        /// constructor, returning an immutable <see cref="AnalysisReport"/>. Cards
        /// with a non-empty <see cref="Card.Tag"/> or <see cref="Card.IsBasicLand"/>
        /// are excluded, matching the existing .txt analyzer behavior.
        /// </summary>
        internal AnalysisReport Analyze()
        {
            IEnumerable<Card> relevant = analyzedCards
                .Where(c => !c.IsBasicLand && string.IsNullOrEmpty(c.Tag))
                .ToList();

            int total = relevant.Count();

            // MacroType ratios.
            Dictionary<MacroType, int> macroTypeCounts = new()
            {
                { MacroType.Creature, 0 },
                { MacroType.Land, 0 },
                { MacroType.OtherPermanent, 0 },
                { MacroType.Spell, 0 },
            };
            foreach (Card c in relevant)
            {
                macroTypeCounts[c.MacroType]++;
            }

            // ColorCategory density (Guilds/Shards/Wedges/4-5/Colorless/mono).
            Dictionary<string, int> colorCategoryCounts = [];
            foreach (Card c in relevant)
            {
                string cat = c.ColorCategory;
                colorCategoryCounts.TryGetValue(cat, out int cur);
                colorCategoryCounts[cat] = cur + 1;
            }

            // Rating tiers: (ColorCategory, stars) — only 3★/4★/5★ per Plan §4.2.
            // Keyed by the full ColorCategory code (W, U, WU, WUB, ...) so a 3-color
            // card contributes to its shard/wedge tier, not to a single-color bucket.
            Dictionary<(string, int), int> ratingTiers = [];
            foreach (Card c in relevant)
            {
                if (c.Rating < 3) continue;
                var key = (c.ColorCategory, c.Rating);
                ratingTiers.TryGetValue(key, out int cur);
                ratingTiers[key] = cur + 1;
            }

            // Curve by MacroType: CMC buckets 1/2/3/4/5/6(=6+).
            Dictionary<MacroType, Dictionary<int, int>> curveByMacroType = new()
            {
                { MacroType.Creature, [] },
                { MacroType.Spell, [] },
                { MacroType.OtherPermanent, [] },
                { MacroType.Land, [] },
            };
            foreach (Card c in relevant)
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

            // Per-ColorCategory MacroType breakdown + CMC distribution, for the
            // .txt per-color section. Replaces the old CardsByColorAndType/CardsByColorAndCmc
            // which used the Colors field and 7 literal type strings.
            Dictionary<string, Dictionary<MacroType, int>> macroByCat = [];
            Dictionary<string, Dictionary<double, int>> cmcByCat = [];
            Dictionary<string, double> avgCmcByCat = [];
            foreach (var g in relevant.GroupBy(c => c.ColorCategory))
            {
                Dictionary<MacroType, int> typeCounts = new()
                {
                    { MacroType.Creature, 0 },
                    { MacroType.Land, 0 },
                    { MacroType.OtherPermanent, 0 },
                    { MacroType.Spell, 0 },
                };
                Dictionary<double, int> cmcCounts = [];
                double sumCmc = 0;
                int catTotal = 0;
                foreach (Card c in g)
                {
                    typeCounts[c.MacroType]++;
                    cmcCounts.TryGetValue(c.Cmc, out int v);
                    cmcCounts[c.Cmc] = v + 1;
                    sumCmc += c.Cmc;
                    catTotal++;
                }
                macroByCat[g.Key] = typeCounts;
                cmcByCat[g.Key] = cmcCounts;
                avgCmcByCat[g.Key] = catTotal > 0 ? sumCmc / catTotal : 0;
            }

            // Pip density: global average colored pips per card.
            double globalPips = relevant.Sum(c => (double)c.ManaPips);
            double globalPipDensity = total > 0 ? globalPips / total : 0;

            // Average CMC.
            double totalManaCost = relevant.Sum(c => c.Cmc);
            double avgCmc = total > 0 ? totalManaCost / total : 0;

            // Pip density per ColorCategory.
            Dictionary<string, double> pipPerCat = [];
            foreach (var g in relevant.GroupBy(c => c.ColorCategory))
            {
                int cnt = g.Count();
                if (cnt > 0)
                {
                    pipPerCat[g.Key] = g.Sum(c => (double)c.ManaPips) / cnt;
                }
            }

            return new AnalysisReport
            {
                MacroTypeCounts = macroTypeCounts,
                ColorCategoryCounts = colorCategoryCounts,
                MacroTypeByCategory = macroByCat,
                CmcByCategory = cmcByCat,
                AverageCmcPerCategory = avgCmcByCat,
                RatingTiers = ratingTiers,
                CurveByMacroType = curveByMacroType,
                GlobalPipDensity = globalPipDensity,
                AverageCmc = avgCmc,
                PipDensityPerCategory = pipPerCat,
                TotalCards = total,
            };
        }

        /// <summary>
        /// Writes one CSV file per ColorCategory, using the canonical plan format:
        /// <c>Name,SetCode,CollectorNumber,ManaValue,MacroType,Rating,XmpLabel,ScryfallId,ColorIdentity</c>.
        /// </summary>
        internal void SaveAnalysisCsv(string directory)
        {
            Directory.CreateDirectory(directory);

            IEnumerable<Card> relevant = analyzedCards
                .Where(c => !c.IsBasicLand && string.IsNullOrEmpty(c.Tag));

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
