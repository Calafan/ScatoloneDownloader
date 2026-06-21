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
| `analyze <FILES>` | Analizza le liste **senza** scaricare immagini | uno o più file |

### Opzioni comuni

| Opzione | Effetto |
|---------|---------|
| `-o, --output <DIR>` | Cartella radice di output (default: `./Output`) |
| `-c, --clear` | Cancella le cartelle di output prima di partire |
| `-r, --reprints` | Include i reprint (esclusi di default) |
| `-t, --tokens` | Include i token (esclusi di default) |
| `-p, --print-only` | Scrive solo la lista delle carte, senza scaricare immagini |
| `-h, --help` | Aiuto (anche per singolo comando, es. `years --help`) |

Opzioni specifiche:

- `all` — `-e, --exclude <FILE>`: esclude le carte elencate nel file.
- `files` — `-l, --lands`: aggiunge le terre base alla lista.

### Esempi

```powershell
# Carte del 2026
ScatoloneDownloader years 2026

# Più anni su un disco esterno
ScatoloneDownloader years 2024 2025 2026 --output D:\Scryfall

# Un paio di set, ripulendo prima la destinazione
ScatoloneDownloader set neo dmu --clear

# Da lista scritta a mano, incluse le terre base
ScatoloneDownloader files mazzo.txt --lands

# Solo analisi, nessun download
ScatoloneDownloader analyze mazzo.txt
```

## Struttura dell'output

Tutto finisce sotto la radice scelta (`./Output` di default):

```
<root>/
├─ All/    <anno>/<set>/<carta>.png
├─ Sets/   <set>/<carta>.png
├─ Years/  <anno>/<set>/<carta>.png
└─ Lists/  <nome-lista>/<tag>/<carta>.png
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
