using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.BulkData
{
	public class BulkData
	{
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("jsonl_download_uri")]
        public string JsonlDownloadUri { get; set; }
    }
}
