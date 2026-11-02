"""Download MTGJSON files: python -I fetch_mtgjson.py OUT_DIR CODE...   (CODE may be SetList)"""
import sys
import time
import urllib.request
from pathlib import Path

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
for code in sys.argv[2:]:
    name = "SetList.json" if code.lower() == "setlist" else f"{code.upper()}.json.gz"
    dest = out / name
    if dest.exists():
        continue
    req = urllib.request.Request(f"https://mtgjson.com/api/v5/{name}", headers={"User-Agent": "ScatoloneDownloader/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            dest.write_bytes(r.read())
        print("ok", name)
    except Exception as e:
        print("FAIL", name, e)
    time.sleep(0.1)
