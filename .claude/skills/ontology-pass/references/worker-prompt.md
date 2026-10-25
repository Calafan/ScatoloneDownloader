# Worker prompt: one ontology pass, one tag, human away

The prompt the orchestrator gives a Sonnet worker for one tag of an unattended multi-tag run (see
"A whole pass to a worker" in SKILL.md). Fill the placeholders and pass everything below the line.
The four judgement rules come from the 2026-10-24 A/B runs: each one is a mistake a worker made
without it.

- `{TAG}`: the CardEffect tag
- `{CLONE}` / `{CLONE_BASH}`: the clone's path in PowerShell and bash form
- `{BRANCH}`, `{BASE}`: the clone's branch and the commit it starts from
- `{BASELINE}`: the tag's `Score` line at `{BASE}`, measured by the orchestrator (support, TP, FP, FN)

---

# Ontology pass on {TAG}

You are running one ontology pass in an isolated clone of the repository. The orchestrator re-measures
everything you report and reviews your diff before anything reaches `main`, so follow the skill, not
shortcuts.

## Where you work

- Clone: `{CLONE}` (branch `{BRANCH}`, base commit `{BASE}`). It has no remote.
- Every shell command starts with `cd {CLONE};` (PowerShell) or `cd {CLONE_BASH} &&` (bash). Every
  path you Read, Edit or Write is under `{CLONE}`.
- The main checkout `E:\Working\Repos\Scryfall\ScatoloneDownloader` and every other clone are off
  limits: do not read them, do not write them.
- The skill's work directory is the clone's own `.claude/skills/ontology-pass/state/work/`; do not set
  `ONTOLOGY_DIR`. If its `store.txt` exists, it points the probes at a frozen copy of the store; leave
  it as it is.

## Read first, in full

1. `{CLONE}\.claude\skills\ontology-pass\SKILL.md`
2. `references/hazards.md` and `references/store.md` in the same skill folder
3. `docs/ontology/{TAG}.md`, the canonical record for this tag

## The task

Tag: **{TAG}**. Baseline `Score` at `{BASE}`, measured before you started: {BASELINE}. Re-measure it
yourself as step 1.

The human is **away** ("vado afk, correggi le letture"). Apply step 5 of the skill as written for
that case: ship only the clean majorities and the readings; every family that splits near 50/50,
every card where the hand tag itself looks wrong, every definition question becomes a numbered
question with the contradicting pair, the count and your recommendation. Never guess a ruling to
keep moving.

A finished pass here is steps 1-8 of the skill for {TAG}, minus the store, the README and the
commits: classifier change with its comments, tests through `splice_tests.py` with real oracle text,
`docs/ontology/{TAG}.md` and the `EffectGlossary` tooltip when a reading changes, the full test suite
green, neutralisation with `neutralise.py` on `state/work/cases-{TAG}.json` where every case turns
its own test red, probe files removed.

## Judgement rules

These four sharpen the skill for an unattended pass; where they are stricter than the skill, they win.

1. **Reading or ruling.** You ship a *reading*: a wording the rules fail to read, under a ruling that
   `docs/ontology/{TAG}.md` already contains — quote that ruling in the comment. Before you turn a
   card into a question, look for that ruling: if the doc already answers it, it is a reading. A rule
   that changes what counts as {TAG} (a new veto, a new kind of card in or out) is a *ruling*: it
   stays a question even when its count is clean, with your recommendation and the count.
2. **Recorded disagreements stay closed.** A card or family that `{TAG}.md` records as left on
   purpose, or as a known disagreement, is not re-opened as a question. If its count in the doc no
   longer matches what you measure, list it under "stale records" in the report, with both numbers.
3. **No two rules that contradict each other.** When a reading supersedes an older alternative,
   pattern or veto, measure the older one (a Dump with and without it, or a neutralisation case) and
   remove it if no card moves; say so in the comment. If cards do move, it is a question.
4. **Rejected hypotheses go into the source.** Every hypothesis you measure and reject is written as
   a comment next to the rule it almost became, with its numbers (the skill's third law), as well as
   in the report.

## Hard limits — breaking one voids the run

- The live metadata store `E:\Working\Repos\ScatoloneQuintet` is **read-only**, and so is any frozen
  copy. Never run `classify`, `store_pass.py`, `apply_ruling.py`, `make_rulings.py` against a store,
  `restore_handed_back.py`, `readme_ontology.py --write`, or anything else that writes a store.
  Rulings on hand tags go into the report as proposals.
- No `git commit`, `push`, `merge`, `rebase`, `reset`, `restore`, `clean`, `checkout -- <file>`, no
  branch changes. Leave every change uncommitted in the clone.
- No `git stash` by hand. `measure.py` stashes and pops by itself: run `git stash list` before and
  after every `measure.py`; it must be empty both times, and if it is not, stop and report.
- No subagents. CodeGraph is not indexed in the clone: use Grep, Glob and Read.
- Python that holds a backslash, and every JSON cases file, is written with the Write tool and run
  from a file (the skill's rule).
- If a tooltip changes, say so in the report; do not touch the ScatoloneQuintet README.

## Report

Your final message is the report, plain text, nothing after it:

1. `Score` for {TAG} before and after (support, TP, FP, FN, prec, rec), the exact tag-set match
   count, and every other tag whose line moved in the full table.
2. Each wording family you found: name, regex, counts (tagged/untagged among reviewed cards),
   decision (shipped reading — with the ruling it applies / question / veto proposed / rejected).
3. Questions for the human, numbered, each with the contradicting pair, the count and your
   recommendation.
4. Hypotheses measured and rejected, with their numbers, and where in the source each is recorded.
5. Stale records in `{TAG}.md`: doc count against measured count.
6. Proposed hand-tag changes for the store, one per card, with the reason.
7. `git status --short` and `git diff --stat` of the clone.
8. Full suite result (passed / failed counts) and the neutralisation result per case.
9. Anything you could not do, and why.
