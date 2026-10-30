namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Cube-design coarse type, mapping Scryfall's fine-grained type line to four
    /// buckets used by the analyzer. Priority order: Creature > Land >
    /// OtherPermanent > Spell. The resolver checks the buckets in that fixed
    /// order and the first one the type line matches wins (so "Land Creature" = Creature, "Artifact
    /// Creature" = Creature, "Tribal Instant" = Spell).
    /// </summary>
    public enum MacroType
    {
        Creature,
        Land,
        OtherPermanent,
        Spell,
    }
}