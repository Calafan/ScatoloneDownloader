using System.Text.Json;
using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Tests.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// The report files: the Markdown rendering with its subtitle, the stats page frozen
/// with its data, the names and the output paths of the <c>report</c> command.
/// </summary>
public sealed class StatsReportTests : IDisposable
{
    private static readonly DateTime Moment = new(2026, 10, 30, 14, 5, 59);
    private const string Marker = "window.STATS_SNAPSHOT =";

    private readonly string tempFile = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.md");

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

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
    public void RenderMarkdown_EqualsWhatSaveAnalysisWrites()
    {
        CardAnalyzer analyzer = CardAnalyzer.ForPool(Cards());

        analyzer.SaveAnalysis(tempFile);

        Assert.Equal(File.ReadAllText(tempFile), analyzer.RenderMarkdown());
    }

    [Fact]
    public void RenderMarkdown_WithASubtitle_AddsOnlyTheItalicLineUnderTheTitle()
    {
        CardAnalyzer analyzer = CardAnalyzer.ForPool(Cards());
        string plain = analyzer.RenderMarkdown();
        string withSubtitle = analyzer.RenderMarkdown("Generated today");

        string[] lines = withSubtitle.Split(Environment.NewLine);
        Assert.Equal("# Scatolone Cube - Analysis Report", lines[0]);
        Assert.Equal("*Generated today*", lines.Where(l => l.Length > 0).ElementAt(1));

        string nl = Environment.NewLine;
        Assert.Equal(plain, withSubtitle.Replace("*Generated today*" + nl + nl, ""));
        Assert.DoesNotContain("*Generated", plain);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RenderMarkdown_WithoutASubtitle_IsTheUnchangedDocument(string? subtitle)
    {
        CardAnalyzer analyzer = CardAnalyzer.ForPool(Cards());

        Assert.Equal(analyzer.RenderMarkdown(), analyzer.RenderMarkdown(subtitle));

        // Straight after the title and its blank line comes the first section, no empty italic line.
        string[] lines = analyzer.RenderMarkdown(subtitle).Split(Environment.NewLine);
        Assert.Equal("", lines[1]);
        Assert.StartsWith("## ", lines[2]);
    }

    [Fact]
    public void Subtitle_AndFileStem_HaveTheirFormats()
    {
        Assert.Equal("Generated 2026-10-30 14:05 · pool: rating 3-5, no status · bench: exactly ★2", StatsReport.Subtitle(Moment));
        Assert.Equal("Cubo_Analysis_2026-10-30", StatsReport.FileStem(Moment));
    }

    [Fact]
    public void RenderHtml_FreezesThePayloadBeforeThePageScript()
    {
        StatsPayload payload = StatsPayload.Build(Cards());
        string subtitle = StatsReport.Subtitle(Moment);

        string html = StatsReport.RenderHtml(payload, subtitle, Moment);

        Assert.Equal(1, html.Split(Marker).Length - 1);
        int snapshot = html.IndexOf(Marker, StringComparison.Ordinal);
        int pageScript = html.IndexOf("const SNAPSHOT = window.STATS_SNAPSHOT", StringComparison.Ordinal);
        Assert.True(pageScript > snapshot, "the page script must come after the snapshot");
        Assert.Contains("async function load()", html);

        using JsonDocument injected = JsonDocument.Parse(InjectedJson(html));
        using JsonDocument expected = JsonDocument.Parse(JsonSerializer.Serialize(payload, StatsPayload.JsonOptions));
        Assert.Equal(expected.RootElement.GetRawText(), injected.RootElement.GetRawText());

        Assert.Contains($"window.STATS_SNAPSHOT_INFO = {JsonSerializer.Serialize(subtitle)};", html);
        Assert.Contains("<title>Scatolone Cube Stats — 2026-10-30</title>", html);
    }

    [Fact]
    public void RenderHtml_CannotBeClosedEarlyByTheDataOrTheSubtitle()
    {
        // The payload holds counts and labels, not card names; the free text that can
        // reach the page is the subtitle. Either way the snapshot script must end
        // only at its own closing tag.
        StatsPayload payload = StatsPayload.Build(Cards("</script><b>x"));

        string html = StatsReport.RenderHtml(payload, "evil </script><b>x", Moment);

        Assert.DoesNotContain("</", InjectedJson(html));
        Assert.Equal(1, html.Split(Marker).Length - 1);

        int start = html.IndexOf(Marker, StringComparison.Ordinal);
        int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        string snapshotScript = html[start..end];
        Assert.DoesNotContain("</", snapshotScript);
        Assert.StartsWith("<script>", html[(end + "</script>".Length)..].TrimStart());
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
