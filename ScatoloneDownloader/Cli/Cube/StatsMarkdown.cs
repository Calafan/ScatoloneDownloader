using System.Globalization;
using System.Text;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// The Markdown twin of the <c>/stats</c> page: the same <see cref="StatsPayload"/>
    /// printed section for section, in the page's order and under its titles, with the
    /// page's numbers and shares (<c>countCell</c> in <c>StatsPage.html</c>). Where the
    /// page has a mono/identity toggle, both views are written one after the other.
    /// The page's links and shading have no Markdown equivalent and are left out.
    /// Pure text: the caller writes the file.
    /// </summary>
    internal static class StatsMarkdown
    {
        private const string Dash = "–";

        private static readonly string[] Buckets = [.. CostBucket.All];

        internal static string Render(StatsPayload payload, StatsDocument document)
        {
            StringBuilder sb = new();

            sb.AppendLine($"# {document.Title}");
            sb.AppendLine();
            sb.AppendLine($"*{document.Subtitle}*");
            sb.AppendLine();
            sb.AppendLine(document.Note);
            sb.AppendLine();

            foreach (StatsSection s in payload.Sections)
            {
                Section(sb, s.Title, s.Note);
                bool bench = s.Tier == "bench";
                StatsTier tier = bench ? payload.Bench! : payload.Pool;
                StatsOverview o = bench ? payload.BenchOverview! : payload.Overview;

                switch (s.Kind)
                {
                    case "types":
                        TypesTable(sb, o);
                        break;
                    case "categories":
                        CategoriesTable(sb, o, withStars: !bench);
                        break;
                    case "manaValue":
                        CostByCategoryTable(sb, o, r => r.ByCost, r => r.Cards);
                        break;
                    case "creatureManaValue":
                        CostByCategoryTable(sb, o, r => r.CreatureByCost, r => r.Creatures);
                        break;
                    case "effects":
                        Views(sb, payload.ViewNames, tier, MatrixTable);
                        break;
                    case "answers":
                        Views(sb, payload.ViewNames, tier, AnswersTable);
                        break;
                    case "creatures":
                        CreaturesTable(sb, tier.Creatures);
                        break;
                    case "curve":
                        CurveViews(sb, payload.ViewNames, tier);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown stats section kind \"{s.Kind}\".");
                }
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ cells

        /// <summary>The page's <c>countCell</c>: zero is a dash; otherwise the number
        /// and, when <paramref name="denominator"/> is above zero, its share rounded
        /// half away from zero (JavaScript's <c>Math.round</c> on a positive).</summary>
        internal static string Cell(int n, int denominator)
        {
            if (n == 0)
            {
                return Dash;
            }

            string number = Plain(n);
            if (denominator <= 0)
            {
                return number;
            }

            int percent = (int)Math.Round(n * 100.0 / denominator, MidpointRounding.AwayFromZero);
            return $"{number} ({percent.ToString(CultureInfo.InvariantCulture)}%)";
        }

        private static string Plain(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A number with no share; zero is a dash.</summary>
        private static string PlainOrDash(int n)
        {
            return n == 0 ? Dash : Plain(n);
        }

        private static string Average(double? value, string format)
        {
            return value is null ? Dash : value.Value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string BucketLabel(string bucket)
        {
            return CostBucket.Label(bucket);
        }

        // -------------------------------------------------------------- scaffold

        private static void Section(StringBuilder sb, string title, string? note)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(note))
            {
                sb.AppendLine(note);
                sb.AppendLine();
            }
        }

        /// <summary>Writes a table: the header, the separator, the rows, then a blank
        /// line (the one before it is the caller's, left by the section or sub-heading).</summary>
        private static void Table(StringBuilder sb, IReadOnlyList<string> heads, IEnumerable<IReadOnlyList<string>> rows)
        {
            sb.AppendLine("| " + string.Join(" | ", heads) + " |");
            sb.AppendLine("| " + string.Join(" | ", heads.Select(_ => ":---")) + " |");
            foreach (IReadOnlyList<string> row in rows)
            {
                sb.AppendLine("| " + string.Join(" | ", row) + " |");
            }

            sb.AppendLine();
        }

        /// <summary>A group row: the label in bold, every other cell empty.</summary>
        private static List<string> GroupRow(string label, int columns)
        {
            List<string> row = ["**" + label + "**"];
            row.AddRange(Enumerable.Repeat("", columns - 1));
            return row;
        }

        /// <summary>Both column views of a tier's matrix, each under its own sub-heading;
        /// the flag tells the table which view it is writing.</summary>
        private static void Views(
            StringBuilder sb,
            StatsViewNames names,
            StatsTier tier,
            Action<StringBuilder, StatsTier, StatsMatrix, bool> table)
        {
            sb.AppendLine($"### {names.Mono}");
            sb.AppendLine();
            table(sb, tier, tier.Mono, false);
            sb.AppendLine($"### {names.Identity}");
            sb.AppendLine();
            table(sb, tier, tier.Identity, true);
        }

        // ---------------------------------------------------------------- tables

        private static void TypesTable(StringBuilder sb, StatsOverview o)
        {
            int all = o.NonLand + o.Lands;
            List<IReadOnlyList<string>> rows = [];

            foreach (StatsTypeRow t in o.Types)
            {
                List<string> row = [t.Label, Cell(t.Cards, all)];
                row.AddRange(Buckets.Select(b => Cell(t.ByCost.GetValueOrDefault(b, 0), t.Cards)));
                rows.Add(row);
            }

            List<string> nonLand = ["Non-land", Cell(o.NonLand, all)];
            nonLand.AddRange(Buckets.Select(b => Cell(o.CurveTotal.GetValueOrDefault(b, 0), o.NonLand)));
            rows.Add(nonLand);

            // Lands carry no mana value in the analysis, so their cost cells stay empty.
            List<string> lands = ["Lands", Cell(o.Lands, all)];
            lands.AddRange(Buckets.Select(_ => Dash));
            rows.Add(lands);

            Table(sb, ["Type", "Cards", .. Buckets.Select(BucketLabel)], rows);
        }

        private static void CategoriesTable(StringBuilder sb, StatsOverview o, bool withStars)
        {
            List<string> heads = ["Category", "Cards", "Creatures", "Other perm.", "Spells", "Avg MV", "Creature avg MV", "Pips"];
            if (withStars)
            {
                heads.AddRange(["★3", "★4", "★5"]);
            }

            heads.Add("Lands");

            List<string> Row(StatsCategoryRow r)
            {
                List<string> row =
                [
                    r.Name,
                    Cell(r.Cards, o.NonLand),
                    Cell(r.Creatures, r.Cards),
                    Cell(r.OtherPermanents, r.Cards),
                    Cell(r.Spells, r.Cards),
                    Average(r.AverageMv, "0.00"),
                    Average(r.CreatureAverageMv, "0.00"),
                    Average(r.PipDensity, "0.00"),
                ];
                if (withStars)
                {
                    row.Add(Cell(r.Rated3, r.Cards));
                    row.Add(Cell(r.Rated4, r.Cards));
                    row.Add(Cell(r.Rated5, r.Cards));
                }

                row.Add(Cell(r.Lands, o.Lands));
                return row;
            }

            List<IReadOnlyList<string>> rows = [];
            foreach (StatsCategoryGroup g in o.Groups)
            {
                rows.Add(GroupRow(g.Label, heads.Count));
                rows.AddRange(g.Rows.Select(Row));
            }

            rows.Add(Row(o.Total));
            Table(sb, heads, rows);
        }

        private static void CostByCategoryTable(
            StringBuilder sb,
            StatsOverview o,
            Func<StatsCategoryRow, IReadOnlyDictionary<string, int>> byCost,
            Func<StatsCategoryRow, int> count)
        {
            List<string> Row(StatsCategoryRow r)
            {
                int n = count(r);
                List<string> row = [r.Name];
                row.AddRange(Buckets.Select(b => Cell(byCost(r).GetValueOrDefault(b, 0), n)));
                row.Add(Cell(n, n));
                return row;
            }

            List<string> heads = ["Category", .. Buckets.Select(BucketLabel), "Total"];
            List<IReadOnlyList<string>> rows = [];
            foreach (StatsCategoryGroup g in o.Groups)
            {
                List<StatsCategoryRow> counted = g.Rows.Where(r => count(r) > 0).ToList();
                if (counted.Count == 0)
                {
                    continue;
                }

                rows.Add(GroupRow(g.Label, heads.Count));
                rows.AddRange(counted.Select(Row));
            }

            rows.Add(Row(o.Total));
            Table(sb, heads, rows);
        }

        private static void MatrixTable(StringBuilder sb, StatsTier tier, StatsMatrix m, bool identity)
        {
            int Of(IReadOnlyDictionary<string, int> counts, string key) => counts.GetValueOrDefault(key, 0);

            List<string> MetaRow(string label, IReadOnlyDictionary<string, int> counts, int total)
            {
                List<string> row = [label];
                row.AddRange(m.Columns.Select(c => Cell(Of(counts, c.Key), Of(m.Cards, c.Key))));
                row.Add(Cell(total, m.TotalCards));
                return row;
            }

            List<IReadOnlyList<string>> rows =
            [
                MetaRow("Non-land", m.Cards, m.TotalCards),
                MetaRow("Reviewed", m.Reviewed, m.TotalReviewed),
                MetaRow("Untagged", m.Untagged, m.TotalUntagged),
            ];

            foreach (StatsEffectRow effect in m.Rows)
            {
                rows.Add(MetaRow(effect.Effect, effect.ByColumn, effect.Total));
            }

            Table(sb, ["Effect", .. m.Columns.Select(c => c.Name), "Total"], rows);
        }

        private static void AnswersTable(StringBuilder sb, StatsTier tier, StatsMatrix m, bool identity)
        {
            List<IReadOnlyList<string>> rows = [];
            foreach (StatsAnswerGroup g in tier.Answers)
            {
                IReadOnlyDictionary<string, int> counts = identity ? g.ByIdentity : g.ByMono;
                List<string> row = [g.Label, string.Join(", ", g.Effects)];
                row.AddRange(m.Columns.Select(c => Cell(counts.GetValueOrDefault(c.Key, 0), m.Cards.GetValueOrDefault(c.Key, 0))));
                row.Add(Cell(g.Total, m.TotalCards));
                row.Add(g.Targets is null ? Dash : Plain(g.Targets.Value));
                row.Add(g.Targets is null || g.Total == 0
                    ? Dash
                    : ((double)g.Targets.Value / g.Total).ToString("0.0", CultureInfo.InvariantCulture));
                rows.Add(row);
            }

            Table(sb, ["Answer", "Tags", .. m.Columns.Select(c => c.Name), "Total", "Targets", "Targets / answer"], rows);
        }

        private static void CreaturesTable(StringBuilder sb, StatsCreatures c)
        {
            List<IReadOnlyList<string>> rows = [];
            foreach (StatsCreatureRow row in c.Rows.Append(c.Total))
            {
                rows.Add(
                [
                    row.Label,
                    Cell(row.Creatures, c.Total.Creatures),
                    Average(row.PowerAvg, "0.0"),
                    Average(row.ToughnessAvg, "0.0"),
                    PlainOrDash(row.Evasive),
                    PlainOrDash(row.Haste),
                    PlainOrDash(row.Defender),
                ]);
            }

            Table(sb, ["Cost", "Creatures", "Avg power", "Avg toughness", "Evasive", "Haste", "Defender"], rows);
        }

        private static void CurveViews(StringBuilder sb, StatsViewNames names, StatsTier tier)
        {
            sb.AppendLine($"### {names.Mono}");
            sb.AppendLine();
            CurveTable(sb, tier, tier.Creatures.CurveMono);
            sb.AppendLine($"### {names.Identity}");
            sb.AppendLine();
            CurveTable(sb, tier, tier.Creatures.CurveIdentity);
        }

        private static void CurveTable(StringBuilder sb, StatsTier tier, StatsCreatureCurve curve)
        {
            int total = tier.Creatures.Total.Creatures;
            List<IReadOnlyList<string>> rows = [];

            foreach (StatsCurveRow r in curve.Rows)
            {
                List<string> row = [r.Label];
                row.AddRange(curve.Columns.Select(c => Cell(
                    r.ByColumn.GetValueOrDefault(c.Key, 0), curve.Totals.GetValueOrDefault(c.Key, 0))));
                row.Add(Cell(r.Total, total));
                rows.Add(row);
            }

            List<string> totalRow = ["Total"];
            totalRow.AddRange(curve.Columns.Select(c =>
            {
                int t = curve.Totals.GetValueOrDefault(c.Key, 0);
                return Cell(t, t);
            }));
            totalRow.Add(Cell(total, total));
            rows.Add(totalRow);

            Table(sb, ["Cost", .. curve.Columns.Select(c => c.Name), "Total"], rows);
        }
    }
}
