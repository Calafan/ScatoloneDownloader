using System.Text.Json;

using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Covers the pure core of <c>register</c>: which matched cards become new unrated
/// entries, and the guarantee that an entry already in the store is never touched.
/// </summary>
public sealed class RegisterCommandTests
{
    [Fact]
    public void RegisterNew_NewCard_IsAddedUnratedWithNameAndScryfallId()
    {
        CubeMetadata metadata = new();
        Card bolt = MakeCard(id: "print-bolt", oracleId: "oracle-bolt", name: "Lightning Bolt");

        RegisterResult result = RegisterCommand.RegisterNew(metadata, [(bolt, @"C:\lib\Lightning Bolt.png")]);

        CardMetadataEntry entry = Assert.Single(metadata.Cards).Value;
        Assert.Equal("oracle-bolt", Assert.Single(metadata.Cards).Key);
        Assert.Equal("Lightning Bolt", entry.Name);
        Assert.Equal(0, entry.Rating);
        Assert.Equal("print-bolt", entry.ScryfallId);
        Assert.Null(entry.Status);
        Assert.Empty(entry.Effects);
        Assert.Null(entry.ReviewedAt);
        Assert.Equal(["Lightning Bolt"], result.AddedNames);
    }

    [Fact]
    public void RegisterNew_ExistingEntry_IsLeftUntouched_EvenWhenTheMatchedCardDiffers()
    {
        CubeMetadata metadata = new();
        metadata.Cards["oracle-bolt"] = new CardMetadataEntry
        {
            Name = "Lightning Bolt",
            Rating = 5,
            ScryfallId = "print-original",
            Status = "Banned",
            Effects = ["Burn", "Removal"],
            ReviewedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        string before = JsonSerializer.Serialize(metadata.Cards["oracle-bolt"]);

        // Same oracle, different printing id and a different name on the matched card.
        Card other = MakeCard(id: "print-other", oracleId: "oracle-bolt", name: "Bolt, Renamed");

        RegisterResult result = RegisterCommand.RegisterNew(metadata, [(other, @"C:\lib\Bolt.png")]);

        Assert.Equal(before, JsonSerializer.Serialize(Assert.Single(metadata.Cards).Value));
        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.AlreadyInStore);
    }

    [Fact]
    public void RegisterNew_TwoFilesOfOneOracle_AddOneEntry()
    {
        CubeMetadata metadata = new();
        Card first = MakeCard(id: "print-1", oracleId: "oracle-x", name: "Island");
        Card second = MakeCard(id: "print-2", oracleId: "oracle-x", name: "Island");

        RegisterResult result = RegisterCommand.RegisterNew(
            metadata, [(first, @"C:\lib\a\Island.png"), (second, @"C:\lib\b\Island.png")]);

        Assert.Single(metadata.Cards);
        Assert.Equal("print-1", metadata.Cards["oracle-x"].ScryfallId);
        Assert.Equal(1, result.Added);
        // The second file is not "already in the store": the store did not hold it
        // before this call.
        Assert.Equal(0, result.AlreadyInStore);
    }

    [Fact]
    public void RegisterNew_EmptyOracleId_IsSkippedAndCounted()
    {
        CubeMetadata metadata = new();
        Card token = MakeCard(id: "print-t", oracleId: string.Empty, name: "Some Token");

        RegisterResult result = RegisterCommand.RegisterNew(metadata, [(token, @"C:\lib\Some Token.png")]);

        Assert.Empty(metadata.Cards);
        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.SkippedNoOracleId);
    }

    [Fact]
    public void RegisterNew_Counts_SeparateAddedKnownAndSkipped()
    {
        CubeMetadata metadata = new();
        metadata.Cards["oracle-known"] = new CardMetadataEntry { Name = "Known", Rating = 3 };

        List<(Card Card, string FilePath)> matched =
        [
            (MakeCard("p1", "oracle-known", "Known"), @"C:\lib\Known.png"),
            (MakeCard("p1b", "oracle-known", "Known"), @"C:\lib\x\Known.png"),
            (MakeCard("p2", "oracle-new-a", "Alpha"), @"C:\lib\Alpha.png"),
            (MakeCard("p3", "oracle-new-b", "Beta"), @"C:\lib\Beta.png"),
            (MakeCard("p4", string.Empty, "Nameless"), @"C:\lib\Nameless.png"),
        ];

        RegisterResult result = RegisterCommand.RegisterNew(metadata, matched);

        Assert.Equal(["Alpha", "Beta"], result.AddedNames);
        Assert.Equal(2, result.Added);
        // Two files of the known oracle count as ONE known card.
        Assert.Equal(1, result.AlreadyInStore);
        Assert.Equal(1, result.SkippedNoOracleId);
        Assert.Equal(3, metadata.Cards.Count);
    }

    private static Card MakeCard(string id, string oracleId, string name)
    {
        JsonCard json = new()
        {
            Name = name,
            Id = id,
            OracleId = oracleId,
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
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
