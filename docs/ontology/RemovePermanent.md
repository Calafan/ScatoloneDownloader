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

Ruled 2026-10-27: a permanent SHUFFLED into its owner's library ("the owner of target
nonenchantment permanent shuffles it into their library") is this tag alone ("Solo
RemovePermanent": Audacious Swap, Removal by hand, corrected by name), as a creature shuffled away
is Removal's; Chaos Warp and Oblation follow. Annihilator's sacrifice of a permanent of their choice
stays out, like the plain edict of 2026-10-15.

Read 2026-10-30: one target per player, the owners named after the targets ("Those permanents'
owners shuffle them into their libraries", Guff Rewrites History), under the 2026-10-27 shuffle
rule.

RULED 2026-11-05, corrected 2026-11-06: Minimus Containment is named RemovePermanent ALONE, not
Removal and not Pacify (11-05 had Removal too; the human on 11-06: "è sia removal che remove
permanent ma basta solo questo secondo me per l'ontologia attuale"; 99 of the 101 reviewed cards
tagged RemovePermanent by hand carry no Removal). An Aura with "Enchant nonland permanent" whose
permanent becomes a Treasure artifact that loses all other abilities is the breadth of Vindicate;
the Auras that enchant a creature, planeswalker or Clue (In Too Deep, Removal) or a creature or Food
(Sugar Coat, Removal and Pacify) stay without this tag.

Read 2026-11-05: YOU choose a nonland permanent among each opponent's and a die picks which one is
destroyed, "for each opponent, choose a nonland permanent that player controls. Destroy one of them
chosen at random" (Chaos Defiler, 1 of 1), not the plain edict of 2026-10-15 where they choose.
