# RemovePermanent

Canonical record of the rulings for `CardEffect.RemovePermanent`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

Vindicate / remove any permanent.

A COLOUR-restricted kill is this tag too, ruled 2026-09-20: "destroy target red permanent" (Active
Volcano, Flash Flood, both Paladins, the Elemental Blasts) answers whatever is in the way, and that
breadth is the point even when you would usually point it at a creature.

NB a "non\w+" alternation written to catch "nonblack permanent" also catches "NONLAND permanent",
which the first rule already reads.

Read 2026-10-15: a permanent narrowed by what it is NOT or as legendary ("target noncreature,
nonland permanent" — Thief of Existence; Commander Sofia Daguerre) and a TOKEN are this tag; a
permanent YOU own or control, a permanent CARD in a graveyard, a blink whose return follows straight
on, and an exile handed back to the owners' hands (The Spot) are not.
