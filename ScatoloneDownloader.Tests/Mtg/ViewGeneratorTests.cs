using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Phase 4 + Plan Part B coverage for <see cref="ViewGenerator.GenerateViews"/>:
/// pins the root/exclusion rules (normal rating 1-2 excluded entirely per D7;
/// any status card routed to a single flat <c>0_Banned</c>/<c>0_Token</c>/
/// <c>0_Jolly</c> tag folder; unrated cards kept to <c>0_Unrated/{Year}/{Set}</c>
/// only; rating 3-5 filling the browse roots) by inspecting the real folder tree
/// it links cards into. Uses a throwaway temp directory tree; link creation falls
/// back from symlink to a native hard link, which works without elevation as long
/// as source and views share a volume (guaranteed here — both live under the same
/// temp root).
/// </summary>
public sealed class ViewGeneratorTests : IDisposable
{
    private readonly string tempRoot;
    private readonly string masterDir;
    private readonly string viewsDir;

    public ViewGeneratorTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "ScatoloneViewTests_" + Guid.NewGuid().ToString("N"));
        masterDir = Path.Combine(tempRoot, "Master");
        viewsDir = Path.Combine(tempRoot, "Views");
        Directory.CreateDirectory(masterDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GenerateViews_Rating1Or2_ExcludedFromEveryRoot(int rating)
    {
        var (card, filePath) = MakeCardFile("Excluded Bear", rating: rating);

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        // D7: no view root should contain this card at all.
        Assert.True(!Directory.Exists(viewsDir) || Directory.GetFiles(viewsDir, "*.png", SearchOption.AllDirectories).Length == 0);
    }

    [Fact]
    public void GenerateViews_Rating0_GoesToUnratedByYearAndSet_Only()
    {
        // Default MakeCardFile releasedAt/setName are "1993-08-05" / "Alpha".
        var (card, filePath) = MakeCardFile("Unrated Bear", rating: 0, colorIdentity: ["G"], typeLine: "Creature — Bear");

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        // B4: 0_Unrated is keyed by Year/Set, mirroring the physical Source layout.
        AssertLinked(Path.Combine(viewsDir, "0_Unrated", "1993", "Alpha"), "Unrated Bear.png");
        // #1: an unrated card lives ONLY in 0_Unrated — it must not leak into any
        // browse view, so the ~26k-card backlog can never flood/choke them.
        foreach (string other in new[] { "2_ByRating", "3_ByEffect", "4_ByColor", "5_ByType", "1_Deep_Effect", "1_Deep_Rating" })
        {
            Assert.False(Directory.Exists(Path.Combine(viewsDir, other)), $"unrated card must not appear in {other}");
        }
    }

    [Fact]
    public void GenerateViews_Rating0_DifferentYearsAndSets_LandInDifferentUnratedBuckets()
    {
        var (oldCard, oldPath) = MakeCardFile("Old Bear", rating: 0, releasedAt: "1994-04-01", setName: "The Dark");
        var (newCard, newPath) = MakeCardFile("New Bear", rating: 0, releasedAt: "2026-03-01", setName: "Some Future Set");

        ViewGenerator.GenerateViews([(oldCard, oldPath), (newCard, newPath)], viewsDir);

        AssertLinked(Path.Combine(viewsDir, "0_Unrated", "1994", "The Dark"), "Old Bear.png");
        AssertLinked(Path.Combine(viewsDir, "0_Unrated", "2026", "Some Future Set"), "New Bear.png");
    }

    [Fact]
    public void GenerateViews_Rating0_SetNameWithForbiddenCharacters_IsSanitized()
    {
        var (card, filePath) = MakeCardFile("Wasteland Bear", rating: 0, releasedAt: "2024-03-08", setName: "Fallout: Wasteland");

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        // OutputPaths.Sanitize strips filesystem-forbidden characters (":" here)
        // so the folder name is always a valid path segment.
        AssertLinked(Path.Combine(viewsDir, "0_Unrated", "2024", "Fallout Wasteland"), "Wasteland Bear.png");
    }

    [Theory]
    [InlineData(CardStatus.Banned, "0_Banned")]
    [InlineData(CardStatus.Token, "0_Token")]
    [InlineData(CardStatus.Jolly, "0_Jolly")]
    public void GenerateViews_StatusCard_GoesToSingleFlatTagFolder_Only(CardStatus status, string folder)
    {
        var (card, filePath) = MakeCardFile("Status Card", rating: 4, status: status, colorIdentity: ["G"], typeLine: "Creature — Bear");

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        // A single flat folder per tag at the top level — the card sits directly
        // inside, with no color/type/cost split.
        AssertLinked(Path.Combine(viewsDir, folder), "Status Card.png");

        // And nowhere else.
        foreach (string other in new[] { "2_ByRating", "3_ByEffect", "4_ByColor", "5_ByType", "1_Deep_Effect", "1_Deep_Rating", "0_Unrated" })
        {
            Assert.False(Directory.Exists(Path.Combine(viewsDir, other)), $"status card must not appear in {other}");
        }
    }

    [Fact]
    public void GenerateViews_StatusCard_OverridesRating1Or2Exclusion()
    {
        // A tagged card must still surface in its flat folder even at rating 1-2,
        // which would exclude a NORMAL card from every view (D7).
        var (card, filePath) = MakeCardFile("Banned Weakling", rating: 1, status: CardStatus.Banned);

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        AssertLinked(Path.Combine(viewsDir, "0_Banned"), "Banned Weakling.png");
    }

    [Fact]
    public void GenerateViews_RatedNoEffect_UsesUntaggedInDeepViews()
    {
        var (card, filePath) = MakeCardFile("Vanilla Bear", rating: 4, colorIdentity: ["G"], typeLine: "Creature — Bear", cmc: 3);

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        AssertLinked(Path.Combine(viewsDir, "1_Deep_Effect", "1 Green", "Creature", "_Untagged", "Cost 3", "4_Stars"), "Vanilla Bear.png");
        AssertLinked(Path.Combine(viewsDir, "1_Deep_Rating", "1 Green", "4_Stars", "Creature", "_Untagged", "Cost 3"), "Vanilla Bear.png");
        AssertLinked(Path.Combine(viewsDir, "3_ByEffect", "_Untagged", "1 Green"), "Vanilla Bear.png");
    }

    [Fact]
    public void GenerateViews_RatedMultiEffect_MultiLinksEveryRootByEffect()
    {
        var (card, filePath) = MakeCardFile(
            "Multi Effect Bear", rating: 5, colorIdentity: ["G"], typeLine: "Creature — Bear", cmc: 2,
            effects: CardEffect.Ramp | CardEffect.Buff);

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        foreach (string effectName in new[] { "Ramp", "Buff" })
        {
            AssertLinked(Path.Combine(viewsDir, "1_Deep_Effect", "1 Green", "Creature", effectName, "Cost 2", "5_Stars"), "Multi Effect Bear.png");
            AssertLinked(Path.Combine(viewsDir, "1_Deep_Rating", "1 Green", "5_Stars", "Creature", effectName, "Cost 2"), "Multi Effect Bear.png");
            AssertLinked(Path.Combine(viewsDir, "3_ByEffect", effectName, "1 Green"), "Multi Effect Bear.png");
        }

        AssertLinked(Path.Combine(viewsDir, "2_ByRating", "5_Stars", "1 Green"), "Multi Effect Bear.png");
        AssertLinked(Path.Combine(viewsDir, "4_ByColor", "1 Green", "Creature", "Cost 2"), "Multi Effect Bear.png");
        AssertLinked(Path.Combine(viewsDir, "5_ByType", "Creature"), "Multi Effect Bear.png");
    }

    private static void AssertLinked(string directory, string fileName)
    {
        Assert.True(Directory.Exists(directory), $"Expected directory to exist: {directory}");
        Assert.True(File.Exists(Path.Combine(directory, fileName)), $"Expected file to exist: {Path.Combine(directory, fileName)}");
    }

    private (Card Card, string FilePath) MakeCardFile(
        string name,
        int rating = 0,
        CardStatus status = CardStatus.None,
        CardEffect effects = CardEffect.None,
        List<string>? colorIdentity = null,
        string typeLine = "Instant",
        double cmc = 1,
        string releasedAt = "1993-08-05",
        string setName = "Alpha")
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
            Set = "LEA",
            SetName = setName,
            SetType = "core",
            BorderColor = "black",
            Cmc = cmc,
            Colors = colorIdentity ?? [],
            ColorIdentity = colorIdentity ?? [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.Status = status;
        card.Effects = effects;

        string filePath = Path.Combine(masterDir, name + ".png");
        File.WriteAllBytes(filePath, [0x89, 0x50, 0x4E, 0x47]); // dummy content; ViewGenerator only checks existence.

        return (card, filePath);
    }
}
