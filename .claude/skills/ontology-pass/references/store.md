# The metadata store

Truth lives in `E:\Working\Repos\ScatoloneQuintet\metadata` — a separate repo from
the code — as three tier files: `fringe.json`, `pool.json`, `unrated.json`. One
entry per `oracle_id`, so a card is one entry however many printings it has.

```json
"646f9ad8-9d18-4b42-954a-e21a62e9f0e8": {
  "name": "Aang's Iceberg",
  "rating": 4,
  "label": "",
  "scryfallId": "6cd25a10-...",
  "effects": ["Filter"],
  "reviewedAt": "2026-09-10T15:29:..."
}
```

`reviewedAt` is what separates a human's decision from the classifier's guess.
`classify` fills `effects` and never touches `reviewedAt`, and it never overwrites
an entry that has one.

## The one rule that must not be broken

**The human's in-progress reviewed entries are never committed by this workflow.**
They commit them themselves when that year's review pass is finished. At any moment
the working tree holds several hundred such entries, plus tier moves (a card
promoted from `fringe` to `pool` when they rate it) and sometimes a modified
`README.md` that has nothing to do with the classifier.

So every commit to the store is built from `HEAD` plus exactly one kind of change,
and the human's tree is restored afterwards. The verification that this worked is:

```
still uncommitted: N entries | NOT reviewed: 0
```

`NOT reviewed: 0` is the invariant. If a non-reviewed entry is still sitting in the
tree after the split, a classifier proposal was left uncommitted and the next pass
will measure against a store that does not match the code.

The one sanctioned exception is a card HANDED BACK for review (`op: "unreview"`
in `apply_ruling.py`, first asked for on 2026-09-25): the human's own card, from
their uncommitted pass, that keeps its ruled tags and loses `reviewedAt`. It
shows as NOT reviewed and differs from HEAD's proposal, and it stays uncommitted
with the rest of that pass. Count them before and after, and say so in the
report. Unreview only AFTER `classify --overwrite` and the split — an unreviewed
entry is overwritten by the next classify.

**While handed-back cards are outstanding, every later classify has to protect
them.** This bit on the very next run of 2026-09-25: classify rewrote them as the
unreviewed entries they are (7 of 105 actually changed), and `split_unreviewed.py`
refused, because a card from the human's pass carries their rating too.

The list lives in `state/handed-back.json` — NOT in the session scratchpad, where
it sat until 2026-09-29 and where a new session would not have found it.
`restore_handed_back.py`, `split_unreviewed.py` and `verify_tree.py` default to
it; `store_pass.py handback` adds to it; `handed_back.py` rebuilds it from git,
because every hand-back is its own "chore(metadata): hand … back for review"
commit and nothing else in this workflow removes a `reviewedAt` (it matched the
session's list exactly, 156 of 156, when it was written). The human confirms the
cards in the tagger and commits them with their own pass; they then drop out of
the rebuilt list by themselves.

## Byte format

Round-trips exactly, and anything else produces a diff of the whole file:

- no BOM
- 2-space indent
- **CRLF** in the working tree, LF in git blobs
- **no trailing newline**

In Python: `json.dumps(data, indent=2, ensure_ascii=False).replace("\n", "\r\n")`
written with `newline=""`.

`effects` is in **enum declaration order**, not alphabetical:

```
Tokens Removal Counter RemovePermanent Wipe Bounce Ramp Disenchant Discard
CardAdvantage Filter Reanimate Buff Protection Burn Sacrifice Steal Tutor
ManaFixing Pacify LandDestruction Mill Regrowth Redirect Cheat
```

Both scripts here already do all of this. Hand-editing the JSON does not.

## The commits of a pass

A pass produces up to four store commits, kept apart because each is a different
kind of claim — *the human changed their mind*, *the classifier reads more
wordings now*, *these cards go back for review*, *the README follows the
tooltips*. Mixing them makes each unreviewable. `scripts/store_pass.py` runs each
step, backs up and restores the human's tree around it, and verifies:

```powershell
# the answers as names and tags -> the two ruling files (named / reached)
python scripts\make_rulings.py spec.json

# 1. the human's rulings: HEAD + rulings committed, the tree restored and ruled too
python scripts\store_pass.py rulings state\work\rulings-NAME.json -F msg-rulings.txt

# (commit the code in the code repo here: the classify below runs it)

# 2. the classifier's proposals: Release build, protected classify, split — then read the moves
python scripts\store_pass.py classify
python scripts\store_moves.py --names        # write the message from this
python scripts\store_pass.py commit -F msg-classify.txt

# 3. the reached cards handed back (AFTER the classify, or it overwrites them)
python scripts\store_pass.py handback state\work\unreview-NAME.json -F msg-handback.txt

# 4. the README table, when a tooltip changed
python scripts\store_pass.py readme -F msg-readme.txt
```

What each step checks, and refuses on:

- `rulings` and `handback` stop unless `apply_ruling --mode head` reports
  `non-effects fields: 0`; `rulings` also stops if a `reviewedAt` moved. The
  tree is restored from the backup whether they stop or not, and when no ruled
  card is reviewed in HEAD there is no commit and the rulings go to the tree
  only.
- `apply_ruling --mode head` REWRITES the tree and is never run by hand: it
  refuses without `--backed-up-to`, a byte-identical copy of the tree that only
  `store_pass` passes (see hazards.md, 'A "dry run" that rewrote the human's tree').
- `classify` prints `reviewedAt lines in the staged diff: 0` — it must be 0 —
  and the per-tag moves; `commit` refuses when it is not 0.
- Every step ends in `verify_tree.py`: `still uncommitted: N reviewed | NOT
  reviewed: 0 (outside the handed-back set: 0)`, and after a classify
  `handed-back entries identical to the backup: all`.

After the commit, `store_moves.py --commit SHA --names` checks every card the
message names. Messages come from a file (`-F`) and end with the attribution
lines the session asks for. Nothing is pushed.

Build ruling files with `make_rulings.py` (or `find_cards.py --json`), never by
pasting oracle ids — a wrong id is a silent no-op, or a tag written onto the
wrong card; `make_rulings.py` refuses any name that does not match exactly one
entry.

## Data-quality note

Five entries were once keyed by NAME rather than oracle id, from an XMP import
onto textless "(Theme color: {X})" cards. Four were re-keyed; **Spider-Verse** is
still wrong and was deliberately left for the human. If a card's stored `name` does
not match what Scryfall returns for its `oracleId`, that is the same bug.
