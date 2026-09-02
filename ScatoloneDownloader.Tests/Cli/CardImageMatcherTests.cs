using System.Collections.Generic;
using System.Linq;

using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Covers the shared image-to-card name matcher extracted from import/tag/
/// build-views (#19): normalized-name matching, DFC underscore handling,
/// first-wins name dedup, and skipping unmatched files. Files need not exist —
/// the matcher only parses the filename.
/// </summary>
public sealed class CardImageMatcherTests
{
    [Fact]
    public void Match_ByNormalizedName_ReturnsCardWithItsFile()
    {
        Card bolt = MakeCard(id: "p1", name: "Lightning Bolt");
        Card[] cards = [bolt];
        string[] files = [@"C:\master\1993\Alpha\Lightning Bolt.png"];

        List<(Card Card, string FilePath)> matched = CardImageMatcher.Match(cards, files, warnUnmatched: false);

        (Card Card, string FilePath) single = Assert.Single(matched);
        Assert.Same(bolt, single.Card);
        Assert.Equal(files[0], single.FilePath);
    }

    [Fact]
    public void Match_DoubleFacedUnderscoreFilename_ResolvesToSlashName()
    {
        // CardNameNormalizer turns "_" into " // ", so a DFC file matches the
        // Scryfall two-part name.
        Card dfc = MakeCard(id: "p1", name: "Fire // Ice");
        string[] files = [@"C:\master\Fire_Ice.png"];

        List<(Card Card, string FilePath)> matched = CardImageMatcher.Match([dfc], files, warnUnmatched: false);

        Assert.Same(dfc, Assert.Single(matched).Card);
    }

    [Fact]
    public void Match_UnmatchedFile_IsSkipped()
    {
        Card bolt = MakeCard(id: "p1", name: "Lightning Bolt");
        string[] files =
        [
            @"C:\master\Lightning Bolt.png",
            @"C:\master\Totally Not A Card.png",
        ];

        List<(Card Card, string FilePath)> matched = CardImageMatcher.Match([bolt], files, warnUnmatched: false);

        Assert.Same(bolt, Assert.Single(matched).Card);
    }

    [Fact]
    public void Match_DuplicateCardName_FirstWins()
    {
        // Two printings share the same name (reprints). Index is first-wins, so
        // the first card enumerated is the one matched to the file.
        Card first = MakeCard(id: "printing-first", name: "Forest");
        Card second = MakeCard(id: "printing-second", name: "Forest");
        string[] files = [@"C:\master\Forest.png"];

        List<(Card Card, string FilePath)> matched = CardImageMatcher.Match([first, second], files, warnUnmatched: false);

        Assert.Same(first, Assert.Single(matched).Card);
    }

    [Fact]
    public void Match_PreservesFileOrder()
    {
        Card a = MakeCard(id: "p1", name: "Ancestral Recall");
        Card b = MakeCard(id: "p2", name: "Black Lotus");
        string[] files =
        [
            @"C:\master\Black Lotus.png",
            @"C:\master\Ancestral Recall.png",
        ];

        List<(Card Card, string FilePath)> matched = CardImageMatcher.Match([a, b], files, warnUnmatched: false);

        Assert.Equal(["Black Lotus", "Ancestral Recall"], matched.Select(m => m.Card.Name));
    }

    private static Card MakeCard(string id, string name)
    {
        JsonCard json = new()
        {
            Name = name,
            Id = id,
            OracleId = id + "-oracle",
            Language = "en",
            ReleasedAt = "1993-08-05",
            Layout = "normal",
            TypeLine = "Instant",
            Games = ["paper"],
            FrameEffects = [],
            Set = "LEA",
            SetName = "Alpha",
            SetType = "core",
            BorderColor = "black",
            Cmc = 1,
            Colors = [],
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
