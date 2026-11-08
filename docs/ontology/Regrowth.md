# Regrowth

Canonical record of the rulings for `CardEffect.Regrowth`. The one-line tooltip shown in the tagger
is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

a card from a graveyard back to HAND — the sibling of Reanimate, same origin, different destination
(Regrowth, Raise Dead, Eternal Witness). Deliberately NOT merged with Reanimate, which puts it
straight onto the battlefield: one hands back a card you still have to pay for, the other cheats the
cost — different speeds, different decks.

The TOP OF A LIBRARY counts too, ruled 2026-09-19: it is a hand you wait one turn for, and all 5
reviewed cards written that way were already tagged.

The BOTTOM is not in it, because putting a card on the bottom of a library is graveyard hate
(Barkform Harvester, Chrome Companion), and neither is an OPPONENT'S graveyard, which is
Misinformation shuffling their answers back rather than rebuying your own.

CASTING ANOTHER CARD out of a graveyard IS this, ruled later the same day: it is reanimation for
spells — the card never reaches your hand, but it is bought back from the same place and for the
same reason (Edgar, Festival of Embers, Noctis, Iroh handing every card in the yard flashback). The
card that gives ITSELF a second cast is NOT, and that split is what rescued a family which first
measured at 10% and looked like noise: read apart, flashback and its cousins are 54 reviewed cards
of which exactly ONE carries this tag — Sorceress's Schemes, which earns it on a different line by
returning a card to hand — while the aimed version is 12 cards of which 5 were already tagged. Three
shapes read like a cast and are not: cost reduction COUNTING cards in the yard ("costs {1} less for
each creature card in your graveyard"), a card exiled from there as an additional COST (Haunting
Misery), and a trigger that only WATCHES a cast happen (Neerdiv), the same shape that keeps
"whenever you draw a card" out of CardAdvantage.

Ruled 2026-10-13: a card returning ITSELF to hand is NOT this either ("Mai Regrowth (come
Reanimate)": Hammer of Bogardan, Ringwraiths, Furious Forebear and 13 more reached and handed back);
a card EXILED out of your graveyard and then cast or played IS ("Sì": Kaervek, Arcane Bombardment,
Tersa; Shiko, Paragon of the Way the named exception), and so is a cast card DESCRIBED before "from"
("Regrowth sempre": Seifer Almasy, Gale, Sword of Once and Future). A return that is the PRICE of
keeping the card (Harvest Wurm) and a keyword's reminder (Deeproot Historian's "cards with retrace")
are not.

Ruled 2026-10-16 (G1, "Solo Steal"): a card cast from an OPPONENT'S graveyard is NOT this tag but
Steal alone (Deluxe Dragster named, Tinybones, Bauble Burglar realigned; no Steal either since
2026-11-08, CardAdvantage at most, Steal.md); from ANY graveyard it
still is, since yours is among them (C4, Urabrask // The Great Work). Read the same day: a target
card in your graveyard you may then cast (Conduit of Worlds), one put into your hand two sentences
later (Cosmic Rebirth), the top card of your graveyard put on top of your library (Guiding Spirit)
and flashback granted to the instants and sorceries in graveyards (Otaria). Handed back without the
tag, the rules reading none: Dutiful Knowledge Seeker, Paradigm Shift, Dredger's Insight,
Undergrowth Recon and Heartless Conscription; Angelic Renewal moves to Reanimate.

Ruled 2026-10-30 (Anikthea, Hand of Erebos, kept Regrowth by hand): a token copy of a NONCREATURE
card exiled from your graveyard is Regrowth. A creature card's copy stays Reanimate (Ardyn, Mordor
on the March, Abyssal Harvester), and so does an "artifact or creature" copy (Tawnos, Lorehold
Archivist). Read the same day: a card of yours put into the graveyard from the battlefield, exiled
and then cast (Mazzy, Truesword Paladin's Auras, 1 of 1).

Read 2026-11-06, the sitting of 11-03..05, each tagged by hand and read under the rulings above: a
card CHOSEN in your graveyard and returned by a d20 table row, "then roll a d20 … 10—20 | Return
that card to your hand" (Aberrant Mind Sorcerer, 1 of 1); an any-graveyard exile whose copy is cast
in a sentence of its own, "Exile target instant or sorcery card from a graveyard. Copy that card …
You may cast the copy" (Mnemonic Deluge, Wizard's Spellbook, 2 of 2; Reenact the Crime says "in a
graveyard" and stays untagged); and the permission to play granted in quotes to the cards in your
graveyard, as Otaria grants flashback (Kethis, the Hidden Hand, 1 of 1). Not this: an exile paid as
a COST in front of a colon, whose Adventure reminder "You may cast the artifact later from exile"
stood for a cast (Altar of Bhaal // Bone Offering returns a creature to the battlefield, so it is
Reanimate), and a draw that only WATCHES a cast, "if you've cast a spell from a graveyard"
(Laboratory Drudge), the shape of Neerdiv.

RULED 2026-11-06, "Tribale non è Regrowth" (asked on Ashcoat of the Shadow Swarm against the 8
returns the hand had tagged): a return to HAND limited to a creature TYPE is NOT this tag, whether
it names one tribe (Elderfang Ritualist, Mirkwood Elk: Elf; Tombstone: Villain; Spawnbed Protector:
Eldrazi; Mtenda Griffin; Gallifrey Stands: Doctor), a list of them (Mudflat Village's "Bat, Lizard,
Rat, or Squirrel") or Soulshift's reminder (Spirit). It is the same TribalCard that keeps a tribal
return to the BATTLEFIELD out of Reanimate, applied to the sentences that put a card into the hand;
the tribal return to the battlefield stays Reanimate, untouched. A card type, a supertype and the
like is no tribe (Aura, Equipment, Vehicle, Saga: Satsuki, Dogmeat, Imperial Recovery Unit stay),
and the tag stays when another Regrowth stands on the card (Kami of Restless Shadows puts a creature
card on top of its library in its other mode). Cast from a graveyard is not touched (Zul Ashur,
Jacob Frye: asked as a question). 7 reviewed cards lose the tag and are handed back.
