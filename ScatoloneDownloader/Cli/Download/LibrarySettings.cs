using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Download
{
    /// <summary>Options of the subcommands that follow the library rules by default
    /// (<c>all</c> and <c>years</c>).</summary>
    internal class LibrarySettings : PrintingSettings
    {
        [CommandOption("--no-rules")]
        [Description("Ignore the library rules (one printing per card; no funny, Vanguard, planar, never-legal or Alchemy cards; no creature tokens; nothing unreleased) and download every valid printing instead: `all` reads the Unique Artwork bulk, `years` the per-set search, and a card with no valid printing still falls back to a plain-looking one or the earliest variant.")]
        public bool NoRules { get; set; }
    }
}
