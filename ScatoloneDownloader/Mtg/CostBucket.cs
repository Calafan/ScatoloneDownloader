using System.Globalization;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// The coarse mana-value buckets the tagger's cost picker groups on.
    /// </summary>
    internal static class CostBucket
    {
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
