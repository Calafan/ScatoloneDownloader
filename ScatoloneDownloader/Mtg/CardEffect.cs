using System;

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
    /// </summary>
    [Flags]
    public enum CardEffect
    {
        None            = 0,
        Tokens          = 1 << 0,
        Removal         = 1 << 1,
        Counter         = 1 << 2,
        RemovePermanent = 1 << 3,   // Vindicate / remove any permanent
        Wipe            = 1 << 4,   // board wipe / mass removal
        Bounce          = 1 << 5,   // unsummon / boomerang
        Ramp            = 1 << 6,
        Disenchant      = 1 << 7,
        Discard         = 1 << 8,
        CardAdvantage   = 1 << 9,
        Filter          = 1 << 10,
        Reanimate       = 1 << 11,
        Buff            = 1 << 12,
        Protection      = 1 << 13,
        Burn            = 1 << 14,   // direct damage
        Sacrifice       = 1 << 15,
        Steal           = 1 << 16,   // threaten / act of treason / control magic (temporary or permanent control theft)
    }
}
