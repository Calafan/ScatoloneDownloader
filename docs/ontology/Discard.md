# Discard

Canonical record of the rulings for `CardEffect.Discard`. The one-line tooltip shown in the tagger
is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-10 (the first of three, widened by the human to Timetwister).** An OPPONENT
loses cards from their HAND because of your card: discarded or exiled, also every player at once
(Wheel of Fortune, Liliana of the Veil's +1), a whole hand shuffled away when YOU choose the moment
(Timetwister), a card chosen by YOU even when it goes back on their library or they may still play
it (Painful Memories, Elite Spellbinder), a card they choose but must put back on their library and
draw again, or the toll of a punisher aimed at the opponents alone (Perforating Artist). Out: your
own discard, which is a cost; a choice every player makes, you included ("each player MAY
discard", "discards ANY NUMBER", the symmetric punisher); a card that only watches a discard; a
card they choose and may still play (Lightstall Inquisitor); a hand reset by itself every turn
(Teferi's Puzzle Box). The card is Hymn to Tourach, "Target player discards two cards at random."
Measured before any code, the classifier agreed with all 166 reviewed cards; the definition moved
six of them, by the four rulings of the same day at the end. The paragraphs between are its
readings.

First read 2026-09-15, like Mill, only outward: a bare "discard a card" is nearly always a cost —
madness, blitz, cycling reminder text, the back half of looting — and paying it is not attacking
anyone.

Widened 2026-09-28 for five wordings the hand tags already carried on every reviewed card using
them: the PUNISHER ("loses 3 life unless that player sacrifices a nonland permanent or discards a
card" — Perforating Artist, Starseer Mentor), the chosen card discarded by a PRONOUN ("you choose a
nonland card from it … they discard it" — Binding Negotiation, Leshrac's Sigil), "they discard a
card" once a player is named (Sonic Shrieker), a LIST ("sacrifices a creature, discards a card" —
Scarring Memories), and the NAME SEARCH through a hand (Lobotomy, Ancient Vendetta). Four shapes
name a player beside "discard" and attack no hand, each untagged on every reviewed card: "an
opponent CAUSES YOU TO DISCARD THIS CARD" (Guerrilla Tactics, Sand Golem), a LOOT handed to a
player, which is Filter (Depth Defiler, Cait), a CONDITION that only watches a discard (Lo and Li —
so Megrim and Waste Not lose the tag too, confirmed by the human the same day: "questi non sono
Discard"), and an ALTERNATIVE COST (Dream Halls). Six rulings the same day. "Each player MAY
discard" is a choice everybody makes and is NOT this (Mind Bomb, Snort, Mog; The Death of Gwen
Stacy's hand tag corrected), while the mandatory "each player discards" of Wheel of Fortune stays.

Confirmed 2026-09-29 for the punisher, which splits on WHO chooses: offered to the OPPONENTS it is
this tag, however worded ("each opponent may sacrifice … or discard a card" — Osseous Sticktwister,
as Perforating Artist), offered to EACH PLAYER it follows the symmetric choice and is not (Tomb of
Annihilation, Possessed Portal, Doom Foretold). A card EXILED from their hand IS (Unscrupulous Agent
as Ruthless Negotiation, Crabomination, Karn Liberated), unless they chose it or lost it at random
and may still play it (Lightstall Inquisitor, Elkin Lair) — the card YOU chose from a hand you
looked at is this tag even so, ruled 2026-10-15 (Invasion of Gobakhan, Elite Spellbinder); so is a
card chosen and put back ON THEIR LIBRARY (Painful Memories as Agonizing Memories), the whole hand
looked at and EXILED (Apple of Eden, "come Wheel of Fortune e sorelle"), and a symmetric REPLACEMENT
of the draw (Chains of Mephistopheles, Breathstealer's Crypt). A discard of YOUR OWN is a cost, and
the four hand tags that said otherwise were ruled errors (Professor Zei, Sabin, Stormbind, Summon:
G.F. Ifrit); Nicol Bolas's missing tag was a slip.

Read 2026-11-10, from the second sitting. The exile from their hand written as YOURS is the same
attack: Elder Brain's "exile all cards from that player's hand, then they draw that many cards" and
Thought Distortion's "Exile all noncreature, nonland cards from that player's hand and graveyard"
(Discard by hand, 3 of 3 reviewed with Isildur's Fateful Strike); that YOU may play them afterwards
changes nothing, only the owner's "may play" keeps a card out (Lightstall Inquisitor). And a NAME
SEARCH through their hand that starts from a card in a GRAVEYARD is not this tag: Kotose, the Silent
Spider's "exile target card … from an opponent's graveyard. Search that player's graveyard, hand,
and library for any number of cards with the same name" is untagged, as Deadly Cover-Up (Wipe
alone); Lobotomy's name chosen from their hand and Ancient Vendetta's named card stay.

RULED 2026-11-10, with the definition ("Ma anche Timetwister o Wheel of Fortune lo sono perché
scegli tu quando farlo"): a hand SHUFFLED into its library is this tag when the card makes every
player, or a player it names, do it, because you choose the moment it hurts. Timetwister by name;
Echo of Eons, Diminishing Returns and Winds of Change ("the cards from their hand … then draws that
many") reached and handed back, 0 of 4 tagged before. Unreviewed and proposed: Time Spiral, Time
Reversal, Day's Undoing, Emergency Powers, Game Plan, Commit // Memory, Temporal Cascade, Sway of
the Stars, Immortus, Molten Psyche, Whirlpool Warrior, and the ultimates of Jace, the Living
Guildpact and Jace, the Mind Sculptor ("that player shuffles their hand"). Out: your own hand
(Weftwalking, Midnight Clock, Lich's Mirror) and "each player MAY shuffle" (Step Between Worlds,
Turtles in Time).

RULED 2026-11-10, Q1 ("No, non scegli quando"): Teferi's Puzzle Box, which puts every hand on the
bottom of its library at every draw step by itself, stays untagged — nobody chooses when, it is a
rule of the table. Chains of Mephistopheles and Breathstealer's Crypt, automatic and symmetric too,
stay this tag because their cards are lost, not shuffled back. Widespread Panic, a card put back on
the library of whoever SHUFFLES, follows Puzzle Box.

RULED 2026-11-10, Q2 ("No, -Discard"): "discards ANY NUMBER of cards, then draws that many" is the
same choice as "each player MAY discard", a loot given away (Filter). Flux loses the tag by name;
Steal the Show ("target player") and Fervent Mastery ("an opponent"), unreviewed, follow. Last Rites
keeps it: its "any number" is your own discard, and "that player discards those cards" the attack.

RULED 2026-11-10, Q3 ("Sì, +Discard"): Jester's Mask by name, "Target opponent puts the cards from
their hand on top of their library. Search that player's library for that many cards": YOU pick
their new hand. Head Games, the same words, follows.

RULED 2026-11-10, Q4 ("Sì, perde una pescata"): a card of their hand PUT BACK on their library,
chosen by its owner, is this tag — it leaves their hand by force and costs them the next draw. No
reviewed card says it; Chittering Rats, Chimney Imp, Volrath's Dungeon, Prying Questions and
Sadistic Augermage ("each player puts a card", when it dies) are proposed.
