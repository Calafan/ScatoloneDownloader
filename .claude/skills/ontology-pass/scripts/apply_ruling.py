"""Apply a list of tag rulings to the metadata store.

A ruling changes what the HUMAN says about a card, so it is the one thing in a
pass that rewrites reviewed entries. It is kept apart from the classifier's own
output for that reason, and it is written twice:

  --mode head   HEAD + only these rulings          -> this is what gets committed
  --mode tree   the current working tree + rulings -> this is what the human keeps

The split exists because the working tree carries the human's own in-progress
review pass, which they commit themselves when the year is finished. Committing
the tree wholesale would take that work with it.

Usage:
    python apply_ruling.py rulings.json --mode head --backed-up-to DIR   # store_pass.py only
    python apply_ruling.py rulings.json --mode tree --store E:\\path\\to\\metadata

--mode head is NOT a dry run: it rewrites the working tree's tier files as HEAD +
rulings, and refuses unless --backed-up-to names a byte-identical copy of the
tree to restore from. Never run it by hand; `store_pass.py rulings` and
`handback` do the backup, the commit and the restore around it.

rulings.json is a list of {name, oracleId, effect, op} where op is
add|remove|unreview (an unreview needs no effect). `name` is for the human
reading the diff; `oracleId` is what is matched.

Byte format of the store, which must round-trip exactly: no BOM, 2-space
indent, CRLF in the working tree, NO trailing newline.
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

FILES = ["fringe.json", "pool.json", "unrated.json"]
DEFAULT_STORE = Path(r"E:\Working\Repos\ScatoloneQuintet\metadata")

# Effects are stored in ENUM DECLARATION ORDER, not alphabetically. A diff that
# reorders them is noise that hides the real change.
ORDER = ("Tokens Removal Counter RemovePermanent Wipe Bounce Ramp Disenchant Discard "
         "CardAdvantage Filter Reanimate Buff Protection Burn Sacrifice Steal Tutor "
         "ManaFixing Pacify LandDestruction Mill Regrowth Redirect Cheat").split()
RANK = {name: i for i, name in enumerate(ORDER)}


def dump(data):
    return json.dumps(data, indent=2, ensure_ascii=False).replace("\n", "\r\n")


def load_head(repo, name):
    raw = subprocess.run(["git", "show", f"HEAD:metadata/{name}"], cwd=repo,
                         capture_output=True, check=True).stdout.decode("utf-8")
    return json.loads(raw)


def apply(data_by_file, rulings, reviewed_only=False):
    """Add or remove each ruling's tag, keeping enum declaration order.

    reviewed_only is for --mode head. A card the human reviewed only in the
    working tree is still an UNREVIEWED proposal in HEAD, and changing it there
    would commit a classifier proposal under a ruling's name — and a ruling on
    it belongs to the human's own uncommitted pass, which they commit. Such
    cards are skipped here and counted; --mode tree gives them the ruling.
    """
    touched = skipped = 0

    for edit in rulings:
        oracle, effect = edit["oracleId"], edit.get("effect", "")
        # An "unreview" names no effect: it moves reviewedAt, not a tag.
        if edit.get("op") != "unreview" and effect not in RANK:
            raise SystemExit(f"not a CardEffect: {effect} ({edit.get('name')})")

        removing = edit.get("op") == "remove"

        for data in data_by_file.values():
            entry = data.get("cards", {}).get(oracle)
            if entry is None:
                continue

            if reviewed_only and not entry.get("reviewedAt"):
                skipped += 1
                break

            # op "unreview" hands the card BACK to the human: its tags stay as
            # they are, and dropping reviewedAt puts it in the tagger's pending
            # queue. Asked for on 2026-09-25, for a ruling the human had only
            # settled on in the last part of a sitting and wanted to see applied
            # card by card. NB a later `classify --overwrite` will replace these
            # tags with proposals, so run it BEFORE unreviewing, not after.
            if edit.get("op") == "unreview":
                if entry.pop("reviewedAt", None) is not None:
                    touched += 1
                break

            effects = entry.get("effects") or []

            # Already in the wanted state: nothing to do, and not an error —
            # a ruling often lands on a card HEAD already agrees with.
            if removing == (effect not in effects):
                break

            kept = [e for e in effects if e != effect] if removing else effects + [effect]
            entry["effects"] = sorted(kept, key=lambda e: RANK[e])
            touched += 1
            break
        else:
            raise SystemExit(f"not found in any tier file: {edit.get('name')} ({oracle})")

    return touched, skipped


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("rulings", help="JSON list of {name, oracleId, effect, op}")
    ap.add_argument("--mode", choices=["head", "tree"], required=True)
    ap.add_argument("--store", default=str(DEFAULT_STORE))
    ap.add_argument("--backed-up-to", help="--mode head only: a directory holding byte-identical copies "
                                           "of the three tier files as they are in the tree now")
    args = ap.parse_args()

    meta = Path(args.store)
    repo = meta.parent
    rulings = json.loads(Path(args.rulings).read_text(encoding="utf-8"))

    # --mode head REWRITES the working tree as HEAD + rulings, so the human's
    # uncommitted pass is gone from the files unless somebody put it back.
    # Run by hand "to check" on 2026-09-29, it wiped a 180-card sitting that had
    # to be rebuilt from a Dump. Only store_pass.py may run it, and it proves the
    # backup it restores from afterwards exists and matches the tree.
    if args.mode == "head":
        backup = Path(args.backed_up_to) if args.backed_up_to else None
        if backup is None:
            sys.exit("--mode head overwrites the working tree: run it through `store_pass.py rulings` "
                     "or `handback`, which back the tree up and restore it (see references/store.md)")
        for name in FILES:
            if not (backup / name).is_file() or (backup / name).read_bytes() != (meta / name).read_bytes():
                sys.exit("--backed-up-to %s does not hold the tree's %s as it is now — nothing written"
                         % (backup, name))

    if args.mode == "head":
        data_by_file = {name: load_head(repo, name) for name in FILES}
    else:
        data_by_file = {name: json.loads((meta / name).read_text(encoding="utf-8")) for name in FILES}

    touched, skipped = apply(data_by_file, rulings, reviewed_only=args.mode == "head")

    for name, data in data_by_file.items():
        (meta / name).write_bytes(dump(data).encode("utf-8"))

    # Verify against HEAD: report what moved and, crucially, whether anything
    # OTHER than `effects` changed. In --mode tree the counts include the
    # human's own uncommitted work, so only the `non-effects` line is a signal.
    head = {name: load_head(repo, name) for name in FILES}
    changed = bad_field = entry_sets = unreviewed = 0
    unreviewing = {e["oracleId"] for e in rulings if e.get("op") == "unreview"}

    for name in FILES:
        after = json.loads((meta / name).read_text(encoding="utf-8")).get("cards", {})
        before = head[name].get("cards", {})

        if set(after) != set(before):
            entry_sets += 1

        for oracle in set(after) & set(before):
            if after[oracle] == before[oracle]:
                continue

            changed += 1
            for key in set(after[oracle]) | set(before[oracle]):
                if key == "effects" or after[oracle].get(key) == before[oracle].get(key):
                    continue
                # A reviewedAt that went AWAY on a card this file unreviews is the
                # one non-effects change that is asked for; count it apart.
                if key == "reviewedAt" and oracle in unreviewing and key not in after[oracle]:
                    unreviewed += 1
                else:
                    bad_field += 1

    print(f"mode={args.mode} rulings applied={touched} of {len(rulings)}"
          + (f" | {skipped} not reviewed in HEAD, left for --mode tree" if skipped else ""))
    print(f"vs HEAD: {changed} entries changed | non-effects fields: {bad_field} "
          f"| reviewedAt cleared as asked: {unreviewed} | entry-set changed in {entry_sets} files")

    if args.mode == "head" and bad_field:
        print("!! a field other than effects moved — do NOT commit this", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
