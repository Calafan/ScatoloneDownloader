using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.Cards
{
    public class JsonImageUris
    {
        [JsonPropertyName("png")]
        public string? Png { get; set; }
    }
}
