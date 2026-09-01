using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ScatoloneDownloader.Download;

using Xunit;

namespace ScatoloneDownloader.Tests.Download;

/// <summary>
/// Pins the shared download-list parser used by both the <c>files</c> command
/// (via GetManager) and <c>make-list</c>: <c>Name</c> / <c>Name -- tag</c> lines,
/// comment and blank lines skipped, whitespace trimmed.
/// </summary>
public sealed class CardListFileTests
{
    [Fact]
    public async Task ReadAsync_ParsesNames_TagsCommentsAndBlanks()
    {
        string path = await WriteTempAsync(
            "-- Cube download list: pool, 3 cards",
            "-- source: metadata",
            "",
            "Lightning Bolt",
            "  Sol Ring  ",
            "",
            "-- Banned",
            "Black Lotus -- Banned",
            "Mind Twist  --  Banned  ");

        try
        {
            List<CardListEntry> entries = await CardListFile.ReadAsync(path);

            Assert.Equal(4, entries.Count);
            Assert.Equal(new CardListEntry("Lightning Bolt", ""), entries[0]);
            Assert.Equal(new CardListEntry("Sol Ring", ""), entries[1]);
            Assert.Equal(new CardListEntry("Black Lotus", "Banned"), entries[2]);
            // Trimmed on both sides of the "--" separator.
            Assert.Equal(new CardListEntry("Mind Twist", "Banned"), entries[3]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_TrailingSeparator_YieldsEmptyTag()
    {
        string path = await WriteTempAsync("Counterspell --");

        try
        {
            List<CardListEntry> entries = await CardListFile.ReadAsync(path);

            CardListEntry only = Assert.Single(entries);
            Assert.Equal(new CardListEntry("Counterspell", ""), only);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_OnlyCommentsAndBlanks_ReturnsEmpty()
    {
        string path = await WriteTempAsync("-- header", "", "   ", "-- another comment");

        try
        {
            Assert.Empty(await CardListFile.ReadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> WriteTempAsync(params string[] lines)
    {
        string path = Path.Combine(Path.GetTempPath(), "cardlist_" + Path.GetRandomFileName() + ".txt");
        await File.WriteAllLinesAsync(path, lines);
        return path;
    }
}
