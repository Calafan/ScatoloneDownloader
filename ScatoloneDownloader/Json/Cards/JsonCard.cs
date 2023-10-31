using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Json.Cards
{
	public class JsonCard
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("lang")]
		public string Language { get; set; }

		[JsonPropertyName("released_at")]
		public string ReleasedAt { get; set; }

		[JsonPropertyName("layout")]
		public string Layout { get; set; }

		[JsonPropertyName("image_uris")]
		public JsonImageUris ImageUris { get; set; }

		[JsonPropertyName("card_faces")]
		public List<JsonCardFace> CardFaces { get; set; }

		[JsonPropertyName("type_line")]
		public string TypeLine { get; set; }

		[JsonPropertyName("games")]
		public List<string> Games { get; set; }

		[JsonPropertyName("frame_effects")]
		public List<string> FrameEffects { get; set; }

		[JsonPropertyName("reprint")]
		public bool Reprint { get; set; }

		[JsonPropertyName("variation")]
		public bool Variation { get; set; }

		[JsonPropertyName("set")]
		public string Set { get; set; }

		[JsonPropertyName("set_name")]
		public string SetName { get; set; }

		[JsonPropertyName("set_type")]
		public string SetType { get; set; }

		[JsonPropertyName("border_color")]
		public string BorderColor { get; set; }

		[JsonPropertyName("cmc")]
		public double Cmc { get; set; }

		[JsonPropertyName("colors")]
		public List<string> Colors { get; set; }
	}

}
