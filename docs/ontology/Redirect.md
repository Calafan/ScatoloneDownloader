# Redirect

Canonical record of the rulings for `CardEffect.Redirect`. The one-line tooltip shown in the tagger
is in [`EffectGlossary.cs`](../../ScatoloneDownloader/Mtg/EffectGlossary.cs).

**Definition, 2026-11-09** ("Sì, così"): a spell or ability ALREADY ON THE STACK, anybody's, made
over without being countered: new targets, a new controller, or a copy (aimed at a target spell,
the next one you cast, or each one of a kind you cast). Redirect is the card: "You may choose new
targets for target spell." Out: a card copying only itself, a copy that is always a creature or
another permanent (Tokens), a trigger that "triggers an additional time", and the three exceptions
ruled below with their reasons (an ability copied on every activation, demonstrate, a copy aimed
only at the card itself). It held on 84 reviewed cards (FP 0, FN 4, every miss a wording).

The rulings below are the history; each one is consistent with the definition or kept as an
exception with its reason.

act on a spell already on the stack WITHOUT countering it: change its target (Misdirection,
Deflection) or copy it (Fork, Reverberate, Twincast).

Widened 2026-09-19 for the possessive: Meddle says "change THAT SPELL'S TARGET to another creature"
where Deflection says "change the target of", and no rule reading the second could see the first.

The DELAYED COPY was tried the same day and ruled OUT: "when you next cast an instant or sorcery
spell this turn, copy it … you may choose new targets for the copy" doubles a spell of YOUR OWN
rather than acting on somebody else's on the stack. It matched 8 reviewed cards, 4 tagged and 4 not,
and Ether reads word for word like Jeong Jeong — the ruling took the tag off all eight rather than
keep a rule that could not tell them apart. Storm's reminder text says the same words and was never
this either. What stays is the AIMED copy, "copy TARGET instant or sorcery spell": Fork,
Reverberate, Twincast.

OVERTURNED 2026-09-28, the delayed copy and every other copy of YOUR OWN spell IS this tag: every
such card reviewed since 09-23 had been tagged (Sword of Wealth and Power, Cursed Recording,
Breeches, Jace Reawakened, Leyline of Resonance, Sunken Palace), against the older ones tagged under
the 09-19 ruling (Ether, Adaptive Training Post, Jeong Jeong, Fire Lord Azula, Alania, Taigam,
handed back). A keyword GIVEN to your spells is the same copy — storm (Crackling Spellslinger),
replicate (Djinn Illuminatus), conspire (Wort) — read through its reminder, which says "that spell";
a card copying only ITSELF is not ("when you cast THIS SPELL, copy it": Storm's own reminder,
Mentor's Guidance, Banish into Fable), and nor is a copy counted by commander casts, which is none
in the cube (Thunderclap Drake). A copy of a CREATURE spell becomes a token and is Tokens instead
(Case of the Shifting Visage, Double Down, Thurid, Jackal).

Widened the same day for two more ways to act on a spell: new targets chosen outright (Boltbender)
and a spell taken over (Invert Polarity, which is not Steal — a spell is not a permanent). Recasting
your own exiled spells is not a copy: Forger's Foundry is CardAdvantage, and Kefka, Dancing Mad is
not this. Kept as one tag because both do the same job in a deck — they turn an opponent's spell
into your problem-solver — and because splitting them would give two tags of a few dozen cards each.
Distinct from Counter, which answers by removing the spell.

Read 2026-10-30: a copy of the NEXT instant or sorcery you cast this turn, with new targets
(Tzaangor Shaman; Flamehold Grappler, untagged on 2026-09-11, handed back), and of the next ability
you activate (Dynaheir, Invoker Adept, 2 of 2 with Pit Automaton). A TRIGGER on every activation,
"whenever you activate …, copy that ability", stays out (0 of 10, Leori, Sparktouched Hunter in the
same sitting).

Ruled 2026-11-09 with the definition, Q1 ("Fuori"): an ABILITY copied on EVERY activation or
trigger stays out, an exception kept with its reason: doubling your own abilities each time is a
doubler like Panharmonicon's "triggers an additional time", not a play on the stack. 11 of 12
reviewed were untagged (Rowan's Talent, Leori, Ertha Jo, Ashnod, Verrak, Chandra's Regulator,
Mirror-Shield Hoplite, Aboleth Spawn, Agrus Kos, Firebender Ascension, Ulalek's abilities); The
Enigma Jewel's Locus ("Whenever you activate an ability that isn't a mana ability, copy it") was
the twelfth, ruled -Redirect. The ability AIMED ("copy target activated or triggered ability") and
the NEXT one (Dynaheir, Pit Automaton) stay in.

Ruled the same day, Q2 ("Fuori"): DEMONSTRATE given to your spells is not this. The copy handed to
an opponent as well cancels yours: The Twelfth Doctor stays untagged, unlike storm or replicate
given to your spells (Crackling Spellslinger, Threefold Signal).

Ruled the same day, Q3 ("Fuori, regola di sé"): a copy that can only target the CARD ITSELF is the
self rule every tag keeps (Mistfolk, 2026-11-08). Ivy, Gleeful Spellthief copies anybody's spell
but "The copy targets Ivy": untagged. A copy aimed at "the chosen" permanent or at "that" creature
is this tag (Exterminator Magmarch, Zevlor; "that" OVERTURNED 11-10, R2 below).

Read the same day under the definition: an exchange of control takes the spell (Sudden
Substitution, Steal for its creature); a target spell chosen and copied in the next sentence
(Tempt with Mayhem, Radiate); "…, YOU MAY copy it" (Spider-Verse, Spinerock Tyrant); every spell
you control copied at once (Ulalek, OVERTURNED 11-10, R3). Reached and handed back, all reviewed
before the ruling of 09-28: Spider-Verse, Ulalek, Feather, Frontline Heroism (+Redirect; the last
three OVERTURNED 11-10, below). A copied ARTIFACT spell is always a permanent and becomes a token
(Iron Man, Bleeding Edge), as the creature spell does.

RULED 2026-11-10 evening, from the second sitting, R2: a copy of your spell on your own creature,
aimed by the card at "THAT creature / token" the trigger picked, can only land on one of yours and
is not this tag — Feather, Radiant Arbiter and Frontline Heroism, named (-Redirect), overturning the
reading of 11-09. A copy aimed at "the chosen" permanent of an opponent stays (Exterminator
Magmarch, Zevlor), and so does a copy with new targets free (Leyline of Resonance). Counted: "the
copy targets that" 0 of 2 reviewed, "the copy targets the chosen" 2 of 2.

RULED the same evening, R3 ("Sia 1 che 2"), on Ulalek, Fused Atrocity (named, -Redirect): a TRIBAL
cast trigger is never this tag, and copying ALL the spells and abilities you control is a doubler, as
Q1's ability copied on every activation. The rule "copy all spells you control" read Ulalek alone and
is gone. No other card has a copy on a creature-type cast trigger (Chancellor of Tales and Gorion
name Adventure, Jeong Jeong a Lesson), so the tribal half needs no guard in the code.
