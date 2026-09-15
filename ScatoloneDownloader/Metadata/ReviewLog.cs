using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using ScatoloneDownloader.Logging;

namespace ScatoloneDownloader.Metadata
{
    /// <summary>
    /// An append-only record of what the classifier proposed and what the human
    /// decided, one line of JSON per save.
    /// <para>
    /// The tier files keep only the final answer, which makes a whole class of
    /// question unanswerable after the fact. Agreement between the classifier and
    /// the human appeared to fall across sittings — 73% on 4 Sep, 55% on 12 Sep —
    /// but the number could not be read, because confirming a card without
    /// changing anything records perfect agreement by construction and looks
    /// identical to a card that was genuinely re-tagged. This log separates the
    /// two: <c>before</c> is what the reviewer was shown, <c>after</c> is what
    /// they left, and <c>changed</c> says whether the review moved anything.
    /// </para>
    /// <para>
    /// Deliberately a SIDE FILE rather than fields on
    /// <see cref="CardMetadataEntry"/>: adding keys would rewrite 30k entries and
    /// churn every future diff, while an append-only file only ever grows at the
    /// end, so a commit shows the sitting and nothing else. It is also a history
    /// rather than a state — the same card reviewed twice leaves two lines, which
    /// is what makes a trend readable at all.
    /// </para>
    /// <para>
    /// Never load-bearing: a failure to write the log must not lose a tag, so
    /// <see cref="Append"/> swallows I/O errors after logging them.
    /// </para>
    /// </summary>
    internal static class ReviewLog
    {
        internal const string FileName = "review-log.jsonl";

        private static readonly ILogger Logger = AppLogger.CreateLogger(nameof(ReviewLog));

        // Compact, one object per line: this file is read by tools, not by hand,
        // and indenting it would multiply its size for nothing. The same
        // unescaped-unicode encoder the tier files use, so a card name with an
        // accent stays readable in a diff.
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>One review: the card, what it held before, what the human
        /// left, and whether that was a first pass or a re-review.</summary>
        internal sealed class Entry
        {
            [JsonPropertyName("at")]
            public DateTimeOffset At { get; set; }

            [JsonPropertyName("oracleId")]
            public string OracleId { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            /// <summary>What the entry held when the reviewer saw it. On a first
            /// review that is the classifier's proposal, which is the whole point
            /// of recording it.</summary>
            [JsonPropertyName("before")]
            public IReadOnlyList<string> Before { get; set; } = [];

            [JsonPropertyName("after")]
            public IReadOnlyList<string> After { get; set; } = [];

            /// <summary>False when the card had already been reviewed once, so a
            /// trend can be measured over first passes alone — a re-review
            /// compares the human against themselves, not against the rules.</summary>
            [JsonPropertyName("firstReview")]
            public bool FirstReview { get; set; }

            /// <summary>Whether the effect tags moved. Confirming an untouched
            /// card is the case the tier files could not distinguish.</summary>
            [JsonPropertyName("changed")]
            public bool Changed { get; set; }
        }

        /// <summary>Appends one line. Creates the file on first use. Callers are
        /// expected to hold whatever lock guards the save they are recording.</summary>
        internal static void Append(string metadataDirectory, Entry entry)
        {
            string path = Path.Combine(Path.GetFullPath(metadataDirectory), FileName);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, JsonSerializer.Serialize(entry, Options) + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The tag itself is already saved; losing a log line is a dented
                // measurement, losing the tag would be lost work.
                Logger.LogWarning("Could not append to {File}: {Message}", FileName, ex.Message);
            }
        }
    }
}
