"""The cards handed back for review — kept durably, and rebuilt from the store's git.

A card handed back (`op: "unreview"`) is unreviewed on purpose, carrying the
ruled tags, and every later `classify --overwrite` has to protect it until the
human confirms it in the tagger. The list used to live in the session
scratchpad, which a new session does not inherit; it lives in
state/handed-back.json now, and it can always be rebuilt from git, because
every hand-back is its own commit ("chore(metadata): hand … back for review")
and nothing else in this workflow ever removes a reviewedAt.

Usage:
    python handed_back.py                  # compare the state file with git, change nothing
    python handed_back.py --write          # (re)write the state file from git
    python handed_back.py --add FILE.json  # merge an unreview ruling file into the state file

The state file is a JSON list of {name, oracleId}, the shape the store scripts
take for --handed-back / --keep-head.
"""
import argparse
import re

from common import HANDED_BACK, STORE_REPO, git, load_store, read_json, utf8_stdout, write_json

HAND_BACK_SUBJECT = re.compile(r"^chore\(metadata\): hand\b")


def from_git():
    """Every card that lost reviewedAt in a hand-back commit and is still unreviewed now."""
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
    now = load_store()
    return sorted(({"name": name, "oracleId": oid} for oid, name in lost.items()
                   if oid in now and not now[oid][1].get("reviewedAt")), key=lambda c: c["name"])


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="rewrite the state file from git")
    ap.add_argument("--add", help="merge an unreview ruling file into the state file")
    args = ap.parse_args()

    state = read_json(HANDED_BACK, [])
    if args.add:
        known = {c["oracleId"] for c in state}
        extra = [{"name": c.get("name", ""), "oracleId": c["oracleId"]} for c in read_json(args.add, [])
                 if c["oracleId"] not in known]
        state = sorted(state + extra, key=lambda c: c["name"])
        write_json(HANDED_BACK, state)
        print("added %d, state file now %d" % (len(extra), len(state)))
        return

    rebuilt = from_git()
    have, want = {c["oracleId"] for c in state}, {c["oracleId"] for c in rebuilt}
    print("state file: %d | from git: %d | only in state: %d | only in git: %d"
          % (len(have), len(want), len(have - want), len(want - have)))
    for c in state:
        if c["oracleId"] not in want:
            print("   only in state (reviewed again, or not a hand-back):", c.get("name"))
    for c in rebuilt:
        if c["oracleId"] not in have:
            print("   only in git:", c["name"])
    if args.write:
        write_json(HANDED_BACK, rebuilt)
        print("wrote", HANDED_BACK)


if __name__ == "__main__":
    main()
