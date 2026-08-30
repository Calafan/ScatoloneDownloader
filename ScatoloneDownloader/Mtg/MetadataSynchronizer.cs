using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Metadata;

namespace ScatoloneDownloader.Mtg
{
    internal static class MetadataSynchronizer
    {
        internal static void SyncCardsFromDisk(IEnumerable<(Card Card, string FilePath)> cardFiles)
        {
            foreach (var (card, filePath) in cardFiles)
            {
                if (File.Exists(filePath))
                {
                    var (rating, label) = XmpManager.ReadMetadata(filePath);
                    card.Rating = rating;
                    card.XmpLabel = label ?? string.Empty;
                }
                else
                {
                    card.Rating = 0;
                    card.XmpLabel = string.Empty;
                }
            }
        }
    }
}