# RemovePermanent

Canonical record of the rulings for `CardEffect.RemovePermanent`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-08: somebody else's permanent of ANY type, chosen by YOU, taken off the
battlefield for good — destroyed, exiled, shuffled into its owner's library, or turned into a
Treasure. Vindicate is the card.** "Any type" means the card says "permanent", narrowed or not:
nonland, noncreature, legendary, a colour, a mana value, a token. "Chosen by you" is a target, or a
pool you name (Chaos Defiler's one per opponent picked at random, Fatal Grudge's card type, The
Legend of Yangchen's permanents chosen among your opponents'). Out: a LIST of types ("target
artifact, creature, or enchantment", Removal and Disenchant on all 21 reviewed cards); a return to
hand or to the library near the top (Bounce); a permanent you own or control; a blink; an edict,
where THEY choose; and an exile its owner may play (Bounce). The answers of that day are at the end.

Every ruling below is coherent with the definition; none keeps a card by name.

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

DEFINED 2026-11-08, the definition at the top ("Sì, così"), from Vindicate. Three answers the same
day:

RULED 2026-11-08, Q1: an EDICT stays out, the permanent is chosen by you ("Edict fuori"). Shard of
the Void Dragon ("each opponent sacrifices a nonland permanent of their choice"), Lord Xander
("sacrifices half the nonland permanents they control of their choice") and Braids, Arisen
Nightmare (an opponent who MAY sacrifice, a punisher) were tagged by hand after the edict reading
of 2026-10-15 and are corrected by name; Throne of the Grim Captain, with Shard's own sentence,
Kefka and the 8 reviewed annihilators were untagged already.

RULED 2026-11-08, Q2: an exile its OWNER MAY PLAY or cast for as long as it stays exiled is Bounce,
a bounce with a tax like airbend and Prowl ("Bounce"): Soul Partition, RemovePermanent by hand, is
corrected by name to Bounce; Aerial Extortionist was Bounce by hand and stops being proposed this
tag. The Bounce rule reads the nonland permanent too, and Release to the Wind (unreviewed) moves
with it.

RULED 2026-11-08, Q3: a nonland permanent PHASED OUT for as long as you control the card is an
O-ring on any permanent ("RemovePermanent"), although it phases back in once the Saga leaves: The
Phasing of Zhalfir, Removal and Wipe by hand, is corrected by name to RemovePermanent and Wipe. The
creature phased out so (Oubliette) stays Removal.

Read 2026-11-12: a colour count qualifies the permanent like "legendary" (Vanishing Verse's "exile
target monocolored permanent"; multicolored and colorless were tried beside it and moved no card).
