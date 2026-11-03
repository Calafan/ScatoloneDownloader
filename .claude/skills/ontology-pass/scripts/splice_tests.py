"""Write classifier tests from card names — the exact oracle text, spliced into the test files.

Every ruling lands as [InlineData] cases (the skill's step 6), and doing that
by hand meant a throwaway splice script per pass and an indentation fixed by
hand. This does both shapes:

    new   a new [Theory] after an existing method
    add   more cases (with their comment) at the end of an existing theory

Usage:
    python splice_tests.py new --after METHOD --method NAME --assert Bounce=true [--assert Wipe=false]
                               --comment "why these cards pin the ruling" --cards "A" "B" ...
    python splice_tests.py add --into METHOD --comment "…" --cards "A" "B" ...

EffectClassifierTests is a partial class, one file per tag
(Mtg/EffectClassifier/EffectClassifierTests.<Tag>.cs; theories whose expected
flags span several tags stay in EffectClassifierTests.cs). The cases go into
the file that declares --after or --into, so a new theory for a tag is placed
after a method in that tag's file; --test-file pins one file instead.

--comment is prose; it is wrapped into // lines at 80 columns. The oracle text
comes from WORK_DIR/dump.jsonl (--dump to override), never retyped. The file
keeps its BOM and CRLF. Refuses when a card is not in the dump or the new
method name already exists in any of the files. Run the suite afterwards, then
neutralise.
"""
import argparse
import sys
import textwrap
from pathlib import Path

from common import CODE_REPO, load_dump
from gen_inline import inline_data

TEST_DIR = CODE_REPO / "ScatoloneDownloader.Tests" / "Mtg" / "EffectClassifier"


def comment_lines(prose):
    return "\n".join("    // " + line for line in textwrap.wrap(prose, 76)) + "\n"


def read(path):
    """The file's text with LF line ends, and the line end to write it back with."""
    # newline="" keeps the CRLFs: read_text would translate them and the file
    # would be written back LF (references/hazards.md).
    with open(path, encoding="utf-8-sig", newline="") as f:
        raw = f.read()
    return raw.replace("\r\n", "\n"), ("\r\n" if "\r\n" in raw else "\n")


def declaring(method, files):
    """The one file among FILES that declares METHOD; exits on none or several."""
    hits = [p for p in files if "    public void %s(" % method in read(p)[0]]
    if len(hits) != 1:
        sys.exit("%s is declared in %d files: %s" % (method, len(hits), ", ".join(p.name for p in hits)))
    return hits[0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["new", "add"])
    ap.add_argument("--after", help="new: the method the theory goes after")
    ap.add_argument("--method", help="new: the new theory's name")
    ap.add_argument("--assert", dest="asserts", action="append", default=[], help="Tag=true|false")
    ap.add_argument("--into", help="add: the theory that gets the cases")
    ap.add_argument("--comment", required=True)
    ap.add_argument("--cards", nargs="+", required=True)
    ap.add_argument("--dump")
    ap.add_argument("--test-file", help="the file to splice into (default: the one declaring --after/--into)")
    args = ap.parse_args()

    if args.mode == "add" and not args.into:
        sys.exit("add needs --into")
    if args.mode == "new" and not (args.after and args.method and args.asserts):
        sys.exit("new needs --after, --method and at least one --assert")

    by_name = {c["name"]: c for c in load_dump(args.dump)}
    missing = [n for n in args.cards if n not in by_name]
    if missing:
        sys.exit("not in the dump: " + ", ".join(missing))
    cases = "\n".join(inline_data(by_name[n]) for n in args.cards) + "\n"

    every = sorted(TEST_DIR.glob("EffectClassifierTests*.cs"))
    anchor_method = args.into if args.mode == "add" else args.after
    path = declaring(anchor_method, [Path(args.test_file)] if args.test_file else every)
    text, nl = read(path)

    if args.mode == "add":
        anchor = "    public void %s(" % args.into
        i = text.index(anchor)
        text = text[:i] + comment_lines(args.comment) + cases + text[i:]
    else:
        clash = [p.name for p in set(every) | {path} if "void %s(" % args.method in read(p)[0]]
        if clash:
            sys.exit("method already exists: %s in %s" % (args.method, ", ".join(clash)))
        checks = []
        for a in args.asserts:
            tag, value = a.split("=")
            checks.append("Assert.%s(%%s.HasFlag(CardEffect.%s));" % ("True" if value.lower() == "true" else "False", tag))
        if len(checks) == 1:
            body = "        " + checks[0] % "EffectClassifier.Classify(MakeCard(name, typeLine, oracle))" + "\n"
        else:
            body = ("        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));\n\n"
                    + "".join("        " + c % "result" + "\n" for c in checks))
        block = ("\n    [Theory]\n" + comment_lines(args.comment) + cases
                 + "    public void %s(string name, string typeLine, string oracle)\n    {\n" % args.method
                 + body + "    }\n")
        i = text.index("    public void %s(" % args.after)
        j = text.index("\n    }\n", i) + len("\n    }\n")
        text = text[:j] + block + text[j:]

    path.write_text(text.replace("\n", nl), encoding="utf-8-sig", newline="")
    print("%s: %d cases -> %s" % (args.mode, len(args.cards), path.name))


if __name__ == "__main__":
    main()
