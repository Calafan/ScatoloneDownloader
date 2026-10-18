"""The box after a candidate swap, measured with speed_metrics' own metrics.

    python -I simulate.py BOX_JSONL BULK_GZ STORE_DIR SETS_DIR

Cut A: every box card rated 1-2 that is Wipe or hard removal (the user's own rating says bench).
Cut B: the unrated Wipes at MV 5+.
Add:   the store's creatures at MV<=2 rated 3-5, status None, not in the box.
"""
import gzip
import importlib.util
import json
import sys
from pathlib import Path

box_path, bulk_path, store_dir, sets_dir = sys.argv[1:5]
here = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("sm", here / "speed_metrics.py")
sys.argv = [sys.argv[0], box_path, sets_dir, store_dir]
sm = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sm)  # prints the base table once; the swap follows

HARD = {"Removal", "RemovePermanent", "Wipe"}
box = [r for r in sm.box]
cut_a = [r for r in box if r.get("rating") in (1, 2) and HARD & set(r["effects"])]
cut_b = [r for r in box if r.get("rating") in (0, None) and "Wipe" in r["effects"] and r["cmc"] >= 5]
cut_ids = {r["oracleId"] for r in cut_a + cut_b}
box_ids = {r["oracleId"] for r in box}

oracle = {}
with gzip.open(bulk_path, "rt", encoding="utf-8") as f:
    for line in f:
        line = line.strip().rstrip(",")
        if line and line not in "[]":
            c = json.loads(line)
            oracle[c["oracle_id"]] = c

adds = []
for tier in ("pool",):
    data = json.loads((Path(store_dir) / f"{tier}.json").read_text(encoding="utf-8-sig"))
    for oid, e in data.get("cards", {}).items():
        c = oracle.get(oid)
        if oid in box_ids or c is None or e.get("status"):
            continue
        row = sm.from_scryfall(c, 1.0)
        if "Creature" in row["frontType"] and row["cmc"] <= 2:
            adds.append(row)

after = [r for r in box if r["oracleId"] not in cut_ids] + adds
print(f"\ncut A {len(cut_a)} | cut B {len(cut_b)} | add {len(adds)} | box {len(box)} -> {len(after)}")
before_m, after_m, retail_m = sm.metrics(box), sm.metrics(after), sm.metrics(sm.retail)
print(f"{'metric':32}{'box now':>12}{'after swap':>12}{'halfway':>12}{'retail':>12}")
for k in before_m:
    print(f"{k:32}{before_m[k]:>12.1f}{after_m[k]:>12.1f}{(before_m[k] + retail_m[k]) / 2:>12.1f}{retail_m[k]:>12.1f}")
