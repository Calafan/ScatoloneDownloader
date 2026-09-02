using System.IO;

using ScatoloneDownloader.Cli.Cube;

using Xunit;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Pins where the metadata store lands when <c>-m|--metadata</c> is omitted. The
/// default is not the working directory for commands that know their master
/// library: the store sits BESIDE it, so it travels with the images it describes
/// instead of following whatever shell the command was launched from. Commands
/// with no master folder have nothing to sit beside and keep the old
/// <c>./metadata</c> fallback.
/// </summary>
public sealed class MetadataSettingsTests
{
    [Fact]
    public void ExplicitOption_AlwaysWins_EvenWithAMasterFolder()
    {
        ImportCommand.Settings settings = new()
        {
            SourceDirectory = Path.Combine(Path.GetTempPath(), "Quintet", "Source"),
            MetadataDirectory = Path.Combine(Path.GetTempPath(), "Elsewhere", "meta"),
        };

        Assert.Equal(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Elsewhere", "meta")),
            settings.ResolveDirectory());
    }

    [Fact]
    public void ImportWithoutOption_ResolvesBesideTheMasterFolder()
    {
        string source = Path.Combine(Path.GetTempPath(), "Quintet", "Source");
        ImportCommand.Settings settings = new() { SourceDirectory = source };

        // Sibling of Source, not a child of it, and not the working directory.
        Assert.Equal(Path.Combine(Path.GetTempPath(), "Quintet", "metadata"), settings.ResolveDirectory());
    }

    [Fact]
    public void BuildViewsAndTag_ResolveToTheSamePlaceAsImport()
    {
        string source = Path.Combine(Path.GetTempPath(), "Quintet", "Source");

        string fromImport = new ImportCommand.Settings { SourceDirectory = source }.ResolveDirectory();
        string fromBuildViews = new BuildViewsCommand.Settings { SourceDirectory = source }.ResolveDirectory();
        string fromTag = new TagCommand.Settings { SourceDirectory = source }.ResolveDirectory();

        // The three commands that share a master library must never disagree about
        // which store they are reading and writing.
        Assert.Equal(fromImport, fromBuildViews);
        Assert.Equal(fromImport, fromTag);
    }

    [Fact]
    public void CommandWithNoMasterFolder_FallsBackToTheWorkingDirectory()
    {
        ClassifyCommand.Settings settings = new();

        Assert.Equal(Path.GetFullPath("metadata"), settings.ResolveDirectory());
    }
}
