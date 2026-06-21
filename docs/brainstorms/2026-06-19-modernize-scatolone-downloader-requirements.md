---
date: 2026-06-19
topic: modernize-scatolone-downloader
---

# Modernize ScatoloneDownloader — Requirements

## Summary

Refactor `ScatoloneDownloader` to preserve current behavior while modernizing
its stack and making the three areas the author actually edits — card
filtering, image composition, and CLI input — easy to read and change. New
foundations: `HttpClient`/async, a cross-platform image library (runs on
Windows, macOS, Linux), and structured logging. Downloads stay sequential but
async-ready.

## Problem Frame

The tool works: it reads a written card list (`name -- tag`), queries the
Scryfall API, and downloads card images for physical printing. But it carries
its age. The runtime stack is legacy and Windows-bound: `WebRequest`/
`HttpWebResponse` (obsolete in .NET 9), synchronous `Thread.Sleep` rate
limiting, and `System.Drawing.Common`, which Microsoft has deprecated as a
cross-platform surface.

More importantly, the parts the author edits most are the parts hardest to
edit. The include/exclude decision lives in one compound boolean in
`ScatoloneDownloader/Mtg/Card.cs` (`IsValid`) crossing seven conditions, with
rule data scattered as inline literals (`InvalidSetsType`, `InvalidFrameEffects`,
`WhiteBorderSets`). The image logic — border normalization, outer border, and
the double-face front/rear merge in `ScatoloneDownloader/Mtg/DoubleFaceCard.cs`
— is a wall of graphics math with magic numbers and commented-out dead code.
The CLI options are fragmented across five interfaces with mutual-exclusion
groups, and input validation is scattered through `Program.Run`/`GetCards`
(which takes 11 positional parameters). The recent edits the author made —
excluding card types and tuning the double-face merge — each meant digging into
these tangles.

## Key Decisions

- **Behavior-preserving refactor, not a rewrite.** Existing download behavior
  and output stay the same; the work is internal structure plus stack swaps.
- **Filter as named rules.** The compound boolean becomes an ordered set of
  individually-named, individually-toggleable rules so a single rule can be
  added or removed in one place.
- **Image composition as its own component.** Imaging is pulled out of the
  `Card` model and isolated behind one boundary that owns the image-library
  dependency.
- **CLI gets a clearer structure.** The five-interface options model is
  consolidated; a subcommand-style surface is acceptable even if invocation
  changes from today.
- **Sequential but async-ready.** Network/I-O becomes async-capable, but
  downloads stay sequential — no shared throttle or thread-safe naming until
  speed is a real pain.
- **Equivalent in print, not bit-identical.** The cross-platform image port
  must look the same when printed; pixel-level differences from a different
  resampling engine are acceptable.
- **`ClearFolders` becomes optional.** It no longer deletes output folders on
  every startup.

## Requirements

**Filter & Validation**

- R1. The card-inclusion decision is expressed as an ordered set of
  individually-named, individually-toggleable rules, replacing the single
  compound boolean in `IsValid`.
- R2. The rule data currently inlined as literals (invalid set types, invalid
  frame effects, white-border sets, basic-land types, border dimensions) is
  collected in named, discoverable locations.
- R3. Adding, removing, or changing one filter rule is a single-location change
  that does not require touching unrelated rules.

**Image Composition**

- R4. Image processing (border normalization, outer-border addition, double-face
  front/rear merge) lives in a dedicated component decoupled from the `Card`
  model.
- R5. The image component is the only place that depends on the image library,
  so swapping the library touches one boundary.
- R6. Output images stay equivalent in print to current output — same physical
  size (63×88mm card + 3mm border), correct borders, double faces side-by-side
  at single-card size — verified manually on sample cards.
- R7. Magic numbers in the imaging logic (pixel sample point, border thickness,
  resize ratios) are named, and commented-out dead code is removed.

**CLI Input**

- R8. The options model (currently split across five interfaces with
  mutual-exclusion groups) is consolidated into a clearer command structure;
  a subcommand-style surface is acceptable even if invocation changes.
- R9. Input validation (year range, file existence, mutually-exclusive modes)
  is collected in one clear place rather than scattered through
  `Program.Run`/`GetCards`.
- R10. The 11-parameter `GetCards` dispatch is replaced by a clearer call shape.

**Stack Modernization**

- R11. HTTP access uses `HttpClient` instead of `WebRequest`/`HttpWebResponse`.
- R12. Network and I/O are async-capable (async/await) while downloads remain
  sequential.
- R13. The image library is cross-platform so the tool builds and runs on
  Windows, macOS, and Linux, replacing `System.Drawing.Common`.
- R14. Logging goes through a structured logging abstraction instead of the
  hand-rolled `SimpleLogger` singleton.
- R15. The Scryfall rate limit (~100ms between requests) is preserved through
  the new HTTP path.

**Behavior & Safety**

- R16. `ClearFolders` becomes optional (opt-in flag or confirmation) instead of
  always deleting `All`/`Sets`/`Years`/`Lists` on startup.
- R17. All existing download behavior is preserved: the four modes (All, Set,
  Years, Files), exclude-file handling, per-card tags, the reprints/tokens/lands
  flags, basic-land handling, duplicate detection, and the rule that original
  artwork keeps the un-numbered filename.

## Acceptance Examples

- AE1. **Covers R17.** **Given** a list where the same card name appears across
  multiple printings, **when** downloaded, **then** the original-artwork copy
  keeps the un-numbered filename and later copies are suffixed — unchanged from
  today.
- AE2. **Covers R6, R17.** **Given** a double-faced card whose type line
  contains "Siege", **when** merged, **then** the combined image is rotated 180°
  as it is today.
- AE3. **Covers R16.** **Given** a normal run without the clear-folders option,
  **when** the tool starts, **then** existing output folders are left intact and
  nothing is deleted.

## Scope Boundaries

Deferred — sensible later, not now:

- Automated tests over filtering and image composition (the safety net for
  future edits).
- Parallel downloads (would force a shared throttle and thread-safe naming).
- Externalizing filter rules to a config file editable without recompiling
  (Approach C).

## Dependencies / Assumptions

- The Scryfall API contract (bulk-data endpoints, set/search endpoints, image
  URIs) and its rate-limit expectation are unchanged.
- A cross-platform image library can reproduce the current composition
  (rotate/resize/merge, border fill, pixel sampling for border color) closely
  enough to be indistinguishable in print.
- Verification of image fidelity is manual — sample cards compared before/after.
  There is no automated regression check, by decision.

## Outstanding Questions

Deferred to planning:

- Which cross-platform image library to adopt (and how its resampling compares
  on sample cards).
- Which CLI library and subcommand shape to use, and the exact command surface
  after consolidation.
- Which structured logging target/abstraction to standardize on.
- The exact opt-in mechanism for `ClearFolders` (flag vs. interactive
  confirmation).
