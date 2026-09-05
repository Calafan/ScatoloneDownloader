#nullable enable annotations

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Converts between <see cref="CardStatus"/> and its JSON string form. Unlike
    /// <see cref="EffectResolver"/> this is single-valued (not flags): a card has
    /// at most one status. Parsing is unknown-safe (falls back to
    /// <see cref="CardStatus.None"/>, never throws); serialization emits the
    /// canonical enum member name, or <c>null</c> for <see cref="CardStatus.None"/>
    /// so normal pool cards omit the "status" property entirely.
    /// </summary>
    internal static class StatusResolver
    {
        /// <summary>Resolves a stored/raw status string. Blank, whitespace, or
        /// unrecognized input all resolve to <see cref="CardStatus.None"/>.</summary>
        internal static CardStatus Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return CardStatus.None;
            }

            return Enum.TryParse(raw.Trim(), ignoreCase: true, out CardStatus parsed)
                ? parsed
                : CardStatus.None;
        }

        /// <summary>Canonical member name for JSON storage, or <c>null</c> for
        /// <see cref="CardStatus.None"/> (omitted by <c>WhenWritingNull</c>).</summary>
        internal static string? ToName(CardStatus status)
        {
            return status == CardStatus.None ? null : status.ToString();
        }

        /// <summary>
        /// One-time default mapping from a legacy Adobe Bridge XMP label to a
        /// <see cref="CardStatus"/>, used only by the <c>import</c> seed command
        /// and only when the JSON entry has no status yet.
        ///
        /// Bridge writes whatever text the user named that colour slot, NOT the
        /// colour: this library's XMP says "Banned", "Token", "Revisione",
        /// "Seleziona". Reading only the colour names is what left 21 red- and
        /// yellow-labelled cards (Zur's Weirding among them) sitting at status
        /// None after every import. So the colours stay mapped, for a stock Bridge
        /// setup, and anything else falls through to <see cref="Parse"/>, which
        /// recognises a label that simply names the status. A label that is
        /// neither ("Revisione") still means None.
        /// </summary>
        internal static CardStatus FromXmpLabel(string? label)
        {
            return label?.Trim().ToLowerInvariant() switch
            {
                "red" => CardStatus.Banned,
                "yellow" => CardStatus.Token,
                "green" => CardStatus.Jolly,
                _ => Parse(label),
            };
        }
    }
}
