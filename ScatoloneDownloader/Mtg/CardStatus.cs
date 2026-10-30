namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Cube pool status of a card. Mutually exclusive, unlike <see cref="CardEffect"/>
    /// (a card cannot be both Banned and Token). <see cref="None"/> = a normal pool
    /// card (default). Authored via the web tagger, hand-editable in the metadata
    /// directory's rating-tier files. The cube analysis
    /// (the stats page, via <see cref="CardAnalyzer.ForPool"/>) excludes them —
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
