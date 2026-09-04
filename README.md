# ScatoloneDownloader

CLI per scaricare le immagini delle carte di *Magic: The Gathering* da
[Scryfall](https://scryfall.com), organizzandole in cartelle pronte per la stampa.
Le carte fronte‑retro vengono composte in un'unica immagine.

## Requisiti

- [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (per
  l'eseguibile pubblicato) oppure l'SDK .NET 10 per compilare dai sorgenti.
- Connessione a Internet (l'app rispetta il rate limit di Scryfall, ~10 richieste/s,
  con retry automatico su 429/5xx).

## Compilazione

```powershell
# Eseguire dai sorgenti
dotnet run --project ScatoloneDownloader -- <comando> [opzioni]

# Pubblicare un singolo eseguibile (framework-dependent, Windows x64)
dotnet publish -c Release -r win-x64
# → ScatoloneDownloader/bin/Release/net10.0/win-x64/publish/ScatoloneDownloader.exe
```

Il `publish` produce **un solo** `ScatoloneDownloader.exe` (~43 MB, librerie native
SkiaSharp incluse). Richiede il runtime .NET 10 installato.

## Uso

```
ScatoloneDownloader <comando> [argomenti] [opzioni]
```

### Comandi

| Comando | Descrizione | Argomento |
|---------|-------------|-----------|
| `all` | Scarica tutte le carte unique‑artwork, raggruppate per anno e set | — |
| `set <SETS>` | Scarica i set indicati per codice | uno o più codici set (es. `neo dmu`) |
| `years <YEARS>` | Scarica le carte uscite negli anni indicati (1993–2050) | uno o più anni |
| `files <FILES>` | Scarica dalle liste scritte a mano e genera un file di statistiche | uno o più file |
| `lands` | Scarica **tutti** gli artwork delle terre base, divisi per tipo | — |
| `analyze <FILES>` | Analizza le liste **senza** scaricare immagini | uno o più file |

### Opzioni comuni

| Opzione | Effetto |
|---------|---------|
| `-o, --output <DIR>` | Cartella radice di output (default: `./Output`) |
| `-c, --clear` | Cancella le cartelle di output prima di partire |
| `-r, --reprints` | Include i reprint (esclusi di default) |
| `-t, --tokens` | Include i token (esclusi di default) |
| `-l, --lands` | Include le terre base (escluse di default) |
| `-p, --print-only` | Scrive solo la lista delle carte, senza scaricare immagini |
| `-h, --help` | Aiuto (anche per singolo comando, es. `years --help`) |

Opzioni specifiche:

- `all` — `-e, --exclude <FILE>`: esclude le carte elencate nel file.
- `lands` — usa solo le opzioni generali `-o/--output`, `-c/--clear`, `-p/--print-only`
  (i filtri reprint/token/lands non si applicano: scarica ogni artwork di terra base).

### Esempi

```powershell
# Carte del 2026
ScatoloneDownloader years 2026

# Più anni su un disco esterno
ScatoloneDownloader years 2024 2025 2026 --output D:\Scryfall

# Un paio di set, ripulendo prima la destinazione
ScatoloneDownloader set neo dmu --clear

# Carte del 2026, terre base incluse
ScatoloneDownloader years 2026 --lands

# Da lista scritta a mano, incluse le terre base
ScatoloneDownloader files mazzo.txt --lands

# Tutte le terre base stampate, divise per tipo
ScatoloneDownloader lands

# Solo analisi, nessun download
ScatoloneDownloader analyze mazzo.txt
```

## Gestione del cubo (rating, status, effetti)

Oltre al download, il tool gestisce la valutazione del cubo: rating, status
(Banned/Token/Jolly) ed effetti funzionali delle carte, salvati nella cartella
`metadata/` (tracciata da git — è la fonte di verità: git + Scryfall bastano a
ricostruire tutto). La cartella è partizionata per fascia di rating —
`pool.json` (3-5), `fringe.json` (1-2), `unrated.json` (0, l'intera libreria
non ancora valutata) — così il file da editare a mano resta piccolo anche con
30k+ carte. Dettagli completi, schema JSON e struttura delle viste in
[`docs/cube-metadata.md`](docs/cube-metadata.md).

| Comando | Descrizione | Esempio |
|---------|-------------|---------|
| `tag <DIR>` | Avvia il tagger web locale (da tastiera) per assegnare rating, status ed effetti; salva automaticamente su `metadata/` a ogni modifica. Apre sulla coda da revisionare (carte non taggate + auto-taggate non ancora confermate), in ordine casuale. Filtri combinabili per stato (`f`), livello (`,`: pool 3-5 / fringe 1-2 / non valutate / stelle esatte) e cartella (anno + set); `/` apre la lista carte con ricerca per nome | `ScatoloneDownloader tag .\Master` |
| `import <DIR>` | Porta dentro `metadata/` i rating/label XMP scritti da Adobe Bridge (unico comando che legge ancora XMP). Da rilanciare dopo ogni sessione di Bridge; `--incremental` rilegge solo i file modificati dall'ultimo import | `ScatoloneDownloader import .\Master --overwrite --incremental` |
| `build-views <DIR>` | Rigenera l'albero `Views/` (symlink/hardlink, multi-radice) e il report `Cubo_Analysis.md` leggendo rating/status/effetti da `metadata/` | `ScatoloneDownloader build-views .\Master -v .\Views` |
| `restore --images <DIR>` | Recovery: ricostruisce la cartella immagini dall'unione di tutti i file di `metadata/` + bulk-data Scryfall (nessuna XMP scritta) | `ScatoloneDownloader restore --images .\Master -m metadata` |
| `make-list -o <FILE>` | Genera (offline) una lista di download per il comando `files` con il solo pool (rating 3-5); i card con status finiscono in sezioni `-- Banned`/`-- Token`/`-- Jolly` così `files` li smista in sotto-cartelle | `ScatoloneDownloader make-list -m metadata -o pool.txt` |
| `classify` | Auto-propone gli effetti dal testo regole Scryfall dentro `metadata/` (solo suggerimenti: scrive `effects` ma non marca `reviewedAt`, non tocca le carte già revisionate; confermali nel tagger) | `ScatoloneDownloader classify -m metadata --dry-run` |

Tutti accettano `-m, --metadata <DIR>` per la cartella dei metadati. Se omesso,
il default e una cartella `metadata` **accanto alla libreria master** (la sorella
di `SOURCE_DIR`, stessa regola con cui `build-views` colloca `Views/`), cosi i
metadati stanno vicino alle immagini che descrivono invece di seguire la
directory da cui lanci il comando. `classify`, `make-list` e `restore` non
ricevono `SOURCE_DIR`: non hanno un master accanto a cui stare e ricadono su
`./metadata`, stampando all'avvio il percorso risolto.

### Lavorare ancora con Adobe Bridge

Il tagger e `metadata/` sono la fonte di verità, ma si può continuare a mettere
rating e label da Adobe Bridge sui PNG della libreria master e riportarli dentro
con `import --overwrite`. Il round-trip non perde lavoro nelle due direzioni:
`import` non declassa mai un rating già salvato a 0, non riscrive lo status di
una carta già revisionata nel tagger, e lascia intatti effetti, `scryfallId` e
`reviewedAt`. A fine run stampa `Changed: n ratings, n labels, n statuses`, così
si vede subito se la sessione di Bridge è arrivata davvero a destinazione.

La libreria è su un disco meccanico e la scansione XMP completa è vincolata dai
seek (circa 15 minuti su 30k file): dopo il primo import usa `--incremental`, che
rilegge solo i file toccati dall'ultima esecuzione (watermark in
`metadata/import-state.json`) e chiude in pochi secondi. Le carte non ancora
presenti nello store vengono comunque sempre lette.

## Struttura dell'output

Tutto finisce sotto la radice scelta (`./Output` di default):

```
<root>/
├─ All/        <anno>/<set>/<carta>.png
├─ Sets/       <set>/<carta>.png
├─ Years/      <anno>/<set>/<carta>.png
├─ Lists/      <nome-lista>/<tag>/<carta>.png
└─ BasicLands/ <tipo>/<carta>.png        (comando lands: Plains/, Island/, ...)
```

## Formato dei file di lista (`files` / `analyze`)

Un file di testo, una carta per riga. Il tag (opzionale) dopo `--` determina la
sotto‑cartella in cui finisce l'immagine:

```
Sol Ring -- artefatti
Lightning Bolt -- rosse
Counterspell
-- questa riga è un commento (le righe che iniziano con -- vengono ignorate)
```

- `Nome -- tag` → immagine in `Lists/<lista>/<tag>/`.
- `Nome` senza tag → immagine direttamente in `Lists/<lista>/`.
- Le terre base sono trattate a parte e incluse solo con `--lands`.

## Note

- Le immagini sono recuperate da Scryfall nel formato di stampa; le carte a doppia
  faccia vengono affiancate in un'unica immagine.
- Il download è sequenziale e regolato per rispettare il rate limit di Scryfall;
  a fine run viene stampato il throughput (carte totali, ms/carta, carte/s).
- I dati delle carte provengono dall'API e dai bulk‑data di Scryfall. Per favore,
  rispetta i [termini d'uso](https://scryfall.com/docs/api) di Scryfall.
