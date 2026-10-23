namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Informal effect ontology applied to cube cards. A card can carry several
    /// effects at once (e.g. an ETB creature that also removes a permanent), so
    /// this is a <see cref="FlagsAttribute"/> bitset combined with <c>|</c>.
    ///
    /// Structural buckets Creature and Land are intentionally NOT effects — they
    /// are covered by <see cref="MacroType"/> and the view tree. This enum holds
    /// only functional effects. Members are authored via the tagging tool and
    /// persisted by name (not packed int) for readable git diffs.
    ///
    /// A card earns a tag only for what it can do ON ITS OWN, ruled 2026-10-13
    /// ("dipendono da condizioni esterne, la carta di suo non è in grado di
    /// generarle -> niente tag"): mana granted to tokens the card never makes
    /// (Jaheira, Friend of the Forest), a land on somebody else's explore
    /// (Nicanzil), a drain on another Faerie entering (Obyra), a removal that
    /// needs a second card (Season of the Bold). The mirror is a card that
    /// ENABLES itself, which counts: Blossoming Tortoise mills its own
    /// graveyard before returning a land from it, and is Ramp.
    ///
    /// A card nobody CASTS carries no tags at all: the printed tokens the
    /// library keeps (Treasure, Blood, Map, the Roles), the dungeons, the
    /// emblem of The Ring and the designations (The Monarch, City's Blessing).
    /// What they do is the effect of the card that makes them. Measured
    /// 2026-10-23: 29 reviewed, all 29 untagged. Ante cards are the same
    /// (docs/ontology/CardAdvantage.md).
    ///
    /// Deliberately absent, decided 2026-09-05 — every tag is a question asked of
    /// 30k cards, so one that never changes a design decision costs and returns
    /// nothing:
    ///   - FOGS. A Fog prevents a combat, it does not answer a threat. It buys a
    ///     turn rather than saving a permanent, which is why it is not folded into
    ///     <see cref="Protection"/> either, despite also being instant-speed
    ///     prevention.
    ///   - LIFEGAIN. A resource, not an interaction. Worth a tag only for a cube
    ///     with an archetype built on it; this one has none.
    ///
    /// The rulings for each member — every boundary, when it was decided, the
    /// cards that settled it and how many reviewed cards it moved — are in
    /// docs/ontology/, one file per member (docs/ontology/Removal.md, …).
    /// </summary>
    [Flags]
    public enum CardEffect
    {
        None            = 0,
        Tokens          = 1 << 0,   // rulings: docs/ontology/Tokens.md
        Removal         = 1 << 1,   // rulings: docs/ontology/Removal.md
        Counter         = 1 << 2,   // rulings: docs/ontology/Counter.md
        RemovePermanent = 1 << 3,   // rulings: docs/ontology/RemovePermanent.md
        Wipe            = 1 << 4,   // rulings: docs/ontology/Wipe.md
        Bounce          = 1 << 5,   // rulings: docs/ontology/Bounce.md
        Ramp            = 1 << 6,   // rulings: docs/ontology/Ramp.md
        Disenchant      = 1 << 7,   // rulings: docs/ontology/Disenchant.md
        Discard         = 1 << 8,   // rulings: docs/ontology/Discard.md
        CardAdvantage   = 1 << 9,   // rulings: docs/ontology/CardAdvantage.md
        Filter          = 1 << 10,  // rulings: docs/ontology/Filter.md
        Reanimate       = 1 << 11,  // rulings: docs/ontology/Reanimate.md
        Buff            = 1 << 12,   // rulings: docs/ontology/Buff.md
        Protection      = 1 << 13,   // rulings: docs/ontology/Protection.md
        Burn            = 1 << 14,   // rulings: docs/ontology/Burn.md
        Sacrifice       = 1 << 15,   // rulings: docs/ontology/Sacrifice.md
        Steal           = 1 << 16,   // rulings: docs/ontology/Steal.md
        Tutor           = 1 << 17,   // rulings: docs/ontology/Tutor.md
        ManaFixing      = 1 << 18,   // rulings: docs/ontology/ManaFixing.md
        Pacify          = 1 << 19,   // rulings: docs/ontology/Pacify.md
        LandDestruction = 1 << 20,   // rulings: docs/ontology/LandDestruction.md
        Mill            = 1 << 21,   // rulings: docs/ontology/Mill.md
        Regrowth        = 1 << 22,  // rulings: docs/ontology/Regrowth.md
        Redirect        = 1 << 23,   // rulings: docs/ontology/Redirect.md
        Cheat           = 1 << 24,   // rulings: docs/ontology/Cheat.md
    }
}
