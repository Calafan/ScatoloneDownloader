using System.Text.Json;

using ScatoloneDownloader.Cli.Cube;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Pins how the cube commands find the library: <c>--root</c> or the settings
/// file, with <c>-m|--metadata</c> overriding only the metadata folder. The
/// settings folder is always a temp one, never the real <c>scatolone.json</c>.
/// </summary>
public sealed class MetadataSettingsTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "ScatoloneSettings_" + Guid.NewGuid().ToString("N"));
    private readonly string settingsDir;
    private readonly string root;

    public MetadataSettingsTests()
    {
        settingsDir = Path.Combine(temp, "exe");
        root = Path.Combine(temp, "Quintet");
        Directory.CreateDirectory(settingsDir);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(settingsDir, "scatolone.json"), JsonSerializer.Serialize(new { root }));
    }

    public void Dispose()
    {
        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public void ExplicitOption_AlwaysWins_OverTheRootMetadata()
    {
        string elsewhere = Path.Combine(temp, "Elsewhere", "meta");
        ClassifyCommand.Settings settings = new() { SettingsDirectory = settingsDir, MetadataDirectory = elsewhere };

        Assert.Equal(Path.GetFullPath(elsewhere), settings.ResolveDirectory());
    }

    [Fact]
    public void WithoutOption_MetadataIsTheRootsMetadataFolder()
    {
        ClassifyCommand.Settings settings = new() { SettingsDirectory = settingsDir };

        Assert.Equal(Path.Combine(root, "metadata"), settings.ResolveDirectory());
    }

    [Fact]
    public void SourceDirectory_IsTheRootsSourceFolder()
    {
        TagCommand.Settings settings = new() { SettingsDirectory = settingsDir };

        Assert.Equal(Path.Combine(root, "Source"), settings.ResolveSourceDirectory());
    }

    [Fact]
    public void RootOption_WinsOverTheSettingsFile()
    {
        string other = Path.Combine(temp, "Other");
        Directory.CreateDirectory(other);
        ClassifyCommand.Settings settings = new() { SettingsDirectory = settingsDir, Root = other };

        Assert.Equal(Path.Combine(other, "metadata"), settings.ResolveDirectory());
    }

    [Fact]
    public void TagReportSync_ResolveToTheSameStoreAndSource()
    {
        MetadataSettings[] all =
        [
            new TagCommand.Settings { SettingsDirectory = settingsDir },
            new ReportCommand.Settings { SettingsDirectory = settingsDir },
            new SyncCommand.Settings { SettingsDirectory = settingsDir },
        ];

        Assert.All(all, s => Assert.Equal(Path.Combine(root, "metadata"), s.ResolveDirectory()));
        Assert.All(all, s => Assert.Equal(Path.Combine(root, "Source"), s.ResolveSourceDirectory()));
    }

    [Fact]
    public void Validate_FailsWithoutARoot_WhenMetadataIsBlank()
    {
        ClassifyCommand.Settings settings = new() { SettingsDirectory = Path.Combine(temp, "nofile") };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void Validate_PassesWithoutARoot_ForAClassifyWithMetadata()
    {
        ClassifyCommand.Settings settings = new()
        {
            SettingsDirectory = Path.Combine(temp, "nofile"),
            MetadataDirectory = Path.Combine(temp, "meta"),
        };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void Validate_FailsForASourceCommand_WhenTheSourceFolderIsMissing()
    {
        TagCommand.Settings settings = new() { SettingsDirectory = settingsDir };

        Assert.False(settings.Validate().Successful);

        Directory.CreateDirectory(Path.Combine(root, "Source"));

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void Validate_FailsForASourceCommand_EvenWithMetadata_WhenThereIsNoRoot()
    {
        SyncCommand.Settings settings = new()
        {
            SettingsDirectory = Path.Combine(temp, "nofile"),
            MetadataDirectory = Path.Combine(temp, "meta"),
        };

        Assert.False(settings.Validate().Successful);
    }
}
