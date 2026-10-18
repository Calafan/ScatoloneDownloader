"""The targets table of docs/cube-targets.md, printed as Markdown from the same measurements.

    python -I targets_table.py BOX_JSONL BULK_GZ STORE_DIR SETS_DIR

Columns: the box today, retail as opened, the halfway target (their mean), and the box after the
proposed swap (simulate.py); every row also per draft (160 cards: 4 players x 4 packs of 10).
"""
import contextlib
import importlib.util
import io
import sys
from pathlib import Path

here = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("sim", here / "simulate.py")
sim = importlib.util.module_from_spec(spec)
with contextlib.redirect_stdout(io.StringIO()):
    spec.loader.exec_module(sim)  # runs the measurements and the swap

sm = sim.sm
box_m, retail_m, after_m = sm.metrics(sim.box), sm.metrics(sm.retail), sm.metrics(sim.after)
PER_DRAFT = 160


def per_draft(m, k):
    return PER_DRAFT * (1 - m["lands % of all"] / 100) * m[k] / 100


skip = {"cards", "lands % of all"}
ratios = {"avg MV nonland", "avg MV creatures", "creatures per hard answer", "creatures per any interaction"}
print("| metric | box today | retail | target (halfway) | after the swap |")
print("|---|---:|---:|---:|---:|")
for k in box_m:
    if k in skip:
        continue
    half = (box_m[k] + retail_m[k]) / 2
    if k in ratios:
        print(f"| {k.strip()} | {box_m[k]:.1f} | {retail_m[k]:.1f} | {half:.1f} | {after_m[k]:.1f} |")
    else:
        print(f"| {k.strip()} | {box_m[k]:.1f}% | {retail_m[k]:.1f}% | {half:.1f}% | {after_m[k]:.1f}% |")

print()
print("| per draft (160 cards) | box today | retail | target (halfway) |")
print("|---|---:|---:|---:|")
for k in ("hard creature removal", "  of which Wipe", "any interaction", "creatures", "creatures MV<=2",
          "aggro 2-drop (MV<=2, P>=2)", "MV 6+", "card advantage", "planeswalkers"):
    b, r = per_draft(box_m, k), per_draft(retail_m, k)
    print(f"| {k.strip()} | {b:.1f} | {r:.1f} | {(b + r) / 2:.1f} |")

print()
print(f"lands: box {box_m['lands % of all']:.1f}% retail {retail_m['lands % of all']:.1f}%; "
      f"swap: cut A {len(sim.cut_a)}, cut B {len(sim.cut_b)}, add {len(sim.adds)}; "
      f"box {len(sim.box)} -> {len(sim.after)}")
