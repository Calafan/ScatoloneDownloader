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
    }
}
