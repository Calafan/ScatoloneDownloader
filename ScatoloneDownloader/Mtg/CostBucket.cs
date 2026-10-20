using System.Globalization;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// The coarse mana-value buckets the tagger's cost picker groups on.
    /// </summary>
    internal static class CostBucket
    {
        /// <summary>Every bucket, in ascending cost order.</summary>
        internal static readonly string[] All = ["0", "1", "2", "3", "4", "5", "6_Plus"];

        /// <summary>Display form of a bucket: "6_Plus" reads "6+", the rest as they are.</summary>
        internal static string Label(string bucket)
        {
            return bucket == "6_Plus" ? "6+" : bucket;
        }

        /// <summary>Cost bucket: exact 0-5, "6_Plus" for 6 or more. Invariant
        /// culture so the decimal never leaks into the name. The tagger page
        /// groups on these very strings, so they must not change.</summary>
        internal static string Of(double cmc)
        {
            return cmc >= 6
                ? "6_Plus"
                : ((int)cmc).ToString(CultureInfo.InvariantCulture);
        }
    }
}
