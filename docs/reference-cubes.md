# Scatolone: confronto con cubi di riferimento

Documento del punto 3 del piano di miglioramento del cubo. Ricerca del 2026-10-31. Il confronto
statistico con gli 8 cubi della tabella, insieme al Limited retail nel tempo, è in
[`limited-history.md`](limited-history.md) (2026-11-02).

Lo scopo è passare liste di cubi noti a `analyze <list>` e mettere i numeri accanto a quelli del
pool, per capire se la diagnosi dell'utente ("troppo lento e con troppe soluzioni") vale anche
rispetto ad altri cubi e non solo rispetto al Limited retail.

Legenda delle fonti: **verificato** = ricontrollato a mano sulla fonte; **riportato** = raccolto
dalla ricerca ma non ricontrollato.

## Scaricare le liste

CubeCobra esporta le liste senza login né chiave (verificato):

- `https://cubecobra.com/cube/download/plaintext/<id>`: prima riga `# mainboard`, poi un nome per
  riga, senza quantità, set o tag. Una sezione maybeboard arriverebbe dopo un altro `#` (non
  provato).
- Un id inesistente risponde comunque HTTP 200, con la pagina HTML d'errore: prima di usare il
  file controllare che la prima riga sia `# mainboard`.
- Funzionano anche `/cube/download/csv/<id>` (colonne `name,CMC,Type,Color,Set,Collector
  Number,Rarity,...`, tag compresi) e `/cube/api/cubejson/<id>` (oggetto completo con dimensione e
  data di aggiornamento) (riportato).

Provati il 2026-10-31: `modovintage` 540 carte, `2hgcb` 574, `42a6g` 531.

## I nomi delle liste in `analyze`

Verificato sul codice e sulle liste scaricate il 2026-11-01:

- CubeCobra scrive le carte a doppia faccia e le avventure col solo nome del fronte
  (`Fable of the Mirror-Breaker`, `Brazen Borrower`); le carte divise col nome intero
  (`Life // Death`).
- `GetManager.GetCardList` cerca il nome come è scritto e, se non lo trova, come fronte di una
  carta a più facce (`BuildFullNamesByFront`). Non contano come fronti le carte d'arte, le
  reversibili, i token e gli emblemi, né i 3 fronti che appartengono a due carte diverse
  (`Start`, `Monster`, `Royal`).
- Carte trovate col solo fronte: `modovintage` 18, `2hgcb` 54, `42a6g` 12, `LSVCube` 17. Nelle
  quattro liste l'unica riga non trovata è l'intestazione `# mainboard`.
- Una carta non trovata compare a schermo come riga di log (`warn: ... Missing card: <nome>`) e,
  letta la lista, nel riepilogo `<lista>: not found (N): <nomi>`.
- Una carta ripetuta conta una volta sola (`Duplicate card:`). `2hgcb` ha 574 righe e 547 carte:
  tiene due copie delle bounceland e di Ash Barrens, e fino a quattro di ogni Gate di Battlebond.

## Cubi candidati

Dimensioni e date dall'API JSON di CubeCobra, il 2026-10-31 (riportato; le prime tre righe sono
verificate).

| Ruolo | Cubo | Curatore | id | Carte | Aggiornato |
|---|---|---|---|---|---|
| Potenza massima | MTGO Vintage Cube (copia della community) | Dekkaru | `modovintage` | 540 | 2026-10-13 |
| Legacy classico | LSVCube | LSVCube | `LSVCube` | 515 | 2026-10-24 |
| Bassa potenza, curato | The Pauper Cube | Adam Styborski | `5d617ac6c2a85f3b75fe95a4` | 450 | 2026-10-30 |
| Potenza media | Gian's Peasant+ Cube | Gian519 | `gianpeasant` | 540 | 2026-10-24 |
| 2v2 | Two-Headed Giant Cube | Silverblood | `2hgcb` | 574 | 2026-08-04 |
| 2v2, regole Battlebond (4 buste per squadra) | Nandochip's Two Headed Giant Unpowered Cube | nandochip | `42a6g` | 531 | 2024-11-04 |
| Multiplayer grande | Commander Cube | DirkJammer | `commandercube` | 960 | 2026-08-30 |
| Dimensioni enormi | Cube Elder's Giant Un-powered 2880 Cube | RocketJonson | `thegiantcube` | 2880 | 2023-12-26 |

Altri trovati, di riserva:

- Legacy+ Cube `legacy` (450), Legacy Cube `leg` (600).
- Pauper Cube `pz4` (450), Team Pauper Cube `teampaupercube` (380), Allston Peasant `nkpc` (442).
- 2HG fermi al 2022: `1xadv` (480), `2tq5u` (480), `44vpc` (360); `compact-2hg` (288);
  Multiplayer Vintage Cube `10ku` (480).
- Commander: `26aij` (600), `laying-pipe-cube` (600, peasant), `vtq` (610, peasant).
- Grandi: Ultimate Commander Cube `1cnb` (1000), Millennium Cube `Millennium2023` (999).
- Set-like: Battlebond/Conspiracy 2 Multiplayer Cube `bbdcn2` (384).

Un Arena Cube verificato non c'è: l'id `arena` è vuoto.

Note:

- I cubi Commander contengono i comandanti, quindi più creature leggendarie del normale.
- **Battlebond (BBD)** è il set di Wizards pensato per il draft Two-Headed Giant: si può aggiungere
  come riferimento "retail 2v2" con gli script di `tools/cube-metrics`, pesato per rarità come gli
  altri set retail.

## Numeri di riferimento

Pochi, e quasi tutti opinioni di forum.

**Removal per busta, Alex Ullman, 2014** (verificato,
https://www.coolstuffinc.com/a/alexullman-030414-removing-removal-in-cube). Buste da 15 carte:

| Formato | Removal per busta | Circa % delle carte |
|---|---|---|
| Avacyn Restored | 1 | 7 |
| Innistrad ×3 | 1,7 | 11 |
| Rise of the Eldrazi ×3 | 1,95 | 13 |
| Il cubo di Ullman | 2,78 ("giocava come se avesse troppa removal") | 18,5 |
| Pauper Cube di Adam | oltre 3 | oltre 20 |

La percentuale è nostra: removal per busta diviso 15. La sua proposta era tagliare removal e
aggiungere creature economiche ed evasive per l'aggro.

Per confronto, in [`limited-history.md`](limited-history.md) (pool del 2026-11-02): rimozione
dura 22,2% delle non-terra nel pool, 3,11 per busta da 15; nei play booster 2024–2026 15,8% e
2,01 per busta. Le definizioni non coincidono: Ullman conta tutta la removal sulle carte della
busta, noi la rimozione dura (tag Removal, RemovePermanent o Wipe) sulle non-terra.

**Forum Riptide Lab** (riportato, https://riptidelab.com/forum/goto/post?id=114760): removal tra il
13% e il 25% delle carte nei cubi da 384, "24–25%" detto in linea con la media del forum; rapporto
creature/removal tra circa 1,6 e 2,4 a 1.

**Terre che fissano il mana** (riportato): Ryan Overturf (SCG) 50–60 minimo in un cubo da 360;
Lucky Paper dal 17% al 50% secondo quanti colori giocano i mazzi
(https://luckypaper.co/articles/how-many-lands-should-you-include-in-your-cube/).

**Multiplayer** (riportato, nessun numero): troppi wipe allungano le partite perché al tavolo
affollato costano poco a chi li lancia; nei cubi 2HG i wipe sono pochi, a costo 5 o più, o chiedono
uno sforzo per rompere la parità (`dzr`, `mrbones`, `micro-edh`, `42a6g`). Opinione opposta: con i
permanenti sparsi sul tavolo i wipe valgono di più e la removal mirata di meno (`hypercube&beyond`).
Indicazioni numeriche per il 2v2: nessuna trovata.

Non trovati: numeri pubblicati per costo medio, quota di 1–2 drop, wipe, planeswalker o
counterspell (Lucky Paper, Wizards, wtwlf123, Usman Jamil, Andy Mangold, Anthony Mattox).

## Unità di confronto

- **As-fan** = (carte della categoria / carte del cubo) × carte per busta (verificato,
  https://riptidelab.com/forum/threads/asfan-calculator-tool.868). Noi apriamo buste da 10, i cubi
  standard da 15: confrontare la percentuale, o l'as-fan alla stessa dimensione di busta.
- La percentuale non dipende dalla dimensione del cubo, quindi un cubo da 540 e il pool si
  confrontano direttamente.

## Somiglianza tra cubi

La Cube Map di Lucky Paper (https://luckypaper.co/resources/cube-map/, dati del 2026-02-24)
raggruppa i cubi di CubeCobra per carte in comune (distanza di Jaccard). I dati non si scaricano
(riportato), ma la stessa misura si calcola sulle liste scaricate: carte in comune diviso carte
dell'unione.

## Cosa serve per il confronto

Il 2026-11-01 l'utente ha scelto un'analisi statistica al posto di una vista nell'app; il
2026-11-02 ha scelto gli 8 cubi della tabella.

1. `analyze`: trovare le carte col solo nome del fronte e stampare quelle non trovate. Fatto il
   2026-11-01, vedi sopra.
2. Pool e cubi affiancati, percentuali sulle non-terra e numero per busta: fatto il 2026-11-02 con
   gli script di `tools/cube-metrics`, in [`limited-history.md`](limited-history.md). Le liste si
   scaricano con `fetch_cubes.py`, che scarta un file senza `# mainboard` in testa.
3. Carte in comune tra il pool e ciascun cubo (Jaccard): non fatto.
4. Per ogni cubo, la quota di carte col tag rivisto a mano: nella tabella di copertura di
   [`limited-history-tables.md`](limited-history-tables.md).

I numeri del pool sono provvisori finché la revisione delle carte è in corso: il confronto va
rifatto alla fine.
