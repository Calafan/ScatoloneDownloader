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
        r = cards[name]
        chunks, cur = [], ""
        for word in r["text"].split(" "):
            if cur and len(cur) + len(word) + 1 > 100:
                chunks.append(cur + " ")
                cur = word
            else:
                cur = word if not cur else cur + " " + word
        chunks.append(cur)
        body = "\n        + ".join(cs(c) for c in chunks)
        print(f"    [InlineData({cs(r['name'])}, {cs(r['type'])},\n        {body})]")


if __name__ == "__main__":
    main()
