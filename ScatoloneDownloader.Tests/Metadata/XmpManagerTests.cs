using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using ScatoloneDownloader.Metadata;

using Xunit;

namespace ScatoloneDownloader.Tests.Metadata;

/// <summary>
/// Covers <see cref="XmpManager.ReadMetadata"/>'s PNG fast path: the chunk-table
/// walk that replaced decoding the whole image to reach two short strings. The
/// fixtures are real PNG byte streams (correct signature, chunk lengths and
/// CRCs) built in memory, so the reader is exercised the way Adobe Bridge writes
/// files — including the compressed and alternate text-chunk layouts the spec
/// allows, which Bridge does not currently emit but a future version could.
/// Anything the fast path rejects falls through to Magick.NET, which on these
/// pixel-less fixtures yields the (0, "") "no metadata" answer.
/// </summary>
public sealed class XmpManagerTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "xmp-tests-" + Guid.NewGuid().ToString("N"));

    public XmpManagerTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
    }

    [Fact]
    public void ReadMetadata_AttributeForm_ReadsRatingAndLabel()
    {
        string path = WritePng("attr.png", ITxt(XmpPacket(@"xmp:Rating=""4"" xmp:Label=""Red""")));

        Assert.Equal((4, "Red"), XmpManager.ReadMetadata(path));
    }

    [Fact]
    public void ReadMetadata_ElementForm_ReadsRatingAndLabel()
    {
        string body = "<xmp:Rating>3</xmp:Rating><xmp:Label>Green</xmp:Label>";
        string path = WritePng("elem.png", ITxt(XmpPacket(string.Empty, body)));

        Assert.Equal((3, "Green"), XmpManager.ReadMetadata(path));
    }

    [Fact]
    public void ReadMetadata_UnratedFile_ReadsZeroAndEmptyLabel()
    {
        // Bridge writes a packet with no Rating for a file nobody has starred.
        string path = WritePng("unrated.png", ITxt(XmpPacket(@"xmp:Label=""""")));

        Assert.Equal((0, string.Empty), XmpManager.ReadMetadata(path));
    }

    [Fact]
    public void ReadMetadata_CompressedITxt_IsInflated()
    {
        string path = WritePng("compressed.png", ITxt(XmpPacket(@"xmp:Rating=""5"""), compress: true));

        Assert.Equal(5, XmpManager.ReadMetadata(path).Rating);
    }

    [Fact]
    public void ReadMetadata_ZTxtChunk_IsInflated()
    {
        string path = WritePng("ztxt.png", ZTxt(XmpPacket(@"xmp:Rating=""2""")));

        Assert.Equal(2, XmpManager.ReadMetadata(path).Rating);
    }

    [Fact]
    public void ReadMetadata_SkipsOtherTextChunks_AndLargeBinaryChunks()
    {
        // The XMP chunk is reached only after a foreign text chunk (wrong keyword)
        // and a bulky binary chunk that must be seeked over, never buffered.
        byte[] comment = TextChunk("tEXt", "Comment", Encoding.Latin1.GetBytes("not the xmp"));
        byte[] filler = Chunk("IDAT", new byte[64 * 1024]);
        string path = WritePng("mixed.png", comment, filler, ITxt(XmpPacket(@"xmp:Rating=""1""")));

        Assert.Equal(1, XmpManager.ReadMetadata(path).Rating);
    }

    [Fact]
    public void ReadMetadata_NoXmpChunk_ReturnsEmpty()
    {
        string path = WritePng("plain.png", TextChunk("tEXt", "Comment", Encoding.Latin1.GetBytes("hello")));

        Assert.Equal((0, string.Empty), XmpManager.ReadMetadata(path));
    }

    [Fact]
    public void ReadMetadata_MissingFile_ReturnsEmpty()
    {
        Assert.Equal((0, string.Empty), XmpManager.ReadMetadata(Path.Combine(directory, "nope.png")));
    }

    [Fact]
    public void ReadMetadata_TruncatedChunk_DoesNotThrow()
    {
        // A chunk header promising more bytes than the file holds: the walk must
        // bail out and hand over to the fallback rather than reading past the end.
        byte[] whole = BuildPng(ITxt(XmpPacket(@"xmp:Rating=""4""")));
        string path = Path.Combine(directory, "truncated.png");
        File.WriteAllBytes(path, whole[..(whole.Length - 40)]);

        Assert.Equal((0, string.Empty), XmpManager.ReadMetadata(path));
    }

    [Fact]
    public void ReadMetadata_NotAPng_DoesNotThrow()
    {
        string path = Path.Combine(directory, "notapng.png");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("this is not an image at all"));

        Assert.Equal((0, string.Empty), XmpManager.ReadMetadata(path));
    }

    [Fact]
    public void ReadMetadata_MalformedXmpXml_FallsBackWithoutThrowing()
    {
        string path = WritePng("badxml.png", ITxt("<x:xmpmeta><rdf:RDF unclosed"));

        Assert.Equal((0, string.Empty), XmpManager.ReadMetadata(path));
    }

    // --- fixture construction -------------------------------------------------

    private static string XmpPacket(string attributes, string body = "")
    {
        return $@"<?xpacket begin="""" id=""W5M0MpCehiHzreSzNTczkc9d""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
 <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
  <rdf:Description rdf:about="""" xmlns:xmp=""http://ns.adobe.com/xap/1.0/"" {attributes}>{body}</rdf:Description>
 </rdf:RDF>
</x:xmpmeta>
<?xpacket end=""w""?>";
    }

    /// <summary>An iTXt chunk under the XMP keyword: flag, method, then the two
    /// empty NUL-terminated language fields, then UTF-8 text.</summary>
    private static byte[] ITxt(string packet, bool compress = false)
    {
        byte[] text = Encoding.UTF8.GetBytes(packet);
        using MemoryStream body = new();

        body.WriteByte((byte)(compress ? 1 : 0)); // compression flag
        body.WriteByte(0);                        // compression method
        body.WriteByte(0);                        // language tag (empty)
        body.WriteByte(0);                        // translated keyword (empty)
        body.Write(compress ? Deflate(text) : text);

        return TextChunk("iTXt", "XML:com.adobe.xmp", body.ToArray());
    }

    /// <summary>A zTXt chunk: compression method byte, then zlib-compressed Latin-1.</summary>
    private static byte[] ZTxt(string packet)
    {
        using MemoryStream body = new();

        body.WriteByte(0); // compression method
        body.Write(Deflate(Encoding.Latin1.GetBytes(packet)));

        return TextChunk("zTXt", "XML:com.adobe.xmp", body.ToArray());
    }

    private static byte[] Deflate(byte[] raw)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return output.ToArray();
    }

    private static byte[] TextChunk(string type, string keyword, byte[] rest)
    {
        using MemoryStream data = new();

        data.Write(Encoding.Latin1.GetBytes(keyword));
        data.WriteByte(0);
        data.Write(rest);

        return Chunk(type, data.ToArray());
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        using MemoryStream chunk = new();

        chunk.Write(BigEndian(data.Length));
        chunk.Write(typeBytes);
        chunk.Write(data);

        byte[] crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        chunk.Write(BigEndian((int)Crc32(crcInput)));

        return chunk.ToArray();
    }

    private static byte[] BigEndian(int value)
    {
        return [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    }

    private string WritePng(string name, params byte[][] chunks)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, BuildPng(chunks));

        return path;
    }

    private static byte[] BuildPng(params byte[][] chunks)
    {
        using MemoryStream png = new();

        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR: 1x1, 8-bit greyscale. Present so the stream is structurally a PNG.
        png.Write(Chunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 0, 0, 0, 0]));

        foreach (byte[] chunk in chunks)
        {
            png.Write(chunk);
        }

        png.Write(Chunk("IEND", []));

        return png.ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }
}
