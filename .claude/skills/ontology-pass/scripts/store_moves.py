"""Which tags moved in the store, and on which cards — for a commit message that is measured, not recalled.

Usage:
    python store_moves.py                    # working tree against HEAD
    python store_moves.py --commit SHA       # one commit against its parent
    python store_moves.py ... --tag Bounce   # list the cards for one tag
    python store_moves.py ... --names        # list the cards for every tag

The summary line is what the "re-propose" commit message quotes; every card a
message names should appear in the listing. Run it on the commit after writing
the message, too: a card named in prose that did not move is a claim to fix.
"""
import argparse
import collections

from common import load_store, utf8_stdout


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("--commit")
    ap.add_argument("--tag")
    ap.add_argument("--names", action="store_true")
    args = ap.parse_args()

    before, after = (load_store(args.commit + "~1"), load_store(args.commit)) if args.commit \
        else (load_store("HEAD"), load_store())
    moves, cards = collections.defaultdict(list), 0
    for oid, (_, entry) in after.items():
        prev = before.get(oid, (None, {}))[1]
        if prev.get("effects") == entry.get("effects"):
            continue
        cards += 1
        a, b = set(prev.get("effects") or []), set(entry.get("effects") or [])
        for t in b - a:
            moves["+" + t].append(entry.get("name", ""))
        for t in a - b:
            moves["-" + t].append(entry.get("name", ""))
    print("%d cards; %s" % (cards, ", ".join("%s %d" % (k, len(v)) for k, v in
                                             sorted(moves.items(), key=lambda kv: -len(kv[1])))))
    for key, names in sorted(moves.items()):
        if args.names or (args.tag and key[1:] == args.tag):
            for n in sorted(names):
                print("  %s %s" % (key, n))


if __name__ == "__main__":
    main()
