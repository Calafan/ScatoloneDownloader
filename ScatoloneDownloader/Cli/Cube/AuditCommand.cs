using System.ComponentModel;
using System.Globalization;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Reports reviewed cards that say the same thing and were tagged differently
    /// (see <see cref="TagConsistencyAuditor"/>). Read-only by design: it never
    /// edits the metadata, because a group that disagrees is as likely to be a
    /// boundary worth ruling on as a slip worth fixing, and only a human can tell
    /// which. Meant to be run after a tagging sitting, while the cards are fresh
    /// — <c>--since</c> narrows the report to groups that sitting touched, though
    /// the comparison always runs against every reviewed card.
    /// </summary>
    internal sealed class AuditCommand : AsyncCommand<AuditCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            [CommandOption("--since")]
            [Description(
                "Only report groups containing a card reviewed on or after this date (yyyy-MM-dd). "
                + "The comparison still runs against every reviewed card — this narrows what is printed, "
                + "so a sitting can be checked without re-reading old findings.")]
            public string? Since { get; set; }

            [CommandOption("--limit")]
            [Description("Maximum groups to print. Default 40; pass 0 for all.")]
            public int Limit { get; set; } = 40;
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            string metadataDir = settings.ResolveDirectory();
            AnsiConsole.MarkupLineInterpolated($"[cyan]Metadata:[/] {metadataDir}");

            DateTimeOffset? since = null;
            if (!string.IsNullOrWhiteSpace(settings.Since))
            {
                if (!DateTimeOffset.TryParse(settings.Since, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]--since '{settings.Since}' is not a date. Use yyyy-MM-dd.[/]");
                    return 1;
                }

                since = parsed;
            }

            CubeMetadata metadata = CubeMetadataStore.Load(metadataDir);
            if (metadata.Cards.Count == 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]No entries in '{metadataDir}'. Nothing to audit.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine("[yellow]Loading bulk data from Scryfall...[/]");

            List<TagConsistencyAuditor.AuditedCard> audited = [];
            HashSet<string> inWindow = [];
            int reviewed = 0, unresolved = 0;

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();

                Dictionary<string, Card> cardsByOracleId = [];
                foreach (Card card in allCards)
                {
                    if (!string.IsNullOrEmpty(card.OracleId))
                    {
                        cardsByOracleId.TryAdd(card.OracleId, card);
                    }
                }

                foreach ((string oracleId, CardMetadataEntry entry) in metadata.Cards)
                {
                    // Only reviewed cards carry a human decision; a classify
                    // proposal disagreeing with another proposal means nothing.
                    if (entry.ReviewedAt is not { } when)
                    {
                        continue;
                    }

                    if (!cardsByOracleId.TryGetValue(oracleId, out Card? card))
                    {
                        unresolved++;
                        continue;
                    }

                    reviewed++;
                    audited.Add(new TagConsistencyAuditor.AuditedCard(
                        oracleId, card.Name ?? oracleId, card.OracleText ?? string.Empty, entry.EffectFlags));

                    if (since == null || when >= since)
                    {
                        inWindow.Add(oracleId);
                    }
                }
            }

            IReadOnlyList<TagConsistencyAuditor.Disagreement> all = TagConsistencyAuditor.Audit(audited);

            List<TagConsistencyAuditor.Disagreement> shown = since == null
                ? [.. all]
                : [.. all.Where(d => d.Cards.Any(c => inWindow.Contains(c.OracleId)))];

            AnsiConsole.MarkupLineInterpolated(
                $"Scored {reviewed} reviewed cards — {all.Count} groups disagree, covering {all.Sum(d => d.Cards.Count)} cards.");

            if (since != null)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[grey]{shown.Count} of them involve a card reviewed since {since:yyyy-MM-dd}.[/]");
            }

            if (unresolved > 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[grey]{unresolved} reviewed entries had no card in the bulk data and were skipped.[/]");
            }

            if (shown.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]Nothing to look at.[/]");
                return 0;
            }

            int printed = 0;
            foreach (TagConsistencyAuditor.Disagreement group in shown)
            {
                if (settings.Limit > 0 && printed++ == settings.Limit)
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"[grey]... and {shown.Count - settings.Limit} more groups. Raise --limit to see them.[/]");
                    break;
                }

                CardEffect? majority = group.Majority;
                AnsiConsole.WriteLine();
                // Plain ASCII: this console is cp1252, and Spectre's Rule (and a
                // real arrow below) come out as replacement characters there.
                AnsiConsole.MarkupLine("[grey]" + new string('-', 68) + "[/]");

                foreach (TagConsistencyAuditor.AuditedCard card in group.Cards)
                {
                    // The odd one out is what a slip looks like, so mark it rather
                    // than leaving the reader to count.
                    bool oddOne = majority != null && card.Effects != majority;
                    string colour = oddOne ? "yellow" : "grey";
                    AnsiConsole.MarkupLineInterpolated(
                        $"[{colour}]{(oddOne ? ">>" : "  ")} {Describe(card.Effects),-32}[/] {card.Name}");
                }

                AnsiConsole.MarkupLineInterpolated($"[grey]    {Snip(group.Cards[0].OracleText)}[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                "[grey]A lone card against a clear majority (>>) is usually a slip. A group split down the "
                + "middle is a boundary that has not been ruled on. Nothing here was changed.[/]");

            return 0;
        }

        private static string Describe(CardEffect effects) =>
            effects == CardEffect.None
                ? "(nothing)"
                : string.Join(",", Enum.GetValues<CardEffect>()
                    .Where(e => e != CardEffect.None && effects.HasFlag(e)));

        private static string Snip(string text)
        {
            string flat = text.Replace("\n", " / ");
            return flat.Length <= 150 ? flat : flat[..150] + "...";
        }
    }
}
