"""Download CubeCobra plaintext lists: python -I fetch_cubes.py OUT_DIR ID..."""
import sys
import time
import urllib.request
from pathlib import Path

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
for cid in sys.argv[2:]:
    dest = out / f"{cid}.txt"
    if dest.exists():
        continue
    req = urllib.request.Request(f"https://cubecobra.com/cube/download/plaintext/{cid}", headers={"User-Agent": "ScatoloneDownloader/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            text = r.read().decode("utf-8")
    except Exception as e:
        print("FAIL", cid, e)
        continue
    if not text.startswith("# mainboard"):
        print("BAD ID", cid, text[:60].replace("\n", " "))
        continue
    dest.write_text(text, encoding="utf-8")
    print("ok", cid)
    time.sleep(0.1)
