using System.Collections.Generic;

using ScatoloneDownloader.Metadata;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Loads rating, status, and effect tags from <c>cube-metadata.json</c> and
    /// applies them to cards by <see cref="Card.OracleId"/>. This is the sole
    /// source of evaluation data for the tagger and view generation: XMP
    /// (<see cref="MetadataSynchronizer"/>) is legacy input only, read once by the
    /// <c>import</c> command to seed this file. Cards absent from the file are
    /// reset to the unrated / no-status / no-effects defaults.
    /// </summary>
    internal static class MetadataJsonSynchronizer
    {
        /// <summary>Mutates each card in place with its stored evaluation. Matching
        /// is by <see cref="Card.OracleId"/> (not <see cref="Card.Id"/>) so the
        /// evaluation follows the card across reprints; a card with no
        /// <see cref="Card.OracleId"/> or no matching entry is reset to the
        /// unrated/no-status/no-effects defaults rather than left with stale
        /// values from a previous sync call.</summary>
        internal static void SyncFromJson(IEnumerable<Card> cards, string metadataPath)
        {
            CubeMetadata data = CubeMetadataStore.Load(metadataPath);

            foreach (Card card in cards)
            {
                if (!string.IsNullOrEmpty(card.OracleId)
                    && data.Cards.TryGetValue(card.OracleId, out CardMetadataEntry entry))
                {
                    card.Rating = entry.Rating;
                    card.Status = entry.StatusValue;
                    card.Effects = entry.EffectFlags;
                }
                else
                {
                    card.Rating = 0;
                    card.Status = CardStatus.None;
                    card.Effects = CardEffect.None;
                }
            }
        }
    }
}
