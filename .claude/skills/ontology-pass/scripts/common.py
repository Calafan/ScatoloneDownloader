"""Paths and store helpers shared by the ontology-pass scripts added on 2026-09-29.

The older scripts carry their own copies of FILES and DEFAULT_STORE; new ones
import from here so the durable locations are named once.

Durable locations — none of them is the session scratchpad, which a new session
does not inherit:
  STATE_DIR     .claude/skills/ontology-pass/state/   (gitignored)
  HANDED_BACK   STATE_DIR/handed-back.json            the cards to protect in a classify
  WORK_DIR      $ONTOLOGY_DIR, else STATE_DIR/work     probes' input and output
"""
import json
import os
import subprocess
import sys
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parents[1]
CODE_REPO = SKILL_DIR.parents[2]
STORE_REPO = Path(r"E:\Working\Repos\ScatoloneQuintet")
STORE = STORE_REPO / "metadata"
FILES = ["fringe.json", "pool.json", "unrated.json"]
STATE_DIR = SKILL_DIR / "state"
HANDED_BACK = STATE_DIR / "handed-back.json"
WORK_DIR = Path(os.environ.get("ONTOLOGY_DIR") or STATE_DIR / "work")

# Effects are stored in ENUM DECLARATION ORDER, not alphabetically.
ORDER = ("Tokens Removal Counter RemovePermanent Wipe Bounce Ramp Disenchant Discard "
         "CardAdvantage Filter Reanimate Buff Protection Burn Sacrifice Steal Tutor "
         "ManaFixing Pacify LandDestruction Mill Regrowth Redirect Cheat").split()


def utf8_stdout():
    sys.stdout.reconfigure(encoding="utf-8")


def git(repo, *args):
    return subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True,
                          encoding="utf-8", check=True).stdout


def load_store(rev=None, store=STORE):
    """oracleId -> (file, entry). rev None reads the working tree; otherwise a git revision."""
    cards = {}
    for name in FILES:
        if rev is None:
            text = (Path(store) / name).read_text(encoding="utf-8")
        else:
            text = git(STORE_REPO, "show", "%s:metadata/%s" % (rev, name))
        for oid, entry in json.loads(text).get("cards", {}).items():
            cards[oid] = (name, entry)
    return cards


def load_dump(path=None):
    path = Path(path) if path else WORK_DIR / "dump.jsonl"
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f]


def read_json(path, default=None):
    path = Path(path)
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
