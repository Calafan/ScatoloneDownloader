using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.Cards
{
    public class JsonCardFace
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("image_uris")]
        public JsonImageUris ImageUris { get; set; }

        [JsonPropertyName("colors")]
        public List<string> Colors { get; set; }
    }
}
