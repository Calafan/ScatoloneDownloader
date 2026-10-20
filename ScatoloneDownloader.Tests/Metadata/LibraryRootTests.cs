using System.Text.Json;

using ScatoloneDownloader.Metadata;

namespace ScatoloneDownloader.Tests.Metadata;

/// <summary>
/// Pins <see cref="LibraryRoot.Resolve"/>: <c>--root</c> wins, otherwise the
/// <c>root</c> of <c>scatolone.json</c> in the settings folder, and every way it
/// can fail says what to do. Settings folders are temp ones; the real
/// <c>scatolone.json</c> is never read.
/// </summary>
public sealed class LibraryRootTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "ScatoloneRoot_" + Guid.NewGuid().ToString("N"));
    private readonly string settingsDir;

    public LibraryRootTests()
    {
        settingsDir = Path.Combine(temp, "exe");
        Directory.CreateDirectory(settingsDir);
    }

    public void Dispose()
    {
        Directory.Delete(temp, recursive: true);
    }

    private string MakeDir(string name)
    {
        string path = Path.Combine(temp, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private void WriteSettings(string json)
    {
        File.WriteAllText(Path.Combine(settingsDir, LibraryRoot.SettingsFileName), json);
    }

    [Fact]
    public void CliRoot_WinsOverTheFile()
    {
        string fromFile = MakeDir("FromFile");
        string fromCli = MakeDir("FromCli");
        WriteSettings(JsonSerializer.Serialize(new { root = fromFile }));

        Assert.Equal(fromCli, LibraryRoot.Resolve(fromCli, settingsDir, out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void FileRoot_Absolute_IsUsed()
    {
        string library = MakeDir("Library");
        WriteSettings(JsonSerializer.Serialize(new { root = library }));

        Assert.Equal(library, LibraryRoot.Resolve(null, settingsDir, out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void FileRoot_Relative_ResolvesAgainstTheSettingsFolder()
    {
        string library = MakeDir(Path.Combine("exe", "lib"));
        WriteSettings(JsonSerializer.Serialize(new { root = "lib" }));

        Assert.Equal(library, LibraryRoot.Resolve("  ", settingsDir, out string? error));
        Assert.Null(error);
    }

    [Fact]
    public void NoFileAndNoCliRoot_Fails_NamingTheFileAndTheOption()
    {
        string? root = LibraryRoot.Resolve(null, settingsDir, out string? error);

        Assert.Null(root);
        Assert.Contains(Path.Combine(settingsDir, "scatolone.json"), error);
        Assert.Contains("--root", error);
    }

    [Fact]
    public void MalformedJson_Fails_NamingTheFile()
    {
        WriteSettings("{ not json");

        Assert.Null(LibraryRoot.Resolve(null, settingsDir, out string? error));
        Assert.Contains("Malformed", error);
        Assert.Contains("scatolone.json", error);
    }

    [Fact]
    public void MissingRoot_Fails()
    {
        WriteSettings("{ \"other\": 1 }");

        Assert.Null(LibraryRoot.Resolve(null, settingsDir, out string? error));
        Assert.Contains("No \"root\"", error);
    }

    [Fact]
    public void BlankRoot_Fails()
    {
        WriteSettings("{ \"root\": \"  \" }");

        Assert.Null(LibraryRoot.Resolve(null, settingsDir, out string? error));
        Assert.Contains("blank", error);
    }

    [Fact]
    public void RootThatDoesNotExist_Fails()
    {
        string missing = Path.Combine(temp, "Missing");
        WriteSettings(JsonSerializer.Serialize(new { root = missing }));

        Assert.Null(LibraryRoot.Resolve(null, settingsDir, out string? error));
        Assert.Equal($"Library root not found: {missing}", error);

        Assert.Null(LibraryRoot.Resolve(missing, settingsDir, out string? cliError));
        Assert.Equal($"Library root not found: {missing}", cliError);
    }

    [Fact]
    public void Subfolders_AreSourceMetadataAndViews()
    {
        string root = Path.Combine(temp, "Q");

        Assert.Equal(Path.Combine(root, "Source"), LibraryRoot.Source(root));
        Assert.Equal(Path.Combine(root, "metadata"), LibraryRoot.Metadata(root));
        Assert.Equal(Path.Combine(root, "Views"), LibraryRoot.Views(root));
    }
}
