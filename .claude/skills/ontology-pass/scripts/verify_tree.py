"""Check the store's working tree against HEAD — the invariant of every split.

Prints how many entries differ from HEAD and are reviewed (the human's own
uncommitted pass, never committed by this workflow) and how many differ and are
NOT reviewed. That second number must be 0 after a split, except for cards
HANDED BACK for review, which --handed-back names; the report says how many
fall outside that list, and that is the number that must be 0.

--compare DIR also lists every entry that differs from a backup, with the
fields that moved — the check after applying a ruling or an unreview to the
tree, where exactly the ruled cards and exactly the ruled fields should move.
With both flags it also says whether the handed-back entries are identical to
the backup, which is the check after a protected classify.

Usage:
    python verify_tree.py
    python verify_tree.py --handed-back handed-back.json --compare <backup-dir>
"""
import argparse
import json
import subprocess
from pathlib import Path

DEFAULT_STORE = Path(r"E:\Working\Repos\ScatoloneQuintet\metadata")
FILES = ["fringe.json", "pool.json", "unrated.json"]


def cards_of(texts):
    out = {}
    for t in texts:
        out.update(json.loads(t).get("cards", {}))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--handed-back", help="JSON list of {oracleId}")
    ap.add_argument("--compare", help="backup directory holding the three tier files")
    ap.add_argument("--store", default=str(DEFAULT_STORE))
    args = ap.parse_args()

    meta = Path(args.store)
    head = cards_of(subprocess.run(["git", "show", f"HEAD:metadata/{f}"], cwd=meta.parent,
                                   capture_output=True, text=True, encoding="utf-8").stdout for f in FILES)
    tree = cards_of((meta / f).read_text(encoding="utf-8") for f in FILES)
    handed = set()
    if args.handed_back:
        handed = {e["oracleId"] for e in json.loads(Path(args.handed_back).read_text(encoding="utf-8"))}

    reviewed = [k for k in tree if tree[k] != head.get(k) and tree[k].get("reviewedAt")]
    unreviewed = [k for k in tree if tree[k] != head.get(k) and not tree[k].get("reviewedAt")]
    outside = [k for k in unreviewed if k not in handed]
    print(f"still uncommitted: {len(reviewed)} reviewed | NOT reviewed: {len(unreviewed)} "
          f"(outside the handed-back set: {len(outside)})")
    for k in outside[:10]:
        print("   NOT reviewed, differs from HEAD:", tree[k].get("name"))
    if handed:
        still = sum(1 for k in handed if k in tree and not tree[k].get("reviewedAt"))
        print(f"handed back, unreviewed in the tree: {still} of {len(handed)}")

    if args.compare:
        backup = cards_of((Path(args.compare) / f).read_text(encoding="utf-8") for f in FILES)
        moved = [k for k in tree if tree[k] != backup.get(k)]
        print(f"entries differing from {args.compare}: {len(moved)}")
        for k in moved[:30]:
            b, t = backup.get(k, {}), tree[k]
            print("  ", t.get("name"), sorted(x for x in set(b) | set(t) if b.get(x) != t.get(x)))
        if handed:
            same = sum(1 for k in handed if tree.get(k) == backup.get(k))
            print(f"handed-back entries identical to the backup: {same} of {len(handed)}")


if __name__ == "__main__":
    main()
