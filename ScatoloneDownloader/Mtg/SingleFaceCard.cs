using System.Drawing;

using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
	internal class SingleFaceCard : Card
	{
		internal string ImageUri { get; init; }


		internal SingleFaceCard(JsonCard jsonCard) : base(jsonCard)
		{
			ImageUri = jsonCard.ImageUris.Png;
		}

		private protected override Image GetImage(GetManager getManager)
		{
			return Image.FromStream(getManager.GetImageStream(ImageUri));
		}
	}
}
