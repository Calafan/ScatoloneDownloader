# Follow-up — segnalazioni pre-esistenti (code review 2026-06-21)

Raccolta dei punti emersi dalla `ce-code-review` del branch `refactor` che **non**
sono stati risolti nel commit `b5318b1`, perché pre-esistenti al refactor o perché
cambierebbero il comportamento osservabile (vincolo **R17 — preservare il
comportamento esistente**). Vanno valutati come lavoro separato, ciascuno con una
decisione di progetto esplicita.

Le due segnalazioni *introdotte* dal refactor e sicure (streaming bulk-data,
dispose di `HttpClient`) sono già state applicate — vedi commit `b5318b1`.

## Stato (aggiornato 2026-06-21)
| ID | Stato | Commit |
|----|-------|--------|
| FU-1 | ✅ Risolto | `e1f3ed6` |
| FU-2 | 🟡 Deciso: lasciato com'è (documentato) — vedi nota nella sezione FU-2 | — |
| FU-3 | ✅ Risolto | `a783ec7` |
| FU-4 | ✅ Risolto — idle-timeout per-read (30 s) | `IdleTimeoutStream` |
| FU-5 | ✅ Risolto | `a783ec7` |
| FU-6 | ✅ Risolto | `a783ec7` |
| FU-7 | ✅ Risolto | `a783ec7` |
| FU-8 | ✅ Risolto | `a783ec7` |

## Legenda
- **Severità**: P1 alto impatto · P2 medio · P3 basso.
- **Tipo**: `pre-existing` = già presente prima del refactor · `behavior-change` =
  la fix altera il comportamento osservabile (richiede ok esplicito).

---

## FU-1 · Path traversal residuo via `Tag` (P2, pre-existing + behavior-change)
**File:** `ScatoloneDownloader/Download/OutputPaths.cs:71-74`

In modalità `Files`, `card.Tag` viene concatenato grezzo al percorso
(`Path.Combine(path, card.Tag)`) senza passare da `Sanitize`. Un tag che contiene
`..` o separatori può far scrivere immagini fuori dalla cartella di output.

- **Perché conta:** l'input arriva dalla lista scritta a mano (`name -- tag`); un
  tag malformato (anche per errore di battitura, es. `../`) scrive in posizioni
  impreviste.
- **Fix proposta:** passare `card.Tag` per `Sanitize` (già esiste) prima del
  `Combine`, oppure normalizzare e verificare che il path risultante resti sotto
  `BasePaths[Mode.Files]`.
- **Nota R17:** `Sanitize` rimuove i caratteri vietati ma **non** `..`; cambia il
  nome cartella prodotto per tag che oggi contengono caratteri filtrati →
  comportamento osservabile diverso. Decidere se accettabile.

## FU-2 · `Card.Tag` mutabile su istanze condivise in cache (P2, behavior-change)
**File:** `ScatoloneDownloader/Mtg/Card.cs` (`Tag { get; set; }`),
mutato in `ScatoloneDownloader/GetManager.cs:291` e `:141`

`CardsByName` mette in cache istanze `Card` riusate tra chiamate successive.
`Tag` è l'unica proprietà mutabile (`set`); assegnarla durante
`GetCardList`/`PopulateCardsByName` muta l'oggetto condiviso, quindi il tag di una
run può "trapelare" in una run successiva sullo stesso processo.

- **Perché conta:** oggi è mascherato perché ogni `GetManager` è usa-e-getta
  (`using` per run); diventerebbe un bug reale se la cache fosse condivisa o se si
  rieseguisse più volte lo stesso manager.
- **Fix proposta:** rendere `Card` immutabile anche su `Tag` e produrre una copia
  con il tag (record `with`) al momento dell'assegnazione, invece di mutare
  l'istanza in cache.
- **Nota:** tocca il confine del modello dati → richiede decisione di design.
- **DECISIONE (2026-06-21): lasciato com'è.** Oggi ogni file di lista crea un
  `GetManager` nuovo (`using GetManager getManager = new();` per chiamata), quindi
  `CardsByName` non è condiviso tra run e la finestra di rischio è chiusa in pratica.
  Lo scenario che la riaprirebbe è riusare *un solo* `GetManager` per più file (per
  evitare di riscaricare il bulk-data ogni volta): se/quando si farà quella
  ottimizzazione, rendere `Tag` immutabile **nello stesso intervento**.

## FU-3 · `catch {}` muto in `PopulateCardsByName` (P3, pre-existing)
**File:** `ScatoloneDownloader/GetManager.cs:118-121`

Il `catch` cattura qualsiasi eccezione e logga solo "Missing parameters", perdendo
il tipo/stack dell'errore reale. Comportamento ereditato dal codice originale.

- **Fix proposta:** loggare `ex` (`Logger.LogError(ex, ...)`) e restringere il tipo
  catturato se possibile.

## FU-4 · Nessun timeout HTTP esplicito (P2, behavior-change — ATTENZIONE)
**File:** `ScatoloneDownloader/Scryfall/ScryfallClient.cs`

`HttpClient` usa il timeout di default (100s) sull'intera richiesta. **Non**
applicare un timeout breve ingenuo (es. 30s): romperebbe il download legittimo dei
bulk-data Scryfall, che richiedono minuti. Con lo streaming (`ResponseHeadersRead`,
già applicato) il default copre solo gli header; per protezione vera serve un
timeout *per-read* via `CancellationToken`, non `HttpClient.Timeout`.

- **DECISIONE (2026-06-21): idle-timeout per-read.** Introdotto
  `ScatoloneDownloader/Scryfall/IdleTimeoutStream.cs`, che avvolge lo stream di
  risposta e cancella la singola lettura se non arrivano byte per 30 s
  (`ReadIdleTimeout`). Il timer si resetta a ogni lettura, quindi un download lungo
  ma sano non viene mai tagliato — solo una connessione muta a metà corpo. Applicato
  al percorso bulk-data (`GetFromJsonAsync`); le immagini restano sul timeout totale
  di default (payload piccoli).

## FU-5 · `File.Move` non protetto sulla regola canonical-artwork (P3, pre-existing)
**File:** `ScatoloneDownloader/Download/CardDownloader.cs:45`

`File.Move(source, dest)` presuppone che `source` esista; se la sequenza di naming
non garantisce la presenza del file base, lancia. Logica ereditata dal codice
originale e duplicata in due punti (vedi commento a `:42`).

- **Fix proposta:** guardia `File.Exists` prima del `Move`, oppure unificare la
  regola canonical-artwork (oggi in `GetManager.PopulateCardsByName` **e** qui).

## FU-6 · `DateTime.Now` nel throttle (P3, pre-existing)
**File:** `ScatoloneDownloader/Scryfall/ScryfallClient.cs:31,38`

Il gate di rate-limit usa `DateTime.Now` (ora locale, sensibile a cambi DST/orario)
invece di un orologio monotòno.

- **Fix proposta:** usare `Stopwatch.GetTimestamp()` / `Environment.TickCount64` per
  l'intervallo minimo tra richieste.

## FU-7 · `AppLogger.Configure` è un extension point morto (P3, pre-existing)
**File:** `ScatoloneDownloader/Logging/AppLogger.cs:23`

`Configure` non è mai chiamato; il logger statico di `GetManager` è inizializzato a
class-load, quindi una `Configure` successiva non avrebbe comunque effetto.

- **Fix proposta:** rimuovere `Configure` (YAGNI) oppure spostare l'inizializzazione
  dei logger dietro la factory così che `Configure` abbia un effetto reale.

## FU-8 · Stream immagine non disposti in `ComposeAsync` (P3)
**File:** `ScatoloneDownloader/Download/CardDownloader.cs:66-67,74`

Gli `Stream` ottenuti da `GetImageStreamAsync` non vengono disposti. Sono
`MemoryStream` (nessuna risorsa non gestita → impatto pratico nullo), ma per pulizia
andrebbero in `using`.

- **Fix proposta:** `using` sugli stream front/rear/image, oppure far sì che il
  composer li disponga.

---

## Lavoro post-review applicato (2026-06-21)
Interventi fatti **dopo** la chiusura dei FU sopra, durante l'hardening del branch
`refactor`. Tutti committati, build pulita (0 warning / 0 errori).

| Intervento | Commit | Note |
|-----------|--------|------|
| Retry 429/5xx con backoff (honra `Retry-After`) + intervallo richieste 100→150 ms | `185ecd3` | Il rate-limit dell'API è il vincolo binding: senza retry il paging di `cards/search` tripava 429. Confermato da una run reale 2026. |
| Misura throughput a fine download (baseline) | `f369af3` | Colonna tempo trascorso sulla progress bar + riga `N carte in Xs — ms/carta, carte/s`. |
| Publish single-file (framework-dependent, win-x64) | `8387367` | `dotnet publish -c Release -r win-x64` → **1 solo** `ScatoloneDownloader.exe` (~43 MB, native SkiaSharp embedded) invece di ~32 file. Solo `publish`; `build` invariato. |
| Fix CA2024 (`StreamReader.EndOfStream` in metodo async) | `6ca76b3` | I due loop di lettura file in `GetManager` ora usano `await reader.ReadLineAsync()`. Vedi anche [dotnet10-opportunities](2026-06-21-dotnet10-opportunities.md). |
| Output root configurabile, default `./Output` | `5535edd` | Le immagini finivano in cartelle piatte `./All`, `./Sets`, `./Years`, `./Lists` relative alla cwd → sparse ovunque venisse lanciato l'app. Ora tutte sotto un'unica radice, sovrascrivibile con `-o\|--output <DIR>`. |

### Nota sull'output root (`5535edd`)
- `OutputPaths.Root` (default `./Output`) tiene la radice; `BasePath(mode)`/`BasePaths`
  derivano le sotto-cartelle per-modo da essa. Sostituisce il vecchio dizionario
  `BasePaths` con prefisso `"."` hardcoded (citato in FU-1).
- Un `ICommandInterceptor` globale (`OutputPathInterceptor`) applica `--output` **prima**
  di qualunque comando, così anche `--clear` agisce sulla radice scelta.
- Struttura risultante: `<root>/{All,Sets,Years,Lists}/...` (gerarchia interna invariata).

### Baseline throughput (run reale 2026)
1664 carte in 481,8 s → ~290 ms/carta, **3,45 carte/s** (download sequenziale). Numero
di riferimento per valutare un eventuale download parallelo delle immagini (vedi nota
trasversale in [dotnet10-opportunities](2026-06-21-dotnet10-opportunities.md)).

---

## Lacune di test (deferite per scelta)
Nessun test automatizzato. Aree a maggior rischio di regressione da coprire quando
si introdurranno i test:
- equivalenza del filtro carte: `CardFilter` vs il vecchio `Card.IsValid`;
- geometria della composizione fronte-retro (affiancamento + riduzione a singola
  carta) — finora verificata **manualmente in stampa** (R6).
