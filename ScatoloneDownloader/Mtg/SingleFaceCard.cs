using ScatoloneDownloader.Imaging;
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

		private protected override byte[] ComposePng(GetManager getManager)
		{
			return CardImageComposer.ComposeSingleFace(getManager.GetImageStream(ImageUri));
		}
	}
}
