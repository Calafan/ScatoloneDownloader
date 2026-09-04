using System;
using System.IO;

using ScatoloneDownloader.Metadata;

using Xunit;

namespace ScatoloneDownloader.Tests.Metadata;

/// <summary>
/// Covers <see cref="ImportState"/>, the watermark behind <c>import --incremental</c>.
/// The theme of every test here is the same: this file is derived state, so every
/// way it can go wrong must degrade to "scan everything again", never to a
/// skipped card or a thrown command.
/// </summary>
public sealed class ImportStateTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "import-state-tests-" + Guid.NewGuid().ToString("N"));

    public ImportStateTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
    }

    [Fact]
    public void Load_NoFile_HasNoWatermark()
    {
        Assert.Null(ImportState.Load(directory).LastImportUtc);
    }

    [Fact]
    public void Load_MissingDirectory_HasNoWatermark()
    {
        Assert.Null(ImportState.Load(Path.Combine(directory, "does-not-exist")).LastImportUtc);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheInstant()
    {
        DateTimeOffset stamp = new(2026, 9, 4, 8, 30, 15, TimeSpan.Zero);

        ImportState.Save(directory, stamp);

        Assert.Equal(stamp, ImportState.Load(directory).LastImportUtc);
    }

    [Fact]
    public void Save_NonUtcInstant_IsNormalisedToUtc()
    {
        // A local-time stamp must not shift the watermark, or an hour of Bridge
        // edits either gets rescanned forever or is skipped outright.
        DateTimeOffset local = new(2026, 9, 4, 10, 30, 0, TimeSpan.FromHours(2));

        ImportState.Save(directory, local);

        Assert.Equal(local.ToUniversalTime(), ImportState.Load(directory).LastImportUtc);
    }

    [Fact]
    public void Save_CreatesTheDirectoryWhenAbsent()
    {
        string fresh = Path.Combine(directory, "fresh");

        ImportState.Save(fresh, DateTimeOffset.UtcNow);

        Assert.True(File.Exists(Path.Combine(fresh, ImportState.FileName)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ this is not json")]
    [InlineData("[]")]
    public void Load_UnusableFile_FallsBackToAFullScan(string content)
    {
        // Corrupt watermark must mean "no watermark" (rescan everything), never an
        // exception that aborts the import and never a bogus instant that skips files.
        File.WriteAllText(Path.Combine(directory, ImportState.FileName), content);

        Assert.Null(ImportState.Load(directory).LastImportUtc);
    }

    [Fact]
    public void Save_Overwrites_APreviousWatermark()
    {
        ImportState.Save(directory, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        ImportState.Save(directory, new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero), ImportState.Load(directory).LastImportUtc);
    }
}
