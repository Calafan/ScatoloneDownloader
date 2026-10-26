using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.Cards
{
    public class JsonCardFace
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("oracle_id")]
        public string? OracleId { get; set; }

        [JsonPropertyName("oracle_text")]
        public string? OracleText { get; set; }

        [JsonPropertyName("type_line")]
        public string? TypeLine { get; set; }

        [JsonPropertyName("power")]
        public string? Power { get; set; }

        [JsonPropertyName("toughness")]
        public string? Toughness { get; set; }

        [JsonPropertyName("image_uris")]
        public JsonImageUris? ImageUris { get; set; }
    }
}
