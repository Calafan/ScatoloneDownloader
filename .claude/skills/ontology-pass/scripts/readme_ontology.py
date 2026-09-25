"""Keep the ontology table in the ScatoloneQuintet README in step with the code.

The README shows every CardEffect with its tagger tooltip, word for word, in
enum declaration order. It is a copy, so a pass that changes a tooltip leaves
it stale unless this runs at the end: --check says whether it matches, --write
rewrites the table rows (and nothing else in the file).

Usage:
    python readme_ontology.py --check
    python readme_ontology.py --write
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4] / "ScatoloneDownloader" / "Mtg"
README = Path(r"E:\Working\Repos\ScatoloneQuintet\README.md")
SECTION = "## Effect ontology"
ROW = re.compile(r"^\| \*\*(\w+)\*\* \| (.*) \|$", re.M)


def ontology():
    """(name, tooltip) for every CardEffect member, in declaration order."""
    enum_src = (ROOT / "CardEffect.cs").read_text(encoding="utf-8-sig")
    gloss_src = (ROOT / "EffectGlossary.cs").read_text(encoding="utf-8-sig")
    members = re.findall(r"^\s*(\w+)\s*=\s*1\s*<<\s*\d+", enum_src, re.M)
    lines = {}
    for m in re.finditer(r"\[CardEffect\.(\w+)\]\s*=\s*((?:\s*\+?\s*\"(?:[^\"\\]|\\.)*\")+)\s*,", gloss_src):
        lines[m.group(1)] = "".join(re.findall(r"\"((?:[^\"\\]|\\.)*)\"", m.group(2))).replace('\\"', '"')
    missing = [n for n in members if n not in lines]
    if missing:
        raise SystemExit(f"no tooltip for: {', '.join(missing)}")
    return [(n, lines[n]) for n in members]


def main():
    ap = argparse.ArgumentParser()
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--write", action="store_true")
    args = ap.parse_args()

    raw = README.read_bytes().decode("utf-8")
    eol = "\r\n" if "\r\n" in raw else "\n"
    text = raw.replace("\r\n", "\n")
    head, rest = text.split(SECTION, 1)
    body, sep, tail = rest.partition("\n## ")

    expected = ontology()
    found = ROW.findall(body)
    if found == expected:
        print(f"README ontology table matches the code ({len(expected)} effects)")
        return 0

    have = dict(found)
    stale = [n for n, t in expected if have.get(n) != t]
    extra = [n for n in have if n not in dict(expected)]
    print(f"README table is stale: {len(stale)} rows differ or are missing"
          + (f", {len(extra)} no longer in the enum" if extra else "")
          + (f" ({', '.join(stale + extra)})" if stale or extra else "; the order differs"))
    if args.check:
        return 1

    rows = "\n".join(f"| **{n}** | {t} |" for n, t in expected)
    first, last = ROW.search(body), list(ROW.finditer(body))[-1]
    body = body[:first.start()] + rows + body[last.end():]
    README.write_bytes((head + SECTION + body + sep + tail).replace("\n", eol).encode("utf-8"))
    print("README table rewritten; commit it in ScatoloneQuintet")
    return 0


if __name__ == "__main__":
    sys.exit(main())
