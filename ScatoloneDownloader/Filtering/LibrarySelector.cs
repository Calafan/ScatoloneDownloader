using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Filtering
{
    /// <summary>
    /// The "library rules": reproduces how the hand-curated library was built. One
    /// printing per card (the earliest plain one), no funny/Vanguard/planar cards, no
    /// card that is legal in no format (dungeons and token-type cards excepted), and a
    /// fallback to the earliest variant for cards that exist only as a variant.
    /// <see cref="Select"/> applies the rules; <see cref="WithFallback"/> keeps the
    /// plain validation (used by <c>set</c> and <c>--no-rules</c>) and only adds the
    /// variant fallback.
    /// </summary>
    internal static class LibrarySelector
    {
        /// <summary>
        /// Library exclusions: funny and Vanguard set types, planar and Vanguard layouts,
        /// and cards legal in no format. Dungeons and token-type cards are never legal
        /// anywhere but are wanted, so they are exempt from the never-legal rule.
        /// </summary>
        internal static bool IsExcluded(Card c)
        {
            bool excludedSetType = c.SetType is "funny" or "vanguard";
            bool excludedLayout = c.Layout is "planar" or "vanguard";
            bool neverLegalUnwanted = c.NeverLegal && !c.IsDungeon && !c.IsTokenType;

            return excludedSetType || excludedLayout || neverLegalUnwanted;
        }

        /// <summary>
        /// The identity of "the same card" across printings: the oracle id, else the
        /// first face's oracle id (reversible cards carry it only on their faces), else
        /// the name.
        /// </summary>
        internal static string GroupKey(Card c)
        {
            if (!string.IsNullOrEmpty(c.OracleId))
            {
                return c.OracleId;
            }

            return !string.IsNullOrEmpty(c.FirstFaceOracleId) ? c.FirstFaceOracleId : c.Name;
        }

        /// <summary>The value of the first run of ASCII digits in a collector number
        /// ("12a" is 12, "A-12" is 12); 0 when there is none ("★", empty).</summary>
        internal static long CollectorDigits(string cn)
        {
            int start = -1;
            int end = 0;

            for (int i = 0; i < cn.Length; i++)
            {
                bool digit = cn[i] >= '0' && cn[i] <= '9';

                if (digit && start < 0)
                {
                    start = i;
                }

                if (start >= 0)
                {
                    if (!digit)
                    {
                        end = i;
                        break;
                    }

                    end = i + 1;
                }
            }

            return start < 0 ? 0 : long.Parse(cn.AsSpan(start, end - start));
        }

        /// <summary>
        /// Strict rank, ascending (the first wins): non-promo set, then plain over promo
        /// treatment, then earliest release, then collector number (digits, then text),
        /// then id. Booleans order false before true.
        /// </summary>
        private static int CompareStrict(Card a, Card b)
        {
            int result = (a.SetType == "promo").CompareTo(b.SetType == "promo");

            if (result == 0)
            {
                result = CardFilter.IsPromoTreatment(a).CompareTo(CardFilter.IsPromoTreatment(b));
            }

            if (result == 0)
            {
                result = a.ReleasedAt.CompareTo(b.ReleasedAt);
            }

            return result != 0 ? result : CompareTail(a, b);
        }

        /// <summary>
        /// Fallback rank, ascending: earliest release first, then non-promo set, plain
        /// over promo treatment, collector number and id. Used for variant-only cards,
        /// where "the earliest printing" matters more than how it was marketed.
        /// </summary>
        private static int CompareFallback(Card a, Card b)
        {
            int result = a.ReleasedAt.CompareTo(b.ReleasedAt);

            if (result == 0)
            {
                result = (a.SetType == "promo").CompareTo(b.SetType == "promo");
            }

            if (result == 0)
            {
                result = CardFilter.IsPromoTreatment(a).CompareTo(CardFilter.IsPromoTreatment(b));
            }

            return result != 0 ? result : CompareTail(a, b);
        }

        /// <summary>The shared tie-break of both ranks: collector digits, collector number, id.</summary>
        private static int CompareTail(Card a, Card b)
        {
            int result = CollectorDigits(a.CollectorNumber).CompareTo(CollectorDigits(b.CollectorNumber));

            if (result == 0)
            {
                result = StringComparer.Ordinal.Compare(a.CollectorNumber, b.CollectorNumber);
            }

            return result != 0 ? result : StringComparer.Ordinal.Compare(a.Id, b.Id);
        }

        /// <summary>
        /// A printing that can stand in for a card with no valid printing: it passes every
        /// gate except the variant one (reprint-like treatments), but a true reprint
        /// never does. Basic lands never reach it: both callers group only the
        /// non-basic cards (a <c>!IsBasicLand</c> here was tried and is dead code).
        /// </summary>
        internal static bool IsFallbackCandidate(Card c, bool tokens, bool lands)
        {
            return CardFilter.IsDownloadable(c, downloadReprints: true, tokens, lands)
                && !c.Reprint;
        }

        /// <summary>
        /// Library rules ON. Drops excluded cards; with <paramref name="reprints"/> returns
        /// every eligible printing; otherwise keeps every eligible basic land plus one
        /// printing per card (the best strict rank among the eligible, else the best
        /// fallback rank among the fallback candidates). The selection is global: callers
        /// filter by year after selecting, so a card first printed in 2010 and reprinted
        /// in 2024 is picked in 2010 and absent from a 2024 download.
        /// </summary>
        internal static List<Card> Select(IEnumerable<Card> cards, bool reprints, bool tokens, bool lands)
        {
            List<Card> kept = cards.Where(c => !IsExcluded(c)).ToList();
            List<Card> result = [];

            if (reprints)
            {
                result.AddRange(kept.Where(c => CardFilter.IsDownloadable(c, reprints, tokens, lands)));
                return Sorted(result);
            }

            foreach (IGrouping<string, Card> group in kept.Where(c => !c.IsBasicLand).GroupBy(GroupKey))
            {
                Card? pick = Best(group.Where(c => CardFilter.IsDownloadable(c, reprints, tokens, lands)), CompareStrict)
                    ?? Best(group.Where(c => IsFallbackCandidate(c, tokens, lands)), CompareFallback);

                if (pick != null)
                {
                    result.Add(pick);
                }
            }

            // Basic lands are exempt from one-per-card: every eligible printing stays.
            result.AddRange(kept.Where(c => c.IsBasicLand && CardFilter.IsDownloadable(c, reprints, tokens, lands)));

            return Sorted(result);
        }

        /// <summary>
        /// Library rules OFF (<c>set</c> and <c>--no-rules</c>): the plain validation, no
        /// exclusions and no dedupe, plus, for every non-basic card with no valid
        /// printing at all, its fallback pick (so a card that exists only as a variant
        /// still downloads that variant).
        /// </summary>
        internal static List<Card> WithFallback(IEnumerable<Card> cards, bool reprints, bool tokens, bool lands)
        {
            List<Card> all = cards.ToList();
            List<Card> result = CardFilter.Validate(all, reprints, tokens, lands);

            HashSet<string> coveredGroups = result.Where(c => !c.IsBasicLand).Select(GroupKey).ToHashSet();

            foreach (IGrouping<string, Card> group in all.Where(c => !c.IsBasicLand).GroupBy(GroupKey))
            {
                if (coveredGroups.Contains(group.Key))
                {
                    continue;
                }

                Card? pick = Best(group.Where(c => IsFallbackCandidate(c, tokens, lands)), CompareFallback);

                if (pick != null)
                {
                    result.Add(pick);
                }
            }

            return Sorted(result);
        }

        private static Card? Best(IEnumerable<Card> candidates, Comparison<Card> compare)
        {
            Card? best = null;

            foreach (Card candidate in candidates)
            {
                if (best == null || compare(candidate, best) < 0)
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Deterministic output order: release date, set name, card name, id.</summary>
        private static List<Card> Sorted(List<Card> cards)
        {
            return cards
                .OrderBy(c => c.ReleasedAt)
                .ThenBy(c => c.SetName, StringComparer.Ordinal)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .ToList();
        }
    }
}
