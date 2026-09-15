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
        Tokens          = 1 << 0,   // put CREATURE bodies onto the battlefield (Lingering Souls, Bitterblossom, Grave Titan) — and yours, not somebody else's. A Treasure, Clue, Food or Lander token is a resource the card hands over in passing, not a board presence, and Afterlife's Spirit goes to the creature's own controller; neither is what a go-wide deck is built on. Narrowed 2026-09-15, and WIDENED the same day in the other direction: a body need not arrive as a token at all, so earthbend and "all lands are 1/1 creatures" (Nature's Revolt) count — a go-wide deck cares about the creatures, not the wording that made them.
        Removal         = 1 << 1,
        Counter         = 1 << 2,
        RemovePermanent = 1 << 3,   // Vindicate / remove any permanent
        Wipe            = 1 << 4,   // board wipe / mass removal
        Bounce          = 1 << 5,   // unsummon / boomerang — a TARGETED return to hand (or a mass one). "Return this creature to its owner's hand" as an activation cost or an end-step drawback is a price the card pays, not an answer it offers. Narrowed 2026-09-15.
        Ramp            = 1 << 6,
        Disenchant      = 1 << 7,
        Discard         = 1 << 8,   // strip cards from somebody else's HAND (Mind Rot, Hymn to Tourach, Liliana of the Veil). Like Mill, only outward: a bare "discard a card" is nearly always a cost — madness, blitz, cycling reminder text, the back half of looting — and paying it is not attacking anyone. Narrowed 2026-09-15.
        CardAdvantage   = 1 << 9,   // a card the opponent does not get: draw two or more, a draw you can go back to (an activated ability or a recurring trigger — an ETB fires once and is a cantrip), or "draw a card for each X", which is the same thing counted the other way round. PARITY is not advantage, and it wears three disguises: a loot draws and discards in one breath (that is Filter alone, ruled 2026-09-04), cycling pays a card to replace itself, and an ability that sacrifices its own permanent runs once. Ruled 2026-09-11, parity carved out 2026-09-15.
        Filter          = 1 << 10,
        Reanimate       = 1 << 11,
        Buff            = 1 << 12,   // raise power/toughness. Only when it lands on SOMETHING ELSE (Giant Growth, Glorious Anthem, Bonesplitter): a creature that pumps only itself has a stat line, not an effect, so "{2}{R}: This creature gets +3/+0" and "Uril gets +2/+2 for each Aura attached to it" stay untagged.
        Protection      = 1 << 13,   // keep something alive, for somebody other than the card itself. TWO vocabularies, ruled equal on 2026-09-15: the keyword shield GRANTED to something else (Mother of Runes, Mystic Veil), and DAMAGE PREVENTION aimed at something else (Circle of Protection, Healing Salve, Orim). They do the same job — you hold them up and something survives — and the hand-tagged cards split 47 to 44 between them. Both clear the same gate: deployable in RESPONSE (an instant, flash, or an activated ability), so a bogle born with hexproof and a static Bubble Matrix are out; "This spell can't be countered" is the same self-shield written for the stack. Three shapes of prevention are deliberately excluded: a shield the card puts on ITSELF, a fog (it names no recipient), and preventing what a creature DEALS, which neutralises it and is Pacify — Maze of Ith and Gaseous Form are tagged that way. Regeneration is NOT in: measured, "regenerate target" cost 12 wrong for 4 right, so Death Ward stays untagged. Decided 2026-09-05, widened 2026-09-15.
        Burn            = 1 << 14,   // direct damage at a FACE (Lightning Bolt, Fireblast, Price of Progress). Damage pointed at a creature is how the game kills creatures and is already Removal, so Explosive Shot is Removal alone; "any target" is both, because it can go either way. A sweeper is Burn only when it catches the players too — Inferno's "each creature and each player" is Wipe AND Burn, Pyroclasm is Wipe alone. Damage to YOURSELF (Ancient Tomb, City of Brass, Juzam Djinn) is a price the card charges, not an effect. Ruled 2026-09-15.
        Sacrifice       = 1 << 15,   // a sacrifice OUTLET for your own creatures (Ashnod's Altar, Goblin Bombardment, Viscera Seer) — the half of the combo that makes a stolen creature worth taking, so read it next to Steal. NOT an edict, which empties somebody else's board (that is Removal's business), and not "as an additional cost to cast this spell, sacrifice a creature", which is a price paid once rather than a place to put things. Creatures, artifacts and anything named as a permanent all count — Atog and Infernal Tribute are Ashnod's Altar with different fuel — but LANDS do not: "sacrifice a land" is Harrow paying for a fetch, never an engine. Ruled 2026-09-15.
        Steal           = 1 << 16,   // threaten / act of treason / control magic (temporary or permanent control theft)
        Tutor           = 1 << 17,   // search library for a card (Demonic Tutor, Green Sun's Zenith, creature fetch) — selection/consistency, not raw card advantage
        ManaFixing      = 1 << 18,   // mana colour fixing (dual/fetch lands, multicolour rocks) — distinct from Ramp; a card can be both. Named in full: bare "Fixing" read as "fixing what?" (mana or hand), and the tagger shows this name verbatim. Old "Fixing" tags still parse via the alias table.
        Pacify          = 1 << 19,   // soft/pseudo removal: neutralise SOMEBODY ELSE'S creature without destroying it (tap-lock/Icy Manipulator, Pacifism/Arrest, detain). A card that only restrains ITSELF is a stat line, not an effect: "this creature can't attack" is defender, printed on a Wall, and "can't attack unless defending player controls an Island" is a drawback. Narrowed 2026-09-15, which was the single largest source of noise on the board.
        LandDestruction = 1 << 20,   // destroy/exile lands (Stone Rain, Sinkhole, Wasteland, Armageddon). Deliberately narrow: attacking the mana base by removing lands, NOT the wider "mana denial" family (Winter Orb, Blood Moon, Sphere effects), which stays untagged. Distinct from RemovePermanent, whose rules only read the literal "destroy target permanent" of a Vindicate.
        Mill            = 1 << 21,   // put cards from a library into a graveyard (Glimpse the Unthinkable, Millstone, Altar of Dementia). Only when it is aimed at SOMEBODY ELSE. A bare "mill three cards" fills your own graveyard, which is fuel for what the card does next rather than an attack on anybody — Stinkweed Imp's dredge and Fell Gravship's self-mill are graveyard cards, not mill cards. Narrowed 2026-09-15; the older cost exclusion (Deep Spawn, Millikin) follows from the same reading, since a price is always paid by yourself.
        Regrowth        = 1 << 22,   // return a card from a graveyard to HAND (Regrowth, Raise Dead, Eternal Witness). Deliberately NOT merged with Reanimate, which puts it straight onto the battlefield: one hands back a card you still have to pay for, the other cheats the cost — different speeds, different decks.
        Redirect        = 1 << 23,   // act on a spell already on the stack WITHOUT countering it: change its target (Misdirection, Deflection) or copy it (Fork, Reverberate, Twincast). Kept as one tag because both do the same job in a deck — they turn an opponent's spell into your problem-solver — and because splitting them would give two tags of a few dozen cards each. Distinct from Counter, which answers by removing the spell.
    }
}
