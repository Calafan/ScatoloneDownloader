# Filter

Canonical record of the rulings for `CardEffect.Filter`. The one-line tooltip shown in the tagger is
in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

selection at NO net gain: you see more than you had to and the card count comes out level.

Two shapes, ruled 2026-09-19: LOOK AT N AND TAKE ONE (scry, surveil, look at the top three and keep
one), and HAND BACK WHAT YOU DREW (discard two draw two, and every rummage — "you may discard a
card. If you do, draw a card"). The second was the tag's biggest hole: the rummage is the plainest
Filter shape printed and the classifier had never read a single one of the 14 cards that carry it,
12 of which were already tagged by hand. The boundary with CardAdvantage is COUNTED, but the two
tags are NOT EXCLUSIVE (OVERTURNED 2026-09-27 — they are, see the end), ruled 2026-09-19 after a
first cut of the count got that wrong: a card that selects AND comes out ahead carries both, because
it really did both — Casting of Bones draws three and keeps two, which is a card gained and a choice
made. Nothing ever withdraws this tag. Measured before the count was ruled: of the 41 reviewed cards
that net zero, 38 were tagged Filter and 3 advantage. Anything whose count cannot be parsed stays
parity and therefore stays here. Two things that look like this and are NOT: a card that pays a card
as a COST to do something else (cycling, madness, blitz) — the discard is the price, not the
selection — and looking at an OPPONENT'S library, which is only 2 of 8 reviewed cards and was
measured and rejected (OVERTURNED 2026-09-27 for a top that is rearranged or trimmed, see the end).

SCRY AND SURVEIL ARE THIS TAG UNCONDITIONALLY (OVERTURNED 2026-09-27 for one made once in passing,
see the end), ruled 2026-09-19: it does not matter how small the number is or what else the card
does, so a surveil 1 tacked onto a bounce spell counts. That was measured before it was ruled and
the opposite theory — that a small one is a mere rider — distinguishes nothing, because Filter is
tagged on 88 of the ~103 reviewed cards that scry or surveil whether or not the card does something
else.

Read across every printed form, which "scry \d" was not: 17 cards say "scry X", 6 say "surveil X",
and a handful say "scries" or "surveils" because the subject is a player. The count is required so
that "whenever you scry OR surveil" — a trigger that watches one happen — stays out, the same
reading that keeps "whenever you draw a card" out of CardAdvantage.

EMPOWER JACE N joins them, ruled 2026-09-19: the Jace planeswalker token it makes exists to "[-1]:
Surveil 1" and "[-3]: Draw a card", so the keyword filters by itself. 35 cards print it and 31 carry
reminder text the surveil rule already read; the rule is for the other four. The token is a
PLANESWALKER, so it earns nothing from Tokens.

Looking at the top few and taking exactly one is still this tag, and from 2026-09-20 it is ALSO
CardAdvantage when the ability repeats — Browse selects and gains, so it carries both, exactly as a
profitable loot does. Two shapes were ruled INTO this tag the same day and out of CardAdvantage:
EXILE SEVERAL AND PLAY ONE OF THEM (Riverwheel Sweep, Heroes' Hangout, Case of the Burning Masks),
which is the impulse with a Filter's count, and a repeatable loot that lets you SACRIFICE A
PERMANENT INSTEAD of discarding (Chandra, Spark Hunter, Reckless Detective) — repeatable or not, one
thing goes for one card. And three were ruled OUT, from the nine cards where the classifier said
Filter and the hand did not.

CLOAK AND MANIFEST DREAD turn what you picked FACE DOWN into a 2/2, so what the ability gave you is
a body and not a choice: Curator Beastie and Hide in Plain Sight are Tokens alone. The keyword is
what names them, not "onto the battlefield", because a card put into play AS ITSELF is still a
choice made — Aang, Gilgamesh, Jet, United Battlefront and Web of Life and Destiny all do that and
all five are tagged here.

A LAND off the top is Ramp (Famished Worldsire, Ignis Scientia), the same rail that makes a land
search Ramp rather than Tutor.

AND A LOOT THAT IS NOT ONE PLAYER'S: "each opponent discards a card AND YOU DRAW a card" is two
players doing two different things, so Jecht, Reluctant Guardian is Discard and CardAdvantage and no
more. Both of the first two vetoes stand down when the SAME LINE also puts a card into your hand,
because then the card really did select — Planar Genesis takes a land if there is one and a CARD if
there is not. The carve-out CardAdvantage already makes for "TARGET player" and "EACH player" holds
here too: you can point Forget at yourself and Flux loots everybody including you, and both keep the
tag.

HIDEAWAY is certified Filter, ruled 2026-09-20 on Clive's Hideaway: you look at four and exile one
you may play later, which is a hand you wait for.

A LOOT SPREAD OVER TWO TRIGGERS is this tag, ruled 2026-09-22: Teferi's Imp discards when it phases
out and draws when it phases back in, and neither line can see the other, so it is asked of the
whole card after everything that could have called it advantage. Two more cards were realigned INTO
this tag the same day and out of CardAdvantage, on the count that was already ruled rather than on
anything new — Laughing Mad and Sazacap's Brew each pay a discard as an ADDITIONAL COST and draw
two, which with the spell itself is exactly level, the reading Grab the Prize has carried since
2026-09-19.

FIVE RULINGS ON 2026-09-27 turned the tag round, taken from the 43 cards where hand and classifier
disagreed and from the human's own 09-23 edits, which had pulled the other way.

THE TWO TAGS ARE EXCLUSIVE — "se ha CardAdvantage non ha Filter": a card that selects and comes out
ahead is advantage alone, so Casting of Bones, Browse, Plan the Heist and every profitable loot lose
this tag, 54 reviewed cards with them; the hand had tagged every such card both up to 09-22, then
moved Meat Locker to advantage alone on 09-23 while Focus the Mind, the same words, still carried
both.

That exclusivity was LIMITED TO ONE ABILITY on 2026-09-29 — "sono due abilità diverse. Es. looter
che se si sacrifica peschi 3": a selection that REPEATS on an ability of its own, beside a separate
one that gains, carries both (Qiqirn Merchant's loot beside its sacrifice for three, Dimir
Strandcatcher, Veronica, Arcade Gannon, Glarb), while a selection inside the ability that draws
(True Identity, Champions from Beyond) or made once beside a draw engine (Case of the Crimson Pulse)
is advantage alone.

A SCRY OR SURVEIL MADE ONCE, BESIDE WHAT THE CARD IS FOR, IS NOT THIS — "non li voglio come Filter,
generano troppo rumore": a creature's or an artifact's enters trigger, a bounce spell's rider. What
keeps the tag is a spell that selects and does nothing else (Opt, Dreams of Laguna) and a scry that
COMES AGAIN (Veteran Guardmouse's valiant, Clandestine Meddler's attacks, both named as slips). 55
reviewed cards, every one tagged under the old ruling.

A LAND keeps it, ruled later the same day — "Surveil e scry land, teniamole. Mi serve come filtro
sulle terre doppie": the tag is how the dual lands that select are told apart, and the 14 surveil
lands the first cut had taken it from were restored as reviewed.

BLOOD follows the same line — "se ripetuti sì se singoli no" — and, being a rummage, is never
advantage: Moonstone Eulogist had been read as a repeatable draw off its reminder.

SOMEBODY ELSE'S TOP, REARRANGED OR TRIMMED, IS THIS — "sì, guardare e basta no": Natural Selection
and Elemental Augury print the same words and were tagged both ways, while Orcish Spy only looks.

MILLING OR REVEALING A FEW AND KEEPING ONE is the look-at-N shape — "sì, ma in caso di terre è
ManaFix": a LAND kept that way is ManaFixing instead (Ainok Wayfarer, Ostrich-Horse, Town Greeter),
and keeping ALL of a kind, or keeping one on every attack, is advantage by the count (Marina
Vendrell, Sludge Titan). Named one card at a time the same day: Highway Robbery (a land may stand in
for the discard), Winds of Change (a wheel), Foresight and Mana Severance (a search that exiles to
thin the library), Scroll Rack and Horrid Shadowspinner are this tag; Pitiless Carnage, Monument to
Endurance and Make Your Own Luck are advantage; Caradora is Tutor alone and Hei Bai keeps this tag
by hand, the classifier reading no rule for it. Perception Bobblehead casts what it looks at for
free and is Cheat, not a choice ("lo mettiamo Cheat"), and the Cheat rules read it from the same
day. The last questions were answered that evening. Arcade Gannon, Case File Auditor, Jace
Reawakened, Squad Rallier, Bucolic Ranch and Resonance Technician, Filter alone by hand, are
CardAdvantage as the advantage rules read them ("correggi"). Thundertrap Trainer, which had both, is
CardAdvantage alone ("solo CA"), because offspring fires its enters trigger twice. Shoreline Looter
and Wonderscape Sage, answered "solo CA" the same way, were put back to Filter an hour later —
"lasciamoli Filter che è lo stato base" — which is what the rules read in "then discard a card
unless", the count Chakra Meditation and Katara were ruled on.

A LOOT tacked onto a spell that does something else is the one-shot rider too — "Refute non ha
Filter, solo counter" — which takes Deny Entry, Broadside Barrage and Transpose with it; a creature
that loots as it enters was not asked about and keeps the tag.

A SPELL'S MODE is an effect of its own and not a rider, ruled that evening after a first answer the
other way: "Kozilek's Command: ho sbagliato io. È una spell, quindi conta l'effetto secco (come
Ponder)" — its "scries X, then draws a card" is a Ponder you can choose, and a Charm's loot mode is
the same (Treva's Charm), so the bullet lines of a spell are never taken for riders.

A scry HANDED to something that repeats it stayed (Unassuming Sage's Sorcerer Role) until
2026-10-13, ruled the other way: a Sorcerer Role's scry is the Role's (Unassuming Sage, Spellbook
Vendor, Living Lectern untagged; Splashy Spellcaster handed back). A planeswalker's "draw, then
discard unless" is this tag AND CardAdvantage (Kaito Shizuki, Teferi Akosa, Tezzeret, Betrayer of
Flesh, 2026-10-13). A scry or surveil inside the ability that draws stays CardAdvantage alone
(Mirror of Galadriel, Serum Sovereign, Saheeli — measured 2 of 18 the other way and ruled "solo
CA"); Rumor Gatherer, Sword of Once and Future and Kellan, Daring Traveler keep both as named cards.

EXPLORE is this tag and not CardAdvantage, ruled 2026-09-29 ("explore = Filter") on Subterranean
Schooner, which the human moved from CardAdvantage: a land into your hand or a counter and the
choice to bin the top card is selection.

Read like a scry — made once beside what the card does it is noise (Merfolk Branchwalker), and a
payoff that watches others explore selects nothing (Wildgrowth Walker).

The 2026-10-14 pass read the sitting's shapes: mill N and keep one "from among the MILLED CARDS"
(Seed of Hope, Wrenn and Realmbreaker's −2; a land kept that way is still ManaFixing, Patient
Naturalist), a card CHOSEN from among the revealed (Green Sun's Twilight), a WHEEL of your own hand
through the graveyard (Decaying Time Loop, Path of the Pyromancer — not Shocker's, which is the
opponent's), a MAP token made on every attack like Blood (Sentinel of the Nameless City; one Map
once is a rider), an explore made X TIMES (Jadelight Spelunker), and HIDEAWAY kept beside a card, 5
of 5 by hand (Wiretapping). Not this tag: THAT player's loot (Memory Worm — "target player" can be
you, Forget), and a scry the card exiles itself out of the graveyard for, which runs once (Survivor
of Korlis; one it SACRIFICES itself for still counts, Grim Bauble).

Withdrawn 2026-10-16: the veto that kept a LOOK AND A FREE CAST out of this tag, written from
Perception Bobblehead in September. The 2026-10-13 answer made Kiora Cheat AND Filter, and the
tribal ruling later took only its Cheat, so a look and a free cast is both: Perception Bobblehead is
handed back with Filter, and Aetherworks Marvel, Svella and Genesis Wave, unreviewed, gain it. Read
the same day: a milled card put into your hand (Terra, Magical Adept), one cast free or put onto the
battlefield from among the milled (Summons of Saruman, Bramble Familiar) and one put onto the
battlefield from among the revealed (Knickknack Ouphe), but not a land alone, which is Ramp
(Shigeki, Jukai Visionary). A connive beside a spell bounced is the rider of a counterspell (Obscura
Interceptor). Handed back without the tag: Thrill-Kill Disciple, Candy Trail and Invasion of
Pyrulea.

Ruled 2026-10-24 (the Filter pass, 488 reviewed: 5 proposed wrongly and 12 missed before, 0 and 7
after the 28 cards the ruling reaches were handed back): A LOOT, A RUMMAGE OR A CONNIVE A PERMANENT
MAKES ONCE is the rider a one-shot scry is — "No Filter, come lo scry singolo" — which answers what
the 09-27 ruling had left open for a creature that loots as it enters. Revel Ruiner and Echo
Inspector were untagged on 10-23 while Raffine's Informant kept the tag on the same words; counted
before the question was put, 6 of 10 reviewed connives on entering carried it, 16 of 18 loots and
6 of 6 rummages, every one tagged before anyone had asked. At any count (Kiora, the Rising Tide
draws two and discards two) and on a Saga's chapter (Founding of Omashu). Only on a trigger that
fires ONCE: a loot on two events (Oracle of Tragedy's "enters or dies"), a loot activated or paid
from the graveyard (Oracle of Dust, Merfolk Pupil), a connive handed over in quotes to come again
(Copycrook, Security Bypass) and an Adventure's spell (Tempest Hart's Scan the Clouds) keep it.

Read the same day: a discard cost that names its card by a word (Chandra's Regulator — one card
only, since Sphinx of the Chimes discards two for four), the revealed put onto the battlefield
without a "may" (Smelting Vat
— not ALL of a kind, Animist's Awakening, nor a RANDOM one, Getaway Barrel), a creature card milled
this way RETURNED to hand (Monster Manual's Zoological Study), and a dig until a card type turns up,
made once, its one card to hand and the rest to the bottom (Artificer Class's level 2, Yuna's
Whistle, 2 of 2 by hand — the same dig again is CardAdvantage, Vivien, Nature's Avenger, and a land
dug for is ManaFixing, House Cartographer). Not this tag: an OPPONENT's loot (Balor, the Memory
Worm reading with the victim named) and a scry only a TRIBE's spell pays out (Lapis Orb of
Dragonkind). Seven stay against the rules, each known: Hei Bai, Kellan, Rumor Gatherer and Sword of
Once and Future by name, Sidequest: Catch a Fish, and Candy Trail and Invasion of Pyrulea, put back
by hand after the 10-16 hand-back — a noncreature's one-shot scry on entering is 1 of 6 tagged
(Lembas, the same Food, is not).
