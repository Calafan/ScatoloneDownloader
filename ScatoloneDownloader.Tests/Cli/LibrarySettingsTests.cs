using ScatoloneDownloader.Cli.Download;

namespace ScatoloneDownloader.Tests.Cli;

public sealed class LibrarySettingsTests
{
    [Fact]
    public void Validate_MissingLibraryFolder_IsAnError()
    {
        LibrarySettings settings = new() { Library = Path.Combine(Path.GetTempPath(), "ScatoloneMissing_" + Guid.NewGuid().ToString("N")) };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void Validate_NoLibrary_IsValid()
    {
        Assert.True(new LibrarySettings().Validate().Successful);
    }

    [Fact]
    public void YearsSettings_HonoursTheBaseValidation()
    {
        YearsSettings settings = new() { Years = [2026], Library = Path.Combine(Path.GetTempPath(), "ScatoloneMissing_" + Guid.NewGuid().ToString("N")) };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void AllSettings_HonoursTheBaseValidation()
    {
        AllSettings settings = new() { Library = Path.Combine(Path.GetTempPath(), "ScatoloneMissing_" + Guid.NewGuid().ToString("N")) };

        Assert.False(settings.Validate().Successful);
    }
}
