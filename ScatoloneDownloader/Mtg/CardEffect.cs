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
    /// Deliberately absent, decided 2026-09-05 — every tag is a question asked of
    /// 30k cards, so one that never changes a design decision costs and returns
    /// nothing:
    ///   - FOGS. A Fog prevents a combat, it does not answer a threat. It buys a
    ///     turn rather than saving a permanent, which is why it is not folded into
    ///     <see cref="Protection"/> either, despite also being instant-speed
    ///     prevention.
    ///   - LIFEGAIN. A resource, not an interaction. Worth a tag only for a cube
    ///     with an archetype built on it; this one has none.
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
        Tutor           = 1 << 17,   // search library for a card (Demonic Tutor, Green Sun's Zenith, creature fetch) — selection/consistency, not raw card advantage
        ManaFixing      = 1 << 18,   // mana colour fixing (dual/fetch lands, multicolour rocks) — distinct from Ramp; a card can be both. Named in full: bare "Fixing" read as "fixing what?" (mana or hand), and the tagger shows this name verbatim. Old "Fixing" tags still parse via the alias table.
        Pacify          = 1 << 19,   // soft/pseudo removal: neutralise a creature without destroying it (tap-lock/Icy Manipulator, Pacifism/Arrest, detain)
        LandDestruction = 1 << 20,   // destroy/exile lands (Stone Rain, Sinkhole, Wasteland, Armageddon). Deliberately narrow: attacking the mana base by removing lands, NOT the wider "mana denial" family (Winter Orb, Blood Moon, Sphere effects), which stays untagged. Distinct from RemovePermanent, whose rules only read the literal "destroy target permanent" of a Vindicate.
        Mill            = 1 << 21,   // put cards from a library into a graveyard (Glimpse the Unthinkable, Hedron Crab, Stinkweed Imp). Covers BOTH directions: milling an opponent as a win condition and milling yourself to fuel a graveyard deck — the card is doing the same thing, only the target differs. NOT the mill you pay as a price, though: Deep Spawn's "sacrifice this creature unless you mill two cards" and Millikin's "{T}, Mill a card: Add {C}" spend the library to buy something else, the way Serendib Djinn's land sacrifice is a drawback rather than land destruction.
        Regrowth        = 1 << 22,   // return a card from a graveyard to HAND (Regrowth, Raise Dead, Eternal Witness). Deliberately NOT merged with Reanimate, which puts it straight onto the battlefield: one hands back a card you still have to pay for, the other cheats the cost — different speeds, different decks.
        Redirect        = 1 << 23,   // act on a spell already on the stack WITHOUT countering it: change its target (Misdirection, Deflection) or copy it (Fork, Reverberate, Twincast). Kept as one tag because both do the same job in a deck — they turn an opponent's spell into your problem-solver — and because splitting them would give two tags of a few dozen cards each. Distinct from Counter, which answers by removing the spell.
    }
}
