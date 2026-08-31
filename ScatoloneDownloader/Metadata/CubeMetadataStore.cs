#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Metadata
{
    /// <summary>
    /// Cube evaluation metadata for a single card, keyed by Scryfall
    /// <c>oracle_id</c> in <see cref="CubeMetadata.Cards"/>. This is the complete,
    /// git-shareable source of truth: rating, status, and effects are authored in
    /// the web tagger and persisted here, so a clone without the image files still
    /// has every evaluation. Adobe Bridge / XMP is legacy input only, read once by
    /// the <c>import</c> command to seed this data; it is never read again.
    /// Physically split across the rating-tier files in the metadata directory
    /// (<see cref="CubeMetadataStore"/>) — this type is the merged, in-memory view.
    /// </summary>
    internal sealed class CardMetadataEntry
    {
        /// <summary>Card name — informational only, for readable diffs. Not a key.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Cube rating: 0 = unrated, 1-5 stars. Authored in the web
        /// tagger (or seeded once from XMP by <c>import</c>); rating 1-2 is
        /// deliberately never surfaced in the generated views (D7) but still
        /// lives here so it round-trips and can be bumped later. This is also
        /// the field <see cref="CubeMetadataStore.Save"/> reads to route the
        /// entry to its tier file (B1/B2) — changing it moves the card.</summary>
        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        /// <summary>Legacy Adobe Bridge color-label text, carried over verbatim by
        /// <c>import</c>/the tagger for reference. Superseded by <see cref="Status"/>
        /// for anything that drives behavior; nothing derives logic from this
        /// field anymore.</summary>
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        /// <summary>Scryfall printing <c>id</c> (not <c>oracle_id</c>) pinning the
        /// exact art/printing this evaluation was made against, so <c>restore</c>
        /// re-downloads the same image. Omitted when null (not yet captured).</summary>
        [JsonPropertyName("scryfallId")]
        public string? ScryfallId { get; set; }

        /// <summary>Ban/Token/Jolly status as a single string (mutually exclusive).
        /// Omitted when null/empty — the common case of a normal pool card.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Effect tags as canonical <see cref="CardEffect"/> member names.
        /// Stored as an array (not a packed int) so git diffs stay readable.</summary>
        [JsonPropertyName("effects")]
        public List<string> Effects { get; set; } = [];

        /// <summary>UTC instant of the last MANUAL review. <c>null</c> = never
        /// reviewed by a human (e.g. auto-tagged and pending review). Only the
        /// tagger stamps this; the store preserves it verbatim so deterministic
        /// re-saves never churn it.</summary>
        [JsonPropertyName("reviewedAt")]
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Convenience view of <see cref="Effects"/> as a flags value.</summary>
        [JsonIgnore]
        public CardEffect EffectFlags
        {
            get => EffectResolver.Parse(Effects);
            set => Effects = EffectResolver.ToNames(value);
        }

        /// <summary>Convenience view of <see cref="Status"/> as a <see cref="CardStatus"/>.</summary>
        [JsonIgnore]
        public CardStatus StatusValue
        {
            get => StatusResolver.Parse(Status);
            set => Status = StatusResolver.ToName(value);
        }
    }

    /// <summary>
    /// One rating-tier file's content, or (after <see cref="CubeMetadataStore.Load"/>)
    /// the merged view of every tier file in the metadata directory.
    /// </summary>
    internal sealed class CubeMetadata
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>Entries keyed by Scryfall <c>oracle_id</c>.</summary>
        [JsonPropertyName("cards")]
        public Dictionary<string, CardMetadataEntry> Cards { get; set; } = [];
    }

    /// <summary>
    /// Loads and saves <see cref="CubeMetadata"/> as a DIRECTORY of rating-tier
    /// files rather than one monolithic JSON file (Plan Part B / B1-B2): the
    /// metadata doubles as the disaster-recovery manifest for the ENTIRE card
    /// library (~30k+ printings and growing, most of them never rated above 0-2),
    /// so a single file would be unwieldy to hand-edit and would churn on every
    /// save regardless of which card actually changed. Splitting by CURRENT
    /// rating keeps the actively-curated pool small:
    /// <list type="bullet">
    /// <item><description><c>pool.json</c> — rating 3-5 (the active cube; small; main hand-edit target).</description></item>
    /// <item><description><c>fringe.json</c> — rating 1-2 (evaluated but cut; grows over time).</description></item>
    /// <item><description><c>unrated.json</c> — rating 0 (the bulk library manifest; changes only when new cards appear).</description></item>
    /// </list>
    /// <see cref="Save"/> rewrites all three files from the full in-memory set
    /// (used by batch commands like <c>import</c>), while <see cref="SaveEntry"/>
    /// persists one card incrementally by touching only the tier file(s) it
    /// belongs in (the hot path for the web tagger). Either way a rating change
    /// transparently moves a card from one tier file to another. Every file is
    /// deterministic — entries sorted by <c>(name, oracle_id)</c>, effects/status
    /// canonicalized — so an untouched tier serializes byte-identically and
    /// produces no git diff.
    /// </summary>
    internal static class CubeMetadataStore
    {
        /// <summary>Tier file names, also the fixed precedence order
        /// <see cref="Load"/> uses to resolve a duplicate <c>oracle_id</c> that
        /// somehow appears in more than one file (shouldn't happen from normal
        /// use — <see cref="Save"/> always partitions a card into exactly one
        /// tier — but a hand-edited directory could produce one).</summary>
        private static readonly string[] TierFileNames = ["pool.json", "fringe.json", "unrated.json"];

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            // Omit reviewedAt (and any other null) so unreviewed entries stay lean.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Keep card names ('//' in split cards, accented set names) literal
            // instead of \u-escaped, for readable diffs.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>Routes a card to its tier file by its CURRENT rating (B1),
        /// using the shared <see cref="RatingTierClassifier"/> so the rating
        /// boundaries live in exactly one place (also read by
        /// <see cref="Cube.ViewGenerator"/>).</summary>
        internal static string TierFileName(int rating)
        {
            return RatingTierClassifier.Classify(rating) switch
            {
                RatingTier.Pool => "pool.json",
                RatingTier.Fringe => "fringe.json",
                _ => "unrated.json",
            };
        }

        /// <summary>
        /// Reads every tier file present in <paramref name="metadataDirectory"/>
        /// and merges them into one <see cref="CubeMetadata"/> keyed by
        /// <c>oracle_id</c>. Tolerant end to end: a missing directory, a missing
        /// individual tier file, or a blank/corrupt tier file all just contribute
        /// nothing rather than throwing — the tagger/views should never crash on
        /// load. If the same <c>oracle_id</c> somehow appears in more than one
        /// tier file, the first one encountered in <see cref="TierFileNames"/>
        /// order (pool -> fringe -> unrated) wins, so the merge is deterministic.
        /// </summary>
        internal static CubeMetadata Load(string metadataDirectory)
        {
            CubeMetadata merged = new();

            if (string.IsNullOrWhiteSpace(metadataDirectory) || !Directory.Exists(metadataDirectory))
            {
                return merged;
            }

            foreach (string tierFileName in TierFileNames)
            {
                string tierPath = Path.Combine(metadataDirectory, tierFileName);
                CubeMetadata tierData = LoadTierFile(tierPath);

                foreach (KeyValuePair<string, CardMetadataEntry> kvp in tierData.Cards)
                {
                    merged.Cards.TryAdd(kvp.Key, kvp.Value);
                }
            }

            return merged;
        }

        /// <summary>Reads a single tier file. A MISSING or blank file returns an
        /// empty document (the normal "this tier has no cards yet" case). A file
        /// that is present and non-blank but cannot be parsed THROWS
        /// <see cref="InvalidDataException"/> instead of being silently treated as
        /// empty: swallowing it here would let the next full-rewrite
        /// <see cref="Save"/> overwrite and permanently destroy a hand-edited or
        /// merge-conflicted tier of the disaster-recovery manifest. Callers run in
        /// a command context and surface the error rather than losing data.</summary>
        private static CubeMetadata LoadTierFile(string path)
        {
            if (!File.Exists(path))
            {
                return new CubeMetadata();
            }

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CubeMetadata();
            }

            try
            {
                return JsonSerializer.Deserialize<CubeMetadata>(json, Options) ?? new CubeMetadata();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Metadata tier file '{path}' is present but is not valid JSON ({ex.Message}). " +
                    "Refusing to load it, so a save cannot overwrite and lose its contents — " +
                    "fix the file (e.g. resolve a merge conflict) or remove it, then retry.",
                    ex);
            }
        }

        /// <summary>
        /// Writes the metadata directory, creating it if needed. ALWAYS rewrites
        /// all three tier files from the full in-memory <paramref name="data"/>
        /// set — each entry is routed to <see cref="TierFileName"/> by its
        /// CURRENT rating, so a rating change moves it into a different file and
        /// disappears from its old one (that is the whole point of B1/B2: no
        /// separate "move" step, just save). Within each tier file, entries are
        /// ordered by <c>(name, oracle_id)</c> ordinal — not by key — exactly as
        /// before, so an unchanged tier serializes byte-identically and produces
        /// no git diff no matter how many times it's re-saved.
        /// </summary>
        internal static void Save(string metadataDirectory, CubeMetadata data)
        {
            string fullDirectory = Path.GetFullPath(metadataDirectory);
            Directory.CreateDirectory(fullDirectory);

            Dictionary<string, List<KeyValuePair<string, CardMetadataEntry>>> byTier =
                TierFileNames.ToDictionary(name => name, _ => new List<KeyValuePair<string, CardMetadataEntry>>());

            foreach (KeyValuePair<string, CardMetadataEntry> kvp in data.Cards)
            {
                byTier[TierFileName(kvp.Value.Rating)].Add(kvp);
            }

            // Stage EVERY tier to a temp file first, then move them all into place.
            // Each move is atomic on the same volume (so a crash mid-write never
            // leaves a torn tier file), and staging first shrinks the window in
            // which the three files are mutually inconsistent — a card demoted
            // across tiers momentarily absent from all of them — to just the few
            // fast File.Move calls rather than the full ~30k serialization.
            List<(string TempPath, string FinalPath)> staged = [];
            foreach (string tierFileName in TierFileNames)
            {
                CubeMetadata tierDocument = new()
                {
                    Version = data.Version,
                    Cards = OrderAndCanonicalize(byTier[tierFileName]),
                };

                string tierPath = Path.Combine(fullDirectory, tierFileName);
                string tempPath = tierPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(tierDocument, Options));
                staged.Add((tempPath, tierPath));
            }

            foreach ((string tempPath, string finalPath) in staged)
            {
                File.Move(tempPath, finalPath, overwrite: true);
            }
        }

        /// <summary>
        /// Persists a SINGLE entry incrementally — the hot path used by the web
        /// tagger on every edit. Touches only the tier file(s) the entry belongs
        /// in: always the new-rating tier, plus the previous-rating tier when the
        /// rating change moved the card across a pool/fringe/unrated boundary. This
        /// replaces the whole-library rewrite the tagger used to do per keystroke
        /// (which re-serialized all ~30k entries — including the huge, unchanged
        /// unrated backlog — every time).
        /// <para>
        /// Each touched tier is RE-READ from disk immediately before rewriting, so
        /// a concurrent external change (a git pull/merge, a hand-edit, a second
        /// process) to any OTHER entry — in this tier or, by never being touched,
        /// in the other tiers — survives instead of being clobbered by a stale
        /// in-memory snapshot.
        /// </para>
        /// <para>
        /// The new-rating tier is written BEFORE the old one is pruned, so a crash
        /// between the two writes leaves the card momentarily in both tiers
        /// (resolved pool-first by <see cref="Load"/>) rather than missing from
        /// every tier. Throws (via <see cref="LoadTierFile"/>) if a touched tier
        /// file is present but unparseable, refusing to overwrite a corrupt tier.
        /// </para>
        /// </summary>
        /// <param name="previousRating">The rating the entry had at its last save,
        /// or <c>null</c> if it was not previously persisted (nothing to prune).</param>
        internal static void SaveEntry(string metadataDirectory, string oracleId, CardMetadataEntry entry, int? previousRating)
        {
            string fullDirectory = Path.GetFullPath(metadataDirectory);
            Directory.CreateDirectory(fullDirectory);

            string newTier = TierFileName(entry.Rating);

            // 1. Add/replace the entry in its destination tier — written first so
            //    the card is never absent from every tier (see remarks above).
            CubeMetadata newDoc = LoadTierFile(Path.Combine(fullDirectory, newTier));
            newDoc.Cards[oracleId] = entry;
            WriteTierFileAtomic(fullDirectory, newTier, newDoc);

            // 2. If the rating moved the card across a tier boundary, prune it from
            //    its previous tier (only rewriting that file if it actually held it).
            if (previousRating is int prev)
            {
                string oldTier = TierFileName(prev);
                if (!string.Equals(oldTier, newTier, StringComparison.Ordinal))
                {
                    CubeMetadata oldDoc = LoadTierFile(Path.Combine(fullDirectory, oldTier));
                    if (oldDoc.Cards.Remove(oracleId))
                    {
                        WriteTierFileAtomic(fullDirectory, oldTier, oldDoc);
                    }
                }
            }
        }

        /// <summary>Serializes one tier document to a temp file then atomically
        /// moves it over the destination, ordering + canonicalizing entries so an
        /// unchanged tier stays byte-identical (no git churn).</summary>
        private static void WriteTierFileAtomic(string fullDirectory, string tierFileName, CubeMetadata document)
        {
            CubeMetadata ordered = new()
            {
                Version = document.Version,
                Cards = OrderAndCanonicalize(document.Cards),
            };

            string tierPath = Path.Combine(fullDirectory, tierFileName);
            string tempPath = tierPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(ordered, Options));
            File.Move(tempPath, tierPath, overwrite: true);
        }

        /// <summary>Orders entries by <c>(name, oracle_id)</c> ordinal and
        /// canonicalizes each, producing the deterministic dictionary a tier file
        /// is written from.</summary>
        private static Dictionary<string, CardMetadataEntry> OrderAndCanonicalize(
            IEnumerable<KeyValuePair<string, CardMetadataEntry>> entries)
        {
            return entries
                .OrderBy(kvp => kvp.Value.Name, StringComparer.Ordinal)
                .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToDictionary(kvp => kvp.Key, kvp => Canonicalize(kvp.Value));
        }

        /// <summary>Normalizes an entry's effects to canonical names (dropping
        /// unknown or duplicate tags via a flags round-trip) and its status to a
        /// canonical member name. <see cref="CardMetadataEntry.ScryfallId"/> is
        /// copied verbatim — it identifies a specific printing and must never be
        /// re-derived by the store.</summary>
        private static CardMetadataEntry Canonicalize(CardMetadataEntry entry)
        {
            return new CardMetadataEntry
            {
                Name = entry.Name,
                Rating = entry.Rating,
                Label = entry.Label,
                ScryfallId = entry.ScryfallId,
                Status = StatusResolver.ToName(StatusResolver.Parse(entry.Status)),
                Effects = EffectResolver.ToNames(EffectResolver.Parse(entry.Effects)),
                ReviewedAt = entry.ReviewedAt, // preserved verbatim — never re-stamped here
            };
        }
    }
}
