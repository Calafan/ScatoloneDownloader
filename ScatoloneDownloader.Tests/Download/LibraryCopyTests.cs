using System.Text.Json;

using ScatoloneDownloader.Cli.Download;
using ScatoloneDownloader.Download;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Download;

/// <summary>
/// Pins what <c>files</c> copies from the library and what it still downloads: the
/// pure split of a list, and the copy itself on a temp directory. No Scryfall, no
/// real library.
/// </summary>
public sealed class LibraryCopyTests : IDisposable
{
    private readonly string dir;

    public LibraryCopyTests()
    {
        dir = Path.Combine(Path.GetTempPath(), "ScatoloneCopy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // --- Split -------------------------------------------------------------

    [Fact]
    public void Split_CardWithAnImage_IsCopiedFromThatFile()
    {
        Card bolt = Make("Lightning Bolt", "Instant", "oracle-bolt");

        LibraryCopySplit split = LibraryCopy.Split(
            [bolt], new Dictionary<string, string> { ["oracle-bolt"] = @"C:\lib\Bolt.png" }, new HashSet<string>());

        Assert.Equal([(bolt, @"C:\lib\Bolt.png")], split.ToCopy);
        Assert.Empty(split.ToDownload);
        Assert.Empty(split.SyncWouldBring);
    }

    [Fact]
    public void Split_CardWithNoImage_IsDownloaded_AndSyncWouldBringItWhenTheRulesSelectIt()
    {
        Card selected = Make("Selected", "Instant", "oracle-selected");
        Card unselected = Make("Unselected", "Instant", "oracle-unselected");

        LibraryCopySplit split = LibraryCopy.Split(
            [selected, unselected], new Dictionary<string, string>(), new HashSet<string> { "oracle-selected" });

        Assert.Empty(split.ToCopy);
        Assert.Equal([selected, unselected], split.ToDownload);
        Assert.Equal([selected], split.SyncWouldBring);
    }

    [Fact]
    public void Split_BasicLand_IsNeverCopied_EvenWithAnImage()
    {
        Card plains = Make("Plains", "Basic Land — Plains", "oracle-plains");

        LibraryCopySplit split = LibraryCopy.Split(
            [plains], new Dictionary<string, string> { ["oracle-plains"] = @"C:\lib\Plains.png" }, new HashSet<string>());

        Assert.Empty(split.ToCopy);
        Assert.Equal([plains], split.ToDownload);
    }

    [Fact]
    public void Split_CardWithoutAnOracleId_IsDownloaded()
    {
        Card nameless = Make("Nameless", "Instant", string.Empty);

        LibraryCopySplit split = LibraryCopy.Split(
            [nameless], new Dictionary<string, string> { [string.Empty] = @"C:\lib\X.png" }, new HashSet<string> { string.Empty });

        Assert.Empty(split.ToCopy);
        Assert.Equal([nameless], split.ToDownload);
        Assert.Empty(split.SyncWouldBring);
    }

    [Fact]
    public void FileByOracleId_FirstFilePerOracleIdWins_AndBlankIdsAreLeftOut()
    {
        Card first = Make("Island", "Basic Land — Island", "oracle-x");
        Card second = Make("Island", "Basic Land — Island", "oracle-x");
        Card nameless = Make("Nameless", "Instant", string.Empty);

        Dictionary<string, string> files = LibraryCopy.FileByOracleId(
            [(first, @"C:\lib\a.png"), (second, @"C:\lib\b.png"), (nameless, @"C:\lib\c.png")]);

        Assert.Equal(new Dictionary<string, string> { ["oracle-x"] = @"C:\lib\a.png" }, files);
    }

    // --- Copy --------------------------------------------------------------

    [Fact]
    public void Copy_CreatesTheTagFolder_AndLeavesNoTempFile()
    {
        string sourceFile = WriteSource("library.png", "image");
        string target = Path.Combine(dir, "Lists", "cube", "Removal", "Bolt.png");

        LibraryCopy.Copy(sourceFile, target);

        Assert.Equal("image", File.ReadAllText(target));
        Assert.Equal([target], Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [Fact]
    public void Copy_ExistingFile_IsOverwritten_NotNumbered()
    {
        string sourceFile = WriteSource("library.png", "new image");
        string folder = Path.Combine(dir, "Lists", "cube");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "Bolt.png");
        File.WriteAllText(target, "old image");

        LibraryCopy.Copy(sourceFile, target);

        Assert.Equal("new image", File.ReadAllText(target));
        Assert.Equal([target], Directory.GetFiles(folder));
    }

    [Fact]
    public void Copy_MissingSource_Throws_AndLeavesNothingBehind()
    {
        string target = Path.Combine(dir, "Lists", "Bolt.png");

        Assert.Throws<FileNotFoundException>(() => LibraryCopy.Copy(Path.Combine(dir, "absent.png"), target));

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    // --- FilesSettings.ResolveLibrarySource ---------------------------------

    [Fact]
    public void ResolveLibrarySource_RootWithSource_ReturnsTheSourceFolder()
    {
        string root = Path.Combine(dir, "Quintet");
        Directory.CreateDirectory(Path.Combine(root, "Source"));
        FilesSettings settings = new() { Root = root, SettingsDirectory = dir };

        Assert.Equal(Path.Combine(root, "Source"), settings.ResolveLibrarySource(out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void ResolveLibrarySource_RootFromTheSettingsFile_IsUsed()
    {
        string root = Path.Combine(dir, "Quintet");
        Directory.CreateDirectory(Path.Combine(root, "Source"));
        File.WriteAllText(Path.Combine(dir, "scatolone.json"), JsonSerializer.Serialize(new { root }));
        FilesSettings settings = new() { SettingsDirectory = dir };

        Assert.Equal(Path.Combine(root, "Source"), settings.ResolveLibrarySource(out string? _));
    }

    [Fact]
    public void ResolveLibrarySource_NoRoot_ReturnsNullWithTheReason()
    {
        FilesSettings settings = new() { SettingsDirectory = Path.Combine(dir, "nofile") };

        Assert.Null(settings.ResolveLibrarySource(out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void ResolveLibrarySource_RootWithoutSource_ReturnsNullWithTheReason()
    {
        string root = Path.Combine(dir, "Quintet");
        Directory.CreateDirectory(root);
        FilesSettings settings = new() { Root = root, SettingsDirectory = dir };

        Assert.Null(settings.ResolveLibrarySource(out string? error));
        Assert.Contains("Source folder not found", error);
    }

    private string WriteSource(string name, string content)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, content);

        return path;
    }

    private static Card Make(string name, string typeLine, string oracleId) =>
        TestCards.Make(
            name, typeLine, cmc: 0, id: "id-" + name, oracleId: oracleId,
            set: "set", setName: "Set", setType: "core", releasedAt: "2000-01-01");
}
