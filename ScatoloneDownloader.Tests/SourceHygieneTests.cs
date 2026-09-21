using System.Text;

using Xunit;

namespace ScatoloneDownloader.Tests;

/// <summary>
/// Guards against a class of corruption that has now shipped twice and was
/// invisible both times: a regex escape written into the source as the control
/// character it names. A tool that renders "\b" through a non-raw string turns
/// it into a literal BACKSPACE, the pattern silently stops matching, and the
/// diff shows nothing at all because the terminal swallows the byte.
///
/// The first one sat in EffectClassifier's self-cost guard for a day before a
/// reflection probe caught it; the second wrote itself into the ontology
/// comment describing the first, the same afternoon and through the same tool.
/// </summary>
public sealed class SourceHygieneTests
{
    [Fact]
    public void EverySourceFile_IsFreeOfControlCharacters()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScatoloneDownloader.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        List<string> offenders = [];

        foreach (string path in Directory.EnumerateFiles(dir.FullName, "*.cs", SearchOption.AllDirectories))
        {
            // Build output is not ours to police, and it holds generated files.
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            string text = File.ReadAllText(path, Encoding.UTF8);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!char.IsControl(c) || c is '\t' or '\r' or '\n')
                {
                    continue;
                }

                int line = text.Take(i).Count(ch => ch == '\n') + 1;
                offenders.Add($"{Path.GetRelativePath(dir.FullName, path)}:{line} U+{(int)c:X4}");
                break;
            }
        }

        Assert.Empty(offenders);
    }
}
