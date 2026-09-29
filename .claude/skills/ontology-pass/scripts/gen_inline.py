"""Print C# [InlineData(name, typeLine, oracle)] lines for the named cards,
taken straight from the `Dump` probe's dump.jsonl.

Tests carry real oracle text, never invented text, and never text retyped by
hand either: a transcription slip makes a test that pins the wrong card. Paste
the output under the [Theory] and add the comment that says what it pins.

Usage:
    python gen_inline.py <dump.jsonl> "Honor" "Apothecary Stomper" ...
"""
import json
import sys


def cs(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'


def oracle_literal(text, indent="        "):
    """The oracle text as a C# string expression wrapped near 100 columns."""
    chunks, cur = [], ""
    for word in text.split(" "):
        if cur and len(cur) + len(word) + 1 > 100:
            chunks.append(cur + " ")
            cur = word
        else:
            cur = word if not cur else cur + " " + word
    chunks.append(cur)
    return ("\n" + indent + "+ ").join(cs(c) for c in chunks)


def inline_data(r):
    """One [InlineData(name, typeLine, oracle)] attribute for a dump record."""
    return f"    [InlineData({cs(r['name'])}, {cs(r['type'])},\n        {oracle_literal(r['text'])})]"


def main():
    dump, names = sys.argv[1], sys.argv[2:]
    cards = {}
    with open(dump, encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            cards[r["name"]] = r

    for name in names:
        if name not in cards:
            print(f"// NOT IN THE DUMP: {name}")
            continue
        print(inline_data(cards[name]))


if __name__ == "__main__":
    main()
