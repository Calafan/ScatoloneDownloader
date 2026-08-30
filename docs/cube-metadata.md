# Cube metadata: workflow, recovery, and schema

This is the reference for the `metadata/` directory — the git-tracked source
of truth for every card evaluation (rating, ban/token/jolly status, and
functional effect tags) for the **entire** card library, ~30k+ printings and
growing — and for the `Views/` folder tree generated from it. It documents
the final state produced by
`docs/plans/2026-08-30-001-feat-card-effect-ontology-plan.md` (including the
"Part B" follow-up); see that plan for the design rationale (decisions
`D1`-`D8`, `P1`-`P9`, `B1`-`B4`).

## Guiding principle: recovery from git + Scryfall

The `metadata/` directory plus the code in this repository, plus a fresh
Scryfall bulk-data download, are **sufficient to reconstruct everything**:
every card's rating, status, and effect tags, and a full re-download of the
image folder. Local `.png` files and any XMP metadata inside them are
**derived artifacts** — re-downloadable, re-injectable, never the only place
an evaluation lives. If you lose the images folder (or the whole machine),
`restore` rebuilds it from `metadata/` alone; nothing evaluative is lost.

Concretely: **never hand-edit an image's XMP as the only record of a rating,
status, or effect.** Always let the workflow below land it in `metadata/`,
because that is the only copy that survives a re-clone.

## Metadata directory layout (rating-tier partition)

`metadata/` is deliberately a **directory of three files**, not one
monolithic JSON blob, because it doubles as the disaster-recovery manifest
for the whole library — every printing Scryfall knows about is kept on
purpose, even ones nobody has looked at yet, so a single all-cards file would
be tens of thousands of entries and unwieldy to hand-edit or diff. Each card
lives in exactly one file, chosen by its **current rating**:

| File | Rating | Contents |
|---|---|---|
| `metadata/pool.json` | 3-5 | The active cube — small, the main hand-edit target, tiny diffs. |
| `metadata/fringe.json` | 1-2 | Evaluated but cut. Grows over time as more of the backlog gets rated and rejected. |
| `metadata/unrated.json` | 0 | The bulk library manifest (tens of thousands of entries). Changes only when Scryfall adds new printings, not from day-to-day curation. |

**Loading** (`CubeMetadataStore.Load`) reads whichever of the three files are
present and merges them into one in-memory set, keyed by `oracle_id`. It is
tolerant end to end: a missing `metadata/` directory, a missing individual
tier file, or a blank/corrupt tier file all just contribute nothing rather
than crashing the tagger/views. (If the same `oracle_id` somehow appears in
more than one file — should never happen from normal use — the first one
found in `pool.json` → `fringe.json` → `unrated.json` order wins.)

**Saving** (`CubeMetadataStore.Save`) **always rewrites all three files**
from the full in-memory set: every entry is routed to its tier file by
`TierFileName(rating)`. This is what makes a rating change "just work" — bump
a card from 0 to 4 in the tagger, hit save, and the entry disappears from
`unrated.json` and appears in `pool.json` on the very next write; there is no
separate "move" step. Within each tier file, entries stay sorted by
`(name, oracle_id)` exactly as before, so an **untouched tier serializes
byte-identically** to what was already on disk and produces no git diff, no
matter how many other cards changed tier that run.

Known cost: because rating a single card in `unrated.json` still triggers a
full rewrite of that ~26k-entry file (~100ms), every tagger save while
working through the unrated backlog pays that cost. Acceptable for now;
optimizable later with dirty-only writes if it becomes a bottleneck.

All four commands below take `-m|--metadata <DIR>` for this directory,
defaulting to `./metadata`.

## Command lifecycle

```
   (legacy Bridge users only, once)
        import
           |
           v
   metadata/{pool,fringe,unrated}.json  <-------+
           |                                    |
           v                                    |
          tag  ------------------------------- -+   (autosaves every keystroke)
           |
           v
      build-views  --> Views/<root>/... (symlinks/hardlinks) + Cubo_Analysis.md

   restore  <-- metadata/ (union of all tiers) + Scryfall bulk   (disaster recovery)
```

1. **`import <SOURCE_DIR> [-m metadata] [--overwrite]`** — one-time seed. Reads
   the rating and color label Adobe Bridge already wrote into each PNG's XMP
   and migrates them into the metadata directory. This is the **only**
   command that still reads XMP. By default it only fills entries that don't
   have a rating/label yet (`--overwrite` forces the XMP value in). `Status`,
   `Effects`, and `ReviewedAt` are never touched by `import` — those belong
   to the tagger. Run it once when adopting this workflow on an existing
   Bridge-rated cube; rarely needed again afterwards.
2. **`tag <SOURCE_DIR> [-m metadata] [-p port]`** — the authoring tool going
   forward. Launches a local web UI (keyboard-driven, one card at a time) to
   set rating (0-5), status (None/Banned/Token/Jolly), and effect tags. Every
   change autosaves immediately (which repartitions and rewrites all three
   tier files, see above) — there is no separate "save" step, so a closed tab
   never loses more than the field just changed. Adobe Bridge is optional
   from this point on; nothing reads XMP again unless you re-run `import`.
3. **`build-views <SOURCE_DIR> [-v views] [-m metadata]`** — generates the
   `Views/` folder tree (see below) by loading rating/status/effects from the
   metadata directory (the merged view of all three tier files) and linking
   (not copying) each master `.png` into every folder it belongs in. Also
   writes `Cubo_Analysis.md` next to the views. Safe to re-run any time the
   metadata changes — it deletes and rebuilds the views root each time, so
   it's always in sync.
4. **`restore -m metadata --images <dir>`** — disaster recovery. Loads the
   **union of all three tier files** (the full library manifest, not just the
   pool), downloads the Scryfall bulk card list, resolves each entry to its
   exact printing (preferring `scryfallId`), and re-downloads + recomposes
   any `.png` missing from `<dir>`. Idempotent — already-present files are
   left untouched, so it's also the right command to top up a partial image
   folder. Writes no XMP; rating/status/effects live only in the metadata.

## Metadata entry schema

Each of the three tier files has the same shape:

```json
{
  "version": 1,
  "cards": {
    "<oracle_id>": { "...": "CardMetadataEntry, see below" }
  }
}
```

- **`version`** (`int`) — schema version, currently always `1`.
- **`cards`** (`object`, keyed by Scryfall `oracle_id`) — one entry per card
  currently in that tier. `oracle_id` is stable across reprints/printings
  (unlike the per-printing `id`), so re-matching a card to a *different*
  printing of the same card still resolves to the same entry. Within a file,
  entries are written in `(name, oracle_id)` order (see
  `CubeMetadataStore.Save`), so each file reads like an alphabetical card
  list and diffs stay small and reviewable — do not rely on dictionary/key
  order meaning anything else. Which *file* an entry lives in is determined
  solely by its `rating` (see the tier table above); the key itself carries
  no tier information.

### `CardMetadataEntry` fields

| Field | Type | Who writes it | Meaning / hand-edit rules |
|---|---|---|---|
| `name` | `string` | `import`, `tag` | Card name. **Informational only, not a key** — safe to leave stale after a rename; the app always looks the entry up by `oracle_id`. |
| `scryfallId` | `string` (nullable, omitted when absent) | `import`, `tag` | The Scryfall printing `id` (not `oracle_id`) of the exact art this evaluation was made against. `restore` prefers this to re-download the *same* printing/art; if it's missing or the printing has vanished from the current bulk data, `restore` falls back to the first printing found for the `oracle_id`. Hand-editing this is fine if you know the correct printing UUID; leaving it blank just means `restore` uses the fallback. |
| `rating` | `int`, 0-5 | `import` (seed), `tag` (authoritative) | Cube rating; `0` = unrated. **Never comes from XMP after the initial `import`** — `tag`/`build-views` read this field exclusively. **This is also the field that decides which tier file (`pool.json`/`fringe.json`/`unrated.json`) the entry lives in** — the next `Save` moves it if you hand-edit this to cross a tier boundary. Ratings of `1` or `2` are deliberately invisible in every generated view (see D7 below) — they still round-trip, just aren't browsable in `Views/`. |
| `label` | `string` | `import` (copies the Bridge label verbatim), `tag` (preserves it) | Legacy Adobe Bridge color-label text (e.g. `"Red"`), carried along for reference. **Nothing derives behavior from this field anymore** — `status` superseded it. Safe to ignore or blank out by hand. |
| `status` | `string`, one of `Banned \| Token \| Jolly`, or omitted/null for a normal card | `import` (default-fills from the Bridge label, only if empty), `tag` (authoritative, always wins) | Mutually-exclusive pool status. A tagged card (`Banned`/`Token`/`Jolly`) is pulled out of every other view and appears **only** under its own flat top-level folder — `Views/0_Banned/`, `Views/0_Token/`, or `Views/0_Jolly/` — with the card directly inside, at any rating (the status is checked before the rating rules, so a tagged 1-2 star card still shows). Hand-editable directly as one of the strings (case-insensitive on read); an unrecognized or blank value is treated as no status. |
| `effects` | `string[]`, canonical `CardEffect` member names | `tag` only | Functional effect tags (a card can carry several). Stored as an array of names, not a packed bitmask, so git diffs show exactly which tag changed. Unknown/duplicate names are dropped on the next `Save` (round-tripped through the resolver). Hand-editable with any of the canonical names in `Mtg/CardEffect.cs` (aliases like `"board wipe"` are also accepted on read, but always re-written canonically). Empty/absent = untagged, which routes to the `_Untagged` bucket in the effect-gated views. |
| `reviewedAt` | ISO-8601 UTC timestamp (`DateTimeOffset?`), omitted when null | `tag` only | Stamped automatically the instant a human changes anything for a card in the tagger (rating, status, or an effect toggle) — it is the "someone actually looked at this" marker used by the tagger's progress counter and untagged-filter. `null`/absent = never manually reviewed. The store preserves this verbatim on every `Save` — nothing else re-stamps or clears it, so do not hand-edit it unless you intend to reset the review flag for that card. |

### Hand-editing checklist

- Edit whichever tier file currently holds the card (`pool.json` for your
  active cube is the common case). Don't rename the `oracle_id` keys.
- If you hand-edit `rating` to cross a tier boundary (e.g. bump a
  `fringe.json` entry from 2 to 4), it stays in the "wrong" file until the
  next time any command (`tag`, `import`, `build-views`) loads and re-saves
  the metadata — at that point it's automatically moved to the correct file.
- `effects` must use member names from `Mtg/CardEffect.cs` (or a recognized
  alias); anything else is silently dropped on the next save by any command.
- `status` must be one of `Banned`, `Token`, `Jolly`, or omitted — anything
  else is silently treated as "no status".
- Don't touch `reviewedAt` unless you specifically want to reset a card's
  reviewed flag back to "never reviewed" (delete the field) — leave it alone
  otherwise so the tagger's progress tracking stays accurate.
- Re-running `tag`, `import`, or `build-views` will re-save all three tier
  files in canonical, sorted form regardless of how they were hand-edited, so
  formatting drift self-heals; only the *values* need to be correct. An
  untouched tier's bytes never change from this, so a stray edit to
  `pool.json` alone won't cause a spurious diff in `unrated.json`.

## The `Views/` folder tree

`build-views` links (never copies) each master `.png` into every applicable
root below. A card can appear multiple times within a root when it carries
several effects. Two blanket rules apply across **all** roots:

- **Any status (Banned/Token/Jolly) → a single flat tag folder.** A card with a
  `status` goes to exactly one top-level folder — `0_Banned/`, `0_Token/`, or
  `0_Jolly/` — with the card directly inside (no color/type/cost split), and
  appears in NO other root. Checked before the rating rules, so a tagged card is
  never dropped by the rating-1-2 exclusion (a banned 1-star card still shows in
  `0_Banned/`). A normal card (no status) is unaffected.
- **D7 — normal rating 1-2 is never generated.** A normal (statusless) card
  rated 1 or 2 appears in no view root at all. It still exists in the master
  folder and `fringe.json`; it's just rarely useful to browse.

Roots (all generated every run):

| Root | Layout | Included cards |
|---|---|---|
| `0_Banned/` `0_Token/` `0_Jolly/` | flat — card directly inside | `status` = that tag (any rating) |
| `0_Unrated/` | `{Year}/{SetName}/` (B4) | rating `0`, no status |
| `1_Deep_Effect/` | `Color / MacroType / Effect / Cost N / Rating` | rating `>=3`, no status |
| `1_Deep_Rating/` | `Color / Rating / MacroType / Effect / Cost N` | rating `>=3`, no status |
| `2_ByRating/` | `{N}_Stars/{Color}/` (N = 3, 4, or 5) | rating `>=3`, no status |
| `3_ByEffect/` | `{Effect}/{Color}/` | rating `>=3`, no status |
| `4_ByColor/` | `{Color}/{MacroType}/Cost N/` | rating `>=3`, no status |
| `5_ByType/` | `{MacroType}/` (flat convenience) | rating `>=3`, no status |

Rating 0 (unrated) now lives **only** in `0_Unrated` — it is deliberately kept
out of `3_ByEffect`/`4_ByColor`/`5_ByType` so the ~26k-card backlog can never
flood and choke those browse roots in Adobe Bridge.

Notes:

- **`0_Unrated` is keyed by year/set, not color/type (B4).** This root holds
  the entire unrated backlog — tens of thousands of cards, effectively the
  whole `unrated.json` tier — so it mirrors the physical master folder's
  year/expansion layout (`{ReleasedAt.Year}/{SetName}`) rather than the
  color/type split every other root uses. Color/type isn't a useful axis for
  cards nobody has evaluated yet; year/set makes it easy to work through the
  backlog one expansion at a time. `SetName` is sanitized the same way as
  every other path segment (forbidden filesystem characters stripped).
- **Both `1_Deep_*` variants are generated** (`P7`) so the two level orderings
  (effect-first vs. rating-first) can be compared in daily use; keep whichever
  turns out more useful, delete the other's shortcut in your file browser.
- **"MacroType"** ("Supertipo", `P6`) is `Creature`, `Land`, `OtherPermanent`,
  or `Spell` — Scryfall's type line collapsed to the coarse bucket the cube
  cares about (see `Mtg/MacroTypeResolver.cs`).
- **`{Color}`** (used by every browse root — not the flat `0_*` tag folders or
  `0_Unrated`) is the guild/shard-aware
  folder name from `ColorCategoryClassifier.ViewFolderName` (e.g. `1 White`,
  `2 Azorius`, `3 Esper`, `4 4-5 Colors`, `5 Colorless`) — the numeric prefix
  keeps a plain filesystem name-sort grouping mono > guild > shard/wedge >
  4-5 > colorless.
- **Untagged cards** in the effect-gated views (`1_Deep_Effect`,
  `1_Deep_Rating`, `3_ByEffect`) fall under an `_Untagged` node instead of
  being skipped, so a rated-but-not-yet-tagged card is still findable.
- `build-views` deletes and regenerates the whole `Views/` root every run —
  it is always a full rebuild, never an incremental patch, so it's always
  consistent with the current metadata.
