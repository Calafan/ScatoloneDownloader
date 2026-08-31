using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
    /// <summary>
    /// Local web tagger for card rating/status/effects. Serves a single-card,
    /// keyboard-driven page from an in-process <see cref="HttpListener"/> (no
    /// ASP.NET dependency), opens the browser, and autosaves each change to the
    /// git-tracked metadata directory's rating-tier files, keyed by
    /// <see cref="Card.OracleId"/> (see <see cref="CubeMetadataStore"/>). This
    /// tool is the authoring authority: rating/status/effects are loaded from
    /// there at startup (never from XMP — Adobe Bridge is optional, and XMP is
    /// read only once, by the <c>import</c> command, to seed the metadata).
    /// </summary>
    internal sealed class TagCommand : AsyncCommand<TagCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<SOURCE_DIR>")]
            [Description("Source folder containing the physical master files (.png).")]
            public string SourceDirectory { get; set; }

            [CommandOption("-m|--metadata")]
            [Description("Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). Defaults to ./metadata.")]
            public string MetadataDirectory { get; set; }

            [CommandOption("-p|--port")]
            [Description("Local port for the tagger web server. Default 8765.")]
            public int Port { get; set; } = 8765;
        }

        // In-memory state shared between request handlers. Guarded by SaveLock.
        private readonly object saveLock = new();
        private List<(Card Card, string Path)> matched = [];
        private CubeMetadata metadata = new();
        private string metadataDir = "metadata";
        private string[] effectNames = [];

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.SourceDirectory) || !Directory.Exists(settings.SourceDirectory))
            {
                AnsiConsole.MarkupLine($"[red]Error: source folder '{settings.SourceDirectory}' does not exist.[/]");
                return 1;
            }

            string masterDir = Path.GetFullPath(settings.SourceDirectory);
            metadataDir = string.IsNullOrWhiteSpace(settings.MetadataDirectory)
                ? Path.GetFullPath("metadata")
                : Path.GetFullPath(settings.MetadataDirectory);

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

                Dictionary<string, Card> cardsByName = new(StringComparer.OrdinalIgnoreCase);
                foreach (Card c in allCards)
                {
                    if (!cardsByName.ContainsKey(c.Name))
                    {
                        cardsByName.Add(c.Name, c);
                    }
                }

                foreach (string file in pngFiles)
                {
                    string cardName = CardNameNormalizer.Normalize(Path.GetFileNameWithoutExtension(file));
                    if (cardsByName.TryGetValue(cardName, out Card card))
                    {
                        matched.Add((card, file));
                    }
                }
            }

            AnsiConsole.MarkupLine($"[green]Matched {matched.Count} cards.[/]");
            if (matched.Count == 0) return 0;

            // Rating/status/effects are authored here and loaded straight from the
            // metadata directory's tier files — XMP is legacy input only, read
            // once by the `import` command.
            metadata = CubeMetadataStore.Load(metadataDir);
            MetadataJsonSynchronizer.SyncFromJson(matched.Select(m => m.Card), metadata);

            effectNames = EffectResolver.ToNames((CardEffect)~0).ToArray();

            int tagged = matched.Count(m => m.Card.Effects != CardEffect.None);
            AnsiConsole.MarkupLine($"[cyan]Already tagged:[/] {tagged} / {matched.Count}");

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

            Task serverLoop = Task.Run(() => ServeAsync(listener));

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
                TryWrite(ctx, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Html));
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
                    reviewed = !string.IsNullOrEmpty(m.Card.OracleId)
                        && metadata.Cards.TryGetValue(m.Card.OracleId, out CardMetadataEntry e)
                        && e.ReviewedAt != null,
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
                SaveRequest req = JsonSerializer.Deserialize<SaveRequest>(body, JsonOpts);

                bool ok = ApplySave(req);
                WriteJson(ctx, new { ok });
                return;
            }

            TryWrite(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
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
        private bool ApplySave(SaveRequest req)
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
                metadata.Cards.TryGetValue(card.OracleId, out CardMetadataEntry existing);

                metadata.Cards[card.OracleId] = new CardMetadataEntry
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
                CubeMetadataStore.Save(metadataDir, metadata);
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
            public string Status { get; set; }
            public string[] Effects { get; set; }
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

        // Single-page tagger UI. Self-contained, no external resources.
        private const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Scatolone Cube Tagger</title>
<style>
  :root { --bg:#12141a; --panel:#1c1f27; --accent:#4da3ff; --on:#2e7d32; --txt:#e6e6e6; --muted:#8b93a3; }
  * { box-sizing: border-box; }
  body { margin:0; background:var(--bg); color:var(--txt); font:15px/1.4 system-ui,Segoe UI,sans-serif; height:100vh; overflow:hidden; }
  #app { display:flex; height:100vh; }
  #imgwrap { flex:1; display:flex; align-items:center; justify-content:center; padding:16px; }
  #card { max-width:100%; max-height:94vh; border-radius:14px; box-shadow:0 8px 40px rgba(0,0,0,.6); }
  #panel { width:360px; background:var(--panel); padding:18px; display:flex; flex-direction:column; gap:12px; overflow-y:auto; }
  #progress { font-size:13px; color:var(--muted); }
  #name { font-size:18px; font-weight:600; }
  #meta { font-size:12px; color:var(--muted); }
  #effects { display:flex; flex-direction:column; gap:6px; }
  .eff { display:flex; align-items:center; gap:10px; padding:8px 10px; border:1px solid #2c313c; border-radius:8px; cursor:pointer; user-select:none; }
  .eff.active { background:var(--on); border-color:var(--on); }
  .key { display:inline-flex; align-items:center; justify-content:center; min-width:22px; height:22px; padding:0 5px; border-radius:5px; background:#0d0f14; color:var(--accent); font-weight:700; font-size:12px; }
  .eff.active .key { color:#fff; }
  #rating { display:flex; gap:6px; }
  .star { font-size:22px; line-height:1; color:#3a3f4b; cursor:pointer; user-select:none; }
  .star.active { color:#f2c94c; }
  #status { display:flex; gap:6px; }
  .stat { display:flex; align-items:center; gap:6px; padding:6px 10px; border:1px solid #2c313c; border-radius:8px; cursor:pointer; user-select:none; }
  .stat.active { background:#8a3b3b; border-color:#8a3b3b; }
  .stat.active .key { color:#fff; }
  #help { font-size:12px; color:var(--muted); border-top:1px solid #2c313c; padding-top:10px; }
  #filter { font-size:12px; color:var(--accent); }
  kbd { background:#0d0f14; border-radius:4px; padding:1px 5px; font-size:11px; }
  #saveErr { position:fixed; top:0; left:0; right:0; z-index:10; background:#8a1f1f; color:#fff; padding:10px 14px; font-weight:600; text-align:center; }
  #saveErr[hidden] { display:none; }
</style>
</head>
<body>
<div id="saveErr" hidden>&#9888; Save failed &mdash; edits are NOT being written to disk. Check the terminal, then retry the last change.</div>
<div id="app">
  <div id="imgwrap"><img id="card" alt="card"></div>
  <div id="panel">
    <div id="progress"></div>
    <div id="name"></div>
    <div id="meta"></div>
    <div id="filter"></div>
    <div id="rating"></div>
    <div id="status"></div>
    <div id="effects"></div>
    <div id="help">
      <div><kbd>hotkey</kbd> toggle effect</div>
      <div><kbd>0</kbd>-<kbd>5</kbd> set rating</div>
      <div><kbd>n</kbd>/<kbd>b</kbd>/<kbd>t</kbd>/<kbd>j</kbd> status: None / Banned / Token / Jolly</div>
      <div><kbd>&larr;</kbd>/<kbd>&rarr;</kbd> or <kbd>Enter</kbd> prev / next</div>
      <div><kbd>c</kbd> confirm reviewed (no change)</div>
      <div><kbd>f</kbd> filter untagged &nbsp; <kbd>Home</kbd> first</div>
    </div>
  </div>
</div>
<script>
// Effect hotkeys deliberately avoid digits (0-5 = rating) and n/b/t/j/c/f
// (status + confirm + filter), so every action has one dedicated key.
const EFFECT_KEYS = "qweryuiopasdghkl".split("");
const STATUS_ORDER = [
  { key: "n", name: "None" },
  { key: "b", name: "Banned" },
  { key: "t", name: "Token" },
  { key: "j", name: "Jolly" },
];
const STATUS_KEYS = Object.fromEntries(STATUS_ORDER.map(s => [s.key, s.name]));

let cards = [], effects = [], order = [], pos = 0, filterUntagged = false;

async function boot() {
  const r = await fetch("/api/cards");
  const data = await r.json();
  effects = data.effects;
  cards = data.cards.map(c => ({...c, effects: new Set(c.effects)}));
  buildOrder();
  render();
}

function buildOrder() {
  order = cards.map((c,i)=>i).filter(i => !filterUntagged || cards[i].effects.size === 0);
  if (order.length === 0) order = cards.map((c,i)=>i);
  if (pos >= order.length) pos = order.length - 1;
  if (pos < 0) pos = 0;
}

function cur() { return cards[order[pos]]; }

function render() {
  const c = cur();
  document.getElementById("card").src = "/img/" + c.index + "?t=" + c.index;
  const taggedTotal = cards.filter(x => x.effects.size > 0).length;
  const reviewedTotal = cards.filter(x => x.reviewed).length;
  document.getElementById("progress").textContent =
    `Card ${pos+1} / ${order.length}` + (filterUntagged ? " (untagged view)" : "") +
    ` — tagged ${taggedTotal}/${cards.length} · reviewed ${reviewedTotal}`;
  document.getElementById("name").textContent = c.name;
  document.getElementById("meta").textContent =
    `${c.rating ? "★".repeat(c.rating) : "unrated"}` +
    `${c.status && c.status !== "None" ? " · " + c.status : ""}` +
    ` · ${c.reviewed ? "reviewed ✓" : "NEW"}`;
  document.getElementById("filter").textContent = filterUntagged ? "Filter: untagged only (press f)" : "";

  renderRating(c);
  renderStatus(c);

  const box = document.getElementById("effects");
  box.innerHTML = "";
  effects.forEach((name, i) => {
    const active = c.effects.has(name);
    const div = document.createElement("div");
    div.className = "eff" + (active ? " active" : "");
    div.innerHTML = `<span class="key">${(EFFECT_KEYS[i]||"").toUpperCase()}</span><span>${name}</span>`;
    div.onclick = () => toggle(i);
    box.appendChild(div);
  });
  preload();
}

function renderRating(c) {
  const box = document.getElementById("rating");
  box.innerHTML = "";
  for (let r = 0; r <= 5; r++) {
    const span = document.createElement("span");
    span.className = "star" + (r > 0 && r <= c.rating ? " active" : "");
    span.textContent = r === 0 ? "0" : "★";
    span.title = r === 0 ? "Unrated (key 0)" : `Rating ${r} (key ${r})`;
    span.onclick = () => setRating(r);
    box.appendChild(span);
  }
}

function renderStatus(c) {
  const box = document.getElementById("status");
  box.innerHTML = "";
  STATUS_ORDER.forEach(s => {
    const div = document.createElement("div");
    div.className = "stat" + (c.status === s.name ? " active" : "");
    div.innerHTML = `<span class="key">${s.key.toUpperCase()}</span><span>${s.name}</span>`;
    div.onclick = () => setStatus(s.name);
    box.appendChild(div);
  });
}

function preload() {
  if (pos+1 < order.length) { const im = new Image(); im.src = "/img/" + cards[order[pos+1]].index; }
}

function toggle(i) {
  const name = effects[i];
  if (!name) return;
  const c = cur();
  if (c.effects.has(name)) c.effects.delete(name); else c.effects.add(name);
  c.reviewed = true; // a manual toggle is a manual review
  save(c);
  render();
}

// Sets the rating outright (0 clears it back to unrated) rather than toggling,
// since rating is single-valued, not a bitset like effects.
function setRating(r) {
  const c = cur();
  c.rating = r;
  c.reviewed = true; // a manual rating change is a manual review
  save(c);
  render();
}

// Status is mutually exclusive (None/Banned/Token/Jolly), so this always
// assigns the new value; pressing "n" is how you clear it back to None.
function setStatus(name) {
  const c = cur();
  c.status = name;
  c.reviewed = true; // a manual status change is a manual review
  save(c);
  render();
}

function showSaveError() { document.getElementById("saveErr").hidden = false; }
function clearSaveError() { document.getElementById("saveErr").hidden = true; }

async function save(c) {
  try {
    const r = await fetch("/api/save", {
      method:"POST", headers:{"Content-Type":"application/json"},
      body: JSON.stringify({ index: c.index, rating: c.rating, status: c.status, effects: [...c.effects] })
    });
    // fetch does NOT reject on HTTP 500, and the server can also answer 200
    // with {ok:false} (e.g. a card with no oracle_id). Treat both as failures
    // so a silent save error can never masquerade as a saved edit.
    let ok = r.ok;
    if (ok) { try { const j = await r.json(); ok = !j || j.ok !== false; } catch { ok = false; } }
    if (!ok) { c.reviewed = false; showSaveError(); render(); return; }
    clearSaveError();
  } catch (e) {
    // network/server unreachable: mark unsaved and surface it, don't swallow.
    c.reviewed = false; showSaveError(); render();
  }
}

function move(d) { pos = Math.max(0, Math.min(order.length-1, pos+d)); render(); }

document.addEventListener("keydown", e => {
  if (e.key === "ArrowRight" || e.key === "Enter" || e.key === " ") { e.preventDefault(); move(1); return; }
  if (e.key === "ArrowLeft") { e.preventDefault(); move(-1); return; }
  if (e.key === "Home") { pos = 0; render(); return; }
  if (e.key === "c") { const c = cur(); c.reviewed = true; save(c); render(); return; } // confirm, no change
  if (e.key === "f") { filterUntagged = !filterUntagged; pos = 0; buildOrder(); render(); return; }

  const key = e.key.toLowerCase();

  if (key >= "0" && key <= "5") { e.preventDefault(); setRating(Number(key)); return; }

  const statusName = STATUS_KEYS[key];
  if (statusName) { e.preventDefault(); setStatus(statusName); return; }

  const i = EFFECT_KEYS.indexOf(key);
  if (i >= 0 && i < effects.length) { e.preventDefault(); toggle(i); }
});

boot();
</script>
</body>
</html>
""";
    }
}
