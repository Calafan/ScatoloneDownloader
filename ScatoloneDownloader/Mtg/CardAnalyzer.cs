using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ScatoloneDownloader.Extensions;

namespace ScatoloneDownloader.Mtg
{
    internal class CardAnalyzer
    {
        private static readonly List<string> CardColors = ["W", "U", "B", "R", "G", "Multicolor", "Colorless"];
        private static readonly List<string> CardTypes = ["creature", "land", "artifact", "enchantment", "planeswalker", "instant", "sorcery"];

        private static readonly Dictionary<string, string> ColorPrintableNames = new() {
            { "W", "White" },
            { "U", "Blue" },
            { "B", "Black" },
            { "R", "Red" },
            { "G", "Green" },
            { "Multicolor", "Multicolor" },
            { "Colorless", "Colorless" } };

        private static readonly Dictionary<string, string> Tabs = new() { { "creature", "\t\t" }, { "land", "\t\t\t" }, { "artifact", "\t\t" }, { "enchantment", "\t" }, { "planeswalker", "\t" }, { "instant", "\t\t" }, { "sorcery", "\t\t" } };

        private readonly Dictionary<string, Dictionary<string, int>> CardsByColorAndType;
        private readonly Dictionary<string, Dictionary<double, int>> CardsByColorAndCmc;
        private readonly Dictionary<string, int> MulticolorColorDistribution;
        private readonly List<Card> analyzedCards;

        internal CardAnalyzer(List<Card> cards)
        {
            analyzedCards = cards ?? [];
            CardsByColorAndType = [];
            CardsByColorAndCmc = [];
            MulticolorColorDistribution = [];

            foreach (string color in CardColors)
            {
                CardsByColorAndType.Add(color, []);
                CardsByColorAndCmc.Add(color, []);

                if (color != "Multicolor" && color != "Colorless")
                {
                    MulticolorColorDistribution.Add(color, 0);
                }


                foreach (string type in CardTypes)
                {
                    CardsByColorAndType[color].Add(type, 0);
                }
            }


            foreach (Card card in cards)
            {
                if (!card.IsBasicLand && string.IsNullOrEmpty(card.Tag))
                {
                    string color;

                    if (card.Colors.Count == 0 ||
                        card.TypeLine.Contains("land", StringComparison.CurrentCultureIgnoreCase) ||
                        card.TypeLine.Contains("conspiracy", StringComparison.CurrentCultureIgnoreCase))
                    {
                        color = "Colorless";
                    }
                    else if (card.Colors.Count > 1)
                    {
                        color = "Multicolor";
                        foreach (string c in card.Colors)
                        {
                            MulticolorColorDistribution[c]++;
                        }

                    }
                    else
                    {
                        color = card.Colors[0];
                    }

                    foreach (string type in CardTypes)
                    {
                        if (card.TypeLine.Contains(type, StringComparison.CurrentCultureIgnoreCase))
                        {
                            CardsByColorAndType[color][type]++;

                            if (!CardsByColorAndCmc[color].TryGetValue(card.Cmc, out int value))
                            {
                                value = 0;
                                CardsByColorAndCmc[color].Add(card.Cmc, value);
                            }

                            CardsByColorAndCmc[color][card.Cmc] = ++value;
                            break;
                        }
                    }
                }
            }
        }


        private static int GetPercentage(int value, int total)
        {
            return total != 0 ? value * 100 / total : 0;
        }

        private static string GetOutput(string header, int totalCards, int totalPermanents, int totalSpells, Dictionary<string, int> cardCountByType, Dictionary<double, int> cardCountByCmc, double totalManaCost)
        {
            const string Header = "Cards: {0} - Permanents: {1} ({2}%) Spells: {3} ({4}%)";
            const string CMCHeader = "CMC distribution:";
            const string AverageCMC = "Average CMC: {0:0.##}";

            StringBuilder stringBuilder = new();

            string tab = string.Empty;

            if (!string.IsNullOrEmpty(header))
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(header);

                tab = "\t";
            }

            stringBuilder.AppendLine(string.Format(tab + Header, totalCards, totalPermanents, GetPercentage(totalPermanents, totalCards), totalSpells, GetPercentage(totalSpells, totalCards)));
            stringBuilder.AppendLine();

            foreach (string type in CardTypes)
            {
                string pluralizedType = type != "sorcery" ? type + "s" : "sorceries";

                stringBuilder.AppendLine(tab + pluralizedType.Capitalize() + Tabs[type] + cardCountByType[type] + " (" + GetPercentage(cardCountByType[type], totalCards) + "%)");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(tab + CMCHeader);

            foreach (double cmc in cardCountByCmc.Keys.OrderBy(k => k))
            {
                stringBuilder.AppendLine(tab + "\t" + Convert.ToInt32(cmc) + ":\t" + cardCountByCmc[cmc]);
            }
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(string.Format(tab + AverageCMC, totalCards != 0 ? totalManaCost / totalCards : 0));

            return stringBuilder.ToString();
        }


        internal void SaveAnalysis(string path)
        {
            StringBuilder stringBuilder = new();

            Dictionary<string, int> cardsByType = [];
            Dictionary<double, int> cardsByCmc = [];

            foreach (string type in CardTypes)
            {
                cardsByType.Add(type, 0);
            }

            int totalCards = 0;
            double totalManaCost = 0;

            //Complete list
            foreach (string color in CardColors)
            {
                foreach (string type in CardTypes)
                {
                    totalCards += CardsByColorAndType[color][type];
                    cardsByType[type] += CardsByColorAndType[color][type];
                }

                foreach (double cmc in CardsByColorAndCmc[color].Keys)
                {
                    totalManaCost += cmc * CardsByColorAndCmc[color][cmc];

                    cardsByCmc.TryAdd(cmc, 0);
                    cardsByCmc[cmc] += CardsByColorAndCmc[color][cmc];
                }
            }

            int totalSpells = cardsByType["instant"] + cardsByType["sorcery"];
            int totalPermanents = totalCards - totalSpells;

            stringBuilder.Append(GetOutput(null, totalCards, totalPermanents, totalSpells, cardsByType, cardsByCmc, totalManaCost));

            //Colors
            foreach (string color in CardColors)
            {
                totalCards = 0;
                totalManaCost = 0;

                foreach (string type in CardTypes)
                {
                    totalCards += CardsByColorAndType[color][type];
                }

                foreach (double cmc in CardsByColorAndCmc[color].Keys)
                {
                    totalManaCost += cmc * CardsByColorAndCmc[color][cmc];
                }

                totalSpells = CardsByColorAndType[color]["instant"] + CardsByColorAndType[color]["sorcery"];
                totalPermanents = totalCards - totalSpells;

                stringBuilder.Append(GetOutput(ColorPrintableNames[color], totalCards, totalPermanents, totalSpells, CardsByColorAndType[color], CardsByColorAndCmc[color], totalManaCost));

                if (color == "Multicolor")
                {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("\tColor distribution:");

                    foreach (string c in MulticolorColorDistribution.Keys)
                    {
                        stringBuilder.AppendLine("\t\t" + ColorPrintableNames[c] + ":\t" + MulticolorColorDistribution[c] + "(" + GetPercentage(MulticolorColorDistribution[c], totalCards) + "%)");
                    }
                    stringBuilder.AppendLine();
                }
            }

            using StreamWriter writer = new(path);
            writer.Write(stringBuilder.ToString());
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

            // Rating tiers: (color, stars) — keyed by first color in ColorIdentity
            // (or "Colorless" for empty). Only 3★/4★/5★ per Plan §4.2.
            Dictionary<(string, int), int> ratingTiers = [];
            foreach (Card c in relevant)
            {
                if (c.Rating < 3) continue;
                string colorKey = c.ColorIdentity.Count > 0 ? c.ColorIdentity[0] : "Colorless";
                var key = (colorKey, c.Rating);
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

            // Pip density: global average colored pips per card.
            double globalPips = relevant.Sum(c => (double)c.ManaPips);
            double globalPipDensity = total > 0 ? globalPips / total : 0;

            // Average CMC.
            double totalManaCost = relevant.Sum(c => c.Cmc);
            double avgCmc = total > 0 ? totalManaCost / total : 0;

            // Pip density per ColorCategory.
            Dictionary<string, double> pipPerCat = [];
            var groupedByCat = relevant.GroupBy(c => c.ColorCategory);
            foreach (var g in groupedByCat)
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
