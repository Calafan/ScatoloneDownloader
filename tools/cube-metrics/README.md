# cube-metrics

The scripts behind the cube measurements in `docs/`. Plain Python, no packages. Run them with
`python -I`, because they read files downloaded from Scryfall, MTGJSON and CubeCobra. Keep those
downloads in a directory of their own, outside this folder.

The cube (the Scatolone) is the store's pool: rating 3–5, no status. The first section measures it.
The second measures the old box list `_ToBe_Scatolone.txt` and is kept only to reproduce the
2026-10-18 measurement, now history: see [`docs/cube-targets.md`](../../docs/cube-targets.md).

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

## Rating consistency

`rating_gaps.py` lists the rated cards (1–5) whose ratings contradict each other: functional
reprints with different ratings, and a card rated below the card it improves on (the same card
plus an optional ability, cheaper or bigger). It only reads; the ratings are changed in the tagger
or by hand. On 2026-11-25 it found 11 reprint groups and 6 pairs, all raised to the higher rating
(store `b1a78bf`). Since 2026-11-27 both checks ignore creature subtypes: two cards that differ
only in them count as the same card (64 groups aligned upwards, store `bed47f6`).

```powershell
$D = "<a new empty directory for the downloads>"
$T = "E:\Working\Repos\Scryfall\ScatoloneDownloader\tools\cube-metrics"
$M = "E:\Working\Repos\ScatoloneQuintet\metadata"

# 1. Scryfall Oracle Cards bulk: GET https://api.scryfall.com/bulk-data/oracle-cards,
#    then download its jsonl_download_uri to "$D\oracle.jsonl.gz"

# 2. the check
python -I "$T\rating_gaps.py" "$D\oracle.jsonl.gz" $M
```

A `better` pair marked `check` adds a triggered or static ability: read it before trusting it.

`cube_presence.py` lists the store cards that the 8 reference cubes contradict: rated 1–2 in at
least 3 cubes, rated 5 in no cube, not rated in at least 3 cubes. Read it again after the review
of each year. Presence in the cubes says "pool or not", not which rating, so the lists are cards
to look at, not proposals. On 2026-11-25: 35, 49 (45 without status), 132.

```powershell
# the bulk of step 1 above, and the cubes of step 3 of "Limited over time"
python -I "$T\fetch_cubes.py" "$D\cubes" modovintage LSVCube 5d617ac6c2a85f3b75fe95a4 gianpeasant 2hgcb 42a6g commandercube thegiantcube
python -I "$T\cube_presence.py" "$D\oracle.jsonl.gz" $M "$D\cubes"
```

`family_review.py` writes, by library year, the families of cards to rate together: the same text
in other colours (Wrath of God and Damnation, River Boa and Mire Boa) and the design cycles of one
set ("Dictate of …", "… Charm"). The colour counts in a rating, so it proposes none: each family
with an unrated card goes under that card's year, next to the ratings its other cards already
have. Run it again before the review of each year. On 2026-11-27: 17 years, 801 cards to rate.

```powershell
# the bulk of step 1 above; the library's Source folder gives each card its year and set
python -I "$T\family_review.py" "$D\oracle.jsonl.gz" $M "E:\Working\Repos\ScatoloneQuintet\Source" "$D\family-review.md"
```

## History: the 2026-10-18 measurement of the box list

The box list against nine retail draft sets, the halfway table and the swap proposal. The box was
the `-- Scatolone` section of `_ToBe_Scatolone.txt`, not the pool, so these numbers do not compare
with the pool's.

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

# 4. the measurements, the halfway table, the swap
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
| `targets_table.py` | the Markdown table of the 2026-10-18 measurement (box, retail, halfway, after the swap), per card and per draft |
| `candidates.py` | where cuts and adds can come from, by rating and mana value |
| `simulate.py` | the box after the swap, measured with the same metrics |
| `proposal_md.py` | the A/B/C proposal file with checkboxes |

`proposal_md.py` overwrites its output file; check it holds nothing of the user's first.
