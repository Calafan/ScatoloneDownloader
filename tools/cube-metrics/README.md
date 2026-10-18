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
