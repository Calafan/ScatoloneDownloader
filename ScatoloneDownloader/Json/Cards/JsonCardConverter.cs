using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Json.Cards
{
	internal class JsonCardConverter : JsonConverter<Card>
	{
		public override Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			JsonCard jsonCard = JsonSerializer.Deserialize<JsonCard>(ref reader, options);

			return Card.CreateCard(jsonCard);
		}

		public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options)
		{
			throw new NotImplementedException();
		}
	}
}
