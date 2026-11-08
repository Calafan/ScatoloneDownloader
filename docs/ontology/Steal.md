# Steal

Canonical record of the rulings for `CardEffect.Steal`. The one-line tooltip shown in the tagger is
in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

Somebody else's PERMANENT taken: you gain control of a permanent another player controls, for a
turn (Ray of Command, Act of Treason) or for good (Control Magic is the card), an EXCHANGE of two
included (Juxtapose, Political Trickery), and a permanent of theirs exiled and then yours (Abstruse
Appropriation, Sothera). A CARD of theirs played out of their library, hand or graveyard, or off the
stack, is CardAdvantage at most and never this tag. The rest follows from the sentence: their
graveyard is Reanimate (a card in it is no permanent), Bribery is Cheat, a swap of life totals is
Burn, an Aura moved changes nobody's control, what you OWN taken back is nobody's, and giving a
permanent AWAY is the mirror image. Two shapes are kept beside the permanent, each with its why: the
PLAYER controlled for a turn (Mindslaver: for that turn every permanent of theirs is yours to run),
and a countered spell put onto the battlefield under your control (Desertion: it ends as a permanent
of theirs on your side). Redefined 2026-11-08, the last rulings below; every earlier paragraph
marked "overturned" is history, and no card is kept in or out of this tag by name.

(Overturned 2026-11-08 except the exchange.) An EXCHANGE is a theft you paid for: Juxtapose,
Legerdemain, Political Trickery, Trade the Helm, Gauntlets of Chaos. Word of Command takes the
PLAYER. And a card taken out of somebody else's LIBRARY, HAND or GRAVEYARD and played is a theft of
a card rather than of a permanent — Outrageous Robbery, Apple of Eden, Laughing Jasper Flint, Rev,
Crabomination, Azula. Playing YOUR OWN exiled card is not: Triple Triad and Wheel of Potential say
"cards you own" after a symmetric exile, and an extra land drop or a second cast for the card itself
belong to Ramp and Regrowth.

GIVING A PERMANENT AWAY is the mirror image of this tag and was 12 of its 14 false positives —
Jinxed Idol, Rainbow Vale, Chaos Lord and Stiltzkin pay it as a price, Rohgahh and Emberwilde Djinn
suffer it for missing an upkeep, Guardian Beast negates it. The subject sits immediately in front of
the verb, because "untap target creature AN OPPONENT CONTROLS and gain control of it" (Ray of
Command, Magus of the Unseen) puts the victim in front of a verb that is yours. (Overturned
2026-11-08, Reanimate alone since.) Reanimating out of an OPPONENT'S graveyard is both Reanimate and
this (Ashen Powder, Bone Dancer, Chorale of the Void, Fire Nation Salvagers), but ordinary
reanimation names no victim — Reanimate, Hymn of Rebirth and Coffin Queen say "from A graveyard"
and stay Reanimate alone.

(Overturned 2026-11-08.) "THEY may play" counts, but only against a NAMED zone: Gonti exiles from
"that OPPONENT'S library" and hands the card to the creature's controller, while Transforming
Flourish has one player exile from "THEIR library" and then cast it, which is nobody robbing
anybody.

(Overturned 2026-11-08 for the mill and the Aura.) A MILL is a zone too, and the only way Locke
could ever be found — "each player mills a card … you may cast a spell from among those cards" names
no library at all. Moving somebody else's AURA onto a permanent of your choosing is theft of its job
if not of its control (Enchantment Alteration, Crown of the Ages). Two shapes were ruled OUT the same
day: a token copy of their creature is a body you made rather than a card you took (Echo Chamber,
which is Tokens), and a card pulled at random out of "that graveyard" names no victim (Mysterious
Stranger).

Ruled 2026-09-19; 42 wrong out of 5,037 reviewed cards down to 2, both of them realignments still to
apply.

Read again 2026-10-14 against the sitting's false positives, none of them tagged Steal (the plays
are history since 2026-11-08): the play is read outside reminder text (an Adventure's "you may cast
the creature later", Cruel Somnophage) and one MODE at a time (Riveteers Charm); an opponent named
only as the TRIGGER for playing your own library is no victim (Virtue of Courage, Fireglass Mentor,
Powerbalance), nor is one who exiles "their library" and plays it themselves (Urabrask); a mill
pairs only with YOU playing the card, not with reanimating from any graveyard (Path of the Schemer,
Nezumi Freewheeler); a LAND out of their graveyard is Ramp (Realmbreaker); "the attacking player"
and "the player to your right" are donations (Contested Game Ball, Hithlain Rope); a TOKEN that
steals is the token's (Wake the Dragon); and moving an Aura YOU control is your own (Codsworth).
Steal F1 94.9 to 98.8.

(Overturned 2026-11-08: no Steal either, CardAdvantage at most.) Ruled 2026-10-16 (G1, "Solo
Steal"): casting a card from an OPPONENT'S graveyard is this tag alone, never Regrowth. Deluxe
Dragster was corrected by name, Tinybones, Bauble Burglar follows, and four unreviewed cards lost the
Regrowth with it (Diluvian Primordial, Whispersteel Dagger, Wrexial, Shaman's Trance). Read the same
day, and still standing: a permanent of theirs exiled and then cast by you (Abstruse Appropriation),
which a CARD exiled from a graveyard is not (Savvy Trader); and a card cast "from among cards IN
your graveyard" is yours (Raul, Trouble Shooter).

Read 2026-11-03: "AS LONG AS you control enchanted creature" is a condition on the Aura's own
creature, not a theft (Mishra's Domination, Buff alone by hand; the one card worded so).

REDEFINED 2026-11-08 ("Sì, solo permanenti"), from the human's proposal of 2026-11-07: Steal is a
PERMANENT taken, for a turn (Ray of Command) or for good (Control Magic). Measured before the code
on the 234 cards tagged Steal by hand: 120 kept (105 "gain or exchange control", 13 "you control
enchanted", 2 permanents exiled and then yours), 114 taking something else, none added. Six
questions followed, one at a time.

RULED 2026-11-08, Q1 "Cheat": Bribery's creature card searched out of an OPPONENT'S library is
Cheat's chosen card with its cost skipped, as Natural Order (Inevitable Betrayal named, Bribery
unreviewed). A card off the TOP of their library stays out of Cheat as off any library (Fishing
Gear, Curse of Unbinding, Sméagol, Helpful Guide).

RULED 2026-11-08, Q2 "Solo Reanimate, anche i 4": reanimating out of an opponent's graveyard is
Reanimate alone, whose graveyard it is no longer matters. The pair was Necromancy and Coffin Queen
(Reanimate alone) against Ashen Powder and Geth's Summons (Reanimate and Steal); 17 cards lost
Steal. Breach the Multiverse, Captain N'ghathrod, Helm of Obedience and Lorcan, Warlock Collector
gained Reanimate (named), and Colossal Grave-Reaver with them (its own library milled, reviewed
untagged on 2026-09-10, reached and handed back).

RULED 2026-11-08, Q3 "Burn": an exchange of life totals is life the opponent loses (Mirror
Universe, Mister Negative, Psychic Transfer, Profane Transfusion, all named).

RULED 2026-11-08, Q4 "Steal, eccezione col perché": the PLAYER controlled during their next turn or
combat stays (Mindslaver, Worst Fears, Secret of Bloodbending, The Dominion Bracelet, Sorin Markov,
Construct a Cosmic Cube), because for that turn every permanent of theirs is yours to run. Word of
Command controls them only to play a card of theirs, and lost the tag (named).

RULED 2026-11-08, Q5 "Nessun tag": somebody else's Aura moved (Enchantment Alteration, Crown of the
Ages, named) changes nobody's control and earns no tag.

RULED 2026-11-08, Q6 "-Steal, Desertion resta": their spell exiled and then cast (Smirking
Spelljacker, Gale's Redirection, named) is a card of theirs played; Desertion keeps the tag, the
countered creature or artifact ending as a permanent of theirs under your control.

Read 2026-11-08 under the definition: what you OWN taken back is nobody's (Coveted Falcon's "target
permanent you own but don't control", CardAdvantage alone by hand; The Fall of Lord Konda's "each
player gains control of all permanents they own", Removal alone; Homeward Path, Brand and Trostani
Discordant unreviewed); "an opponent OF YOUR CHOICE gains control of it" is a donation (Khârn the
Betrayer, CardAdvantage alone). Applied: 112 hand-tag changes, 111 of them Steal removed; 17 cards
named, 95 reached and handed back, the 78 cards of theirs played among them, 11 of which are left
with no tag (Opposition Agent, Zara, Renegade Recruiter and nine more). The rules that read the card
of theirs (TakesSomebodyElsesCard and its ten regexes) were deleted. The CardAdvantage rulings that
sent such a card here ("is Steal and not this") keep their outcome, no CardAdvantage. Steal on the
reviewed cards went from FP 3 FN 2 on 234 to 0 and 0 on 123.
