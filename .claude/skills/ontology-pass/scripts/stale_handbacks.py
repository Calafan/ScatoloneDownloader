"""Which handed-back cards are out of step with a ruling made after they were handed back?

A handed-back entry is frozen by restore_handed_back.py at its pre-classify state,
so a ruling made after the hand-back never reaches it until the human reviews it
(references/hazards.md, "A handed-back card reviewed against a ruling it never
saw"). Two questions, one per side of the review:

REVIEWED hand-backs — did the human confirm one WITHOUT seeing a tag the
classifier now gives? For every handed-back card reviewed in the tree whose hand
tags differ from today's proposal, it prints what the tagger showed on the first
save (`before`, from review-log.jsonl) and flags the tags the human never saw
proposed. Those are the questions; the other differences are the classifier's
misses, for the tag's own pass.

WAITING hand-backs, still unreviewed in the tree — what will the tagger show that
today's code no longer says? Every one whose frozen tags differ from today's
proposal, with the tags the code dropped (-) and added (+). Added 2026-11-03:
Fire Lord Ozai, Strago and Relm, Fallen Shinobi and Court of Locthwain were handed
back with Cheat by the Gríma ruling of that morning, the Cheat redefinition of the
afternoon overturned it, and the tagger kept showing Cheat. A difference is either
a later ruling the card never received — correct it in that ruling's file, so the
human reviews the right tags — or a hand ruling the classifier misses (a known
miss, Winter's Reanimate): read each one. Run it after every redefinition, with
--tag for the tag that moved.

Needs a fresh dump of the current code: probes.py Dump first.

Usage:
    python stale_handbacks.py              # never-shown tags, then the waiting cards
    python stale_handbacks.py --all        # every reviewed hand-back that differs, too
    python stale_handbacks.py --tag Cheat  # only differences on one tag
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
    ap.add_argument("--tag", help="only differences on this tag")
    args = ap.parse_args()

    handed_back = {x["oracleId"] for x in read_json(HANDED_BACK, [])}
    shown = first_shown(STORE / "review-log.jsonl")
    questions = 0
    waiting = []
    only = (lambda tags: tags & {args.tag}) if args.tag else (lambda tags: tags)

    for card in load_dump():
        if card["oracleId"] not in handed_back:
            continue
        human, auto = only(set(card.get("stored") or [])), only(set(card.get("auto") or []))
        if human == auto:
            continue
        if not card.get("reviewedAt"):
            waiting.append((card["name"], human, auto))
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

    print("\nwaiting in the tagger, frozen tags against today's proposal:")
    for name, frozen, auto in sorted(waiting):
        moved = [f"-{t}" for t in sorted(frozen - auto)] + [f"+{t}" for t in sorted(auto - frozen)]
        print(f"{name[:40]:40} frozen={','.join(sorted(frozen)) or '-'} | code now: {' '.join(moved)}")
    print(f"{len(waiting)} waiting hand-backs differ from today's proposal"
          + (f" on {args.tag}" if args.tag else ""))


if __name__ == "__main__":
    main()
