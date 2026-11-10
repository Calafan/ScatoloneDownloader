# ManaFixing

Canonical record of the rulings for `CardEffect.ManaFixing`. The one-line tooltip shown in the
tagger is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-10** ("Colore, non quantità"): the card CONVERTS what you already have — the
land drop, a land, a card, mana you pay — into the COLOUR you lack, without more mana: a land of
several colours or one that fetches a land, a land searched to HAND (or to the top) and every
landcycling, mana PAID that comes back in another colour (Signets, Chromatic Sphere, the filters),
a Treasure of yours, a basic land type or a colour given to lands. Evolving Wilds is the card. Out
as consequences, not exceptions: mana of any colour for nothing but a tap, or for a price that is
not mana (a life, a sacrifice, energy, a trigger), on a non-land is a mana SOURCE, Ramp alone (Birds
of Paradise, Lotus Petal); a permission riding on what the card does; types that REPLACE instead of
adding (Blood Moon). Kept as exceptions with their reasons, ruled below: the Treasure, a sacrifice
for any colour that is colour and not quantity (Ramp, Q1 of 2026-11-09); restricted mana on a land;
lands are never asked whether their mana is free. It held on 657 of 659 reviewed cards before the
answers of the same day (FP 1, FN 2: Grell Philosopher, Druid of the Emerald Grove, Yavimaya).

The rulings below are the history; each one is consistent with the definition or kept as an
exception with its reason.

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
"the basic land type of your choice" is split 1 to 1, Mystic Compass against Jinx, and is not read;
overturned 2026-10-16, below); Renewal's one land for one; a card TYPED Treasure (Buried Treasure,
Courier's Briefcase); a basic land put on top (Silver Deputy); a choice of colours granted to a
target land (Spara's Adjudicators). Not a Treasure handed to somebody else (An Offer You Can't
Refuse, Erestor of the Council, Blooming Blast's gift), nor the free extra mana of an enchanted
land, a source (Blighted Burgeoning, Buried in the Garden), nor free mana handed to "target player"
(Bigger on the Inside, The Warring Triad).

Ruled 2026-10-16 (M1, "MF a entrambe"): a target land made the basic land type of your choice is
this tag, Mystic Compass and Jinx alike; Jinx is handed back with it. Read the same day: lands
searched, revealed and put into your hand (Aang's Journey; the 2026-10-15 rejection of that shape
revisited, Analyze the Pollen staying out through "that card"), which moves Gift of Estates and
Tithe here from Ramp, and a reveal until a land card goes to hand (House Cartographer). Not this: a
Treasure maker lent for a turn (Furnace Reins), a source adding the colour its cost already paid
(Sunken Palace), and one mana of any colour only when the card returned is green (Tamiyo,
Inquisitive Student). Handed back: Land Tax with the tag; Crystal Vein, Metamorphosis, All-Fates
Scroll, Fellwar Stone, Candelabra of Tawnos and Hall of Gemstone without it.

Read 2026-10-30: "add two mana of different colors" (Interplanar Beacon, Guild Globe; 2 of 2).

Ruled 2026-11-05 (Stoic Farmer, named, +ManaFixing): a land searched to HAND is this tag on the
other branch of a condition too, "search your library for a Plains card and reveal it. If an
opponent controls more lands than you, put it onto the battlefield tapped. Otherwise, put it into
your hand", a basic land or a "Plains card" alike. Scholar of New Horizons says the same, Ruin in
Their Wake is unreviewed; all three are Ramp as well. "A creature or land card" stays out
(Archdruid's Charm), and so does Boreas Charger's split, one onto the battlefield and the rest into
the hand, unreviewed and a family of one (overturned 2026-11-10, Q1 below).

Ruled 2026-11-10, Q1 ("Ramp+MF a tutte"): the CULTIVATE split, "put one onto the battlefield
tapped and the other into your hand", is Ramp for the land in play and this tag for the land in
hand — "the rest" (Boreas Charger, Nissa's Pilgrimage) and "two of them onto the battlefield"
(Viewpoint Synchronization, Verdant Mastery) alike, and a basic Forest only (Bloomvine Regent) as
well, as Forestcycling is. The reviewed cards were 1 of 6 before it: Druid of the Emerald Grove,
read in its d20 table; Flare of Cultivation, Navigation Orb and Bloomvine Regent named; Flourishing
Bloom-Kin and Viewpoint Synchronization handed back. Only a LAND search: the cards Nissa, Nature's
Artisan sends to hand are the ones that are not lands.

Ruled 2026-11-10, Q2 ("MF a tutte"): ONE basic land type ADDED to every land, "Each land is a Swamp
in addition to its other land types", symmetric or yours alone: Yavimaya, Cradle of Growth
(tagged), Blanket of Night and Swampbenders named, Urborg and Stormtide Leviathan unreviewed. A
type that replaces (Blood Moon's "Nonbasic lands are Mountains", Celestial Dawn's "Lands you
control are Plains") takes colours away and stays out, and so does ONE TARGET land of a fixed type
(Xolatoyac, The Flood of Mars; 0 of 2 reviewed), where the type of your choice is this tag (Jinx).

Read the same day: a permission to spend mana of ONE colour as any riding on what the card does
(Grell Philosopher, Quicksilver Elemental; 0 of 3 reviewed), and the counters a bare tap stores
and a trigger cashes for any colour, a source (Coalition Relic, Ramp alone). The land search to
hand across "where X is …" (Oreskos Explorer, Prismatic Undercurrents) moved with the split.
