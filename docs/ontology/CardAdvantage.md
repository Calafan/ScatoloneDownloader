# CardAdvantage

Canonical record of the rulings for `CardEffect.CardAdvantage`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

a card the opponent does not get: draw two or more, a draw you can go back to (an activated ability
or a recurring trigger — an ETB fires once and is a cantrip), or "draw a card for each X", which is
the same thing counted the other way round.

PARITY is not advantage, and it wears three disguises: a loot draws and discards in one breath (that
is Filter alone, ruled 2026-09-04), cycling pays a card to replace itself, and an ability that
sacrifices its own permanent runs once.

The CLUE and the IMPULSE are the borderline cases and both answer the same question: one card, once?
A single Clue has to be cashed for {2} and a single impulse replaces the card that made it, so each
is a rider — they earn the tag when the card makes them REPEATEDLY, and an impulse earns it outright
when it takes more than one card off the top, which is a draw two wearing a different coat.

Ruled 2026-09-11, parity carved out 2026-09-15, the one-for-one line drawn 2026-09-15.

Parity is COUNTED, from 2026-09-16: a loot that draws three and discards one is a Careful Study with
a bonus, so only a provable gain rescues the tag and anything unreadable stays parity. The same day
closed four more ways the card is handed back without the word "discard" — Dream Cache puts it on
top, Lat-Nam's Legacy shuffles it in, Jandor's Ring pays back the card just drawn.

Three more shapes ruled 2026-09-16: the TOP OF YOUR LIBRARY is a second hand (Fblthp, Glarb, the
Traveling Chocobo never run out even though they never draw); LOOK AT N AND TAKE MORE THAN ONE is a
draw with selection, while taking exactly one is Filter; and CASTING OUT OF A GRAVEYARD OR EXILE
counts only when you can go back to it every turn (Festival of Embers, Edgar), never for one
flashback card.

Ruled again 2026-09-19 and this time the count is symmetric: ANY net gain is CardAdvantage and stops
being Filter, a net of zero is Filter and was never advantage. Four more ways a card arrives were
measured and added the same day.

WHOSE DRAW IS IT: a card that only ever draws for somebody else gives nothing away (Sibilant Spirit,
Harbor Guardian, Phelddagrif) and 8 of those 10 cards are untagged, but "TARGET player" and "EACH
player" are deliberately not in that list because you point Ancestral Recall and Braingeyser at
yourself.

TRIGGERING OFF a draw is not drawing (Underworld Dreams, Clinquant Skymage), 9 of 10 untagged. A
draw that EXILES THE CARD ITSELF out of your graveyard runs once, the sibling of the one-shot
sacrifice, and four of the fifty over-fires were identical Surveyors printed with that line.

And the second-hand rule written on 2026-09-16 had a window too short for the very cards it named —
Glarb puts 44 characters between "play lands" and "from the top of your library" where 40 were
allowed, and Fblthp plots rather than plays — so it was widened to 15 cards of which 14 are tagged.
Several Clues in one breath now count too, the reading the Treasure got the day before.

THE CARD ITSELF IS A CARD, ruled later the same day, and it completes a count that had been
half-done since 2026-09-16: Ponder is -1 for the Ponder and +1 for the draw, which is nothing, and
Grab the Prize is -1 for the spell, -1 for the discard it charges and +2, which is also nothing and
makes it Abandon Attachments with the discard moved into the cost line. So does every card the
effect charges, including a discard paid as a COST and the lands Soldevi Sage feeds itself.

A ONE-SHOT therefore has to draw two more than it pays before it has gained anything, while a
REPEATABLE ability paid for the card once and never again and needs only one — which is the whole
difference between Emmessi Tome and Racers' Scoreboard, two cards that print the same words on an
activated ability and on an enters trigger. Repeatability and cost are read PER LINE, because they
belong to the ability that loots: reading the colon in Uthros Scanship's Station text as its loot's
activation cost made a one-shot look repeatable and handed it a card it never got.

Four rulings on 2026-09-20 and they are one ruling read four ways.

LOOK AT THE TOP FEW AND PUT ONE INTO YOUR HAND earns the tag when the ability REPEATS, which
overturns the 2026-09-16 line that taking exactly one is Filter alone: Browse, Beastrider Vanguard
and Water Tribe Rallier charge a cost you can pay again, while Morbius the Living Vampire prints the
same words behind "Exile this card from your graveyard" and Lupinflower Village behind "Sacrifice
this land", and those run once. The same question settles the IMPULSE — Elkin Bottle, Bruse Tarl and
Cait Sith repeat, Equilibrium Adept's enters trigger does not — and it settles a repeatable draw
whose COST IS A CARD OR A PERMANENT, which is advantage even so, because the ability bought that
card once: Ahriman was the only untagged card among the twelve written that way. And a loot that
only breaks even is Filter and nothing else however the halves are worded, which is why Brainstorm
is not here (draw three, put two back) and neither are Rook Turret and Oblivious Bookworm ("you may
draw a card. If you do, discard a card"), while Tersa Lightshatter earns it on a different line
entirely — an attack trigger that exiles a card from your graveyard and lets you play it.
Repeatability is now read PER ABILITY rather than per card, and that is what makes those splits
possible: a modal bullet belongs to the trigger that introduced it, because Parapet Thrasher prints
"Whenever one or more Dragons you control deal combat damage" three lines above its impulse, and a
Saga chapter that names two numbers fires twice (Rediscover the Way's "I, II —"). Two of those were
put to the human and RULED later the same day.

EXILE SEVERAL AND PLAY ONLY ONE is Filter and not this: you saw two and played one, which is a count
of zero — Riverwheel Sweep, Heroes' Hangout and Case of the Burning Masks all say it in the same
words, and the last prints it behind "Sacrifice this Case" so it cannot repeat either.

A REPEATABLE LOOT THAT LETS YOU PAY A PERMANENT INSTEAD of the card is Filter too (Chandra, Spark
Hunter and Reckless Detective) — the ability is repeatable, but each use still trades one thing for
one card, and that is selection. Three one-offs were recovered rather than left as known misses,
because a ruling that does not reach the odd card has not been applied: Three Wishes puts 108
characters between exiling three cards and letting you play them where the rule allowed 80; Memories
Returning reaches into your hand three times, one card at a time, and is advantage by the COUNT and
not by repetition; and Mnemonic Sliver's "{2}, Sacrifice this permanent: Draw a card" is printed
INSIDE QUOTES and handed to every Sliver, so the body that pays is a different one each time and the
one-shot veto does not apply, the same reading OnlyPumpsInsideQuotes gives Buff.

The LOOT guard is asked of the card with somebody else's discard blanked out, ruled 2026-09-20:
parity means ONE player trading, and "each opponent discards a card AND YOU DRAW a card" is not a
loot at all — it is their Discard beside your draw, which is what Jecht, Reluctant Guardian is
hand-tagged.

A SAGA CHAPTER THAT NAMES TWO NUMBERS fires twice, so the card it draws was bought once and drawn
again; same reading ChapterFiresTwice gives the put-into-hand rule.

Six more rulings on 2026-09-21.

A REPLACEMENT draw — "the next time you would draw a card this turn, INSTEAD …" — adds nothing at
all, it spends the draw you were going to have: all three reviewed cards are untagged and each is
tagged for what it swaps the draw for (Aladdin's Lamp Filter, Mangara's Tome and Ring of Ma'rûf
Tutor).

A DELAYED cantrip ("draw a card at the beginning of the next turn's upkeep") is still a cantrip: 2
of the 48 reviewed cards that say it were tagged and both are slips.

CYCLING is parity and always was: 2 of 45, same answer. A card that gives ITSELF A SECOND CAST is
not one card, so the loot that nets zero on one cast comes out ahead over both — Welcome the Dead is
-3 +4 with its flashback, Winternight Stories the same behind harmonize, Whispers of the Muse behind
buyback.

NB this is the OPPOSITE of what those same keywords mean for Regrowth, deliberately: there a card
rebuying itself fetched nothing, here it really is a second helping of the draw. And a REPEATABLE
DRAW WHOSE TRIGGER YOU CANNOT COUNT ON is not advantage. The human's words were "il punto è proprio
la difficoltà di trigger"; there is no wording that means "this rarely happens", but the four cards
each NAME their own difficulty and none has a counterexample — a coin flip (Goblin Artisans),
becoming the target of an AURA SPELL or an ACTIVATED ABILITY, which needs a second card before the
draw happens at all (Fugitive Druid, Professor Hojo), and a draw bought by handing an opponent a
permanent (Stiltzkin). The line it does NOT cross is Surrak, Elusive Hunter: his trigger is ANY
spell or ability an opponent aims at your creatures, which happens by itself, and he keeps the tag.
Recorded as four cards read one at a time rather than a principle. Four wordings were simply
unreadable and are now read: an ADDITIONAL card (Howling Mine, Sylvan Library), "draws UP TO n
cards" without the "may" that separates Diminishing Returns from Truce, a trigger with one sentence
in front of it (Rowen), and "that many cards FROM THE TOP of your library" (Symbiote Spider-Man,
Choco, Stargaze).

EXILE AND PLAY IT FROM THERE was confirmed as this tag on 2026-09-21 and was the single biggest hole
left: twenty reviewed cards, every one tagged by hand, every one missed, because each writes the
same idea differently. Five wordings now read — dig until a nonland card turns up and play it, which
is what DISCOVER spells out (Territorial Bruntar, Solstice Revelations, Breaching Dragonstorm) —
though a discover is ONE card and counts only when its ability repeats, ruled 2026-09-29 when the
human took the tag off Geological Appraiser's enters trigger and Hidden Volcano's self-sacrifice,
against Long-Range Sensor, Monstrous Vortex and Caparocti Sunborn; a standing permission over a
growing pile ("you may play cards exiled with this creature" — Evendo Brushrazer, Urianger, Triple
Triad); that pile going to your HAND instead (Necropotence, Duplicity, Knowledge Vault); casting for
free from among the exiled cards (Kefka, Portent of Calamity); and the top of SOMEBODY ELSE'S
library (Vaan, Outrageous Robbery). The one line inside it: when the pile is THEIRS ALREADY the card
is Steal and not this — Azula exiles a creature an opponent controls and a card from their
graveyard, Crabomination does it in three zones, and both are hand-tagged without CardAdvantage,
while Vaan and Outrageous Robbery take cards nobody had yet. Esoteric Duplicator was put to the
human as the one card standing against the self-sacrifice rule and RULED to keep the tag: the
trigger above its draw turns the sacrifice into a token copy of the same artifact, so the ability is
on the table again and the card was never really paid.

The tag was worked a SECOND TIME on 2026-09-21 and closed at 20 wrong out of 381 (precision 97.6%,
recall 97.1%), and almost all of it was reading rather than ruling. Seven wordings joined the ones
already read.

The COUNT IS OPTIONAL in "look at the top few and put one into your hand": a single card off the
top, every upkeep, is the same second hand the plural is (Darkstar Augur, Traveling Botanist, Bison
Whistle, and Phyrexian Portal, which digs ten and says "LOOKS at" because an opponent does the
looking) — 13 reviewed cards, 11 tagged. The same hand reached with a SEARCH counts when the ability
repeats, which is exactly what separates Land Tax from Gift of Estates. A card SACRIFICED FOR MORE
THAN ONE CARD is a trade it wins (Blitzball, Qiqirn Merchant, Reverberating Summons), which corrects
the one-shot-sacrifice rule rather than overturning it — that rule was written for the one-card case
and reads Preferred Selection right. Two exclusions were measured, not assumed: the count has to be
READABLE (All-Fates Scroll draws "X cards, where X is the number of differently named lands" and is
Ramp alone, the same answer an unreadable loot gets) and the line must not hand a card back (Conch
Horn draws two and puts one from your hand on top, which with the artifact is level).

EMPTYING YOUR HAND and drawing a fixed number back is not a loot, because the hand you paid can be
empty — Case of the Crimson Pulse solves itself on "You have no cards in hand" and then draws two
for nothing every upkeep; 3 cards, all 3 tagged.

A TRIGGERED DRAW PRINTED INSIDE QUOTES has been handed to a body that keeps it (Thief's Knife writes
it onto the Hero it makes, Herd Heirloom lends it for a turn), the same reading Mnemonic Sliver got
— 5 cards, 4 tagged, and it must not be a loot, because Ninja's Blades hands out "draw a card, then
discard a card".

THE MONARCHY is a card at the end of every turn with no draw written down anywhere, and it outlives
the permanent that granted it, so it is asked after the guards (Coin of Fate sacrifices itself to
hand the crown over).

ONE TRIGGER WORD COVERING TWO EVENTS is two cards: "when this artifact enters OR LEAVES the
battlefield, draw a card" (Cryogen Relic). And three more were simply the same sentence backwards —
"for each vote you received, draw a card" (Mob Verdict, Tempt with Bunnies), granting FLASHBACK to a
whole graveyard rather than naming a cast (Iroh), and looking at the top and casting it after rather
than before (Planetarium of Wan Shi Tong).

The SAGA CHAPTER rule now reads any number of numbers ("I, II, III — Pain — You draw a card") and
requires the draw to be UNCONDITIONAL, because a chapter that draws only when something else went
its way is the conditional rider this tag has no ruling on: The Tale of Tamiyo draws when two milled
cards happen to share a type, Vault 11 only when a vote produced no creature, and neither is tagged.
Two boundaries were drawn.

THE PILE IS THEIRS reaches their GRAVEYARD as well as their hand — six reviewed cards exile from a
named opponent's graveyard and not one is CardAdvantage (Hama, Ashen Powder, Fire Nation Salvagers),
because the card was already somebody's. And PARITY ACROSS THE TABLE is parity: "you draw a card AND
target opponent may draw a card" (Tataru Taru), "you may have target opponent draw three cards. If
you do, draw three" (The Legend of Yangchen), a loot every player may take (Mog) — three cards, none
tagged, and the carve-out for "TARGET player" and "EACH player" still holds. Four guards were asked
of the wrong thing and are now asked of the right one.

The SELF-SACRIFICE guard must be the line that DRAWS (Cryogen Relic sacrifices itself to stun a
creature on a different ability and was losing the tag to it).

The EXILE-FROM-GRAVEYARD guard must ignore REMINDER TEXT, since encore and flashback both spell
their cost as "Exile this card from your graveyard:" inside brackets (Eumidian Wastewaker). "Draw
that many cards" is added back after the guards, so it has to ask the one that reads whose
misfortune it counts (Kain hands himself to the player he hit; Mister Negative draws only if the
life he swapped left him worse off). And the self-cost pattern had never fired at all: its "\b" had
been written into the source as a literal BACKSPACE character by an earlier tool, invisible in every
diff since. Four hypotheses were MEASURED AND REJECTED and are recorded so they are not re-derived.
"A Saga chapter that draws is advantage" splits 7 to 13. "A modal bullet that draws under a trigger"
buys Teval's Judgment and costs Monument to Endurance and Zuko, Conflicted — 1 to 2. "A card paid
for with ANOTHER permanent is not free" is backwards: 9 of the 10 reviewed cards that sacrifice
something else to draw ARE tagged, and Susur Secundi is the lone exception, left as a known miss
rather than written into a rule. And "you may cast it from the top" must say LOOK or REVEAL, never
EXILE, because "exile the top card, you may play it" is the one-card impulse this tag already calls
a rider (Equilibrium Adept, Haste Magic, Summon: Brynhildr).

AN ANTE CARD carries no tags at all, ruled by measurement on 2026-09-21: every one of them opens by
telling you to take it out of the deck, so none is ever cast and none of their effects ever happens
— nine are reviewed and all nine are untagged. That is a veto on the WHOLE classifier and not on
this tag, and it lives at the top of Classify.

The tag was CLOSED on 2026-09-22 at 2 wrong out of 380 — precision 99.7%, recall 99.7% — and the
twelve questions that got it there were answered by the human in one sitting. Four answers were the
tag already being right and the HAND TAG being realigned.

The CONDITIONAL DRAW is not a principle at all: Haliya, Guided by Light and Stormbeacon Blade earn
the tag because they REPEAT (an end step, an attack), not because of the "if", and Beza and Summon:
Fenrir were the human's own slips and lost it.

A DISCARD CHARGED AS AN ADDITIONAL COST is parity once the spell itself is counted — that is Grab
the Prize's reading from 2026-09-19, and Laughing Mad and Sazacap's Brew now follow it and are
Filter alone.

"THEN DISCARD A CARD UNLESS …" is decided by the COUNT and never by the condition: Waterbending
Lesson is -1 +3 -1 and comes out ahead even when the waterbend goes unpaid, Winternight Stories is
the same behind harmonize, while Chakra Meditation and Katara draw one and hand one back and are
Filter. Four more were the HAND TAG being right and the reading extended to say so.

CASTING FROM THE TOP OF YOUR LIBRARY is the second hand whatever you pay for the card — Madame Web,
Clairvoyant had been left untagged on a reading of "Spider spells" alone and is this tag.

EMPTYING YOUR HAND TO REDRAW is advantage (Smellerbee, Rebel Fighter).

SACRIFICING ANOTHER PERMANENT to draw is advantage, which closes the 9-of-10 measurement of
2026-09-21 at 10 of 10 (Susur Secundi). And a REPEATABLE INVESTIGATE is advantage, which takes that
rule to 15 of 15 (Sophia, Dogged Detective) — the one card it was recorded as costing was not a
cost.

A LOOT SPREAD OVER TWO TRIGGERS is Filter and not this, ruled the same day: Teferi's Imp discards
when it phases out and draws when it phases back in, which is one card for one card on a timer. Five
more wordings joined the tag, each measured: CUMULATIVE UPKEEP PAID IN CARDS, which draws one more
every turn the permanent survives and is written where a cost goes so nothing was looking there
(Psychic Vortex); BOTH PLAYERS revealing the top and BOTH taking it, because symmetry is not parity
when everybody gains — Howling Mine's reading, given by name (Parker Luck); SEVERAL TOKENS THAT EACH
DRAW, which is the Clue count in a costume the Clue rules could not read and which the human put as
"-1 +2 = +1" (Niko, Nyla); WARP as a second cast, so a card that pays you on the way OUT pays you
twice (Anticausal Vestige) — deliberately not folded into the flashback family, which would reprieve
32 warp cards of which only 5 are tagged; and COPYING YOUR OWN SPELL, which is a second copy of a
card paid for once (Taigam). The copy has to follow the trigger IMMEDIATELY, which is what separates
Taigam from Mendicant Core, Guidelight, whose copy sits behind Max speed and a further {1} and which
carries no tag.

MAX SPEED is NOT a blanket difficulty: 34 reviewed cards print it and 4 are tagged. A third way an
ability runs ONCE was added to the repeat gate: it TRANSFORMS the permanent the moment it succeeds,
and the other face does something else. That is the whole difference between Sidequest: Catch a Fish
and Traveling Botanist, which print the same sentence word for word — the Botanist looks every time
it taps, the Sidequest turns into a land the first time it finds anything. Six reviewed cards
transform themselves this way and five are untagged. And the trigger-difficulty family took two
more, still read one at a time rather than as a principle: BENDING, a keyword action a deck has to
be built to perform (Avatar Aang), and a CHOSEN CARD NAME, which asks you to name a card and then
meet it that same turn (The Clone Saga, Demonic Consultation).

TWO CARDS ARE LEFT and both are recorded rather than chased. Lich replaces lifegain with draws and
the human called it "carta particolare, difficile da taggare"; it carries no tag at all and the
classifier proposes this one. And Teval's Judgment offers a draw as a MODAL BULLET under a trigger,
which was measured and rejected the day before: the shape buys that one card and costs Monument to
Endurance and Zuko, Conflicted.

Explained on 2026-09-22, and the three do not disagree — each falls to a rule this tag already has.
Teval's Judgment says "hasn't been chosen THIS TURN", so the choice returns every turn and the
second iteration is a card. Monument to Endurance triggers ON A DISCARD, so the draw is paid for
with a card and is Filter (OVERTURNED 2026-09-27: the human named it advantage, and "hasn't been
chosen THIS TURN" is now read, which takes Teval's Judgment with it — see Filter). Zuko, Conflicted
says "hasn't been chosen" with no reset, so the mode runs once in the game. The distinction is
readable — "this turn" is there in the words — but the family was ruled too card-specific to encode,
so no rule is written and Teval's Judgment stays a known miss.

An OMEN is not a second cast, corrected 2026-09-29: its "(Then shuffle this card into its owner's
library.)" sends the card away rather than handing it back, so Flush Out's "discard a card. If you
do, draw two cards" nets zero like Abandon Attachments and is Filter alone.

Filter is withdrawn from a card that gains only when both come from the SAME ability, 2026-09-29: a
repeatable loot beside a separate draw is both (Qiqirn Merchant) — see Filter.

Ruled 2026-10-13: a DRAW MODE under any trigger that repeats is this tag (Glissa Sunslayer, Hylda of
the Icy Crown, Black Market Connections — 7 of 9, Kastral, the Windcrested handed back), but not
when the mode is bought with a permanent (Gorbag of Minas Morgul) or each mode runs once a game
(Zuko, Conflicted, Silent Hallcreeper) — which reads the "this turn" distinction above after all,
Teval's Judgment included. A trigger ON A DISCARD that draws or impulses is this tag and not Filter
(Inti, Seneschal of the Sun; Monument to Endurance). A planeswalker's "draw, then discard unless" is
this tag and Filter both (Kaito Shizuki).

Ruled 2026-10-14: a draw bought by HANDING another player a permanent of yours is this tag ("Tutti e
due CA": Discerning Financier, Stiltzkin, Moogle Merchant — overturning 09-21 — and Kain, Traitorous
Dragoon, who hands over himself like Yes Man), and a COPY of your own spell is this tag when the
spell can be anything (Storm of Saruman, like Taigam) but not when it is historic or colorless,
which copies a permanent as a token as often as not (The Sixth Doctor, Echoes of Eternity — "Dipende
dal tipo copiato"). The same pass read the sitting's shapes: a PILE off your library into your hand
(Fact or Fiction, not Split the Spoils' graveyard pile), SEVERAL kept from one look, saga chapters
and Junk that repeat, a draw mode you may choose more than once, rebound granted, the monarch given
to any player, "that many" exiled and played, and three late-draw trigger shapes. Not this tag:
their draw after a shuffle (The End), a rummage for as many as were discarded (Fable of the
Mirror-Breaker, Filter), a card sacrificing itself BY NAME for one card (Stone of Erech), a card
tutored into exile (Beseech the Mirror), and a one-shot look that exiles ONE card to cast (Feral
Encounter, Filter).

Ruled 2026-10-16 (the CardAdvantage pass, 969 reviewed: 19 proposed wrongly and 31 missed before, 14
and 3 after, every one of them a ruling below or a known disagreement): a card off the top of an
OPPONENT's library that you may play is this tag when the ability REPEATS ("CA se ripete":
Dream-Thief's Bandana, Edward Kenway, Rev, Tithe Extractor — the family stood 9 tagged to 6), and so
is a card out of THEIR graveyard (3 to 3: Saruman of Many Colors), while the one that fires once
stays Steal alone (Azula, Cunning Usurper; Crabomination); a sacrifice that buys TWO modes, one of
them a draw, is this tag (Caesar, Legion's Emperor) and a choose-one is not (Gorbag); a graveyard
cast for ONE turn is not this tag but Regrowth ("Una volta = no CA": Urabrask // The Great Work);
exiling that many and playing one is Filter and Cheat, not this tag (Vault 112: Sadistic
Simulation); and a graveyard ability granted to, or a cast limited to, a creature TYPE is not this
tag ("Tribale = niente CA": Solemn Doomguide, Zul Ashur, Deeproot Historian, Rivaz of the Claw),
unless a card type stands beside the type (Arcade Gannon).

Read the same day — not this tag: a draw when the card ITSELF becomes the target of an opponent's
spell (Altanak, Cactarantula: 0 of 2, where "a creature you control" is 2 of 2, Surrak and Elrond)
or of your OWN, which needs a second card to fire (Nadu); "that player draws" after combat damage
(Red Death, Shipwrecker); THEIR spell exiled and cast, which is Steal (Smirking Spelljacker);
"Activate only once" (Thought Shucker); a look at the top any time with no leave to play it (Vesuvan
Drifter: the other 24 reviewed looks all play it); a card sacrificing itself by its short name
(Vohar); a graveyard cast paid by exiling OTHER graveyard cards, forage and escape (Osteomancer
Adept, The Grim Captain's Locker: 0 of 11; Tarrian's Journal keeps it); a one-turn impulse that
fires once (Waltz of Rage); your own creature exiled and played back (Norin); a draw for each
opponent who can't (Refurbished Familiar); combat damage to its owner (The Beast, Deathless Prince);
a flashback bought by sacrificing a Clue (The Fugitive Doctor). This tag: a draw-then-put-back
counted like a loot (Brainsurge -1 +4 -2 = +1; Brainstorm nets 0 and stays Filter; a REPEATABLE
"draw two, then discard or put back one" is this tag, 4 of 6 reviewed, the two others paying a card
or lands); a draw and an investigate (Deduce); investigate four times (Carnage Interpreter: the
family is 8 of 8); a pile exiled with the card put into its owners' hands (Connecting the Dots,
Expert-Level Safe); exile two and choose one at the beginning of a phase, each turn (Fireglass
Mentor, where the one-shots stay Filter: 4 to 5, split by the repeat); a closing "Then draw a card."
(Fisher's Talent); discard your hand, then draw cards equal to a count (Hoarder's Overflow); a mill
that hands back more than one, or again (Leyline Dowser, Nashi, Searcher in the Dark, Ripples of
Undeath, Greenhouse // Rickety Gazebo: 9 to 15, split by the repeat); an ETB sacrifice that draws
(Selfcraft Mechan); an additional beginning phase (Shadow of the Second Sun); reveal-until into your
hand on every attack (Songbirds' Blessing); an attack that investigates (The Seventh Doctor);
cascade granted beside mana (Bigger on the Inside; the Vehicles, Smoldering Stagecoach and TARDIS,
stay out); gravestorm and a Clue (Follow the Bodies); "that many" exiled by its controller and
played (Expedited Inheritance); a combat trigger closing on a draw (Éowyn, Shieldmaiden; Galadriel,
Elven-Queen); the copies Arcane Bombardment casts; a Token card that draws on entering (Sicarian
Infiltrator, 1 of 1 — WITHDRAWN 2026-10-23: it read the token printing the store held until the
set-aware re-key of 2026-10-20, and a printed token now carries no tags at all, see `CardEffect`).
Left as known disagreements: Lich, Innocent Bystander.

Ruled 2026-10-23 (the CardAdvantage pass, 1181 reviewed: 17 proposed wrongly and 24 missed before, 2
and 5 after, 2 and 1 once the named cards were corrected): CASCADE is one card, once, like a
discover ("No CA, come discover"). The human had untagged 11 of the 14 reviewed cards that cascade
once, and the three left (Forceful Denial, Sakashima's Protege, Heralds of Tzeentch) were corrected
by name; it had been tagged by accident, the cascade reminder read as a second hand. Cascade that
comes round again stays this tag: printed more than once (Apex Devastator), beside a second cast of
the card (Into the Time Vortex, Bloodbraid Challenger), handed to your spells (Abaddon the
Despoiler, Flamekin Herald, Bigger on the Inside), or the same exile-until on every legendary spell
(Jodah, the Unifier); "this spell has cascade as long as" is once (Bloodbraid Marauder). Diviner's
Lockbox, which names a card and draws three when the top one matches, is not this tag: it is
difficulty, like the other guessed draws ("No CA, è difficoltà"). Forging the Anchor, kept as
CardAdvantage and Filter, is CardAdvantage alone by the ruling of 2026-09-27 ("Solo CA").

Read the same day — this tag: a self-sacrifice for X cards or for "you draw two" (All-Fates Scroll,
Infernal Idol: 8 of 10 reviewed, the two others handing a card back, Conch Horn and Starting
Column); "for each {U}{U} spent" (Bladecoil Serpent); the top card of each opponent's library
(Breeches, Brazen Plunderer); a cantrip that splices (Everdream); an impulse on entering and on
leaving (Experimental Synthesizer); every card of a card type kept from a look (Forging the Anchor,
where Rat cards stay Filter: Karumonix); a graveyard card on every hit (Ghost of Ramirez DePietro);
a draw the spell repeats (Invoke Despair); four exiled every upkeep with one played (Journey to the
Lost City); each player drawing on a repeating trigger (Master of Ceremonies); squad copies that
each draw (Sicarian Infiltrator, the real card this time); a mill handed back by an activation
(Sivriss, Nightmare Speaker); a land card paid for a draw by a card that brings lands back (Soul of
Windgrace); a card off the top per target, all playable (Soulfire Eruption); a token that taps to
draw (Tamiyo, Compleated Sage); up to two kept from a look (The Mana Rig); a death draw that encore
repeats (Trove Tracker); the same mode chosen for a target player (Unite the Coalition); a loyalty
reveal-until into the hand (Vivien, Nature's Avenger); a life cost before a land's draw (War Room).
Not this tag: a curse watching the enchanted player draw (Curse of Fool's Wisdom); a tutor into
exile cast from there (Evolving Door); ANY target spell exiled and cast, which is Steal (Gale's
Redirection); a draw an opponent pays for to free their creature (Hold for Ransom); an impulse only
a Goblin may be cast from (Rundvelt Hordemaster, the tribal ruling of 2026-10-16), where an Otter
spell that triggers it (Harnesser of Storms) or a Hero card that pumps (Daredevil) is no tribe.
Measured and rejected: a repeated MANIFEST of the top card as this tag, 1 tagged of 9 (Cryptic
Pursuit against Abhorrent Oculus and Hauntwoods Shrieker). Left as known disagreements: Lich,
Innocent Bystander, Cryptic Pursuit.
