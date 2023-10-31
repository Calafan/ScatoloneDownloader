using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.BulkData
{
	public class BulkDataCollection
	{
        [JsonPropertyName("data")]
        public List<BulkData> Data { get; set; }
    }
}
