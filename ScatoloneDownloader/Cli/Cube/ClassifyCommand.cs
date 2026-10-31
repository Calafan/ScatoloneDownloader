using System.ComponentModel;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Auto-classifies effect tags from Scryfall rules text and PROPOSES them into
    /// the metadata (see <see cref="EffectClassifier"/>). Strictly propose-not-decide:
    /// it writes <c>effects</c> but never stamps <c>reviewedAt</c>, so every
    /// suggestion still shows up in the tagger as pending human review, and it
    /// NEVER touches an entry a human already reviewed (<c>reviewedAt != null</c>).
    /// By default it only fills entries that have no effects yet; <c>--overwrite</c>
    /// re-proposes over any still-unreviewed entry (reviewed ones stay untouched
    /// regardless), which includes WITHDRAWING auto-tags the rules no longer
    /// produce — otherwise tightening a rule could never take effect. Reads the
    /// Scryfall bulk for oracle text; writes only the metadata directory.
    /// </summary>
    internal sealed class ClassifyCommand : AsyncCommand<ClassifyCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            [CommandOption("--overwrite")]
            [Description("Re-propose effects over entries that already have auto-tags (still-unreviewed only). Reviewed entries are never touched.")]
            public bool Overwrite { get; set; }

            [CommandOption("--dry-run")]
            [Description("Report what would be classified without writing the metadata.")]
            public bool DryRun { get; set; }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string metadataDir = settings.ResolveDirectory();

            // -m wins; otherwise the store of the library root. Say which one was
            // picked before touching it.
            AnsiConsole.MarkupLineInterpolated($"[cyan]Metadata:[/] {metadataDir}");

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);
            if (metadata.Cards.Count == 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]No entries in '{metadataDir}'. Nothing to classify.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine("[yellow]Loading bulk data from Scryfall...[/]");

            int classified = 0, reviewed = 0, hasEffects = 0, noProposal = 0, cleared = 0, unresolved = 0;

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();

                Dictionary<string, Card> cardsByOracleId = CardIndex.ByOracleId(allCards);

                foreach ((string oracleId, CardMetadataEntry entry) in metadata.Cards)
                {
                    if (!cardsByOracleId.TryGetValue(oracleId, out Card? card))
                    {
                        unresolved++;
                        continue;
                    }

                    (ClassifyOutcome outcome, CardEffect proposed) = ClassifyDecision.Decide(entry, card, settings.Overwrite);
                    switch (outcome)
                    {
                        case ClassifyOutcome.Reviewed:
                            reviewed++;
                            break;
                        case ClassifyOutcome.AlreadyTagged:
                            hasEffects++;
                            break;
                        case ClassifyOutcome.NoProposal:
                            noProposal++;
                            break;
                        case ClassifyOutcome.Cleared:
                            if (!settings.DryRun)
                            {
                                entry.EffectFlags = CardEffect.None;
                            }

                            cleared++;
                            break;
                        case ClassifyOutcome.Classified:
                            if (!settings.DryRun)
                            {
                                // Propose: write effects, leave reviewedAt null (pending review).
                                entry.EffectFlags = proposed;
                            }
                            classified++;
                            break;
                    }
                }
            }

            if (!settings.DryRun)
            {
                CubeMetadataStore.Save(metadataDir, metadata);
            }

            string clearedNote = cleared > 0
                ? $", {cleared} cleared (rules no longer propose them)"
                : string.Empty;

            string verb = settings.DryRun ? "Would classify" : "Classified";
            string prefix = settings.DryRun ? "[grey](dry-run)[/] " : string.Empty;
            // MarkupLine (not interpolated) so the [green]/[grey] tags render;
            // only integer counts are interpolated, which carry no markup chars.
            AnsiConsole.MarkupLine(
                $"{prefix}[green]{verb} {classified} cards[/] — {reviewed} reviewed (kept), {hasEffects} already tagged (kept), {noProposal} no suggestion{clearedNote}, {unresolved} unresolved.");
            if (!settings.DryRun && classified > 0)
            {
                AnsiConsole.MarkupLine("[grey]All suggestions are unreviewed — confirm them in the tagger (they show as AUTO / pending).[/]");
            }

            return 0;
        }
    }
}
