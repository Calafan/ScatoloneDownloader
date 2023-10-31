using System.Collections.Generic;
using System.Text.Json.Serialization;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Json.Cards
{
	public class CardSearch
    {
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        [JsonPropertyName("next_page")]
        public string NextPage { get; set; }

        [JsonPropertyName("data")]
        public List<Card> Data { get; set; }
    }
}
