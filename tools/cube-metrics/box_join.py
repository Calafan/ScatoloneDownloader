"""Join the user's box list with Scryfall oracle data and the metadata store.

Writes box.jsonl (one line per list entry) next to this script and prints the resolution summary.
Run with python -I: it reads a downloaded file.

    python -I box_join.py LIST BULK_GZ STORE_DIR OUT
"""
import gzip
import json
import re
import sys
from collections import Counter
from pathlib import Path

list_path, bulk_path, store_dir, out_path = sys.argv[1:5]

# --- the list: "-- Section" headers, then "Name<tabs>-- tag" lines
entries = []
section = None
for raw in Path(list_path).read_text(encoding="utf-8-sig").splitlines():
    line = raw.strip()
    if not line:
        continue
    m = re.match(r"^--\s*(.*)$", line)
    if m:
        head = m.group(1).strip()
        if not re.match(r"^\d", head) and head != "LOG":
            section = head
        continue
    name = re.split(r"\s+--", line, maxsplit=1)[0].strip()
    entries.append((section, name))

# --- Scryfall: one object per oracle id
by_name, by_face, by_lower = {}, {}, {}
skip_layouts = {"token", "double_faced_token", "art_series", "emblem", "vanguard", "scheme", "planar"}
with gzip.open(bulk_path, "rt", encoding="utf-8") as f:
    for line in f:
        line = line.strip().rstrip(",")
        if not line or line in "[]":
            continue
        c = json.loads(line)
        if c.get("layout") in skip_layouts:
            continue
        by_name.setdefault(c["name"], c)
        by_lower.setdefault(c["name"].lower(), c)
        for face in c.get("card_faces") or []:
            by_face.setdefault(face["name"], c)

# --- the store, by oracle id
store = {}
for tier in ("pool", "fringe", "unrated"):
    data = json.loads((Path(store_dir) / f"{tier}.json").read_text(encoding="utf-8-sig"))
    for oid, e in data.get("cards", {}).items():
        store[oid] = (tier, e)


def face_field(c, key):
    if c.get(key) is not None:
        return c.get(key)
    faces = c.get("card_faces") or []
    return faces[0].get(key) if faces else None


rows, missing = [], []
for section, name in entries:
    c = by_name.get(name) or by_face.get(name) or by_lower.get(name.lower())
    if c is None and re.search(r"\d$", name):
        bare = re.sub(r"\d+$", "", name)
        c = by_name.get(bare) or by_face.get(bare)
    if c is None:
        missing.append((section, name))
        rows.append({"section": section, "listName": name, "found": False})
        continue
    tier, e = store.get(c["oracle_id"], (None, {}))
    rows.append({
        "section": section, "listName": name, "found": True,
        "name": c["name"], "oracleId": c["oracle_id"], "layout": c.get("layout"),
        "cmc": c.get("cmc", 0), "type": c.get("type_line") or face_field(c, "type_line") or "",
        "frontType": face_field(c, "type_line") or "",
        "power": face_field(c, "power"), "toughness": face_field(c, "toughness"),
        "colors": c.get("colors") if c.get("colors") is not None else (face_field(c, "colors") or []),
        "identity": c.get("color_identity") or [], "keywords": c.get("keywords") or [],
        "text": c.get("oracle_text") or "\n".join(fc.get("oracle_text", "") for fc in c.get("card_faces") or []),
        "rarity": c.get("rarity"),
        "tier": tier, "rating": e.get("rating"), "status": e.get("status"),
        "effects": e.get("effects", []), "reviewed": bool(e.get("reviewedAt")),
    })

with open(out_path, "w", encoding="utf-8") as f:
    for r in rows:
        f.write(json.dumps(r, ensure_ascii=False) + "\n")

print("entries per section:", dict(Counter(s for s, _ in entries)))
print("missing on Scryfall:", len(missing), missing[:20])
main = [r for r in rows if r["section"] == "Scatolone" and r["found"]]
print("main found:", len(main), "dupes:", sum(v - 1 for v in Counter(r["oracleId"] for r in main).values() if v > 1))
print("main tiers:", dict(Counter(r["tier"] for r in main)))
print("main ratings:", dict(sorted(Counter(r["rating"] for r in main).items(), key=lambda kv: str(kv[0]))))
print("main statuses:", dict(Counter(r["status"] for r in main)))
print("main not in store:", [r["name"] for r in main if r["tier"] is None][:20])
