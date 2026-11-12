# Disenchant

Canonical record of the rulings for `CardEffect.Disenchant`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-10 ("Sì, così").** An ARTIFACT or an ENCHANTMENT that is not a creature and
can be somebody else's, named by its type — artifact, enchantment, or Aura — leaves the battlefield
for good: destroyed, exiled (also until the card that exiled it leaves), shuffled into its owner's
library, or sacrificed to an edict; one, several or all, as long as a mass blow does not take the
creatures too (that is Wipe). The card is Disenchant, "Destroy target artifact or enchantment."
Measured before any code, it already held on all 229 reviewed cards: edicts 9 of 9 tagged, a mass
blow that takes the creatures 0 of 10, "target artifact creature" 0 of 10, your own only 0 of 5, a
blink 0 of 7. The paragraphs below are its readings, and the two rulings of the same day are at the
end.

Any count and any verb: "destroy target artifact", "destroy UP TO ONE target artifact", "destroy X
target artifacts", "exile TWO target artifacts", "destroy ANOTHER target artifact", and the mass
form — Shatterstorm, Tranquility, Serenity, Seeds of Innocence.

An EDICT aimed at an artifact answers one too, as the creature edict is Removal: Pick Your Poison
and Gaius van Baelsar are tagged here.

AURA is the third noun. A SWEEPER that takes the creatures in the same breath is Wipe (Jokulhaups,
Nevinyrral's Disk, Ultima, Death Begets Life) — every mass Disenchant that counts leaves creatures
alone; Structural Assault destroys the artifacts alone and only then deals damage. An artifact
CREATURE is a creature, so Chandler and Hearth Charm are Removal.

Your OWN is never an answer (Rats of Rath). The O-ring family splits on what it NAMES rather than
on the exile coming back: "exile target ARTIFACT or creature … until this leaves the battlefield"
counts (Mystical Tether, Detention Chariot, Trapped in the Screen, Earth Kingdom Jailer), "exile
target nonland PERMANENT" is RemovePermanent (Web Up, Seam Rip, White Auracite).

Ruled 2026-09-19; took the tag from 43 wrong out of 5,037 reviewed cards to 2.

Read 2026-10-16: "any number of target" artifacts (Rampaging Yao Guai), the type Season of Gathering
lets you choose, and an artifact or enchantment shuffled into its owner's library (Cathartic
Parting). Not this: a sacrifice watched by "whenever an opponent sacrifices", a trigger and no edict
(Vengeful Tracker), and a target artifact or enchantment CARD in a graveyard (Grave Robbers, Saruman
of Many Colors; 0 of 31 reviewed). Wrath of the Skies is handed back without the tag.

Read 2026-11-06, each tagged by hand: the permanent every player may CHOOSE, "starting with you,
each player may choose an artifact or enchantment you don't control. Destroy each permanent chosen
this way" (Druid of Purification, as Ultimate Magic: Meteor, 2 of 2); a second target after "and",
"destroy up to one target planeswalker and up to one target artifact" (Sword of Sinew and Steel; 2
of 2 with Convert to Slime); and the edict repeated for another noun, "then repeat this process for
an enchantment and a planeswalker" (Invoke Despair, as Pick Your Poison; 1 of 1).

RULED 2026-11-10, Q1 ("Disenchant a tutte"): an AURA ATTACHED TO a creature or a land is the
answer, and the 2026-09-19 reading that it was about that thing is overturned. The Pacifism on your
creature, the Utopia Sprawl on a land, is somebody else's enchantment, so Miracle Worker ("destroy
target Aura attached to a creature you control"), Savaen Elves and Pyramids ("… attached to a
land") are tagged by name; they were 0 of 3 reviewed, all from 2026-09-04. Every Aura attached to a
TARGET permanent counts too, the permanent staying: Strip Bare ("destroy all Auras and Equipment
attached to target creature"), Street Sweeper; Devout Harpist and Piety Charm follow, all four
unreviewed. Still out: the Auras that leave WITH their permanent (Silence the Believers, Gauntlets
of Chaos, Flickerform, "attached to them / it"), the Auras on the card itself (Hakim, Loreweaver),
and a fight that reaches them with no target (Treefolk Mystic, "that creature").

RULED 2026-11-10, Q2 ("nessun sottotipo", "risposta stretta"): no subtype but Aura is read.
Equipment, Vehicle, Room or Curse is a niche answer to one kind of permanent, not to "an artifact
or an enchantment" — Molten Rebuke ("destroy target Equipment"), Anthropede ("destroy target
Room"), Crash and Burn and Plow Through ("destroy target Vehicle"), Fiery Annihilation, Ray of Ruin:
0 of 6 reviewed. The Aura stays because it is the shape in which an enchantment takes a creature
out, the answer Disenchant is for. Unreviewed and left without the tag: Rustspore Ram, Turn to
Dust, Unforge, Witchbane Orb ("destroy all Curses attached to you").

Read 2026-11-12: a target permanent destroyed when it turns out to be an artifact (Topple the
Statue's "Tap target permanent. If it's an artifact, destroy it", Disenchant and Pacify by hand).
