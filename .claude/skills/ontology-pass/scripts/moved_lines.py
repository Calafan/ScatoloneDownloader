"""Cards whose proposal for one tag moved between two dumps, with the lines that likely moved it.

blast_radius.py says which cards moved; this says WHY, one line per card, so a
false positive among a hundred right ones is seen at a glance (the God-Eternals
putting themselves into the library, a reminder text, a cost).

Usage:
    python moved_lines.py TAG [--before F] [--after F] [--reviewed] [--hint REGEX]

--before defaults to WORK_DIR/dump-before.jsonl and --after to WORK_DIR/dump.jsonl
(measure.py writes both). Unreviewed cards by default; --reviewed for the others.
--hint picks which lines of the text to print (default: the usual verbs).
"""
import argparse
import json
import re

from common import WORK_DIR, utf8_stdout


def load(path):
    with open(path, encoding="utf-8") as f:
        return {c["oracleId"]: c for c in map(json.loads, f)}


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("tag")
    ap.add_argument("--before", default=str(WORK_DIR / "dump-before.jsonl"))
    ap.add_argument("--after", default=str(WORK_DIR / "dump.jsonl"))
    ap.add_argument("--reviewed", action="store_true")
    ap.add_argument("--hint", default=r"destroy|exile|damage|get -|gets -|sacrific|return|discard|draw|counter|"
                                      r"prevent|copy|airbend|library|hand|gain|lose|search")
    args = ap.parse_args()
    hint = re.compile(args.hint, re.I)
    before, after = load(args.before), load(args.after)
    for oid, c in sorted(after.items(), key=lambda kv: kv[1]["name"]):
        if bool(c.get("reviewedAt")) != args.reviewed or oid not in before:
            continue
        was, now = args.tag in (before[oid]["auto"] or []), args.tag in (c["auto"] or [])
        if was == now:
            continue
        lines = [l for l in c["text"].split("\n") if hint.search(l)]
        print("%s %-36s %s" % ("+" if now else "-", c["name"][:36], " | ".join(l[:200] for l in lines)[:400]))


if __name__ == "__main__":
    main()
