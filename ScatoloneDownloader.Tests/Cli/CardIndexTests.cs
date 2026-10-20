using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Pins the two bulk lookups <see cref="CardIndex"/> shares between
/// <c>classify</c>, <c>audit</c> and <c>restore</c>: the first card in bulk order
/// wins a duplicate key, and a card with an empty key is not indexed.
/// </summary>
public sealed class CardIndexTests
{
    [Fact]
    public void ByOracleId_FirstPrintingInBulkOrderWins()
    {
        Card first = MakeCard("Bolt (LEA)", id: "p1", oracleId: "o1");
        Card second = MakeCard("Bolt (2ED)", id: "p2", oracleId: "o1");

        Dictionary<string, Card> index = CardIndex.ByOracleId([first, second]);

        Assert.Same(first, Assert.Single(index).Value);
    }

    [Fact]
    public void ByOracleId_SkipsAnEmptyOracleId()
    {
        Card noOracle = MakeCard("Token", id: "p1", oracleId: null);
        Card real = MakeCard("Bolt", id: "p2", oracleId: "o1");

        Dictionary<string, Card> index = CardIndex.ByOracleId([noOracle, real]);

        Assert.Equal(["o1"], index.Keys);
    }

    [Fact]
    public void ById_FirstCardWinsAndAnEmptyIdIsSkipped()
    {
        Card first = MakeCard("A", id: "p1", oracleId: "o1");
        Card duplicate = MakeCard("B", id: "p1", oracleId: "o2");
        Card noId = MakeCard("C", id: null, oracleId: "o3");

        Dictionary<string, Card> index = CardIndex.ById([first, duplicate, noId]);

        Assert.Same(first, Assert.Single(index).Value);
    }

    private static Card MakeCard(string name, string? id, string? oracleId)
    {
        JsonCard json = new()
        {
            Name = name,
            Id = id,
            OracleId = oracleId,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = "Instant",
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 1,
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
