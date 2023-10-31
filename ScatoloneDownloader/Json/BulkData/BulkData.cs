using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.BulkData
{
	public class BulkData
	{
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("download_uri")]
        public string DownloadUri { get; set; }
    }
}
