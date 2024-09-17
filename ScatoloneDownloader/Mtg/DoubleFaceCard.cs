using System.Drawing;
using System.IO;
using System.Reflection.Metadata.Ecma335;

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

			Colors = new System.Collections.Generic.List<string>(jsonCard.CardFaces[0].Colors);
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

		private static Image MergeFaces(Stream frontStream, Stream rearStream)
		{
			Image front = Image.FromStream(frontStream);
			Image rear = Image.FromStream(rearStream);
			Image doubleCard = (Image)front.Clone();

			float percentageResize = ((float)front.Width) / ((float)front.Height);

			Color borderColor = ((Bitmap)front).GetPixel(20, 20);

			front.RotateFlip(RotateFlipType.Rotate270FlipNone);
			rear.RotateFlip(RotateFlipType.Rotate270FlipNone);

			float newWidth = rear.Width * percentageResize;
			float newHeight = rear.Height * percentageResize;

			RectangleF sourceRectangle = new(0, 0, rear.Width, rear.Height);
			RectangleF rearRectangle = new(0, 0, newWidth, newHeight);
			RectangleF frontRectangle = new(0, doubleCard.Height - newHeight, newWidth, newHeight);

			using (Graphics g = Graphics.FromImage(doubleCard))
			{
				g.Clear(((Bitmap)doubleCard).GetPixel(0, 0));
				g.DrawImage(rear, rearRectangle, sourceRectangle, GraphicsUnit.Pixel);
				g.DrawImage(front, frontRectangle, sourceRectangle, GraphicsUnit.Pixel);

				//SizeF sizeF = new(35, 25);

				//g.FillRectangle(new Pen(borderColor).Brush, new RectangleF(new PointF(0, doubleCard.Height - newHeight), sizeF));
				//g.FillRectangle(new Pen(borderColor).Brush, new RectangleF(new PointF(doubleCard.Width - sizeF.Width, doubleCard.Height - newHeight), sizeF));
			}

			front.Dispose();
			rear.Dispose();

			return doubleCard;
		}

		private protected override Image GetImage(GetManager getManager)
		{
			Image mergedImage = MergeFaces(getManager.GetImageStream(FrontImageUri), getManager.GetImageStream(RearImageUri));

			if (TypeLine.Contains("Siege"))
			{
				mergedImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
			}

			return mergedImage;
		}
	}
}
