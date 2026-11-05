# Scatolone, cubi di riferimento e Limited nel tempo

Analisi del 2026-11-02. Il pool è messo accanto a 8 cubi di CubeCobra e a 83 formati Limited
retail, dal 1997 al 2026, pesati per rarità come escono dalle buste. È l'analisi statistica
lasciata in sospeso dal punto 3 del piano (`docs/reference-cubes.md`).

- Grafici, uno per metrica nel tempo, con pool e cubi a destra: [`limited-history.html`](limited-history.html).
- Tabelle complete, formato per formato: [`limited-history-tables.md`](limited-history-tables.md).
- Script e comandi per rifarla: [`tools/cube-metrics`](../tools/cube-metrics/README.md).

## Da dove vengono i numeri

1. **Le buste retail.** Il campo `booster` dei file per set di MTGJSON
   (`https://mtgjson.com/api/v5/<SET>.json.gz`): per ogni set, le varianti di busta con il loro peso
   e i fogli di stampa con il peso di ogni carta. Si usa il prodotto `play` se c'è, altrimenti
   `draft`, altrimenti `default`. Per ogni carta si calcolano le copie attese in una busta. Entrano
   tutti i fogli: comuni, non comuni, rare e mitiche, foil, wildcard, terre, e gli slot speciali
   (timeshifted di Time Spiral, doppia faccia di Innistrad, Mystical Archive di Strixhaven, The List
   dei play booster). Le carte che stanno in un altro set (STA, TSB, The List…) si trovano
   scaricando anche quei set.
   Controlli: la somma delle copie attese fa 15,00 per ogni busta draft e 14,00 per ogni play
   booster; nessuna carta resta senza oracle id. Quattro ricalcoli fatti con codice indipendente
   danno gli stessi numeri: Tempest col modello 11 comuni, 3 non comuni e 1 rara costruito
   dall'elenco delle carte, Bloomburrow dai fogli, il Vintage Cube e il pool.
2. **I formati.** Un formato è il draft come si giocava: una busta per set nella configurazione
   finale del blocco, oppure tre buste dello stesso set. Le copie attese sono la media delle buste.
   - 15 blocchi a 3 set, 1 grande e 2 piccole: da Mirage (MIR-VIS-WTH) a Theros (THS-BNG-JOU).
   - 10 blocchi a 2 set, nella configurazione finale: Lorwyn (LRW-LRW-MOR), Shadowmoor, Zendikar,
     Innistrad (ISD-ISD-DKA), Dragons of Tarkir (DTK-DTK-FRF), e i cinque blocchi a due del
     2015–2018.
   - 41 set singoli: Coldsnap, Rise of the Eldrazi, Avacyn Restored, Khans of Tarkir, e ogni set
     di tipo expansion da Dominaria (2018) a Reality Fracture (ottobre 2026) che ha buste da draft
     o play. Restano fuori MAT (nessuna busta da draft) e BIG e OM1 (nessuna busta in MTGJSON).
   - 17 core set, da Fifth Edition a Foundations.
   La data di un formato è l'uscita del suo ultimo set. L'elenco è
   [`formats.json`](../tools/cube-metrics/formats.json).
3. **I cubi.** Le liste di CubeCobra scaricate il 2026-11-02 (`/cube/download/plaintext/<id>`).
   Ogni riga è una carta, quindi le copie ripetute contano: la 2HG Cube ha 574 righe, la Commander
   Cube 960. Nomi non trovati: 6 nella Giant Peasant su 540 righe (`Endwalker`,
   `Vivi's Thunder Magic`…), 6 nella Giant Cube su 2880 (nomi Godzilla e Walking Dead come
   `Mothra, Supersonic Queen`, `Lucille`). Nessuno di questi nomi è nel bulk Oracle di Scryfall.
4. **Il pool.** Le carte di `pool.json` senza status: 2864 (store al commit `bc7fd72` del
   2026-11-02). Ogni carta pesa 1, come in un cubo.
5. **I dati delle carte.** Il bulk Oracle Cards di Scryfall del 2026-11-02, per oracle id.
6. **Gli effetti.** I tag dello store: quello rivisto a mano se c'è, altrimenti la proposta del
   classificatore. Le terre base non sono nello store, ma non contano nelle percentuali.

## Definizioni

Le percentuali sono sulle carte non-terra, con le definizioni di
[`cube-targets.md`](cube-targets.md#definizioni) (rimozione dura = Removal, RemovePermanent o Wipe;
interazione = rimozione dura, risposte morbide, Counter, Disenchant, LandDestruction, Discard).
In più:

| metrica | come si conta |
|---|---|
| per busta, retail | percentuale × carte non-terra attese in una busta vera (15 carte, 14 dal 2024) |
| per busta, cubi e pool | percentuale × quota di non-terre del cubo × 15 (e × 10 per le buste del pool) |
| MV medio rimozione | costo medio delle carte con rimozione dura |
| P+T a MV3 | forza più costituzione medie delle creature di costo 3, solo valori numerici |
| media di un'era | media semplice dei formati usciti in quell'era, core set compresi |

## I numeri

Le ere seguono i cambi di struttura: mitiche da Shards of Alara (ottobre 2008), blocchi a due da
Battle for Zendikar (ottobre 2015), set singoli da Dominaria (aprile 2018), play booster da Murders
at Karlov Manor (febbraio 2024). Per busta: retail sulla busta vera, cubi e pool su una busta da 15.

| era o cubo | formati o carte | rimozione dura % | Wipe % | interazione % | vantaggio carte % | creature % | creature MV≤2 % | MV medio | MV 6+ % | P+T MV3 | rim. dura per busta | Wipe per busta | creature MV≤2 per busta | vantaggio carte per busta |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1996–2008: 11C/3U/1R, blocchi a 3 | 20 | 12.0 | 1.7 | 26.3 | 2.4 | 55.7 | 19.7 | 2.87 | 5.0 | 3.82 | 1.73 | 0.24 | 2.84 | 0.34 |
| 2008–2015: mitiche, blocchi a 3 | 17 | 12.1 | 1.1 | 25.0 | 3.0 | 53.2 | 17.7 | 3.05 | 7.2 | 4.25 | 1.65 | 0.15 | 2.42 | 0.41 |
| 2015–2018: blocchi a 2 | 5 | 13.3 | 1.0 | 25.0 | 3.3 | 57.0 | 18.9 | 3.04 | 5.8 | 4.59 | 1.78 | 0.14 | 2.54 | 0.44 |
| 2018–2023: set singoli, draft booster | 24 | 13.8 | 1.0 | 24.6 | 6.1 | 56.4 | 20.2 | 2.97 | 5.7 | 4.61 | 1.88 | 0.13 | 2.75 | 0.83 |
| 2024–2026: play booster | 17 | 15.8 | 0.9 | 25.5 | 8.3 | 59.2 | 20.7 | 3.04 | 6.2 | 4.84 | 2.01 | 0.11 | 2.62 | 1.05 |
| tutti i formati | 83 | 13.4 | 1.2 | 25.3 | 4.8 | 56.2 | 19.6 | 2.98 | 5.9 | 4.39 | 1.82 | 0.16 | 2.67 | 0.65 |
| MTGO Vintage Cube | 540 | 19.9 | 2.7 | 34.5 | 18.1 | 45.4 | 21.2 | 2.67 | 7.4 | 5.15 | 2.44 | 0.33 | 2.61 | 2.22 |
| LSV Cube | 515 | 18.1 | 2.1 | 34.0 | 17.8 | 48.0 | 20.7 | 2.70 | 7.6 | 5.22 | 2.21 | 0.26 | 2.53 | 2.18 |
| Pauper Cube | 450 | 14.6 | 1.0 | 27.8 | 7.1 | 57.8 | 27.3 | 2.65 | 7.6 | 4.45 | 1.93 | 0.13 | 3.60 | 0.93 |
| Giant Peasant | 534 | 19.7 | 1.5 | 33.5 | 8.4 | 58.4 | 29.0 | 2.53 | 3.9 | 4.46 | 2.58 | 0.20 | 3.79 | 1.10 |
| 2HG Cube (2v2) | 574 | 19.1 | 1.9 | 33.8 | 11.3 | 64.1 | 18.7 | 3.24 | 11.1 | 4.30 | 2.61 | 0.26 | 2.56 | 1.54 |
| 42a6g (2v2) | 531 | 16.8 | 1.3 | 27.1 | 11.8 | 62.2 | 21.9 | 3.28 | 9.5 | 4.20 | 2.20 | 0.17 | 2.88 | 1.55 |
| Commander Cube | 960 | 14.0 | 3.4 | 25.5 | 19.3 | 52.2 | 11.0 | 3.50 | 12.4 | 4.73 | 1.83 | 0.44 | 1.44 | 2.52 |
| The Giant Cube | 2874 | 21.8 | 4.7 | 35.8 | 14.1 | 49.2 | 13.8 | 3.46 | 12.6 | 4.85 | 2.86 | 0.62 | 1.81 | 1.86 |
| **Scatolone (pool)** | **2864** | **22.2** | **4.5** | **40.6** | **14.3** | **51.9** | **15.8** | **3.23** | **8.9** | **5.09** | **3.11** | **0.63** | **2.21** | **2.00** |

Le carte di un cubo sono quelle trovate. Una busta da 10 del pool contiene in media 2.07 carte di
rimozione dura, 0.42 Wipe, 1.47 creature di costo ≤2 e 1.33 carte di vantaggio carte. In un draft
da 160 carte sono 33.1 rimozioni dure, 6.8 Wipe e 23.6 creature di costo ≤2.

## Cosa dicono

**Il Limited retail in trent'anni.**

- La rimozione dura è cresciuta poco, da 12.0% a 15.8% (da 1.73 a 2.01 per busta).
- I Wipe sono dimezzati, da 1.7% a 0.9%.
- L'interazione totale è rimasta intorno al 25% in ogni era.
- Sono cambiate tre cose:
  - il vantaggio carte: da 2.4% a 8.3%;
  - la taglia delle creature: a costo 3, forza più costituzione da 3.82 a 4.84;
  - poco le creature: da 55.7% a 59.2%.
- Curva ferma: costo medio sempre tra 2.87 e 3.05, creature di costo ≤2 tra 17.7% e 20.7%.
- Gli estremi:
  - rimozione dura: dall'8.2% di Ninth Edition al 18.1% di Outlaws of Thunder Junction;
  - interazione: dal 18.9% di The Brothers' War al 32.0% di Invasion;
  - Wipe: massimo 3.3% in Seventh Edition.

**Il pool contro il retail.** Nessuno degli 83 formati raggiunge il pool:

| metrica | pool | formato retail più alto |
|---|---:|---|
| rimozione dura | 22.2% | 18.1%, Outlaws of Thunder Junction |
| Wipe | 4.5% | 3.3%, Seventh Edition |
| interazione | 40.6% | 32.0%, Invasion |
| vantaggio carte | 14.3% | 10.4%, Secrets of Strixhaven |

Il pool ha cinque volte i Wipe di un play booster di oggi.

**Il pool contro i cubi.**

- Il pool ha l'interazione più alta di tutti i riferimenti: 40.6%, contro il 35.8% della Giant
  Cube e il 34.5% del Vintage Cube.
- Il cubo più simile è The Giant Cube, che ha anche le stesse dimensioni: rimozione 21.8%, Wipe
  4.7%, costo medio 3.46, creature di costo ≤2 13.8%.
- I due cubi pensati per il 2 contro 2 hanno la curva del pool: costo medio 3.24 e 3.28 contro
  3.23, costo 6+ 11.1% e 9.5% contro 8.9%. Hanno però:
  - molte più creature: 64.1% e 62.2%, contro 51.9%;
  - meno Wipe: 1.9% e 1.3%, contro 4.5%;
  - meno interazione: 33.8% e 27.1%, contro 40.6%.

**Rispetto alla diagnosi "troppo lento e con troppe soluzioni".** La direzione di
[`cube-targets.md`](cube-targets.md#la-direzione) è meno rimozione dura e Wipe, più creature
economiche e una curva più bassa, senza percentuali fissate. Il pool di oggi:

- è lontano nelle risposte: rimozione dura, Wipe, interazione e vantaggio carte sono sopra ogni
  formato retail, e sopra quasi tutti i cubi;
- ha la curva dei cubi 2v2 (costo medio 3.23 contro 3.24 e 3.28), sopra il retail (3.04 nei play
  booster);
- ha meno creature di costo ≤2 del retail e dei cubi 2v2: 15.8% contro 20.7%, 18.7% e 21.9%.

## Quanto fidarsi

- **Molti tag sono proposte del classificatore.** Nel pool il 66.2% del peso è rivisto a mano.
  Nei cubi la quota va dal 17.7% (42a6g) al 58.4% (Giant Peasant). Nei formati retail la mediana è
  2.8%: 23 formati su 83 superano il 50%, 45 stanno sotto il 5%.
- **Le proposte vengono dal classificatore di `41179a6`**, riapplicato allo store il 2026-11-02
  (`bc7fd72`). Rispetto alle proposte del 2026-10-30 sono cambiate due carte (Scrapwork Mutt
  senza Filter, Akuta senza Reanimate), e nessun numero di questa analisi si è spostato.
- **Le carte senza tag** vanno dal 17.9% al 54.4% nei formati retail, dove le comuni vanilla sono
  tante, e sono il 14.5% nel pool.
- **I fogli dei set vecchi sono uniformi in MTGJSON:** ogni comune pesa uguale. Sui fogli di
  stampa veri alcune comuni potevano comparire più volte; l'effetto non è misurato.
- **I cubi sono pensati quasi tutti per l'1 contro 1 a buste da 15.** Solo la 2HG Cube e la 42a6g
  sono per il 2 contro 2.
- **I numeri del pool cambiano con la revisione in corso.** L'analisi va rifatta alla fine con
  gli script di `tools/cube-metrics`.
