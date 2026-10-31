using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Filtering
{
    /// <summary>
    /// The "library rules": reproduces how the hand-curated library was built. One
    /// printing per card (the earliest plain one), no funny/Vanguard/planar cards, no
    /// card that is legal in no format (dungeons and token-type cards excepted), no
    /// creature token and only the token-layout cards of <see cref="KeptTokens"/> unless
    /// tokens are requested, no Alchemy card, nothing not yet released, and for a card
    /// with no plain printing a fallback to a plain-looking printing from any set, else
    /// to the earliest variant.
    /// <see cref="Select"/> applies the rules; <see cref="WithFallback"/> keeps the
    /// plain validation (used by <c>set</c> and <c>--no-rules</c>) and only adds the
    /// variant fallback.
    /// </summary>
    internal static class LibrarySelector
    {
        /// <summary>
        /// The token-layout cards the library keeps without tokens being requested, by
        /// name (the user's list, 2026-10-20): the reminders of a rule — The Monarch, day
        /// and night, the city's blessing, the Ring, speed — and the predefined
        /// non-creature tokens, each named after its own subtype (Treasure, Clue, Food
        /// and the like). Dungeons (the Initiative's Undercity too) pass the token gate
        /// on their type and the Role tokens have a flip layout, so neither is listed.
        /// Creature tokens are not wanted; the other reminders (energy, poison,
        /// experience, foretell, plot, emblems) were offered and not chosen.
        /// </summary>
        internal static readonly HashSet<string> KeptTokens = new(StringComparer.Ordinal)
        {
            "The Monarch",
            "Day // Night",
            "City's Blessing",
            "The Ring // The Ring Tempts You",
            "Start Your Engines! // Max Speed",
            "Treasure",
            "Clue",
            "Food",
            "Blood",
            "Gold",
            "Map",
            "Powerstone",
            "Junk",
            "Incubator // Phyrexian",
            "Shard",
            "Lander",
            "Mutagen",
            "Vibranium",
            "Heartwood",
        };

        /// <summary>
        /// Library exclusions: funny and Vanguard set types, planar and Vanguard layouts,
        /// Alchemy cards printed on paper (the Mystery Booster 2 playtest slot; the user
        /// wants none, 2026-10-26) and cards legal in no format. Dungeons, token-type
        /// cards and the <see cref="KeptTokens"/> are never legal anywhere but are
        /// wanted, so they are exempt from the never-legal rule.
        /// </summary>
        internal static bool IsExcluded(Card c)
        {
            bool excludedSetType = c.SetType is "funny" or "vanguard";
            bool excludedLayout = c.Layout is "planar" or "vanguard";
            bool alchemy = c.PromoTypes.Contains("alchemy");
            bool neverLegalUnwanted = c.NeverLegal && !c.IsDungeon && !c.IsTokenType && !KeptTokens.Contains(c.Name);

            return excludedSetType || excludedLayout || alchemy || neverLegalUnwanted;
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
        /// A plain-looking printing that stands in for a card with no valid printing:
        /// English, paper, a valid layout and border, not etched,
        /// <see cref="CardFilter.HasPlainLook"/> and legal somewhere, whatever its set
        /// type and reprint flag. Scryfall can flag the set's own printing a reprint
        /// (Elesh Norn, Mother of Machines: the borderless Concept Praetor is dated a
        /// week earlier), a crossover's in-universe art can exist only in a masters set
        /// (Zilortha in Commander Masters, the Secret Lair crossovers in Universes
        /// Within), and some cards were printed only there (the Commander Masters
        /// newcomers) or first in an excluded set (Saw in Half in Unfinity); the user
        /// wants all of them (2026-10-26). Legal somewhere keeps out what the
        /// never-legal exemptions let through, like the Jumpstart 2022 front card named
        /// Treasure.
        /// </summary>
        internal static bool IsPlainStandIn(Card c, bool tokens)
        {
            return CardFilter.IsEnglish(c)
                && CardFilter.HasValidLayout(c, tokens)
                && CardFilter.IsPaperGame(c)
                && CardFilter.HasValidBorder(c)
                && !CardFilter.IsEtched(c)
                && CardFilter.HasPlainLook(c)
                && !c.NeverLegal;
        }

        /// <summary>
        /// The pick for a card with no valid printing: its best plain-looking stand-in
        /// (strict rank), else its best fallback candidate (fallback rank). With
        /// <paramref name="onlyOverAVariant"/> a stand-in only replaces a variant the
        /// fallback would take, and a card with no fallback candidate gets nothing:
        /// <see cref="WithFallback"/> sees one set's printings, where a plain reprint is
        /// the normal case and not a card to add.
        /// </summary>
        private static Card? FallbackPick(IEnumerable<Card> group, Func<Card, bool> tokensFor, bool lands, bool onlyOverAVariant)
        {
            Card? fallback = Best(group.Where(c => IsFallbackCandidate(c, tokensFor(c), lands)), CompareFallback);

            if (fallback == null && onlyOverAVariant)
            {
                return null;
            }

            return Best(group.Where(c => IsPlainStandIn(c, tokensFor(c))), CompareStrict) ?? fallback;
        }

        /// <summary>
        /// The printings <c>all</c> and <c>years</c> download under the library rules, as of
        /// today: one per card, no reprints, tokens or basic lands.
        /// </summary>
        internal static List<Card> LibraryPicks(IEnumerable<Card> bulk)
        {
            return Select(bulk, reprints: false, tokens: false, lands: false, releasedBy: DateTime.Today);
        }

        /// <summary>
        /// Library rules ON. Drops excluded cards; with <paramref name="reprints"/> returns
        /// every eligible printing; otherwise keeps every eligible basic land plus one
        /// printing per card (the best strict rank among the eligible, else the
        /// <see cref="FallbackPick"/>). The selection is global: callers
        /// filter by year after selecting, so a card first printed in 2010 and reprinted
        /// in 2024 is picked in 2010 and absent from a 2024 download.
        /// <para>
        /// Without <paramref name="tokens"/>, creature tokens are dropped (Spirit Sorcerer
        /// Token has a normal layout, so the token gate alone lets it through) and the
        /// <see cref="KeptTokens"/> pass the token gate. A printing released after
        /// <paramref name="releasedBy"/> is dropped before the pick, so a preview never
        /// displaces a printing already out; <c>null</c> keeps every printing.
        /// </para>
        /// </summary>
        internal static List<Card> Select(IEnumerable<Card> cards, bool reprints, bool tokens, bool lands, DateTime? releasedBy = null)
        {
            List<Card> kept = cards.Where(c => !IsExcluded(c)
                && (tokens || !c.IsCreatureToken)
                && (releasedBy is null || c.ReleasedAt <= releasedBy)).ToList();
            List<Card> result = [];

            bool Eligible(Card c) => CardFilter.IsDownloadable(c, reprints, tokens || KeptTokens.Contains(c.Name), lands);

            if (reprints)
            {
                result.AddRange(kept.Where(Eligible));
                return Sorted(result);
            }

            foreach (IGrouping<string, Card> group in kept.Where(c => !c.IsBasicLand).GroupBy(GroupKey))
            {
                Card? pick = Best(group.Where(Eligible), CompareStrict)
                    ?? FallbackPick(group, c => tokens || KeptTokens.Contains(c.Name), lands, onlyOverAVariant: false);

                if (pick != null)
                {
                    result.Add(pick);
                }
            }

            // Basic lands are exempt from one-per-card: every eligible printing stays.
            result.AddRange(kept.Where(c => c.IsBasicLand && Eligible(c)));

            return Sorted(result);
        }

        /// <summary>
        /// Library rules OFF (<c>set</c> and <c>--no-rules</c>): the plain validation, no
        /// exclusions and no dedupe, plus, for every non-basic card with no valid
        /// printing at all, its <see cref="FallbackPick"/> (so a card that exists only as
        /// a variant still downloads that variant).
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

                Card? pick = FallbackPick(group, _ => tokens, lands, onlyOverAVariant: true);

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
