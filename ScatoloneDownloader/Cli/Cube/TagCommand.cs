using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Local web tagger for card rating/status/effects. Serves a single-card,
    /// keyboard-driven page from an in-process <see cref="HttpListener"/> (no
    /// ASP.NET dependency), opens the browser, and autosaves each change to the
    /// git-tracked metadata directory's rating-tier files, keyed by
    /// <see cref="Card.OracleId"/> (see <see cref="CubeMetadataStore"/>). This
    /// tool is the authoring authority: rating/status/effects are loaded from
    /// there at startup. New cards reach the store through the <c>sync</c>
    /// command, which must not run while this tool is open.
    /// The page opens on the "to review" filter — see <see cref="Card.Reviewed"/>.
    /// </summary>
    internal sealed class TagCommand : AsyncCommand<TagCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            internal override bool NeedsSource => true;

            [CommandOption("-p|--port")]
            [Description("Local port for the tagger web server. Default 8765.")]
            public int Port { get; set; } = 8765;

            [CommandOption("--host")]
            [Description(
                "Extra hostname the tagger also answers to, besides localhost — e.g. a Tailscale "
                + "MagicDNS name, so the page can be opened from a phone. Repeatable. Needs a "
                + "one-time `netsh http add urlacl` reservation, which the startup error prints.")]
            public string[] ExtraHosts { get; set; } = [];
        }

        // In-memory state shared between request handlers. Guarded by SaveLock.
        private readonly object saveLock = new();
        private List<(Card Card, string Path)> matched = [];
        private CubeMetadata metadata = new();
        private string metadataDir = "metadata";
        private string masterDir = string.Empty;
        private string[] effectNames = [];

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            masterDir = settings.ResolveSourceDirectory();
            metadataDir = settings.ResolveDirectory();

            List<(Card Card, string FilePath)>? scanned = await CardImageMatcher.ScanAndMatchAsync(masterDir);
            if (scanned is null)
            {
                return 0;
            }

            matched = scanned;

            AnsiConsole.MarkupLine($"[green]Matched {matched.Count} cards.[/]");
            if (matched.Count == 0) return 0;

            // Rating/status/effects are authored here and loaded straight from the
            // metadata directory's tier files, never from the image files.
            metadata = CubeMetadataStore.Load(metadataDir);
            MetadataJsonSynchronizer.SyncFromJson(matched.Select(m => m.Card), metadata);

            effectNames = EffectResolver.ToNames((CardEffect)~0).ToArray();

            // Guard the effect-to-hotkey mapping: the page assigns EFFECT_KEYS[i]
            // to effect i, so a new CardEffect beyond the available keys would get
            // no hotkey and could only be toggled by mouse. Fail loudly at startup
            // rather than shipping a silently un-keyable effect.
            if (effectNames.Length > EffectHotkeys.Length)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error: {effectNames.Length} effects but only {EffectHotkeys.Length} hotkeys in TagCommand.EffectHotkeys.[/]");
                AnsiConsole.MarkupLine(
                    "[yellow]Add more keys to EffectHotkeys (6-9 and most punctuation are free; avoid 0-5, n/b/t/j/c/f and . , /).[/]");
                return 1;
            }

            int tagged = matched.Count(m => m.Card.Effects != CardEffect.None);
            // The review queue is every card no human has confirmed: untagged AND
            // auto-tagged together, because classify writes effects only when it has
            // a suggestion, so a card it read and found nothing for stays effect-less.
            // The page's default filter inverts the same Reviewed flag.
            int pending = matched.Count(m => !m.Card.Reviewed);
            AnsiConsole.MarkupLine($"[cyan]Already tagged:[/] {tagged} / {matched.Count}");
            AnsiConsole.MarkupLine(
                $"[cyan]To review:[/] {pending} / {matched.Count} [grey](untagged + auto-tagged — the tagger's default filter)[/]");

            string[] prefixes;
            try
            {
                prefixes = BuildPrefixes(settings.Port, settings.ExtraHosts);
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                return 1;
            }

            using HttpListener listener = new();
            string url = prefixes[0];
            foreach (string prefix in prefixes)
            {
                listener.Prefixes.Add(prefix);
            }

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                AnsiConsole.MarkupLine($"[red]Could not start web server on {url}: {ex.Message}[/]");

                if (prefixes.Length > 1)
                {
                    // A named host needs a one-time reservation — and so does localhost
                    // once the port has one, because reserving any URL on a port revokes
                    // the implicit grant that lets any user register
                    // http://localhost:<port>/ there. Reserving only the named hosts
                    // therefore BREAKS the prefix that already worked, which is why the
                    // whole set is printed rather than just the new ones.
                    AnsiConsole.MarkupLine(
                        "[yellow]These prefixes need a one-time reservation. In an ADMIN terminal, ALL of them:[/]");
                    foreach (string prefix in prefixes)
                    {
                        AnsiConsole.MarkupLine(
                            $"[grey]  netsh http add urlacl url={prefix} user={Environment.UserDomainName}\\{Environment.UserName}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Try a different port with -p, e.g. `tag <dir> -p 8790`.[/]");
                }

                return 1;
            }

            Task serverLoop = Task.Run(() => ServeAsync(listener), cancellationToken);

            TryOpenBrowser(url);

            AnsiConsole.MarkupLine($"[green]Tagger running at[/] [underline]{url}[/]");
            foreach (string prefix in prefixes.Skip(1))
            {
                AnsiConsole.MarkupLine($"[green]Also answering to[/] [underline]{prefix}[/]");
            }

            AnsiConsole.MarkupLine($"[grey]Autosaving to {metadataDir}. Press ENTER here to stop.[/]");

            await Task.Run(() => Console.ReadLine());

            listener.Stop();
            await serverLoop;

            AnsiConsole.MarkupLine("[green]Tagger stopped.[/]");
            return 0;
        }

        private async Task ServeAsync(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch
                {
                    break; // listener stopped
                }

                try
                {
                    Route(ctx);
                }
                catch (Exception ex)
                {
                    TryWrite(ctx, 500, "text/plain", Encoding.UTF8.GetBytes(ex.Message));
                }
            }
        }

        private void Route(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url?.AbsolutePath ?? "/";

            if (path == "/")
            {
                TryWrite(ctx, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(GetPageHtml()));
                return;
            }

            if (path == "/stats")
            {
                TryWrite(ctx, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(GetStatsPageHtml()));
                return;
            }

            if (path == "/api/stats")
            {
                // Recomputed per request from the live cards: requests are served one
                // at a time, so no save can land halfway through the tally.
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
                    StatsPayload.Build(matched.Select(m => m.Card)), StatsJsonOpts);
                TryWrite(ctx, 200, "application/json; charset=utf-8", bytes);
                return;
            }

            if (path == "/api/cards")
            {
                var dto = matched.Select((m, i) => new
                {
                    index = i,
                    oracleId = m.Card.OracleId ?? string.Empty,
                    name = m.Card.Name,
                    rating = m.Card.Rating,
                    status = m.Card.Status.ToString(),
                    effects = EffectResolver.ToNames(m.Card.Effects),
                    // The coarse type, for the page's type filter. Sent as the
                    // enum NAME rather than its int so the page reads as the
                    // ontology does and a reordered enum cannot silently
                    // re-point the filter at a different bucket.
                    macro = m.Card.MacroType.ToString(),
                    // Colour and cost are sent as display names ("2 Azorius",
                    // and the cost bucket), which the page filters on directly.
                    color = ColorCategoryClassifier.ViewFolderName(m.Card.ColorCategory),
                    // The stats page's two colour views: the one-column split the
                    // report uses ("Multi" for every gold card) and the raw identity
                    // letters ("BG", "" for colourless) for "any card with this colour".
                    column = CardAnalyzer.EffectColumn(m.Card.ColorCategory),
                    identity = string.Concat(m.Card.ColorIdentity),
                    cost = CostBucket.Of(m.Card.Cmc),
                    turns = ReadingTurns(m.Card),
                    reviewed = m.Card.Reviewed,
                    folder = RelativeFolder(masterDir, m.Path),
                });
                // The glossary rides alongside the names, same order, so the
                // page can index it without knowing anything about the enum.
                WriteJson(ctx, new
                {
                    effects = effectNames,
                    glossary = EffectGlossary.DescribeAll(effectNames),
                    cards = dto,
                });
                return;
            }

            if (path.StartsWith("/img/", StringComparison.Ordinal))
            {
                if (int.TryParse(path.AsSpan("/img/".Length), out int idx)
                    && idx >= 0 && idx < matched.Count
                    && File.Exists(matched[idx].Path))
                {
                    TryWrite(ctx, 200, "image/png", File.ReadAllBytes(matched[idx].Path));
                }
                else
                {
                    TryWrite(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
                }
                return;
            }

            if (path == "/api/save" && ctx.Request.HttpMethod == "POST")
            {
                using StreamReader reader = new(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                string body = reader.ReadToEnd();
                SaveRequest? req = JsonSerializer.Deserialize<SaveRequest>(body, JsonOpts);

                bool ok = ApplySave(req);
                WriteJson(ctx, new { ok });
                return;
            }

            TryWrite(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
        }

        // Anything that would make a --host value something other than a bare name.
        // A scheme, port or path in there builds a prefix that parses but silently
        // never matches, which is the worst failure to debug: the tagger starts
        // clean and the phone still gets a 400.
        private static readonly SearchValues<char> InvalidHostChars = SearchValues.Create(['/', ':', '?', '#', '@', '\\', ' ', '\t']);

        /// <summary>
        /// The listener prefixes for this run: always localhost, plus one per
        /// <c>--host</c>. http.sys routes on the request's HOST HEADER, not on the
        /// socket the request arrived through, so a reverse proxy that forwards to
        /// localhost while preserving the original Host — which is exactly what
        /// <c>tailscale serve</c> does — is answered "400 Invalid Hostname" by the
        /// kernel, before the request ever reaches this code. Naming that host here
        /// is what lets it through.
        ///
        /// Only the NAME is matched: http.sys ignores the port inside the Host
        /// header and keys off the port the connection actually arrived on, so a
        /// proxy published on a DIFFERENT tailnet port still lands here and needs no
        /// prefix of its own. Both behaviours measured against http.sys, not assumed.
        ///
        /// localhost stays first and unconditional. Windows normally lets any user
        /// register it with no reservation, but that grant is PER PORT and is revoked
        /// the moment the port carries any explicit reservation — so reserving a
        /// --host silently breaks localhost on the same port unless it is reserved
        /// too. The startup error prints the whole set for exactly that reason.
        /// </summary>
        /// <exception cref="ArgumentException">A host carries a scheme, port, path or
        /// whitespace.</exception>
        internal static string[] BuildPrefixes(int port, IEnumerable<string>? extraHosts)
        {
            List<string> prefixes = [$"http://localhost:{port}/"];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { "localhost" };

            foreach (string raw in extraHosts ?? [])
            {
                string host = raw.Trim();

                if (host.Length == 0)
                {
                    continue;
                }

                if (host.AsSpan().IndexOfAny(InvalidHostChars) >= 0)
                {
                    throw new ArgumentException(
                        $"--host '{raw}' must be a bare hostname — no scheme, port, path or spaces "
                        + "(e.g. --host cala.tail6de9de.ts.net).");
                }

                // A repeated --host, or one that just re-states localhost, is a
                // duplicate prefix, and HttpListener throws on those.
                if (seen.Add(host))
                {
                    prefixes.Add($"http://{host}:{port}/");
                }
            }

            return [.. prefixes];
        }

        /// <summary>The card's folder relative to the master library, with forward
        /// slashes ("2000/Invasion"), or "" for a file sitting in the root. This is
        /// what the page's folder filter groups on — the library's own
        /// folder layout on disk. Derived from the path rather than from the card,
        /// because the folder is a property of how the library is laid out on disk,
        /// not of the printing Scryfall matched.</summary>
        internal static string RelativeFolder(string masterDir, string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(masterDir))
            {
                return string.Empty;
            }

            string relative = Path.GetRelativePath(masterDir, dir);

            return relative == "." ? string.Empty : relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        /// <summary>
        /// The orientations the page shows this card in, as clockwise turns in
        /// degrees: the first is how it opens, and clicking the card cycles
        /// through the rest. Only cards whose master is drawn sideways get more
        /// than one.
        /// <list type="bullet">
        /// <item><description>A double-faced master holds both faces turned 90°
        /// anticlockwise and stacked (<see cref="Imaging.CardImageComposer"/>),
        /// so a quarter turn clockwise stands them both up, front on the left.
        /// </description></item>
        /// <item><description>A Siege is the exception: the composer flips the
        /// whole master, so the battle face — itself printed sideways — already
        /// reads straight, and the back face needs the turn the other way.
        /// </description></item>
        /// <item><description>A split card's Scryfall image has both halves
        /// sideways the same way a double's faces are. Aftermath is left alone:
        /// its top half reads straight, and that is the half cast first.
        /// </description></item>
        /// </list>
        /// </summary>
        internal static int[] ReadingTurns(Card card)
        {
            if (card is DoubleFaceCard doubleFace)
            {
                return doubleFace.IsSiege ? [0, 270] : [90, 0];
            }

            if (card.Layout == "split" && !card.Keywords.Contains("Aftermath"))
            {
                return [90, 0];
            }

            return [0];
        }

        /// <summary>
        /// Handles one <c>POST /api/save</c>: applies rating/status/effects to the
        /// in-memory <see cref="Card"/> and immediately persists the whole
        /// <see cref="metadata"/> document (every keystroke autosaves — there is no
        /// separate "commit" action in the UI, so a browser crash never loses more
        /// than the single field just changed). Returns <c>false</c> without
        /// writing when the request is malformed or the card has no
        /// <see cref="Card.OracleId"/> to key the entry by.
        /// </summary>
        private bool ApplySave(SaveRequest? req)
        {
            if (req == null || req.Index < 0 || req.Index >= matched.Count)
            {
                return false;
            }

            Card card = matched[req.Index].Card;
            if (string.IsNullOrEmpty(card.OracleId))
            {
                return false; // cannot key an entry without an oracle_id
            }

            CardEffect flags = EffectResolver.Parse(req.Effects ?? []);
            CardStatus status = StatusResolver.Parse(req.Status);
            int rating = Math.Clamp(req.Rating, 0, 5);

            card.Rating = rating;
            card.Status = status;
            card.Effects = flags;
            // Saved means a human looked at it; the live /stats page counts Reviewed,
            // so it must not wait for the next reload to notice.
            card.Reviewed = true;

            lock (saveLock)
            {
                metadata.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? existing);
                int? previousRating = existing?.Rating;

                CardMetadataEntry updated = new()
                {
                    Name = card.Name,
                    Rating = rating,
                    ScryfallId = card.Id,
                    Status = StatusResolver.ToName(status),
                    Effects = EffectResolver.ToNames(flags),
                    // A human saved this card: record the manual-review instant.
                    ReviewedAt = DateTimeOffset.UtcNow,
                };
                metadata.Cards[card.OracleId] = updated;

                // Persist just this card: rewrites only the tier file(s) it belongs
                // in, and reloads them from disk first, so a huge unrated backlog is
                // not re-serialized per keystroke and a concurrent external edit to
                // other entries is not clobbered by this in-memory snapshot.
                CubeMetadataStore.SaveEntry(metadataDir, card.OracleId, updated, previousRating);

                // Record what the reviewer was shown against what they left. On a
                // first pass "before" is the classifier's proposal, which is the
                // only moment it can be captured — SaveEntry has just overwritten
                // it. See ReviewLog for why this is a side file.
                IReadOnlyList<string> before = existing?.Effects ?? [];
                IReadOnlyList<string> after = updated.Effects ?? [];
                ReviewLog.Append(metadataDir, new ReviewLog.Entry
                {
                    At = updated.ReviewedAt ?? DateTimeOffset.UtcNow,
                    OracleId = card.OracleId,
                    Name = card.Name ?? string.Empty,
                    Before = before,
                    After = after,
                    FirstReview = existing?.ReviewedAt == null,
                    Changed = !before.OrderBy(e => e, StringComparer.Ordinal)
                        .SequenceEqual(after.OrderBy(e => e, StringComparer.Ordinal), StringComparer.Ordinal),
                });
            }

            return true;
        }

        /// <summary>Wire shape of a <c>POST /api/save</c> body: the full evaluation
        /// state for one card (index into <see cref="matched"/>, rating, status
        /// name, effect names). The client always sends the complete state, never
        /// a delta, so <see cref="ApplySave"/> can just overwrite the entry.</summary>
        private sealed class SaveRequest
        {
            public int Index { get; set; }
            public int Rating { get; set; }
            public string? Status { get; set; }
            public string[]? Effects { get; set; }
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        // The stats payload is camelCase (its records are PascalCase); kept apart from
        // JsonOpts because /api/cards and /api/save use anonymous lower-case objects.
        // Dictionary keys (W, Multi, 1 White) are left exactly as they are.
        private static readonly JsonSerializerOptions StatsJsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private static void WriteJson(HttpListenerContext ctx, object payload)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            TryWrite(ctx, 200, "application/json; charset=utf-8", bytes);
        }

        private static void TryWrite(HttpListenerContext ctx, int status, string contentType, byte[] body)
        {
            try
            {
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = contentType;
                ctx.Response.ContentLength64 = body.Length;
                ctx.Response.OutputStream.Write(body, 0, body.Length);
            }
            catch
            {
                // client went away; nothing to do
            }
            finally
            {
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }

        private static void TryOpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                // headless / no default browser: user opens the URL manually
            }
        }

        // Effect hotkey string injected into the tagger page (TaggerPage.html's
        // "__EFFECT_KEYS__" placeholder). Kept here as the SINGLE source of truth
        // so the startup check above can guarantee there are at least as many keys
        // as effects — a new CardEffect with no key would otherwise silently get no
        // hotkey. Indexed by ENUM POSITION: the Nth character is the Nth member of
        // CardEffect, which is why adding a member in the middle would shift every
        // key after it. Append, don't insert.
        //
        // Keys are chosen initial-first, most common effect first, measured against
        // the real library: Discard, Filter, ManaFixing, Sacrifice, Ramp, Pacify,
        // Wipe and LandDestruction all get theirs. Eight is the CEILING, not a
        // compromise — the 24 effects share only 11 distinct initials, and three of
        // those (b, c, t) are reserved for status and confirm, which is what blocks
        // Buff, Burn, Bounce, CardAdvantage, Counter, Tokens and Tutor. Freeing b
        // and t by moving status elsewhere would raise it to ten.
        //
        // Where the initial is taken, the key is another letter from the name
        // (bUff, tOkens, cArdAdvantage, remoVal); the three rarest effects take the
        // leftovers. Filter selection is mouse-only, so no letter is spent on it.
        // Cheat (added 2026-09-18) gets "7": every letter in its name is gone —
        // h, e and a are spent, and c and t are the reserved ones — so it takes
        // a leftover, which is exactly what the paragraph above predicted would
        // happen to the next effect added.
        internal const string EffectHotkeys = "ovy9wgrxdafkuiesz8mplqh-7";

        // The tagger's single-page UI lives in the embedded resource
        // Cli/TaggerPage.html (so editors/linters see the HTML/JS). It is loaded and
        // key-substituted once, on the first "/" request.
        private static string? pageHtml;

        internal static string GetPageHtml()
        {
            if (pageHtml == null)
            {
                const string resourceName = "ScatoloneDownloader.Cli.TaggerPage.html";
                using Stream stream = typeof(TagCommand).Assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded tagger page '{resourceName}' not found.");
                using StreamReader reader = new(stream);
                pageHtml = reader.ReadToEnd().Replace("__EFFECT_KEYS__", EffectHotkeys);
            }

            return pageHtml;
        }

        // The live /stats page (Cli/StatsPage.html), loaded and cached like the tagger page.
        private static string? statsPageHtml;

        internal static string GetStatsPageHtml()
        {
            if (statsPageHtml == null)
            {
                const string resourceName = "ScatoloneDownloader.Cli.StatsPage.html";
                using Stream stream = typeof(TagCommand).Assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded stats page '{resourceName}' not found.");
                using StreamReader reader = new(stream);
                statsPageHtml = reader.ReadToEnd();
            }

            return statsPageHtml;
        }
    }
}
