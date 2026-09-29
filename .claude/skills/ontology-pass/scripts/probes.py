"""Run OntologyProbes by name, without copying the probe file in and out by hand.

Usage:
    python probes.py Score Dump [TagDetail Why Text Count Matrix Blank] [--tag TAG] [--names "A" "B"] [--keep]

Copies assets/OntologyProbes.cs into ScatoloneDownloader.Tests/Cube/ (the
folder is gitignored), writes tag.txt / names.txt into WORK_DIR when given,
runs the chosen probes with ONTOLOGY_DIR=WORK_DIR, and removes the probe file
again unless --keep — a stale probe left in the tree inflates the next full
test count and confuses the reader. Prints the Score lines for --tag and the
TagDetail card headers, which is usually all a pass needs to read.
"""
import argparse
import os
import shutil
import subprocess
import sys

from common import CODE_REPO, SKILL_DIR, WORK_DIR, utf8_stdout

PROBE_SRC = SKILL_DIR / "assets" / "OntologyProbes.cs"
PROBE_DST = CODE_REPO / "ScatoloneDownloader.Tests" / "Cube" / "OntologyProbes.cs"


def run_probes(names, tag=None, cards=None, keep=False, quiet=False):
    WORK_DIR.mkdir(parents=True, exist_ok=True)
    if tag:
        (WORK_DIR / "tag.txt").write_text(tag, encoding="utf-8")
    if cards:
        (WORK_DIR / "names.txt").write_text("\n".join(cards), encoding="utf-8")
    PROBE_DST.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(PROBE_SRC, PROBE_DST)
    try:
        flt = "|".join("FullyQualifiedName~OntologyProbes." + n for n in names)
        env = dict(os.environ, ONTOLOGY_DIR=str(WORK_DIR))
        done = subprocess.run(["dotnet", "test", str(CODE_REPO / "ScatoloneDownloader.Tests"), "--filter", flt,
                               "--nologo", "-v", "q"], env=env, capture_output=True, text=True,
                              encoding="utf-8", errors="replace")
        tail = [l for l in done.stdout.splitlines() if "Superat" in l or "Passed" in l or "Failed" in l
                or "error" in l.lower()]
        if done.returncode != 0:
            print(done.stdout[-3000:])
            sys.exit("probes failed")
        if not quiet:
            print("\n".join(tail[-2:]))
    finally:
        if not keep and PROBE_DST.exists():
            PROBE_DST.unlink()


def print_reports(names, tag):
    if "Score" in names:
        for line in (WORK_DIR / "acc-summary.txt").read_text(encoding="utf-8").splitlines():
            if line.startswith("exact tag-set") or (tag and line.startswith(tag + " ")):
                print(line)
    if "TagDetail" in names:
        for line in (WORK_DIR / "tag-detail.txt").read_text(encoding="utf-8").splitlines():
            if line.startswith("###") or " OVER-FIRED " in line or " MISSED " in line:
                print(line)


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("probes", nargs="+")
    ap.add_argument("--tag")
    ap.add_argument("--names", nargs="*")
    ap.add_argument("--keep", action="store_true")
    args = ap.parse_args()
    run_probes(args.probes, args.tag, args.names, args.keep)
    print_reports(args.probes, args.tag)


if __name__ == "__main__":
    main()
