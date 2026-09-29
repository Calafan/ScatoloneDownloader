"""Count wording families against one tag before writing any rule.

Step 4 of the pass ("count every family before writing it"), answered from the
Dump probe's dump.jsonl in a second instead of a build: for each family, how
many REVIEWED cards carry the tag and how many do not — each listed with its
review date and the human's tags, which is where a split and its contradicting
pair show up — and how many UNREVIEWED cards match and how many of those the
classifier already proposes.

Usage:
    python families.py TAG families.txt [--only PREFIX ...] [--keep-reminders] [--dump PATH]

families.txt holds one family per line, "name<TAB>regex" — the format of the
Count probe's hypotheses.txt. Lines starting with # are skipped. A regex
starting with "(?-i)" is CASE-SENSITIVE (a creature type is the capitalised
word); every other regex ignores case. Reminder text in parentheses is blanked
unless --keep-reminders, because reminder text is never an effect the card has.

Reading the result: a clean majority is a rule, a near 50/50 is a question for
the human with the contradicting pair named, a clean minority is a veto.
"""
import argparse
import re

from common import load_dump, utf8_stdout

PAREN = re.compile(r"\([^()]*\)")


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("tag")
    ap.add_argument("families")
    ap.add_argument("--only", nargs="*", default=[], help="run only the families whose name starts so")
    ap.add_argument("--keep-reminders", action="store_true")
    ap.add_argument("--dump")
    args = ap.parse_args()

    dump = load_dump(args.dump)
    for raw in open(args.families, encoding="utf-8-sig"):
        raw = raw.rstrip("\n")
        if not raw.strip() or raw.startswith("#"):
            continue
        name, rx = raw.split("\t", 1)
        if args.only and not any(name.startswith(p) for p in args.only):
            continue
        flags = re.M
        if rx.startswith("(?-i)"):
            rx = rx[5:]
        else:
            flags |= re.I
        r = re.compile(rx, flags)
        tagged, untagged, unrev, unrev_t = [], [], 0, 0
        for c in dump:
            text = c["text"] if args.keep_reminders else PAREN.sub(" ", c["text"])
            if not r.search(text):
                continue
            if c.get("reviewedAt"):
                (tagged if args.tag in (c["stored"] or []) else untagged).append(c)
            else:
                unrev += 1
                unrev_t += args.tag in (c["auto"] or [])
        print("\n== %s: reviewed %d tagged / %d untagged; unreviewed %d (proposed %d)"
              % (name, len(tagged), len(untagged), unrev, unrev_t))
        for c in tagged:
            print("   +  %s %-34s human=%s" % (c["reviewedAt"][5:10], c["name"][:34], ",".join(c["stored"] or [])))
        for c in untagged:
            print("   -- %s %-34s human=%s" % (c["reviewedAt"][5:10], c["name"][:34], ",".join(c["stored"] or [])))


if __name__ == "__main__":
    main()
