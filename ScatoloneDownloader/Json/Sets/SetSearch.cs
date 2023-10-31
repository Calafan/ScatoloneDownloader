using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.Sets
{
	public class SetSearch
	{
        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        [JsonPropertyName("data")]
        public List<Set> Sets { get; set; }
    }
}
