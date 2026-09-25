"""What did the human change while reviewing? Read from the tagger's review log.

When the human comes back from a review sitting saying "found some errors",
this is the first thing to run: for every card saved since a given instant it
compares what the reviewer was SHOWN on the first save (`before`, the
classifier's proposal or a handed-back card's ruled tags) with what they LEFT
on the last one (`after`). Every tag they added or removed is a disagreement
with the rules — the exact evidence, not a reconstruction from the store.

Usage:
    python review_changes.py                       # since the store's last commit
    python review_changes.py --since 2026-09-25T07:30 --tag Buff
    python review_changes.py --tag Buff --handed-back handed-back.json

--since defaults to the time of HEAD in the store repo. Timestamps in the log
are UTC; give --since in UTC too.
"""
import argparse
import collections
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

DEFAULT_STORE = Path(r"E:\Working\Repos\ScatoloneQuintet\metadata")


def head_time(repo):
    iso = subprocess.run(["git", "log", "-1", "--format=%cI"], cwd=repo,
                         capture_output=True, text=True).stdout.strip()
    return datetime.fromisoformat(iso).astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--since", help="UTC instant, e.g. 2026-09-25T07:30")
    ap.add_argument("--tag", help="list the cards whose change touches this effect")
    ap.add_argument("--handed-back", help="JSON list of {oracleId}: marks cards handed back for review")
    ap.add_argument("--store", default=str(DEFAULT_STORE))
    args = ap.parse_args()

    meta = Path(args.store)
    since = args.since or head_time(meta.parent)
    log = meta / "review-log.jsonl"
    # The human clears the log between sittings, so its absence is normal: it
    # means nothing has been reviewed since, not that something broke.
    if not log.exists():
        raise SystemExit(f"no review log at {log}: nothing reviewed since it was last cleared")
    rows = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines() if line.strip()]
    rows = [r for r in rows if r["at"] >= since]

    handed = set()
    if args.handed_back:
        handed = {e["oracleId"] for e in json.loads(Path(args.handed_back).read_text(encoding="utf-8"))}

    first, last = {}, {}
    for r in rows:
        first.setdefault(r["oracleId"], r)
        last[r["oracleId"]] = r

    print(f"since {since} UTC: {len(first)} cards saved"
          + (f", {len(set(first) & handed)} of them handed back" if handed else ""))

    moves = collections.Counter()
    for k in first:
        shown, left = set(first[k]["before"]), set(last[k]["after"])
        moves.update("-" + t for t in shown - left)
        moves.update("+" + t for t in left - shown)
    print("tags the human changed:", ", ".join(f"{m} {n}" for m, n in moves.most_common()) or "none")

    if args.tag:
        print(f"\n{args.tag}:")
        for k in first:
            shown, left = set(first[k]["before"]), set(last[k]["after"])
            if (args.tag in shown) != (args.tag in left):
                sign = "-" if args.tag in shown else "+"
                mark = "HB " if k in handed else "   "
                print(f"  {mark}{sign}{args.tag}  {first[k]['name']}  {sorted(shown)} -> {sorted(left)}")


if __name__ == "__main__":
    main()
