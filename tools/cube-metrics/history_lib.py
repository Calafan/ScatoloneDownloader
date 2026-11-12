"""Shared code for the Limited-history scripts: data loading, the booster model, the metrics.

The metric definitions are those of speed_metrics.py (HARD, SOFT, INTERACTION, EVASION,
LIFEGAIN, FOG, the nonland rule on the front face type line, face(), num()), plus the
per-pack counts and the averages added for the history.
"""
import gzip
import json
import re
from pathlib import Path

HARD = {"Removal", "RemovePermanent", "Wipe"}
SOFT = {"Pacify", "Bounce", "Steal"}
INTERACTION = HARD | SOFT | {"Counter", "Disenchant", "LandDestruction", "Discard"}
EVASION = {"Flying", "Menace", "Trample", "Shadow", "Horsemanship", "Fear", "Intimidate", "Skulk"}
LIFEGAIN = re.compile(r"\bgains? (?:\d+|X|that much|life equal)|\byou gain\b")
FOG = re.compile(r"prevent all combat damage", re.I)
SKIP_LAYOUTS = {"art_series", "reversible_card", "token", "double_faced_token", "emblem"}


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


# ---------------------------------------------------------------- loading
def load_oracle(path):
    """oracle_id -> Scryfall card object."""
    out = {}
    with gzip.open(path, "rt", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                c = json.loads(line)
                if c.get("oracle_id"):
                    out[c["oracle_id"]] = c
    return out


def name_index(oracle):
    """name -> oracle_id: exact name first, then the front-face name (unique only)."""
    exact = {}
    for oid, c in oracle.items():
        exact.setdefault(c["name"], oid)
    front = {}
    for oid, c in oracle.items():
        if c.get("layout") in SKIP_LAYOUTS or not c.get("card_faces"):
            continue
        front.setdefault(c["card_faces"][0]["name"], set()).add(oid)
    return exact, {n: next(iter(s)) for n, s in front.items() if len(s) == 1}


def load_store(store_dir):
    store, tiers = {}, {}
    for tier in ("pool", "bench", "fringe", "unrated"):
        if not (Path(store_dir) / f"{tier}.json").is_file():
            continue  # bench.json is absent from stores older than 2026-11-12
        data = json.loads((Path(store_dir) / f"{tier}.json").read_text(encoding="utf-8-sig"))
        for oid, e in data.get("cards", {}).items():
            store[oid] = e
            tiers[oid] = tier
    return store, tiers


def card_row(oracle_card, store, weight):
    c = oracle_card
    e = store.get(c["oracle_id"])
    text = c.get("oracle_text") or "\n".join(f.get("oracle_text", "") for f in c.get("card_faces") or [])
    return {"cmc": c.get("cmc", 0), "frontType": face(c, "type_line") or "", "power": face(c, "power"),
            "toughness": face(c, "toughness"), "keywords": c.get("keywords") or [], "text": text,
            "effects": (e or {}).get("effects", []), "inStore": e is not None,
            "reviewed": bool(e and e.get("reviewedAt")), "w": weight}


# ---------------------------------------------------------------- MTGJSON
class Mtgjson:
    def __init__(self, directory):
        self.dir = Path(directory)
        self.sets = {}
        self.uuid_oracle = {}
        self.uuid_set = {}
        for p in sorted(self.dir.glob("*.json.gz")):
            code = p.name[:-len(".json.gz")]
            with gzip.open(p, "rt", encoding="utf-8") as f:
                data = json.load(f)["data"]
            self.sets[code] = data
            for c in data.get("cards", []):
                oid = (c.get("identifiers") or {}).get("scryfallOracleId")
                self.uuid_oracle[c["uuid"]] = oid
                self.uuid_set[c["uuid"]] = code
            for c in data.get("tokens", []):
                self.uuid_set.setdefault(c["uuid"], code)

    def product(self, code):
        b = (self.sets[code].get("booster") or {})
        for name in ("play", "draft", "default"):
            if name in b:
                return name, b[name]
        return None, None

    def expected(self, code):
        """-> (E {uuid: copies per pack}, product name, fallback flag, pack size)."""
        name, prod = self.product(code)
        data = self.sets[code]
        if prod is not None:
            E = {}
            total_w = prod["boostersTotalWeight"]
            for b in prod["boosters"]:
                bw = b["weight"] / total_w
                for sheet, count in b["contents"].items():
                    s = prod["sheets"][sheet]
                    for u, w in s["cards"].items():
                        E[u] = E.get(u, 0.0) + bw * count * w / s["totalWeight"]
            return E, name, False, sum(E.values())
        old = data["releaseDate"] < "2008-10-03"
        nc, nu = (11, 3) if old else (10, 3)
        by = {"common": [], "uncommon": [], "rare": [], "mythic": []}
        for c in data["cards"]:
            if "Basic" in (c.get("supertypes") or []):
                continue
            if c.get("rarity") in by:
                by[c["rarity"]].append(c["uuid"])
        E = {}
        for r, n in (("common", nc), ("uncommon", nu)):
            for u in by[r]:
                E[u] = n / len(by[r])
        rw = 2 * len(by["rare"]) + len(by["mythic"])
        for u in by["rare"]:
            E[u] = 2 / rw
        for u in by["mythic"]:
            E[u] = 1 / rw
        return E, "fallback", True, sum(E.values())

    def per_oracle(self, code):
        """-> (E per oracle_id, product, fallback, pack size, unresolved per pack, unresolved by set)."""
        E, name, fb, size = self.expected(code)
        out, unres, by_set = {}, 0.0, {}
        for u, e in E.items():
            oid = self.uuid_oracle.get(u)
            if oid:
                out[oid] = out.get(oid, 0.0) + e
            else:
                unres += e
                s = self.uuid_set.get(u, "?")
                by_set[s] = by_set.get(s, 0.0) + e
        return out, name, fb, size, unres, by_set


# ---------------------------------------------------------------- metrics
def metrics(cards, pack_sizes=()):
    """Same keys as speed_metrics.metrics, plus the history additions.

    pack_sizes: for a pool of equal-weight cards (cubes, pool), the pack sizes to report per-pack
    counts for (share x nonland share of all weight x size). Formats pass nothing: their weights
    are cards per pack, so the per-pack count is share x nonland per pack.
    """
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
    m["creatures per hard answer"] = m["creatures"] / m["hard creature removal"] if m["hard creature removal"] else None
    m["creatures per any interaction"] = m["creatures"] / m["any interaction"] if m["any interaction"] else None

    # history additions
    hard = [c for c in nonland if HARD & eff(c)]
    HW = sum(c["w"] for c in hard)
    m["avg MV hard removal"] = sum(c["cmc"] * c["w"] for c in hard) / HW if HW else None
    for mv in (2, 3, 4):
        sel = [c for c in creatures if c["cmc"] == mv and num(c["power"]) is not None and num(c["toughness"]) is not None]
        sw = sum(c["w"] for c in sel)
        m[f"avg P+T creatures MV{mv}"] = sum((num(c["power"]) + num(c["toughness"])) * c["w"] for c in sel) / sw if sw else None
    m["reviewed share"] = 100.0 * sum(c["w"] for c in cards if c["reviewed"]) / W
    m["not in store share"] = 100.0 * sum(c["w"] for c in cards if not c["inStore"]) / W
    m["not in store share nonland"] = 100.0 * sum(c["w"] for c in nonland if not c["inStore"]) / N
    counts = {"hard creature removal": "hard removal", "  of which Wipe": "Wipe", "creatures": "creatures",
              "creatures MV<=2": "creatures MV<=2", "MV 6+": "MV 6+", "card advantage": "card advantage",
              "any interaction": "any interaction", "counterspells": "counterspells"}
    if pack_sizes:
        for size in pack_sizes:
            for k, label in counts.items():
                m[f"{label} per {size}"] = m[k] / 100 * (N / W) * size
    else:
        m["nonland per pack"] = round(N, 3)
        m["cards per pack"] = round(W, 3)
        for k, label in counts.items():
            m[f"{label} per pack"] = m[k] / 100 * N
    return m
