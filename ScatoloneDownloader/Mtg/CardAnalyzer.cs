namespace ScatoloneDownloader.Mtg
{
    internal class CardAnalyzer
    {
        /// <summary>The colour categories grouped as the category analysis shows them
        /// (the stats page's by-category tables and their Markdown twin), in
        /// display order. The one list both read.</summary>
        internal static readonly (string Label, string[] Categories)[] CategoryGroups =
        [
            ("Monocolor", ["W", "U", "B", "R", "G"]),
            ("Guilds (2-Color)", ["WU", "UB", "BR", "RG", "GW", "WB", "UR", "BG", "RW", "GU"]),
            ("Shards & Wedges (3-Color)", ["WUB", "UBR", "BRG", "RGW", "GWU", "WBG", "URW", "BUG", "RWB", "GUR"]),
            ("4-5 Colors & Colorless", ["4_5_Colors", "Colorless"]),
        ];

        // Every CardEffect member in declared order, read once: the tallies below
        // walk it per card, and Enum.GetValues allocates a fresh array each call.
        private static readonly CardEffect[] AllEffects = Enum.GetValues<CardEffect>();

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

        // Rating-2 cards, kept apart from the pool metrics — they are candidates,
        // not pool.
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
        /// and no Banned/Token/Jolly status — so the cube analysis matches what the
        /// tagger's pool surfaces (and the <see cref="CardStatus"/> doc), plus the
        /// <see cref="RatingTier.Bench"/> cards for the availability appendix. The
        /// tier boundaries come from <see cref="RatingTierClassifier"/> rather than
        /// a literal, so the analysis can never disagree with the tagger or
        /// metadata storage about where a rating belongs. The plain constructor
        /// analyzes whatever it is given, which the <c>analyze</c> command relies on
        /// (a list's cards count whatever their rating or status).</summary>
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
        /// instead of the pool, so the stats page and its Markdown twin
        /// report the bench with exactly the same code.</summary>
        internal AnalysisReport AnalyzeBench()
        {
            return new CardAnalyzer(benchCards).Analyze();
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

            // Creature profile: the non-land creatures by cost bucket, plus the three
            // summary counts and the curve by colour (both views).
            Dictionary<string, CreatureCostRow> creatureByCost = CostBucket.All.ToDictionary(b => b, _ => new CreatureCostRow());
            Dictionary<string, Dictionary<string, int>> creatureCurveByColumn = [];
            Dictionary<string, Dictionary<string, int>> creatureCurveByIdentity = [];
            int creatureCount = 0;
            int creaturesMv2 = 0;
            int aggressiveTwoDrops = 0;
            int mvThreePowerThree = 0;
            foreach (Card c in nonLands.Where(c => c.MacroType == MacroType.Creature))
            {
                string bucket = CostBucket.Of(c.Cmc);
                int? power = CreatureStats.ParseStat(c.Power);
                int? toughness = CreatureStats.ParseStat(c.Toughness);

                CreatureCostRow row = creatureByCost[bucket];
                row.Creatures++;
                if (power is int p)
                {
                    row.PowerSum += p;
                    row.PowerCount++;
                }

                if (toughness is int t)
                {
                    row.ToughnessSum += t;
                    row.ToughnessCount++;
                }

                row.Evasive += CreatureStats.IsEvasive(c) ? 1 : 0;
                row.Haste += CreatureStats.IsHaste(c) ? 1 : 0;
                row.Defender += CreatureStats.IsDefender(c) ? 1 : 0;

                // A variable power counts as 0 in the thresholds.
                int thresholdPower = power ?? 0;
                creatureCount++;
                creaturesMv2 += c.Cmc <= 2 ? 1 : 0;
                aggressiveTwoDrops += c.Cmc <= 2 && thresholdPower >= 2 ? 1 : 0;
                mvThreePowerThree += c.Cmc <= 3 && thresholdPower >= 3 ? 1 : 0;

                AddToCurve(creatureCurveByColumn, EffectColumn(c.ColorCategory), bucket);
                foreach (string colour in IdentityColumns(c))
                {
                    AddToCurve(creatureCurveByIdentity, colour, bucket);
                }
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

            int landCount = lands.Count();

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
                CreatureByCost = creatureByCost,
                CreatureCount = creatureCount,
                CreaturesMv2 = creaturesMv2,
                AggressiveTwoDrops = aggressiveTwoDrops,
                MvThreePowerThree = mvThreePowerThree,
                CreatureCurveByColumn = creatureCurveByColumn,
                CreatureCurveByIdentity = creatureCurveByIdentity,
                TotalCards = total,
            };
        }

        private static void AddToCurve(Dictionary<string, Dictionary<string, int>> curve, string column, string bucket)
        {
            if (!curve.TryGetValue(column, out Dictionary<string, int>? byBucket))
            {
                byBucket = [];
                curve[column] = byBucket;
            }

            byBucket[bucket] = byBucket.GetValueOrDefault(bucket, 0) + 1;
        }
    }
}