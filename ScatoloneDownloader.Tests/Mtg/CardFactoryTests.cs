using System;
using System.Collections.Generic;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Coverage for the <see cref="ScatoloneDownloader.Mtg.Card"/> factory (<see cref="ScatoloneDownloader.Mtg.Card.CreateCard"/>),
/// the derived <see cref="SingleFaceCard"/> / <see cref="DoubleFaceCard"/>
/// constructors, and the <see cref="ScatoloneDownloader.Mtg.Card.IsBasicLand"/> derived fact. These
/// are the type-discrimination rules referenced across the filter,
/// downloader, and imaging paths.
/// </summary>
public sealed class CardFactoryTests
{
    [Fact]
    public void CreateCard_WithImageUris_ReturnsSingleFaceCard_AndCopiesPngUri()
    {
        JsonCard json = MakeCard(imageUris: MakePngUri("https://test/bolt.png"));

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        SingleFaceCard single = Assert.IsType<SingleFaceCard>(card);
        Assert.Equal("https://test/bolt.png", single.ImageUri);
        Assert.Equal("Lightning Bolt", single.Name);
        Assert.Equal("en", single.Language);
        Assert.Equal(new DateTime(1993, 8, 5), single.ReleasedAt);
        Assert.Equal("Instant", single.TypeLine);
        Assert.Equal("LEA", single.Set);
        Assert.False(single.IsBasicLand);
    }

    [Fact]
    public void CreateCard_WithoutImageUris_ReturnsDoubleFaceCard_AndReadsFacesFromCardFaces()
    {
        JsonCard json = MakeCard(imageUris: null);
        json.CardFaces =
        [
            new JsonCardFace
            {
                Name = "Delver of Secrets",
                Colors = ["U"],
                ImageUris = MakePngUri("https://test/front.png"),
            },
            new JsonCardFace
            {
                Name = "Insectile Aberration",
                Colors = ["U"],
                ImageUris = MakePngUri("https://test/rear.png"),
            },
        ];

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        DoubleFaceCard doubleFace = Assert.IsType<DoubleFaceCard>(card);
        Assert.Equal("Delver of Secrets", doubleFace.FrontName);
        Assert.Equal("Insectile Aberration", doubleFace.RearName);
        Assert.Equal("https://test/front.png", doubleFace.FrontImageUri);
        Assert.Equal("https://test/rear.png", doubleFace.RearImageUri);
        // Colors are unioned from both faces (face0 colors + face1 colors).
        Assert.Equal(["U", "U"], doubleFace.Colors);
    }

    [Fact]
    public void CreateCard_DoubleFaceFaceWithoutImageUris_LeavesImageUriNull()
    {
        // Some Scryfall double-face entries omit image_uris on one face (e.g.
        // adventure, split with art only on one side). The ctor guards with null
        // checks so absence must not throw.
        JsonCard json = MakeCard(imageUris: null);
        json.CardFaces =
        [
            new JsonCardFace { Name = "Front", Colors = ["R"], ImageUris = MakePngUri("https://test/front.png") },
            new JsonCardFace { Name = "Rear", Colors = ["R"], ImageUris = null! },
        ];

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        DoubleFaceCard doubleFace = Assert.IsType<DoubleFaceCard>(card);
        Assert.Equal("https://test/front.png", doubleFace.FrontImageUri);
        Assert.Null(doubleFace.RearImageUri);
    }

    [Theory]
    [InlineData("Basic Land — Plains", true)]
    [InlineData("Basic Land — Island", true)]
    [InlineData("Basic Snow Land — Forest", true)]
    [InlineData("Land — Plains", false)]
    [InlineData("Creature — Goblin", false)]
    [InlineData("Basic // Creature", false)]
    [InlineData("Basic Creature", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsBasicLand_DetectsBasicLandTypeLine(string? typeLine, bool expected)
    {
        JsonCard json = MakeCard(typeLine: typeLine ?? string.Empty, imageUris: MakePngUri("https://test/x.png"));

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        Assert.Equal(expected, card.IsBasicLand);
    }

    [Fact]
    public void CreateCard_PreservesAllBaseFields_FromJsonCard()
    {
        JsonCard json = new()
        {
            Name = "Wrath of God",
            Language = "en",
            ReleasedAt = "1993-08-05",
            Layout = "normal",
            TypeLine = "Sorcery",
            Games = ["paper", "mtgo"],
            FrameEffects = ["extendedart"],
            Reprint = true,
            Variation = true,
            Textless = true,
            Set = "2ED",
            SetName = "Unlimited Edition",
            SetType = "core",
            BorderColor = "white",
            Cmc = 2.5,
            Colors = ["W"],
            ImageUris = MakePngUri("https://test/wog.png"),
        };

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        Assert.Equal("Wrath of God", card.Name);
        Assert.Equal("en", card.Language);
        Assert.Equal("normal", card.Layout);
        Assert.Equal(new DateTime(1993, 8, 5), card.ReleasedAt);
        Assert.Equal("Sorcery", card.TypeLine);
        Assert.Equal(["paper", "mtgo"], card.Games);
        Assert.Equal(["extendedart"], card.FrameEffects);
        Assert.True(card.Reprint);
        Assert.True(card.Variation);
        Assert.True(card.Textless);
        Assert.Equal("2ED", card.Set);
        Assert.Equal("Unlimited Edition", card.SetName);
        Assert.Equal("core", card.SetType);
        Assert.Equal("white", card.BorderColor);
        Assert.Equal(2.5, card.Cmc);
        Assert.Equal(["W"], card.Colors);
    }

    [Fact]
    public void CreateCard_MapsOracleTextAndKeywords_FromTopLevel()
    {
        JsonCard json = MakeCard(imageUris: MakePngUri("https://test/x.png"));
        json.OracleText = "Deal 3 damage to any target.";
        json.Keywords = ["Flash"];

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        Assert.Equal("Deal 3 damage to any target.", card.OracleText);
        Assert.Equal(["Flash"], card.Keywords);
    }

    [Fact]
    public void CreateCard_DoubleFace_AggregatesFaceOracleText_WhenTopLevelEmpty()
    {
        // DFC entries carry rules text on the faces, not the top level.
        JsonCard json = MakeCard(imageUris: null);
        json.OracleText = null;
        json.CardFaces =
        [
            new JsonCardFace { Name = "Front", Colors = ["U"], OracleText = "Front text.", ImageUris = MakePngUri("https://test/f.png") },
            new JsonCardFace { Name = "Rear", Colors = ["U"], OracleText = "Rear text.", ImageUris = MakePngUri("https://test/r.png") },
        ];

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        Assert.Equal("Front text.\nRear text.", card.OracleText);
    }

    [Fact]
    public void CreateCard_NoOracleTextOrKeywords_DefaultToEmpty()
    {
        JsonCard json = MakeCard(imageUris: MakePngUri("https://test/x.png"));
        // OracleText and Keywords intentionally left null on the JSON.

        ScatoloneDownloader.Mtg.Card card = ScatoloneDownloader.Mtg.Card.CreateCard(json);

        Assert.Equal(string.Empty, card.OracleText);
        Assert.Empty(card.Keywords);
    }

    // --- factory -----------------------------------------------------------

    private static JsonCard MakeCard(
        string name = "Lightning Bolt",
        string set = "LEA",
        string releasedAt = "1993-08-05",
        string typeLine = "Instant",
        JsonImageUris? imageUris = null)
    {
        return new JsonCard
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
            SetName = "Limited Edition Alpha",
            SetType = "core",
            BorderColor = "black",
            Cmc = 1,
            Colors = ["R"],
            ImageUris = imageUris,
        };
    }

    private static JsonImageUris MakePngUri(string png) => new() { Png = png };
}