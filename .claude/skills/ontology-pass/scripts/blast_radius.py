"""What did a code change move? Compare two `Dump` probe outputs.

Take a dump BEFORE touching the classifier (stash the change, or dump first),
then one after. For every REVIEWED card whose automatic tags moved, each moved
tag is judged against the human's: RIGHT when the new proposal agrees with the
hand tag, WRONG when it disagrees. The WRONG list is the one to read — it is
either a rule that overreaches or an older hand tag the ruling overturns, and
only reading the cards tells which. Unreviewed cards are listed too: they are
the proposals the next classify will rewrite, the blast radius on the backlog.

Usage:
    python blast_radius.py dump-before.jsonl dump.jsonl
    python blast_radius.py dump-before.jsonl dump.jsonl --tag Buff --unreviewed
"""
import argparse
import collections
import json


def tags(value):
    if isinstance(value, str):
        return {t for t in value.split(", ") if t and t != "None"}
    return set(value or [])


def load(path):
    out = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            out[r["oracleId"]] = r
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("before")
    ap.add_argument("after")
    ap.add_argument("--tag", help="only changes to this effect")
    ap.add_argument("--unreviewed", action="store_true", help="list every unreviewed card that moved")
    args = ap.parse_args()

    a, b = load(args.before), load(args.after)
    right = wrong = 0
    summary = collections.Counter()
    judged, backlog = [], []

    for k, card in b.items():
        was, now = tags(a[k]["auto"]), tags(card["auto"])
        moved = sorted((was ^ now) & ({args.tag} if args.tag else was | now))
        if not moved:
            continue
        reviewed = bool(card.get("reviewedAt"))
        for t in moved:
            sign = "+" if t in now else "-"
            summary[("reviewed" if reviewed else "unreviewed", sign + t)] += 1
            if reviewed:
                ok = (t in now) == (t in tags(card["stored"]))
                right += ok
                wrong += not ok
                judged.append(f"{'RIGHT' if ok else 'WRONG'} {sign}{t:<16} {card['name']}")
            else:
                backlog.append(f"      {sign}{t:<16} {card['name']}")

    for (kind, move), n in sorted(summary.items()):
        print(f"{kind:<10} {move:<18} {n}")
    print(f"\nreviewed cards: {right} right, {wrong} wrong")
    for line in sorted(judged):
        print(line)
    if args.unreviewed:
        print(f"\nunreviewed proposals that move: {len(backlog)}")
        for line in sorted(backlog):
            print(line)


if __name__ == "__main__":
    main()
