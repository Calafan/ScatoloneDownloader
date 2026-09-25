"""Put the handed-back entries back into a post-classify copy of the store.

`classify --overwrite` rewrites every unreviewed entry, and a card handed back
for review is unreviewed on purpose — so the classify overwrites the human's
ruled tags with its own proposal. This copies those entries back from the
pre-classify backup into the post-classify copy, before that copy is split and
restored. Byte format kept: CRLF, 2-space indent, no BOM, no trailing newline.

Usage:
    python restore_handed_back.py <pre-classify-dir> <post-classify-dir> <handed-back.json>
"""
import json
import sys
from pathlib import Path

FILES = ["fringe.json", "pool.json", "unrated.json"]


def main():
    pre, post, listing = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    ids = {e["oracleId"] for e in json.loads(listing.read_text(encoding="utf-8"))}

    restored = found = 0
    for name in FILES:
        before = json.loads((pre / name).read_text(encoding="utf-8"))["cards"]
        data = json.loads((post / name).read_text(encoding="utf-8"))
        for k in ids & set(before):
            found += 1
            if data["cards"].get(k) != before[k]:
                restored += 1
            data["cards"][k] = before[k]
        text = json.dumps(data, indent=2, ensure_ascii=False).replace("\n", "\r\n")
        (post / name).write_text(text, encoding="utf-8", newline="")

    print(f"handed-back entries: {found} of {len(ids)} found, {restored} had been rewritten by classify")


if __name__ == "__main__":
    main()
