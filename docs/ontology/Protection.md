# Protection

Canonical record of the rulings for `CardEffect.Protection`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-10** ("Scudo pronto"): a PERMANENT that is not the card itself survives an
answer because the card gives it a shield — a keyword (hexproof, indestructible, protection, shroud,
ward), damage prevented or redirected, regeneration, a return from death, a shield counter, a
blink, an unsummon or a phasing of your own — choosing WHO at a moment of yours (an instant, flash,
an activated ability, a trigger on being targeted), or covering ALWAYS a group of your permanents
with no condition. Mother of Runes is the card. Out as consequences, not exceptions: a shield on
itself; on the player alone; on a SPELL, which is no permanent ("can't be countered"); a recipient
chosen at SORCERY SPEED (an Aura or an Equipment on its creature, an enters trigger without flash,
"Activate only as a sorcery"); an area with a CONDITION; a fog, which stops a combat and saves no
permanent from a threat (CardEffect.cs); damage a creature DEALS (Pacify); a toughness-only bonus
(Buff); a shield on what the card itself destroys. Kept as exceptions with their reasons, ruled
below: one TRIBE or TOKENS (the Buff reading of 2026-09-21, as in Reanimate and Burn), and
protection from a creature TYPE over the area (2026-10-25, the same tribe seen from the other
side). It held on 296 of the 297 reviewed cards tagged by hand (FN Become Anonymous) with one
proposed wrongly (FP Ellie, Vengeful Hunter), both readings below; the two answers of the same day
moved two more, both named (Q1, Q2; Yarus, given the tag in Q2, lost it again that evening).

The rulings below are the history; each one is consistent with the definition or kept as an
exception with its reason.

keep something alive, for somebody other than the card itself.

TWO vocabularies, ruled equal on 2026-09-15: the keyword shield GRANTED to something else (Mother of
Runes, Mystic Veil), and DAMAGE PREVENTION aimed at something else (Circle of Protection, Healing
Salve, Orim). They do the same job — you hold them up and something survives — and the hand-tagged
cards split 47 to 44 between them. Both clear the same gate: deployable in RESPONSE (an instant,
flash, or an activated ability), so a bogle born with hexproof and a static Bubble Matrix are out;
"This spell can't be countered" is the same self-shield written for the stack. Three shapes of
prevention are deliberately excluded: a shield the card puts on ITSELF, a fog (it names no
recipient), and preventing what a creature DEALS, which neutralises it and is Pacify — Maze of Ith
and Gaseous Form are tagged that way.

Regeneration was kept out on 2026-09-05 ("regenerate target" cost 12 wrong for 4 right) — OVERTURNED
2026-09-28.

Decided 2026-09-05, widened 2026-09-15.

Three rulings on 2026-09-28, the questions left open since 09-11, all "sì": REGENERATING somebody
else is this tag (Death Ward, Village Elder, Life Matrix's granted one; a tribe's is not, Baron
Sengir; an Aura's "regenerate enchanted creature" was read in too, and taken out again on
2026-09-29, below); a BLINK that saves is (your own exiled and returned — Salvation Swan, Safe
Haven; anybody's until the next end step — Getaway Glamer, Parting Gust; your own airbent when
targeted — Monk Gyatso; your own returned to HAND in response — Ambrosia Whiteheart, Sunpearl Kirin
— taken out and put back on 2026-09-29, below), but a return paid as a COST is not (Familiar's
Ruse); and a SHIELD COUNTER is (Protection Magic).

Read the same day because the hand tags already had them: a creature handed a RETURN FROM DEATH
(Presumed Dead), damage REDIRECTED away from a creature (Blood of the Martyr, Reflect Damage,
Martyrdom), UMBRA ARMOR cast with flash (Dog Umbra, not Lion Umbra), an ARTIFACT phased out (Vision
Charm), and four prevention wordings (Remedy's "dealt THIS TURN to", Gatta and Luzzu's passive,
Shadowbane's "you and/or creatures", Fleeting Flight's "dealt to IT" after a target).

The GATE is asked of the LINE the shield sits on since the same day: an unrelated activation used to
vouch for a static shield (Flickering Ward, Paladin's Arms, Cathedral Acolyte, all untagged), and a
card cast "as though it had flash" (Mystic Veil, Ward of Lights), turned FACE UP (Essence of
Antiquity), CYCLED (Agonasaur Rex) or triggered by BECOMING THE TARGET counts as held up. A shield
for ONE TRIBE or for TOKENS is not this (Basri's Cats, Ainok Strike Leader's tokens — 0 of 4 tagged,
the Buff reading of 2026-09-21), nor is one being TAKEN AWAY (Nowhere to Run, Spectacular Pileup — 0
of 5; and Rebel Salvo's singular "loses indestructible", 2026-09-29).

Ruled 2026-09-29: a save you can trigger at instant speed counts ("si possono usare instant quindi
contano") — Kykar's blink under "whenever you cast a noncreature spell", read with the modal bullet
it introduces, and Phelia's, which has flash and writes the clock first ("at the beginning of the
next end step, return that card"); Another Round, a sorcery, was a slip, and so were Final
Showdown's missing tag and Angus Mackenzie's fog. The same day "IT" opening a sentence after "this
creature" was read as the card itself (Pristine Skywise, Seasoned Hallowblade), and so is "HE" or
"SHE" anywhere (Miles Morales's back face).

A STATIC shield over an AREA is this tag, ruled 2026-09-29 ("le protezioni statiche ad area …
mettiamole Protection, quelle condizionate no"): a group of your permanents named by card type or
colour (Restricted Office, The Walls of Ba Sing Se, Righteous War, Shalai), a targeting ban on
everybody (Dense Foliage), static prevention for creatures (Inner Sanctum, Crystal Barricade, Bubble
Matrix — which a test had pinned as NOT this since 09-05), and damage to you and your permanents
redirected to one creature (Ancient Adamantoise). Not an Aura's or an Equipment's single creature
unless it can be held up (0 of 18 tagged), not a creature TYPE (Riders of Gavony), and not a
CONDITION — counters, tapped or untapped, during your turn, as long as (Innkeeper's Talent,
Cathedral Acolyte, Saryth, The Seriema). The four single cases the same day were slips: Subdue,
Fatal Fissure, Reverse Polarity, Raiding Party. A shield for the PLAYER ALONE — "you have protection
from each of your opponents" (Absolute Virtue, whose hand tag was corrected), "you gain hexproof"
(Blossoming Calm), "you gain shroud"

(Gilded Light) — is neither this tag nor Pacify, ruled 2026-09-29 ("nessuno dei due"); "you AND
permanents you control" still shields the permanents (Veil of Summer), and phasing out every
permanent you control is an area shield (Teferi's Protection, Perch Protection).

Prevention of damage to the player alone stays Pacify, per the 2026-09-22 ruling, when a permanent
prevents it again and again; one cast once, from a spell, is a Fog and neither tag (Pacify.md,
ruled 2026-10-13).

A BLINK behind an activation limited to SORCERY SPEED saves nothing, corrected 2026-09-29
(Lilysplash Mentor): a blink dodges only what is already on the stack. A shield that lasts the turn
still counts at sorcery speed, which is why Endurance Bobblehead's indestructible "until end of
turn" was ruled this tag by name on 2026-09-25 — OVERTURNED 2026-11-10 (Q1, below). A shield an AURA
or an EQUIPMENT gives the creature it is ON is not this tag even behind a cost you can pay in
response, ruled 2026-09-29 on the regeneration Auras ("non è instant e non è ad area" — Carapace,
Thrull Retainer, The Brute, Regeneration): the Aura went on at sorcery speed.

A FLASH Aura still answers (Fae Flight, Serpent Skin), and so does an ability the Aura hands its
creature in quotes or aims at a target (Samite Blessing, Healer's Headdress, Floating Shield).

An UNSUMMON of your own was taken out the same day on Dour Port-Mage ("Unsummon su se stessi non è
protection, Airbend sì perché costa poco rigiocarlo") and PUT BACK that evening — "una creatura
flash che entra e rimbalza qualcosa di tuo conta protection -> anche Dour Port-Mage può tornare
Protection" — for every instant-speed form: a flash creature's enters trigger (Ambrosia Whiteheart,
Sunpearl Kirin, Whitemane Lion), a disguise turned face up (Forum Familiar), an activation (Dour
Port-Mage, Vedalken Mastermind) and the instant itself (Narrow Escape). The human had already put
Protection back on the three cards the first ruling handed back. It is still not Bounce, because it
answers nobody. Nor is a shield on a creature the same card DESTROYS at the next end step, ruled the
same day on Glyph of Destruction ("il muro viene distrutto alla fine, non è una protezione"): it
buys one combat and the creature is gone, so nothing is saved ("destroy or sacrifice it at the
beginning of the next end step" is on 15 reviewed cards, none of them this tag). Kithkin Armor,
whose prevention is the Aura's own for its creature, is not this tag either (confirmed the same
day).

Read again 2026-10-14, the plan's gaps: your own permanents phased out BY CHOICE ("any number of
target … you control", Clever Concealment, Gallifrey Falls // No More; Ripples of Potential's "those
permanents"), but not your lands alone (Taniwha); a BACKUP creature's hexproof handed on with flash
(Saiba Cryptomancer); what died THIS TURN returned from your graveyard when it can be held up
(Samwise the Stouthearted, Gerrard's Hourglass Pendant — not Moira's combat trigger nor Lobelia's
opponent's graveyard); a blink of "artifact, creature, or enchantment you control" (Scrollshift); an
activation cost up to 60 characters long (Arwen, Mortal Queen, which also reached Delif's Cube's
regeneration and Seasoned Tactician's Circle of Protection, 9 to 1 and 13 to 1, both handed back);
and a tribe's creature spell entering with hexproof is a tribe's shield (Jade Orb of Dragonkind).
Measured: Protection F1 97.9 to 99.5 before the store pass.

Read 2026-10-16: "all abilities" lost names no beneficiary (Monument to Perfection).

Read 2026-10-24, each under a ruling above: a granted return from death that also comes back from
EXILE and goes to the HAND (Ashnod's Intervention); a MORPH Aura attached as it is turned face up,
which lands like a flash Aura (Gift of Doom); a blink whose return opens a d20 TABLE (Lae'zel's
Acrobatics); an exile its CONTROLLER may accept, marked with a counter and brought back by a second
activation the way Safe Haven's is (Livio, Oathsworn Sentinel); target creatures phased out in the
PLURAL and a target PERMANENT phased out (March of Swirling Mist, Unite the Coalition), but not one
that "can't phase in" for as long as something holds it (The Pandorica, The Phasing of Zhalfir);
WARD written out over you and your permanents (Parnesse, the Subtle Brush). Out: "IT" after an
activation cost that names a land you control is still the card (Trench Behemoth), and a TRIBE's
prevention is a tribe's shield (Rescue Retriever, Goldbug — 0 of 2 tagged).

Ruled 2026-10-25, on the three cards those readings left wrong and one more: protection from a
creature TYPE over the area is not this tag (Gor Muldrak's "You and permanents you control have
protection from Salamanders", like Lord of the Nazgûl's Ring-bearers and Haytham Kenway's Assassins,
0 of 3 tagged); a condition the card makes TRUE ITSELF as it enters is no condition (Dawnglade
Regent makes you the monarch, so its hexproof over your permanents is static, and the initiative
reads the same way); a death TRIGGER that returns your creature is no save (Magnanimous Magistrate,
untagged by hand like Valkyrie's Call and Yarus) — OVERTURNED 2026-11-10 for an area with no
condition (Q2, below; Magistrate stays out on its condition), while a return HELD UP for one
creature that dies this turn is (Cryptek, tagged by hand; Reincarnation, which returns another card,
stays out). Gift of Doom and Livio, Oathsworn Sentinel keep the tag ("una specie di Protection"),
Rescue Retriever stays out, and March of Swirling Mist gains Pacify beside it (Pacify.md). Measured
with the readings of 2026-10-24: Protection 12 wrong to none, F1 97.6 to 100.

Read 2026-10-30: a keyword granted beside the return from death ("gains lifelink and "When this
creature dies, return it to the battlefield…"", Return to Action; 6 of 7, the seventh Perigee
Beckoner's on an enters trigger). Its +1/+0 stays Buff, the lifelink being more than the shield's
rider (Buff.md).

Read 2026-11-05: "IT phases out" in the sentence AFTER a target creature, Slip Out the Back's "Put a
+1/+1 counter on target creature. It phases out." (Protection alone by hand, 1 of 1 in the store).
Read for this tag only, as the plural is: nobody puts a counter on a creature to take it out of the
game, so it is yours. The "until" and "can't phase in" guards are the same, so The Moment's "It
phases out until The Moment leaves" and The Pandorica stay out. The counter is the shield's rider
and not a Buff of its own, because Protection is already on the card.

Read 2026-11-05: target creatures phased out by CHOICE when they are one of several types, Spectral
Adversary's "up to that many other target artifacts, creatures, and/or enchantments phase out"
(Pacify and Protection by hand, with March of Swirling Mist), and when the phasing is an option of
the sentence after the targets, Change of Plans' "Each of X target creatures you control connive.
You may have any number of them phase out" (Filter and Protection by hand, 1 of 1).

Read 2026-11-05: "exile any number of target creatures AND/OR planeswalkers you control" with a
return at the end step, Semester's End (Protection alone by hand): your own blink, which the
"you control" in the type list now shows (no Removal, no answer to anybody).

Read 2026-11-05: a creature or artifact exiled and BROUGHT STRAIGHT BACK, whoever's it is, at
instant speed ("Exile target artifact or creature, then return it to the battlefield under its
owner's control with a +1/+1 counter on it", Planar Incision, Protection alone by hand), the save of
Twining Twins and Parting Gust without the end-step wait: the blink of anybody's of 2026-09-28.
1 reviewed card of this wording is tagged and 1 is not: Eldrazi Confluence's blink is one mode of
three on a card tagged Tokens and Removal on 2026-09-12, and is reached by the same reading
(+Protection, handed back). Proposed with it, unreviewed:
Eldrazi Displacer, Flicker of Fate (which loses its Removal, 2026-10-14: a blink is no answer) and
Spaceshift; Flicker, Hallowed Respite and The Mighty Thor, Jane Foster stay out, being sorceries or
loyalty abilities.

Read 2026-11-05: a self-shield that ENDS its sentence, Guardian Naga's "During your turn, prevent
all damage that would be dealt to this creature." (Disenchant alone by hand): 0 of 5 reviewed cards
written so carry the tag. Glittering Lion and Glittering Lynx, unreviewed with Protection in the
store, lose that old proposal with it.

RULED 2026-11-05 ("Solo Buff"): a toughness-only bonus is Buff and not this tag, flash or not.
Paladin's Shield (flash, attaches to a creature you control as it enters, +0/+2) is named: its hand
tags become Buff alone, as Great Defender's instant +0/+X already is.

RULED 2026-11-10, Q1 ("Bobblehead -Protection"): a shield whose recipient is chosen at SORCERY
SPEED answers nothing, whatever the shield is. Endurance Bobblehead ("Up to X target creatures you
control get +1/+0 and gain indestructible until end of turn … Activate only as a sorcery") is named
and keeps Ramp and Buff, like Last Night Together, a sorcery whose indestructible until end of turn
is Buff alone; the blink of Lilysplash Mentor was already out for the same reason. Unreviewed, lost
with it: Burdened Stoneback, and Daring Fiendbonder's indestructible counter. Prevention at sorcery
speed is on no card. Fated Clash keeps the tag: it is cast "as though it had flash" in combat.

RULED 2026-11-10, Q2 ("ci sta e così rimuoviamo eccezioni alla regola"): a RETURN FROM DEATH over
an area of your creatures with no condition is the static area shield of 2026-09-29, overturning
the 2026-10-25 line on death triggers. Valkyrie's Call ("Whenever a nontoken, non-Angel creature
you control dies, return that card to the battlefield", Reanimate by hand) is named and gains the
tag. Persist or undying handed to the same area reads the same way. Reached, all unreviewed:
Avacyn, Angel of Horror; Luminous Broodmoth ("without flying" filters the area, as "non-Angel"
does); Isilu, Carrier of Twilight; Mikaeus, the Unhallowed. Ruled the same evening ("Yarus non è
protection. Yedora, Grave Gardener neanche"): only FACE-DOWN creatures is a condition — Yarus, Roar
of the Old Gods, named in Q2 and given the tag, loses it again and keeps CardAdvantage — and a
return FACE DOWN brings back something that is not the creature (Yedora's Forest land, Missy's 2/2
Cyberman). Still out, by the
definition's condition: a +1/+1 counter the creature must have (Marchesa), a budget of reprieve
counters (Magnanimous Magistrate), one tribe (Thunderbolts Conspiracy's Villains), a creature you
do not own (Edea), anybody's (Missy), the creature an Equipment is on (Resurrection Orb).

Read 2026-11-10: the TARGET of the damage the card deals is not who the grant after "and" goes to —
"Ellie deals 2 damage to target player and gains indestructible until end of turn" shields Ellie
(Burn and Sacrifice by hand, the one false positive); no Buff moved with it. Your own creature
exiled and CLOAKED back at instant speed is the blink that saves (Become Anonymous, Tokens and
Protection by hand, 1 of 1). And a spell kept from a counter is no permanent saved: "can't be
countered" left the vocabulary, 0 of 7 reviewed cards so worded tagged (Mistrise Village's held-up
"The next spell you cast this turn can't be countered", Frenzied Baloth, Rhythm of the Wild);
Vexing Shusher, Surrak Dragonclaw, Cavern of Souls and Bound // Determined, unreviewed, lose it.
