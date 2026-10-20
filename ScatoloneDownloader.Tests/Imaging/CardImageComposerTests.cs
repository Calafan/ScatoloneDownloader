using ScatoloneDownloader.Imaging;

using SkiaSharp;

namespace ScatoloneDownloader.Tests.Imaging;

/// <summary>
/// Geometry checks for the printable-PNG composer. Today this path is verified
/// only by printing physical cards ("manually in stampa", R6 per
/// <c>docs/follow-ups/2026-06-21-pre-existing-findings.md</c>) — these tests
/// at least pin the output dimensions, valid-PNG encoding, and double-face
/// canvas/rotation shape so refactors regress loudly.
///
/// Formula (see <c>CardImageComposer.AddOuterBorder</c>): the proportional
/// outer border adds <c>round(W * 3mm / 63mm)</c> pixels on each horizontal
/// side and <c>round(H * 3mm / 88mm)</c> pixels on each vertical side.
/// Final canvas = <c>(W + 2*hb, H + 2*vb)</c>.
/// </summary>
public sealed class CardImageComposerTests
{
    private const int InputW = 488;
    private const int InputH = 680;

    private const int ExpectedHBorder = 23; // round(488 * 3 / 63) = 23
    private const int ExpectedVBorder = 23; // round(680 * 3 / 88) = 23
    private const int ExpectedFinalW = InputW + (ExpectedHBorder * 2); // 534
    private const int ExpectedFinalH = InputH + (ExpectedVBorder * 2); // 726

    [Fact]
    public void ComposeSingleFace_ReturnsValidPng_WithProportionalOuterBorder()
    {
        using Stream input = MakePng(InputW, InputH, SKColors.DarkRed);

        byte[] output = CardImageComposer.ComposeSingleFace(input);

        using SKBitmap decoded = SKBitmap.Decode(output);

        Assert.NotNull(decoded);
        Assert.Equal(ExpectedFinalW, decoded.Width);
        Assert.Equal(ExpectedFinalH, decoded.Height);
        Assert.True(IsPng(output), "output must be a PNG (magic header 89 50 4E 47)");
    }

    [Fact]
    public void ComposeDoubleFace_CanvasFrontSized_WithOuterBorder_Applied()
    {
        using Stream front = MakePng(InputW, InputH, SKColors.DarkBlue);
        using Stream rear = MakePng(InputW, InputH, SKColors.Firebrick);

        byte[] output = CardImageComposer.ComposeDoubleFace(front, rear, isSiege: false);

        using SKBitmap decoded = SKBitmap.Decode(output);

        Assert.NotNull(decoded);
        // MergeFaces uses front width/height as canvas, then Finalize adds the border.
        Assert.Equal(ExpectedFinalW, decoded.Width);
        Assert.Equal(ExpectedFinalH, decoded.Height);
        Assert.True(IsPng(output));
    }

    [Fact]
    public void ComposeDoubleFace_Siege_PreservesOuterDimensions()
    {
        // A 180° Siege rotation is dimension-preserving (content flips, canvas
        // stays front-sized), so the expected output is identical to non-Siege.
        using Stream front = MakePng(InputW, InputH, SKColors.Purple);
        using Stream rear = MakePng(InputW, InputH, SKColors.Gold);

        byte[] output = CardImageComposer.ComposeDoubleFace(front, rear, isSiege: true);

        using SKBitmap decoded = SKBitmap.Decode(output);

        Assert.NotNull(decoded);
        Assert.Equal(ExpectedFinalW, decoded.Width);
        Assert.Equal(ExpectedFinalH, decoded.Height);
        Assert.True(IsPng(output));
    }

    [Fact]
    public void ComposeSingleFace_ThrowsOnUndecodableStream()
    {
        using Stream garbage = new MemoryStream([0x00, 0x01, 0x02, 0x03]);

        Assert.Throws<InvalidOperationException>(() => CardImageComposer.ComposeSingleFace(garbage));
    }

    [Fact]
    public void ComposeSingleFace_SpeckInBorder_DoesNotColourTheBleed()
    {
        using Stream input = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 0, speck: true);

        byte[] output = CardImageComposer.ComposeSingleFace(input);

        using SKBitmap decoded = SKBitmap.Decode(output);

        Assert.True(IsNearBlack(decoded.GetPixel(2, 2)));
        Assert.True(IsNearBlack(decoded.GetPixel(decoded.Width - 3, decoded.Height / 2)));
        Assert.True(IsNearBlack(decoded.GetPixel(decoded.Width / 2, decoded.Height - 3)));
    }

    // A battle prints its defence shield over the top-right corner of the
    // frame, into the black border. Measuring the border on a few sample lines
    // missed it, and the crop/band then cut the shield in half. The block here
    // stands in for it: inside the border, off every old sample line (30/50/70%
    // of the width is x = 146/244/341), clear of the corner zone.
    private static readonly SKRect Shield = SKRect.Create(380, 8, 50, 12);

    [Fact]
    public void ComposeSingleFace_SomethingPrintedIntoTheBorder_IsNotPaintedOver()
    {
        using Stream input = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 0, protrusion: Shield);

        byte[] output = CardImageComposer.ComposeSingleFace(input);

        using SKBitmap decoded = SKBitmap.Decode(output);

        Assert.Equal(SKColors.Lime, decoded.GetPixel(ExpectedHBorder + 405, ExpectedVBorder + 14));
    }

    [Fact]
    public void ComposeDoubleFace_SomethingPrintedIntoTheBorder_IsNotCropped()
    {
        using Stream front = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 0, protrusion: Shield);
        using Stream rear = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Blue, 0);

        byte[] output = CardImageComposer.ComposeDoubleFace(front, rear, isSiege: true);

        using SKBitmap decoded = SKBitmap.Decode(output);

        bool shieldSurvived = false;
        for (int y = 0; y < decoded.Height && !shieldSurvived; y++)
        {
            for (int x = 0; x < decoded.Width && !shieldSurvived; x++)
            {
                SKColor pixel = decoded.GetPixel(x, y);
                shieldSurvived = pixel.Green > 200 && pixel.Red < 80 && pixel.Blue < 80;
            }
        }

        Assert.True(shieldSurvived, "the shield printed into the border must survive the crop");
    }

    [Fact]
    public void ComposeSingleFace_TransparentCorners_YieldFullyOpaqueOutput()
    {
        using Stream input = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 40);

        byte[] output = CardImageComposer.ComposeSingleFace(input);

        using SKBitmap decoded = SKBitmap.Decode(output);

        AssertFullyOpaque(decoded);
    }

    [Fact]
    public void ComposeDoubleFace_TransparentCorners_YieldFullyOpaqueOutput()
    {
        using Stream front = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 40);
        using Stream rear = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Blue, 40);

        byte[] output = CardImageComposer.ComposeDoubleFace(front, rear, isSiege: false);

        using SKBitmap decoded = SKBitmap.Decode(output);

        AssertFullyOpaque(decoded);
    }

    [Fact]
    public void ComposeDoubleFace_RearOnTop_FacesSameSizeAndSeparated()
    {
        using Stream front = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 0);
        using Stream rear = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Blue, 0);

        byte[] output = CardImageComposer.ComposeDoubleFace(front, rear, isSiege: false);

        AssertStackedFaces(output, topIsBlue: true);
    }

    [Fact]
    public void ComposeDoubleFace_Siege_FrontOnTop_FacesSameSizeAndSeparated()
    {
        using Stream front = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Red, 0);
        using Stream rear = MakeFramedPng(InputW, InputH, 20, SKColors.Black, SKColors.Blue, 0);

        byte[] output = CardImageComposer.ComposeDoubleFace(front, rear, isSiege: true);

        AssertStackedFaces(output, topIsBlue: false);
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>
    /// Reads the vertical centre column and checks: the two face runs are
    /// separated by near-black, are the same length (within 2px) and match the
    /// composer's layout.
    ///
    /// Expected length, from the layout rules with this test's constants:
    /// border t = 20 on every side, so the cropped face is (488 - 2*20) = 448
    /// wide and (680 - 2*20) = 640 tall. Rotated 270° it is 640 x 448. Margin
    /// m = round(488*15/745) = 10, box width 488 - 2m = 468, box height
    /// (680 - 3m) / 2 = 325. Common scale s = min(468 / 640, 325 / 448). The
    /// centre column crosses the rotated height, so each run is round(s * 448).
    /// </summary>
    private static void AssertStackedFaces(byte[] output, bool topIsBlue)
    {
        const int cropped = InputW - (2 * 20);
        const int croppedTall = InputH - (2 * 20);
        const int margin = 10;
        const int boxW = InputW - (2 * margin);
        const int boxH = (InputH - (3 * margin)) / 2;

        double scale = Math.Min((double)boxW / croppedTall, (double)boxH / cropped);
        int expected = (int)Math.Round(scale * cropped);

        using SKBitmap decoded = SKBitmap.Decode(output);

        int x = decoded.Width / 2;
        int blueFirst = -1;
        int blueLast = -1;
        int redFirst = -1;
        int redLast = -1;
        int blue = 0;
        int red = 0;

        for (int y = 0; y < decoded.Height; y++)
        {
            SKColor pixel = decoded.GetPixel(x, y);

            if (pixel.Blue > 200 && pixel.Red < 60)
            {
                blue++;
                blueFirst = blueFirst < 0 ? y : blueFirst;
                blueLast = y;
            }
            else if (pixel.Red > 200 && pixel.Blue < 60)
            {
                red++;
                redFirst = redFirst < 0 ? y : redFirst;
                redLast = y;
            }
        }

        Assert.InRange(Math.Abs(blue - red), 0, 2);
        Assert.InRange(blue, expected - 2, expected + 2);
        Assert.InRange(red, expected - 2, expected + 2);

        // Each run is contiguous.
        Assert.Equal(blue, blueLast - blueFirst + 1);
        Assert.Equal(red, redLast - redFirst + 1);

        (int topLast, int bottomFirst) = topIsBlue ? (blueLast, redFirst) : (redLast, blueFirst);

        Assert.True(topLast < bottomFirst, "the top face must lie entirely above the bottom face");

        for (int y = topLast + 1; y < bottomFirst; y++)
        {
            Assert.True(IsNearBlack(decoded.GetPixel(x, y)), $"seam pixel at y={y} must be near-black");
        }

        Assert.True(bottomFirst - topLast - 1 >= 1);
    }

    private static void AssertFullyOpaque(SKBitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Assert.Equal(255, bitmap.GetPixel(x, y).Alpha);
            }
        }
    }

    private static bool IsNearBlack(SKColor color)
    {
        return color.Red < 40 && color.Green < 40 && color.Blue < 40;
    }

    /// <summary>
    /// Builds a PNG with a border of thickness <paramref name="thickness"/>, an
    /// interior colour, optionally the four corners cleared to transparent with a
    /// square of side <paramref name="corner"/> and a one-pixel white speck at (20, 20).
    /// </summary>
    private static Stream MakeFramedPng(
        int w, int h, int thickness, SKColor border, SKColor interior, int corner, bool speck = false, SKRect? protrusion = null)
    {
        using SKBitmap bmp = new(w, h);
        using (SKCanvas canvas = new(bmp))
        {
            canvas.Clear(border);

            using SKPaint fill = new() { Color = interior, BlendMode = SKBlendMode.Src };
            canvas.DrawRect(thickness, thickness, w - (2 * thickness), h - (2 * thickness), fill);

            using SKPaint clear = new() { Color = SKColors.Transparent, BlendMode = SKBlendMode.Src };
            canvas.DrawRect(0, 0, corner, corner, clear);
            canvas.DrawRect(w - corner, 0, corner, corner, clear);
            canvas.DrawRect(0, h - corner, corner, corner, clear);
            canvas.DrawRect(w - corner, h - corner, corner, corner, clear);
        }

        if (speck)
        {
            bmp.SetPixel(20, 20, SKColors.White);
        }

        if (protrusion is SKRect rect)
        {
            using SKCanvas canvas = new(bmp);
            using SKPaint lime = new() { Color = SKColors.Lime, BlendMode = SKBlendMode.Src };
            canvas.DrawRect(rect, lime);
        }

        SKData data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    }

    /// <summary>Builds an in-memory PNG of the given size and fill colour.</summary>
    private static Stream MakePng(int w, int h, SKColor fill)
    {
        using SKBitmap bmp = new(w, h);
        using (SKCanvas canvas = new(bmp))
        {
            canvas.Clear(fill);
        }
        SKData data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    }

    private static bool IsPng(byte[] bytes)
    {
        // PNG magic: 89 50 4E 47 0D 0A 1A 0A
        return bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47;
    }
}