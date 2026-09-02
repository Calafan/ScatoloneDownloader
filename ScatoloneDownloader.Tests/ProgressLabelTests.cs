using ScatoloneDownloader;

using Spectre.Console;

using Xunit;

namespace ScatoloneDownloader.Tests;

/// <summary>
/// Guards the progress-counter escaping. A raw "[26/30151]" in a Spectre task
/// description is parsed as a style tag and throws on the render thread, which
/// killed a full import partway through — so the counter is asserted to survive
/// the same markup parser Spectre uses to render it.
/// </summary>
public sealed class ProgressLabelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 30151)]
    [InlineData(26, 30151)]
    [InlineData(30151, 30151)]
    public void Counter_IsParseableAsMarkup(double done, int total)
    {
        string label = ProgressLabel.Counter(done, total);

        // Constructing a Markup runs the parser that threw at render time.
        Markup markup = new($"[yellow]Working... [cyan]{label}[/][/]");

        Assert.NotNull(markup);
    }

    [Fact]
    public void Counter_EscapesTheBrackets_AndTruncatesTheProgressValue()
    {
        // Spectre hands out a double for task.Value; the counter must read as a
        // whole card count, not "26.0".
        Assert.Equal("[[26/30151]]", ProgressLabel.Counter(26.0, 30151));
    }
}
