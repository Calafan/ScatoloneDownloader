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

## The two commits

A pass produces at most two commits, and they are kept apart because they are two
different kinds of claim. The first says *the human changed their mind*; the second
says *the classifier reads more wordings now*. Mixing them makes both unreviewable.

### 1. Rulings on reviewed entries

Only when the human overturned their own tags.

```powershell
# 1. back up the human's tree
Copy-Item $META\*.json $WORK\pre-pass\

# 2. build HEAD + only the rulings, and commit that
python scripts\apply_ruling.py rulings.json --mode head
git -C $REPO add metadata/ ; git -C $REPO commit -F msg.txt

# 3. give the human their tree back, with the rulings also applied to it
Copy-Item $WORK\pre-pass\*.json $META\
python scripts\apply_ruling.py rulings.json --mode tree
```

`--mode head` prints `non-effects fields: 0` when it is safe. In `--mode tree` the
entry counts include the human's own work, so only that line is a signal.

Build `rulings.json` with `scripts/find_cards.py --json`, never by pasting oracle
ids — a wrong id is a silent no-op, or a tag written onto the wrong card.

### 2. The classifier's new proposals

```powershell
Copy-Item $META\*.json $WORK\post-ruling\           # the human's tree, rulings applied
dotnet run --project ScatoloneDownloader -- classify -m $META --overwrite
Copy-Item $META\*.json $WORK\post-classify\         # what classify produced

python scripts\split_unreviewed.py $WORK\post-classify
git -C $REPO add metadata/ ; git -C $REPO commit -F msg.txt

Copy-Item $WORK\post-classify\*.json $META\         # restore the human's tree
```

Then verify, every time:

```powershell
git -C $REPO diff -U0 -- metadata/ | Select-String 'reviewedAt'   # must be empty
```

and the `still uncommitted: N | NOT reviewed: 0` check above.

## Data-quality note

Five entries were once keyed by NAME rather than oracle id, from an XMP import
onto textless "(Theme color: {X})" cards. Four were re-keyed; **Spider-Verse** is
still wrong and was deliberately left for the human. If a card's stored `name` does
not match what Scryfall returns for its `oracleId`, that is the same bug.
