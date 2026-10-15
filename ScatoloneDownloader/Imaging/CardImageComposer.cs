using SkiaSharp;

namespace ScatoloneDownloader.Imaging
{
    /// <summary>
    /// Builds the final printable PNG for a card. This is the only component that
    /// references the image library (R5). The pipeline order is load-bearing:
    /// for a double-faced card it is <c>merge → Siege rotation → outer border</c>;
    /// a single face is border-measured and band-painted, then given the outer border.
    /// </summary>
    internal static class CardImageComposer
    {
        // Physical card geometry, used to size the proportional outer border.
        private const double CardWidthMm = 63d;
        private const double CardHeightMm = 88d;
        private const double AdditionalBorderMm = 3d;

        // Border measurement, expressed against the 745px-wide Scryfall PNG and
        // scaled to the actual width.
        private const double ReferenceWidth = 745d;
        private const double SampleInsetReference = 12d;
        private const double MaxThicknessReference = 45d;
        private const double CornerSkipReference = 50d;
        private const int SampleStep = 4;
        private const int WalkStep = 2;
        private const int FirstWalkInset = 4;
        private const int DepartureThreshold = 90;

        // Double-face margin and seam, against the 745px-wide reference.
        private const double MarginReference = 15d;

        /// <summary>Border colour and per-side thickness of one card face.</summary>
        private readonly record struct BorderInfo(SKColor Color, int Left, int Top, int Right, int Bottom);

        /// <summary>Decodes a single-face card and applies the border finishing.</summary>
        internal static byte[] ComposeSingleFace(Stream imageStream)
        {
            using SKBitmap card = Decode(imageStream);

            BorderInfo border = MeasureBorder(card);

            // Composite over the border colour so the transparent rounded corners
            // of the Scryfall PNG become border instead of holes.
            using SKBitmap opaque = new(card.Width, card.Height);

            using (SKCanvas canvas = new(opaque))
            using (SKPaint paint = new() { Color = border.Color, BlendMode = SKBlendMode.Src, IsAntialias = false })
            {
                canvas.Clear(border.Color);
                canvas.DrawBitmap(card, 0, 0);

                canvas.DrawRect(0, 0, border.Left, opaque.Height, paint);
                canvas.DrawRect(opaque.Width - border.Right, 0, border.Right, opaque.Height, paint);
                canvas.DrawRect(0, 0, opaque.Width, border.Top, paint);
                canvas.DrawRect(0, opaque.Height - border.Bottom, opaque.Width, border.Bottom, paint);
            }

            return Finalize(opaque, border.Color);
        }

        /// <summary>
        /// Stacks the two faces at single-card size, optionally rotates a Siege card
        /// 180°, then applies the outer border. The merged canvas is not band-painted
        /// again: its margins are already clean and measured bands would cut into the faces.
        /// </summary>
        internal static byte[] ComposeDoubleFace(Stream frontStream, Stream rearStream, bool isSiege)
        {
            (SKBitmap merged, SKColor background) = MergeFaces(frontStream, rearStream);

            using (merged)
            {
                if (isSiege)
                {
                    using SKBitmap rotated = Rotate(merged, 180f);

                    return Finalize(rotated, background);
                }

                return Finalize(merged, background);
            }
        }

        private static SKBitmap Decode(Stream stream)
        {
            using Stream owned = stream;

            return SKBitmap.Decode(owned)
                ?? throw new InvalidOperationException("Unable to decode card image.");
        }

        /// <summary>
        /// Measures the border colour (per-channel median of opaque pixels just
        /// inside each edge, so one speck cannot colour the bleed) and how deep
        /// the border runs on each side (<see cref="MeasureSide"/>).
        /// </summary>
        private static BorderInfo MeasureBorder(SKBitmap face)
        {
            int width = face.Width;
            int height = face.Height;
            int inset = (int)Math.Round(width * SampleInsetReference / ReferenceWidth);

            List<byte> reds = [];
            List<byte> greens = [];
            List<byte> blues = [];

            void Collect(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                SKColor pixel = face.GetPixel(x, y);

                if (pixel.Alpha == 255)
                {
                    reds.Add(pixel.Red);
                    greens.Add(pixel.Green);
                    blues.Add(pixel.Blue);
                }
            }

            int xFrom = (int)(width * 0.2);
            int xTo = (int)(width * 0.8);
            int yFrom = (int)(height * 0.2);
            int yTo = (int)(height * 0.8);

            for (int y = yFrom; y <= yTo; y += SampleStep)
            {
                Collect(inset, y);
                Collect(width - 1 - inset, y);
            }

            for (int x = xFrom; x <= xTo; x += SampleStep)
            {
                Collect(x, inset);
                Collect(x, height - 1 - inset);
            }

            SKColor color = reds.Count == 0
                ? SKColors.Black
                : new SKColor(Median(reds), Median(greens), Median(blues));

            int max = (int)Math.Round(width * MaxThicknessReference / ReferenceWidth);
            int cornerSkip = (int)Math.Round(width * CornerSkipReference / ReferenceWidth);

            int left = MeasureSide(face, color, height, cornerSkip, max, (depth, line) => (depth, line));
            int right = MeasureSide(face, color, height, cornerSkip, max, (depth, line) => (width - 1 - depth, line));
            int top = MeasureSide(face, color, width, cornerSkip, max, (depth, line) => (line, depth));
            int bottom = MeasureSide(face, color, width, cornerSkip, max, (depth, line) => (line, height - 1 - depth));

            return new BorderInfo(color, left, top, right, bottom);
        }

        /// <summary>
        /// How deep the border runs on one side: the shallowest depth, over EVERY
        /// line along the side, at which the colour leaves the border colour. A
        /// depth is only ever as deep as the cleanest stretch of the whole edge,
        /// so anything printed into the border stops the crop before it: the
        /// collector line and artist credit in the bottom border of modern frames,
        /// and the defence shield a battle prints over its top-right corner, which
        /// a crop measured on a few sample lines cut in half. Not even a few lines
        /// may be written off as dirt: the shield's tip is only a handful of lines
        /// deeper than the rest of it (Invasion of Zendikar: 16px on 4 lines, 20px
        /// from the 10th), and a percentile cut it off.
        /// The corners are left out, because they are transparent; the band
        /// painted over the full side still covers them.
        /// </summary>
        private static int MeasureSide(
            SKBitmap face,
            SKColor color,
            int alongLength,
            int cornerSkip,
            int max,
            Func<int, int, (int X, int Y)> locate)
        {
            int best = max;

            for (int line = cornerSkip; line < alongLength - cornerSkip; line += WalkStep)
            {
                for (int depth = FirstWalkInset; depth < best; depth++)
                {
                    (int x, int y) = locate(depth, line);
                    SKColor pixel = face.GetPixel(x, y);

                    int distance = Math.Abs(pixel.Red - color.Red)
                        + Math.Abs(pixel.Green - color.Green)
                        + Math.Abs(pixel.Blue - color.Blue);

                    if (distance > DepartureThreshold)
                    {
                        best = depth;

                        break;
                    }
                }
            }

            return best <= FirstWalkInset ? 0 : best;
        }

        private static byte Median(List<byte> values)
        {
            values.Sort();

            return values[values.Count / 2];
        }

        /// <summary>
        /// Stacks the two faces (rear on top, front below) on a front-sized canvas.
        /// Each face is cropped to its content first, so the faces are not shrunk by
        /// their own black borders, and both share one scale so they come out the
        /// same size without overlapping.
        /// </summary>
        private static (SKBitmap Canvas, SKColor Background) MergeFaces(Stream frontStream, Stream rearStream)
        {
            using SKBitmap front = Decode(frontStream);
            using SKBitmap rear = Decode(rearStream);

            BorderInfo frontBorder = MeasureBorder(front);
            BorderInfo rearBorder = MeasureBorder(rear);
            SKColor background = frontBorder.Color;

            int canvasWidth = front.Width;
            int canvasHeight = front.Height;

            int margin = (int)Math.Round(canvasWidth * MarginReference / ReferenceWidth);
            int boxWidth = canvasWidth - (2 * margin);
            int boxHeight = (canvasHeight - (3 * margin)) / 2;

            using SKBitmap frontCropped = CropToContent(front, frontBorder);
            using SKBitmap rearCropped = CropToContent(rear, rearBorder);
            using SKBitmap frontRotated = Rotate(frontCropped, 270f);
            using SKBitmap rearRotated = Rotate(rearCropped, 270f);

            float scale = Math.Min(
                Math.Min((float)boxWidth / frontRotated.Width, (float)boxHeight / frontRotated.Height),
                Math.Min((float)boxWidth / rearRotated.Width, (float)boxHeight / rearRotated.Height));

            SKBitmap doubleCard = new(canvasWidth, canvasHeight);

            using (SKCanvas canvas = new(doubleCard))
            using (SKPaint paint = new() { FilterQuality = SKFilterQuality.High, IsAntialias = true })
            {
                canvas.Clear(background);
                DrawCentered(canvas, rearRotated, margin, margin, boxWidth, boxHeight, scale, paint);
                DrawCentered(canvas, frontRotated, margin, margin + boxHeight + margin, boxWidth, boxHeight, scale, paint);
            }

            return (doubleCard, background);
        }

        /// <summary>Crops a face to its content, composited over its border colour so no transparency survives.</summary>
        private static SKBitmap CropToContent(SKBitmap face, BorderInfo border)
        {
            SKBitmap cropped = new(face.Width - border.Left - border.Right, face.Height - border.Top - border.Bottom);

            using (SKCanvas canvas = new(cropped))
            {
                canvas.Clear(border.Color);
                canvas.DrawBitmap(face, -border.Left, -border.Top);
            }

            return cropped;
        }

        private static void DrawCentered(
            SKCanvas canvas,
            SKBitmap bitmap,
            int boxX,
            int boxY,
            int boxWidth,
            int boxHeight,
            float scale,
            SKPaint paint)
        {
            float width = bitmap.Width * scale;
            float height = bitmap.Height * scale;
            SKRect destination = SKRect.Create(
                boxX + ((boxWidth - width) / 2f),
                boxY + ((boxHeight - height) / 2f),
                width,
                height);

            canvas.DrawBitmap(bitmap, destination, paint);
        }

        /// <summary>Adds the proportional outer border in the given colour and encodes PNG.</summary>
        private static byte[] Finalize(SKBitmap card, SKColor borderColor)
        {
            using SKBitmap bordered = AddOuterBorder(card, borderColor);
            using SKData data = bordered.Encode(SKEncodedImageFormat.Png, 100);

            return data.ToArray();
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
