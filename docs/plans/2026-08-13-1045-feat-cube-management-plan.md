---
artifact_contract: ce-unified-plan/v1
artifact_readiness: requirements-only
product_contract_source: ce-brainstorm
title: "Scatolone Cube Management - Plan"
date: 2026-08-13
---

# Scatolone Cube Management - Plan

## Goal Capsule

**Objective:** Refactor and expand ScatoloneDownloader into a full Cube management system: analyze a 3,000–4,000 card cube, version-control its metadata in git via clean CSV snapshots, generate local virtual folder views via hard links, and recover the physical image library entirely from the versioned repository.

**Product authority:** This plan extends an existing, working CLI (ScatoloneDownloader) with new capabilities. It does not redefine the download/printing workflow already in production; it builds a management layer on top of it.

**Open blockers:** None material. All architectural decisions resolved during brainstorm (see Key Decisions).

---

## Product Contract

### Problem Frame

ScatoloneDownloader currently downloads card images from Scryfall and organizes them for physical printing. A physical cube of 3,000–4,000 cards exists as PNG files on disk, rated and labeled in Adobe Bridge via XMP metadata. There is no version control, no balance analysis beyond a basic text report, no disaster recovery, and no way to generate virtual folder views for browsing by color/type/CMC. The plan addresses four independent but related needs: analyze, version-control, recover, and view.

### Actors

- **Cube designer** (single user): edits card ratings and labels in Adobe Bridge, runs CLI commands to analyze, export, restore, and build views. Technical enough for CLI; does not write CSV by hand.

### Primary Outcome

A single CLI that, from a working folder of rated/labeled card images, can: produce extended balance reports, snapshot the cube state to a git-trackable CSV repository, rebuild the image folder from that repository if lost, and generate local hard-link folder hierarchies for browsing.

### Key Decisions

1. **Source of truth — XMP live, CSV snapshot.** XMP metadata embedded in PNG images is the live working state (edited in Adobe Bridge). CSV files in git are snapshots generated FROM XMP via `export-git`, never written by hand. No database. Runtime joins CSV (4,000 rows, trivial parse) with the Scryfall bulk "Default Cards" download (already plumbed via `GetJsonLinesAsync`) in-memory. Restore = CSV → download missing PNGs → inject XMP from CSV. Sync is monodirectional per command: `export-git` = XMP→CSV, `restore` = CSV→XMP. Zero implicit bidirectional sync.

2. **No DB cache.** LiteDB (original Plan proposal) is eliminated. CSV is source, Scryfall bulk is join data, both fit in memory at ~4,000 cards. Removes a dependency, a schema, and a sync layer.

3. **XMP write — hand-rolled, zero-dep.** PNG XMP lives in an `iTXt` chunk with keyword `XML:com.adobe.xmp`. Read via `MetadataExtractor` (MIT, already considered). Write via hand-rolled PNG chunk manipulation + `System.Xml.Linq` for RDF/XMP XML (~200 lines, TDD with SkiaSharp test infra). Eliminates Magick.NET (AGPL/commercial license) and Adobe XmpCore SDK. Fragile only if Scryfall changes PNG encoding (improbable).
   > **SUPERSEDED (2026-08-30, see `2026-08-30-001-feat-card-effect-ontology-plan.md#4`).** Only XMP *read* is needed (Bridge writes it); `XmpManager` reads via `Magick.NET-Q16-AnyCPU`. No hand-rolled writer, no in-app XMP write.

4. **Bridge labels mapping (locked to existing configuration).** Red = Banned, Yellow = Token, Green = Jolly (Wildcard). Blue and Purple remain free for ad-hoc designer use. Aligned to the user's current Adobe Bridge preset — zero migration, zero custom preset file. `xmp:Label` XMP field stores the color name; the analyzer maps to the semantic category.

5. **Color classification — `color_identity`.** Guilds (2-color) and Shards/Wedges (3-color) classifications use Scryfall's `color_identity` field (includes colored pips from mana cost + rules + color indicator), not `colors` (face-only). Standard for Commander-style cube design. `color_identity` must be added to `JsonCard.cs`.

6. **ManaPips — `mana_cost` parse, separate from color classification.** Pip density (colored mana pips per card vs total CMC) requires parsing the `mana_cost` string (e.g. `"{2}{R}{R}"` → 2 colored pips, CMC 4). `mana_cost` is not currently in `JsonCard.cs` — must be added. Pip density uses `colors`/`mana_cost` for face-level commitment, `color_identity` for classification.

7. **MacroType priority — Creature > Land > OtherPermanent > Spell (Plan default).** Priority order as in original Plan. Edge case: "Land Creature" (Dryad Arbor, ~10 cards historically) resolves to Creature. Annotated as assumption; revisit if land-curve analysis needs Land-first resolution.

8. **CLI — subcommand Spectre, coherent with existing.** New commands: `analyze` (upgrades existing — adds CSV output + extended metrics), `export-git`, `restore`, `build-views`. `render-xmp` (project CSV → XMP on images) is optional tooling, not in MVP. All follow the existing Spectre.Console.Cli pattern (`AsyncCommand<TSettings>`).

9. **Restore — incremental idempotent.** `restore` reads CSV, compares with filesystem, downloads only missing PNGs, injects XMP where absent or misaligned. Idempotent: running twice = no-op. A 4,000-card cube with 3,950 present = ~50 downloads. A fully lost folder = full download. More usable than the Plan's "full rebuild" wording for partial recovery.

10. **Build-views — clean rebuild, idempotent.** `build-views` deletes the Views/ folder and reconstructs from scratch. Hard links on NTFS, same volume as source PNGs (cross-volume fails with a clear error + shortcut suggestion). Missing source PNGs log a warning and skip (another pass processes them). No merge logic — simpler to reason about.

11. **Rating → CSV buckets.** 1★ = `scraps/rank_1_unplayable.csv`, 2★ = `scraps/rank_2_obsolete.csv`, 3–5★ = `active_cube/` (split by color/guild), Label=Banned → `exceptions/banned.csv`, Label=Token → `exceptions/tokens.csv` (new, from Bridge label mapping), Label=Jolly → `exceptions/wildcards.csv`.

12. **CSV format — extended.** `Name,SetCode,CollectorNumber,ManaValue,MacroType,Rating,XmpLabel,ScryfallId,ColorIdentity`. The `ColorIdentity` column is added beyond the original Plan format to drive Guilds/Shards classification without re-fetching Scryfall.
    > **SUPERSEDED (2026-08-30, see `2026-08-30-001-feat-card-effect-ontology-plan.md#2`).** Git versioning moves to a JSON sidecar keyed by `oracle_id` (multi-valued effects don't fit CSV cleanly). The CSV-repository / `export-git` / `restore` flow was never built. CSV survives only as an analyzer report artifact (`SaveAnalysisCsv`).

### Requirements

#### R1 — Extended Card Model
The `Card` model gains: `Rating` (1–5, int), `XmpLabel` (string: "", "Banned", "Token", "Jolly"), `MacroType` (enum: Creature/Land/OtherPermanent/Spell, derived from `TypeLine` by priority), `ColorCategory` (string: "W"|"U"|"B"|"R"|"G"|"Colorless"|"Lands" + guild/shard/4-5 names, derived from `color_identity`), `ManaPips` (int, parsed from `mana_cost`). `JsonCard.cs` gains `color_identity` and `mana_cost` fields. The model fields are populated from Scryfall bulk data at runtime; `Rating` and `XmpLabel` are populated from XMP when available, defaulting to 0/"".

#### R2 — CardAnalyzer Overhaul
The analyzer computes: MacroType ratios (permanents vs spells, creature vs non-creature per color), curve analysis (CMC 1/2/3/4/5/6+ separately for creatures vs spells), average CMC (global, per color, per MacroType), guild/shard/wedge/4-5 color density distribution, rating tier distribution (3★/4★/5★ per color), pip density (colored pips vs CMC), fixing land ratio (dual/fixing lands per guild vs utility). Output: existing `.txt` format (preserved for backward compat) + per-color CSV files under the git repository structure. Cards with a non-empty `Tag` or `IsBasicLand` are excluded (preserves current behavior).

#### R3 — XMP Engine
Read: use `MetadataExtractor` NuGet (MIT) to extract `xmp:Rating` (1–5) and `xmp:Label` (Red/Yellow/Green/Blue/Purple → Banned/Token/Jolly/free) from PNG files. Write: hand-rolled PNG `iTXt` chunk insertion with `System.Xml.Linq`-generated RDF/XMP XML. The writer preserves existing PNG image data; it only inserts/replaces the XMP chunk. Idempotent: writing the same XMP twice produces the same file.

#### R4 — `export-git` Command
Reads the image folder (or a configured root), extracts XMP from every PNG, joins with Scryfall bulk data for `MacroType`/`ColorCategory`/`ManaValue`/`ScryfallId`, and writes the structured CSV repository: `scraps/`, `active_cube/monocolor/`, `active_cube/multicolor/guilds/`, `active_cube/multicolor/shards_wedges/`, `exceptions/`, plus `cube_analysis_summary.txt`. The CSV repository is the snapshot of the cube state at export time.

#### R5 — `restore` Command (Incremental Idempotent)
Reads the CSV repository, cross-references each card with the filesystem. Downloads missing PNGs from Scryfall (reusing `CardDownloader` + `CardImageComposer`). Injects XMP (rating + label) into every image where XMP is absent or misaligned with the CSV. Idempotent: a second run with everything aligned is a no-op. Reports a summary: N downloaded, N XMP-injected, N already aligned.

#### R6 — `build-views` Command (Clean Rebuild)
Deletes `Views/` folder, reconstructs hard-link hierarchy: `0_Exceptions/{Banned,Token,Wildcards}`, `1_ActivePool/[Color_or_Guild]/[MacroType]/CMC_{1,2,3,4,5,6_Plus}/`. Hard links via `Kernel32.dll CreateHardLink` P/Invoke. Same NTFS volume required (cross-volume → clear error + shortcut suggestion). Missing source → warn + skip. No merge logic — full rebuild every run.

#### R7 — CLI Coherence
All new commands follow the existing Spectre.Console.Cli subcommand pattern. Shared options (`-o/--output`, `-c/--clear`) apply where meaningful. `analyze` upgrades the existing command (adds `--format csv|txt|both`, default `both`). `export-git` takes `--output <repo-path>` (default `./mtg-cube-repository`). `restore` takes `--from <repo-path>` + `--images <image-folder>`. `build-views` takes `--from <image-folder>` + `--to <views-path>`.

### Success Criteria

- `analyze` on a 4,000-card cube produces both `.txt` and CSV outputs with all R2 metrics, in under 10 seconds (in-memory join, no network).
- `export-git` on a 4,000-card image folder produces the full CSV repository; git diff on a second run with no rating changes is empty (idempotent).
- `restore` on a folder missing 50 cards downloads exactly those 50 and injects XMP; a second run reports "N already aligned, 0 downloaded, 0 injected".
- `build-views` on a 4,000-card folder produces the hard-link hierarchy; running it twice produces identical results; deleting Views/ and rebuilding is free (hard links, not copies).
- All existing tests (93) remain green; new features add tests following TDD.

### Out of Scope

- GUI card classifier (Plan backlog item).
- LiteDB or any embedded database.
- Magick.NET or any AGPL/commercial dependency.
- Automatic sync between XMP and CSV (all sync is explicit via commands).
- Multi-language support (English only, per Plan §3.1).
- Cloud backup or remote storage.

### Assumptions

- "Land Creature" (Dryad Arbor, ~10 cards) resolves to MacroType.Creature per priority order. Revisit if land-curve analysis needs Land-first.
- Scryfall "Default Cards" bulk contains all English printings needed for the cube. If a card is missing from bulk, `restore` logs a warning and skips it.
- Adobe Bridge labels are configured as Red=Banned, Yellow=Token, Green=Jolly on the user's machine. The CLI does not enforce or create the preset; it reads `xmp:Label` and maps by color name.
- NTFS hard links work on the user's drive configuration (source PNGs and Views/ on the same volume).

---

## Implementation Phases

### Phase 0 — Extended Card Model (prerequisite for all)
Add `color_identity`, `mana_cost` to `JsonCard.cs`. Add `Rating`, `XmpLabel`, `MacroType`, `ColorCategory`, `ManaPips` to `Card.cs`. Implement `MacroType` priority resolver and `ColorCategory` classifier (Guilds/Shards/Wedges/4-5 colors). TDD: pure logic, no I/O.

### Phase 1 — CardAnalyzer Overhaul (MVP branch target)
Refactor `CardAnalyzer.cs` to compute all R2 metrics. Add CSV output alongside `.txt`. Runtime join with Scryfall bulk (in-memory, no DB). TDD: test analyzer output structure with crafted card sets.

### Phase 2 — XMP Engine
Add `MetadataExtractor` NuGet for read. Implement `PngXmpWriter` (hand-rolled iTXt chunk + RDF/XMP). TDD: write XMP to a test PNG, read it back, verify round-trip.

### Phase 3 — `export-git` + `restore`
Implement `export-git` (XMP → CSV repository). Implement `restore` (CSV → download missing + inject XMP). TDD: mock filesystem, verify CSV generation and incremental download logic.

### Phase 4 — `build-views`
Implement `HardLinkEngine` (P/Invoke `CreateHardLink`). Implement `build-views` command. TDD: mock filesystem, verify hierarchy structure; integration test on temp NTFS volume.

---

## MVP Branch Scope

**First branch: Phase 0 + Phase 1** (Extended Card Model + CardAnalyzer Overhaul).

Rationale: no external dependencies (no MetadataExtractor, no P/Invoke), uses only already-plumbed infrastructure (`GetJsonLinesAsync`, `CardFilter`, existing `CardAnalyzer` tests). Delivers standalone value: the analyzer works with rating=0/XmpLabel="" defaults even before XMP integration. Branch name: `feat/cube-analyzer-model`.