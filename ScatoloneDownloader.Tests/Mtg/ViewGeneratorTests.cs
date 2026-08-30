using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Phase 4 coverage for <see cref="ViewGenerator.GenerateViews"/>: pins the
/// root/exclusion rules from the plan (D7 rating 1-2 excluded entirely,
/// Banned/Token routed only to <c>0_Excluded</c>, all other roots generated
/// per P8) by inspecting the real folder tree it links cards into. Uses a
/// throwaway temp directory tree; link creation falls back from symlink to a
/// native hard link, which works without elevation as long as source and
/// views share a volume (guaranteed here — both live under the same temp root).
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
    public void GenerateViews_Rating0_GoesToUnratedByColorAndMacroType_OnlyAndAlsoGeneralViews()
    {
        var (card, filePath) = MakeCardFile("Unrated Bear", rating: 0, colorIdentity: ["G"], typeLine: "Creature — Bear");

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        AssertLinked(Path.Combine(viewsDir, "0_Unrated", "1 Green", "Creature"), "Unrated Bear.png");
        // General views are not rating-gated, so an unrated card still appears here.
        AssertLinked(Path.Combine(viewsDir, "5_ByType", "Creature"), "Unrated Bear.png");
        // Rating-gated roots must NOT contain an unrated card.
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "2_ByRating")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "1_Deep_Effect")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "1_Deep_Rating")));
    }

    [Theory]
    [InlineData(CardStatus.Banned, "Banned")]
    [InlineData(CardStatus.Token, "Token")]
    public void GenerateViews_BannedOrToken_OnlyGoesToExcludedRoot(CardStatus status, string folder)
    {
        var (card, filePath) = MakeCardFile("Excluded Card", rating: 4, status: status);

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        AssertLinked(Path.Combine(viewsDir, "0_Excluded", folder), "Excluded Card.png");

        // Must not leak into any pool/effect/rating/color/type view.
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "2_ByRating")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "3_ByEffect")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "4_ByColor")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "5_ByType")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "1_Deep_Effect")));
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "1_Deep_Rating")));
    }

    [Fact]
    public void GenerateViews_JollyStatus_IsNotExcluded_BehavesLikeNormalCard()
    {
        var (card, filePath) = MakeCardFile("Jolly Bear", rating: 3, status: CardStatus.Jolly, colorIdentity: ["G"], typeLine: "Creature — Bear");

        ViewGenerator.GenerateViews([(card, filePath)], viewsDir);

        AssertLinked(Path.Combine(viewsDir, "2_ByRating", "3_Stars", "1 Green"), "Jolly Bear.png");
        Assert.False(Directory.Exists(Path.Combine(viewsDir, "0_Excluded")));
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
        double cmc = 1)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = "1993-08-05",
            Layout = "normal",
            TypeLine = typeLine,
            Games = ["paper"],
            FrameEffects = [],
            Set = "LEA",
            SetName = "Alpha",
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
