using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>The per-entry decision of <c>classify</c> (and of <c>sync</c>, which proposes effects for the cards it registers).</summary>
    internal static class ClassifyDecision
    {
        /// <summary>Per-entry decision, extracted pure so the propose-not-decide
        /// contract is unit-testable without the Scryfall bulk. Reviewed entries
        /// are never proposed over; unreviewed entries are filled when empty (or
        /// re-proposed under <paramref name="overwrite"/>); a card the classifier
        /// can't read yields <see cref="ClassifyOutcome.NoProposal"/>, or
        /// <see cref="ClassifyOutcome.Cleared"/> when it already carried tags the
        /// rules have since stopped producing. Never stamps <c>reviewedAt</c> —
        /// the caller applies only the effects.</summary>
        internal static (ClassifyOutcome Outcome, CardEffect Proposed) Decide(CardMetadataEntry entry, Card card, bool overwrite)
        {
            if (entry.ReviewedAt != null)
            {
                return (ClassifyOutcome.Reviewed, CardEffect.None);
            }

            if (entry.EffectFlags != CardEffect.None && !overwrite)
            {
                return (ClassifyOutcome.AlreadyTagged, CardEffect.None);
            }

            CardEffect proposed = EffectClassifier.Classify(card);
            if (proposed == CardEffect.None)
            {
                // Under --overwrite, an entry that HAS auto-tags the current rules
                // would no longer produce has to lose them. Without this, tightening
                // a rule can never take effect: the card keeps a tag no rule in the
                // codebase still stands behind, and re-running classify looks like a
                // no-op. Only ever an unreviewed entry, so it is a stale machine
                // guess being withdrawn, never a human decision.
                return entry.EffectFlags != CardEffect.None
                    ? (ClassifyOutcome.Cleared, CardEffect.None)
                    : (ClassifyOutcome.NoProposal, CardEffect.None);
            }

            return (ClassifyOutcome.Classified, proposed);
        }
    }

    /// <summary>Outcome of classifying one metadata entry (see
    /// <see cref="ClassifyDecision.Decide"/>).</summary>
    internal enum ClassifyOutcome
    {
        /// <summary>An effect suggestion was produced (to be applied unless dry-run).</summary>
        Classified,

        /// <summary>Human-reviewed entry left untouched.</summary>
        Reviewed,

        /// <summary>Already has effects and <c>--overwrite</c> was not given.</summary>
        AlreadyTagged,

        /// <summary>The classifier found nothing to suggest for this card.</summary>
        NoProposal,

        /// <summary>Under <c>--overwrite</c>: the entry carried auto-tags the
        /// current rules no longer produce, so they are withdrawn.</summary>
        Cleared,
    }
}
