# Removal

Canonical record of the rulings for `CardEffect.Removal`. The one-line tooltip shown in the tagger
is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

answer ONE creature or planeswalker.

A SHRINK counts, ruled 2026-09-15: a creature at zero toughness dies exactly as one that is
destroyed, in both vocabularies — "target creature gets -3/-3" and "put a -1/-1 counter on target
creature". Two edges matter. The toughness has to actually go down, so Icy-style "-2/-0" is Pacify,
not this; and it has to be aimed at ONE named creature, because "each creature gets -1/-1" is a Wipe
and is tagged as one.

Widened 2026-09-17 so every rule here reads a QUALIFIED target — "damage to target ATTACKING
creature", "target creature AN OPPONENT CONTROLS gets -X/-X" — and reads a fireball divided among
several things, and an EDICT in any wording provided it is aimed at THEM — and, ruled 2026-09-27,
"EACH PLAYER sacrifices A creature" too, although it costs you one: one creature each is an edict,
not a board, so it is this tag and not Wipe (Fleshbag Marauder, Plaguecrafter, Accursed Marauder);
an edict that EXILES says "each opponent chooses a creature they control and exiles it" and counts
the same, even repeated (Sothera, 2026-09-28). Two shapes are deliberately out: a NONCREATURE
target, which ends in the same letters and was reading as a kill, and an answer that can only touch
what is already blocking the card, which is a combat trick.

Widened again 2026-09-19, 113 wrong out of 5,037 reviewed cards down to 67.

"EQUAL TO" is how the game writes one creature hitting another, and the rule read one word order,
one length of filler and four destinations: the order reverses ("target creature deals damage TO
ITSELF equal to its power"), the filler runs to 99 characters (Slash of Light), and the destination
is qualified as freely as any other target ("to target creature AN OPPONENT CONTROLS", "to UP TO ONE
OTHER target creature", "to EACH OF TWO OTHER target creatures", "to ANY OTHER target").

The AMOUNT too: "half X damage, rounded down" (Banshee), "X plus 1 damage" (Meteor Shower), "each of
X targets" (Firestorm).

An AURA is the third place the shrink lives and says "ENCHANTED CREATURE gets -2/-2" rather than
"target".

ANY toughness malus counts, ruled 2026-09-19: the size is not the question and neither is what
happens to the power, so "+2/-1" (Funeral Charm), "+1/-1" (Coils of the Medusa) and "-0/-1"
(Ironclaw Curse) are all this tag, and so is a "-2/-1 counter" (Contagion). A threshold tried first,
requiring real power loss or two whole points of toughness, was reading four hand tags as a rule and
they were slips. "-2/-0" stays Pacify, which is the same ruling from the other side: it is the
TOUGHNESS that has to move. A shrink aimed at a creature YOU CONTROL is a pump with a price
(Ashnod's Battle Gear). A creature on the BOTTOM of a library is answered (The Spot's Portal,
Dramatic Accusation); on TOP it is handed straight back (Time Ebb).

A FIGHT has to name what it fights: the bare word is also the name of a mode, and School Daze offers
"Fight Crime", which counters a spell and draws a card. Three shapes match the words and answer
nobody, and all three are read across the WHOLE card so that killing one of theirs and blinking one
of yours keeps the tag: your OWN creature (Cold Storage, Safe Haven, Niko, Y'shtola), a creature
CARD IN A GRAVEYARD (Eater of the Dead, Summoner's Sending), and a clause restricted to combat —
which the game writes in the active voice ("target creature IT'S BLOCKING") and in the trigger
("WHENEVER THIS CREATURE BLOCKS") as often as the passive one the rule already knew. Measured and
rejected, twice over: "destroy target \<colour> permanent" as Removal. It ran a day on the reading
that such a card is aimed at a creature in practice, and was ruled back to RemovePermanent on
2026-09-20 — the BREADTH is what that tag is for, the same reason Vindicate lives there.

NB a "non\w+" alternation written to catch "nonblack permanent" also catches "NONLAND permanent",
which is RemovePermanent's whole job — 26 false positives in one measurement.

A SPELL that only sets up damage on the next spells you cast removes nothing by itself, ruled
2026-10-13 on Season of the Bold ("di suo non genera una removal, deve aggiungere un altro
effetto"); a permanent with that trigger still is (Flaming Tyrannosaurus, Surtr, 4 of 4).

Read again 2026-10-14 from the sitting of 10-06..10-10: a creature SHUFFLED into its owner's library
by a spell or an ability; the edict that EXILES, the one offered as a VILLAINOUS CHOICE, and
Bellowing Mauler's life-or-a-creature price; a creature that STOPS BEING ONE — a Treasure, a Clue, a
Food, a face-down 2/2; a phasing that lasts as long as the card does (a tapper's "remains tapped" is
Pacify's); an Aura that can exile what it holds; and "(twice) that much damage to target creature",
ruled "Tutte Removal" the same day. Out: a BLINK of any creature (Twining Twins, The Eternal
Wanderer, Explosive Getaway, 3 of 3), a creature YOU OWN, a token's ONE-POINT ping (the Devils, 3 of
3 — two or three points from a token that enters or leaves still count), and an exile its owner may
play from, which is Bounce (Extraordinary Journey).

Ruled 2026-10-16 (the Removal pass, 836 reviewed: 18 proposed wrongly and 26 missed before, 18 and 6
after, every one of them a ruling below or a known disagreement — Tidal Wave, and Bounty Hunter,
which has no text): a PUNISHER that lets them keep the creature for life or another price is an
edict and this tag, like Bellowing Mauler and the villainous choice (Fandaniel, Fatal Lore, Pillar
Tombs of Aku; Mogis, Tomb Blade, Unnatural Hunger); a target limited to a creature TYPE never is (7
of the 9 reviewed: the Walls, King Suleiman's Djinn or Efreet, Knight of the Mists — so Rashida
Scalebane, Phoenix Down and Human Frailty are not), in the spirit of the tribal rulings of 10-15; an
ultimate's EMBLEM counts (Chandra, Spark Hunter); and a removal ability GRANTED for one turn counts,
from a spell as from a permanent (Dead Before Sunrise, Gaze of Pain, Vorinclex // The Grand
Evolution) — Season of the Bold's ruling is about damage set up for the next spells and is not
extended ("No, conta sempre").

Read the same day — this tag: a trigger's damage to any target (14 of 17: Spider-Man 2099, Sawblade
Skinripper), a repeatable ping at an attacking or blocking creature (8 of 11: Crimson Manticore,
Marjhan, Sawback Manticore), a qualified edict (5 of 6: Papalymo Totolymo), "artifact or creature"
(9 of 10: Transforming Flourish), an artifact creature (Hearth Charm, like Chandler), a tap bite
with its drawback (Gargantuan Gorilla, like Karplusan Yeti), each creature you control dealing
damage to one of theirs (Case of the Gateway Express), a random mode like any other mode (Umaro,
Summon: Magus Sisters), damage dealt instead of combat damage (Farrel's Zealot, Soltari Guerrillas,
Farrel's Mantle), a bite naming "the creature an opponent controls" (Beastie Beatdown), a delayed
destroy of a target that is not yours (Blood Frenzy; Mogg Cannon and Stone Giant pick your own),
"then if that creature's power is 0 or less, destroy it" (Depressurize), the creature with the least
power (Drop of Honey), "the chosen creature(s)" (Expel the Unworthy, V.A.T.S., Sadistic Shell Game;
Plasma Caster's blocker stays out), "that amount of" and "twice X" damage (Fear of Burning Alive,
Torch the Witness), a creature with a stun counter shuffled away (Floodpits Drowner), "that
permanent" (Galvanic Discharge), a -1/-1 counter every draw step (Giant Oyster), a cumulative upkeep
in MANA handed to it (Mana Chains; Decomposition's life stays out), "chooses and sacrifices one of
those creatures" (Retribution), half their creatures (Rush of Dread), the most votes (Vault 11:
Voter's Dilemma), "then sacrifices a nontoken creature" (Vile Mutilator), "each of those creatures
with flying" (Winter Blast). Not this tag: "and this creature assigns no combat damage" names no
victim (Goblin Vandal), an exile that ends when the card UNTAPS is Pacify (Tawnos's Coffin), and a
pure Wipe stays Wipe alone (Misfortune, Withering Wisps: 200 of the 212 reviewed Wipes carry no
Removal).
