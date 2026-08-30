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
    /// <see cref="Save"/> always rewrites all three files from the full
    /// in-memory set, so a rating change transparently moves a card from one
    /// tier file to another. Every file is otherwise as deterministic as before
    /// — entries sorted by <c>(name, oracle_id)</c>, effects/status
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

        /// <summary>Routes a card to its tier file by its CURRENT rating (B1):
        /// 3-5 -> pool (active cube), 1-2 -> fringe (evaluated but cut), 0 (or
        /// any unexpected out-of-range value, defensively) -> unrated.</summary>
        internal static string TierFileName(int rating)
        {
            if (rating >= 3)
            {
                return "pool.json";
            }

            if (rating >= 1)
            {
                return "fringe.json";
            }

            return "unrated.json";
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

        /// <summary>Reads a single tier file. Returns an empty document when the
        /// file is missing, blank, or corrupt.</summary>
        private static CubeMetadata LoadTierFile(string path)
        {
            if (!File.Exists(path))
            {
                return new CubeMetadata();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<CubeMetadata>(json, Options) ?? new CubeMetadata();
            }
            catch (Exception)
            {
                return new CubeMetadata();
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

            foreach (string tierFileName in TierFileNames)
            {
                CubeMetadata tierDocument = new()
                {
                    Version = data.Version,
                    Cards = byTier[tierFileName]
                        .OrderBy(kvp => kvp.Value.Name, StringComparer.Ordinal)
                        .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                        .ToDictionary(kvp => kvp.Key, kvp => Canonicalize(kvp.Value)),
                };

                string tierPath = Path.Combine(fullDirectory, tierFileName);
                File.WriteAllText(tierPath, JsonSerializer.Serialize(tierDocument, Options));
            }
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
