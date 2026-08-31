using System;
using System.Linq;

using ScatoloneDownloader.Cli;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Covers <see cref="MakeListCommand.BuildListText"/>: the flat alphabetical
/// pool body, the per-status sections written as <c>Name -- Status</c>, section
/// ordering, and — critically — that a generated status line splits back to the
/// right name and tag through the SAME logic the download reader uses.
/// </summary>
public sealed class MakeListCommandTests
{
    [Fact]
    public void BuildListText_PlainPoolCards_FlatAlphabetical_NoInlineTag()
    {
        CardMetadataEntry[] entries =
        [
            Entry("Wrath of God", 5),
            Entry("Counterspell", 4),
            Entry("Ancestral Recall", 5),
        ];

        string text = MakeListCommand.BuildListText(entries, "metadata");
        string[] lines = Lines(text);

        Assert.Contains("-- Cube download list: pool (rating 3-5), 3 cards", lines);

        int first = Array.FindIndex(lines, l => l.Length > 0 && !l.StartsWith("--"));
        Assert.Equal("Ancestral Recall", lines[first]);
        Assert.Equal("Counterspell", lines[first + 1]);
        Assert.Equal("Wrath of God", lines[first + 2]);

        // No plain card carries an inline " -- " tag.
        Assert.DoesNotContain(" -- ", text);
    }

    [Fact]
    public void BuildListText_StatusCards_GoToOwnSections_TaggedInline_AndNotInFlatBody()
    {
        CardMetadataEntry[] entries =
        [
            Entry("Sol Ring", 5, CardStatus.Banned),
            Entry("Krenko, Mob Boss", 5, CardStatus.Token),
            Entry("Plain Card", 4),
        ];

        string text = MakeListCommand.BuildListText(entries, "m");

        Assert.Contains("\nPlain Card\n", text);                       // plain, bare
        Assert.Contains("\n-- Banned\nSol Ring -- Banned\n", text);    // own section, tagged
        Assert.Contains("\n-- Token\nKrenko, Mob Boss -- Token\n", text);
        Assert.DoesNotContain("-- Jolly", text);                      // empty section skipped

        // The banned card must NOT also appear as a bare "Sol Ring" line.
        Assert.DoesNotContain("\nSol Ring\n", text);
    }

    [Fact]
    public void BuildListText_StatusSections_InFixedOrder_BannedTokenJolly()
    {
        CardMetadataEntry[] entries =
        [
            Entry("J Card", 5, CardStatus.Jolly),
            Entry("B Card", 5, CardStatus.Banned),
            Entry("T Card", 5, CardStatus.Token),
        ];

        string text = MakeListCommand.BuildListText(entries, "m");

        int banned = text.IndexOf("-- Banned", StringComparison.Ordinal);
        int token = text.IndexOf("-- Token", StringComparison.Ordinal);
        int jolly = text.IndexOf("-- Jolly", StringComparison.Ordinal);

        Assert.True(banned >= 0 && banned < token && token < jolly);
    }

    [Fact]
    public void GeneratedStatusLine_SplitsBackToNameAndTag_LikeTheDownloadReader()
    {
        // Mirror GetManager.GetCardList's line parsing to prove the generated
        // format round-trips: name before "--", tag after.
        string text = MakeListCommand.BuildListText([Entry("Sol Ring", 5, CardStatus.Banned)], "m");
        string line = Lines(text).First(l => !l.StartsWith("--") && l.Contains("--"));

        string[] parts = line.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal("Sol Ring", parts[0]);
        Assert.Equal("Banned", parts[1]);
    }

    private static CardMetadataEntry Entry(string name, int rating, CardStatus status = CardStatus.None)
    {
        return new CardMetadataEntry { Name = name, Rating = rating, StatusValue = status };
    }

    private static string[] Lines(string text)
    {
        return text.Replace("\r\n", "\n").Split('\n');
    }
}
