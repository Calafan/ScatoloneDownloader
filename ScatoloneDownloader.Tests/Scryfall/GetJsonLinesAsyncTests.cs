using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Scryfall;

namespace ScatoloneDownloader.Tests.Scryfall;

/// <summary>
/// Regression coverage for the bulk-data ingestion path — the JSONL.gz wire
/// format Scryfall migrated to. The stub handler returns a canned gzipped
/// JSONL body so the test is hermetic (no network) and exercises the real
/// <see cref="ScryfallClient.GetJsonLinesAsync{T}"/> steam -> GZipStream ->
/// line-by-line deserializer path end-to-end.
/// </summary>
public sealed class GetJsonLinesAsyncTests
{
    private const byte SingleFace = 1;
    private const byte DoubleFace = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonCardConverter() }
    };

    [Fact]
    public async Task StreamsCardsFromGzippedJsonl_OnePerLine()
    {
        JsonCard lightning = SampleCard("Lightning Bolt", "LEA");
        JsonCard taiga = SampleCard("Taiga", "2ED", typeLine: "Basic Land — Mountain Forest");
        JsonCard sea = SampleCard("Island", "LEA", typeLine: "Basic Land — Island");

        byte[] gzBody = GzipJsonl([lightning, taiga, sea]);

        using ScryfallClient client = new(new StubHandler(gzBody, HttpStatusCode.OK), _ => Task.CompletedTask);

        List<Card> cards = [];
        await foreach (Card card in client.GetJsonLinesAsync<Card>("https://test/bulk", Options))
        {
            cards.Add(card);
        }

        Assert.Equal(3, cards.Count);
        Assert.Equal("Lightning Bolt", cards[0].Name);
        Assert.Equal("Taiga", cards[1].Name);
        Assert.Equal("Island", cards[2].Name);
        Assert.All(cards, c => Assert.Equal("en", c.Language));
    }

    [Theory]
    [InlineData(SingleFace)]
    [InlineData(DoubleFace)]
    public async Task ReturnsCorrectCardSubtypeBasedOnImageUrisPresence(byte marker)
    {
        JsonCard target = marker == SingleFace
            ? SampleCard("Sol Ring", "LEB")
            : SampleDoubleFace("Delver of Secrets", "ISD");

        using ScryfallClient client = new(new StubHandler(GzipJsonl([target]), HttpStatusCode.OK), _ => Task.CompletedTask);

        List<Card> cards = [];
        await foreach (Card c in client.GetJsonLinesAsync<Card>("https://test/bulk", Options))
        {
            cards.Add(c);
        }

        Card single = Assert.Single(cards);
        if (marker == SingleFace)
        {
            Assert.IsType<SingleFaceCard>(single);
        }
        else
        {
            Assert.IsType<DoubleFaceCard>(single);
        }
    }

    [Fact]
    public async Task SkipsBlankLinesBetweenRecords()
    {
        byte[] body = GzipJsonl([
            SampleCard("Alpha", "LEA"),
            null!, // blank line
            SampleCard("Beta", "LEA"),
        ]);

        using ScryfallClient client = new(new StubHandler(body, HttpStatusCode.OK), _ => Task.CompletedTask);

        List<Card> cards = [];
        await foreach (Card c in client.GetJsonLinesAsync<Card>("https://test/bulk", Options))
        {
            cards.Add(c);
        }

        Assert.Equal(2, cards.Count);
        Assert.Equal("Alpha", cards[0].Name);
        Assert.Equal("Beta", cards[1].Name);
    }

    [Fact]
    public async Task Throws_HttpRequestException_On5xx()
    {
        using ScryfallClient client = new(new StubHandler(Array.Empty<byte>(), HttpStatusCode.InternalServerError), _ => Task.CompletedTask);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (Card _ in client.GetJsonLinesAsync<Card>("https://test/bulk", Options))
            {
            }
        });
    }

    // --- helpers ------------------------------------------------------------

    /// <summary>A single-face card: art at the top level, which is what makes
    /// the factory build a <see cref="SingleFaceCard"/>.</summary>
    private static JsonCard SampleCard(string name, string set, string typeLine = "Instant")
    {
        return TestCards.Json(
            name, typeLine, set: set, setName: "Limited Edition Alpha", setType: "core", releasedAt: "1993-08-05");
    }

    /// <summary>A transform card: art only on the faces, so the factory builds a
    /// <see cref="DoubleFaceCard"/>.</summary>
    private static JsonCard SampleDoubleFace(string name, string set)
    {
        return TestCards.Json(
            name, "Creature — Insect", set: set, setName: "Innistrad", releasedAt: "2011-09-30", layout: "transform",
            png: null,
            faces: [
                new JsonCardFace
                {
                    Name = "Delver of Secrets",
                    ImageUris = new JsonImageUris { Png = "https://test/front.png" },
                },
                new JsonCardFace
                {
                    Name = "Insectile Aberration",
                    ImageUris = new JsonImageUris { Png = "https://test/rear.png" },
                },
            ]);
    }

    /// <summary>
    /// Encodes the supplied <see cref="JsonCard"/>s as one JSON object per
    /// line, gzip-compresses, and returns the body bytes — matching Scryfall's
    /// bulk-data wire format (`.jsonl.gz`). A null entry produces a blank line
    /// so callers can probe blank-line skipping.
    /// </summary>
    private static byte[] GzipJsonl(List<JsonCard?> cards)
    {
        using MemoryStream gz = new();
        using (GZipStream gzip = new(gz, CompressionLevel.Optimal, leaveOpen: true))
        using (StreamWriter writer = new(gzip, Encoding.UTF8, leaveOpen: true))
        {
            foreach (JsonCard? c in cards)
            {
                if (c is null)
                {
                    writer.WriteLine();
                    continue;
                }
                writer.WriteLine(JsonSerializer.Serialize(c));
            }
        }
        return gz.ToArray();
    }

    /// <summary>Returns a fixed canned body for every URL; status code is
    /// configurable per test. Used to drive the production
    /// <see cref="ScryfallClient"/> without wire I/O.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] body;
        private readonly HttpStatusCode status;

        internal StubHandler(byte[] body, HttpStatusCode status)
        {
            this.body = body;
            this.status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(status)
            {
                Content = new ByteArrayContent(body),
                RequestMessage = request,
            };
            return Task.FromResult(response);
        }
    }
}