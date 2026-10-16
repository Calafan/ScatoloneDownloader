# Regrowth

Canonical record of the rulings for `CardEffect.Regrowth`. The one-line tooltip shown in the tagger
is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

a card from a graveyard back to HAND — the sibling of Reanimate, same origin, different destination.

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
are not.   // return a card from a graveyard to HAND (Regrowth, Raise Dead, Eternal Witness).
Deliberately NOT merged with Reanimate, which puts it straight onto the battlefield: one hands back
a card you still have to pay for, the other cheats the cost — different speeds, different decks.
