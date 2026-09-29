"""Which handed-back cards did the human confirm WITHOUT seeing a tag the classifier now gives?

A handed-back entry is frozen by restore_handed_back.py at its pre-classify state,
so a ruling made after the hand-back never reaches it until the human reviews it
(references/hazards.md, "A handed-back card reviewed against a ruling it never
saw"). Run this after a control run: for every handed-back card reviewed in the
tree whose hand tags differ from today's proposal, it prints what the tagger
showed on the first save (`before`, from review-log.jsonl) and flags the tags the
human never saw proposed. Those are the questions; the other differences are the
classifier's misses, for the tag's own pass.

Needs a fresh dump of the current code: probes.py Dump first.

Usage:
    python stale_handbacks.py            # only the cards with a tag never shown
    python stale_handbacks.py --all      # every reviewed hand-back that differs
"""
import argparse
import json

from common import HANDED_BACK, STORE, load_dump, read_json, utf8_stdout


def first_shown(log_path):
    """oracleId -> the tags the tagger showed on the card's first review."""
    shown = {}
    if not log_path.exists():
        return shown
    with open(log_path, encoding="utf-8") as f:
        for line in f:
            row = json.loads(line)
            if row.get("firstReview") and row["oracleId"] not in shown:
                shown[row["oracleId"]] = set(row.get("before") or [])
    return shown


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true", help="also the cards whose difference was shown")
    args = ap.parse_args()

    handed_back = {x["oracleId"] for x in read_json(HANDED_BACK, [])}
    shown = first_shown(STORE / "review-log.jsonl")
    questions = 0

    for card in load_dump():
        if card["oracleId"] not in handed_back or not card.get("reviewedAt"):
            continue
        human, auto = set(card.get("stored") or []), set(card.get("auto") or [])
        if human == auto:
            continue
        before = shown.get(card["oracleId"], set())
        never_shown = sorted(t for t in auto - human if t not in before)
        if never_shown:
            questions += 1
        elif not args.all:
            continue
        print(f"{card['name'][:40]:40} human={','.join(sorted(human)) or '-'}"
              f" | proposed now={','.join(sorted(auto)) or '-'}"
              f" | shown={','.join(sorted(before)) or '-'}"
              + (f" | NEVER SHOWN: {', '.join(never_shown)}" if never_shown else ""))

    print(f"\n{questions} reviewed hand-backs with a tag the human never saw proposed")


if __name__ == "__main__":
    main()
