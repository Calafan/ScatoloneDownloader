---
name: ontology-pass
description: >-
  Run an ontology pass on the MTG cube effect classifier — measure one CardEffect
  tag against the hand-reviewed cards, bucket the disagreements, count each
  hypothesis before writing it, bring the genuinely split cases back as questions,
  then apply the rulings to EffectClassifier, the tests, the CardEffect ontology
  comment, the glossary tooltip and the metadata store. Use this whenever the work
  touches EffectClassifier, CardEffect, EffectGlossary, the ScatoloneQuintet
  metadata store, precision/recall of the auto-tagger, or a disagreement between a
  hand tag and an automatic one — and use it even when the request names only a tag
  ("vediamo CardAdvantage", "procedi con Pacify", "prossima ontologia", "let's do
  Burn"), or asks why a card is or is not getting some effect. Use it too when the
  human comes back from a review sitting reporting errors ("ho finito la review,
  errori trovati: buff ai tribali"), answers a list of ruling questions, or asks
  for the ontology to be shown in a README. Also use it before writing any regex
  into EffectClassifier, because the rule here is that nothing is written before it
  is counted.
---

# Ontology pass

The classifier proposes, the human decides. `classify` writes `effects` and never
`reviewedAt`, and it never overwrites a reviewed entry — so the reviewed cards are
the only ground truth there is, and every number in this skill is measured against
them.

A pass takes one tag from "wrong a lot" to "wrong on a handful, and each one
recorded". Most tags have been through it; the shape below is what worked, and the
parts that look fussy are each there because skipping them cost a day.

## Three laws

**Measure before shipping.** A change that does not move the score did not happen.
The score is `acc-summary.txt`, and it is cheap — run it after every edit, not at
the end. When a change makes things worse, back it out rather than explaining it.

**A split is a question, not a rule.** When the hand tags divide near 50/50 on
identical wording, no regex can fix that: the ontology itself is undecided, and
writing a rule anyway just moves the errors around. Take it to the human with the
contradicting pair named — "Twiddle is untagged, Twitch is tagged, same card" — not
as a statistic. That one habit is what turned Pacify from 62% recall to 94%.

**Record what measurement rejected.** A hypothesis that failed is worth as much as
one that passed, and it will look just as plausible in three weeks. Write it into
the source next to the rule it almost became, with its numbers: *"a bare -5/-0 is
NOT here: measured and rejected at 2 tagged out of 12."*

## The work directory

Everything is driven by files, so no probe is ever edited to change a tag or a
pattern. The work directory is `state/work/` inside this skill — gitignored,
per machine, and **durable across sessions**, which the session scratchpad is
not. `$env:ONTOLOGY_DIR` overrides it; the scripts and `probes.py` use it
without being told.

`state/handed-back.json` beside it is the list of cards handed back for review
that every classify must protect. It is the one piece of state a pass cannot
lose, so it lives here and `scripts/handed_back.py` rebuilds it from the
store's git whenever in doubt (`python handed_back.py` compares, `--write`
rebuilds).

| you write | they write |
|---|---|
| `tag.txt` — one effect name | `acc-summary.txt` — the score, every tag |
| `names.txt` — one card name per line | `acc-detail.txt` — a sample of each tag's misses |
| `hypotheses.txt` — `name<TAB>regex` | `tag-detail.txt` — **every** disagreement on `tag.txt`, full text |
| `store.txt` — optional store path | `counts.txt` — each hypothesis counted |
| `blank.txt` — one regex to blank out | `text.txt` — oracle text, ids and current tags |
| | `why.txt` — which regex field fired, by reflection |
| | `matrix.txt` — which hand tags a NEW definition keeps |
| | `blank.jsonl` — every card whose proposal moves without the phrase |
| | `dump.jsonl` — every store card: text, type, stored and current tags |

`dump.jsonl` is for the questions the reports were not shaped for — joining with
`review-log.jsonl`, counting across tags, comparing the store with the current
rules — answered in Python against one file instead of a build per question.
`Blank` answers "which cards earn this tag ONLY from this phrase", which no regex
over the text can, because the rest of the card may earn the same tag otherwise.

When the human MOVES A DEFINITION rather than correcting the classifier, measure
the definition before any code: which hand tags it drops, which it adds, and
whether the human's own recent tags already follow it. Read the cards, not only
the counts — a clause regex that is too generous (every +1/+1 counter counted as
"permanent") reports a two-card blast radius for a rule that drops forty-six.

`scripts/probes.py` copies `assets/OntologyProbes.cs` into the gitignored
`ScatoloneDownloader.Tests/Cube/`, runs the probes named, prints the lines worth
reading, and removes the probe file again (a stale one inflates the next suite
count):

```powershell
python .claude\skills\ontology-pass\scripts\probes.py Score Dump TagDetail --tag Bounce
python .claude\skills\ontology-pass\scripts\probes.py Why --names "Magmasaur"
```

The probes are `Score`, `TagDetail`, `Count`, `Why`, `Text`, `Matrix`, `Blank`,
`Dump`. A run is ~15–30s and costs no tokens, which is the point: the searching
is deterministic, and the thinking is the expensive part. Never grep the
store's JSON by hand to answer a question one of these already answers exactly.

The scripts do the rest of the deterministic work. Every Python that holds a
backslash — and every JSON cases file — is written with the Write tool and run
from a file, never through a heredoc (`references/hazards.md`).

| script | answers |
|---|---|
| `probes.py` | runs probes by name, prints the tag's score and the TagDetail headers |
| `measure.py` | a code change against the code it replaces: before/after dumps, score, RIGHT/WRONG, moved lines |
| `families.py` | step 4 in a second: each wording family's tagged/untagged reviewed cards, with dates |
| `show.py` | text, hand tags, proposal and review date of cards by name, from `dump.jsonl` |
| `moved_lines.py` | which unreviewed (or reviewed) cards moved one tag, and the line that moved them |
| `review_changes.py` | what the human changed in their last sitting, per tag, from `review-log.jsonl` |
| `blast_radius.py` | what a code change moved: reviewed cards RIGHT/WRONG, unreviewed proposals |
| `gen_inline.py` | `[InlineData]` lines with the exact oracle text, from `dump.jsonl` |
| `splice_tests.py` | a new `[Theory]`, or more cases for an existing one, spliced into the test file |
| `neutralise.py` | which test goes red when each fix is undone alone |
| `find_cards.py` | name → oracle id, tier, tags |
| `make_rulings.py` | the ruling and hand-back files from `{"Card": ["+Bounce", "-Wipe"]}` |
| `store_pass.py` | the store commits: rulings, protected classify, commit, hand-back, README |
| `store_moves.py` | which tags moved on which cards — tree vs HEAD, or one commit |
| `handed_back.py` | the durable hand-back list: compare with git, rebuild, add |
| `apply_ruling.py` | a ruling or a hand-back applied to HEAD or to the tree |
| `split_unreviewed.py` | the classifier's proposals committed without the human's pass |
| `restore_handed_back.py` | handed-back entries put back after a classify |
| `verify_tree.py` | the tree against HEAD, and against a backup |
| `readme_ontology.py` | the ontology table in the ScatoloneQuintet README, checked or rewritten |

## When the human comes back from a review

"Ho finito la review, errori trovati: buff ai tribali" names a family, not the
cards. The cards are in the review log: `before` is what the reviewer was shown,
`after` what they left, so every tag they added or removed is a disagreement
with the rules, recorded exactly.

```powershell
python scripts\review_changes.py --tag Buff      # since the store's last commit
```

Read every changed card's text (`Text`), not only the family the human named:
on 2026-09-25 "buff ai tribali, buff ai token" came with fourteen other Buff
changes in the same log — a quoted token pump hiding a double strike, a
cast-creature engine, a scavenge grant — each a rule that could not read a
wording. Then look for the OLDER reviewed cards the reported family also
covers (`Count`): twelve Krenko-style tribal counters were still tagged Buff
from before the human had settled the question.

Bring back as questions only what the log cannot settle: a card changed
against an explicit earlier ruling (Honor put back to Buff against B1), or two
cards with the same words tagged both ways on the same day (Kozilek and It That
Heralds the End). The human clears the log after committing a pass, so it covers
the latest sitting only.

## The pass

**1. Score, and pick the tag.** `Score` prints every tag worst-first. Take the top
one unless the human named a different one.

**2. Read every disagreement.** `tag.txt` ← the tag, then `TagDetail`. Read the
whole thing. The false positives and the misses are one problem seen from two
sides, and a rule written from the misses alone usually creates false positives.

**3. Bucket by WORDING, not by card.** Twenty misses are rarely twenty problems;
they are three or four families, each a sentence the rules cannot read. Name each
family by what it says, not by which cards are in it.

**4. Count every family before writing it.** One line per family,
`name<TAB>regex`, then `scripts/families.py TAG families.txt` — a second, no
build, every card listed with its review date and the human's tags (the `Count`
probe answers the same from the build, with full texts). Count the BROAD
reading, not only the wording that prompted it: on 2026-09-29 "return your own
permanent" measured 0 of 7 in its narrow form and 3 of 31 in the broad one,
and the three were the human's questions. Then:

- clean majority (say 9 of 10) → write it, and name the exception in the comment
- near 50/50 → **stop**, this is step 5
- mostly untagged → it is a veto, not a rule; write it as a guard

**5. Ask, with pairs.** Collect the split families and bring them as a numbered
list, each with the two cards that contradict each other and the count, and a
recommendation. Keep doing the mechanical work while waiting; do not guess a
ruling to keep moving. When the human is AWAY ("vado afk, correggi le letture"),
ship only the clean majorities and the readings, commit them, and leave every
split as a question with its pair — that is what they asked for.

Reading the answers:

- "X = Tag" on a card that has other tags ADDS the tag; the human says "solo"
  when they mean replace ("Neutralize the Guards confermo solo Wipe").
- "errore", "svista", "errore di click" correct the named card and nothing else;
  when the obvious replacement is another tag (Krile's Bounce "errore di
  click" for its plain Regrowth), apply it and say so, so it can be undone.
- "Come impatta con il punto N?" and "cosa avevamo deciso?" are questions, not
  rulings: answer from the `CardEffect` comment (the canonical record), propose
  the consistent reading, and implement only after "confermo".

**6. Apply.** A ruling lands in up to five places, and missing one leaves the
ontology lying to the next reader:

- **the hand tags**, when the human's own tags were the thing that was wrong →
  `scripts/make_rulings.py` then `scripts/store_pass.py rulings`, see
  `references/store.md`
- **the code** → `EffectClassifier.*`, with the ruling and its count in the comment
- **the tests** → one `[InlineData]` per ruling, with real oracle text, through
  `scripts/splice_tests.py`; never invented or retyped text. A test pinning the
  ruling that was just overturned is moved, with a comment saying so and when
- **the ontology** → the `CardEffect` member comment, which is the canonical
  record, and the `EffectGlossary` tooltip (40–340 characters, enforced by a test)
- **the README** of ScatoloneQuintet, whose ontology table copies the tooltips
  → `scripts/readme_ontology.py --write` whenever a tooltip changed

The hand tags move in two different ways, and the difference is the human's:

- a card the human **named** in the ruling ("It That Heralds the End vecchio,
  correggilo") is applied and stays reviewed — they have already looked at it;
- a card the rule **reaches** but the human did not name, and that was tagged the
  other way, is applied and **handed back** (`op: "unreview"`), so they confirm
  it in the tagger. Asked for on 2026-09-25 ("togli anche reviewed così le
  riguardo direttamente io"), and it applies to every realignment since.

Score the realigned cards BEFORE handing them back: once unreviewed they drop
out of `Score`, so a tag the ruling put on by hand that the code does not
propose stays invisible until the human re-reviews it. The Sacrifice pass
reported 100% with Radiant Lotus's "Sacrifice one or more artifacts:" unread —
the ruling had tagged it, the base pattern never asked.

Sort the WRONG list by REASON before building the realignment, not by tag. A
ruling that makes two tags exclusive ("se ha CardAdvantage non ha Filter",
2026-09-27) also surfaces cards where the hand had one tag and the rules the
other — Arcade Gannon was Filter by hand and CardAdvantage by the rules, and
dropping Filter would have realigned it on a question nobody had asked. Group:
the hand already has both (the ruling reaches it), the ruling's own shape (69
one-shot scries), and "hand says X, rules say Y" — the last is a question.

When the review log is gone, a store snapshot from before the sitting still says
what the tagger SHOWED. On 2026-09-27 it proved the human's 09-23 edits active
(22 of 25 differed from the stale proposals on screen), which is what made them
evidence rather than noise.

**7. Re-measure after each change**, not after all of them. When two edits go in
together and the score drops, the run has to be repeated to find which one did it.
`scripts/measure.py TAG` stashes the classifier, dumps the old proposals, pops
the change, and prints the score, the WRONG list on the reviewed cards and every
unreviewed card that moved with its line. Each WRONG is either a rule that
overreaches or an older tag the ruling overturns; READ the unreviewed lines too —
that is where a self-reference (God-Eternals), a reminder text (shroud's "you
can't be the targets") or a cost hides among a hundred right ones.

When the rule as the human worded it breaks reviewed cards, find the narrower
rule they meant before asking. "Alziamo la soglia per le creature" applied to
every creature one-shot took Buff from eight cards the human had tagged
(Toucan-Puffin's temporary pump on entry, Agent of Kotis's counters from the
graveyard); the reason they gave — "come se fosse un 6/6" — was about counters
a creature hands out AS IT ENTERS, and that narrower rule moved none of the
eight. Say in the report which one shipped and why.

**8. Finish.** Full suite green, then **verification by neutralisation**: one case
per fix in a JSON file, `scripts/neutralise.py cases.json`, and every case must
turn its own test red (run one by hand first to prove the harness reads
failures). Break a regex by prefixing `(?!)`, never by deleting a line. A case
that turns NOTHING red is either an untested fix (add the test) or dead code —
remove the dead alternative after checking with `measure.py` that no card moves
without it, and say in the comment that it was tried; this happened four times
on 2026-09-27/29. Probes deleted, `readme_ontology.py --check` clean, then the
commits described in `references/store.md`. Every number and every card named in
a commit message is measured: `store_moves.py --commit SHA --names` after the
commit checks the prose. Report the before/after numbers, what was ruled, and
what measurement rejected.

## When a card disagrees and the reason is not obvious

Put it in `names.txt` and run `Why`. It reflects over every private static `Regex`
on `EffectClassifier` and reports which ones the text matches, so the answer is the
field's name rather than a guess. It has already found a rule that had never once
fired, and a rule vetoed by its own reminder text.

`Why` also reports a field that is **null at read time**, which is the
partial-class initialisation hazard in `references/hazards.md`.

## Delegation

Read `C:\Users\Cala\.claude\AGENTS.md` first; it governs, and this section only
says how it applies here.

The honest position for this workflow: **delegation is the exception.** The
expensive operations in an ontology pass are deterministic, not cognitive — the
probes above do all the searching exactly, for no tokens, and the repo is
CodeGraph-indexed so a `codegraph_explore` beats a spawned agent for any code
question. What is left is the judgement, and AGENTS.md forbids delegating that.

**Never delegate** (AGENTS.md §3, and each has bitten here): deciding whether a
disagreement is real; choosing a threshold; reading a measurement run; **any number
that ends up in a commit message or in an answer to the human** — those are
measured, never estimated; and commits.

**Worth delegating**, when the volume justifies a cold start:

| task | how |
|---|---|
| implement an already-written pattern spec + its tests | `Agent` with `model: "sonnet"`, `isolation: "worktree"` |
| sweep many files for one conclusion, outside this repo | `Agent` with `model: "haiku"` |

On return from the Sonnet agent, the third column of AGENTS.md's table is the part
that gets forgotten: build, the whole suite, and **verification by neutralisation**
— remove the fix and the suite must go red on the right cases. Re-run `Score`
yourself; a subagent's accuracy figure is raw material, not a result. And never let
an agent loop on a test until it passes: a test written green proves nothing, and
the loop optimises for green rather than for right.

Use the `Agent` tool, not a nested `claude` CLI process — a spawned session
re-reads the repo instructions cold and returns unstructured text. Model names are
`sonnet` / `haiku` / `opus` / `fable`, not dated API ids.

## References

- `references/store.md` — the metadata store: byte format, the commit split, and
  the rule that the human's in-progress reviewed entries are never committed
- `references/hazards.md` — the traps that have cost real time, each with the
  symptom that identifies it
- `scripts/` — the table under "The work directory"; each script's docstring
  says how to call it. `common.py` names the durable paths once
- `state/` — gitignored working state: `handed-back.json` and `work/`
