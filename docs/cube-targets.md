# Scatolone: obiettivo e misure

Documento del punto 2 del piano di miglioramento del cubo. Riscritto il 2026-11-05: l'obiettivo è
una direzione, non un elenco di percentuali da raggiungere. I numeri si fissano a revisione finita.

## Lo scatolone

Lo scatolone è il pool dello store di ScatoloneQuintet: le carte con rating 3-5 e senza status
(`metadata/pool.json`). È lo stesso pool che misurano la pagina /stats, i file di `report` e
[`limited-history.md`](limited-history.md). La lista `E:\Working\_ToBe_Scatolone.txt` non è lo
scatolone: è servita solo per la prima misura, quella del 2026-10-18 (vedi in fondo).

Formato: 2 contro 2, ogni giocatore gioca il proprio turno in senso orario. Draft da 4 buste di 10
carte a testa, quindi 160 carte per draft e 40 scelte per giocatore. I due compagni possono
scambiarsi 3 carte a testa.

## La direzione

La diagnosi è dell'utente: lo scatolone "sembra essere troppo lento e con troppe soluzioni". La
direzione che ne viene:

- meno rimozione dura, e soprattutto meno Wipe;
- più creature economiche (costo 2 o meno);
- una curva più bassa.

Nessuna percentuale è fissata. Il 2026-10-20 l'utente ha detto che dell'obiettivo c'è "solo una
vaga idea", e il 2026-11-05 ha scelto di tenere la sola direzione. I numeri si decidono a revisione
finita, sul pool rimisurato.

## Dove siamo

Il pool del 2026-11-02 (2864 carte, store `bc7fd72`) accanto al Limited retail di oggi e ai due cubi
pensati per il 2 contro 2, dalla tabella di [`limited-history.md`](limited-history.md#i-numeri),
dove ci sono anche gli altri sei cubi e le ere precedenti. Percentuali sulle carte non-terra.

| metrica | pool | play booster 2024–2026 | 2HG Cube | 42a6g |
|---|---:|---:|---:|---:|
| rimozione dura | 22.2 | 15.8 | 19.1 | 16.8 |
| Wipe | 4.5 | 0.9 | 1.9 | 1.3 |
| interazione | 40.6 | 25.5 | 33.8 | 27.1 |
| vantaggio carte | 14.3 | 8.3 | 11.3 | 11.8 |
| creature | 51.9 | 59.2 | 64.1 | 62.2 |
| creature costo ≤2 | 15.8 | 20.7 | 18.7 | 21.9 |
| costo medio | 3.23 | 3.04 | 3.24 | 3.28 |
| costo 6+ | 8.9 | 6.2 | 11.1 | 9.5 |

- Le risposte sono lontane da ogni riferimento: rimozione dura, Wipe, interazione e vantaggio carte
  sono sopra ogni formato retail e sopra entrambi i cubi 2 contro 2.
- La curva è quella dei cubi 2 contro 2 (costo medio 3.23 contro 3.24 e 3.28), sopra il retail.
- Mancano creature: 51.9% contro il 62-64% dei cubi 2 contro 2, e quelle di costo ≤2 sono sotto
  sia il retail sia i due cubi.

## Definizioni

Le percentuali sono sulle carte non-terra: una carta è terra se la riga del tipo della faccia
frontale contiene "Land" e non "Creature".

| metrica | come si conta |
|---|---|
| rimozione dura | tag Removal, RemovePermanent o Wipe |
| Wipe | tag Wipe |
| risposte morbide | tag Pacify, Bounce o Steal |
| interazione totale | rimozione dura, risposte morbide, Counter, Disenchant, LandDestruction, Discard |
| vantaggio carte | tag CardAdvantage |
| creature costo ≤2 | creatura con costo di mana totale 2 o meno |
| 2-drop aggressivo | creatura di costo ≤2 con forza 2 o più (una forza `*` conta 0) |
| creature evasive | Flying, Menace, Trample, Shadow, Horsemanship, Fear, Intimidate, Skulk, o "can't be blocked" nel testo |
| guadagno vita | Lifelink, o "gain(s) N life" / "you gain" nel testo |
| fog | "prevent all combat damage" nel testo |
| per draft | 160 × quota di non-terre × percentuale |

Il tag è quello rivisto a mano quando c'è, altrimenti la proposta del classificatore. Guadagno vita
e fog si leggono dal testo con una regex, non dai tag.

## Quanto fidarsi

- **Il retail è un riferimento, non un modello.** È pensato per l'1 contro 1 e ha meno potenza di
  un cubo, che di solito vuole più rimozione.
- **In 2 contro 2 un Wipe colpisce due avversari**, quindi pesa più che in un 1 contro 1. È una
  ragione in più per tagliarli, e non è misurata nei numeri.
- **Molti tag sono proposte del classificatore.** Nel pool del 2026-11-02 il 66.2% del peso è
  rivisto a mano; nei formati retail la mediana è il 2.8%.
- **I numeri cambiano con la revisione.** Ogni carta rivista o valutata sposta le percentuali.

## Per riprendere

1. Finire la revisione delle carte: rating e tag.
2. Rimisurare pool, retail e cubi con gli script di
   [`tools/cube-metrics`](../tools/cube-metrics/README.md) (sezione "Limited over time").
3. Fissare con l'utente i numeri, metrica per metrica, partendo dalla direzione qui sopra.
4. Poi i tagli e le aggiunte: nel pool sono cambi di rating o di status.

## Storico: la misura del 2026-10-18 sulla lista txt

La prima misura prese come scatolone la sezione `-- Scatolone` di `E:\Working\_ToBe_Scatolone.txt`
(4164 carte, 1258 rivedute) e come retail nove set recenti (FDN, DSK, BLB, OTJ, MKM, LCI, WOE, DFT,
TDM) dalla ricerca Scryfall `set:X is:booster -type:basic`, pesati per rarità: 7 comuni, 3 non
comuni, 7/8 di rara e 1/8 di mitica per busta. L'obiettivo era la media fra le due, scelta
dall'utente fra tre livelli (a metà strada, livello retail, solo i Wipe):

| metrica | lista txt | retail | metà strada |
|---|---:|---:|---:|
| rimozione dura | 24.7% | 14.8% | 19.8% |
| di cui Wipe | 5.1% | 0.9% | 3.0% |
| creature costo ≤2 | 9.9% | 21.6% | 15.7% |
| costo medio non-terre | 3.7 | 3.0 | 3.3 |

Lo stesso giorno l'utente ha chiarito che lo scatolone finale è il pool, e che ogni carta viene
rivista da capo. Quei numeri non si confrontano con il pool: la base è un'altra lista, e il retail
è pesato con un modello per rarità su nove set invece che con i fogli di stampa di MTGJSON.

Di quella misura restano:

- la proposta di scambio `E:\Working\_ToBe_Scatolone_proposta.md` (A: 133 carte da tagliare, B: 91
  Wipe da valutare, C: 195 creature da aggiungere), costruita sulla lista txt: non è un passo da
  fare. Nemmeno un filtro "nel box" nel tagger serve, perché lo scatolone è il pool;
- gli script della sezione storica di [`tools/cube-metrics`](../tools/cube-metrics/README.md);
- la tabella completa, 30 metriche per carta e per draft, in questo file al commit `e6f5f38`.
