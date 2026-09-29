"""Build the ruling files for apply_ruling.py from a short spec, instead of pasting oracle ids.

The human's answers come as card names and tags; this turns them into the two
files the store scripts take, looking every name up in the store and refusing
unless it matches exactly one entry — a wrong oracle id is a silent no-op, or a
tag written onto the wrong card.

Usage:
    python make_rulings.py spec.json [--out-dir DIR]

spec.json:
    {
      "name": "bounce",
      "named":   {"Airbender's Reversal": ["-Bounce", "+Protection"]},
      "reached": {"Airbender Ascension": ["+Bounce"]}
    }

"named" are the cards the human named: corrected, and they stay reviewed.
"reached" are the cards a rule reaches that were tagged the other way: corrected
AND handed back, so the human confirms them in the tagger (the 2026-09-25
method). An empty list of ops is allowed for a pure hand-back.

Writes, in --out-dir (default WORK_DIR):
    rulings-NAME.json    every tag op, for `apply_ruling.py --mode head`
    unreview-NAME.json   the reached cards, for the hand-back AFTER the classify
                         (store_pass.py handback), which also adds them to
                         state/handed-back.json
"""
import argparse
import sys
from pathlib import Path

from common import ORDER, WORK_DIR, load_store, read_json, utf8_stdout, write_json


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("spec")
    ap.add_argument("--out-dir", default=str(WORK_DIR))
    args = ap.parse_args()

    spec = read_json(args.spec)
    by_name = {}
    for oid, (_, entry) in load_store().items():
        by_name.setdefault(entry.get("name", ""), []).append(oid)

    rulings, unreview, problems = [], [], []
    for group in ("named", "reached"):
        for name, ops in (spec.get(group) or {}).items():
            ids = by_name.get(name, [])
            if len(ids) != 1:
                problems.append("%s: %d matches" % (name, len(ids)))
                continue
            for op in ops:
                sign, effect = op[0], op[1:]
                if sign not in "+-" or effect not in ORDER:
                    problems.append("%s: bad op %r" % (name, op))
                    continue
                rulings.append({"name": name, "oracleId": ids[0], "effect": effect,
                                "op": "add" if sign == "+" else "remove"})
            if group == "reached":
                unreview.append({"name": name, "oracleId": ids[0], "op": "unreview"})
    if problems:
        sys.exit("refused:\n  " + "\n  ".join(problems))

    out = Path(args.out_dir)
    write_json(out / ("rulings-%s.json" % spec["name"]), rulings)
    print("%d tag ops -> %s" % (len(rulings), out / ("rulings-%s.json" % spec["name"])))
    if unreview:
        write_json(out / ("unreview-%s.json" % spec["name"]), unreview)
        print("%d hand-backs -> %s" % (len(unreview), out / ("unreview-%s.json" % spec["name"])))


if __name__ == "__main__":
    main()
