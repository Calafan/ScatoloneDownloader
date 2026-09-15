using System.Text.Json;

using ScatoloneDownloader.Metadata;

using Xunit;

namespace ScatoloneDownloader.Tests.Metadata;

public sealed class ReviewLogTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "reviewlog-" + Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(dir, ReviewLog.FileName);

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static ReviewLog.Entry Reviewed(string name, string[] before, string[] after, bool first = true) => new()
    {
        At = DateTimeOffset.UtcNow,
        OracleId = name.ToLowerInvariant(),
        Name = name,
        Before = before,
        After = after,
        FirstReview = first,
        Changed = !before.OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(after.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal),
    };

    [Fact]
    public void Append_CreatesTheFileAndWritesOneLinePerReview()
    {
        ReviewLog.Append(dir, Reviewed("Lightning Bolt", ["Removal", "Burn"], ["Removal", "Burn"]));
        ReviewLog.Append(dir, Reviewed("Fog", [], []));

        string[] lines = File.ReadAllLines(LogPath);

        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => JsonSerializer.Deserialize<ReviewLog.Entry>(line));
    }

    [Fact]
    public void Append_NeverRewritesWhatIsAlreadyThere()
    {
        // The whole value of the file is that it is a history: a card reviewed
        // twice has to leave two lines, not one updated line.
        ReviewLog.Append(dir, Reviewed("Icy Manipulator", [], ["Pacify"]));
        ReviewLog.Append(dir, Reviewed("Icy Manipulator", ["Pacify"], ["Pacify"], first: false));

        string[] lines = File.ReadAllLines(LogPath);

        Assert.Equal(2, lines.Length);
        Assert.True(JsonSerializer.Deserialize<ReviewLog.Entry>(lines[0])!.FirstReview);
        Assert.False(JsonSerializer.Deserialize<ReviewLog.Entry>(lines[1])!.FirstReview);
    }

    [Fact]
    public void Changed_SeparatesAConfirmFromARetag()
    {
        // The distinction the tier files could not record, and the reason this
        // file exists: both of these stamp reviewedAt and look identical there.
        ReviewLog.Append(dir, Reviewed("Millstone", ["Mill"], ["Mill"]));
        ReviewLog.Append(dir, Reviewed("Stinkweed Imp", ["Mill"], []));

        var entries = File.ReadAllLines(LogPath)
            .Select(l => JsonSerializer.Deserialize<ReviewLog.Entry>(l)!)
            .ToList();

        Assert.False(entries[0].Changed);
        Assert.True(entries[1].Changed);
    }

    [Fact]
    public void Changed_IgnoresTheOrderTheTagsHappenToBeIn()
    {
        ReviewLog.Append(dir, Reviewed("Blaze", ["Burn", "Removal"], ["Removal", "Burn"]));

        ReviewLog.Entry only = JsonSerializer.Deserialize<ReviewLog.Entry>(File.ReadAllLines(LogPath)[0])!;

        Assert.False(only.Changed);
    }

    [Fact]
    public void Append_RoundTripsTheProposalItWasShown()
    {
        ReviewLog.Append(dir, Reviewed("Disintegrate", ["Removal"], ["Removal", "Burn"]));

        ReviewLog.Entry only = JsonSerializer.Deserialize<ReviewLog.Entry>(File.ReadAllLines(LogPath)[0])!;

        Assert.Equal(["Removal"], only.Before);
        Assert.Equal(["Removal", "Burn"], only.After);
        Assert.True(only.Changed);
    }
}
