"""history.json -> one static HTML file with inline SVG (no scripts, no external fonts).

    python -I history_html.py HISTORY_JSON OUT_HTML
"""
import html
import json
import sys
from pathlib import Path

hist = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
rows = hist["rows"]
formats = [r for r in rows if r["kind"] not in ("cube", "pool")]
cubes = [r for r in rows if r["kind"] == "cube"]
pool = next(r for r in rows if r["kind"] == "pool")

KINDS = [("3 sets", "3 set (1 grande + 2 piccole)", "circle"), ("2 sets", "2 set (blocchi a due)", "square"),
         ("1 set", "1 set (tre pacchetti uguali)", "diamond"), ("core", "Base (tre pacchetti)", "triangle")]
KIND_VAR = {k: f"--k{i}" for i, (k, _, _) in enumerate(KINDS)}
SHAPE = {k: s for k, _, s in KINDS}
MARKERS = [("2008-10-03", "2008-10 mitiche"), ("2015-10-02", "2015-10 due set"), ("2018-04-27", "2018-04 set singoli"),
           ("2024-02-09", "2024-02 play booster")]

# (title, kind of value, key, format key, cube key, unit)
CHARTS = [
    ("Rimozione dura creature %", "hard creature removal", None, "%"),
    ("Wipe %", "  of which Wipe", None, "%"),
    ("Creature %", "creatures", None, "%"),
    ("Creature MV<=2 %", "creatures MV<=2", None, "%"),
    ("MV medio non terre", "avg MV nonland", None, ""),
    ("MV 6+ %", "MV 6+", None, "%"),
    ("Card advantage %", "card advantage", None, "%"),
    ("Qualsiasi interazione %", "any interaction", None, "%"),
    ("Creature evasive %", "evasive creatures", None, "%"),
    ("P+T medio creature MV3", "avg P+T creatures MV3", None, ""),
    ("Rimozione dura per pacchetto", "hard removal", "pack", "per pacchetto"),
    ("Creature MV<=2 per pacchetto", "creatures MV<=2", "pack", "per pacchetto"),
]

CUBE_SHORT = {"MTGO Vintage Cube": "Vintage", "LSV Cube": "LSV", "Pauper Cube": "Pauper", "Giant Peasant": "G. Peasant",
              "2HG Cube": "2HG", "42a6g": "42a6g", "Commander Cube": "Commander", "The Giant Cube": "Giant"}

W, H = 400, 230
ML, MR, MT, MB = 40, 112, 26, 26
PW, PH = W - ML - MR, H - MT - MB
X0, X1 = 1996.0, 2027.0


def year(d):
    y, m, dd = (int(p) for p in d.split("-"))
    return y + (m - 1) / 12 + (dd - 1) / 365


def nice_ticks(lo, hi, n=4):
    span = hi - lo
    raw = span / n
    mag = 10 ** (len(str(int(raw))) - 1) if raw >= 1 else 10 ** -(len(str(int(1 / raw))))
    for step in (1, 2, 2.5, 5, 10):
        if step * mag >= raw:
            step = step * mag
            break
    t = (int(lo / step)) * step
    out = []
    while t <= hi + 1e-9:
        if t >= lo - 1e-9:
            out.append(t)
        t += step
    return out


def shape_svg(shape, x, y, cls, title, r=3.6):
    t = f"<title>{html.escape(title)}</title>"
    if shape == "circle":
        return f'<circle class="{cls}" cx="{x:.1f}" cy="{y:.1f}" r="{r}">{t}</circle>'
    if shape == "square":
        return f'<rect class="{cls}" x="{x - r:.1f}" y="{y - r:.1f}" width="{2 * r}" height="{2 * r}" rx="1">{t}</rect>'
    if shape == "diamond":
        d = r * 1.35
        return f'<polygon class="{cls}" points="{x:.1f},{y - d:.1f} {x + d:.1f},{y:.1f} {x:.1f},{y + d:.1f} {x - d:.1f},{y:.1f}">{t}</polygon>'
    d = r * 1.3
    return f'<polygon class="{cls}" points="{x:.1f},{y - d:.1f} {x + d:.1f},{y + d * 0.8:.1f} {x - d:.1f},{y + d * 0.8:.1f}">{t}</polygon>'


def fmt_v(v, unit):
    return f"{v:.1f}%" if unit == "%" else (f"{v:.2f}" if unit == "" else f"{v:.2f} {unit}")


def chart(title, key, mode, unit):
    def fval(r):
        return r["metrics"].get(f"{key} per pack" if mode else key)

    def cval(r, size=15):
        return r["metrics"].get(f"{key} per {size}" if mode else key)

    ys = [fval(r) for r in formats if fval(r) is not None] + [cval(r) for r in cubes + [pool] if cval(r) is not None]
    if mode:
        ys.append(cval(pool, 10))
    lo, hi = min(0, min(ys)), max(ys)
    pad = (hi - lo) * 0.06
    hi += pad
    if lo != 0:
        lo -= pad
    ticks = nice_ticks(lo, hi)
    hi = max(hi, ticks[-1])

    def px(d):
        return ML + (year(d) - X0) / (X1 - X0) * PW

    def py(v):
        return MT + PH - (v - lo) / (hi - lo) * PH

    s = [f'<svg viewBox="0 0 {W} {H}" role="img" aria-label="{html.escape(title)}">']
    for t in ticks:
        s.append(f'<line class="gl" x1="{ML}" x2="{ML + PW}" y1="{py(t):.1f}" y2="{py(t):.1f}"/>')
        s.append(f'<text class="tick" x="{ML - 5}" y="{py(t) + 3:.1f}" text-anchor="end">{t:g}</text>')
    for yr in range(2000, 2027, 5):
        x = ML + (yr - X0) / (X1 - X0) * PW
        s.append(f'<text class="tick" x="{x:.1f}" y="{MT + PH + 14}" text-anchor="middle">{yr}</text>')
    s.append(f'<line class="axis" x1="{ML}" x2="{ML + PW}" y1="{MT + PH}" y2="{MT + PH}"/>')
    for i, (d, lab) in enumerate(MARKERS):
        x = px(d)
        s.append(f'<line class="marker" x1="{x:.1f}" x2="{x:.1f}" y1="{MT}" y2="{MT + PH}"><title>{lab}</title></line>')
        s.append(f'<text class="mlabel" x="{x + 2:.1f}" y="{MT - 3 - (i % 2) * 0}" >{["M", "2s", "1s", "P"][i]}</text>')
    for r in formats:
        v = fval(r)
        if v is None:
            continue
        s.append(shape_svg(SHAPE[r["kind"]], px(r["date"]), py(v), f"dot {KIND_VAR[r['kind']][2:]}",
                           f"{r['label']} ({r['date']}): {fmt_v(v, unit)}"))
    # rail: pool, cubes
    rail = [(pool["label"].replace(" (pool)", ""), cval(pool), "pool", f"{pool['label']}: {fmt_v(cval(pool), unit)}" + (" per 15" if mode else ""))]
    if mode:
        rail.append(("Scatolone da 10", cval(pool, 10), "pool10", f"{pool['label']}, pacchetto da 10: {fmt_v(cval(pool, 10), unit)}"))
    for r in cubes:
        rail.append((CUBE_SHORT.get(r["label"], r["label"]), cval(r), "cube", f"{r['label']}: {fmt_v(cval(r), unit)}" + (" per 15" if mode else "")))
    rail = [x for x in rail if x[1] is not None]
    rx = ML + PW + 14
    s.append(f'<line class="axis" x1="{ML + PW + 6}" x2="{ML + PW + 6}" y1="{MT}" y2="{MT + PH}"/>')
    ypos = sorted(rail, key=lambda a: py(a[1]))
    lab_y, prev = [], -99
    for a in ypos:
        y = max(py(a[1]), prev + 10.5)
        lab_y.append(y)
        prev = y
    over = lab_y[-1] - (MT + PH + 4) if lab_y else 0
    if over > 0:
        lab_y = [y - over for y in lab_y]
    for a, ly in zip(ypos, lab_y):
        name, v, cls, tip = a
        y = py(v)
        if cls == "pool":
            s.append(shape_svg("diamond", rx, y, "dot pool", tip, 4.2))
        elif cls == "pool10":
            s.append(shape_svg("diamond", rx, y, "dot pool10", tip, 4.2))
        else:
            s.append(shape_svg("circle", rx, y, "dot cube", tip, 3.2))
        s.append(f'<line class="lead" x1="{rx + 5}" x2="{rx + 9}" y1="{y:.1f}" y2="{ly:.1f}"/>')
        s.append(f'<text class="rl{" poolt" if cls.startswith("pool") else ""}" x="{rx + 11}" y="{ly + 3:.1f}">{html.escape(name)}</text>')
    s.append("</svg>")
    return f'<figure><figcaption>{html.escape(title)}</figcaption>{"".join(s)}</figure>'


def table_view():
    keys = [(t, k, m) for t, k, m, _ in CHARTS]
    head = "".join(f"<th>{html.escape(t)}</th>" for t, _, _ in keys)
    out = [f"<table><thead><tr><th>Formato</th><th>Tipo</th><th>Data</th>{head}</tr></thead><tbody>"]
    for r in sorted(formats, key=lambda r: r["date"]) + cubes + [pool]:
        cells = []
        for _, k, m in keys:
            if m:
                kk = f"{k} per pack" if r["kind"] not in ("cube", "pool") else f"{k} per 15"
            else:
                kk = k
            v = r["metrics"].get(kk)
            cells.append(f"<td>{'' if v is None else f'{v:.2f}'}</td>")
        out.append(f"<tr><td>{html.escape(r['label'])}</td><td>{r['kind']}</td><td>{r['date'] or ''}</td>{''.join(cells)}</tr>")
    out.append("</tbody></table>")
    return "".join(out)


legend = "".join(
    f'<span class="lg">{shape_svg(s, 8, 8, f"dot {KIND_VAR[k][2:]}", lab, 4).join(["<svg width=16 height=16>", "</svg>"])}{html.escape(lab)}</span>'
    for k, lab, s in KINDS)
legend += ('<span class="lg"><svg width=16 height=16><circle class="dot cube" cx=8 cy=8 r=3.2 /></svg>Cubo (CubeCobra)</span>'
           '<span class="lg"><svg width=16 height=16><polygon class="dot pool" points="8,2 14,8 8,14 2,8"/></svg>Scatolone (pool)</span>')
mark_legend = " &middot; ".join(f"<b>{a}</b> {html.escape(b)}" for a, b in zip(["M", "2s", "1s", "P"], [m[1] for m in MARKERS]))

CSS = """
:root{color-scheme:light;--bg:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--grid:#e6e5e1;--k0:#2a78d6;--k1:#eb6834;--k2:#1baf7a;--k3:#6250d6;--cube:#8a8984;--pool:#e34948}
@media (prefers-color-scheme:dark){:root{color-scheme:dark;--bg:#1a1a19;--ink:#f0efec;--ink2:#c3c2b7;--grid:#34332f;--k0:#3987e5;--k1:#d95926;--k2:#199e70;--k3:#9085e9;--cube:#a3a29c;--pool:#e66767}}
body{margin:0;padding:20px 24px;background:var(--bg);color:var(--ink);font:14px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif}
h1{font-size:22px;margin:0 0 4px}p.sub{color:var(--ink2);margin:0 0 12px;max-width:80ch}
.legend{display:flex;flex-wrap:wrap;gap:4px 18px;margin:10px 0 4px;color:var(--ink2);font-size:13px}.lg{display:inline-flex;align-items:center;gap:4px}
.mk{color:var(--ink2);font-size:12px;margin-bottom:14px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(400px,1fr));gap:14px 18px}
figure{margin:0}figcaption{font-weight:600;font-size:13px;margin-bottom:2px}figure svg{width:100%;height:auto;display:block}
.gl{stroke:var(--grid);stroke-width:1}line.axis{stroke:var(--ink2);stroke-width:1}
line.marker{stroke:var(--ink2);stroke-width:1;stroke-dasharray:3 3;opacity:.55}
text{fill:var(--ink2);font-size:10px}text.mlabel{font-size:9px}text.rl{font-size:10px}text.poolt{fill:var(--ink);font-weight:700}
line.lead{stroke:var(--ink2);stroke-width:.6;opacity:.6}
.dot{stroke:var(--bg);stroke-width:1.2}.dot.k0{fill:var(--k0)}.dot.k1{fill:var(--k1)}.dot.k2{fill:var(--k2)}.dot.k3{fill:var(--k3)}
.dot.cube{fill:var(--cube)}.dot.pool{fill:var(--pool);stroke:var(--ink);stroke-width:1.4}.dot.pool10{fill:none;stroke:var(--pool);stroke-width:1.8}
.dot:hover{stroke:var(--ink);stroke-width:2}
details{margin-top:22px}summary{cursor:pointer;color:var(--ink2)}
table{border-collapse:collapse;font-size:12px;margin-top:8px}th,td{padding:2px 8px;border-bottom:1px solid var(--grid);text-align:right}
th:first-child,td:first-child,td:nth-child(2),td:nth-child(3){text-align:left}th{position:sticky;top:0;background:var(--bg)}
.note{color:var(--ink2);font-size:12px;margin-top:14px;max-width:90ch}
"""

body = "".join(chart(t, k, m, u) for t, k, m, u in CHARTS)
doc = f"""<!DOCTYPE html>
<html lang="it"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Scatolone e Limited nel tempo</title><style>{CSS}</style></head><body>
<h1>Scatolone e Limited nel tempo</h1>
<p class="sub">Formati Limited al dettaglio (copie attese per pacchetto, rarit&agrave; e fogli come aperti) contro 8 cubi CubeCobra e il pool del Scatolone. Percentuali sulle carte non terra. Nei grafici &laquo;per pacchetto&raquo; i formati sono per pacchetto reale (15 carte, 14 dal play booster), cubi e pool per pacchetto da 15; il pool da 10 &egrave; il rombo vuoto.</p>
<div class="legend">{legend}</div>
<div class="mk">Linee verticali: {mark_legend}</div>
<div class="grid">{body}</div>
<details><summary>Vista tabella</summary>{table_view()}</details>
<p class="note">Il passaggio del mouse su un punto mostra nome e valore. Le forme dei punti ripetono il colore del tipo di formato.</p>
</body></html>"""
Path(sys.argv[2]).write_text(doc, encoding="utf-8")
print("written", sys.argv[2], len(doc))
