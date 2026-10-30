using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Imaging;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Download
{
    /// <summary>What <see cref="CardDownloader.DownloadAsync"/> did with a card.</summary>
    internal enum DownloadOutcome
    {
        Downloaded,
        Skipped,
    }

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

        /// <summary>
        /// Downloads one card. With <paramref name="skipExisting"/> (one printing per card) an
        /// image already on disk means the card is done and nothing is fetched.
        /// </summary>
        internal async Task<DownloadOutcome> DownloadAsync(Card card, Mode mode, string? fileName, bool skipExisting)
        {
            string baseDirectory = OutputPaths.BuildCardDirectory(card, mode, fileName);

            string? path = ResolveImagePath(baseDirectory, card, skipExisting);

            if (path == null)
            {
                return DownloadOutcome.Skipped;
            }

            byte[] png = await ComposeAsync(card);

            // Write beside the target and move into place, so a crash never leaves a
            // truncated PNG that a later skip-existing run would mistake for a finished one.
            await AtomicFile.WriteAllBytesAsync(path + ".png", png);

            return DownloadOutcome.Downloaded;
        }

        /// <summary>
        /// Decides where a card's image goes, as a path WITHOUT extension, or null to skip it.
        /// With <paramref name="skipExisting"/> a non-basic card has exactly one slot (the
        /// un-numbered name): present means skip. Otherwise collisions are numbered
        /// ("Name1", "Name2", ...) and the canonical artwork takes the un-numbered name.
        /// </summary>
        internal static string? ResolveImagePath(string baseDirectory, Card card, bool skipExisting)
        {
            string validName = OutputPaths.Sanitize(card.Name);
            string path = Path.Combine(baseDirectory, validName);

            if (skipExisting && !card.IsBasicLand)
            {
                return File.Exists(path + ".png") ? null : path;
            }

            int i = 1;

            while (File.Exists(path + ".png"))
            {
                path = Path.Combine(baseDirectory, validName + i++.ToString());
            }

            // Cards arrive in random order, but the original artwork must always keep the un-numbered name.
            // This is one of the two places the canonical-artwork rule lives (see GetManager.BuildCardsByName).
            // Treated promos (surgefoil, textured, ...) are excluded from the canonical slot so a plain
            // printing always frees the un-numbered name.
            if (i != 1 && !card.IsBasicLand && CardFilter.IsCanonicalArtwork(card))
            {
                string canonicalPath = Path.Combine(baseDirectory, validName) + ".png";

                // Free the un-numbered name for the canonical art. The earlier non-canonical
                // file should be there, but guard the move so a missing file can't crash the run.
                if (File.Exists(canonicalPath))
                {
                    File.Move(canonicalPath, path + ".png");
                }

                path = Path.Combine(baseDirectory, validName);
            }

            return path;
        }

        internal static void WriteToList(Card card)
        {
            string baseDirectory = OutputPaths.BuildCardDirectory(card, Mode.Files, string.Empty);

            File.AppendAllText(Path.Combine(baseDirectory, ListFileName), card.Name + "\n");
        }

        /// <summary>Fetches face image(s) and composes the final printable PNG bytes,
        /// without touching disk. Exposed (not just used by <see cref="DownloadAsync"/>)
        /// so <c>restore</c> can reuse the exact same composition pipeline when
        /// rebuilding an image folder from the metadata directory + Scryfall bulk.</summary>
        internal async Task<byte[]> ComposeAsync(Card card)
        {
            switch (card)
            {
                case DoubleFaceCard doubleFace:
                    {
                        using Stream front = await getManager.GetImageStreamAsync(RequireImageUri(doubleFace.FrontImageUri, card, "front"));
                        using Stream rear = await getManager.GetImageStreamAsync(RequireImageUri(doubleFace.RearImageUri, card, "rear"));
                        return CardImageComposer.ComposeDoubleFace(front, rear, doubleFace.IsSiege);
                    }
                case SingleFaceCard singleFace:
                    {
                        using Stream image = await getManager.GetImageStreamAsync(RequireImageUri(singleFace.ImageUri, card, "card"));

                        return CardImageComposer.ComposeSingleFace(image);
                    }
                default:
                    throw new InvalidOperationException("Unknown card type: " + card.GetType().Name);
            }
        }

        /// <summary>Scryfall can list a printing with no PNG for a face. Fail with
        /// the card's name instead of letting an empty URL reach the HTTP client,
        /// where the error says nothing about which card was being downloaded.</summary>
        private static string RequireImageUri(string? uri, Card card, string face)
        {
            return string.IsNullOrEmpty(uri)
                ? throw new InvalidOperationException($"Card '{card.Name}' has no {face} image URL.")
                : uri;
        }
    }
}
