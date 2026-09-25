"""Verification by neutralisation: undo each fix ALONE and report which tests go red.

A fix whose removal turns nothing red is not proven — either no test pins it, or
the test pins something else. Every case is applied, the classifier tests run,
and the file is restored before the next case, even if the run is interrupted
by an exception.

cases.json is a list of {"name", "file", "old", "new"}: `file` relative to the
repo root, `old` a string that must occur exactly once in it, `new` what it
becomes for this run. Break a regex by making it unmatchable rather than by
deleting lines — `'exile")'` -> `'exileQQQ")'` — because a deleted line next to a
comment leaves `);` on the comment's line and the case reports a failed build
instead of a verdict. Line endings do not matter: `old`/`new` are matched with
the file's own.

Usage:
    python neutralise.py cases.json [--filter FullyQualifiedName~EffectClassifierTests]

Run ONE case by hand first and check it goes red: a harness that cannot read the
failures reports "NOTHING WENT RED" for every case (see references/hazards.md).
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]


def run(test_filter):
    out = subprocess.run(["dotnet", "test", str(ROOT / "ScatoloneDownloader.Tests"), "--nologo", "-v", "q",
                          "--filter", test_filter],
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, encoding="utf-8", errors="replace").stdout
    # xUnit prints each failure as "...Tests.Method(name: "Card", ...) [FAIL]",
    # or "...Tests.Method [FAIL]" for a Fact.
    red = sorted({m.group(1) + (f" [{m.group(2)}]" if m.group(2) else "")
                  for m in re.finditer(r"(?<=\s)(?:\w+\.){2,}(\w+)(?:\(name: \"([^\"]+)\")?[^\n]*\[FAIL\]", out)})
    if not red and "Superati:" not in out and "Passed:" not in out:
        return ["BUILD FAILED"]
    return red


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cases")
    ap.add_argument("--filter", default="FullyQualifiedName~EffectClassifierTests")
    args = ap.parse_args()

    for case in json.loads(Path(args.cases).read_text(encoding="utf-8")):
        path = ROOT / case["file"]
        original = path.read_bytes()
        text = original.decode("utf-8")
        eol = "\r\n" if "\r\n" in text else "\n"
        old, new = case["old"].replace("\n", eol), case["new"].replace("\n", eol)
        if text.count(old) != 1:
            print(f"{case['name']}: PATTERN FOUND {text.count(old)} TIMES — fix the case")
            continue
        try:
            path.write_bytes(text.replace(old, new).encode("utf-8"))
            red = run(args.filter)
            print(f"{case['name']}: {'; '.join(red) if red else 'NOTHING WENT RED'}", flush=True)
        finally:
            path.write_bytes(original)


if __name__ == "__main__":
    main()
