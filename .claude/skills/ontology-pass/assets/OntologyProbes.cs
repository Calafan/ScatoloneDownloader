using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Cube;

/// <summary>
/// The measuring harness for an ontology pass. NOT tests of behaviour — each
/// [Fact] is a report that scores <see cref="EffectClassifier"/> against the
/// cards a human has actually reviewed, which is the only ground truth there
/// is, and writes the answer to a file. Output goes to a file rather than the
/// console because card text carries characters cp1252 cannot print.
///
/// Copied into ScatoloneDownloader.Tests/Cube/ for a pass and deleted before
/// committing; that folder is gitignored so it cannot be committed by accident.
/// See .claude/skills/ontology-pass/SKILL.md.
///
/// Everything is driven by FILES in the work directory, never by editing this
/// source — that was the single biggest waste in the first four passes, where
/// the same probe was rewritten six times in one session just to change a tag
/// name or a pattern.
///
///   work dir : %ONTOLOGY_DIR%, or %TEMP%\ontology when that is unset
///   store    : store.txt, one path; defaults to the ScatoloneQuintet metadata
///   tag.txt        one effect name          -> tag-detail.txt
///   names.txt      one card name per line   -> text.txt, why.txt
///   hypotheses.txt "name<TAB>regex" per line -> counts.txt
/// </summary>
public sealed class OntologyProbes
{
    private const string DefaultStore = @"E:\Working\Repos\ScatoloneQuintet\metadata";

    private static string Dir
    {
        get
        {
            string dir = Environment.GetEnvironmentVariable("ONTOLOGY_DIR") is { Length: > 0 } set
                ? set
                : Path.Combine(Path.GetTempPath(), "ontology");

            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string StorePath =>
        ReadLines("store.txt").FirstOrDefault() is { Length: > 0 } path ? path : DefaultStore;

    private static List<string> ReadLines(string file)
    {
        string path = Path.Combine(Dir, file);
        return File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList()
            : [];
    }

    private static void Write(string file, string body) =>
        File.WriteAllText(Path.Combine(Dir, file), body, new UTF8Encoding(false));

    /// <summary>Every reviewed card, with what the human said and what the
    /// classifier says. This is the input to every report below.</summary>
    private static async Task<List<(Card Card, CardEffect Human, CardEffect Auto, DateTimeOffset When)>> Ground()
    {
        CubeMetadata store = CubeMetadataStore.Load(StorePath);

        List<Card> all;
        using (GetManager manager = new())
        {
            all = await manager.GetDefaultCards();
        }

        Dictionary<string, Card> byOracle = [];
        foreach (Card card in all)
        {
            if (!string.IsNullOrEmpty(card.OracleId))
            {
                byOracle.TryAdd(card.OracleId, card);
            }
        }

        // The UNTAGGED reviewed cards matter most: they are the only evidence of
        // what the classifier should NOT have fired on, so dropping them would
        // make precision meaningless.
        List<(Card, CardEffect, CardEffect, DateTimeOffset)> rows = [];
        foreach ((string oracleId, CardMetadataEntry entry) in store.Cards)
        {
            if (entry.ReviewedAt != null && byOracle.TryGetValue(oracleId, out Card? card))
            {
                rows.Add((card, entry.EffectFlags, EffectClassifier.Classify(card), entry.ReviewedAt.Value));
            }
        }

        return rows;
    }

    /// <summary>THE SCORE. Run this before touching a rule and after every
    /// change; a change that does not move these numbers did not happen.</summary>
    [Fact]
    public async Task Score()
    {
        var rows = await Ground();
        CardEffect[] effects = Enum.GetValues<CardEffect>().Where(e => e != CardEffect.None).ToArray();

        StringBuilder sum = new();
        StringBuilder detail = new();

        int exact = rows.Count(r => r.Human == r.Auto);
        sum.AppendLine($"reviewed cards scored : {rows.Count}");
        sum.AppendLine($"  with human tags     : {rows.Count(r => r.Human != CardEffect.None)}");
        sum.AppendLine($"  human says NO effect: {rows.Count(r => r.Human == CardEffect.None)}");
        sum.AppendLine($"exact tag-set match   : {exact} ({100.0 * exact / rows.Count:F1}%)");
        sum.AppendLine();
        sum.AppendLine("prec = of what the classifier fired, how much was right.");
        sum.AppendLine("rec  = of what the human tagged, how much the classifier found.");
        sum.AppendLine();
        sum.AppendLine($"{"effect",-17}{"support",8}{"TP",6}{"FP",6}{"FN",6}{"prec",8}{"rec",8}{"F1",8}");
        sum.AppendLine(new string('-', 67));

        List<(CardEffect Effect, int Support, int Tp, int Fp, int Fn, double Prec, double Rec, double F1)> scored = [];

        foreach (CardEffect effect in effects)
        {
            int tp = rows.Count(r => r.Human.HasFlag(effect) && r.Auto.HasFlag(effect));
            int fp = rows.Count(r => !r.Human.HasFlag(effect) && r.Auto.HasFlag(effect));
            int fn = rows.Count(r => r.Human.HasFlag(effect) && !r.Auto.HasFlag(effect));
            int support = tp + fn;

            double prec = tp + fp == 0 ? double.NaN : (double)tp / (tp + fp);
            double rec = support == 0 ? double.NaN : (double)tp / support;
            double f1 = double.IsNaN(prec) || double.IsNaN(rec) || prec + rec == 0
                ? double.NaN
                : 2 * prec * rec / (prec + rec);

            scored.Add((effect, support, tp, fp, fn, prec, rec, f1));
        }

        foreach (var s in scored.OrderByDescending(s => s.Support))
        {
            sum.AppendLine(
                $"{s.Effect,-17}{s.Support,8}{s.Tp,6}{s.Fp,6}{s.Fn,6}"
                + $"{Pct(s.Prec),8}{Pct(s.Rec),8}{Pct(s.F1),8}");
        }

        sum.AppendLine();
        sum.AppendLine("Worst first — many FP means over-firing, many FN means blind:");
        foreach (var s in scored.OrderByDescending(s => s.Fp + s.Fn).Take(12))
        {
            sum.AppendLine($"  {s.Effect,-17} {s.Fp + s.Fn,5} wrong  ({s.Fp} over-fired, {s.Fn} missed)");
        }

        // A rule applied differently on different days shows up as one review
        // date scoring far off the others.
        sum.AppendLine();
        sum.AppendLine("Agreement by review date (sittings of 20+ cards):");
        foreach (var g in rows.GroupBy(r => r.When.UtcDateTime.Date).OrderBy(g => g.Key))
        {
            int n = g.Count();
            if (n < 20) { continue; }

            int ex = g.Count(r => r.Human == r.Auto);
            sum.AppendLine($"  {g.Key:yyyy-MM-dd}  {n,5} cards  {ex,5} exact  {100.0 * ex / n,5:F1}%");
        }

        foreach (var s in scored.OrderByDescending(s => s.Fp + s.Fn))
        {
            if (s.Fp + s.Fn == 0) { continue; }

            detail.AppendLine();
            detail.AppendLine(new string('=', 78));
            detail.AppendLine($"{s.Effect}  support={s.Support}  over-fired={s.Fp}  missed={s.Fn}");
            detail.AppendLine(new string('=', 78));

            detail.AppendLine();
            detail.AppendLine($"-- OVER-FIRED: classifier said {s.Effect}, human did not ({s.Fp}) --");
            foreach (var r in rows.Where(r => !r.Human.HasFlag(s.Effect) && r.Auto.HasFlag(s.Effect)).Take(14))
            {
                detail.AppendLine($"  {r.Card.Name}");
                detail.AppendLine($"      {Snip(r.Card.OracleText)}");
            }

            detail.AppendLine();
            detail.AppendLine($"-- MISSED: human said {s.Effect}, classifier did not ({s.Fn}) --");
            foreach (var r in rows.Where(r => r.Human.HasFlag(s.Effect) && !r.Auto.HasFlag(s.Effect)).Take(14))
            {
                detail.AppendLine($"  {r.Card.Name}");
                detail.AppendLine($"      {Snip(r.Card.OracleText)}");
            }
        }

        Write("acc-summary.txt", sum.ToString());
        Write("acc-detail.txt", detail.ToString());
    }

    /// <summary>EVERY disagreement on ONE tag, with the whole card text — the
    /// 190-character snip in the score report cuts these in half, and these are
    /// decided by wording. Put the effect name in tag.txt.</summary>
    [Fact]
    public async Task TagDetail()
    {
        List<string> wanted = ReadLines("tag.txt");
        if (wanted.Count == 0)
        {
            Write("tag-detail.txt", "tag.txt is empty — put one effect name in it (e.g. Pacify).");
            return;
        }

        var rows = await Ground();
        StringBuilder sb = new();

        foreach (string name in wanted)
        {
            if (!Enum.TryParse(name, ignoreCase: true, out CardEffect effect))
            {
                sb.AppendLine($"!! not a CardEffect: {name}");
                continue;
            }

            foreach ((string label, bool over) in new[] { ("OVER-FIRED", true), ("MISSED", false) })
            {
                var hits = rows
                    .Where(r => over
                        ? !r.Human.HasFlag(effect) && r.Auto.HasFlag(effect)
                        : r.Human.HasFlag(effect) && !r.Auto.HasFlag(effect))
                    .OrderBy(r => r.Card.Name)
                    .ToList();

                sb.AppendLine();
                sb.AppendLine(new string('=', 78));
                sb.AppendLine($"{effect} {label} ({hits.Count})");
                sb.AppendLine(new string('=', 78));

                foreach (var r in hits)
                {
                    sb.AppendLine();
                    sb.AppendLine($"### {r.Card.Name}   [{r.Card.TypeLine}]");
                    sb.AppendLine($"    human: {r.Human}");
                    sb.AppendLine($"    auto : {r.Auto}");
                    sb.AppendLine("    " + (r.Card.OracleText ?? string.Empty).Replace("\n", "\n    "));
                }
            }
        }

        Write("tag-detail.txt", sb.ToString());
    }

    /// <summary>COUNT A HYPOTHESIS BEFORE WRITING IT AS A RULE. For each
    /// "name&lt;TAB&gt;regex" line in hypotheses.txt, how many reviewed cards
    /// match and how many of those carry the tag in tag.txt. A family that
    /// splits near 50/50 is not a rule — it is a question for the human, and
    /// writing it anyway is how a pass goes backwards.</summary>
    [Fact]
    public async Task Count()
    {
        List<string> lines = ReadLines("hypotheses.txt");
        List<string> tagNames = ReadLines("tag.txt");

        if (lines.Count == 0 || tagNames.Count == 0)
        {
            Write("counts.txt", "need hypotheses.txt (name<TAB>regex per line) and tag.txt (one effect).");
            return;
        }

        if (!Enum.TryParse(tagNames[0], ignoreCase: true, out CardEffect effect))
        {
            Write("counts.txt", $"not a CardEffect: {tagNames[0]}");
            return;
        }

        var rows = await Ground();
        StringBuilder sb = new();
        sb.AppendLine($"Counted against {rows.Count} reviewed cards; tag = {effect}.");
        sb.AppendLine("A split near 50/50 is a QUESTION, not a rule.");

        foreach (string line in lines)
        {
            string[] parts = line.Split('\t', 2);
            if (parts.Length != 2)
            {
                parts = line.Split("  ", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }

            if (parts.Length != 2)
            {
                sb.AppendLine($"\n### (unparsed) {line}");
                continue;
            }

            string name = parts[0].Trim();
            Regex rx;
            try
            {
                rx = new(parts[1].Trim(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
            }
            catch (ArgumentException ex)
            {
                sb.AppendLine($"\n### {name}: BAD REGEX — {ex.Message}");
                continue;
            }

            var hits = rows.Where(r => rx.IsMatch(r.Card.OracleText ?? string.Empty)).ToList();
            int tagged = hits.Count(h => h.Human.HasFlag(effect));

            sb.AppendLine();
            sb.AppendLine($"### {name}: {hits.Count} fire, {tagged} tagged {effect}");
            foreach (var h in hits.OrderBy(h => h.Human.HasFlag(effect)).ThenBy(h => h.Card.Name))
            {
                bool has = h.Human.HasFlag(effect);
                sb.AppendLine($"    [{(has ? "YES" : "no ")}] {h.Card.Name}  ({h.Human})");

                // The ones that DON'T carry the tag are the evidence; print them.
                if (!has)
                {
                    sb.AppendLine($"        {(h.Card.OracleText ?? string.Empty).Replace("\n", " / ")}");
                }
            }
        }

        Write("counts.txt", sb.ToString());
    }

    /// <summary>WHICH RULE FIRED. Reads every private static Regex field on
    /// EffectClassifier by reflection and reports which ones a card's text
    /// matches, so a disagreement is traced to the field that caused it instead
    /// of guessed at. This is what found a regex whose "\b" had been written
    /// into the source as a literal BACKSPACE byte. Card names in names.txt.</summary>
    [Fact]
    public async Task Why()
    {
        List<string> names = ReadLines("names.txt");
        if (names.Count == 0)
        {
            Write("why.txt", "names.txt is empty — one card name per line.");
            return;
        }

        List<Card> all;
        using (GetManager manager = new())
        {
            all = await manager.GetDefaultCards();
        }

        FieldInfo[] fields = typeof(EffectClassifier)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Regex))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToArray();

        StringBuilder sb = new();
        sb.AppendLine($"{fields.Length} private static Regex fields on EffectClassifier");

        foreach (string name in names)
        {
            Card? card = Find(all, name);

            sb.AppendLine();
            sb.AppendLine(new string('=', 78));
            sb.AppendLine(name);
            sb.AppendLine(new string('=', 78));

            if (card == null)
            {
                sb.AppendLine("  NOT FOUND");
                continue;
            }

            string text = card.OracleText ?? string.Empty;
            sb.AppendLine($"  auto: {EffectClassifier.Classify(card)}");

            foreach (FieldInfo field in fields)
            {
                // A null here is the partial-class initialisation hazard: a
                // pattern array that reads a field declared LOWER in the same
                // file is built before that field exists. Report it loudly.
                if (field.GetValue(null) is not Regex rx)
                {
                    sb.AppendLine($"  {field.Name,-34} <- !! NULL AT READ TIME");
                    continue;
                }

                if (!rx.IsMatch(text))
                {
                    continue;
                }

                string hit = rx.Match(text).Value;
                sb.AppendLine($"  {field.Name,-34} <- {(hit.Length > 70 ? hit[..70] + "..." : hit).Replace("\n", " / ")}");
            }
        }

        Write("why.txt", sb.ToString());
    }

    /// <summary>ORACLE TEXT for named cards, so a rule is written against what
    /// the card really says rather than against memory of it. Names in
    /// names.txt; this is also where the text for a pinned test comes from.</summary>
    [Fact]
    public async Task Text()
    {
        List<string> names = ReadLines("names.txt");
        if (names.Count == 0)
        {
            Write("text.txt", "names.txt is empty — one card name per line.");
            return;
        }

        List<Card> all;
        using (GetManager manager = new())
        {
            all = await manager.GetDefaultCards();
        }

        CubeMetadata store = CubeMetadataStore.Load(StorePath);

        StringBuilder sb = new();
        foreach (string name in names)
        {
            Card? card = Find(all, name);

            sb.AppendLine();
            sb.AppendLine($"### {name}");

            if (card == null)
            {
                sb.AppendLine("    NOT FOUND");
                continue;
            }

            store.Cards.TryGetValue(card.OracleId ?? string.Empty, out CardMetadataEntry? entry);

            sb.AppendLine($"    [{card.Name}] [{card.TypeLine}]");
            sb.AppendLine($"    oracleId: {card.OracleId}");
            sb.AppendLine($"    human   : {(entry == null ? "(not in store)" : entry.EffectFlags.ToString())}"
                + $"{(entry?.ReviewedAt == null ? "  (NOT reviewed)" : "  (reviewed)")}");
            sb.AppendLine($"    auto    : {EffectClassifier.Classify(card)}");
            sb.AppendLine("    " + (card.OracleText ?? string.Empty).Replace("\n", "\n    "));
        }

        Write("text.txt", sb.ToString());
    }

    private static Card? Find(List<Card> all, string name) =>
        all.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? all.FirstOrDefault(c => c.Name.StartsWith(name + " //", StringComparison.OrdinalIgnoreCase))
        ?? all.FirstOrDefault(c => c.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));

    private static string Pct(double v)
        => double.IsNaN(v) ? "-" : (100 * v).ToString("F1", CultureInfo.InvariantCulture) + "%";

    private static string Snip(string? text)
    {
        string flat = (text ?? string.Empty).Replace("\n", " / ");
        return flat.Length <= 190 ? flat : flat[..190] + "...";
    }
}
