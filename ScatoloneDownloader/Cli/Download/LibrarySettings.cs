using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Download
{
    /// <summary>Options of the subcommands that follow the library rules by default
    /// (<c>all</c> and <c>years</c>).</summary>
    internal class LibrarySettings : DownloadSettings
    {
        [CommandOption("--no-rules")]
        [Description("Ignore the library rules (one printing per card; no funny, Vanguard, planar or never-legal cards) and download every valid printing, as before.")]
        public bool NoRules { get; set; }
    }
}
