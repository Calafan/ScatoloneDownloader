"""Oracle text, stored and proposed tags, and review date for cards by name.

Reads the Dump probe's dump.jsonl, so it answers in a moment and needs no
build. The quickest way to read the cards behind a count or a blast radius.

Usage:
    python show.py "Name" ["Name" ...] [--exact] [--dump PATH]

Names are prefix matches, case-insensitive, unless --exact.
"""
import argparse

from common import load_dump, utf8_stdout


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="+")
    ap.add_argument("--exact", action="store_true")
    ap.add_argument("--dump")
    args = ap.parse_args()
    dump = load_dump(args.dump)
    for want in args.names:
        hits = [c for c in dump if (c["name"] == want if args.exact else c["name"].lower().startswith(want.lower()))]
        if not hits:
            print("?? " + want)
        for c in hits:
            print("### %s  [%s]  reviewed=%s" % (c["name"], c["type"], (c.get("reviewedAt") or "-")[:10]))
            print("    human: %s | auto: %s" % (",".join(c["stored"] or []), ",".join(c["auto"] or [])))
            for line in c["text"].split("\n"):
                print("    " + line)


if __name__ == "__main__":
    main()
