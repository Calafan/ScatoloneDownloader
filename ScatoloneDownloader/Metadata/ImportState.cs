#nullable enable annotations

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScatoloneDownloader.Metadata
{
    /// <summary>
    /// Bookkeeping for the <c>import</c> command's incremental mode: when the last
    /// import ran, so the next one can skip files Adobe Bridge has not touched
    /// since. Unlike <see cref="CubeMetadata"/> this is derived state, not truth —
    /// deleting it costs nothing but a full rescan.
    /// <para>
    /// It earns its keep on spinning disks. The XMP pass is bound by seek latency,
    /// not by CPU or bytes: reading 30151 files costs about 15 minutes on the 7200
    /// rpm drive this library lives on, whether the reader decodes the image or
    /// walks the PNG chunk table. A Bridge session touches a few hundred files, so
    /// skipping the untouched ones is the only change that removes the cost rather
    /// than shaving it.
    /// </para>
    /// </summary>
    internal sealed class ImportState
    {
        internal const string FileName = "import-state.json";

        /// <summary>UTC instant the last import STARTED. Deliberately the start and
        /// not the finish: a file Bridge writes while the scan is running then has a
        /// timestamp at or after this mark and is picked up next run, instead of
        /// falling into the gap between the scan reaching it and the run ending.</summary>
        [JsonPropertyName("lastImportUtc")]
        public DateTimeOffset? LastImportUtc { get; set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// Reads the watermark, or returns an empty state when the file is absent,
        /// blank, or unreadable. Corruption deliberately degrades to "no watermark"
        /// — a full rescan — rather than throwing: the failure mode of this file
        /// must never be "silently skipped a card", only "did more work than
        /// strictly needed". Contrast <see cref="CubeMetadataStore.Load"/>, which
        /// aborts on a corrupt tier file because that one IS the truth.
        /// </summary>
        internal static ImportState Load(string metadataDirectory)
        {
            string path = Path.Combine(metadataDirectory, FileName);

            try
            {
                if (!File.Exists(path))
                {
                    return new ImportState();
                }

                string json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new ImportState();
                }

                return JsonSerializer.Deserialize<ImportState>(json, Options) ?? new ImportState();
            }
            catch (Exception)
            {
                return new ImportState();
            }
        }

        /// <summary>Stamps the watermark, creating the metadata directory if needed.</summary>
        internal static void Save(string metadataDirectory, DateTimeOffset startedUtc)
        {
            Directory.CreateDirectory(metadataDirectory);

            ImportState state = new() { LastImportUtc = startedUtc.ToUniversalTime() };

            File.WriteAllText(
                Path.Combine(metadataDirectory, FileName),
                JsonSerializer.Serialize(state, Options));
        }
    }
}
