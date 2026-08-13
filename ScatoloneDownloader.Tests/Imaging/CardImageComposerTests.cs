using System.IO;

using ScatoloneDownloader.Imaging;

using SkiaSharp;

using Xunit;

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

    // --- helpers -----------------------------------------------------------

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