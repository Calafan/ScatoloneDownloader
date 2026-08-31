namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Cube pool status of a card. Mutually exclusive, unlike <see cref="CardEffect"/>
    /// (a card cannot be both Banned and Token). <see cref="None"/> = a normal pool
    /// card (default). Authored via the web tagger, hand-editable in the metadata
    /// directory's rating-tier files. The generated views route Banned/Token/Jolly
    /// to a dedicated flat folder (out of the browse tree); the cube analysis
    /// (<c>build-views</c>, via <see cref="CardAnalyzer.ForPool"/>) excludes them —
    /// and non-pool cards — from its distributions.
    /// </summary>
    public enum CardStatus
    {
        None,
        Banned,
        Token,
        Jolly,
    }
}
