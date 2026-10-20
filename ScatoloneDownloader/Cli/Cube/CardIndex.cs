using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Lookup tables over the Scryfall bulk, shared by the commands that walk the
    /// metadata store and need the card behind each entry (<c>classify</c>,
    /// <c>audit</c>, <c>restore</c>).
    /// </summary>
    internal static class CardIndex
    {
        /// <summary>
        /// Indexes <paramref name="cards"/> by <c>oracle_id</c>. A card with no
        /// oracle id is skipped. The first printing encountered in the bulk download
        /// wins, which matches the "first matched printing wins" assumption used when
        /// entries are created.
        /// </summary>
        internal static Dictionary<string, Card> ByOracleId(IEnumerable<Card> cards)
        {
            Dictionary<string, Card> index = [];
            foreach (Card card in cards)
            {
                if (!string.IsNullOrEmpty(card.OracleId))
                {
                    index.TryAdd(card.OracleId, card);
                }
            }

            return index;
        }

        /// <summary>
        /// Indexes <paramref name="cards"/> by printing id (<c>scryfallId</c>). A card
        /// with no id is skipped. Printing ids are unique, so on a duplicate the
        /// first card in bulk order is kept.
        /// </summary>
        internal static Dictionary<string, Card> ById(IEnumerable<Card> cards)
        {
            Dictionary<string, Card> index = [];
            foreach (Card card in cards)
            {
                if (!string.IsNullOrEmpty(card.Id))
                {
                    index.TryAdd(card.Id, card);
                }
            }

            return index;
        }
    }
}
