"""Rewrite the store as HEAD plus ONLY the entries that are not reviewed.

`classify --overwrite` writes its proposals into the working tree, where the
human's own in-progress reviewed entries also live. Those are never committed
by this tool — the human commits them when that year's pass is finished. So a
classifier run is split: this builds the tree the commit should see (HEAD's
reviewed entries, the classifier's proposals on everything else), and the
caller restores the human's working tree from its backup straight afterwards.

Usage:
    python split_unreviewed.py <backup-of-post-classify-metadata> [--store ...]

Refuses rather than guesses if anything but `effects` moved on an entry.
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

FILES = ["fringe.json", "pool.json", "unrated.json"]
DEFAULT_STORE = Path(r"E:\Working\Repos\ScatoloneQuintet\metadata")


def load_head(repo, name):
    blob = subprocess.run(["git", "show", f"HEAD:metadata/{name}"], cwd=repo,
                          capture_output=True, text=True, encoding="utf-8").stdout
    return json.loads(blob) if blob else {"version": 1, "cards": {}}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source", help="directory holding the post-classify tier files")
    ap.add_argument("--store", default=str(DEFAULT_STORE))
    # Cards HANDED BACK for review (apply_ruling.py op "unreview") are the
    # human's, not the classifier's: unreviewed, so classify rewrites them, and
    # often carrying the human's own rating, which makes this script refuse.
    # Their HEAD version is kept here, and the caller puts the human's tags back
    # in the working tree from the pre-classify backup.
    ap.add_argument("--keep-head", action="append", default=[],
                    help="JSON list of {oracleId, ...}: entries left exactly as HEAD has them")
    args = ap.parse_args()

    keep_head = set()
    for path in args.keep_head:
        keep_head |= {e["oracleId"] for e in json.loads(Path(path).read_text(encoding="utf-8"))}

    meta = Path(args.store)
    repo = meta.parent
    source_dir = Path(args.source)

    # Keyed across ALL THREE files, because the human moves a card between tiers
    # when they rate it, and that move is their work and not the classifier's:
    # the entry stays where HEAD has it and only its effects may travel.
    source_cards = {}
    for name in FILES:
        source_cards.update(json.loads((source_dir / name).read_text(encoding="utf-8")).get("cards", {}))

    moved = touched_reviewed = kept = 0

    for name in FILES:
        head = load_head(repo, name)
        head_cards = head.get("cards", {})

        for key in list(head_cards):
            entry = source_cards.get(key)
            if entry is None:
                continue

            if key in keep_head:
                kept += 1
                continue

            if entry == head_cards[key]:
                continue

            if entry.get("reviewedAt"):
                touched_reviewed += 1
                continue

            before, after = head_cards[key], entry
            if {k: v for k, v in before.items() if k != "effects"} != \
               {k: v for k, v in after.items() if k != "effects"}:
                raise SystemExit(f"{name}/{key}: a field other than effects changed — refusing")

            head_cards[key] = after
            moved += 1

        text = json.dumps(head, indent=2, ensure_ascii=False).replace("\n", "\r\n")
        (meta / name).write_text(text, encoding="utf-8", newline="")

    print(f"moved onto HEAD: {moved} | reviewed left untouched: {touched_reviewed}"
          + (f" | handed-back kept as HEAD: {kept}" if keep_head else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
