"""Write classifier tests from card names — the exact oracle text, spliced into the test file.

Every ruling lands as [InlineData] cases (the skill's step 6), and doing that
by hand meant a throwaway splice script per pass and an indentation fixed by
hand. This does both shapes:

    new   a new [Theory] after an existing method
    add   more cases (with their comment) at the end of an existing theory

Usage:
    python splice_tests.py new --after METHOD --method NAME --assert Bounce=true [--assert Wipe=false]
                               --comment "why these cards pin the ruling" --cards "A" "B" ...
    python splice_tests.py add --into METHOD --comment "…" --cards "A" "B" ...

--comment is prose; it is wrapped into // lines at 80 columns. The oracle text
comes from WORK_DIR/dump.jsonl (--dump to override), never retyped. The file
keeps its BOM and CRLF. Refuses when a card is not in the dump or the new
method name already exists. Run the suite afterwards, then neutralise.
"""
import argparse
import sys
import textwrap
from pathlib import Path

from common import CODE_REPO, load_dump
from gen_inline import inline_data

TEST_FILE = CODE_REPO / "ScatoloneDownloader.Tests" / "Mtg" / "EffectClassifierTests.cs"


def comment_lines(prose):
    return "\n".join("    // " + line for line in textwrap.wrap(prose, 76)) + "\n"


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
    ap.add_argument("--test-file", default=str(TEST_FILE))
    args = ap.parse_args()

    by_name = {c["name"]: c for c in load_dump(args.dump)}
    missing = [n for n in args.cards if n not in by_name]
    if missing:
        sys.exit("not in the dump: " + ", ".join(missing))
    cases = "\n".join(inline_data(by_name[n]) for n in args.cards) + "\n"

    path = Path(args.test_file)
    # newline="" keeps the CRLFs: read_text would translate them and the file
    # would be written back LF (references/hazards.md).
    with open(path, encoding="utf-8-sig", newline="") as f:
        raw = f.read()
    nl = "\r\n" if "\r\n" in raw else "\n"
    text = raw.replace("\r\n", "\n")

    if args.mode == "add":
        anchor = "    public void %s(" % args.into
        i = text.index(anchor)
        text = text[:i] + comment_lines(args.comment) + cases + text[i:]
    else:
        if not (args.after and args.method and args.asserts):
            sys.exit("new needs --after, --method and at least one --assert")
        if "void %s(" % args.method in text:
            sys.exit("method already exists: " + args.method)
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
