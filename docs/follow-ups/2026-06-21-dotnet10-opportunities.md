# Valutazione — opportunità post-migrazione a .NET 10 (2026-06-21)

Il progetto è stato migrato a `net10.0` (SDK 10.0.301). Build pulita (0/0). Questa
nota raccoglie cosa conviene sfruttare della migrazione, calato su *questo* codice,
così la decisione resta tracciata.

> **Chiuso il 2026-09-04.** Tutte e tre le voci sono state affrontate: 1 e 2
> implementate, 3 misurata e scartata con i numeri sotto. Non resta lavoro
> aperto in questa nota.

## Priorità

| # | Intervento | Stato | Commit |
|---|-----------|-------|--------|
| 1 | Bump `Microsoft.Extensions.Logging.Console` + `ImplicitUsings` | FATTO | `80323eb`, `b04ce5c` |
| 2 | `<Nullable>enable</Nullable>` + fix dei warning | FATTO | `eac42da` |
| 3 | JSON source generation (`JsonSerializerContext`) sul path bulk-data | SCARTATO dopo misura | - |
| ❌ | NativeAOT / trimming | Sconsigliato (invariato) | - |

## Dettaglio

### 1. Quick win (sicuro)
- **Pacchetto logging disallineato:** `Microsoft.Extensions.Logging.Console` è fermo a
  `9.0.0` mentre il target è net10. Portarlo a `10.0.0` evita di trascinare assembly
  9.x accanto al runtime 10 e prende fix/perf. Una riga nel `.csproj`.
- **`<ImplicitUsings>enable</ImplicitUsings>`** per togliere i `using System;` &c.
  ripetuti in ogni file.
- **Primary constructor** dove tagliano boilerplate: `CardDownloader`,
  `IdleTimeoutStream`, `CardService`.
- Il linter ha già applicato le collection expression (`[]`).

**Fatto il 2026-09-04.** Pacchetto portato a **10.0.11** (ultima della linea 10.0,
non 10.0.0): in output ora ci sono assembly 10.0.11 per Logging, Options,
DependencyInjection e Primitives (commit `80323eb`). `ImplicitUsings` acceso sul
progetto principale — era già acceso solo su quello di test — e rimossi i sette
using che l'SDK fornisce (`System`, `Collections.Generic`, `IO`, `Linq`,
`Net.Http`, `Threading`, `Threading.Tasks`) dai 47 file che li portavano; 166
righe in meno, nessun'altra modifica (commit `b04ce5c`).

**Primary constructor: non fatti, decisione esplicita.** `CardService` è una
classe `static`, quindi non ha costruttore da convertire — era un errore della
lista. Restano `CardDownloader` e `IdleTimeoutStream`, dove i campi sono
`readonly`: i parametri di un primary constructor **non** lo sono, quindi la
conversione scambierebbe una garanzia del compilatore per tre righe in meno, e
introdurrebbe due classi con uno stile diverso da tutte le altre. Non vale il
cambio.

### 2. Nullable reference types (qualità)
Non è una feature di .NET 10, ma la migrazione è il momento giusto. `Nullable enable`
farebbe emergere a compile-time i rischi di null-deref già notati in code review
(es. le face-URI delle carte doppie). Costo medio (molti warning iniziali da
sistemare), alto valore di manutenibilità — allineato all'obiettivo "più
leggibile/modificabile".

**Fatto il 2026-09-04** (commit `eac42da`): 117 warning iniziali, ora build 0/0
con `Nullable enable`. La linea di taglio: i DTO di rete (`Json/**`) diventano
tutti `T?` — Scryfall omette davvero `image_uris` sulle doppia-faccia,
`card_faces` sulle singole, `oracle_text`, `promo_types` — mentre `Card`, che
li consuma, resta non-nullable e fa il coalesce in ingresso (testo a
`string.Empty`, liste a `[]`). Tre null-deref latenti sono venuti fuori davvero:
`CardFilter.IsPaperGame` faceva `card.Games.Count` su una lista che poteva
essere null; `DoubleFaceCard` indicizzava `CardFaces[0]`/`[1]` senza controllare
né la lista né la lunghezza, e `SingleFaceCard` leggeva `ImageUris.Png` sempre;
`GetCardSearch` paginava dereferenziando una variabile inizializzata a null.
Tutti e tre ora falliscono con il nome della carta invece che con una NRE.

### ✅ CA2024 — `StreamReader.EndOfStream` in metodo async (fatto, commit `6ca76b3`)
Analyzer **nuovo in .NET 10**: la migrazione l'ha fatto emergere su due loop
pre-esistenti in `GetManager` (`!reader.EndOfStream` + `reader.ReadLine()` sincroni
dentro metodi `async`). Risolto convertendo entrambi a `await reader.ReadLineAsync()`,
che cede il thread e restituisce `null` a fine stream — eliminando anche la potenziale
`NullReferenceException` del vecchio `ReadLine().Trim()`. Build di nuovo a 0 warning.

### 3. JSON source generation (il win "di sostanza", con caveat)
Il percorso caldo è la deserializzazione del bulk-data (centinaia di MB,
centinaia di migliaia di oggetti) che oggi passa per la reflection di
`System.Text.Json`. Un `JsonSerializerContext` source-gen sui DTO (`JsonCard`,
`JsonImageUris`, `JsonCardFace`, `CardSearch`, `Set`, `SetSearch`,
`BulkDataCollection`) riduce allocazioni e costo di startup ed è AOT-ready.
- **Caveat:** il `JsonCardConverter` custom resta (va tenuto), quindi il guadagno è
  soprattutto sulle allocazioni. Il *wall-clock* di quell'operazione è probabilmente
  dominato dal **download di rete**, non dalla reflection → non aspettarsi che dimezzi
  i tempi. Va ri-verificato che l'output resti identico.

> **Aggiornamento 2026-08-13:** il percorso bulk è cambiato forma. Scryfall
> migra i bulk export a **JSONL gzip** (`.jsonl.gz`), e il path .NET ora
> usa il nuovo `ScryfallClient.GetJsonLinesAsync<Card>` che deserializza
> **una `Card` per riga** via `JsonSerializer.Deserialize<Card>(line)`, non
> più `GetFromJsonAsync<List<Card>>` (vedi
> `docs/solutions/bugs/scryfall-bulk-data-migrated-to-jsonl-gz.md` e
> `pre-existing-findings.md` → "Lavoro 2026-08-13"). Il source-gen resta
> utile, ma ora si applicherebbe al **parser per-riga** invece che a un
> array deserializer; il guadagno relativo è minore (line-level reflection
> è già più leggero dell'array reflection per via delle allocazioni
> ridotte). Rivalutare dopo aver misurato.

> **SCARTATO il 2026-09-04, misurato.** Banco di prova: 120.000 righe JSONL
> realistiche (una riga Default Cards completa, campi ignorati inclusi)
> deserializzate in `JsonCard`, reflection contro un `JsonSerializerContext`
> source-gen sullo stesso DTO, **processo freddo** — che è esattamente come gira
> l'app, un bulk per run e poi esce. Tre esecuzioni per lato:
>
> | | run 1 | run 2 | run 3 |
> |---|---|---|---|
> | reflection | 1.041 ms | 970 ms | 967 ms |
> | source-gen | 1.070 ms | 1.029 ms | 1.031 ms |
>
> Il source-gen è **più lento del ~6%**, non più veloce. Il suo vantaggio sta
> nel costo di startup dei metadati e nell'AOT-readiness, ma qui i metadati di
> un solo DTO si costruiscono una volta e spariscono dentro 120k parse, e
> l'AOT è già stato scartato per Spectre e SkiaSharp. In più il parsing JSON
> vale circa **1 secondo** su un import che ne dura centinaia (il collo di
> bottiglia è il seek su disco meccanico, vedi il commento su `--incremental`).
> Aggiungere un contesto parziale da tenere allineato a ogni modifica dei DTO,
> per perdere il 6% su un secondo, non si giustifica.
>
> Da riaprire solo se cambia una delle premesse: NativeAOT torna in gioco, o il
> path bulk diventa dominante nel wall-clock.

### ❌ NativeAOT / trimming — sconsigliato (per ora)
Sarebbe ideale per un CLI (avvio istantaneo, singolo exe), ma tre ostacoli concreti:
- **Spectre.Console.Cli** lega comandi/settings via reflection;
- **SkiaSharp** ha native assets;
- il path JSON usa `JsonSerializer.Deserialize` a runtime.
Troppo attrito per il guadagno. Rivalutare solo se 2 e 3 vengono completati (il
source-gen rimuoverebbe l'ostacolo JSON).

> **Nota 2026-09-04:** il 2 è fatto, ma il 3 è stato scartato dopo misura, quindi
> l'ostacolo JSON resta e questa voce non si sblocca. Invariata.

## Nota trasversale
- Il `.csproj` non fissa `LangVersion`: su SDK 10 si ha **già C# 14** senza toccare nulla.
- La leva prestazionale più tangibile **non** è il JSON ma resta legata alla rete
  (bulk-data e immagini sono I/O-bound). Se un giorno si vorrà accelerare davvero, la
  mossa è il **download parallelo delle immagini** — a suo tempo classificato "bello ma
  non prioritario" nel brainstorm. Vedi requisiti di parallelismo nel piano.
