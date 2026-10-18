"""Download the booster cards of a few retail draft sets from Scryfall search, one JSONL per set.

    python -I fetch_sets.py OUT_DIR SET [SET ...]
"""
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
HEADERS = {"User-Agent": "ScatoloneDownloader/1.0", "Accept": "application/json"}

for code in sys.argv[2:]:
    q = urllib.parse.quote(f"set:{code} is:booster -type:basic")
    url = f"https://api.scryfall.com/cards/search?q={q}&unique=cards"
    cards = []
    while url:
        with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS)) as r:
            page = json.load(r)
        cards.extend(page["data"])
        url = page.get("next_page") if page.get("has_more") else None
        time.sleep(0.2)
    with open(out / f"{code}.jsonl", "w", encoding="utf-8") as f:
        for c in cards:
            f.write(json.dumps(c, ensure_ascii=False) + "\n")
    print(code, len(cards))
