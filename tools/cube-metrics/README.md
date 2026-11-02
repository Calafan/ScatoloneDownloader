# cube-metrics

Exploratory scripts behind [`docs/cube-targets.md`](../../docs/cube-targets.md): the box list
against retail draft sets, the targets table, and the swap proposal. Plain Python, no packages.
Run them with `python -I`, because they read files downloaded from Scryfall. Keep those downloads
in a directory of their own, outside this folder.

```powershell
$D = "<a new empty directory for the downloads>"
$T = "E:\Working\Repos\Scryfall\ScatoloneDownloader\tools\cube-metrics"
$M = "E:\Working\Repos\ScatoloneQuintet\metadata"

# 1. Scryfall Oracle Cards bulk (one object per oracle id: cmc, type, power/toughness, keywords)
curl.exe -s -H "User-Agent: ScatoloneDownloader/1.0" https://api.scryfall.com/bulk-data/oracle-cards -o "$D\meta.json"
#    then download its jsonl_download_uri to "$D\oracle.jsonl.gz"

# 2. the retail reference: booster cards of nine draft sets
python -I "$T\fetch_sets.py" "$D\sets" fdn dsk blb otj mkm lci woe dft tdm

# 3. the box list joined with Scryfall and the store
python -I "$T\box_join.py" E:\Working\_ToBe_Scatolone.txt "$D\oracle.jsonl.gz" $M "$D\box.jsonl"

# 4. the measurements, the targets table, the swap
python -I "$T\speed_metrics.py" "$D\box.jsonl" "$D\sets" $M
python -I "$T\targets_table.py" "$D\box.jsonl" "$D\oracle.jsonl.gz" $M "$D\sets"
python -I "$T\candidates.py" "$D\box.jsonl" "$D\oracle.jsonl.gz" $M $D
python -I "$T\proposal_md.py" "$D\box.jsonl" "$D\oracle.jsonl.gz" $M E:\Working\_ToBe_Scatolone_proposta.md
```

| script | what it does |
|---|---|
| `box_join.py` | parses the list (sections, `-- tag` suffixes, a trailing digit on a name) and joins each card with Scryfall and the store |
| `fetch_sets.py` | `set:X is:booster -type:basic` from Scryfall search, one JSONL per set |
| `speed_metrics.py` | the metrics, for the box by rating group and for retail weighted as opened |
| `targets_table.py` | the Markdown table of the targets document, per card and per draft |
| `candidates.py` | where cuts and adds can come from, by rating and mana value |
| `simulate.py` | the box after the swap, measured with the same metrics |
| `proposal_md.py` | the A/B/C proposal file with checkboxes |

`proposal_md.py` overwrites its output file; check it holds nothing of the user's first.

## Limited over time, reference cubes and the pool

The scripts behind [`docs/limited-history.md`](../../docs/limited-history.md): 83 retail Limited
formats weighted as opened, from the booster sheets of MTGJSON, next to 8 CubeCobra cubes and the
pool, with the metric definitions of `speed_metrics.py`.

```powershell
$D = "<a new empty directory for the downloads>"
$T = "E:\Working\Repos\Scryfall\ScatoloneDownloader\tools\cube-metrics"
$M = "E:\Working\Repos\ScatoloneQuintet\metadata"

# 1. Scryfall Oracle Cards bulk: GET https://api.scryfall.com/bulk-data/oracle-cards,
#    then download its jsonl_download_uri to "$D\scryfall\oracle.jsonl.gz"

# 2. MTGJSON set files: every set in formats.json, plus the sets that hold the cards of bonus
#    sheets (Time Spiral timeshifted, Mystical Archive, The List, source material...)
$sets = python -I -c "import json,sys; print(' '.join(sorted({p for f in json.load(open(sys.argv[1], encoding='utf-8')) for p in f['packs']})))" "$T\formats.json"
python -I "$T\fetch_mtgjson.py" "$D\mtgjson" SetList @($sets -split ' ')
python -I "$T\fetch_mtgjson.py" "$D\mtgjson" TSB STA BRR MUL WOT OTP BIG SPG PLST EOS FCA SPE TLE PZA SOA EXP MPS MP2 MAR

# 3. the cubes
python -I "$T\fetch_cubes.py" "$D\cubes" modovintage LSVCube 5d617ac6c2a85f3b75fe95a4 gianpeasant 2hgcb 42a6g commandercube thegiantcube

# 4. the measurements and the charts
python -I "$T\limited_history.py" "$D\scryfall\oracle.jsonl.gz" $M "$D\mtgjson" "$D\cubes" "$T\formats.json" "$D\out"
python -I "$T\history_html.py" "$D\out\history.json" "$D\out\limited-history.html"
```

`limited_history.py` prints, for each set, the product used and the cards per pack it adds up to
(15 for a draft booster, 14 for a play booster), and the cards it could not resolve to an oracle
id. A new set with a bonus sheet usually needs its bonus set added to step 2: the name of the
sheet points to it.

| script | what it does |
|---|---|
| `formats.json` | the 83 formats: id, label, kind (3 sets, 2 sets, 1 set, core), one set code per pack, date of the latest set |
| `fetch_mtgjson.py` | MTGJSON `<SET>.json.gz` files (or `SetList`), skipping the ones already there |
| `fetch_cubes.py` | CubeCobra plaintext lists; refuses a file that does not start with `# mainboard` |
| `history_lib.py` | loading, the booster model (expected copies per pack per oracle id), the metrics |
| `limited_history.py` | every format, cube and the pool: `history.json` and `history_tables.md` |
| `history_html.py` | `history.json` to one static HTML page with inline SVG charts |

New sets: `formats.json` holds every expansion with a draft or play booster up to Reality Fracture
(2026-10-02); add a later set by hand, as one more `1 set` entry.
