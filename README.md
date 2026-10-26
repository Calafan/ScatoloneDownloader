# ScatoloneDownloader

Command-line tool behind the **Scatolone**, our club's *Magic: The Gathering*
cube. It does two jobs:

- **Downloads card images** from [Scryfall](https://scryfall.com) into folders
  ready to print. A double-faced card is composed into a single image, both
  faces side by side.
- **Manages the cube**: a rating, a status and effect tags for every card, kept
  as JSON in the [ScatoloneQuintet](https://github.com/Calafan/ScatoloneQuintet)
  repository, with a web tagger to edit them, a classifier that proposes effect
  tags from the rules text, and the folders and report used to browse the cube.

## Requirements

- [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0): the runtime for
  the published executable, or the SDK to build from source.
- An internet connection. The tool keeps to Scryfall's rate limit (about 10
  requests a second) and retries on 429 and 5xx responses.

## Building

```powershell
# Run from source
dotnet run --project ScatoloneDownloader -- <command> [options]

# Run the tests
dotnet test

# Publish a single executable (framework-dependent, Windows x64)
dotnet publish -c Release -r win-x64
# -> ScatoloneDownloader/bin/Release/net10.0/win-x64/publish/ScatoloneDownloader.exe
```

The publish produces **one** `ScatoloneDownloader.exe`, with SkiaSharp's native
libraries inside. It needs the .NET 10 runtime installed.

## Downloading images

```
ScatoloneDownloader <command> [arguments] [options]
```

| Command | What it downloads | Argument |
|---|---|---|
| `all` | Every card, one printing each (library rules), grouped by year and set | — |
| `set <SETS>` | Every valid printing of the given sets | one or more set codes (e.g. `neo dmu`) |
| `years <YEARS>` | Cards first released in the given years (1993–2050), library rules | one or more years |
| `files <FILES>` | Cards from hand-written lists, plus a stats file | one or more files |
| `lands` | **Every** basic land artwork, split by land type | — |
| `analyze <FILES>` | Analyses the lists **without** downloading images | one or more files |

### Common options

| Option | Effect |
|---|---|
| `-o, --output <DIR>` | Output root (default `./Output`) |
| `-c, --clear` | Empty the output folders before starting |
| `-r, --reprints` | Include reprints (left out by default) |
| `-t, --tokens` | Include tokens (left out by default) |
| `-l, --lands` | Include basic lands (left out by default) |
| `-p, --print-only` | Write the card list only, without downloading images |
| `--no-rules` | Ignore the library rules (`all` and `years` only) |
| `--library <DIR>` | Write straight into a `<year>\<set>` library (`all` and `years` only) |
| `-h, --help` | Help, also per command (e.g. `years --help`) |

Library rules: `all` and `years` reproduce the hand-curated library by default.
They use the Default Cards bulk and download one printing per card, the earliest
plain one (non-promo set, no promo treatment). A card with no such printing
takes a plain-looking printing from any set, even one Scryfall flags a reprint
or a masters set (Commander Masters, Universes Within, The List), as long as it
is legal somewhere; a card that exists only as a variant takes its earliest
variant. Funny and Vanguard sets, planes, Alchemy cards, and cards legal in no
format are left out, except dungeons and token-type cards. Without
`--tokens` creature tokens stay out and only a short list of tokens comes in: The
Monarch, Day // Night, City's Blessing, The Ring, Start Your Engines! and the
predefined tokens such as Treasure, Clue and Food (`LibrarySelector.KeptTokens`),
besides dungeons and Role tokens.
Cards of a set not yet released are left out until it is. The pick
is global, so a card first printed in 2010 and reprinted in 2024 appears in the
2010 download only. `--no-rules` restores the previous behaviour (every valid
printing). `--library <DIR>` writes into `<DIR>\<year>\<set>\` with no mode
sub-folder and skips images already there. `set` downloads every valid printing
of the set and, for a card with no valid printing there, its variant.

Command-specific options:

- `all` — `-e, --exclude <FILE>` leaves out the cards listed in the file.
- `lands` — takes only `-o/--output`, `-c/--clear` and `-p/--print-only`. The
  reprint, token and land filters do not apply: it downloads every basic land
  artwork.

### Examples

```powershell
# Cards from 2026
ScatoloneDownloader years 2026

# Cards from 2026, straight into the curated library
ScatoloneDownloader years 2026 --library E:\Working\Repos\ScatoloneQuintet\Source

# Several years, onto an external disk
ScatoloneDownloader years 2024 2025 2026 --output D:\Scryfall

# Two sets, emptying the destination first
ScatoloneDownloader set neo dmu --clear

# Cards from 2026, basic lands included
ScatoloneDownloader years 2026 --lands

# From a hand-written list, basic lands included
ScatoloneDownloader files deck.txt --lands

# Every basic land ever printed, split by type
ScatoloneDownloader lands

# Analysis only, no download
ScatoloneDownloader analyze deck.txt
```

### Output layout

Everything goes under the chosen root (`./Output` by default):

```
<root>/
├─ All/        <year>/<set>/<card>.png
├─ Sets/       <set>/<card>.png
├─ Years/      <year>/<set>/<card>.png
├─ Lists/      <list name>/<tag>/<card>.png
└─ BasicLands/ <type>/<card>.png        (lands command: Plains/, Island/, …)
```

### List files (`files` / `analyze`)

A text file, one card per line. The optional tag after `--` names the
sub-folder the image goes into:

```
Sol Ring -- artifacts
Lightning Bolt -- red
Counterspell
-- a comment (lines starting with -- are ignored)
```

- `Name -- tag` puts the image in `Lists/<list>/<tag>/`.
- A bare `Name` puts it straight in `Lists/<list>/`.
- Basic lands are handled apart and included only with `--lands`.

## Managing the cube

Every evaluation lives in a `metadata/` folder tracked by git, which is the
source of truth: git plus Scryfall is enough to rebuild everything, images
included. The folder is split by rating so the file edited day to day stays
small even with 30k+ cards:

| File | Rating | Contents |
|---|---|---|
| `pool.json` | 3–5 | the cube |
| `fringe.json` | 1–2 | evaluated and cut |
| `unrated.json` | 0 | the rest of the library, not yet evaluated |

The rating scale, the statuses (Banned, Token, Jolly), the entry schema and the
full effect ontology are described in the
[ScatoloneQuintet README](https://github.com/Calafan/ScatoloneQuintet#readme).
The technical reference — tier files, save behaviour, recovery, the report
— is [`docs/cube-metadata.md`](docs/cube-metadata.md).

| Command | What it does | Example |
|---|---|---|
| `tag` | Starts the local web tagger (keyboard-driven) to set rating, status and effects; every change is saved to `metadata/` at once. It opens on the review queue (untagged cards plus classifier proposals not yet confirmed), shuffled. Filters, picked in the left rail, combine by review state, rating (pool 3–5 / fringe 1–2 / unrated / exact stars), status, colour, type, effect, cost and folder (year, then set); `/` opens the card list with a name search. The effect filter is multi-select (all of the ticked effects, or any of them) and the scope is kept in the page address. `/stats` shows the cube's effects by colour, answers against targets, the bench and the creature profile (creatures by cost and by colour), computed live from the tagger's memory, and every count there opens the tagger on exactly those cards. Each effect button carries the tag's **definition** as a tooltip (and as a fixed line above the list on a phone). `-p, --port` sets the port (default 8765); `--host` adds a host name to answer to besides localhost, e.g. a Tailscale name | `ScatoloneDownloader tag` |
| `classify` | Proposes effect tags from each card's Scryfall rules text. Proposals only: it writes `effects` but never `reviewedAt`, and never touches a reviewed card. `--overwrite` re-proposes over unreviewed entries, `--dry-run` reports without writing | `ScatoloneDownloader classify --overwrite` |
| `audit` | Lists **reviewed** cards that say the same thing but were tagged differently — the slips repetitive tagging produces. Read-only. `--since` limits the report to recent sittings, `--limit` the number of groups | `ScatoloneDownloader audit --since 2026-09-12` |
| `report` | Writes the `Cubo_Analysis.md` report (section 8 is the creature profile of the pool; default `Views\Cubo_Analysis.md` in the library root, or `-o <FILE>`) from `metadata/`. Writes nothing else | `ScatoloneDownloader report` |
| `register` | Adds the library's new PNGs to `metadata/` as unrated entries. Never touches an existing entry; run `classify` after it to propose the new cards' effects. `--dry-run` reports without writing. Don't run it while `tag` is open | `ScatoloneDownloader register` |
| `sync` | Brings the library up to the library rules in one go: downloads the cards the rules select and `Source\` lacks (one Scryfall bulk download, reused by every step), registers them in `metadata/` as unrated entries and proposes their effects (never `reviewedAt`, never an existing entry). Files no rule selects and store entries with no image are only **listed** — it never deletes or moves a file or removes an entry. `--dry-run` shows the plan and downloads and writes nothing. Don't run it while `tag` is open | `ScatoloneDownloader sync --dry-run` |
| `make-list` | Writes, offline, a download list for `files` with the pool only (rating 3–5). Cards with a status go in `-- Banned` / `-- Token` / `-- Jolly` sections, so `files` sorts them into sub-folders | `ScatoloneDownloader make-list -o pool.txt` |
| `restore --images <DIR>` | Recovery: rebuilds the image folder from every file in `metadata/` plus Scryfall's bulk data | `ScatoloneDownloader restore --images .\Source` |

All of them work against one **library root** holding `Source\` (the images,
`<year>\<set>\<Name>.png`), `metadata\` (the store) and `Views\` (the report).
The root is `"root"` in [`scatolone.json`](ScatoloneDownloader/scatolone.json),
which is copied next to the executable, or `--root <DIR>` for one run; with
neither, the command stops and says where to set it. `tag`, `register`, `report`
and `sync` take no folder argument: their library is `<root>\Source`.
`-m, --metadata <DIR>` overrides only the metadata folder (default
`<root>\metadata`), and `classify`, `audit`, `make-list` and `restore` need no
root at all when it is given. `restore --images <DIR>` stays explicit, since it
writes images. Every command prints the paths it resolved at start-up.

### Starting the tagger: `tagger.cmd`

`tagger.cmd` builds the Release executable and then starts the tagger so that
both this PC and a phone on the same Tailscale network can reach it. The build
comes first on purpose: `classify` run from an old build writes that build's
rules over every unreviewed proposal. The library comes from `scatolone.json`; edit the host names at the top
of the file if the network changes; the tagger's start-up error
prints the `netsh http add urlacl` reservations a new host name needs.

### The effect classifier

`classify` reads the rules text and proposes tags from the 25-effect ontology in
[`Mtg/CardEffect.cs`](ScatoloneDownloader/Mtg/CardEffect.cs). The rulings are in
[`docs/ontology/`](docs/ontology), one file per member: every boundary, when it
was decided, the cards that settled it and how many reviewed cards it moved.
The one-line definitions
shown as tooltips in the tagger are in
[`Mtg/EffectGlossary.cs`](ScatoloneDownloader/Mtg/EffectGlossary.cs), and the
rules themselves in [`Cube/Effects/`](ScatoloneDownloader/Cube/Effects).

It is rule-based and only proposes: a person confirms every card in the tagger,
and those reviewed cards are the ground truth each rule is measured against.
How a ruling is measured and applied is written up in the Claude Code skill
[`.claude/skills/ontology-pass`](.claude/skills/ontology-pass/SKILL.md).

## Notes

- Images come from Scryfall at print size; the faces of a double-faced card are
  placed side by side in one image.
- Downloads are sequential and paced to Scryfall's rate limit; each run ends by
  printing the throughput (total cards, ms per card, cards per second).
- Card data comes from Scryfall's API and bulk data. Please respect Scryfall's
  [terms of use](https://scryfall.com/docs/api). *Magic: The Gathering* is a
  trademark of Wizards of the Coast; this is an unofficial fan project.
