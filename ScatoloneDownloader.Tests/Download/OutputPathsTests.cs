using System;
using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Download;

/// <summary>
/// Pure-logic + filesystem checks for <see cref="OutputPaths"/>. The static
/// <see cref="OutputPaths.Root"/> is restored in a fixture so tests do not
/// leak state across runs. <see cref="BuildCardDirectory"/> creates real
/// folders under a temp root so the path logic and order of operations
/// (sanitize → Tag trim → neutralize leading dots) is observable.
/// </summary>
public sealed class OutputPathsTests : IDisposable
{
    private static readonly string OriginalRoot = OutputPaths.Root;

    private readonly string tempRoot;

    public OutputPathsTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "ScatoloneTests_" + Guid.NewGuid().ToString("N"));
        OutputPaths.UseRoot(tempRoot);
    }

    public void Dispose()
    {
        OutputPaths.UseRoot(OriginalRoot);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Sanitize_RemovesForbiddenFilesystemCharacters_AndCollapsesDoubleFaceSeparator()
    {
        Assert.Equal("Plains", OutputPaths.Sanitize("Plains"));
        Assert.Equal("Delver_Secrets", OutputPaths.Sanitize("Delver // Secrets"));
        Assert.Equal("Lightning Bolt", OutputPaths.Sanitize("Lightning Bolt"));
        // Forbidden characters are stripped (not replaced with "_") — so
        // "A\B/C" becomes "ABC", and a mixed punctuation run collapses to nothing.
        Assert.Equal("ABC", OutputPaths.Sanitize("A\\B/C"));
        Assert.Equal("SolRing", OutputPaths.Sanitize("Sol:Ring*?\"<>|"));
        // The " // " separator collapses to "_" before the forbidden-character sweep,
        // so the two embedded "/" inside it are gone before the per-char strip runs.
        Assert.Equal("A_B", OutputPaths.Sanitize("A // B"));
    }

    [Fact]
    public void Sanitize_DoesNotTrimWhitespaceOrDots()
    {
        // Sanitize is path-segment cleaning, not full normalization — leading dots
        // are stripped by the Tag path in BuildCardDirectory, not here.
        Assert.Equal("  Plains  ", OutputPaths.Sanitize("  Plains  "));
        Assert.Equal("..secret", OutputPaths.Sanitize("..secret"));
    }

    [Fact]
    public void UseRoot_IgnoresNullAndBlank_RestoresExplicitValue()
    {
        string before = OutputPaths.Root;

        OutputPaths.UseRoot(null!);
        Assert.Equal(before, OutputPaths.Root);

        OutputPaths.UseRoot("   ");
        Assert.Equal(before, OutputPaths.Root);

        OutputPaths.UseRoot(Path.Combine(tempRoot, "Custom"));
        Assert.Equal(Path.Combine(tempRoot, "Custom"), OutputPaths.Root);

        OutputPaths.UseRoot(before);
    }

    [Fact]
    public void BasePath_PerMode_AppendsSubfolderUnderRoot()
    {
        Assert.Equal(Path.Combine(tempRoot, "All"), OutputPaths.BasePath(Mode.All));
        Assert.Equal(Path.Combine(tempRoot, "Sets"), OutputPaths.BasePath(Mode.Set));
        Assert.Equal(Path.Combine(tempRoot, "Years"), OutputPaths.BasePath(Mode.Years));
        Assert.Equal(Path.Combine(tempRoot, "Lists"), OutputPaths.BasePath(Mode.Files));
        Assert.Equal(Path.Combine(tempRoot, "BasicLands"), OutputPaths.BasePath(Mode.Lands));
    }

    [Fact]
    public void BasePaths_EnumeratesAllFiveSubfolders()
    {
        List<string> all = [.. OutputPaths.BasePaths];

        Assert.Equal(5, all.Count);
        Assert.Contains(Path.Combine(tempRoot, "All"), all);
        Assert.Contains(Path.Combine(tempRoot, "Sets"), all);
        Assert.Contains(Path.Combine(tempRoot, "Years"), all);
        Assert.Contains(Path.Combine(tempRoot, "Lists"), all);
        Assert.Contains(Path.Combine(tempRoot, "BasicLands"), all);
    }

    [Fact]
    public void BuildCardDirectory_ModeSet_AppendsSanitizedSetName_CreatesIt()
    {
        Card bolt = MakeCard("Lightning Bolt", set: "LEA", setName: "Limited Edition Alpha");

        string dir = OutputPaths.BuildCardDirectory(bolt, Mode.Set, fileName: "");

        string expected = Path.Combine(tempRoot, "Sets", "Limited Edition Alpha");
        Assert.Equal(expected, dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeAll_AppendsYearThenSanitizedSetName()
    {
        Card bolt = MakeCard("Lightning Bolt", set: "LEA", setName: "Limited Edition Alpha");

        string dir = OutputPaths.BuildCardDirectory(bolt, Mode.All, fileName: "");

        string expected = Path.Combine(tempRoot, "All", "1993", "Limited Edition Alpha");
        Assert.Equal(expected, dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeYears_MatchesAllShape()
    {
        Card delver = MakeCard("Delver of Secrets", set: "ISD", setName: "Innistrad", releasedAt: "2011-09-30");

        string dir = OutputPaths.BuildCardDirectory(delver, Mode.Years, fileName: "");

        Assert.Equal(Path.Combine(tempRoot, "Years", "2011", "Innistrad"), dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeLands_AppendsSanitizedCardName()
    {
        Card plains = MakeCard("Plains", set: "LEA", setName: "Limited Edition Alpha", typeLine: "Basic Land — Plains");

        string dir = OutputPaths.BuildCardDirectory(plains, Mode.Lands, fileName: "");

        Assert.Equal(Path.Combine(tempRoot, "BasicLands", "Plains"), dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeFiles_NoTag_FileNameBecomesSubfolder()
    {
        Card bolt = MakeCard("Lightning Bolt", set: "LEA", setName: "Alpha");
        bolt.Tag = "";

        string dir = OutputPaths.BuildCardDirectory(bolt, Mode.Files, fileName: "decklistA.txt");

        Assert.Equal(Path.Combine(tempRoot, "Lists", "decklistA"), dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeFiles_WithTag_AppendsSanitizedTagUnderFileName()
    {
        Card bolt = MakeCard("Lightning Bolt", set: "LEA");
        bolt.Tag = "burn";

        string dir = OutputPaths.BuildCardDirectory(bolt, Mode.Files, fileName: "decklist.txt");

        Assert.Equal(Path.Combine(tempRoot, "Lists", "decklist", "burn"), dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeFiles_TagWithLeadingDots_IsNeutralized()
    {
        // FU-1 mitigation: "..." → "" via TrimStart('.'); a tag like ".." cannot
        // add an extra path segment, so the returned dir is the file-list base
        // (no tag subdir) — the attack surface is the absence of an extra folder
        // segment, not a literal ".." directory.
        Card mal = MakeCard("Lightning Bolt", set: "LEA");
        mal.Tag = "..";

        string dir = OutputPaths.BuildCardDirectory(mal, Mode.Files, fileName: "evil.txt");

        Assert.Equal(Path.Combine(tempRoot, "Lists", "evil"), dir);
        Assert.True(Directory.Exists(dir));
        // Crucially: no sibling subfolder under "Lists/evil" was created out of the
        // dotted tag — the tag was neutralized to empty, so the returned dir carries
        // no extra path segment.
        Assert.Empty(Directory.GetDirectories(Path.Combine(tempRoot, "Lists", "evil")));
    }

    [Fact]
    public void BuildCardDirectory_ModeFiles_TagWithBackslashes_SanitizedToNothing()
    {
        Card mal = MakeCard("Lightning Bolt", set: "LEA");
        mal.Tag = @"\\";

        string dir = OutputPaths.BuildCardDirectory(mal, Mode.Files, fileName: "evil.txt");

        Assert.Equal(Path.Combine(tempRoot, "Lists", "evil"), dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void BuildCardDirectory_ModeFiles_NullTag_BehavesLikeEmptyTag()
    {
        Card bolt = MakeCard("Lightning Bolt", set: "LEA");
        bolt.Tag = null!;

        string dir = OutputPaths.BuildCardDirectory(bolt, Mode.Files, fileName: "list.txt");

        Assert.Equal(Path.Combine(tempRoot, "Lists", "list"), dir);
        Assert.True(Directory.Exists(dir));
    }

    // --- factory -----------------------------------------------------------

    private static Card MakeCard(
        string name,
        string set = "LEA",
        string setName = "Limited Edition Alpha",
        string releasedAt = "1993-08-05",
        string typeLine = "Instant")
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = releasedAt,
            Layout = "normal",
            TypeLine = typeLine,
            Games = ["paper"],
            FrameEffects = [],
            Reprint = false,
            Variation = false,
            Textless = false,
            Set = set,
            SetName = setName,
            SetType = "core",
            BorderColor = "black",
            Cmc = 1,
            Colors = ["R"],
            ImageUris = new JsonImageUris { Png = "https://test/img.png" },
        };

        return Card.CreateCard(json);
    }
}