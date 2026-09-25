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
pattern. Set the work directory once per session — the scratchpad is the right
place — and the probes read their input and write their reports there:

```powershell
$env:ONTOLOGY_DIR = "<scratchpad>\ontology"
```

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

Copy `assets/OntologyProbes.cs` into `ScatoloneDownloader.Tests/Cube/` and run by
filter. That folder is gitignored, so the probes cannot reach a commit; delete it
before finishing anyway, because a stale probe in the tree is confusing.

```powershell
Copy-Item .claude\skills\ontology-pass\assets\OntologyProbes.cs ScatoloneDownloader.Tests\Cube\ -Force
dotnet test ScatoloneDownloader.Tests --filter "FullyQualifiedName~OntologyProbes.Score" --nologo -v q
```

Filter by method for one report (`.Score`, `.TagDetail`, `.Count`, `.Why`,
`.Text`, `.Matrix`, `.Blank`, `.Dump`) or by `~OntologyProbes` for all eight. A
full run is ~30s and costs no
tokens, which is the point: the searching is deterministic, and the thinking is
the expensive part. Never grep the store's JSON by hand to answer a question one
of these already answers exactly.

The scripts in `scripts/` do the rest of the deterministic work. Every Python
script that holds a backslash lives in a file and is run from there — never a
heredoc, which rewrites escapes (`references/hazards.md`).

| script | answers |
|---|---|
| `review_changes.py` | what the human changed in their last sitting, per tag, from `review-log.jsonl` |
| `blast_radius.py` | what a code change moved: reviewed cards RIGHT/WRONG, unreviewed proposals |
| `gen_inline.py` | `[InlineData]` lines with the exact oracle text, from `dump.jsonl` |
| `neutralise.py` | which test goes red when each fix is undone alone |
| `find_cards.py` | name → oracle id, tier, tags; builds ruling files |
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

**4. Count every family before writing it.** `hypotheses.txt` ← one line per
family, then `Count`. The report prints the full text of every card that matches
but is NOT tagged, because those are the evidence. Then:

- clean majority (say 9 of 10) → write it, and name the exception in the comment
- near 50/50 → **stop**, this is step 5
- mostly untagged → it is a veto, not a rule; write it as a guard

**5. Ask, with pairs.** Collect the split families and bring them as a numbered
list, each with the two cards that contradict each other and the count. Keep doing
the mechanical work while waiting; do not guess a ruling to keep moving.

**6. Apply.** A ruling lands in up to five places, and missing one leaves the
ontology lying to the next reader:

- **the hand tags**, when the human's own tags were the thing that was wrong →
  `scripts/apply_ruling.py`, see `references/store.md`
- **the code** → `EffectClassifier.*`, with the ruling and its count in the comment
- **the tests** → one `[InlineData]` per ruling, with real oracle text from
  `scripts/gen_inline.py`; never invented or retyped text. A test pinning the
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

**7. Re-measure after each change**, not after all of them. When two edits go in
together and the score drops, the run has to be repeated to find which one did it.
`Dump` before the change and after it, then `scripts/blast_radius.py`: the score
says how much moved, the WRONG list says which reviewed cards, and each one is
either a rule that overreaches or an older tag the ruling overturns.

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
failures). Probes deleted, `readme_ontology.py --check` clean, then the commits
described in `references/store.md`. Report the before/after numbers, what was
ruled, and what measurement rejected.

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
  says how to call it
