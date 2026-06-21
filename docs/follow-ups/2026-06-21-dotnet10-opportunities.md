# Valutazione — opportunità post-migrazione a .NET 10 (2026-06-21)

Il progetto è stato migrato a `net10.0` (SDK 10.0.301). Build pulita (0/0). Questa
nota raccoglie cosa conviene sfruttare della migrazione, calato su *questo* codice,
così la decisione resta tracciata. **Nessuna di queste voci è stata applicata** —
decisione del 2026-06-21: solo valutazione.

## Priorità

| # | Intervento | Costo | Rischio | Note |
|---|-----------|-------|---------|------|
| 1 | Bump `Microsoft.Extensions.Logging.Console` 9.0.0 → 10.0.0 + `ImplicitUsings` + primary constructor | basso | nullo | Quick win |
| 2 | `<Nullable>enable</Nullable>` + fix dei warning | medio | basso | Cattura i null-deref segnalati in review |
| 3 | JSON source generation (`JsonSerializerContext`) sul path bulk-data | medio | basso | Da ri-verificare che l'output resti identico (R17/R6) |
| ❌ | NativeAOT / trimming | alto | alto | Sconsigliato (vedi sotto) |

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

### 2. Nullable reference types (qualità)
Non è una feature di .NET 10, ma la migrazione è il momento giusto. `Nullable enable`
farebbe emergere a compile-time i rischi di null-deref già notati in code review
(es. `StreamReader.ReadLine()` che può restituire `null`, le face-URI delle carte
doppie). Costo medio (molti warning iniziali da sistemare), alto valore di
manutenibilità — allineato all'obiettivo "più leggibile/modificabile".

### 3. JSON source generation (il win "di sostanza", con caveat)
Il percorso caldo è la deserializzazione del bulk-data (`List<Card>`, centinaia di MB,
centinaia di migliaia di oggetti) che oggi passa per la reflection di
`System.Text.Json`. Un `JsonSerializerContext` source-gen sui DTO (`JsonCard`,
`JsonImageUris`, `JsonCardFace`, `CardSearch`, `Set`, `SetSearch`,
`BulkDataCollection`) riduce allocazioni e costo di startup ed è AOT-ready.
- **Caveat:** il `JsonCardConverter` custom resta (va tenuto), quindi il guadagno è
  soprattutto sulle allocazioni. Il *wall-clock* di quell'operazione è probabilmente
  dominato dal **download di rete**, non dalla reflection → non aspettarsi che dimezzi
  i tempi. Va ri-verificato che l'output resti identico.

### ❌ NativeAOT / trimming — sconsigliato (per ora)
Sarebbe ideale per un CLI (avvio istantaneo, singolo exe), ma tre ostacoli concreti:
- **Spectre.Console.Cli** lega comandi/settings via reflection;
- **SkiaSharp** ha native assets;
- il path JSON usa `JsonSerializer.Deserialize` a runtime.
Troppo attrito per il guadagno. Rivalutare solo se 2 e 3 vengono completati (il
source-gen rimuoverebbe l'ostacolo JSON).

## Nota trasversale
- Il `.csproj` non fissa `LangVersion`: su SDK 10 si ha **già C# 14** senza toccare nulla.
- La leva prestazionale più tangibile **non** è il JSON ma resta legata alla rete
  (bulk-data e immagini sono I/O-bound). Se un giorno si vorrà accelerare davvero, la
  mossa è il **download parallelo delle immagini** — a suo tempo classificato "bello ma
  non prioritario" nel brainstorm. Vedi requisiti di parallelismo nel piano.
