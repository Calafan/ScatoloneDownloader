# Cube metadata: workflow, recovery, and schema

This is the reference for the `metadata/` directory — the git-tracked source
of truth for every card evaluation (rating, ban/token/jolly status, and
functional effect tags) for the **entire** card library, ~30k+ printings and
growing — and for the analysis report generated from it. It documents
the final state produced by
`docs/plans/2026-08-30-001-feat-card-effect-ontology-plan.md` (including the
"Part B" follow-up); see that plan for the design rationale (decisions
`D1`-`D8`, `P1`-`P9`, `B1`-`B4`).

## Guiding principle: recovery from git + Scryfall

The `metadata/` directory plus the code in this repository, plus a fresh
Scryfall bulk-data download, are **sufficient to reconstruct everything**:
every card's rating, status, and effect tags, and a full re-download of the
image folder. Local `.png` files are
**derived artifacts** — re-downloadable, never the place an evaluation lives:
nothing reads a rating, status or effect from an image. If you lose the images folder (or the whole machine),
`restore` rebuilds it from `metadata/` alone; nothing evaluative is lost.

Concretely: **a rating, status or effect only exists once it is in
`metadata/`.** Author it in the tagger, because that is the only copy that
survives a re-clone.

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
| `metadata/fringe.json` | 1-2 | Evaluated but cut. Grows over time as more of the backlog gets rated and rejected. Rating 1 and rating 2 share this file on purpose: they are reported differently (only `2` appears, as the bench appendix of the report, see below) but stored identically, so the split moves nothing on disk. |
| `metadata/unrated.json` | 0 | The bulk library manifest (tens of thousands of entries). Changes only when `register` adds new cards, not from day-to-day curation. |

Beside the three tiers sits one file that is **not** a tier and holds no card
state:

| File | Contents |
|---|---|
| `metadata/review-log.jsonl` | Append-only history, one JSON object per save from the tagger: `at`, `oracleId`, `name`, `before`, `after`, `firstReview`, `changed`. |

`before` is what the reviewer was SHOWN — on a first pass, the classifier's
proposal — and `after` is what they left. The tier files keep only the final
answer, which makes agreement between the rules and the human unmeasurable
after the fact: confirming a card without touching it records perfect
agreement by construction and is indistinguishable from a card that was
genuinely re-tagged. `changed` is exactly that distinction, and it is why the
per-sitting trend (73% on 4 Sep, 55% on 12 Sep) could not be read before this
file existed.

It is a side file rather than extra keys on each entry on purpose: adding
fields would rewrite tens of thousands of entries and churn every future diff,
while an append-only file only grows at the end, so a commit shows the sitting
and nothing else. It is also a HISTORY — a card reviewed twice leaves two
lines, which is what makes a trend readable at all. Writing it is never
load-bearing: an I/O failure logs a warning and the tag is still saved.

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
- **`CubeMetadataStore.Save`** — the batch path (e.g. `register`) that rewrites
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

Every command below works against one **library root** holding `Source\` (the
images), `metadata\` (this directory) and `Views\` (the report). The root is
`"root"` in `scatolone.json`, copied beside the executable, or `--root <DIR>` for
one run; without either the command stops and says where to set it. `tag`,
`register`, `report` and `sync` read the images from `<root>\Source`. `-m|--metadata
<DIR>` overrides only this directory (default `<root>\metadata`); with it,
`classify`, `audit`, `make-list` and `restore` need no root at all. Each command
prints the directory it resolved before touching it, so a mismatch shows up on
the first line instead of as a mysteriously empty store.

## Command lifecycle

```
        sync       (download what is missing + register + propose, in one go)
           |
        register   (new PNGs -> unrated entries, never touches existing ones)
           |
           v
   metadata/{pool,fringe,unrated}.json  <-------+
           |                                    |
           v                                    |
        classify  (propose effects)             |
           |                                    |
           v                                    |
          tag  ------------------------------- -+   (autosaves every keystroke)
           |
           v
       report  --> Views/Cubo_Analysis.md

   restore  <-- metadata/ (union of all tiers) + Scryfall bulk   (disaster recovery)
```

1. **`register [--root dir] [-m metadata] [--dry-run]`** — adds the library's
   new cards to the metadata directory. It matches every PNG under
   `<root>\Source` to a Scryfall card by name — a printing from the set the
   file's folder is named after first, since the bulk lists tokens and front
   cards of the same name earlier — and for each `oracle_id` the store
   does not hold yet it adds an entry with the card's name and printing id,
   rating `0`, no status, no effects and no `reviewedAt`. It **never modifies an
   existing entry** (not its name, `scryfallId`, rating or anything else), and
   several files of one card add one entry. Cards with no `oracle_id` are
   skipped and counted. It prints how many were registered, how many were
   already in the store and how many were skipped, lists the new names, and
   ends with the next step: `classify` to propose their effects. `--dry-run`
   reports without writing. Don't run it while the tagger is open, since the
   tagger holds its own snapshot of the store in memory and would write it back
   on the next keystroke; restart `tag` afterwards.
2. **`tag [--root dir] [-m metadata] [-p port]`** — the authoring tool going
   forward. Launches a local web UI (keyboard-driven, one card at a time) to
   set rating (0-5), status (None/Banned/Token/Jolly), and effect tags. Every
   change autosaves immediately (incrementally, via `SaveEntry` — only the
   affected tier file(s) are rewritten, see above) — there is no separate
   "save" step, so a closed tab never loses more than the field just changed.
   If a save fails (bad `--metadata` path, disk full, a corrupt tier file) the
   web UI shows a red banner and marks the card unsaved rather than silently
   dropping the edit.
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
   - **Effect** (picker): one effect, or the two questions a name cannot ask
     - which cards carry nothing at all, and which carry anything at all.
   - **Folder** (two pickers): year, then set. Taken from each file's path
     relative to `<root>\Source` (`TagCommand.RelativeFolder`), not from the
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
3. **`report [--root dir] [-m metadata] [-o file]`** — writes the analysis
   report, `Cubo_Analysis.md`, by loading rating/status/effects from the
   metadata directory (the merged view of all three tier files) and analysing
   the pool (see below). By default it lands in `<root>\Views`, created
   if missing; `-o` names another file. Read-only toward the
   library and the store, and it deletes nothing. Safe to re-run any time the
   metadata changes.
4. **`restore -m metadata --images <dir>`** — disaster recovery. Loads the
   **union of all three tier files** (the full library manifest, not just the
   pool), downloads the Scryfall bulk card list, resolves each entry to its
   exact printing (preferring `scryfallId`), and re-downloads + recomposes
   any `.png` missing from `<dir>`. Idempotent — already-present files are
   left untouched, so it's also the right command to top up a partial image
   folder. Rating/status/effects live only in the metadata, never in the images.
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
7. **`audit -m metadata [--since yyyy-MM-dd] [--limit N]`** — reports reviewed
   cards that say the SAME THING and were tagged differently (see
   `TagConsistencyAuditor`). Tagging tens of thousands of cards by hand is
   repetitive work, and repetitive work slips: Red Mana Battery went untagged
   while its four siblings are Ramp. That is not a taxonomy problem, so no
   amount of arguing about definitions finds it — but grouping cards by what
   their rules text reduces to does, in one pass.

   Two cards share a signature when their oracle text matches once reminder
   text, the card's own name, mana symbols and the SIZE of every number are
   stripped. Three things are kept, each because dropping it reported correct
   cards as contradictions: the sign (`+3/+3` and `-3/-3` are opposite cards),
   the activation cost (`Sacrifice a creature:` and `{T}:` in front of the same
   effect are different cards), and **zero** (`-2/-0` only shrinks power while
   `-1/-1` can kill, which is a distinction the tagging already makes).

   Read-only on purpose. A group that disagrees is either a **slip** — one card
   against a clear majority, marked `>>` — or a **boundary** nobody has ruled
   on, which looks like a family split down the middle. Only a human can tell
   which, and the second kind is a decision to record in `CardEffect`'s doc
   rather than an entry to edit. Run it after a sitting with
   `--since <that day>` so only the fresh groups are printed; the comparison
   always runs against every reviewed card.

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
| `name` | `string` | `register`, `tag` | Card name. **Informational only, not a key** — safe to leave stale after a rename; the app always looks the entry up by `oracle_id`. |
| `scryfallId` | `string` (nullable, omitted when absent) | `register`, `tag`, `sync` | The Scryfall printing `id` (not `oracle_id`) of the art in the library: the printing the library rules pick, which `sync` restamps when a rule change moves the card to another printing. `restore` prefers this to re-download the *same* printing/art; if it's missing or the printing has vanished from the current bulk data, `restore` falls back to the first printing found for the `oracle_id`. Hand-editing this is fine if you know the correct printing UUID; leaving it blank just means `restore` uses the fallback. |
| `rating` | `int`, 0-5 | `register` (always `0`), `tag` (authoritative) | Cube rating; `0` = unrated. **This is also the field that decides which tier file (`pool.json`/`fringe.json`/`unrated.json`) the entry lives in** — the next `Save` moves it if you hand-edit this to cross a tier boundary. Rating `1` is deliberately left out of the analysis (see D7 below) — it still round-trips. Rating `2` appears only in the report's bench appendix. |
| `status` | `string`, one of `Banned \| Token \| Jolly`, or omitted/null for a normal card | `tag` | Mutually-exclusive pool status. A tagged card (`Banned`/`Token`/`Jolly`) is excluded from the report's pool at any rating. Hand-editable directly as one of the strings (case-insensitive on read); an unrecognized or blank value is treated as no status. |
| `effects` | `string[]`, canonical `CardEffect` member names | `tag` only | Functional effect tags (a card can carry several). Stored as an array of names, not a packed bitmask, so git diffs show exactly which tag changed. Unknown/duplicate names are dropped on the next `Save` (round-tripped through the resolver). Hand-editable with any of the canonical names in `Mtg/CardEffect.cs` (aliases like `"board wipe"` are also accepted on read, but always re-written canonically). Empty/absent = untagged. |
| `reviewedAt` | ISO-8601 UTC timestamp (`DateTimeOffset?`), omitted when null | `tag` only | Stamped automatically the instant a human changes anything for a card in the tagger (rating, status, or an effect toggle) — it is the "someone actually looked at this" marker used by the tagger's progress counter and its default to-review filter. `null`/absent = never manually reviewed. The store preserves this verbatim on every `Save` — nothing else re-stamps or clears it, so do not hand-edit it unless you intend to reset the review flag for that card. |

A tier file written before the Adobe Bridge workflow was dropped may still carry
a `"label"` key on its entries. It is ignored on load and not written back, so
the key disappears from a tier the next time that tier is saved.

### Hand-editing checklist

- Edit whichever tier file currently holds the card (`pool.json` for your
  active cube is the common case). Don't rename the `oracle_id` keys.
- If you hand-edit `rating` to cross a tier boundary (e.g. bump a
  `fringe.json` entry from 2 to 4), it stays in the "wrong" file until a
  command re-saves *that card*: editing it in `tag` moves it (the incremental
  save prunes the old tier), and `register` (which, when it adds a card, does a
  full re-partitioning save) moves every mis-filed entry at once. `report` only
  *reads* the metadata, so it never moves anything. Simplest: just fix the entry in the
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
  only the *values* need to be correct. `register` re-canonicalizes all three
  files when it adds a card; the `tag` incremental save re-canonicalizes only the tier(s) of the
  card you edit. An untouched tier's bytes never change, so a stray edit to
  `pool.json` alone won't cause a spurious diff in `unrated.json`.
- **Never leave a tier file as invalid JSON** (e.g. an unresolved merge
  conflict). Commands now *refuse to load* a present-but-unparseable tier and
  abort, rather than silently dropping it — so fix the JSON (or delete the
  file if you truly mean it to be empty) before re-running anything.

## The analysis report

`report` writes `Cubo_Analysis.md`, a Markdown analysis of the **pool**: normal
cards (no status) rated 3-5. It is built by `CardAnalyzer.ForPool`, the same
code the tagger's live `/stats` page uses, so the two cannot disagree. Two
rules decide who is counted:

- **Any status (Banned/Token/Jolly) → left out of every distribution**, at any
  rating. A tagged card is not part of the pool being measured.
- **D7 — normal rating 0 and 1 are never counted.** The unrated backlog and the
  cards rejected outright cannot skew the distributions of the cube. Rating `2`
  is the exception carved out of the original D7: cut, but only just, and
  therefore worth being able to find again — it feeds the report's closing
  **Bench Availability** section.

The Bench Availability section counts the rating-`2` cards by color and cost
bucket, by effect, and (for lands) by color identity, so the report that shows
a hole in the pool (a top-heavy curve, a color short on removal) also shows what
is on hand to fill it. It counts toward no metric in the sections above and is
omitted entirely when nothing is rated `2`. To act on it, raise the card to 3
in the tagger and re-run `report`.

`{Color}` names throughout come from `ColorCategoryClassifier.ViewFolderName`
(e.g. `1 White`, `2 Azorius`, `3 Esper`, `4 4-5 Colors`, `5 Colorless`) — the
numeric prefix keeps a plain name-sort grouping mono > guild > shard/wedge >
4-5 > colorless. The macro type ("Supertipo", `P6`) is `Creature`, `Land`,
`OtherPermanent`, or `Spell` — Scryfall's type line collapsed to the coarse
bucket the cube cares about (see `Mtg/MacroTypeResolver.cs`).
