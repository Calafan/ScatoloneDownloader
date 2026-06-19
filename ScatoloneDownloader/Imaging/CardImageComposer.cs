using System;
using System.IO;

using SkiaSharp;

namespace ScatoloneDownloader.Imaging
{
	/// <summary>
	/// Builds the final printable PNG for a card. This is the only component that
	/// references the image library (R5). The pipeline order is load-bearing:
	/// for a double-faced card it is <c>merge → Siege rotation → normalize borders
	/// → outer border</c>; a single face skips the first two steps.
	/// </summary>
	internal static class CardImageComposer
	{
		// Physical card geometry, used to size the proportional outer border.
		private const double CardWidthMm = 63d;
		private const double CardHeightMm = 88d;
		private const double AdditionalBorderMm = 3d;

		// The pixel sampled to pick the border-fill colour.
		private const int BorderSampleX = 20;
		private const int BorderSampleY = 20;

		// Thickness of the edge bands repainted during border normalization.
		private const int NormalizeBorderThickness = 25;


		/// <summary>Decodes a single-face card and applies the border finishing.</summary>
		internal static byte[] ComposeSingleFace(Stream imageStream)
		{
			using SKBitmap card = Decode(imageStream);

			return Finalize(card);
		}

		/// <summary>
		/// Merges the two faces side-by-side at single-card size, optionally rotates
		/// a Siege card 180°, then applies the border finishing.
		/// </summary>
		internal static byte[] ComposeDoubleFace(Stream frontStream, Stream rearStream, bool isSiege)
		{
			using SKBitmap merged = MergeFaces(frontStream, rearStream);

			if (isSiege)
			{
				using SKBitmap rotated = Rotate(merged, 180f);

				return Finalize(rotated);
			}

			return Finalize(merged);
		}


		private static SKBitmap Decode(Stream stream)
		{
			using Stream owned = stream;

			return SKBitmap.Decode(owned)
				?? throw new InvalidOperationException("Unable to decode card image.");
		}

		private static SKBitmap MergeFaces(Stream frontStream, Stream rearStream)
		{
			using SKBitmap front = Decode(frontStream);
			using SKBitmap rear = Decode(rearStream);

			int canvasWidth = front.Width;
			int canvasHeight = front.Height;

			// Background taken from the front face's top-left pixel, as before.
			SKColor background = front.GetPixel(0, 0);

			float resizeRatio = (float)front.Width / front.Height;

			using SKBitmap frontRotated = Rotate(front, 270f);
			using SKBitmap rearRotated = Rotate(rear, 270f);

			float newWidth = rearRotated.Width * resizeRatio;
			float newHeight = rearRotated.Height * resizeRatio;

			SKRect rearRect = SKRect.Create(0, 0, newWidth, newHeight);
			SKRect frontRect = SKRect.Create(0, canvasHeight - newHeight, newWidth, newHeight);

			SKBitmap doubleCard = new(canvasWidth, canvasHeight);

			using (SKCanvas canvas = new(doubleCard))
			using (SKPaint paint = new() { FilterQuality = SKFilterQuality.High, IsAntialias = true })
			{
				canvas.Clear(background);
				canvas.DrawBitmap(rearRotated, rearRect, paint);
				canvas.DrawBitmap(frontRotated, frontRect, paint);
			}

			return doubleCard;
		}

		/// <summary>Applies border normalization then the proportional outer border, and encodes PNG.</summary>
		private static byte[] Finalize(SKBitmap card)
		{
			SKColor borderColor = SamplePixel(card, BorderSampleX, BorderSampleY);

			NormalizeBorders(card, borderColor);

			using SKBitmap bordered = AddOuterBorder(card, borderColor);
			using SKData data = bordered.Encode(SKEncodedImageFormat.Png, 100);

			return data.ToArray();
		}

		private static SKColor SamplePixel(SKBitmap bitmap, int x, int y)
		{
			int sampleX = Math.Clamp(x, 0, bitmap.Width - 1);
			int sampleY = Math.Clamp(y, 0, bitmap.Height - 1);

			// SKBitmap.GetPixel returns a straight (non-premultiplied) colour,
			// matching the old System.Drawing GetPixel.
			return bitmap.GetPixel(sampleX, sampleY);
		}

		private static void NormalizeBorders(SKBitmap bitmap, SKColor color)
		{
			using SKCanvas canvas = new(bitmap);
			using SKPaint paint = new() { Color = color, BlendMode = SKBlendMode.Src, IsAntialias = false };

			int thickness = NormalizeBorderThickness;

			canvas.DrawRect(0, 0, thickness, bitmap.Height, paint);
			canvas.DrawRect(bitmap.Width - thickness, 0, thickness, bitmap.Height, paint);
			canvas.DrawRect(0, 0, bitmap.Width, thickness, paint);
			canvas.DrawRect(0, bitmap.Height - thickness, bitmap.Width, thickness, paint);
		}

		private static SKBitmap AddOuterBorder(SKBitmap bitmap, SKColor color)
		{
			int horizontalBorder = (int)Math.Round(bitmap.Width * (AdditionalBorderMm / CardWidthMm));
			int verticalBorder = (int)Math.Round(bitmap.Height * (AdditionalBorderMm / CardHeightMm));

			SKBitmap bordered = new(bitmap.Width + (horizontalBorder * 2), bitmap.Height + (verticalBorder * 2));

			using (SKCanvas canvas = new(bordered))
			{
				canvas.Clear(color);
				canvas.DrawBitmap(bitmap, horizontalBorder, verticalBorder);
			}

			return bordered;
		}

		/// <summary>Rotates a bitmap clockwise by the given angle (90/180/270).</summary>
		private static SKBitmap Rotate(SKBitmap source, float degrees)
		{
			double radians = degrees * Math.PI / 180d;
			float sin = (float)Math.Abs(Math.Sin(radians));
			float cos = (float)Math.Abs(Math.Cos(radians));

			int newWidth = (int)Math.Round(source.Width * cos + source.Height * sin);
			int newHeight = (int)Math.Round(source.Width * sin + source.Height * cos);

			SKBitmap rotated = new(newWidth, newHeight);

			using (SKCanvas canvas = new(rotated))
			{
				canvas.Translate(newWidth / 2f, newHeight / 2f);
				canvas.RotateDegrees(degrees);
				canvas.DrawBitmap(source, -source.Width / 2f, -source.Height / 2f);
			}

			return rotated;
		}
	}
}
