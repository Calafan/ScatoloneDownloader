---
title: Scryfall bulk-data files migrated from JSON array to gzipped JSONL
category: bugs
component: getmanager
module: Scryfall bulk-data ingestion
tags: [scryfall, bulk-data, jsonl, gzip, deserialization, streaming, regression]
problem_type: bug
severity: P1
date: 2026-08-13
status: fixed
fu_related: FU-4
---

# Scryfall bulk-data files migrated from JSON array to gzipped JSONL

## Problem
Every CLI run that ingests the Scryfall bulk export (`all`, `lands`,
`files`, `analyze`, and any code path that resolves card names via the
`Default Cards` / `Unique Artwork` bulk) crashed at file ingestion time
after Scryfall switched the bulk-export format. The old code tried to
deserialize the bulk file as a single JSON array.

## Symptoms
- Deserialization threw on the bulk fetch (`GetManager.GetCardList`, the
  `/bulk-data` lookup path), because the downloaded body was no longer a
  JSON array.
- The bulk-data metadata endpoint (`/bulk-data/:id`, served at
  `bulkData.Uri`) returns a `bulk_data` metadata object, **not** cards — so
  `GetFromJsonAsync<List<Card>>(bulkData.Uri)` deserialized a metadata
  payload into `List<Card>` and failed.
- A new field, `jsonl_download_uri`, was present in the `/bulk-data`
  listing but not mapped in `BulkData.cs`, so the downloader had no handle
  to the actual cards file.

## What didn't work
- Treating `bulkData.Uri` as the cards URL. It is the metadata endpoint;
  it returns a JSON object describing the bulk file, including the new
  `jsonl_download_uri`. Deserializing that object into `List<Card>` was the
  crash. Confirmed against the live `GET /bulk-data/:id` response
  (Content-Type `application/json`, payload is a single `bulk_data` object).
- Hypothesizing the body was "JSON but pretty-printed". The actual change
  is a *format* change: Scryfall now ships `.jsonl.gz` (one JSON object per
  line, gzip-compressed), as documented at
  <https://scryfall.com/docs/api/bulk-data>. The old code-path buffers and
  array-deserializes; neither works on a gzipped JSONL body.

## Solution
Three coordinated edits, build-clean (0/0).

### 1. Map the new field — `ScatoloneDownloader/Json/BulkData/BulkData.cs`
The bulk-data metadata object now carries `jsonl_download_uri`; add it
next to the existing `Name`/`Uri`.

```csharp
[JsonPropertyName("name")]
public string Name { get; set; }

[JsonPropertyName("uri")]
public string Uri { get; set; }

[JsonPropertyName("jsonl_download_uri")]
public string JsonlDownloadUri { get; set; }
```

`Uri` is intentionally kept — it is still useful as a metadata endpoint
and other code paths may want to inspect `updated_at` / `compressed_size`
later. Removing it would be a wider blast radius than the bug needs.

### 2. Stream the JSONL archive — `ScatoloneDownloader/Scryfall/ScryfallClient.cs`
Add `GetJsonLinesAsync<T>` that streams the bulk file, gunzips it, and
deserializes one record per line, yielding `IAsyncEnumerable<T>`. It reuses
the existing rate-limit gate (`SendWithRetryAsync`, 250 ms + 429/5xx retry)
and the `IdleTimeoutStream` (30 s per-read) guard from FU-4, so the
hardening landed in `8e043d4` is preserved.

```csharp
internal async IAsyncEnumerable<T> GetJsonLinesAsync<T>(
    string url, JsonSerializerOptions options = null)
{
    using HttpResponseMessage response =
        await SendWithRetryAsync(url, HttpCompletionOption.ResponseHeadersRead);

    Stream stream = await response.Content.ReadAsStreamAsync();
    using IdleTimeoutStream guardedStream = new(stream, ReadIdleTimeout);
    using GZipStream gzip = new(guardedStream, CompressionMode.Decompress);
    using StreamReader reader = new(gzip);

    string line;
    while ((line = await reader.ReadLineAsync()) is not null)
    {
        if (line.Length == 0) continue;
        yield return JsonSerializer.Deserialize<T>(line, options);
    }
}
```

Notes:
- `HttpCompletionOption.ResponseHeadersRead` keeps the parse streaming —
  nothing is buffered into memory. The decompressed archive is often
  >1 GB, so this matters.
- The 30 s `ReadIdleTimeout` guards every read, not the whole download; a
  healthy long download is never cut. Same shape as the old
  `GetFromJsonAsync<List<Card>>` path, so FU-4's reasoning still holds.
- `JsonSerializer.Deserialize<T>(line)` uses the same `JsonSerializerOptions`
  (which registers `JsonCardConverter` → `Card.CreateCard`), so the per-record
  construction path is byte-identical to the previous whole-array
  deserialization — same `SingleFaceCard` vs `DoubleFaceCard` decision via
  `JsonCard.ImageUris != null`.

### 3. Use the new field + streamer — `ScatoloneDownloader/GetManager.cs`
Switch the single `GetFromJsonAsync<List<Card>>(bulkData.Uri, ...)` call in
`GetCardList` to `GetJsonLinesAsync<Card>(bulkData.JsonlDownloadUri, ...)`
and accumulate the records into a `List<Card>`.

```csharp
foreach (BulkData bulkData in bulkDataCollection.Data)
{
    if (bulkData.Name == name)
    {
        // Scryfall bulk files are gzipped JSONL — one card per line — not a
        // single JSON array anymore. Stream them line by line.
        List<Card> cards = [];
        await foreach (Card card in scryfallClient.GetJsonLinesAsync<Card>(
            bulkData.JsonlDownloadUri, JsonSerializerOptions))
        {
            cards.Add(card);
        }
        return cards;
    }
}
```

`CardService` downstream is unchanged — it still receives a
`List<Card>` from `GetManager`; the streaming is internal to the fetch.

## Why this works
- **Format match.** Scryfall documents (and the live
  `GET /bulk-data` confirms) that each bulk export is served at
  `jsonl_download_uri` as `application/gzip` content whose decompressed
  body is JSONL — one card per line. `GetJsonLinesAsync` matches that wire
  format exactly: gzip-decompress, then line-by-line `JsonSerializer`.
- **Endpoint match.** `bulkData.Uri` returns the *metadata* `bulk_data`
  object; `bulkData.JsonlDownloadUri` is the *cards* file. The new code
  uses each for its purpose and stops conflating them.
- **Steam preserves prior hardening.** `SendWithRetryAsync` (250 ms
  throttle, 429/5xx backoff, `Retry-After`-aware) and `IdleTimeoutStream`
  (30 s per-read) both wrap the gzip stream — so the FU-4 guarantee that
  a silent connection dies quickly still holds, and rate-limit retry
  survives the JSONL migration unchanged. The `idleTimeout` timer resets
  per read, so a long-but-healthy download is never cut.
- **`Card` / `JsonCardConverter` unchanged.** The line-level
  `JsonSerializer.Deserialize<Card>(line, options)` uses the same
  converter (registered via `JsonSerializerOptions.Converters`) that
  `GetFromJsonAsync` used; per-card construction (`Card.CreateCard` →
  `SingleFaceCard` / `DoubleFaceCard`) is identical to the prior path.

## Prevention
- **Trust official docs for wire-format change.** The migration was
  announced (and a redirect placed under "Bulk Data JSONL" in the docs
  sidebar). Pinning assumptions to the wire format (`List<Card>` from a
  single JSON array) without a unit test against even a small recorded
  bulk-data fixture meant the regression surfaced only at run time.
  Capture a small `.jsonl.gz` fixture (a dozen lines) and cover the
  ingestion round-trip with one integration-style test. The fix path now
  lives in `GetJsonLinesAsync`, which is the right shape to exercise in
  isolation.
- **Map all fields of an external metadata object.** `BulkData.cs`
  mapped only `name` and `uri`. The arrival of `jsonl_download_uri` was
  silent because unmapped JSON properties are ignored by `System.Text.Json`
  by default. Consider `JsonSerializerOptions.UnmappedMemberHandling = Disallow`
  (net9+) on the bulk-data metadata deserialization so additions to the
  contract surface as an error rather than a silently dropped field. That
  validator already exists in `System.Text.Json` 9+ (and is wired in
  net10).
- **Document upstream change logs.** Periodic check of the Scryfall API
  changelog (<https://scryfall.com/blog/category/api>) would have caught
  the bulk-export format migration before a run-time crash. Worth a
  pinned note wherever the integration is referenced.

## Related documents
- `docs/follow-ups/2026-06-21-pre-existing-findings.md` — **FU-4** introduced
  the per-read `IdleTimeoutStream` (30 s) that this fix reuses. The
  streaming JSONL path inherits FU-4's guarantee unchanged.
- `docs/follow-ups/2026-06-21-dotnet10-opportunities.md` — **section 3**
  ("JSON source generation") discussed the bulk path as
  `GetFromJsonAsync<List<Card>>`. That characterization is now stale: the
  hot path is per-line `JsonSerializer.Deserialize<Card>(line)` on
  individual records, streamed via `GetJsonLinesAsync<Card>`. Update that
  follow-up to reflect the new shape.

## Caveats / open follow-ups
- `JsonSerializer.Deserialize<Card>(line)` throws if any single line is
  malformed; one bad record aborts the whole bulk. Acceptable for now
  (Scryfall's exports are well-formed JSONL), but a `try/catch` around the
  single-line parse could surface the offending line index and continue —
  useful when Scryfall ships a one-off broken record. Low priority until
  seen.
- `dotnet10-opportunities.md` section 3's source-generation idea still
  applies (it would benefit the line-level parser too), but the
  reflections vs network split argument is unchanged — wall-clock is
  still I/O-bound.