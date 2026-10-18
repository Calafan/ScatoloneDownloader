"""Where the cuts and the adds can come from.

    python -I candidates.py BOX_JSONL BULK_GZ STORE_DIR OUT_DIR

Cuts: the box's Wipe and hard-removal cards by rating group and mana value.
Adds: cheap creatures (MV<=2) in the store but not in the box, by rating, status None.
Writes cuts-wipe.tsv, cuts-removal.tsv and adds-cheap.tsv to OUT_DIR for reading.
"""
import gzip
import json
import sys
from collections import Counter
from pathlib import Path

box_path, bulk_path, store_dir, out_dir = sys.argv[1:5]
out = Path(out_dir)
HARD = {"Removal", "RemovePermanent", "Wipe"}

box = [json.loads(l) for l in open(box_path, encoding="utf-8")]
box = [r for r in box if r["section"] == "Scatolone" and r["found"]]
box_ids = {r["oracleId"] for r in box}
nonland = [r for r in box if "Land" not in r["frontType"] or "Creature" in r["frontType"]]


def group(r):
    rt = r.get("rating")
    return "unrated" if rt in (0, None) else ("1-2" if rt <= 2 else str(rt))


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def table(rows, title):
    print(f"== {title}: {len(rows)}")
    c = Counter((group(r), "MV<=3" if r["cmc"] <= 3 else ("MV4" if r["cmc"] == 4 else "MV5+")) for r in rows)
    for g in ("1-2", "unrated", "3", "4", "5"):
        print(f"   rating {g:8}" + "".join(f"  {mv} {c[(g, mv)]:>4}" for mv in ("MV<=3", "MV4", "MV5+"))
              + f"   total {sum(c[(g, mv)] for mv in ('MV<=3', 'MV4', 'MV5+')):>4}")


def write(rows, name):
    order = {"1-2": 0, "unrated": 1, "3": 2, "4": 3, "5": 4}
    with open(out / name, "w", encoding="utf-8") as f:
        f.write("rating\tMV\tname\ttype\teffects\treviewed\n")
        for r in sorted(rows, key=lambda r: (order[group(r)], -r["cmc"], r["name"])):
            f.write(f"{group(r)}\t{r['cmc']:g}\t{r['name']}\t{r['frontType']}\t{','.join(r['effects'])}\t"
                    f"{'R' if r.get('reviewed') else '-'}\n")


wipes = [r for r in nonland if "Wipe" in r["effects"]]
spot = [r for r in nonland if HARD & set(r["effects"]) and "Wipe" not in r["effects"]]
table(wipes, "box Wipe")
table(spot, "box hard removal without Wipe")
write(wipes, "cuts-wipe.tsv")
write(spot, "cuts-removal.tsv")

# --- adds: the store's cheap creatures that are not in the box
oracle = {}
with gzip.open(bulk_path, "rt", encoding="utf-8") as f:
    for line in f:
        line = line.strip().rstrip(",")
        if line and line not in "[]":
            c = json.loads(line)
            oracle[c["oracle_id"]] = c


def face(c, key):
    if c.get(key) is not None:
        return c.get(key)
    faces = c.get("card_faces") or []
    return faces[0].get(key) if faces else None


adds = []
for tier in ("pool", "fringe", "unrated"):
    data = json.loads((Path(store_dir) / f"{tier}.json").read_text(encoding="utf-8-sig"))
    for oid, e in data.get("cards", {}).items():
        c = oracle.get(oid)
        if oid in box_ids or c is None or e.get("status"):
            continue
        t = face(c, "type_line") or ""
        if "Creature" not in t or c.get("cmc", 99) > 2:
            continue
        adds.append({"name": c["name"], "cmc": c.get("cmc", 0), "frontType": t, "rating": e.get("rating", 0),
                     "effects": e.get("effects", []), "reviewed": bool(e.get("reviewedAt")),
                     "power": face(c, "power")})

rc = Counter(group(r) for r in adds)
aggro = Counter(group(r) for r in adds if num(r["power"]) >= 2)
print(f"== store creatures MV<=2 not in the box, status None: {len(adds)}")
for g in ("5", "4", "3", "1-2", "unrated"):
    print(f"   rating {g:8} {rc[g]:>5}   of which power>=2 {aggro[g]:>5}")
write(adds, "adds-cheap.tsv")
