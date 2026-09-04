using System.Text.Json;
using System.Text.Json.Serialization;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Json.Cards
{
    internal class JsonCardConverter : JsonConverter<Card>
    {
        public override Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonCard jsonCard = JsonSerializer.Deserialize<JsonCard>(ref reader, options)
                ?? throw new JsonException("Expected a card object, found null.");

            return Card.CreateCard(jsonCard);
        }

        public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options)
        {
            // Read-only converter: Card is only ever deserialized from Scryfall,
            // never written back out. Serialize a JsonCard directly if you need JSON.
            throw new NotSupportedException($"{nameof(Card)} is deserialize-only; it cannot be written back to JSON.");
        }
    }
}
