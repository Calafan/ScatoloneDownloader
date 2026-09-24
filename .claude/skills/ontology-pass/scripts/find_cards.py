"""Look up cards in the metadata store by name: oracle id, tier, tags, reviewed.

This is what a ruling file is built from — writing one by hand means pasting
oracle ids, and a wrong id is a silent no-op or, worse, a tag written onto the
wrong card. Reads the store only; never writes.

Usage:
    python find_cards.py "Twiddle" "Riptide" "Word of Binding"
    python find_cards.py --file names.txt --json > rulings-draft.json
    python find_cards.py --grep "phases out"          # by oracle text instead

--json emits ruling stubs (op/effect left blank) ready to fill in.
"""

import argparse
import json
import re
import sys
from pathlib import Path

FILES = ["fringe.json", "pool.json", "unrated.json"]
DEFAULT_STORE = Path(r"E:\Working\Repos\ScatoloneQuintet\metadata")


def load(meta):
    cards = {}
    for name in FILES:
        data = json.loads((meta / name).read_text(encoding="utf-8"))
        for oracle, entry in data.get("cards", {}).items():
            cards[oracle] = (name, entry)
    return cards


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*")
    ap.add_argument("--file", help="file with one card name per line")
    ap.add_argument("--grep", help="regex over the stored card NAME instead of exact match")
    ap.add_argument("--json", action="store_true", help="emit ruling stubs")
    ap.add_argument("--store", default=str(DEFAULT_STORE))
    args = ap.parse_args()

    wanted = list(args.names)
    if args.file:
        wanted += [l.strip() for l in Path(args.file).read_text(encoding="utf-8").splitlines()
                   if l.strip() and not l.startswith("#")]

    cards = load(Path(args.store))
    hits, missing = [], []

    if args.grep:
        rx = re.compile(args.grep, re.IGNORECASE)
        hits = [(o, f, e) for o, (f, e) in cards.items() if rx.search(e.get("name", ""))]
    else:
        for name in wanted:
            found = [(o, f, e) for o, (f, e) in cards.items()
                     if e.get("name") == name or e.get("name", "").startswith(name + " //")]
            if found:
                hits.extend(found)
            else:
                missing.append(name)

    if args.json:
        print(json.dumps(
            [{"name": e.get("name"), "oracleId": o, "effect": "", "op": ""} for o, _, e in hits],
            indent=2, ensure_ascii=False))
    else:
        for oracle, tier, entry in sorted(hits, key=lambda h: h[2].get("name", "")):
            state = "reviewed" if entry.get("reviewedAt") else "NOT reviewed"
            print(f"{entry.get('name')}|{oracle}|{tier}|{entry.get('effects')}|{state}")

    for name in missing:
        print(f"!! not in store: {name}", file=sys.stderr)

    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
