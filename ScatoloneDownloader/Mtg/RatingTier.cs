namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// The rating bands the cube is partitioned into. This is the SINGLE source of
    /// truth for the rating boundaries, shared by metadata storage
    /// (<see cref="Metadata.CubeMetadataStore.TierFileName"/> routes each band to
    /// its tier file) and view generation (<see cref="Cube.ViewGenerator"/> shows
    /// <see cref="Pool"/> in the browse tree, keeps <see cref="Unrated"/> in the
    /// year/set backlog view, gives <see cref="Bench"/> its own recovery root, and
    /// excludes <see cref="Fringe"/> entirely per D7). Change the boundaries in
    /// <see cref="RatingTierClassifier.Classify"/> once and both storage and views
    /// follow.
    /// </summary>
    internal enum RatingTier
    {
        /// <summary>Rating 0 — the un-evaluated bulk library backlog.</summary>
        Unrated,

        /// <summary>Rating 1 — evaluated and rejected outright; kept for
        /// round-tripping, never surfaced in the generated views (D7).</summary>
        Fringe,

        /// <summary>Rating 2 — cut, but only just. Browsable in its own recovery
        /// root so a card can be promoted back when the pool turns out to be short
        /// somewhere (a top-heavy curve needing more two-drops, a color missing
        /// removal). Shares <c>fringe.json</c> with <see cref="Fringe"/>: this
        /// split is a VIEW distinction, deliberately not a storage one, so
        /// introducing it moved no entry between tier files.</summary>
        Bench,

        /// <summary>Rating 3-5 — the active, curated cube pool.</summary>
        Pool,
    }

    /// <summary>Maps a numeric rating to its <see cref="RatingTier"/>.</summary>
    internal static class RatingTierClassifier
    {
        /// <summary>Pool = 3-5, Bench = 2, Fringe = 1, Unrated = 0 (or any
        /// unexpected out-of-range value, defensively).</summary>
        internal static RatingTier Classify(int rating)
        {
            if (rating >= 3)
            {
                return RatingTier.Pool;
            }

            if (rating == 2)
            {
                return RatingTier.Bench;
            }

            if (rating == 1)
            {
                return RatingTier.Fringe;
            }

            return RatingTier.Unrated;
        }
    }
}
