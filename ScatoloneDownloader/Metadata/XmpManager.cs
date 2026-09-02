using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using ImageMagick;

namespace ScatoloneDownloader.Metadata
{
    /// <summary>
    /// Reads and writes Adobe XMP metadata in image files. Uses Magick.NET to
    /// safely extract the XMP chunk from the PNG.
    /// </summary>
    internal static class XmpManager
    {
        // Standard Adobe XMP XML namespaces.
        private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

        /// <summary>
        /// Reads the Rating (0-5) and text Label from a PNG file.
        /// Returns (0, string.Empty) if the file does not exist or has no XMP metadata.
        /// </summary>
        internal static (int Rating, string Label) ReadMetadata(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                return (0, string.Empty);
            }

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

                // In Adobe XMP (RDF-structured), properties usually live inside <rdf:Description>.
                XElement descriptionNode = xDocument.Descendants(Rdf + "Description").FirstOrDefault();

                if (descriptionNode == null)
                {
                    return (0, string.Empty);
                }

                return (ParseRating(descriptionNode), ParseLabel(descriptionNode));
            }
            catch (Exception)
            {
                // Corrupt file or invalid image: silent fallback.
                // A logger could be injected here in the future.
                return (0, string.Empty);
            }
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