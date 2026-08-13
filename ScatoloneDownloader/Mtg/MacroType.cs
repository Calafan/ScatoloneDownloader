namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Cube-design coarse type, mapping Scryfall's fine-grained type line to four
    /// buckets used by the analyzer. Priority order: Creature > Land >
    /// OtherPermanent > Spell. A card's type line is scanned left-to-right and the
    /// first matching bucket wins (so "Land Creature" = Creature, "Artifact
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