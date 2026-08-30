---
artifact_contract: ce-unified-plan/v1
artifact_readiness: ready-for-execution
product_contract_source: interactive-session
title: "Cube Metadata (Effects + Ban/Token + Rating), Git Recovery & Restructured Views - Plan"
date: 2026-08-30
supersedes_decisions:
  - "2026-08-13-1045-feat-cube-management-plan.md#3 (XMP hand-rolled / eliminate Magick.NET)"
  - "2026-08-13-1045-feat-cube-management-plan.md#12 (CSV repository as git snapshot)"
---

# Cube Metadata, Git Recovery & Restructured Views - Plan

## Goal Capsule

**Objective:** Make `cube-metadata.json` (git) the complete, hand-editable source of truth for every card evaluation — rating, ban/token status, and functional effects — such that the whole scheme can be recreated from git + Scryfall alone. Author rating, status, and effects in a fast web tagger (Adobe Bridge optional; XMP read once as a seed via `import`), and generate rating-gated, multi-root view trees that don't choke Bridge.

**Product authority:** Extends the working `feature/xmp-manager` branch. Does not change the download/printing workflow; builds a metadata + views layer on top.

## Guiding Principle (anchoring requirement)

**Disaster recovery:** the git repository (`cube-metadata.json` + code) plus the Scryfall bulk download must be sufficient to reconstruct EVERYTHING — rating, ban, token, effects. Local images and their XMP are **derived artifacts** (re-downloadable, re-injectable). Nothing evaluative may live only inside local images.

---

## Status — ✅ COMPLETE (Phases 1-5, 2026-08-30)

All five phases are implemented, documented, and verified on `feature/xmp-manager`:

- **Phase 1** — `cube-metadata.json` schema (`scryfallId`, `status`, name-ordered `Save`), `CardStatus`/`StatusResolver`, `MetadataJsonSynchronizer` (rename of `EffectSynchronizer`), tagger rating/status editing. ✅ DONE
- **Phase 2** — `import` command (XMP → JSON one-time seed). ✅ DONE
- **Phase 3** — `restore` command (git + Scryfall → images, no XMP writer). ✅ DONE
- **Phase 4** — Restructured multi-root `Views/` tree (`0_Unrated`, `0_Excluded`, both `1_Deep_*` variants, `2_ByRating`, `3_ByEffect`, `4_ByColor`, `5_ByType`); `build-views` now reads rating/status/effects from the JSON only. ✅ DONE
- **Phase 5** — Documentation pass: XML doc comments on every new/changed type in Phases 1-4, stale-comment cleanup (`Card.cs` cube-management-field block corrected — rating/status/effects no longer described as XMP-sourced), new `docs/cube-metadata.md` (lifecycle, recovery guarantee, full JSON schema, view-tree layout), README updated with the four new commands. ✅ DONE

**Final verification:** `dotnet build` — 0 warnings / 0 errors. `dotnet test` — 196 passed, 7 failed (the pre-existing `CardAnalyzerTests` stale `.txt`-era assertions called out in Part A below; zero new failures introduced by this feature).

---

## Part A — DONE (foundation, on branch)

Implemented and building clean (solution 0 warnings / 0 errors; 159 tests pass, 7 pre-existing `CardAnalyzerTests` fail = stale `.txt`-era assertions from the `a69cb63` .md migration, unrelated):

- `oracle_id` on `JsonCard` + `Card.OracleId` (stable key across printings).
- `[Flags] enum CardEffect` (16 effects; Creature/Land excluded — they are `MacroType`).
- `EffectResolver` (names/aliases <-> flags, unknown-safe, canonical order).
- `Metadata/CubeMetadataStore.cs` — `CubeMetadata`/`CardMetadataEntry` + deterministic Load/Save; `ReviewedAt` (`DateTimeOffset?`) manual-review timestamp (tagger stamps, store preserves).
- `Card.Effects` + `Mtg/EffectSynchronizer.cs` (loads effects from JSON by oracle_id).
- Analyzer `EffectCounts` + Markdown "## 5. Effect Distribution". **CSV analysis output removed** (redundant; git versioning is the JSON).
- `Cli/CardNameNormalizer.cs` (shared PNG-name -> Scryfall-name matcher).
- `Cli/TagCommand.cs` = `tag` web tagger (HttpListener, hotkey UI, autosave, reviewed badge).
- `ViewGenerator` anglicized; by-color folders use ordered guild/shard names (`1 White`, `2 Azorius`, `3 Esper`, `4 4-5 Colors`, `5 Colorless`) via `ColorCategoryClassifier.ViewFolderName`.
- Consistency: all comments/UI strings English; CS8632 fixed via per-file `#nullable enable annotations`.

---

## Locked Decisions

- **D1. Storage = JSON sidecar** keyed by `oracle_id`, effects as canonical name array. Supersedes the old CSV repository. CSV analysis output removed.
- **D2. `oracle_id` = identity key** (not printing `id`).
- **D3. Effects = `[Flags] enum`**, Creature/Land are not effects.
- **D4. XMP read via Magick.NET** (`Magick.NET-Q16-AnyCPU`).
- **D5. Web tagger** (not Bridge Keywords) for effects/status authoring.
- **D6. `reviewedAt`** = `DateTimeOffset?`, stamped only on manual save, preserved by the store.
- **D7. Never generate rating 1-2 star views** (rarely viewed; keep master + JSON only).
- **D8. Recovery from git + Scryfall** is mandatory (guiding principle).

---

## Open Decisions — resolve ONE AT A TIME before execution

> Fill `RESOLVED:` on each as we lock it. Execution (by Sonnet) starts only when all are resolved.

- **P1 — `status` shape.** ✅ `RESOLVED:` single string field `status` (`Banned | Token | Jolly`, omitted/null = normal). JSON stores the string; in code use a `CardStatus` enum (`None/Banned/Token/Jolly`) + resolver (project convention, like `CardEffect`). Mutually exclusive; hand-editable; optional default mapping from Bridge label (Red->Banned, Yellow->Token, Green->Jolly) only when status is empty.
- **P2 — `scryfallId` in entry.** ✅ `RESOLVED:` yes. Store the printing `id` (`card.Id`) per entry (`[JsonPropertyName("scryfallId")]`) so `restore` re-downloads the exact printing/art. oracle_id = identity key, scryfallId = which printing.
- **P3 — JSON ordering.** ✅ `RESOLVED:` order entries by card `name` (ordinal, tiebreak `oracle_id`); key stays `oracle_id`. `CubeMetadataStore.Save` sorts by `(Name, OracleId)`. Deterministic + hand-navigable.
- **P4 — `import`/`export` timing.** ✅ `RESOLVED:` before the views (closes recovery first).
- **P5 — XMP write.** ✅ `RESOLVED:` **NO XMP writer.** Add rating (0-5) + status editing to the web tagger, so rating/status/effects are all authored in the tagger -> JSON, which becomes authoritative. **Adobe Bridge becomes optional; XMP is legacy INPUT only** — read once by a seed `import` command to migrate existing Bridge ratings/labels into the JSON. After that the tagger rules. `restore` therefore only re-downloads images (no XMP injection). Rating for views/analysis is loaded from the JSON, not XMP.
- **P6 — "Supertipo" = MacroType.** ✅ `RESOLVED:` MacroType (Creature/Land/OtherPermanent/Spell) is View1's 2nd level.
- **P7 — View1 level order.** ✅ `RESOLVED:` generate BOTH variants to compare in use:
  - `1_Deep_Effect/` = `Color / MacroType / Effect / CMC / Rating`
  - `1_Deep_Rating/` = `Color / Rating / MacroType / Effect / CMC`
  Both rating>=3; rated-but-untagged cards go under an `_Untagged` effect node.
- **P8 — Which views to generate.** ✅ `RESOLVED:` ALL roots (0_Unrated, 0_Excluded, 1_Deep_Effect, 1_Deep_Rating, 2_ByRating, 3_ByEffect, 4_ByColor, 5_ByType). Links are cheap; keep the useful ones after trying.
- **P9 — Comments & documentation review.** ✅ `RESOLVED:` yes — a final pass (Phase 5, runs LAST after 1-4) so the entire feature is re-readable months later: intent-explaining XML docs on all new/changed types & methods, no stale comments, a workflow + JSON-schema doc, README updated.

**All decisions resolved — plan is ready for execution.**

---

## Execution Plan (reordered by dependency; execute after P1-P8 locked)

### Phase 1 — Complete JSON schema + tagger becomes authoring authority (source of truth) ✅ DONE
Files: `Metadata/CubeMetadataStore.cs`, `Mtg/Card.cs`, `Mtg/CardStatus.cs` (new), `Mtg/StatusResolver.cs` (new), `Mtg/EffectSynchronizer.cs` -> `Mtg/MetadataJsonSynchronizer.cs`, `Cli/TagCommand.cs`.

1. `CardMetadataEntry`: add `ScryfallId` (`[JsonPropertyName("scryfallId")]`, default null, omitted when null) — **[P2]**. Add `status` single string (`[JsonPropertyName("status")]`, omitted when null/empty) — **[P1]**.
2. New `CardStatus` enum (`None/Banned/Token/Jolly`) + `StatusResolver` (string <-> enum, unknown-safe) — project convention. Add `Card.Status` (`CardStatus`).
3. `CubeMetadataStore.Canonicalize`: copy `ScryfallId` + `status` verbatim (never re-derive). Normalize status via resolver round-trip.
4. `CubeMetadataStore.Save`: order by `(Name, OracleId)` ordinal — **[P3]**. Keep deterministic + `WhenWritingNull`.
5. Synchronizer (rename `EffectSynchronizer` -> `MetadataJsonSynchronizer`): load `Rating`, `Status`, `Effects` from JSON into the card. **Rating for views/analysis now comes from JSON, not XMP.**
6. `TagCommand`: at startup load rating/status/effects from JSON via `MetadataJsonSynchronizer` (NOT from XMP — remove the `MetadataSynchronizer` XMP call from this path). **Add rating (0-5) + status (None/Banned/Token/Jolly) editing to the web UI** (digits `0-5` currently toggle effects — remap effect hotkeys off the digits, use digits for rating; status via keys like `b`/`t`/`j`/`n`). `ApplySave` writes `Rating`, `Status`, `Effects`, `ScryfallId = card.Id`, and stamps `ReviewedAt`. Tagger is the authoring authority; Bridge optional.
7. Build; store round-trip test (status + scryfallId + rating survive Save/Load; ordering by name).

### Phase 2 — `import` command (one-time seed: XMP -> JSON) **[P4]** ✅ DONE
Files: new `Cli/ImportCommand.cs`, `Program.cs`. Purpose: migrate existing Adobe Bridge ratings/labels into the JSON once, so the git snapshot is complete and Bridge can be retired.

1. `import <SOURCE_DIR> [-m metadata] [--overwrite]`: scan PNGs, match via `CardNameNormalizer`, load Scryfall bulk.
2. `MetadataSynchronizer.SyncCardsFromDisk` reads rating/label from XMP (the only place XMP is still read).
3. Load existing `CubeMetadata`. For each matched card **upsert**: set `Name`, `ScryfallId = card.Id`; set `Rating`/`Label` from XMP **only if absent in JSON** (default) or always when `--overwrite`. **PRESERVE** `Effects`, `Status`, `ReviewedAt`.
4. Save (ordered). Report: N added / N updated. Register command.
5. Result: existing Bridge ratings are now in git; going forward the tagger is authoritative, `import` is rarely re-run.

### Phase 3 — `restore` command (git + Scryfall -> images) **[P5]** ✅ DONE
Files: new `Cli/RestoreCommand.cs`, `Program.cs`. **No XMP writer** (P5 = tagger authoritative, Bridge optional).

1. `restore [-m metadata] --images <dir>`: load `CubeMetadata`, load Scryfall bulk, index by printing `id` (fallback oracle_id).
2. For each entry: resolve the card (prefer `scryfallId`), download the image if missing (reuse existing download infra / `ImageUri`). Idempotent (skip present).
3. Report: N downloaded / N already present / N unresolved. Register command.
4. Recovery loop closed: `restore` rebuilds the image folder; rating/status/effects come from the JSON (viewed/edited in the tagger, no Bridge/XMP needed).

### Phase 4 — Restructured views **[P6][P7][P8]** ✅ DONE
Files: `Mtg/ViewGenerator.cs` (rewrite), `Cli/BuildViewsCommand.cs`.

1. Exclude rating 1-2 entirely (**D7**). Exclude `status` Banned/Token from pool/effect/rating/color/type views.
2. Roots (prefix-ordered), each leaf sharded so no folder is huge:
   - `0_Unrated/` (rating 0) — `{color}/{MacroType}/`
   - `0_Excluded/` — `Banned/`, `Token/` (by `status`)
   - `1_Deep_Effect/` — `Color / MacroType / Effect / CMC / Rating` (rating>=3; no-effect -> `_Untagged`) — **[P7]**
   - `1_Deep_Rating/` — `Color / Rating / MacroType / Effect / CMC` (rating>=3; no-effect -> `_Untagged`) — **[P7]**
   - `2_ByRating/{5_Stars|4_Stars|3_Stars}/{color}/`
   - `3_ByEffect/{Effect}/{color}/`
   - `4_ByColor/{color}/{MacroType}/Cost N/`
   - `5_ByType/{MacroType}/` (flat convenience)
   - Generate the set per **[P8]**.
3. "Supertipo" resolves per **[P6]**. Reuse `ColorCategoryClassifier.ViewFolderName` + `EffectResolver.ToNames` + `CmcBucket`.
4. `BuildViewsCommand`: load rating+status+effects from JSON (Phase 1 `MetadataJsonSynchronizer`) before generating; drop the XMP rating sync from this path.

---

### Phase 5 — Comments & documentation review (runs LAST) **[P9]** ✅ DONE
Goal: anyone (or future-you) can re-read the whole feature months later and understand it. Runs after Phases 1-4 are implemented and green, so it documents the final code.

1. **XML doc comments** on every new/changed public/internal type and non-trivial method (`CardStatus`, `StatusResolver`, `MetadataJsonSynchronizer`, `ImportCommand`, `RestoreCommand`, the new `CardMetadataEntry` fields, `TagCommand` rating/status handlers, restructured `ViewGenerator`). Explain **intent/why**, not just what — match the existing style in `Card.cs` / `AnalysisReport.cs` / `ColorCategoryClassifier.cs`.
2. **No stale/contradictory comments**; all English; remove dead TODOs; fix any comment the new code invalidated.
3. **Workflow + schema doc** — a new `docs/cube-metadata.md` (or a README section) describing: the command lifecycle (`import` seed -> `tag` author -> `build-views`; `restore` for recovery), the recovery guarantee (git + Scryfall), and the full `cube-metadata.json` schema (each field: `name`, `scryfallId`, `rating`, `label`, `status`, `effects`, `reviewedAt` — meaning, type, who writes it, hand-edit rules).
4. **README** updated: list the new commands (`tag`, `import`, `restore`, `build-views`) with one-line purpose + example.
5. Mark this plan doc's phases DONE and update the Status section.
6. Final build + test (0 warnings/errors; only the 7 known pre-existing test failures).

## Out of Scope
- Auto-classification of effects (ML/oracle-text). Manual tagging only.
- CSV versioning repository (superseded by JSON).
- Multi-user merge of the JSON (single-curator).
- Refreshing the 7 stale `CardAnalyzerTests` (separate task).

## Assumptions
- One `oracle_id` per normalized image name (first matched printing wins); `scryfallId` pins the exact image.
- Scryfall bulk contains the needed printings; missing -> warn + skip on restore.
- `status` is JSON-authoritative and manually editable; XMP sync never overwrites it.

## Handoff
Once P1-P9 are resolved (filled above), this plan is a complete, file-level spec. Execution delegated to Sonnet, phase by phase (1 -> 2 -> 3 -> 4), building after each. **Phase 5 (docs/comments, P9) runs LAST**, after 1-4 are green, so it documents the final code.

**Executed 2026-08-30 — all five phases complete.** See the Status section above for the final verification numbers. No further phases are planned; follow-on work (e.g. refreshing the 7 stale `CardAnalyzerTests`) is out of scope per the section above and should be tracked as a separate task.
