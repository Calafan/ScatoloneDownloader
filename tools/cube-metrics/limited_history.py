"""Retail Limited formats over time, 8 CubeCobra cubes and the pool, with the speed-metrics definitions.

    python -I limited_history.py ORACLE_JSONL_GZ STORE_DIR MTGJSON_DIR CUBES_DIR FORMATS_JSON OUT_DIR

Writes OUT_DIR/history.json and OUT_DIR/history_tables.md.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from history_lib import Mtgjson, card_row, load_oracle, load_store, metrics, name_index  # noqa: E402

oracle_path, store_dir, mtgjson_dir, cubes_dir, formats_path, out_dir = sys.argv[1:7]
out = Path(out_dir)
out.mkdir(parents=True, exist_ok=True)

oracle = load_oracle(oracle_path)
store, tiers = load_store(store_dir)
M = Mtgjson(mtgjson_dir)
formats = json.loads(Path(formats_path).read_text(encoding="utf-8"))

CUBES = [("modovintage", "MTGO Vintage Cube"), ("LSVCube", "LSV Cube"), ("5d617ac6c2a85f3b75fe95a4", "Pauper Cube"),
         ("gianpeasant", "Giant Peasant"), ("2hgcb", "2HG Cube"), ("42a6g", "42a6g"),
         ("commandercube", "Commander Cube"), ("thegiantcube", "The Giant Cube")]

rows = []
set_cache = {}
set_report = []


def set_data(code):
    if code not in set_cache:
        set_cache[code] = M.per_oracle(code)
        _, name, fb, size, un, by = set_cache[code]
        set_report.append({"set": code, "product": name, "fallback": fb, "packSize": round(size, 3),
                           "unresolvedPerPack": round(un, 4), "unresolvedBySet": {k: round(v, 4) for k, v in by.items()}})
    return set_cache[code]


# ---- formats
for fm in formats:
    weights, products, fallbacks, unres = {}, {}, {}, {}
    n = len(fm["packs"])
    unres_total = size_total = 0.0
    for code in fm["packs"]:
        per, name, fb, size, un, _ = set_data(code)
        products[code], fallbacks[code], unres[code] = name, fb, round(un, 4)
        unres_total += un
        size_total += size
        for oid, e in per.items():
            weights[oid] = weights.get(oid, 0.0) + e / n
    cards = [card_row(oracle[oid], store, w) for oid, w in weights.items() if oid in oracle]
    lost = sum(w for oid, w in weights.items() if oid not in oracle)
    m = metrics(cards)
    rows.append({"id": fm["id"], "label": fm["label"], "kind": fm["kind"], "date": fm["date"], "packs": fm["packs"],
                 "product": products, "fallback": fallbacks, "unresolvedPerPack": unres,
                 "unresolvedAvg": round(unres_total / n, 4), "notInOracleWeight": round(lost, 4),
                 "avgPackSize": round(size_total / n, 3), "metrics": m})

# ---- cubes
exact, front = name_index(oracle)


def lookup(name):
    if name in exact:
        return exact[name]
    if name in front:
        return front[name]
    if " // " in name:
        first = name.split(" // ")[0]
        return exact.get(first) or front.get(first)
    return None


SIZES = (15, 10)
cube_summary = []
for cid, label in CUBES:
    lines = []
    for ln in (Path(cubes_dir) / f"{cid}.txt").read_text(encoding="utf-8").splitlines():
        if ln.startswith("#") and ln.strip() != "# mainboard":
            break
        if ln.strip() == "# mainboard":
            continue
        if ln.strip():
            lines.append(ln.strip())
    weights, miss = {}, []
    for ln in lines:
        oid = lookup(ln)
        if oid is None:
            miss.append(ln)
        else:
            weights[oid] = weights.get(oid, 0) + 1
    cards = [card_row(oracle[oid], store, w) for oid, w in weights.items()]
    rows.append({"id": cid, "label": label, "kind": "cube", "date": None, "lines": len(lines), "distinct": len(weights),
                 "misses": miss, "metrics": metrics(cards, SIZES)})
    cube_summary.append((label, len(lines), len(weights), miss))

# ---- pool
pool = json.loads((Path(store_dir) / "pool.json").read_text(encoding="utf-8-sig"))["cards"]
pcards, pmiss = [], []
for oid, e in pool.items():
    if e.get("status"):
        continue
    if oid not in oracle:
        pmiss.append(e.get("name"))
        continue
    pcards.append(card_row(oracle[oid], store, 1.0))
rows.append({"id": "pool", "label": "Scatolone (pool)", "kind": "pool", "date": None, "lines": len(pcards) + len(pmiss),
             "distinct": len(pcards), "misses": pmiss, "metrics": metrics(pcards, SIZES)})

(out / "history.json").write_text(json.dumps({"sets": sorted(set_report, key=lambda r: r["set"]), "rows": rows},
                                             indent=1, ensure_ascii=False), encoding="utf-8")

# ---- tables
fmt = sorted([r for r in rows if r["kind"] not in ("cube", "pool")], key=lambda r: r["date"])
others = [r for r in rows if r["kind"] in ("cube", "pool")]


def val(r, key, d=1):
    v = r["metrics"].get(key)
    if v is None:
        return "-"
    return f"{v:.{d}f}"


def pack_key(r, label):
    return f"{label} per pack" if r["kind"] not in ("cube", "pool") else f"{label} per 15"


def table(title, cols):
    lines = [f"### {title}", "", "| format | kind | date | " + " | ".join(c[0] for c in cols) + " |",
             "|---|---|---|" + "---:|" * len(cols)]
    for r in fmt + others:
        cells = []
        for hdr, key, d in cols:
            k = pack_key(r, key[1:]) if key.startswith("@") else key
            cells.append(val(r, k, d))
        lines.append(f"| {r['label']} | {r['kind']} | {r['date'] or ''} | " + " | ".join(cells) + " |")
    lines.append("")
    return lines


md = ["# Limited nel tempo", "",
      "Retail: copie attese per pacchetto, rarita' e fogli come aperti. Cubi e pool: peso 1 per carta (righe per i cubi); "
      "le conte per pacchetto dei cubi e del pool sono per pacchetto da 15 carte. % sulle non terre.", ""]
md += table("Composizione", [("carte/pacch.", "cards per pack", 1), ("non terre/pacch.", "nonland per pack", 1),
                             ("creature %", "creatures", 1), ("creature MV<=2 %", "creatures MV<=2", 1),
                             ("MV medio", "avg MV nonland", 2), ("MV 6+ %", "MV 6+", 1),
                             ("evasive %", "evasive creatures", 1), ("P+T MV3", "avg P+T creatures MV3", 2)])
md += table("Interazione", [("rimozione dura %", "hard creature removal", 1), ("Wipe %", "  of which Wipe", 1),
                            ("qualsiasi inter. %", "any interaction", 1), ("contromagie %", "counterspells", 1),
                            ("card advantage %", "card advantage", 1), ("MV medio rim.", "avg MV hard removal", 2)])
md += table("Per pacchetto", [("rim. dura", "@hard removal", 2), ("Wipe", "@Wipe", 2), ("creature", "@creatures", 2),
                              ("creature MV<=2", "@creatures MV<=2", 2), ("MV 6+", "@MV 6+", 2),
                              ("card adv.", "@card advantage", 2), ("inter.", "@any interaction", 2),
                              ("contromagie", "@counterspells", 2)])
md += table("Copertura dello store", [("rivisto %", "reviewed share", 1), ("fuori store %", "not in store share", 1), ("fuori store non terre %", "not in store share nonland", 2)])
(out / "history_tables.md").write_text("\n".join(md), encoding="utf-8")

# ---- console report
print("set check (product, pack size, unresolved/pack):")
for r in sorted(set_report, key=lambda r: r["set"]):
    flag = " FALLBACK" if r["fallback"] else ""
    print(f"  {r['set']:4} {r['product']:9} size {r['packSize']:6.2f} unres {r['unresolvedPerPack']:.4f}{flag}"
          + (f" {r['unresolvedBySet']}" if r["unresolvedPerPack"] > 0.05 else ""))
print("cubes:")
for label, n, d, miss in cube_summary:
    print(f"  {label}: lines {n} distinct {d} misses {len(miss)} {miss[:10]}")
print("pool:", len(pcards), "missing from oracle", pmiss)
print("not in store (worst 5 formats):")
for r in sorted(fmt, key=lambda r: -r["metrics"]["not in store share"])[:5]:
    print(f"  {r['label']}: {r['metrics']['not in store share']:.1f}%")
print("not in store, nonland only (worst 5 formats):")
for r in sorted(fmt, key=lambda r: -r["metrics"]["not in store share nonland"])[:5]:
    print(f"  {r['label']}: {r['metrics']['not in store share nonland']:.2f}%")
print("not in store cubes/pool:", [(r['label'], round(r['metrics']['not in store share'], 1)) for r in others])
