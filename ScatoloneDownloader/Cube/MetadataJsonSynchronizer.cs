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
        /// values from a previous sync call. Takes the metadata already loaded
        /// (e.g. the tagger, which keeps the merged document in memory for
        /// autosave) so no tier file is deserialized a second time.</summary>
        internal static void SyncFromJson(IEnumerable<Card> cards, CubeMetadata data)
        {
            foreach (Card card in cards)
            {
                Apply(card, data, _ => CardEffect.None);
            }
        }

        /// <summary>Like <see cref="SyncFromJson"/>
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
                if (Apply(card, data, EffectClassifier.Classify))
                {
                    inStore++;
                }
            }

            return inStore;
        }

        /// <summary>Applies the card's store entry, or the unrated/no-status/not-reviewed
        /// defaults with <paramref name="fallbackEffects"/> when there is none. Returns
        /// whether the card had an entry.</summary>
        private static bool Apply(Card card, CubeMetadata data, Func<Card, CardEffect> fallbackEffects)
        {
            if (!string.IsNullOrEmpty(card.OracleId)
                && data.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? entry))
            {
                card.Rating = entry.Rating;
                card.Status = entry.StatusValue;
                card.Effects = entry.EffectFlags;
                card.Reviewed = entry.ReviewedAt != null;
                return true;
            }

            card.Rating = 0;
            card.Status = CardStatus.None;
            card.Effects = fallbackEffects(card);
            card.Reviewed = false;
            return false;
        }
    }
}
