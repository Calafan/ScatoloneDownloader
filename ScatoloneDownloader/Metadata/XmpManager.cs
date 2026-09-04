using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using ImageMagick;

namespace ScatoloneDownloader.Metadata
{
    /// <summary>
    /// Reads Adobe XMP metadata (Rating and Label) from image files.
    /// <para>
    /// PNGs — every file the <c>import</c> command touches — take a fast path that
    /// walks the PNG chunk table with seeks and lifts the XMP text chunk out
    /// directly. Decoding the image to reach two short strings is what made the
    /// import's XMP pass the longest phase in the whole tool: measured over the
    /// 30151-file library, <c>new MagickImage(path)</c> sustains ~30 files/s
    /// because it expands every one of ~800&#160;KB of pixels, while the chunk walk
    /// reads a few hundred bytes per file and sustains ~21000 files/s — the same
    /// pass dropping from about 17 minutes to under two seconds.
    /// </para>
    /// <para>
    /// Anything that is not a PNG, carries no XMP chunk, or fails to parse falls
    /// back to Magick.NET, so odd inputs behave exactly as they always did.
    /// </para>
    /// </summary>
    internal static class XmpManager
    {
        // Standard Adobe XMP XML namespaces.
        private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        /// <summary>The PNG text-chunk keyword Adobe writes the XMP packet under.</summary>
        private const string XmpKeyword = "XML:com.adobe.xmp";

        /// <summary>Refuse to buffer a text chunk larger than this. A real XMP packet
        /// is a few kilobytes; anything past this is a corrupt length field, and
        /// allocating on it would be a denial of service against ourselves.</summary>
        private const int MaxTextChunkBytes = 8 * 1024 * 1024;

        /// <summary>
        /// Reads the Rating (0-5) and text Label from an image file.
        /// Returns (0, string.Empty) if the file does not exist or has no XMP metadata.
        /// </summary>
        internal static (int Rating, string Label) ReadMetadata(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                return (0, string.Empty);
            }

            if (TryReadPngXmp(imagePath, out string packet) && TryParsePacket(packet, out (int, string) fast))
            {
                return fast;
            }

            return ReadMetadataWithMagick(imagePath);
        }

        /// <summary>The original whole-image path: decodes through Magick.NET and
        /// pulls the XMP profile off the decoded image. Still the authority for
        /// non-PNG input and for a PNG whose chunk table we could not follow.</summary>
        private static (int Rating, string Label) ReadMetadataWithMagick(string imagePath)
        {
            try
            {
                // Load the image only to extract the XMP profile.
                using MagickImage image = new(imagePath);
                IXmpProfile profile = image.GetXmpProfile();

                if (profile == null)
                {
                    return (0, string.Empty);
                }

                // Magick.NET offers a convenient method to convert the raw profile to an XDocument.
                XDocument xDocument = profile.ToXDocument();

                return ParseDocument(xDocument);
            }
            catch (Exception)
            {
                // Corrupt file or invalid image: silent fallback.
                // A logger could be injected here in the future.
                return (0, string.Empty);
            }
        }

        /// <summary>
        /// Walks a PNG's chunk table and returns the XMP packet text, without
        /// decoding any pixel data: every chunk that is not a text chunk is skipped
        /// with a seek over its payload, so the cost per file is a handful of small
        /// reads regardless of how large the image is.
        /// </summary>
        private static bool TryReadPngXmp(string imagePath, out string packet)
        {
            packet = null;

            try
            {
                // FileShare.ReadWrite so a file currently open in Bridge still reads.
                using FileStream stream = new(
                    imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, FileOptions.SequentialScan);

                Span<byte> header = stackalloc byte[8];

                if (!TryFill(stream, header) || !header.SequenceEqual(PngSignature))
                {
                    return false; // not a PNG: let Magick.NET decide what it is
                }

                while (TryFill(stream, header))
                {
                    uint length = BinaryPrimitives.ReadUInt32BigEndian(header);
                    string type = Encoding.ASCII.GetString(header[4..]);

                    if (type == "IEND")
                    {
                        return false;
                    }

                    bool isText = type is "iTXt" or "tEXt" or "zTXt";

                    if (!isText || length > MaxTextChunkBytes)
                    {
                        // Payload plus its 4-byte CRC, never read.
                        stream.Seek(length + 4L, SeekOrigin.Current);
                        continue;
                    }

                    byte[] payload = new byte[length];
                    if (!TryFill(stream, payload))
                    {
                        return false; // truncated file
                    }

                    stream.Seek(4, SeekOrigin.Current); // CRC

                    if (TryExtractXmpText(type, payload, out packet))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false; // unreadable / locked / malformed: fall back
            }

            return false;
        }

        /// <summary>
        /// Pulls the XMP text out of one PNG text chunk, honouring the three chunk
        /// layouts the spec allows. Returns false for any text chunk that is not the
        /// XMP one, which is the common case (Bridge also writes tEXt comments).
        /// </summary>
        private static bool TryExtractXmpText(string chunkType, byte[] payload, out string text)
        {
            text = null;

            int nul = Array.IndexOf(payload, (byte)0);
            if (nul <= 0)
            {
                return false;
            }

            // The keyword is Latin-1 in every text chunk type.
            if (Encoding.Latin1.GetString(payload, 0, nul) != XmpKeyword)
            {
                return false;
            }

            int offset = nul + 1;
            bool compressed;

            switch (chunkType)
            {
                case "iTXt":
                    // compression flag, compression method, language tag, translated keyword
                    if (offset + 1 >= payload.Length)
                    {
                        return false;
                    }

                    compressed = payload[offset] != 0;
                    offset += 2;

                    offset = SkipNulTerminated(payload, offset);
                    offset = SkipNulTerminated(payload, offset);
                    break;

                case "zTXt":
                    // compression method only; always compressed
                    compressed = true;
                    offset += 1;
                    break;

                default: // tEXt
                    compressed = false;
                    break;
            }

            if (offset < 0 || offset > payload.Length)
            {
                return false;
            }

            // iTXt carries UTF-8; tEXt/zTXt are Latin-1 by spec.
            Encoding encoding = chunkType == "iTXt" ? Encoding.UTF8 : Encoding.Latin1;

            if (!compressed)
            {
                text = encoding.GetString(payload, offset, payload.Length - offset);
                return text.Length > 0;
            }

            try
            {
                using MemoryStream source = new(payload, offset, payload.Length - offset);
                using ZLibStream inflate = new(source, CompressionMode.Decompress);
                using MemoryStream expanded = new();

                inflate.CopyTo(expanded, 16 * 1024);
                text = encoding.GetString(expanded.ToArray());

                return text.Length > 0;
            }
            catch (Exception)
            {
                return false; // bad deflate stream: fall back to Magick.NET
            }
        }

        /// <summary>Index just past the next NUL at or after <paramref name="offset"/>,
        /// or -1 when the field is unterminated.</summary>
        private static int SkipNulTerminated(byte[] payload, int offset)
        {
            if (offset < 0)
            {
                return -1;
            }

            int nul = Array.IndexOf(payload, (byte)0, offset);

            return nul < 0 ? -1 : nul + 1;
        }

        /// <summary>Reads exactly <paramref name="buffer"/>.Length bytes, or reports
        /// false at end of file. <see cref="Stream.Read(Span{byte})"/> is free to
        /// return fewer bytes than asked, so a plain length check would be wrong.</summary>
        private static bool TryFill(Stream stream, Span<byte> buffer)
        {
            int read = 0;

            while (read < buffer.Length)
            {
                int got = stream.Read(buffer[read..]);
                if (got == 0)
                {
                    return false;
                }

                read += got;
            }

            return true;
        }

        /// <summary>Parses a raw XMP packet the same way the Magick.NET path parses
        /// the profile it hands back, so both routes agree field for field.</summary>
        private static bool TryParsePacket(string packet, out (int Rating, string Label) result)
        {
            result = (0, string.Empty);

            try
            {
                // An XMP packet is wrapped in <?xpacket ...?> processing instructions
                // and padded with whitespace; XDocument reads that unchanged. Trailing
                // NUL padding after the closing PI would not, so cut at the last '>'.
                int end = packet.LastIndexOf('>');
                if (end < 0)
                {
                    return false;
                }

                result = ParseDocument(XDocument.Parse(packet[..(end + 1)]));

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static (int Rating, string Label) ParseDocument(XDocument xDocument)
        {
            // In Adobe XMP (RDF-structured), properties usually live inside <rdf:Description>.
            XElement descriptionNode = xDocument.Descendants(Rdf + "Description").FirstOrDefault();

            if (descriptionNode == null)
            {
                return (0, string.Empty);
            }

            return (ParseRating(descriptionNode), ParseLabel(descriptionNode));
        }

        private static int ParseRating(XElement descriptionNode)
        {
            // Adobe Bridge sometimes stores the info as attributes (<rdf:Description xmp:Rating="5" />)
            // and sometimes as child nodes (<xmp:Rating>5</xmp:Rating>). Check both.
            int rating = 0;

            XAttribute attr = descriptionNode.Attribute(Xmp + "Rating");
            if (attr != null)
            {
                _ = int.TryParse(attr.Value, out rating);
                return rating;
            }

            XElement element = descriptionNode.Element(Xmp + "Rating");
            if (element != null)
            {
                _ = int.TryParse(element.Value, out rating);
            }

            return rating;
        }

        private static string ParseLabel(XElement descriptionNode)
        {
            XAttribute attr = descriptionNode.Attribute(Xmp + "Label");
            if (attr != null)
            {
                return attr.Value;
            }

            XElement element = descriptionNode.Element(Xmp + "Label");
            if (element != null)
            {
                return element.Value;
            }

            return string.Empty;
        }
    }
}
