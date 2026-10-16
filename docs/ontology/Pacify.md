# Pacify

Canonical record of the rulings for `CardEffect.Pacify`. The one-line tooltip shown in the tagger is
in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

soft/pseudo removal: neutralise SOMEBODY ELSE'S creature without destroying it (tap-lock/Icy
Manipulator, Pacifism/Arrest, detain), INCLUDING by preventing what it deals — Maze of Ith and
Gaseous Form, the reading ruled on 2026-09-05 and finally implemented on 2026-09-15. Three things
are not this tag, all of them the same question asked again: UNTAPPING a creature (the rules were
reading "untap target creature" as "tap target creature", which was a quarter of the false
positives), tapping a creature YOU control as a cost (Energy Tap), and a card that stops YOUR OWN
creatures attacking (Akron Legionnaire). The prevention half asks the same question twice more:
Mtenda Lion prevents what IT deals, which blunts its own attack, and Ebony Horse prevents what a
creature YOU CONTROL deals, which is vigilance bought at instant speed — Foxfire says almost the
same sentence and is Pacify, so the whole difference is "you control". And a lock the OPPONENT CAN
BUY OUT OF is a tax rather than a lock — Heroism and Winter's Chill let the attacker pay and come
through, so whether anything is neutralised is their decision (ruled 2026-09-16, "non c'è
certezza"). Unconditional prevention IS the tag: Safeguard, Warning and Subdue say what Horn of
Deafening says and were realigned to match. A card that only restrains ITSELF is a stat line, not an
effect: "this creature can't attack" is defender, printed on a Wall, and "can't attack unless
defending player controls an Island" is a drawback.

Narrowed 2026-09-15, which was the single largest source of noise on the board.

The tag was worked on 2026-09-22 and closed at 17 wrong out of 164 — precision 78.8% -> 95.1%,
recall 62.1% -> 94.5% — and the finding was not a reading problem: THE HAND TAGS CONTRADICTED
THEMSELVES on identical wording. Six pairs were put to the human and all six were resolved the same
way. Twiddle against Twitch, Riptide ("tap all blue creatures") against Blinding Light ("tap all
nonwhite creatures"), Word of Binding ("tap X target creatures") against Crashing Wave, Storm
Elemental and Sunstar Chaplain against Sterling Keykeeper and Starport Security, Koskun Falls
against Propaganda, and Ultimecia against them all.

A PLAIN TAP IS THIS TAG, and both proposed lines were rejected: the lock need not LAST — "altrimenti
saltano il punto e tutti i tappini", and Detain is temporary and has always been here — and tapping
need not be the card's main job. 39 reviewed entries were realigned onto the tag that day, which is
what the rest of this paragraph is measured against. Four boundaries hold it in.

THE VICTIM MUST BE ABLE TO BE A CREATURE: "tap target artifact" is mana denial and neutralises
nobody (Relic Barrier, Touchstone, Phyrexian Gremlins, and Hyperion Blacksmith, which twiddles an
artifact where Twiddle itself names a creature). AN UNTAP LOCK ON LANDS is mana denial too, ruled
"per le terre niente Pacify" — Choke and Curse of Marit Lage stop Islands and Winter's Night a snow
land, and none is tagged; asked per LINE and phrased as "names a land and nothing else" rather than
"fails to name a creature", because an Aura says "enchanted PERMANENT doesn't untap" and names
neither (Flood the Engine, Tractor Beam, Stuck in Summoner's Sanctum).

TAPPING ON YOUR OWN ATTACK TRIGGER is a combat trick and not a lock: it clears one blocker for a
swing already happening, and seven reviewed cards do it with none tagged (Seasoned Marshal, Sidar
Jabari, Conformer Shuriken, Thunder Lasso, Web-Shooters, Vengeful Villagers, Kimahri, whose "at the
beginning of combat on your turn" is the same trigger from the other side). And "\<this creature>
CAN'T ATTACK OR BLOCK UNLESS …" is the price of a statline — the Wall reading of 2026-09-15, which
could not see this wording because "can't attack or block" vouches for the sentence before anyone
asks whose creature it is; read per line, and an older card writes its own name (Hazoret Godseeker,
Ketramose, Sab-Sunen, Patchwork Beastie, Tiger-Dillo, The Lion-Turtle). Five wordings were added the
same day.

A STUN COUNTER is how every card printed since 2021 writes an untap lock, and it has to land on
somebody else — Tonberry, Baloth Prime and Ambling Stormshell stun themselves as a drawback.

The PLURAL "creatures DON'T untap" was missing entirely, and with it every classic lock (Meekstone,
Marble Titan, Mudslide, Dream Tides, Magnetic Mountain, Thelon's Curse, An-Zerrin Ruins, Wrath of
Marit Lage); so was "players skip their untap steps" (Stasis, Sands of Time). The Propaganda tax
says "can't attack YOU unless", which the rule could not read because it wanted punctuation straight
after the verb.

NEUTRALISING WITHOUT KILLING is the tag's other half and was ruled in wholesale: base power 0
(Island of Wak-Wak, Singing Tree, Sorceress Queen), becoming a small NAMED body (Spider-Man No
More's 1/1 Citizen with defender, Honest Work's Humble Merchant, Unable to Scream's 0/2 Toy — the
[0-2] is what keeps Lizard, Connors's Curse and Titania's Song out), a shrink that also strips the
abilities (Fresh Start), and "attacking creatures get -1/-0" (Weakstone).

A BARE "-5/-0" was not here (measured and rejected at 2 tagged out of 12) until it came back SPLIT
on 2026-10-13, ruled "Pacify solo se dura": a power shrink that LASTS is this tag — an Aura
(Cryoshatter, Morgul-Knife Wound, Disturbing Conversion, 4 of 4) or an activation you can aim every
turn (Jace, the Perfected Mind's +1, Pradesh Gypsies, Ghosts of the Damned and Staff of Zegon
realigned and handed back) — while a one-shot until end of turn is not (Desperate Parry, Deceive the
Messenger and Sea Hag untagged by name; Shrink, Unagi's Spray never were), nor one on a repeating
trigger (0 of 6, Fear of Falling), nor an activation that sacrifices the card (Cabal Trainee), nor
an Aura on a creature that becomes yours (Duskmourn's Domination, Animate Dead).

A TAP fired at sorcery speed, by an enchantment of yours entering, is not this tag either (Rimefur
Reindeer, 2026-10-13: "gli incantesimi si giocano come sorcery"); a creature's own ETB tap still is
(20 of 22).

Read the same day: a Cursed Role on any creature not yours (Asinine Antics, Gadwick's First Duel), a
creature that becomes a 1/1 and loses all abilities (Hunted by The Family), an exile until its
controller's NEXT end step (The Eternal Wanderer), a phase-out for as long as the card stays tapped
(The Pandorica), and a stun counter "on it" once the card has named the victim (Trap the
Trespassers, Shackle Slinger).

PHASING OUT is this tag AND Protection, ruled together because the same words do both jobs — "puoi
usarlo sia sull'opponent per prendere tempo che su di te per salvare qualcosa"; the permanent has to
be a creature (Vision Charm phases an artifact) and an O-ring phase-out that lasts until the
enchantment leaves belongs to the temporary-exile family, which still has no ruling (Oubliette,
Tawnos's Coffin, Summon: Ixion, and Icy Prison, which the human called their own mistake and moved
to Removal). And PREVENTING DAMAGE TO THE PLAYER ALONE is this tag and NOT Protection — "le
prevenzioni al solo giocatore mettiamole come solo Pacify": nothing of yours is saved, the attack
simply stops mattering, which is what a Propaganda does. A prevention that also covers permanents
keeps both, and so does one that answers a SOURCE (Prismatic Strands).

But a prevention to you CAST ONCE, from a spell, is a Fog and not this tag, ruled 2026-10-13
("Ultimate Magic: Holy errore" — Holy keeps only the Protection of its indestructible; Eerie
Interference and Take the Bait untagged; Deep Wood realigned and handed back); the permanent that
prevents again and again stays (Al-abara's Carpet, Conservator, Scarecrow). And a tap on your own
attack that STUNS every time outlasts the swing and is this tag (Mjölnir, 2026-10-13: "lo fa
sempre"), while a stun bought with a sacrifice is the trick's price (Vengeful Villagers, "solo
dietro un costo"). Finally, REMINDER TEXT and worked EXAMPLES lock nobody down — Primal Clay
explains defender with "(A creature with defender can't attack.)" and Balduvian Shaman quotes "black
creatures can't attack" twice inside an example of text-changing — so the patterns are re-asked with
the brackets stripped.

Worked again 2026-10-16 and closed at 10 wrong out of 265 reviewed (F1 96.0% -> 98.1%), every one of
the ten a card the rulings below correct or hand back. Five rulings, asked one at a time.

A FLICKER IS NEVER THIS TAG ("Flicker mai Pacify"): Parting Gust and Phelia, Exuberant Shepherd were
the only two of seven reviewed flickers of somebody's permanent to the next end step tagged here,
and both lose it and were handed back. Twining Twins says Parting Gust's very words and is
Protection alone; the gift that exiles for good is what makes Parting Gust Removal ("Parting Gust ha
l'opzione per rimuovere definitivamente la creatura"). The Eternal Wanderer's exile to ITS
CONTROLLER'S next end step, a whole turn of theirs, stays.

An O-RING ON A SAGA is this tag ALONE, not Removal ("Sì, solo Pacify"): the creature comes back when
the Saga is sacrificed after its last chapter. Summon: Ixion (Removal and Pacify by hand) and Trial
of a Time Lord (Removal alone) were corrected by name, and Vault 13: Dweller's Journey was reached
and handed back (its Disenchant was not asked). An O-Ring on any other permanent stays Removal: 23
reviewed, none tagged here.

An untap lock GIVEN to the creature a Wall blocked is this tag ("entrambe Pacify"): Mindbender
Spores and Glyph of Delusion, whose quoted "This creature doesn't untap during your untap step" had
read as the card taxing itself. Glyph was handed back.

A CUMULATIVE UPKEEP an Aura hands to the creature is Removal and not this tag, paid in life as in
mana ("Removal, no Pacify"): Decomposition, Burn and Pacify by hand, is now Burn and Removal like
Mana Chains, because a tax the controller can pay is not a lock (2026-09-16).

A creature an AURA turns into an artifact is this tag as well as Removal ("Aura = +Pacify"): Sugar
Coat's Food comes back as the creature when the Aura goes, while Vraska, Betrayal's Sting's Treasure
is for good and stays Removal alone.

Read the same day without a ruling. Out: an Equipment's untap lock, which holds down your own
creature (T-45 Power Armor; Cement Shoes, Vulshok Gauntlets and Heavy Arbalest among the unreviewed;
Shield of the Righteous locks the creature equipped one BLOCKS and stays), an Aura's on a creature
it has put under your control (Dance of the Dead), "can't attack or block ALONE" (Toby's token and
Pipsqueak, 0 of 2; six unreviewed like Mogg Flunkies), a base 0 on a creature you cast (Cycle of
Life, 0 of 8), a tap that makes the creature deal the damage (Deadshot), and "other creatures can't
attack" (Oracle en-Vec). In, one reviewed card each: an Aura's "otherwise, it has base power and
toughness 1/1" (Burden of Proof), a target of theirs that "loses all abilities until your next turn"
(Azure Beastbinder), an attacker made blocked (Dazzling Beauty), an Aura that removes its creature
from combat (Imprison), and damage dealt to you sent back to the creature that dealt it (Mangara's
Equity).

Handed back the same day, older than the rule that now covers them or alone against it: Wayspeaker
Bodyguard +Pacify (a Flurry CAST trigger, which taps like Grappling Kraken's landfall and Galvanic
Giant's cast trigger, 3 of 4; reviewed before the plain-tap ruling of 09-22, and wrongly listed
among the attack-trigger taps since), Summon: Yojimbo +Pacify (a Propaganda tax on chapters II and
III, 3 of 4 with Propaganda, Koskun Falls and Elephant Grass; also before 09-22), Harrier Strix
+Pacify (the one "when ... enters, tap target" card of 19 reviewed left untagged), and Nature's
Chosen -Pacify (an untapper, tagged before the 09-15 ruling; Infuse says the same and is untagged).
