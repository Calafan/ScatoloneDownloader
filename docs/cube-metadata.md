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
| `metadata/fringe.json` | 1-2 | Evaluated but cut. Grows over time as more of the backlog gets rated and rejected. Rating 1 and rating 2 share this file on purpose: they are browsed differently (only `2` gets a view, see `6_Bench` below) but stored identically, so the view split moves nothing on disk. |
| `metadata/unrated.json` | 0 | The bulk library manifest (tens of thousands of entries). Changes only when Scryfall adds new printings, not from day-to-day curation. |

**Loading** (`CubeMetadataStore.Load`) reads whichever of the three files are
present and merges them into one in-memory set, keyed by `oracle_id`. A
missing `metadata/` directory, a missing individual tier file, or a blank
(whitespace-only) tier file all just contribute nothing. But a tier file that
is **present and non-blank yet cannot be parsed** (a merge conflict, a
hand-edit that broke the JSON) now **throws** and aborts the command, instead
of being silently treated as empty — otherwise the next save would overwrite
and permanently destroy that tier of the recovery manifest. Fix or remove the
file and retry. (If the same `oracle_id` somehow appears in more than one file
— should never happen from normal use — the first one found in `pool.json` →
`fringe.json` → `unrated.json` order wins.)

**Saving** comes in two forms:

- **`CubeMetadataStore.SaveEntry`** — the hot path the tagger uses on every
  edit. It persists a *single* card, touching only the tier file(s) it belongs
  in: the new-rating tier always, plus the previous-rating tier when the
  rating crossed a `pool`/`fringe`/`unrated` boundary. Each touched tier is
  **re-read from disk first**, so a concurrent external change (a `git pull`, a
  second editor, a hand-edit) to *other* entries survives instead of being
  clobbered by the tagger's in-memory snapshot. The new tier is written
  *before* the old one is pruned, so a crash between the two writes leaves the
  card momentarily in both tiers (resolved pool-first by `Load`) rather than
  missing from all of them.
- **`CubeMetadataStore.Save`** — the batch path (e.g. `import`) that rewrites
  all three files from the full in-memory set, routing each entry by
  `TierFileName(rating)`. It stages all three temp files first, then moves them
  into place, so the window in which the tiers are mutually inconsistent is
  just the fast `File.Move` calls.

Either way a rating change "just works" — bump a card from 0 to 4 and it
disappears from `unrated.json` and appears in `pool.json`; there is no
separate "move" step. Within each tier file, entries stay sorted by
`(name, oracle_id)`, so an **untouched tier serializes byte-identically** to
what was already on disk and produces no git diff.

Known cost: an incremental save that touches the `unrated.json` tier (rating a
card *out* of the backlog, or editing a card that stays unrated) still
rewrites that whole ~26k-entry file once. But the common case — editing a card
already in the pool — now rewrites only the small `pool.json`, so working
through the pool no longer pays the backlog's cost on every keystroke.

All four commands below take `-m|--metadata <DIR>` for this directory. Omitted,
it defaults to a `metadata` folder **beside the master library** — the sibling of
`SOURCE_DIR`, the same rule `build-views` uses to place `Views/` — so the store
travels with the images it describes rather than following whatever directory
the command was launched from. `classify`, `make-list` and `restore` take no
`SOURCE_DIR`, so they have nothing to sit beside and fall back to `./metadata`;
each prints the directory it resolved before touching it, so a mismatch with the
importing command shows up on the first line instead of as a mysteriously empty
store. Pass `-m` explicitly to those three whenever the master library is not a
sibling of the working directory.

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
   have a rating/label yet (`--overwrite` forces the XMP value in). `Effects`
   and `ReviewedAt` are never touched by `import` — those belong to the tagger.
   The summary reports what actually *changed* (`Changed: n ratings, n labels,
   n statuses`), not just how many entries were rewritten — the seed touches
   every matched card either way, so without that line a run against the wrong
   folder is indistinguishable from a successful one.
   `--incremental` reads the XMP only of files modified since the previous
   import, whose start instant is recorded in `metadata/import-state.json`.
   On the 30151-file library that turns a 15-minute pass into 4 seconds when
   nothing changed, or ~36 seconds after a Bridge session touching a few
   hundred files. The filter works per **oracle_id group**, never per file: if
   one printing changed, every printing of that card is re-read, because the
   seed keeps the highest rating among them and re-reading only the touched
   file could silently demote the card. Cards not yet in the store are always
   read regardless of timestamps (a `restore` can leave a file older than the
   last import), an unreadable timestamp counts as changed, and a missing or
   corrupt `import-state.json` degrades to a full scan. The default is still a
   full scan.
   `Status` is only ever *default-filled* from the Bridge color label
   (Red/Yellow/Green → Banned/Token/Jolly), never overwritten, and only on an
   entry with no `reviewedAt`: a human who cleared a card back to `None` in the
   tagger writes `null` there, which on disk is indistinguishable from "never
   set", so without that gate a still-red PNG would silently re-ban the card on
   the next import. Run it once when adopting this workflow on an existing
   Bridge-rated cube; rarely needed again afterwards — and don't run it while
   the tagger is open, since the tagger holds the pre-import snapshot in memory
   and would write it back on the next keystroke. Restart `tag` after an
   `import`.
2. **`tag <SOURCE_DIR> [-m metadata] [-p port]`** — the authoring tool going
   forward. Launches a local web UI (keyboard-driven, one card at a time) to
   set rating (0-5), status (None/Banned/Token/Jolly), and effect tags. Every
   change autosaves immediately (incrementally, via `SaveEntry` — only the
   affected tier file(s) are rewritten, see above) — there is no separate
   "save" step, so a closed tab never loses more than the field just changed.
   If a save fails (bad `--metadata` path, disk full, a corrupt tier file) the
   web UI shows a red banner and marks the card unsaved rather than silently
   dropping the edit. Adobe Bridge is optional from this point on; nothing
   reads XMP again unless you re-run `import`.
   The page opens on the **to review** filter — every card with no
   `reviewedAt`, i.e. untagged cards *and* `classify` suggestions in one queue.
   That union is deliberate: `classify` writes `effects` only when it has
   something to propose, so a card it read and found nothing for stays
   untagged and would never surface in an auto-only view, while an
   untagged-only view hides every auto suggestion. `f` cycles
   **to review → all → untagged → auto**; a card leaves the queue only once a
   human confirms it (any edit, or `c` to confirm with no change), which is
   what makes the backlog finite. The queue is re-cut on `f` and on reload,
   not on each save, so cards don't vanish under the cursor mid-pass.
   The queue is **shuffled by default**; `.` toggles back to library order (a
   punctuation key because the effect hotkeys plus `n/b/t/j/c/f` use up the
   whole alphabet — but not the keyboard: `6`-`9` and most punctuation stay
   free, which is where a 21st effect goes rather than into a merge). Cards arrive in `Directory.GetFiles` order, i.e. year
   folders ascending, so working the queue from the top reviews 1993, then
   1994, and so on — which is how the first 65 reviewed cards ended up being
   entirely Alpha and Arabian Nights, a sample far too era-skewed to tune the
   auto-classifier against.
   Two more axes narrow the queue, and they combine with the review filter
   rather than replacing it — the questions "what still needs a human?" and
   "which part of the library am I on?" are independent, so collapsing them
   into one list would force choosing between *pool* and *to review* instead
   of asking for both:
   - **Rating** (`,` cycles, or the picker): any / pool (3-5) / fringe (1-2) /
     unrated / each exact star. The first three mirror the store's own tier
     files, so "pool" here means what it means in `pool.json` — the ~2600
     cards that actually make the cube, against 30k in the library.
   - **Folder** (two pickers): year, then set. Taken from each file's path
     relative to `SOURCE_DIR` (`TagCommand.RelativeFolder`), not from the
     card, because the folder describes how the library is laid out on disk.
     A layout deeper or shallower than `<year>/<set>` still groups by
     whatever levels it has.
   When a combination matches nothing the page shows every card and says so,
   rather than going blank.
   `/` opens the **card list** for the current queue — name, rating, status,
   effects, review badge — with a search box; clicking a row jumps to that
   card. It renders at most 300 rows (search narrows before the cap applies,
   so any name is reachable) because putting 30k rows in the DOM would freeze
   the page for no gain. Keys typed while a picker or the search box has
   focus go to the field, not to the tagger.
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
5. **`make-list -m metadata -o <file>`** — writes a download list (the exact
   format the pre-existing `files` command reads) of the **pool (rating 3-5)**
   from the metadata, offline (no Scryfall call). The plain pool is one card
   name per line, alphabetical; cards with a status are pulled into their own
   `-- Banned` / `-- Token` / `-- Jolly` sections and written as
   `Name -- Status`. Because the `files` reader takes the name before `--` and
   the tag after it (which becomes an output sub-folder), running
   `files <that file>` re-downloads the pool and drops Banned/Token/Jolly cards
   into matching sub-folders automatically. Deterministic (no timestamp), so a
   committed list only diffs when the pool actually changes.
6. **`classify -m metadata [--overwrite] [--dry-run]`** — auto-proposes effect
   tags from Scryfall rules text (see `EffectClassifier`) into the metadata.
   Strictly *propose, never decide*: it writes `effects` but never stamps
   `reviewedAt`, and it never touches a human-reviewed entry. By default it
   only fills entries with no effects yet (`--overwrite` re-proposes over
   still-unreviewed ones; `--dry-run` reports without writing). Every
   suggestion then shows up in the tagger as **AUTO — pending review**, inside
   the default *to review* queue (press `f` there to narrow to auto-only);
   note that a card `classify` had nothing to suggest for stays untagged and
   unreviewed, so it is in the same queue rather than lost; confirming it in the tagger
   is what promotes a suggestion to a human-verified tag (`reviewedAt` gets
   stamped). Rule-based and heuristic — a starting point, not an oracle.

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
| `rating` | `int`, 0-5 | `import` (seed), `tag` (authoritative) | Cube rating; `0` = unrated. **Never comes from XMP after the initial `import`** — `tag`/`build-views` read this field exclusively. **This is also the field that decides which tier file (`pool.json`/`fringe.json`/`unrated.json`) the entry lives in** — the next `Save` moves it if you hand-edit this to cross a tier boundary. Rating `1` is deliberately invisible in every generated view (see D7 below) — it still round-trips, just isn't browsable in `Views/`. Rating `2` is browsable, but only under the `6_Bench` recovery root. |
| `label` | `string` | `import` (copies the Bridge label verbatim), `tag` (preserves it) | Legacy Adobe Bridge color-label text (e.g. `"Red"`), carried along for reference. **Nothing derives behavior from this field anymore** — `status` superseded it. Safe to ignore or blank out by hand. |
| `status` | `string`, one of `Banned \| Token \| Jolly`, or omitted/null for a normal card | `import` (default-fills from the Bridge label, only if empty), `tag` (authoritative, always wins) | Mutually-exclusive pool status. A tagged card (`Banned`/`Token`/`Jolly`) is pulled out of every other view and appears **only** under its own flat top-level folder — `Views/0_Banned/`, `Views/0_Token/`, or `Views/0_Jolly/` — with the card directly inside, at any rating (the status is checked before the rating rules, so a tagged 1-2 star card still shows). Hand-editable directly as one of the strings (case-insensitive on read); an unrecognized or blank value is treated as no status. |
| `effects` | `string[]`, canonical `CardEffect` member names | `tag` only | Functional effect tags (a card can carry several). Stored as an array of names, not a packed bitmask, so git diffs show exactly which tag changed. Unknown/duplicate names are dropped on the next `Save` (round-tripped through the resolver). Hand-editable with any of the canonical names in `Mtg/CardEffect.cs` (aliases like `"board wipe"` are also accepted on read, but always re-written canonically). Empty/absent = untagged, which routes to the `_Untagged` bucket in the effect-gated views. |
| `reviewedAt` | ISO-8601 UTC timestamp (`DateTimeOffset?`), omitted when null | `tag` only | Stamped automatically the instant a human changes anything for a card in the tagger (rating, status, or an effect toggle) — it is the "someone actually looked at this" marker used by the tagger's progress counter and its default to-review filter. `null`/absent = never manually reviewed. The store preserves this verbatim on every `Save` — nothing else re-stamps or clears it, so do not hand-edit it unless you intend to reset the review flag for that card. |

### Hand-editing checklist

- Edit whichever tier file currently holds the card (`pool.json` for your
  active cube is the common case). Don't rename the `oracle_id` keys.
- If you hand-edit `rating` to cross a tier boundary (e.g. bump a
  `fringe.json` entry from 2 to 4), it stays in the "wrong" file until a
  command re-saves *that card*: editing it in `tag` moves it (the incremental
  save prunes the old tier), and `import` (which does a full re-partitioning
  save) moves every mis-filed entry at once. `build-views` only *reads* the
  metadata, so it never moves anything. Simplest: just fix the entry in the
  correct tier file directly (add it to `pool.json`, remove it from
  `fringe.json`).
- `effects` must use member names from `Mtg/CardEffect.cs` (or a recognized
  alias); anything else is silently dropped on the next save by any command.
- `status` must be one of `Banned`, `Token`, `Jolly`, or omitted — anything
  else is silently treated as "no status".
- Don't touch `reviewedAt` unless you specifically want to reset a card's
  reviewed flag back to "never reviewed" (delete the field) — leave it alone
  otherwise so the tagger's progress tracking stays accurate.
- Any save re-writes the tier(s) it touches in canonical, sorted form
  regardless of how they were hand-edited, so formatting drift self-heals;
  only the *values* need to be correct. `import` re-canonicalizes all three
  files; the `tag` incremental save re-canonicalizes only the tier(s) of the
  card you edit. An untouched tier's bytes never change, so a stray edit to
  `pool.json` alone won't cause a spurious diff in `unrated.json`.
- **Never leave a tier file as invalid JSON** (e.g. an unresolved merge
  conflict). Commands now *refuse to load* a present-but-unparseable tier and
  abort, rather than silently dropping it — so fix the JSON (or delete the
  file if you truly mean it to be empty) before re-running anything.

## The `Views/` folder tree

`build-views` links (never copies) each master `.png` into every applicable
root below. A card can appear multiple times within a root when it carries
several effects. Two blanket rules apply across **all** roots:

- **Any status (Banned/Token/Jolly) → a single flat tag folder.** A card with a
  `status` goes to exactly one top-level folder — `0_Banned/`, `0_Token/`, or
  `0_Jolly/` — with the card directly inside (no color/type/cost split), and
  appears in NO other root. Checked before the rating rules, so a tagged card is
  never dropped by the rating-1 exclusion (a banned 1-star card still shows in
  `0_Banned/`). A normal card (no status) is unaffected.
- **D7 — normal rating 1 is never generated.** A normal (statusless) card rated
  `1` appears in no view root at all. It still exists in the master folder and
  `fringe.json`; it was rejected outright, so there is nothing to browse it for.
  Rating `2` is the exception carved out of the original D7: cut, but only just,
  and therefore worth being able to find again — it gets `6_Bench/` below.

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
| `6_Bench/` | `{Color}/{MacroType}/{Effect}/Cost N/` | rating `2`, no status |

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
- **`6_Bench` is the recovery root.** It holds exactly the rating-`2` cards —
  the ones cut by a hair — and is laid out like `1_Deep_Effect` minus the
  trailing rating level, since every card in it is a `2` and a rating folder
  would carry no information. It is kept out of roots `1`-`5` for the same
  reason D7 exists: the browse tree must stay the curated pool. Use it when the
  analysis report exposes a hole — a top-heavy curve, a color short on removal —
  and you want to see what is available to promote back: open the color, the
  macro type, the effect you are missing, then the cost bucket. `Cubo_Analysis.md`
  closes the loop from the other side: its **section 6, Bench Availability**,
  counts the same rating-`2` cards by color and cost bucket, by effect, and (for
  lands) by color identity, so the report that shows the hole also shows what is
  on hand to fill it. That section counts toward no metric in sections 1-5 and is
  omitted entirely when nothing is rated `2`.
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
