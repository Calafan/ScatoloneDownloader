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
    /// there at startup (never from XMP — Adobe Bridge is optional, and XMP is
    /// read only by the <c>import</c> command, which folds Bridge sessions back in).
    /// The page opens on the "to review" filter — see <see cref="IsPendingReview"/>.
    /// </summary>
    internal sealed class TagCommand : AsyncCommand<TagCommand.Settings>
    {
        public sealed class Settings : MetadataSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png).")]
            public string SourceDirectory { get; set; } = string.Empty;

            internal override string MasterDirectory => SourceDirectory;

            [CommandOption("-p|--port")]
            [Description("Local port for the tagger web server. Default 8765.")]
            public int Port { get; set; } = 8765;
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
            if (string.IsNullOrWhiteSpace(settings.SourceDirectory) || !Directory.Exists(settings.SourceDirectory))
            {
                AnsiConsole.MarkupLine($"[red]Error: source folder '{settings.SourceDirectory}' does not exist.[/]");
                return 1;
            }

            masterDir = Path.GetFullPath(settings.SourceDirectory);
            metadataDir = settings.ResolveDirectory();

            string[] pngFiles = Directory.GetFiles(masterDir, "*.png", SearchOption.AllDirectories);
            if (pngFiles.Length == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No .png files found in '{masterDir}'.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]Found {pngFiles.Length} files. Loading bulk data from Scryfall...[/]");

            using (GetManager manager = new())
            {
                List<Card> allCards = await manager.GetDefaultCards();
                matched = CardImageMatcher.Match(allCards, pngFiles, warnUnmatched: true);
            }

            AnsiConsole.MarkupLine($"[green]Matched {matched.Count} cards.[/]");
            if (matched.Count == 0) return 0;

            // Rating/status/effects are authored here and loaded straight from the
            // metadata directory's tier files — XMP is legacy input only, read
            // once by the `import` command.
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
            int pending = matched.Count(m => IsPendingReview(m.Card));
            AnsiConsole.MarkupLine($"[cyan]Already tagged:[/] {tagged} / {matched.Count}");
            AnsiConsole.MarkupLine(
                $"[cyan]To review:[/] {pending} / {matched.Count} [grey](untagged + auto-tagged — the tagger's default filter)[/]");

            using HttpListener listener = new();
            string url = $"http://localhost:{settings.Port}/";
            listener.Prefixes.Add(url);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                AnsiConsole.MarkupLine($"[red]Could not start web server on {url}: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[yellow]Try a different port with -p, e.g. `tag <dir> -p 8790`.[/]");
                return 1;
            }

            Task serverLoop = Task.Run(() => ServeAsync(listener), cancellationToken);

            TryOpenBrowser(url);

            AnsiConsole.MarkupLine($"[green]Tagger running at[/] [underline]{url}[/]");
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
                    reviewed = !IsPendingReview(m.Card),
                    folder = RelativeFolder(masterDir, m.Path),
                });
                WriteJson(ctx, new { effects = effectNames, cards = dto });
                return;
            }

            if (path.StartsWith("/img/", StringComparison.Ordinal))
            {
                if (int.TryParse(path.Substring("/img/".Length), out int idx)
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

        /// <summary>
        /// True when no human has confirmed this card yet — its metadata entry is
        /// missing, or present with no <c>reviewedAt</c>. This single predicate
        /// backs both the startup "To review" count and the page's <c>reviewed</c>
        /// flag, which the tagger's DEFAULT filter inverts: the review queue is
        /// untagged cards AND auto-tagged ones together, because <c>classify</c>
        /// writes effects only when it has a suggestion — a card it read and found
        /// nothing for stays effect-less, so it is invisible in an auto-only view
        /// while an untagged-only view hides every auto suggestion.
        /// </summary>
        /// <summary>The card's folder relative to the master library, with forward
        /// slashes ("2000/Invasion"), or "" for a file sitting in the root. This is
        /// what the page's folder filter groups on — the equivalent of picking a
        /// folder in Adobe Bridge. Derived from the path rather than from the card,
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

        private bool IsPendingReview(Card card)
            => string.IsNullOrEmpty(card.OracleId)
                || !metadata.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? entry)
                || entry.ReviewedAt == null;

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

            lock (saveLock)
            {
                // Label is legacy XMP-mirror data this tool no longer authors;
                // preserve whatever the `import` seed (or a prior save) left there.
                metadata.Cards.TryGetValue(card.OracleId, out CardMetadataEntry? existing);
                int? previousRating = existing?.Rating;

                CardMetadataEntry updated = new()
                {
                    Name = card.Name,
                    Rating = rating,
                    Label = existing?.Label ?? string.Empty,
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
        // so the startup check below can guarantee there are at least as many keys
        // as effects — a new CardEffect with no key would otherwise silently get no
        // hotkey. Keys avoid 0-5 (rating) and n/b/t/j/c/f (status/confirm/filter);
        // four page actions had to take punctuation because no letter was left:
        // "." (shuffle), "," (rating filter), "/" (card list), and "-" now carries
        // the 21st effect.
        //
        // The alphabet is full, but the keyboard is not: the rating branch only
        // claims 0-5, so 6-9 are free, as are several punctuation keys. Prefer
        // ones that need no modifier on an Italian layout, which is what ruled
        // out ";" (Shift+",") in favour of "-" (bottom row, beside ".").
        internal const string EffectHotkeys = "qweryuiopasdghklmvxz-";

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
    }
}
