using System;
using System.IO;
using System.Threading.Tasks;

using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Imaging;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Download
{
	/// <summary>
	/// Fetches a card's face image(s), composes the printable PNG, and writes it to
	/// disk. Holds the download/path/output behavior that used to live on
	/// <see cref="Card"/>, leaving the card itself as data.
	/// </summary>
	internal sealed class CardDownloader
	{
		private const string ListFileName = "List.txt";

		private readonly GetManager getManager;

		internal CardDownloader(GetManager getManager)
		{
			this.getManager = getManager;
		}

		internal async Task DownloadAsync(Card card, Mode mode, string fileName)
		{
			string baseDirectory = OutputPaths.BuildCardDirectory(card, mode, fileName);

			int i = 1;
			string validName = OutputPaths.Sanitize(card.Name);
			string path = Path.Combine(baseDirectory, validName);

			while (File.Exists(path + ".png"))
			{
				path = Path.Combine(baseDirectory, validName + i++.ToString());
			}

			// Le carte sono in ordine casuale ma voglio che l'art originale abbia sempre il nome senza numero.
			// This is one of the two places the canonical-artwork rule lives (see GetManager.PopulateCardsByName).
			if (i != 1 && !card.IsBasicLand && CardFilter.IsDownloadable(card, false, false))
			{
				File.Move(Path.Combine(baseDirectory, validName) + ".png", path + ".png");
				path = Path.Combine(baseDirectory, validName);
			}

			byte[] png = await ComposeAsync(card);
			await File.WriteAllBytesAsync(path + ".png", png);
		}

		internal void WriteToList(Card card)
		{
			string baseDirectory = OutputPaths.BuildCardDirectory(card, Mode.Files, string.Empty);

			File.AppendAllText(Path.Combine(baseDirectory, ListFileName), card.Name + "\n");
		}

		private async Task<byte[]> ComposeAsync(Card card)
		{
			switch (card)
			{
				case DoubleFaceCard doubleFace:
				{
					Stream front = await getManager.GetImageStreamAsync(doubleFace.FrontImageUri);
					Stream rear = await getManager.GetImageStreamAsync(doubleFace.RearImageUri);
					bool isSiege = doubleFace.TypeLine.Contains("Siege");

					return CardImageComposer.ComposeDoubleFace(front, rear, isSiege);
				}
				case SingleFaceCard singleFace:
				{
					Stream image = await getManager.GetImageStreamAsync(singleFace.ImageUri);

					return CardImageComposer.ComposeSingleFace(image);
				}
				default:
					throw new InvalidOperationException("Unknown card type: " + card.GetType().Name);
			}
		}
	}
}
