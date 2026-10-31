using System.Text.Json;
using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Tests.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The report files: the document words, the stats page frozen with its data, the
/// names and the output paths of the <c>report</c> and <c>analyze</c> commands.
/// </summary>
public sealed class StatsReportTests
{
    private static readonly DateTime Moment = new(2026, 10, 30, 14, 5, 59);
    private const string Marker = "window.STATS_SNAPSHOT =";

    private static List<Card> Cards(string creatureName = "Bear")
    {
        return
        [
            CardAnalyzerCreatureProfileTests.MakeCard(creatureName, ["G"], "Creature — Bear", 2, "2", "2", rating: 4),
            CardAnalyzerCreatureProfileTests.MakeCard("Bolt", ["R"], "Instant", 1, "", "", rating: 3),
            CardAnalyzerCreatureProfileTests.MakeCard("Bench Bear", ["G"], "Creature — Bear", 2, "2", "2", rating: 2),
        ];
    }

    private static string InjectedJson(string html)
    {
        int start = html.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        int end = html.IndexOf("; window.STATS_SNAPSHOT_INFO", start, StringComparison.Ordinal);
        return html[start..end].Trim();
    }

    [Fact]
    public void Render_WithTheCubeDocument_PutsTheSubtitleOnlyUnderTheTitle()
    {
        StatsPayload payload = StatsPayload.Build(Cards());

        string withSubtitle = StatsMarkdown.Render(payload, StatsDocument.ForCube(Moment));
        string withOther = StatsMarkdown.Render(payload, StatsDocument.ForCube(Moment) with { Subtitle = "Generated today" });

        string[] lines = withOther.Split(Environment.NewLine);
        Assert.Equal("# Scatolone Cube Stats", lines[0]);
        Assert.Equal("*Generated today*", lines.Where(l => l.Length > 0).ElementAt(1));

        // Only that line differs: the numbers do not depend on the words around them.
        string nl = Environment.NewLine;
        Assert.Equal(withSubtitle.Replace("*" + StatsDocument.Generated(Moment) + "*", "*Generated today*"), withOther);
        Assert.DoesNotContain("Generated today", withSubtitle.Replace(nl, " "));
    }

    [Fact]
    public void StatsDocument_ForCube_CarriesTheCubeWords()
    {
        StatsDocument document = StatsDocument.ForCube(Moment);

        Assert.Equal("Scatolone Cube Stats", document.Title);
        Assert.Equal(StatsDocument.Generated(Moment), document.Subtitle);
        Assert.Equal("Generated 2026-10-30 14:05", document.Subtitle);
        Assert.Equal("A snapshot of the cube's statistics. The pool is the cards rated 3–5 with no status; the bench is exactly ★2.", document.Note);
    }

    [Fact]
    public void StatsDocument_ForList_CarriesTheListWords()
    {
        StatsDocument document = StatsDocument.ForList("my list.txt", Moment, 120, 87, 0);

        Assert.Equal("my list — List Stats", document.Title);
        Assert.Equal("Generated 2026-10-30 14:05 · list: my list.txt · 120 cards, 87 in the cube's store", document.Subtitle);
        Assert.StartsWith("A snapshot of a card list's statistics. Every card of the list counts, whatever its rating or status in the cube.", document.Note);
        Assert.EndsWith("a card not in the store carries the classifier's proposed effects.", document.Note);
    }

    [Fact]
    public void StatsDocument_ForList_NamesTheTaggedLinesLeftOut()
    {
        StatsDocument document = StatsDocument.ForList("pool.txt", Moment, 100, 100, 7);

        Assert.Equal("Generated 2026-10-30 14:05 · list: pool.txt · 100 cards, 100 in the cube's store · tagged lines (Name -- tag) left out: 7", document.Subtitle);
    }

    private static string InjectedInfo(string html)
    {
        const string infoMarker = "window.STATS_SNAPSHOT_INFO =";
        int start = html.IndexOf(infoMarker, StringComparison.Ordinal) + infoMarker.Length;
        int end = html.IndexOf(";</script>", start, StringComparison.Ordinal);
        return html[start..end].Trim();
    }

    [Fact]
    public void Generated_AndFileStem_HaveTheirFormats()
    {
        Assert.Equal("Generated 2026-10-30 14:05", StatsDocument.Generated(Moment));
        Assert.Equal("Cubo_Analysis_2026-10-30", StatsReport.FileStem(Moment));
    }

    [Fact]
    public void RenderHtml_FreezesThePayloadBeforeThePageScript()
    {
        StatsPayload payload = StatsPayload.Build(Cards());
        StatsDocument document = StatsDocument.ForCube(Moment);

        string html = StatsReport.RenderHtml(payload, document, Moment);

        Assert.Equal(1, html.Split(Marker).Length - 1);
        int snapshot = html.IndexOf(Marker, StringComparison.Ordinal);
        int pageScript = html.IndexOf("const SNAPSHOT = window.STATS_SNAPSHOT", StringComparison.Ordinal);
        Assert.True(pageScript > snapshot, "the page script must come after the snapshot");
        Assert.Contains("async function load()", html);

        using JsonDocument injected = JsonDocument.Parse(InjectedJson(html));
        using JsonDocument expected = JsonDocument.Parse(JsonSerializer.Serialize(payload, StatsPayload.JsonOptions));
        Assert.Equal(expected.RootElement.GetRawText(), injected.RootElement.GetRawText());

        Assert.Contains("<title>Scatolone Cube Stats — 2026-10-30</title>", html);
    }

    [Fact]
    public void RenderHtml_InfoObjectCarriesTitleSubtitleAndNote()
    {
        StatsDocument document = StatsDocument.ForList("mylist.txt", Moment, 3, 1, 0);

        string html = StatsReport.RenderHtml(StatsPayload.BuildForList(Cards(), "mylist"), document, Moment);

        using JsonDocument info = JsonDocument.Parse(InjectedInfo(html));
        Assert.Equal(document.Title, info.RootElement.GetProperty("title").GetString());
        Assert.Equal(document.Subtitle, info.RootElement.GetProperty("subtitle").GetString());
        Assert.Equal(document.Note, info.RootElement.GetProperty("note").GetString());
        Assert.Contains("<title>mylist — List Stats — 2026-10-30</title>", html);
    }

    [Fact]
    public void RenderHtml_CannotBeClosedEarlyByTheDataOrTheDocument()
    {
        // The payload holds counts and labels, not card names; the free text that can
        // reach the page is the document (a list's file name is in its title). Either
        // way the snapshot script must end only at its own closing tag.
        StatsPayload payload = StatsPayload.Build(Cards("</script><b>x"));
        StatsDocument document = new("evil </script><b>x", "sub </script>", "note </script>");

        string html = StatsReport.RenderHtml(payload, document, Moment);

        Assert.DoesNotContain("</", InjectedJson(html));
        Assert.DoesNotContain("</", InjectedInfo(html));
        Assert.Equal(1, html.Split(Marker).Length - 1);

        int start = html.IndexOf(Marker, StringComparison.Ordinal);
        int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        string snapshotScript = html[start..end];
        Assert.DoesNotContain("</", snapshotScript);
        Assert.StartsWith("<script>", html[(end + "</script>".Length)..].TrimStart());

        // And in the <title> the text is HTML-encoded, not a live tag.
        Assert.Contains("<title>evil &lt;/script&gt;&lt;b&gt;x — 2026-10-30</title>", html);
    }

    [Fact]
    public void ResolveOutputs_BlankIsCuboAnalysisInTheCurrentFolder()
    {
        string cwd = Path.Combine(Path.GetTempPath(), "somewhere");

        Assert.Equal((Path.Combine(cwd, "Cubo_Analysis.md"), Path.Combine(cwd, "Cubo_Analysis.html")),
            ReportCommand.ResolveOutputs(null, cwd));
        Assert.Equal((Path.Combine(cwd, "Cubo_Analysis.md"), Path.Combine(cwd, "Cubo_Analysis.html")),
            ReportCommand.ResolveOutputs("  ", cwd));
    }

    [Fact]
    public void ResolveOutputs_BlankUsesTheGivenDefaultStem_DotsIncluded()
    {
        string cwd = Path.Combine(Path.GetTempPath(), "somewhere");

        // A list called "cube.v2" must keep its dot: the stem is appended, not an extension.
        Assert.Equal((Path.Combine(cwd, "cube.v2_Analysis.md"), Path.Combine(cwd, "cube.v2_Analysis.html")),
            ReportCommand.ResolveOutputs(null, cwd, "cube.v2_Analysis"));
    }

    [Fact]
    public void ResolveOutputs_AGivenOutputWinsOverTheDefaultStem()
    {
        string dir = Path.Combine(Path.GetTempPath(), "out");

        (string md, string html) = ReportCommand.ResolveOutputs(Path.Combine(dir, "mine.md"), Path.GetTempPath(), "list_Analysis");

        Assert.Equal(Path.Combine(dir, "mine.md"), md);
        Assert.Equal(Path.Combine(dir, "mine.html"), html);
    }

    [Theory]
    [InlineData("stats")]
    [InlineData("stats.md")]
    [InlineData("stats.html")]
    public void ResolveOutputs_ReplacesAnyExtension(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "out");
        string given = Path.Combine(dir, name);

        (string md, string html) = ReportCommand.ResolveOutputs(given, Path.GetTempPath());

        Assert.Equal(Path.Combine(dir, "stats.md"), md);
        Assert.Equal(Path.Combine(dir, "stats.html"), html);
    }
}
