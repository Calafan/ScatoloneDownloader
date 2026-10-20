using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.Sets
{
    public class Set
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("search_uri")]
        public string? SearchUri { get; set; }

        [JsonPropertyName("released_at")]
        public string? ReleasedAt { get; set; }

        [JsonPropertyName("card_count")]
        public int CardCount { get; set; }
    }
}
