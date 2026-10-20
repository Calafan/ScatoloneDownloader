using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// The pure steps of the <c>sync</c> command, separated from its network and
    /// disk work so they are testable without either: comparing the cards the
    /// library rules select with the files on disk, registering and proposing the
    /// new cards, and finding the store entries with no image. Nothing here ever
    /// deletes or moves a file or removes a store entry.
    /// </summary>
    internal static class LibrarySync
    {
        /// <summary>
        /// Compares the selected cards (each with the path its image belongs at)
        /// with the PNG files that exist, as full paths, case-insensitively. A
        /// path wanted by several cards counts once.
        /// </summary>
        internal static SyncPlan Plan(IEnumerable<(Card Card, string ExpectedPath)> expected, IEnumerable<string> existing)
        {
            HashSet<string> existingPaths = new(existing, StringComparer.OrdinalIgnoreCase);
            HashSet<string> expectedPaths = new(StringComparer.OrdinalIgnoreCase);
            List<(Card Card, string ExpectedPath)> missing = [];
            int present = 0;

            foreach ((Card card, string path) in expected)
            {
                if (!expectedPaths.Add(path))
                {
                    continue;
                }

                if (existingPaths.Contains(path))
                {
                    present++;
                }
                else
                {
                    missing.Add((card, path));
                }
            }

            List<string> extra = existingPaths.Where(path => !expectedPaths.Contains(path)).ToList();
            extra.Sort(StringComparer.Ordinal);

            return new SyncPlan(missing, present, extra);
        }

        /// <summary>
        /// Registers the matched cards the store does not hold yet (see
        /// <see cref="RegisterCommand.RegisterNew"/>) and, for each ADDED entry
        /// only, proposes its effects the way <c>classify</c> does. Never stamps
        /// <c>reviewedAt</c> and never touches an entry that existed before.
        /// </summary>
        internal static SyncRegistration RegisterAndPropose(
            CubeMetadata metadata,
            IEnumerable<(Card Card, string FilePath)> matched,
            IReadOnlyDictionary<string, Card> cardsByOracleId)
        {
            RegisterResult registered = RegisterCommand.RegisterNew(metadata, matched);
            int proposed = 0;
            int noProposal = 0;

            foreach (string oracleId in registered.AddedOracleIds)
            {
                CardMetadataEntry entry = metadata.Cards[oracleId];

                if (cardsByOracleId.TryGetValue(oracleId, out Card? card))
                {
                    (ClassifyOutcome outcome, CardEffect effects) = ClassifyCommand.Decide(entry, card, overwrite: false);
                    if (outcome == ClassifyOutcome.Classified)
                    {
                        entry.EffectFlags = effects;
                        proposed++;
                        continue;
                    }
                }

                noProposal++;
            }

            return new SyncRegistration(registered.AddedNames, registered.AddedOracleIds, proposed, noProposal);
        }

        /// <summary>
        /// The distinct, non-blank oracle ids the store does not hold: what a
        /// register would add. Does not touch the store.
        /// </summary>
        internal static IReadOnlyList<string> NewOracleIds(CubeMetadata metadata, IEnumerable<string> oracleIds)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            List<string> fresh = [];

            foreach (string oracleId in oracleIds)
            {
                if (!string.IsNullOrWhiteSpace(oracleId) && !metadata.Cards.ContainsKey(oracleId) && seen.Add(oracleId))
                {
                    fresh.Add(oracleId);
                }
            }

            return fresh;
        }

        /// <summary>
        /// The store entries whose oracle id has no image, sorted by name. They are
        /// only reported: removing an entry is always the user's decision.
        /// </summary>
        internal static IReadOnlyList<SyncOrphan> Orphans(CubeMetadata metadata, IReadOnlySet<string> presentOracleIds)
        {
            return metadata.Cards
                .Where(pair => !presentOracleIds.Contains(pair.Key))
                .Select(pair => new SyncOrphan(pair.Value.Name, pair.Value.Rating, pair.Value.ReviewedAt != null))
                .OrderBy(orphan => orphan.Name, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>What <see cref="LibrarySync.Plan"/> found.</summary>
    /// <param name="Missing">One card per expected path that is not on disk.</param>
    /// <param name="PresentCount">Expected paths that are on disk.</param>
    /// <param name="Extra">Existing files no selected card expects, sorted ordinally.</param>
    internal sealed record SyncPlan(
        IReadOnlyList<(Card Card, string ExpectedPath)> Missing, int PresentCount, IReadOnlyList<string> Extra);

    /// <summary>What <see cref="LibrarySync.RegisterAndPropose"/> did.</summary>
    /// <param name="AddedNames">Names of the entries added, in match order.</param>
    /// <param name="AddedOracleIds">Their oracle ids, in the same order.</param>
    /// <param name="Proposed">Added entries that received a proposal.</param>
    /// <param name="NoProposal">Added entries the classifier had nothing to propose for.</param>
    internal sealed record SyncRegistration(
        IReadOnlyList<string> AddedNames, IReadOnlyList<string> AddedOracleIds, int Proposed, int NoProposal);

    /// <summary>A store entry with no image, as listed by <c>sync</c>.</summary>
    internal sealed record SyncOrphan(string Name, int Rating, bool Reviewed);
}
