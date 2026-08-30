# Cube metadata: workflow, recovery, and schema

This is the reference for `cube-metadata.json` — the git-tracked file that is
the single source of truth for every card evaluation in the cube (rating,
ban/token/jolly status, and functional effect tags) — and for the `Views/`
folder tree generated from it. It documents the final state produced by
`docs/plans/2026-08-30-001-feat-card-effect-ontology-plan.md`; see that plan
for the design rationale (decisions `D1`-`D8`, `P1`-`P9`).

## Guiding principle: recovery from git + Scryfall

`cube-metadata.json` plus the code in this repository, plus a fresh Scryfall
bulk-data download, are **sufficient to reconstruct everything**: every
card's rating, status, and effect tags, and a full re-download of the image
folder. Local `.png` files and any XMP metadata inside them are **derived
artifacts** — re-downloadable, re-injectable, never the only place an
evaluation lives. If you lose the `Images/` folder (or the whole machine),
`restore` rebuilds it from the JSON alone; nothing evaluative is lost.

Concretely: **never hand-edit an image's XMP as the only record of a rating,
status, or effect.** Always let the workflow below land it in
`cube-metadata.json`, because that is the only copy that survives a
re-clone.

## Command lifecycle

```
   (legacy Bridge users only, once)
        import
           |
           v
   cube-metadata.json  <-------+
           |                   |
           v                   |
          tag  --------------->+   (autosaves every keystroke)
           |
           v
      build-views  --> Views/<root>/... (symlinks/hardlinks) + Cubo_Analysis.md

   restore  <-- cube-metadata.json + Scryfall bulk   (disaster recovery)
```

1. **`import <SOURCE_DIR> [-m metadata] [--overwrite]`** — one-time seed. Reads
   the rating and color label Adobe Bridge already wrote into each PNG's XMP
   and migrates them into `cube-metadata.json`. This is the **only** command
   that still reads XMP. By default it only fills entries that don't have a
   rating/label yet in the JSON (`--overwrite` forces the XMP value in).
   `Status`, `Effects`, and `ReviewedAt` are never touched by `import` — those
   belong to the tagger. Run it once when adopting this workflow on an
   existing Bridge-rated cube; rarely needed again afterwards.
2. **`tag <SOURCE_DIR> [-m metadata] [-p port]`** — the authoring tool going
   forward. Launches a local web UI (keyboard-driven, one card at a time) to
   set rating (0-5), status (None/Banned/Token/Jolly), and effect tags. Every
   change autosaves immediately to `cube-metadata.json` — there is no
   separate "save" step, so a closed tab never loses more than the field just
   changed. Adobe Bridge is optional from this point on; nothing reads XMP
   again unless you re-run `import`.
3. **`build-views <SOURCE_DIR> [-v views] [-m metadata]`** — generates the
   `Views/` folder tree (see below) by loading rating/status/effects from
   `cube-metadata.json` and linking (not copying) each master `.png` into
   every folder it belongs in. Also writes `Cubo_Analysis.md` next to the
   views. Safe to re-run any time the JSON changes — it deletes and rebuilds
   the views root each time, so it's always in sync.
4. **`restore -m metadata --images <dir>`** — disaster recovery. Loads
   `cube-metadata.json`, downloads the Scryfall bulk card list, resolves each
   entry to its exact printing (preferring `scryfallId`), and re-downloads +
   recomposes any `.png` missing from `<dir>`. Idempotent — already-present
   files are left untouched, so it's also the right command to top up a
   partial image folder. Writes no XMP; rating/status/effects live only in
   the JSON.

## `cube-metadata.json` schema

Top-level document:

```json
{
  "version": 1,
  "cards": {
    "<oracle_id>": { "...": "CardMetadataEntry, see below" }
  }
}
```

- **`version`** (`int`) — schema version, currently always `1`.
- **`cards`** (`object`, keyed by Scryfall `oracle_id`) — one entry per
  evaluated card. `oracle_id` is stable across reprints/printings (unlike the
  per-printing `id`), so re-matching a card to a *different* printing of the
  same card still resolves to the same entry. Entries are written in
  `(name, oracle_id)` order (see `CubeMetadataStore.Save`), so the file reads
  like an alphabetical card list and diffs stay small and reviewable — do not
  rely on dictionary/key order meaning anything else.

### `CardMetadataEntry` fields

| Field | Type | Who writes it | Meaning / hand-edit rules |
|---|---|---|---|
| `name` | `string` | `import`, `tag` | Card name. **Informational only, not a key** — safe to leave stale after a rename; the app always looks the entry up by `oracle_id`. |
| `scryfallId` | `string` (nullable, omitted when absent) | `import`, `tag` | The Scryfall printing `id` (not `oracle_id`) of the exact art this evaluation was made against. `restore` prefers this to re-download the *same* printing/art; if it's missing or the printing has vanished from the current bulk data, `restore` falls back to the first printing found for the `oracle_id`. Hand-editing this is fine if you know the correct printing UUID; leaving it blank just means `restore` uses the fallback. |
| `rating` | `int`, 0-5 | `import` (seed), `tag` (authoritative) | Cube rating; `0` = unrated. **Never comes from XMP after the initial `import`** — `tag`/`build-views` read this field exclusively. Hand-editable (just a number), but ratings of `1` or `2` are deliberately invisible in every generated view (see D7 below) — they still round-trip through the JSON, just aren't browsable in `Views/`. |
| `label` | `string` | `import` (copies the Bridge label verbatim), `tag` (preserves it) | Legacy Adobe Bridge color-label text (e.g. `"Red"`), carried along for reference. **Nothing derives behavior from this field anymore** — `status` superseded it. Safe to ignore or blank out by hand. |
| `status` | `string`, one of `Banned \| Token \| Jolly`, or omitted/null for a normal card | `import` (default-fills from the Bridge label, only if empty), `tag` (authoritative, always wins) | Mutually-exclusive pool status. `Banned`/`Token` cards are pulled out of every normal view (`0_Unrated`, `1_Deep_*`, `2_ByRating`, `3_ByEffect`, `4_ByColor`, `5_ByType`) and appear **only** under `Views/0_Excluded/{Banned\|Token}/`. `Jolly` is not excluded from anything — it's informational. Hand-editable directly as one of the four strings (case-insensitive on read); an unrecognized or blank value is treated as no status. |
| `effects` | `string[]`, canonical `CardEffect` member names | `tag` only | Functional effect tags (a card can carry several). Stored as an array of names, not a packed bitmask, so git diffs show exactly which tag changed. Unknown/duplicate names are dropped on the next `Save` (round-tripped through the resolver). Hand-editable with any of the canonical names in `Mtg/CardEffect.cs` (aliases like `"board wipe"` are also accepted on read, but always re-written canonically). Empty/absent = untagged, which routes to the `_Untagged` bucket in the effect-gated views. |
| `reviewedAt` | ISO-8601 UTC timestamp (`DateTimeOffset?`), omitted when null | `tag` only | Stamped automatically the instant a human changes anything for a card in the tagger (rating, status, or an effect toggle) — it is the "someone actually looked at this" marker used by the tagger's progress counter and untagged-filter. `null`/absent = never manually reviewed. The store preserves this verbatim on every `Save` — nothing else re-stamps or clears it, so do not hand-edit it unless you intend to reset the review flag for that card. |

### Hand-editing checklist

- Keep the JSON keyed by `oracle_id` — don't rename keys.
- `effects` must use member names from `Mtg/CardEffect.cs` (or a recognized
  alias); anything else is silently dropped on the next save by any command.
- `status` must be one of `Banned`, `Token`, `Jolly`, or omitted — anything
  else is silently treated as "no status".
- Don't touch `reviewedAt` unless you specifically want to reset a card's
  reviewed flag back to "never reviewed" (delete the field) — leave it alone
  otherwise so the tagger's progress tracking stays accurate.
- Re-running `tag`, `import`, or `build-views` will re-save the file in
  canonical, sorted form regardless of how it was hand-edited, so formatting
  drift self-heals; only the *values* need to be correct.

## The `Views/` folder tree

`build-views` links (never copies) each master `.png` into every applicable
root below. A card can appear multiple times within a root when it carries
several effects. Two blanket exclusion rules apply across **all** roots
except `0_Excluded` itself:

- **D7 — rating 1-2 is never generated.** Cards rated 1 or 2 star do not
  appear in any view root at all (not even `0_Unrated` — that's for rating
  `0` specifically). They still exist in the master folder and in the JSON;
  they're just rarely useful to browse, so no links are created for them.
- **Banned/Token status routes exclusively to `0_Excluded`.** A card with
  `status: "Banned"` or `status: "Token"` never appears in any of the other
  roots — only under `0_Excluded/Banned/` or `0_Excluded/Token/`. `Jolly` and
  unset status are not excluded from anything.

Roots (all generated every run):

| Root | Layout | Included cards |
|---|---|---|
| `0_Unrated/` | `{Color}/{MacroType}/` | rating `0`, not Banned/Token |
| `0_Excluded/` | `Banned/`, `Token/` (flat) | `status` is Banned or Token (any rating) |
| `1_Deep_Effect/` | `Color / MacroType / Effect / Cost N / Rating` | rating `>=3`, not Banned/Token |
| `1_Deep_Rating/` | `Color / Rating / MacroType / Effect / Cost N` | rating `>=3`, not Banned/Token |
| `2_ByRating/` | `{N}_Stars/{Color}/` (N = 3, 4, or 5) | rating `>=3`, not Banned/Token |
| `3_ByEffect/` | `{Effect}/{Color}/` | rating `0` or `>=3`, not Banned/Token |
| `4_ByColor/` | `{Color}/{MacroType}/Cost N/` | rating `0` or `>=3`, not Banned/Token |
| `5_ByType/` | `{MacroType}/` (flat convenience) | rating `0` or `>=3`, not Banned/Token |

Notes:

- **Both `1_Deep_*` variants are generated** (`P7`) so the two level orderings
  (effect-first vs. rating-first) can be compared in daily use; keep whichever
  turns out more useful, delete the other's shortcut in your file browser.
- **"MacroType"** ("Supertipo", `P6`) is `Creature`, `Land`, `OtherPermanent`,
  or `Spell` — Scryfall's type line collapsed to the coarse bucket the cube
  cares about (see `Mtg/MacroTypeResolver.cs`).
- **`{Color}`** is the guild/shard-aware folder name from
  `ColorCategoryClassifier.ViewFolderName` (e.g. `1 White`, `2 Azorius`,
  `3 Esper`, `4 4-5 Colors`, `5 Colorless`) — the numeric prefix keeps a plain
  filesystem name-sort grouping mono > guild > shard/wedge > 4-5 > colorless.
- **Untagged cards** in the effect-gated views (`1_Deep_Effect`,
  `1_Deep_Rating`, `3_ByEffect`) fall under an `_Untagged` node instead of
  being skipped, so a rated-but-not-yet-tagged card is still findable.
- `build-views` deletes and regenerates the whole `Views/` root every run —
  it is always a full rebuild, never an incremental patch, so it's always
  consistent with the current JSON.
