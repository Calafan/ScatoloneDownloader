"""Speed and interaction metrics: the box against retail draft sets as opened.

    python -I speed_metrics.py BOX_JSONL SETS_DIR STORE_DIR

Box cards weigh 1 each (a pack is a random draw from the box). A retail set's
card weighs its rarity's booster slots over the cards at that rarity (common 7,
uncommon 3, rare 7/8, mythic 1/8 of the rare slot): the set as it is opened.
Effects come from the store (reviewed tags, else the classifier's proposal).
"""
import json
import re
import sys
from pathlib import Path

box_path, sets_dir, store_dir = sys.argv[1:4]

store = {}
for tier in ("pool", "fringe", "unrated"):
    data = json.loads((Path(store_dir) / f"{tier}.json").read_text(encoding="utf-8-sig"))
    for oid, e in data.get("cards", {}).items():
        store[oid] = e

HARD = {"Removal", "RemovePermanent", "Wipe"}
SOFT = {"Pacify", "Bounce", "Steal"}
INTERACTION = HARD | SOFT | {"Counter", "Disenchant", "LandDestruction", "Discard"}
EVASION = {"Flying", "Menace", "Trample", "Shadow", "Horsemanship", "Fear", "Intimidate", "Skulk"}
LIFEGAIN = re.compile(r"\bgains? (?:\d+|X|that much|life equal)|\byou gain\b")
FOG = re.compile(r"prevent all combat damage", re.I)


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def face(c, key):
    if c.get(key) is not None:
        return c.get(key)
    faces = c.get("card_faces") or []
    return faces[0].get(key) if faces else None


def from_scryfall(c, weight):
    e = store.get(c["oracle_id"], {})
    text = c.get("oracle_text") or "\n".join(f.get("oracle_text", "") for f in c.get("card_faces") or [])
    return {"cmc": c.get("cmc", 0), "frontType": face(c, "type_line") or "", "power": face(c, "power"),
            "toughness": face(c, "toughness"), "keywords": c.get("keywords") or [], "text": text,
            "effects": e.get("effects", []), "inStore": c["oracle_id"] in store, "w": weight}


def metrics(cards):
    W = sum(c["w"] for c in cards)
    nonland = [c for c in cards if "Land" not in c["frontType"] or "Creature" in c["frontType"]]
    N = sum(c["w"] for c in nonland)

    def share(pred, pool=None):
        pool = nonland if pool is None else pool
        return 100.0 * sum(c["w"] for c in pool if pred(c)) / N

    creature = lambda c: "Creature" in c["frontType"]
    eff = lambda c: set(c["effects"])
    creatures = [c for c in nonland if creature(c)]
    CW = sum(c["w"] for c in creatures)
    m = {
        "cards": round(W, 1),
        "lands % of all": 100.0 * (W - N) / W,
        "creatures": share(creature),
        "planeswalkers": share(lambda c: "Planeswalker" in c["frontType"]),
        "instants+sorceries": share(lambda c: "Instant" in c["frontType"] or "Sorcery" in c["frontType"]),
        "avg MV nonland": sum(c["cmc"] * c["w"] for c in nonland) / N,
        "avg MV creatures": sum(c["cmc"] * c["w"] for c in creatures) / CW,
        "MV 0-1": share(lambda c: c["cmc"] <= 1),
        "MV 2": share(lambda c: c["cmc"] == 2),
        "MV 3": share(lambda c: c["cmc"] == 3),
        "MV 4": share(lambda c: c["cmc"] == 4),
        "MV 5": share(lambda c: c["cmc"] == 5),
        "MV 6+": share(lambda c: c["cmc"] >= 6),
        "creatures MV<=2": share(lambda c: creature(c) and c["cmc"] <= 2),
        "aggro 2-drop (MV<=2, P>=2)": share(lambda c: creature(c) and c["cmc"] <= 2 and (num(c["power"]) or 0) >= 2),
        "MV<=3 with P>=3": share(lambda c: creature(c) and c["cmc"] <= 3 and (num(c["power"]) or 0) >= 3),
        "haste creatures": share(lambda c: creature(c) and "Haste" in c["keywords"]),
        "evasive creatures": share(lambda c: creature(c) and (EVASION & set(c["keywords"])
                                                               or "can't be blocked" in c["text"])),
        "defenders": share(lambda c: creature(c) and "Defender" in c["keywords"]),
        "hard creature removal": share(lambda c: bool(HARD & eff(c))),
        "  of which Wipe": share(lambda c: "Wipe" in eff(c)),
        "soft creature answers": share(lambda c: bool(SOFT & eff(c))),
        "counterspells": share(lambda c: "Counter" in eff(c)),
        "disenchant": share(lambda c: "Disenchant" in eff(c)),
        "discard": share(lambda c: "Discard" in eff(c)),
        "any interaction": share(lambda c: bool(INTERACTION & eff(c))),
        "card advantage": share(lambda c: "CardAdvantage" in eff(c)),
        "lifegain (text/lifelink)": share(lambda c: "Lifelink" in c["keywords"] or bool(LIFEGAIN.search(c["text"]))),
        "fogs": share(lambda c: bool(FOG.search(c["text"]))),
        "untagged": share(lambda c: not c["effects"]),
    }
    m["creatures per hard answer"] = m["creatures"] / m["hard creature removal"]
    m["creatures per any interaction"] = m["creatures"] / m["any interaction"]
    return m


# --- the box
box = []
with open(box_path, encoding="utf-8") as f:
    for line in f:
        r = json.loads(line)
        if r["section"] == "Scatolone" and r["found"]:
            r["w"] = 1.0
            r["inStore"] = r["tier"] is not None
            box.append(r)

# --- retail sets as opened
SLOTS = {"common": 7.0, "uncommon": 3.0, "rare": 7.0 / 8, "mythic": 1.0 / 8}
retail, coverage = [], []
for path in sorted(Path(sets_dir).glob("*.jsonl")):
    cards = [json.loads(l) for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]
    counts = {}
    for c in cards:
        counts[c["rarity"]] = counts.get(c["rarity"], 0) + 1
    rows = [from_scryfall(c, SLOTS[c["rarity"]] / counts[c["rarity"]]) for c in cards if c["rarity"] in SLOTS]
    coverage.append(f"{path.stem} {sum(r['inStore'] for r in rows)}/{len(rows)}")
    retail.extend(rows)

columns = {
    "box (all 4164)": box,
    "box rating 3-5": [c for c in box if (c.get("rating") or 0) >= 3],
    "box rating 1-2": [c for c in box if c.get("rating") in (1, 2)],
    "box unrated": [c for c in box if c.get("rating") in (0, None)],
    "retail 9 sets": retail,
}
results = {k: metrics(v) for k, v in columns.items()}
print("retail store coverage:", ", ".join(coverage))
print("box in store:", sum(c["inStore"] for c in box), "/", len(box), "| reviewed:", sum(bool(c.get("reviewed")) for c in box))
keys = list(next(iter(results.values())).keys())
print(f"{'metric':32}" + "".join(f"{k:>17}" for k in columns))
for k in keys:
    print(f"{k:32}" + "".join(f"{results[c][k]:>17.1f}" for c in columns))
