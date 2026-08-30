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
    /// the <c>import</c> command to seed this file; it is never read again.
    /// </summary>
    internal sealed class CardMetadataEntry
    {
        /// <summary>Card name — informational only, for readable diffs. Not a key.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Cube rating: 0 = unrated, 1-5 stars. Authored in the web
        /// tagger (or seeded once from XMP by <c>import</c>); rating 1-2 is
        /// deliberately never surfaced in the generated views (D7) but still
        /// lives here so it round-trips and can be bumped later.</summary>
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

    /// <summary>Root document of <c>cube-metadata.json</c>.</summary>
    internal sealed class CubeMetadata
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>Entries keyed by Scryfall <c>oracle_id</c>.</summary>
        [JsonPropertyName("cards")]
        public Dictionary<string, CardMetadataEntry> Cards { get; set; } = [];
    }

    /// <summary>
    /// Loads and saves <see cref="CubeMetadata"/>. Save output is deterministic —
    /// card keys sorted, each entry's effects re-emitted in canonical order — so
    /// re-running the tagger produces minimal, reviewable git diffs.
    /// </summary>
    internal static class CubeMetadataStore
    {
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

        /// <summary>Reads the metadata file. Returns an empty document when the file
        /// is missing, blank, or corrupt — the tagger should never crash on load.</summary>
        internal static CubeMetadata Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
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

        /// <summary>Writes the metadata file with deterministic ordering, creating
        /// the target directory if needed. Entries are ordered by
        /// <c>(Name, oracle_id)</c> ordinal — not by key — so the file reads like
        /// an alphabetical card list; the key (dictionary insertion order) follows
        /// because <see cref="JsonSerializer"/> emits dictionaries in enumeration
        /// order.</summary>
        internal static void Save(string path, CubeMetadata data)
        {
            CubeMetadata ordered = new()
            {
                Version = data.Version,
                Cards = data.Cards
                    .OrderBy(kvp => kvp.Value.Name, StringComparer.Ordinal)
                    .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .ToDictionary(kvp => kvp.Key, kvp => Canonicalize(kvp.Value)),
            };

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonSerializer.Serialize(ordered, Options));
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
