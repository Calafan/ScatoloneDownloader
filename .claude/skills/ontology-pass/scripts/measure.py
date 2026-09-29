"""Measure the uncommitted classifier change against the code it replaces, in one command.

Step 7 of the pass. It stashes the classifier source, dumps the store as the
OLD code proposes it (WORK_DIR/dump-before.jsonl), restores the change, runs
Score, Dump and TagDetail with the NEW code, and prints: the tag's score line,
the exact-match line, blast_radius's RIGHT/WRONG list on the reviewed cards,
and every unreviewed card whose proposal for the tag moved, with its line.

Usage:
    python measure.py TAG [--other TAG ...] [--paths PATH ...] [--no-before]

--paths is what gets stashed (default ScatoloneDownloader/Cube, where the
classifier lives). --no-before reuses the existing dump-before.jsonl, for a
second measurement against the same baseline. --other lists moves for more
tags (a Bounce change that also moves Protection).

The stash is always popped, even when a probe fails; if it is not, `git stash
list` in the code repo shows it and `git stash pop` restores the change.
"""
import argparse
import os
import shutil
import subprocess
import sys

from common import CODE_REPO, WORK_DIR, utf8_stdout
from probes import print_reports, run_probes

HERE = __import__("pathlib").Path(__file__).resolve().parent


def git(*args):
    return subprocess.run(["git", "-C", str(CODE_REPO), *args], capture_output=True, text=True,
                          encoding="utf-8").stdout


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("tag")
    ap.add_argument("--other", nargs="*", default=[])
    ap.add_argument("--paths", nargs="*", default=["ScatoloneDownloader/Cube"])
    ap.add_argument("--no-before", action="store_true")
    args = ap.parse_args()

    if not args.no_before:
        stashes = len(git("stash", "list").splitlines())
        git("stash", "push", "-q", "--", *args.paths)
        pushed = len(git("stash", "list").splitlines()) > stashes
        if not pushed:
            print("nothing to stash under %s — the baseline is the current code" % " ".join(args.paths))
        try:
            run_probes(["Dump"], quiet=True)
            shutil.copyfile(WORK_DIR / "dump.jsonl", WORK_DIR / "dump-before.jsonl")
        finally:
            if pushed:
                git("stash", "pop", "-q")

    run_probes(["Score", "Dump", "TagDetail"], tag=args.tag)
    print_reports(["Score"], args.tag)
    # The child prints card names ("Lothlórien Blade"): make it write UTF-8, or
    # it writes the console's code page and this read dies on the first accent.
    child_env = {**os.environ, "PYTHONIOENCODING": "utf-8"}
    for line in subprocess.run([sys.executable, str(HERE / "blast_radius.py"), str(WORK_DIR / "dump-before.jsonl"),
                                str(WORK_DIR / "dump.jsonl")], capture_output=True, text=True,
                               encoding="utf-8", env=child_env).stdout.splitlines():
        if line.startswith(("reviewed", "unreviewed", "WRONG")):
            print(line)
    for tag in [args.tag, *args.other]:
        print("\n-- unreviewed %s moves" % tag)
        sys.stdout.flush()
        subprocess.run([sys.executable, str(HERE / "moved_lines.py"), tag])


if __name__ == "__main__":
    main()
