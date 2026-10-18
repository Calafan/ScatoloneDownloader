"""Write the swap proposal as one Markdown file the user can read and tick.

    python -I proposal_md.py BOX_JSONL BULK_GZ STORE_DIR OUT_MD
"""
import gzip
import json
import sys
from pathlib import Path

box_path, bulk_path, store_dir, out_md = sys.argv[1:5]
HARD = {"Removal", "RemovePermanent", "Wipe"}

box = [json.loads(l) for l in open(box_path, encoding="utf-8")]
box = [r for r in box if r["section"] == "Scatolone" and r["found"]]
box_ids = {r["oracleId"] for r in box}


def face(c, key):
    if c.get(key) is not None:
        return c.get(key)
    faces = c.get("card_faces") or []
    return faces[0].get(key) if faces else None


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


oracle = {}
with gzip.open(bulk_path, "rt", encoding="utf-8") as f:
    for line in f:
        line = line.strip().rstrip(",")
        if line and line not in "[]":
            c = json.loads(line)
            oracle[c["oracle_id"]] = c

cut_a = [r for r in box if r.get("rating") in (1, 2) and HARD & set(r["effects"])]
review_b = [r for r in box if r.get("rating") in (0, None) and "Wipe" in r["effects"]]
adds = []
data = json.loads((Path(store_dir) / "pool.json").read_text(encoding="utf-8-sig"))
for oid, e in data.get("cards", {}).items():
    c = oracle.get(oid)
    if oid in box_ids or c is None or e.get("status"):
        continue
    t = face(c, "type_line") or ""
    if "Creature" in t and c.get("cmc", 99) <= 2:
        adds.append({"name": c["name"], "cmc": c.get("cmc", 0), "type": t, "rating": e.get("rating"),
                     "pt": f"{face(c, 'power')}/{face(c, 'toughness')}", "effects": e.get("effects", []),
                     "power": num(face(c, "power"))})


def line(r, extra=""):
    tags = ",".join(r["effects"]) or "-"
    return f"- [ ] {r['name']} — MV {r['cmc']:g}, {r.get('frontType') or r.get('type')}{extra} — {tags}"


out = ["# Scatolone: proposta di scambio (2026-10-18)", "",
       "Obiettivo scelto: a metà strada fra il box di oggi e i set retail. Misurato sulla sezione",
       "Scatolone di `_ToBe_Scatolone.txt` (4164 carte); tag dallo store (revisionati o proposte).", "",
       "Con A tagliato, i Wipe senza rating da costo 5+ tagliati e le aggiunte C dentro:",
       "rimozione dura 24.7% -> 20.1% (obiettivo 19.8), Wipe 5.1% -> 3.1% (3.0), creature costo <=2",
       "9.9% -> 14.8% (15.7), costo medio 3.7 -> 3.5 (3.3).", ""]

out += [f"## A. Da tagliare: rating 1-2 con rimozione dura o Wipe ({len(cut_a)})", "",
        "Il tuo stesso rating le mette in panchina. Wipe prima, poi per costo decrescente.", ""]
out += [line(r, f", rating {r['rating']}")
        for r in sorted(cut_a, key=lambda r: ("Wipe" not in r["effects"], -r["cmc"], r["name"]))]

out += ["", f"## B. Da valutare: Wipe senza rating ({len(review_b)})", "",
        f"Per l'obiettivo ne vanno via circa 57; i {sum(r['cmc'] >= 5 for r in review_b)} da costo 5+ sono i primi candidati.", ""]
out += [line(r) for r in sorted(review_b, key=lambda r: (-r["cmc"], r["name"]))]

out += ["", f"## C. Da aggiungere: creature costo <=2, rating 3-5, non nel box ({len(adds)})", "",
        f"{sum(r['power'] >= 2 for r in adds)} hanno forza 2 o più. Per rating, poi forza decrescente.", ""]
out += [f"- [ ] {r['name']} — MV {r['cmc']:g}, {r['pt']}, rating {r['rating']} — {','.join(r['effects']) or '-'}"
        for r in sorted(adds, key=lambda r: (-(r['rating'] or 0), -r["power"], r["cmc"], r["name"]))]

Path(out_md).write_text("\n".join(out) + "\n", encoding="utf-8")
print(out_md, len(cut_a), len(review_b), len(adds))
