# Scatolone: percentuali obiettivo

Documento del punto 2 del piano di miglioramento del cubo. Misurato il 2026-10-18. Le percentuali
qui sotto sono l'obiettivo scelto; i tagli e le aggiunte si applicano dopo che le carte saranno
taggate e selezionate, rimisurando prima.

## Il problema

Formato: 2 contro 2, ogni giocatore gioca il proprio turno in senso orario. Draft da 4 buste di 10
carte a testa, quindi 160 carte per draft e 40 scelte per giocatore. I due compagni possono
scambiarsi 3 carte a testa.

La diagnosi di partenza è dell'utente: lo scatolone "sembra essere troppo lento e con troppe
soluzioni". I numeri la confermano: rispetto a un set da draft retail il box ha due terzi di
rimozione in più, più di cinque volte i Wipe, metà delle creature economiche e una curva più alta.

## Da dove vengono i numeri

1. **Il box.** La sezione `-- Scatolone` di `E:\Working\_ToBe_Scatolone.txt`: 4164 carte, tutte
   trovate su Scryfall. Le sezioni Jolly, Bannate, Dungeon, Melds, Tokens ed Extra sono escluse.
   Ogni carta pesa 1, perché una busta è un'estrazione a caso dal box.
2. **I dati delle carte.** Il bulk "Oracle Cards" di Scryfall
   (`oracle-cards-20261018090155.jsonl.gz`, 38.861 oggetti): costo di mana, riga del tipo,
   forza e costituzione, parole chiave, testo. Per le carte a due facce vale la faccia frontale.
3. **Gli effetti.** I tag del metadata store di ScatoloneQuintet (commit `483357a` più il working
   tree del 2026-10-18): il tag rivisto a mano quando c'è, altrimenti la proposta del
   classificatore. Delle 4164 carte del box, 1258 sono rivedute e 46 non sono nello store (contano
   come senza tag).
4. **Il riferimento retail.** Nove set da draft recenti: Foundations (FDN), Duskmourn (DSK),
   Bloomburrow (BLB), Outlaws of Thunder Junction (OTJ), Murders at Karlov Manor (MKM), The Lost
   Caverns of Ixalan (LCI), Wilds of Eldraine (WOE), Aetherdrift (DFT), Tarkir: Dragonstorm (TDM).
   Le carte vengono dalla ricerca Scryfall `set:X is:booster -type:basic`, 2434 in tutto, di cui
   2399 nello store. Sono pesate come escono dalle buste: per ogni busta 7 comuni, 3 non comuni,
   7/8 di rara e 1/8 di mitica, diviso per il numero di carte di quella rarità nel set.
5. **L'obiettivo.** Per ogni metrica, la media fra il box di oggi e il retail. L'utente l'ha
   scelto il 2026-10-18 fra tre livelli: a metà strada, livello retail, oppure solo i Wipe.

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

## Le percentuali obiettivo

Le quattro che definiscono "lento e con troppe soluzioni":

| metrica | box oggi | retail | obiettivo |
|---|---:|---:|---:|
| rimozione dura | 24.7% | 14.8% | **19.8%** |
| di cui Wipe | 5.1% | 0.9% | **3.0%** |
| creature costo ≤2 | 9.9% | 21.6% | **15.7%** |
| costo medio non-terre | 3.7 | 3.0 | **3.3** |

Tutte le metriche, con il box dopo lo scambio proposto (vedi sotto):

| metrica | box oggi | retail | obiettivo | dopo lo scambio |
|---|---:|---:|---:|---:|
| creature | 51.4% | 59.6% | 55.5% | 54.9% |
| planeswalker | 3.8% | 0.1% | 1.9% | 3.4% |
| istantanei e stregonerie | 33.7% | 28.8% | 31.3% | 31.2% |
| costo medio non-terre | 3.7 | 3.0 | 3.3 | 3.5 |
| costo medio creature | 4.0 | 3.2 | 3.6 | 3.7 |
| costo 0-1 | 6.7% | 13.3% | 10.0% | 8.2% |
| costo 2 | 19.5% | 30.3% | 24.9% | 22.4% |
| costo 3 | 25.4% | 24.3% | 24.9% | 24.6% |
| costo 4 | 20.0% | 15.6% | 17.8% | 19.2% |
| costo 5 | 14.9% | 9.8% | 12.3% | 13.7% |
| costo 6+ | 13.5% | 6.6% | 10.0% | 11.9% |
| creature costo ≤2 | 9.9% | 21.6% | 15.7% | 14.8% |
| 2-drop aggressivi | 6.0% | 11.9% | 9.0% | 8.8% |
| costo ≤3 con forza ≥3 | 7.8% | 9.3% | 8.5% | 8.3% |
| creature con haste | 4.3% | 2.0% | 3.2% | 4.5% |
| creature evasive | 19.5% | 16.2% | 17.9% | 20.2% |
| defender | 0.5% | 1.0% | 0.7% | 0.5% |
| rimozione dura | 24.7% | 14.8% | 19.8% | 20.1% |
| di cui Wipe | 5.1% | 0.9% | 3.0% | 3.1% |
| risposte morbide | 10.8% | 5.9% | 8.4% | 10.7% |
| counterspell | 3.2% | 1.6% | 2.4% | 3.3% |
| disenchant | 5.2% | 3.3% | 4.2% | 4.8% |
| scarto | 3.4% | 2.1% | 2.7% | 3.4% |
| interazione totale | 43.7% | 25.5% | 34.6% | 39.4% |
| vantaggio carte | 12.5% | 7.6% | 10.0% | 12.5% |
| guadagno vita | 9.5% | 8.9% | 9.2% | 9.6% |
| fog | 0.3% | 0.1% | 0.2% | 0.3% |
| senza tag | 18.4% | 30.3% | 24.4% | 20.0% |
| creature per rimozione dura | 2.1 | 4.0 | 3.0 | 2.7 |
| creature per interazione | 1.2 | 2.3 | 1.8 | 1.4 |

Le stesse cose contate in un draft da 160 carte:

| per draft | box oggi | retail | obiettivo |
|---|---:|---:|---:|
| rimozione dura | 36.8 | 22.0 | 29.4 |
| di cui Wipe | 7.6 | 1.3 | 4.5 |
| interazione totale | 65.1 | 37.8 | 51.5 |
| creature | 76.6 | 88.5 | 82.5 |
| creature costo ≤2 | 14.7 | 32.1 | 23.4 |
| 2-drop aggressivi | 9.0 | 17.7 | 13.3 |
| costo 6+ | 20.1 | 9.8 | 14.9 |
| vantaggio carte | 18.6 | 11.4 | 15.0 |
| planeswalker | 5.6 | 0.2 | 2.9 |

A box di dimensione costante (3878 carte non-terra), l'obiettivo vale circa: Wipe da 198 a 116
(−82), rimozione dura da 958 a 766 (−192), creature di costo ≤2 da 383 a 611 (+228).

## Quanto fidarsi di questi numeri

- **Il retail è un riferimento, non un modello.** È pensato per l'1 contro 1 e ha meno potenza di
  un cubo, che di solito vuole più rimozione. Per questo l'obiettivo è a metà strada e non al
  livello retail.
- **In 2 contro 2 un Wipe colpisce due avversari**, quindi pesa più che in un 1 contro 1. È una
  ragione in più per tagliarli, e non è misurata nei numeri.
- **Molti tag sono proposte del classificatore.** Nel box le carte senza rating danno gli stessi
  rapporti di quelle valutate (interazione 45.2% contro 43.7% sul rating 3-5), quindi il quadro
  regge. Nel retail le carte senza tag sono il 30%: i commons "vanilla" sono tanti, ma qualche
  risposta può sfuggire al classificatore.
- **Guadagno vita e fog** sono letti dal testo con una regex, non dai tag.
- **Il box non è il pool dello store.** Delle 4164 carte, 1689 hanno rating 3-5, 583 rating 1-2
  e 1892 nessun rating; 945 carte che lo store valuta 3-5 non sono nella lista. La pagina
  /stats e i file di `report` misurano il pool, non lo scatolone vero.
- **I numeri cambiano con il tagging.** Ogni carta rivista o valutata sposta le percentuali:
  vanno rimisurate prima di applicare i tagli.

## La proposta già pronta

`E:\Working\_ToBe_Scatolone_proposta.md`, con una casella per carta:

- **A, da tagliare (133):** carte del box con rating 1-2 che hanno rimozione dura o Wipe. Il
  rating dell'utente le mette già in panchina.
- **B, da valutare (91):** Wipe senza rating. Per l'obiettivo ne vanno via circa 57; i primi
  candidati sono i 52 di costo 5 o più.
- **C, da aggiungere (195):** creature di costo ≤2 valutate 3-5 nello store ma assenti dal box,
  112 con forza 2 o più.

Con A tagliato, i 52 Wipe di B da costo 5+ tagliati e C aggiunto, il box passa da 4164 a 4174
carte e arriva alla colonna "dopo lo scambio": rimozione e Wipe all'obiettivo, creature economiche
quasi. Restano sopra l'obiettivo la parte alta della curva (costo 6+ al 11.9%), il vantaggio carte
(12.5%) e i planeswalker (3.4%): sono la leva successiva.

## Per riprendere

1. Finire di taggare e valutare le carte del box, soprattutto le 1892 senza rating.
2. Rimisurare con gli script di [`tools/cube-metrics`](../tools/cube-metrics/README.md) e
   rigenerare la proposta.
3. Rivedere A, B e C. Un filtro "nel box" nel tagger, che legga la lista, permetterebbe di farlo
   con le immagini e di avere /stats sullo scatolone vero.
4. Poi la stessa analisi sulla parte alta della curva, sul vantaggio carte e sui planeswalker.
