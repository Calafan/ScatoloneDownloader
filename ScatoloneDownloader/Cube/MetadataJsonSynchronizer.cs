using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// Loads rating, status, and effect tags from the metadata directory's
    /// rating-tier files (pool/fringe/unrated — see <see cref="CubeMetadataStore"/>)
    /// and applies them to cards by <see cref="Card.OracleId"/>. This is the sole
    /// source of evaluation data for the tagger and its stats; nothing is read
    /// from the image files. Cards absent from every tier file
    /// are reset to the unrated / no-status / no-effects defaults.
    /// </summary>
    internal static class MetadataJsonSynchronizer
    {
        /// <summary>Mutates each card in place with its stored evaluation. Matching
        /// is by <see cref="Card.OracleId"/> (not <see cref="Card.Id"/>) so the
        /// evaluation follows the card across reprints; a card with no
        /// <see cref="Card.OracleId"/> or no matching entry is reset to the
        /// unrated/no-status/no-effects defaults rather than left with stale
        /// values from a previous sync call.</summary>
        internal static void SyncFromJson(IEnumerable<Card> cards, string metadataDirectory)
        {
            SyncFromJson(cards, CubeMetadataStore.Load(metadataDirectory));
        }

        /// <summary>Overload for callers that have already loaded the metadata
        /// (e.g. the tagger, which keeps the merged document in memory for
        /// autosave) — avoids deserializing every tier file a second time.</summary>
        internal static void SyncFromJson(IEnumerable<Card> cards, CubeMetadata data)
        {
            foreach (Card card in cards)
            {
                if (!string.IsNullOrEmpty(card.OracleId)
                    && data.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? entry))
                {
                    card.Rating = entry.Rating;
                    card.Status = entry.StatusValue;
                    card.Effects = entry.EffectFlags;
                    card.Reviewed = entry.ReviewedAt != null;
                }
                else
                {
                    card.Rating = 0;
                    card.Status = CardStatus.None;
                    card.Effects = CardEffect.None;
                    card.Reviewed = false;
                }
            }
        }

        /// <summary>Like <see cref="SyncFromJson(IEnumerable{Card}, CubeMetadata)"/>
        /// for a card list that is not the cube (the <c>analyze</c> command): a card
        /// with a store entry takes the entry's values exactly as there, even when its
        /// effects are None (a reviewed "nothing"); a card with no entry carries the
        /// classifier's proposed effects, rating 0, no status, not reviewed. Returns
        /// how many cards had an entry.</summary>
        internal static int SyncFromJsonOrClassify(IEnumerable<Card> cards, CubeMetadata data)
        {
            int inStore = 0;
            foreach (Card card in cards)
            {
                if (!string.IsNullOrEmpty(card.OracleId)
                    && data.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? entry))
                {
                    card.Rating = entry.Rating;
                    card.Status = entry.StatusValue;
                    card.Effects = entry.EffectFlags;
                    card.Reviewed = entry.ReviewedAt != null;
                    inStore++;
                }
                else
                {
                    card.Rating = 0;
                    card.Status = CardStatus.None;
                    card.Effects = EffectClassifier.Classify(card);
                    card.Reviewed = false;
                }
            }

            return inStore;
        }
    }
}
