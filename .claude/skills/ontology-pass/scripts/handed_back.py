"""The cards handed back for review — kept durably, and rebuilt from the store's git.

A card handed back (`op: "unreview"`) is unreviewed on purpose, carrying the
ruled tags, and every later `classify --overwrite` has to protect it until the
human confirms it in the tagger. The list used to live in the session
scratchpad, which a new session does not inherit; it lives in
state/handed-back.json now, and it can always be rebuilt from git, because
every hand-back is its own commit ("chore(metadata): hand … back for review")
and nothing else in this workflow ever removes a reviewedAt.

Git finds only the hand-backs still waiting, so the state file holds more than
git, on purpose (2026-11-25):
  - cards the human has reviewed since: stale_handbacks.py reads them to ask
    whether one was confirmed without seeing a tag the classifier now gives;
  - cards handed back that were never reviewed before (Chandra, Acolyte of
    Flame; Eye Spy; Overwhelmed Apprentice): they lost no reviewedAt, so git
    cannot see them; they came in with --add.
--write therefore only adds what git finds and the file lacks; it never removes
an entry. It used to replace the file with git's list, which would have dropped
both groups.

Usage:
    python handed_back.py                  # compare the state file with git, change nothing
    python handed_back.py --write          # add the hand-backs git finds and the state file lacks
    python handed_back.py --add FILE.json  # merge an unreview ruling file into the state file

The state file is a JSON list of {name, oracleId}, the shape the store scripts
take for --handed-back / --keep-head.
"""
import argparse
import re

from common import HANDED_BACK, STORE_REPO, git, load_store, read_json, utf8_stdout, write_json

# Two subject shapes: "hand back the N cards ..." / "hand X back for review", and, since the
# Redirect and Ramp passes of 2026-11-09, "eight cards handed back, the Ramp definition ...".
# Missing the second dropped ten waiting cards from the rebuild on 2026-11-10. A commit that
# only REVIEWS cards may say "handed back" too; it is scanned and finds no lost reviewedAt.
HAND_BACK_SUBJECT = re.compile(r"^chore\(metadata\): (hand\b|.*\bhanded back\b)")


def from_git(now):
    """Every card that lost reviewedAt in a hand-back commit and is still unreviewed in `now`."""
    lost = {}
    for line in git(STORE_REPO, "log", "--format=%H%x09%s", "--", "metadata/").splitlines():
        sha, subject = line.split("\t", 1)
        if not HAND_BACK_SUBJECT.match(subject):
            continue
        before, after = load_store(sha + "~1"), load_store(sha)
        for oid, (_, entry) in after.items():
            prev = before.get(oid)
            if prev and prev[1].get("reviewedAt") and not entry.get("reviewedAt"):
                lost[oid] = entry.get("name", "")
    return sorted(({"name": name, "oracleId": oid} for oid, name in lost.items()
                   if oid in now and not now[oid][1].get("reviewedAt")), key=lambda c: c["name"])


def merge(state, cards):
    """The state file plus the cards it lacks, sorted by name; and how many were added."""
    known = {c["oracleId"] for c in state}
    extra = [{"name": c.get("name", ""), "oracleId": c["oracleId"]} for c in cards if c["oracleId"] not in known]
    return sorted(state + extra, key=lambda c: c["name"]), len(extra)


def split_by_review(cards, now):
    """The cards grouped by their state in the store's working tree: reviewed, waiting, not in store."""
    reviewed, waiting, missing = [], [], []
    for c in cards:
        entry = now.get(c["oracleId"])
        if entry is None:
            missing.append(c)
        elif entry[1].get("reviewedAt"):
            reviewed.append(c)
        else:
            waiting.append(c)
    return reviewed, waiting, missing


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="add the hand-backs git finds and the state file lacks")
    ap.add_argument("--add", help="merge an unreview ruling file into the state file")
    args = ap.parse_args()

    state = read_json(HANDED_BACK, [])
    if args.add:
        state, added = merge(state, read_json(args.add, []))
        write_json(HANDED_BACK, state)
        print("added %d, state file now %d" % (added, len(state)))
        return

    now = load_store()
    rebuilt = from_git(now)
    have, want = {c["oracleId"] for c in state}, {c["oracleId"] for c in rebuilt}
    reviewed, waiting, missing = split_by_review([c for c in state if c["oracleId"] not in want], now)
    only_in_git = [c for c in rebuilt if c["oracleId"] not in have]
    print("state file: %d | from git: %d | only in git: %d" % (len(state), len(rebuilt), len(only_in_git)))
    print("only in state: %d = reviewed since %d (kept, stale_handbacks.py reads them)"
          " + waiting %d (never reviewed before the hand-back, git cannot see them) + not in store %d"
          % (len(reviewed) + len(waiting) + len(missing), len(reviewed), len(waiting), len(missing)))
    for c in waiting:
        print("   waiting, not from git:", c.get("name"))
    for c in missing:
        print("   not in store (re-keyed or removed?):", c.get("name"))
    for c in only_in_git:
        print("   only in git:", c["name"])
    if args.write:
        state, added = merge(state, rebuilt)
        write_json(HANDED_BACK, state)
        print("added %d, wrote %s (%d)" % (added, HANDED_BACK, len(state)))


if __name__ == "__main__":
    main()
