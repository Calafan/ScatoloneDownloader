# ManaFixing

Canonical record of the rulings for `CardEffect.ManaFixing`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

mana colour fixing (dual/fetch lands, multicolour rocks) — distinct from Ramp; a card can be both.

Two shapes added 2026-09-15 that never say "or" on one line: TYPECYCLING, which fetches the colour
you are short of, and a land with TWO mana abilities in different colours (Bleachbone Verge and the
whole modern cycle). One shape taken out: mana you may ONLY SPEND on one thing is not fixing, it
buys the card the designer had in mind — judged per line, so a card with one restricted ability and
one free one still counts, and never asked of a LAND, ruled 2026-10-13 (Great Hall of the Citadel;
the restricted lands are this tag by hand 8 of 8, the restricted non-lands 0 of 8). "Two mana in any
combination of colors" is read since the same day (Great Hall, Wizard's Rockets).

A SEARCH FOR A LAND was measured and rejected once, then split by DESTINATION on 2026-09-18 and half
of it came back: fetched to HAND it fixes the colour you are short of and ramps nothing, because the
land still has to be played off your one land drop (tagged on 28 of 30 reviewed cards); put straight
onto the BATTLEFIELD it is the extra mana rather than the colour, and stays Ramp (8 of 51). Asked
undivided the family is 42% and reads like noise, which is what the first measurement saw. The other
ruling of that day cuts the opposite way: mana you get for NOTHING BUT A TAP is a mana SOURCE, so
the card is Ramp and the colour is a property of the source rather than a service — Birds of
Paradise, Mox Jasper, Solar Array are Ramp alone (a bare {T} for any colour was tagged on 2 of 28).

So is mana a TRIGGER adds for nothing, ruled 2026-10-13 ("free any colour = source"): Nissa,
Resurgent Animist is Ramp alone, Muerra was untagged by hand, and Outcaster Trailblazer, tagged on
09-23, went back for review. Pay MANA on top and it converts colour instead of making it, which IS
the job: Celestial Prism, Mana Prism, Standing Stones.

Until 2026-09-29 any price on top converted (a life, the tap of another permanent — Gene Pollinator,
11 of 12 then); ruled that day, "solo il mana converte": a life, energy, a sacrifice, the tap of
other permanents or a mill leave the card a mana SOURCE, Ramp and not this tag, as the human had
tagged Haunted Screen, Solar Transformer, Lotus Ring, Supportive Parents and The Cabbage Merchant —
Gene Pollinator, Baylen, Dragonbroods' Relic and Lion's Eye Diamond went back for review. Mana paid
one line up still counts (Diamond Kaleidoscope's {3} Prism), and ONE mana of any colour GRANTED TO
LANDS is a land's colour and fixes (Dune Chanter, Greenhouse, Preston Garvey), while three of one
colour is Ramp (Tectonic Split).

A LAND THAT FETCHES A LAND onto the battlefield — the fetch lands, sacrificing themselves or paying
for it (Evolving Wilds, the Landscapes, Thawing Glaciers) — is this tag: 18 of 18 reviewed, read
from 2026-09-29. So is a land of the colour or basic land type CHOSEN as it enters (Mirage Mesa,
Night Market, Multiversal Passage — 6 of 6), and a land whose one tap adds TWO DIFFERENT colours
(the ten Ravnica Karoos, Azorius Chancery …, ruled 2026-10-13 once the Karoos lost Ramp; Guildless
Commons and the one-colour Karoos are just lands). A permission to SPEND MANA AS THOUGH it were any
type, riding on what the card does, is not (Abstruse Appropriation, Outrageous Robbery, Case File
Auditor untagged; Ixhel, tagged on 2026-09-29, handed back), unless it is the whole activation
(North Star).

A LAND is exempt from that and never asked, because a land makes no mana you did not already have
and the colour is the only thing it gives you (17 of 17). Two more added the same day: EVERY LAND
FLAVOUR OF CYCLING, where "basic landcycling" alone is printed on 125 cards — more than all five
named types together — while slivercycling and wizardcycling fetch a body and stay out; and the
TREASURE, which is a Lotus Petal in token form and fixes however many you get, the count deciding
only whether it also ramps — but a Treasure a TOKEN the card creates will make is the token's and
neither fixes nor ramps for the card, ruled 2026-09-29 on There and Back Again ("non crea Treasure
ma li crea il token che viene creato solo quando muore": Smaug's fourteen), and so the Goblin
Shaman's of Fable of the Mirror-Breaker. And the COLOUR FILTER, the mirror of the Ramp ruling: you
feed it mana and it hands back a colour you did not have. Generic in and a colour out always counts
(Farrelite Priest "{1}: Add {W}", Sea Scryer, Coal Golem); a colour in counts only when a DIFFERENT
colour comes out (Fire Sprites "{G}, {T}: Add {R}", Agent of Stromgald), because Evendo pays green
for more green and that is Ramp. Colourless output fixes nothing. Named in full: bare "Fixing" read
as "fixing what?" (mana or hand), and the tagger shows this name verbatim. Old "Fixing" tags still
parse via the alias table.

A LAND KEPT out of a few milled, revealed or looked at is this tag and not Filter, ruled 2026-09-27
— "in caso di terre è ManaFix" — the land fetched to hand in another coat (Ainok Wayfarer,
Ostrich-Horse, Town Greeter, Satyr Wayfinder).

Read 2026-10-15: the Capenna lands that sacrifice themselves as they enter and fetch (Brokers
Hideout); a chosen colour beside a fixed one, and the colours of what a land exiled (Citadel Gate,
Pit of Offerings); lands made EVERY basic land type (Leyline of the Guildpact, Energybending, Omo —
"the basic land type of your choice" is split 1 to 1, Mystic Compass against Jinx, and is not read);
Renewal's one land for one; a card TYPED Treasure (Buried Treasure, Courier's Briefcase); a basic
land put on top (Silver Deputy); a choice of colours granted to a target land (Spara's
Adjudicators). Not a Treasure handed to somebody else (An Offer You Can't Refuse, Erestor of the
Council, Blooming Blast's gift), nor the free extra mana of an enchanted land, a source (Blighted
Burgeoning, Buried in the Garden), nor free mana handed to "target player" (Bigger on the Inside,
The Warring Triad).
