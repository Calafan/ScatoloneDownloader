"""The store commits of a pass, one command per step — the sequence of references/store.md.

Every step keeps the human's uncommitted work: it is backed up before anything
is written on top of HEAD, committed around, and put back. Every step ends with
verify_tree. Messages come with -F (a file); end them with the attribution
lines the session asks for. Nothing is pushed.

    python store_pass.py rulings FILE.json -F MSG   the human's rulings, on HEAD and then on the tree
    python store_pass.py classify                   Release build, protected classify, split — no commit
    python store_pass.py commit -F MSG              commit the split, restore the tree, verify
    python store_pass.py handback FILE.json -F MSG  unreview on HEAD and tree, commit, add to state/handed-back.json
    python store_pass.py readme -F MSG              rewrite the README ontology table, commit it if it changed

A pass runs them in this order: rulings -> (code commit) -> classify -> commit
-> handback -> readme. Between `classify` and `commit`, read the moves it prints
(store_moves.py --names for the cards) and write the message from them.
"""
import argparse
import shutil
import subprocess
import sys
from pathlib import Path

from common import CODE_REPO, FILES, HANDED_BACK, STORE, STORE_REPO, WORK_DIR, utf8_stdout

HERE = Path(__file__).resolve().parent
PASS_DIR = WORK_DIR / "store-pass"


def run(*cmd, cwd=None, check=True):
    print("$", " ".join(str(c) for c in cmd))
    done = subprocess.run([str(c) for c in cmd], cwd=cwd, text=True, encoding="utf-8", errors="replace",
                          capture_output=True)
    out = (done.stdout or "") + (done.stderr or "")
    if out.strip():
        print(out.rstrip())
    if check and done.returncode != 0:
        sys.exit("failed (%d): %s" % (done.returncode, " ".join(str(c) for c in cmd)))
    return out


def py(script, *args, check=True):
    return run(sys.executable, HERE / script, *args, check=check)


def copy_store(src, dst):
    Path(dst).mkdir(parents=True, exist_ok=True)
    for name in FILES:
        shutil.copyfile(Path(src) / name, Path(dst) / name)


def reviewed_at_in_diff():
    diff = run("git", "-C", STORE_REPO, "diff", "-U0", "--", "metadata/", check=False)
    return sum(1 for line in diff.splitlines() if line[:1] in "+-" and '"reviewedAt"' in line)


def commit(message_file):
    run("git", "-C", STORE_REPO, "add", *("metadata/" + f for f in FILES))
    run("git", "-C", STORE_REPO, "commit", "-q", "-F", message_file)
    run("git", "-C", STORE_REPO, "log", "--oneline", "-1")


def on_head_and_tree(rulings, message_file, unreview):
    """HEAD + rulings committed; the human's tree restored and given the same rulings."""
    backup = PASS_DIR / "pre-pass"
    copy_store(STORE, backup)
    out = py("apply_ruling.py", rulings, "--mode", "head")
    if "non-effects fields: 0" not in out:
        sys.exit("a field other than effects moved — nothing committed; the tree is in "
                 "HEAD+rulings state, restore it from %s" % backup)
    if not unreview and reviewed_at_in_diff():
        sys.exit("reviewedAt moved in a rulings commit — nothing committed")
    commit(message_file)
    copy_store(backup, STORE)
    py("apply_ruling.py", rulings, "--mode", "tree")


def main():
    utf8_stdout()
    ap = argparse.ArgumentParser()
    ap.add_argument("step", choices=["rulings", "classify", "commit", "handback", "readme"])
    ap.add_argument("file", nargs="?", help="rulings / unreview file")
    ap.add_argument("-F", dest="message", help="commit message file")
    args = ap.parse_args()
    if args.step != "classify" and not args.message:
        sys.exit("-F MESSAGE is required for " + args.step)

    if args.step == "rulings":
        on_head_and_tree(args.file, args.message, unreview=False)
        py("verify_tree.py")

    elif args.step == "classify":
        # The Release build is what the tagger and this classify run; an old one
        # silently reverts every proposal (the stale-release-build memory).
        run("dotnet", "build", CODE_REPO / "ScatoloneDownloader", "-c", "Release", "--nologo", "-v", "q")
        pre, post = PASS_DIR / "pre-classify", PASS_DIR / "post-classify"
        copy_store(STORE, pre)
        run("dotnet", "run", "--project", CODE_REPO / "ScatoloneDownloader", "-c", "Release", "--",
            "classify", "-m", STORE, "--overwrite")
        copy_store(STORE, post)
        py("restore_handed_back.py", pre, post, HANDED_BACK)
        py("split_unreviewed.py", post, "--keep-head", HANDED_BACK)
        n = reviewed_at_in_diff()
        print("reviewedAt lines in the staged diff: %d (must be 0)" % n)
        py("store_moves.py")
        print("\nnext: read the moves (store_moves.py --names), write the message, `store_pass.py commit -F MSG`")

    elif args.step == "commit":
        if reviewed_at_in_diff():
            sys.exit("reviewedAt moved — nothing committed")
        commit(args.message)
        copy_store(PASS_DIR / "post-classify", STORE)
        py("verify_tree.py", "--compare", PASS_DIR / "pre-classify")

    elif args.step == "handback":
        on_head_and_tree(args.file, args.message, unreview=True)
        py("handed_back.py", "--add", args.file)
        py("verify_tree.py")

    elif args.step == "readme":
        py("readme_ontology.py", "--write")
        if run("git", "-C", STORE_REPO, "status", "--short", "README.md").strip():
            run("git", "-C", STORE_REPO, "add", "README.md")
            run("git", "-C", STORE_REPO, "commit", "-q", "-F", args.message)
            run("git", "-C", STORE_REPO, "log", "--oneline", "-1")
        else:
            print("README unchanged, nothing committed")


if __name__ == "__main__":
    main()
