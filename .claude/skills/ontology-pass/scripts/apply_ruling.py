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
    python apply_ruling.py rulings.json --mode head
    python apply_ruling.py rulings.json --mode tree --store E:\\path\\to\\metadata

rulings.json is a list of {name, oracleId, effect, op} where op is add|remove.
`name` is for the human reading the diff; `oracleId` is what is matched.

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


def apply(data_by_file, rulings):
    """Add or remove each ruling's tag, keeping enum declaration order."""
    touched = 0

    for edit in rulings:
        oracle, effect = edit["oracleId"], edit["effect"]
        if effect not in RANK:
            raise SystemExit(f"not a CardEffect: {effect} ({edit.get('name')})")

        removing = edit.get("op") == "remove"

        for data in data_by_file.values():
            entry = data.get("cards", {}).get(oracle)
            if entry is None:
                continue

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

    return touched


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("rulings", help="JSON list of {name, oracleId, effect, op}")
    ap.add_argument("--mode", choices=["head", "tree"], required=True)
    ap.add_argument("--store", default=str(DEFAULT_STORE))
    args = ap.parse_args()

    meta = Path(args.store)
    repo = meta.parent
    rulings = json.loads(Path(args.rulings).read_text(encoding="utf-8"))

    if args.mode == "head":
        data_by_file = {name: load_head(repo, name) for name in FILES}
    else:
        data_by_file = {name: json.loads((meta / name).read_text(encoding="utf-8")) for name in FILES}

    touched = apply(data_by_file, rulings)

    for name, data in data_by_file.items():
        (meta / name).write_bytes(dump(data).encode("utf-8"))

    # Verify against HEAD: report what moved and, crucially, whether anything
    # OTHER than `effects` changed. In --mode tree the counts include the
    # human's own uncommitted work, so only the `non-effects` line is a signal.
    head = {name: load_head(repo, name) for name in FILES}
    changed = bad_field = entry_sets = 0

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
                if key != "effects" and after[oracle].get(key) != before[oracle].get(key):
                    bad_field += 1

    print(f"mode={args.mode} rulings applied={touched} of {len(rulings)}")
    print(f"vs HEAD: {changed} entries changed | non-effects fields: {bad_field} "
          f"| entry-set changed in {entry_sets} files")

    if args.mode == "head" and bad_field:
        print("!! a field other than effects moved — do NOT commit this", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
