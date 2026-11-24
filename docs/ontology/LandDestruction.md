# LandDestruction

Canonical record of the rulings for `CardEffect.LandDestruction`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-24: a LAND that may be somebody else's leaves the battlefield because of your
card — destroyed, exiled, sacrificed, or put back in its owner's hand or library — taken by a
TARGET that names it a land (in a type list, by basic type, nonbasic), by an EDICT that has to take
a land, or by a RULE that takes them all, even one that never says "land" (Balance, Apocalypse).
Stone Rain is the card.** Out, and out by hand already: mana denial that leaves the land on the
battlefield (Winter Orb, Blood Moon, Choke; 0 of the 38 reviewed cards that make a land a basic
type, keep it from untapping or tap it); "target permanent" (Generous Gift, Despark:
RemovePermanent, 0 of 24); a land taken by Steal (0 of 3); your own lands; a land card in a
graveyard; an Aura destroyed while ATTACHED to a land, which protects it; a choice of types left to
the victim (Braids, Cabal Minion).

Every ruling below is coherent with the definition, and no card is kept as an exception by name;
Blight, Kudzu and Erosion are the three cards corrected by name, nothing is handed back.

Before 2026-11-24: destroy/exile lands (Stone Rain, Sinkhole, Wasteland, Armageddon). Deliberately
narrow: attacking the mana base by removing lands, NOT the wider "mana denial" family (Winter Orb,
Blood Moon, Sphere effects), which stays untagged. Distinct from RemovePermanent, whose rules only
read the literal "destroy target permanent" of a Vindicate.

Widened 2026-09-19, where all 23 of the misses were: the land is almost never alone in the sentence.
It sits in a TYPE LIST ("destroy target artifact, creature, or land" — Aftershock, Creeping Mold),
behind a COUNT ("destroy up to one target artifact, up to one target creature, and up to one target
land" — Boom Box), beside a SECOND TARGET (Fumarole), or in a mass form that takes the creatures
with it ("destroy all creatures and lands" — Devastation).

A BASIC LAND TYPE is a land by another name and the only wording Boil, Acid Rain, Reign of Chaos and
Volcanic Eruption ever use. Three more name the victim without saying "target land": Cleansing's
"for each land, destroy that land", Desolation's land edict with the victim clause in between, and
Planetary Annihilation's "chooses six lands they control, then sacrifices the rest". Fallow Earth
answers a land without destroying it. Four shapes match the words and are not this: a land card
already IN A GRAVEYARD is fuel (Steward of the Harvest), an AURA ATTACHED TO a land protects it
(Savaen Elves, Pyramids), your OWN denies nobody anything (Rats of Rath), and a rebalance that
destroys nothing and hands basics back was not denial (Natural Balance, overturned 2026-10-16 —
see below) — that last excused only a line with no destroy or exile in it, because Sandworm, Price
of Freedom and Magmatic Hellkite all kill a land and offer a basic as consolation. 23 wrong out of
5,037 reviewed cards down to 4.

Read 2026-10-16: lands an opponent controls destroyed by count, not by target (Krenko's
Buzzcrusher). Not this: exiling THIS CARD from your hand as a cost, whatever land it then names
(Spara's Adjudicators).

Ruled 2026-10-16 ("sì, sempre"), on Balance (Wipe alone) against Planetary Annihilation (Wipe and
this) and Apocalypse against Jokulhaups: EVERY SWEEP THAT TAKES THE OPPONENTS' LANDS is this tag,
whether or not it says "land". All the PERMANENTS destroyed, exiled or returned (Apocalypse,
Upheaval, Worldfire, Urza's Sylex, Worldslayer), and a pile kept that may hold lands or is lands,
the rest sacrificed (Balance, Cataclysm, Global Ruin, Balancing Act, Liliana, Dreadhorde General).
Natural Balance is in with them, and a basic offered back is consolation, as it already was for
Sandworm. Not this: what a NAME, a COLOUR, a mana value or a chosen type picks (Wirefly Hive, Wash
Out, Celestial Kirin, Season of Gathering), a pile of NONLAND permanents (Cataclysmic Gearhulk), all
permanents EXCEPT FOR LANDS (Cyclone Summoner, Elspeth Tirel), and your own (Glimpse of Tomorrow,
Return of the Nightstalkers's "destroy all Swamps you control"). Every land sweep was Wipe too from
the same day until 2026-10-17, when a sweep of lands alone went back to this tag alone — see Wipe.

Read the same day: lands returned to their owners' hands (Sunder), a land closing a TYPE LIST
after "sacrifices all" (Wave of Vitriol) or after "an artifact and" (Structural Collapse), a land
edict following a creature edict, ", then sacrifices a land" (Din of the Fireherd, Smallpox), and
"all AURAS attached to" a land, plural, which protects it like the singular (Street Sweeper). A land
that closes a CHOICE, "an artifact, creature, or land of their choice", never has to be a land and
is not this (Braids, Cabal Minion).

Read 2026-10-30: a land closing a list of "up to one target" too long for the run of 80 (Chaotic
Transformation's five, after "and/or"; 2 of 2 with Boom Box).

Read 2026-11-03, on the principle of 2026-09-15 (a tag is what the card aims at somebody, never what
it pays): a COMPARISON names nobody as the victim. Akuta, Born of Ash's "if you have more cards in
hand than each opponent, you may sacrifice a Swamp" sacrifices its own land, and own-land sacrifice
is untagged on 66 of the 69 reviewed cards that make it. Akuta, an unreviewed proposal, is the one
card it moves.

RULED 2026-11-24 with the definition ("Sì, così"), one answer. Q1 "LandDestruction a tutte": an
AURA that destroys the land it enchants is a Stone Rain on a delay — when the land is tapped
(Blight, Kudzu: "when enchanted land becomes tapped, destroy it"), when its counters run out (Orcish
Mine, the one tagged by hand, on 11-06), at your command (Pooling Venom's "{3}{B}: Destroy enchanted
land"), or unless its controller pays every upkeep (Erosion, as Cleansing's "unless any player pays
1 life" already was). Blight, Kudzu and Erosion, untagged on 09-04, are corrected by name; Spreading
Algae, Steam Vines, Uncontrolled Infestation and Pooling Venom, unreviewed, follow. It is not the
Aura of Savaen Elves and Pyramids: there the Aura on a land is what gets destroyed.
