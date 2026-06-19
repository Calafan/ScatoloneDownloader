using System.Collections.Generic;

using ScatoloneDownloader.Imaging;
using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
	internal class DoubleFaceCard : Card
	{
		internal string FrontName { get; init; }
		internal string RearName { get; init; }
		internal string FrontImageUri { get; init; }
		internal string RearImageUri { get; init; }


		internal DoubleFaceCard(JsonCard jsonCard) : base(jsonCard)
		{
			FrontName = jsonCard.CardFaces[0].Name;
			RearName = jsonCard.CardFaces[1].Name;

			Colors = new List<string>(jsonCard.CardFaces[0].Colors);
			Colors.AddRange(jsonCard.CardFaces[1].Colors);

			if (jsonCard.CardFaces[0].ImageUris != null)
			{
				FrontImageUri = jsonCard.CardFaces[0].ImageUris.Png;
			}

			if (jsonCard.CardFaces[1].ImageUris != null)
			{
				RearImageUri = jsonCard.CardFaces[1].ImageUris.Png;
			}
		}

		private protected override byte[] ComposePng(GetManager getManager)
		{
			bool isSiege = TypeLine.Contains("Siege");

			return CardImageComposer.ComposeDoubleFace(
				getManager.GetImageStream(FrontImageUri),
				getManager.GetImageStream(RearImageUri),
				isSiege);
		}
	}
}
