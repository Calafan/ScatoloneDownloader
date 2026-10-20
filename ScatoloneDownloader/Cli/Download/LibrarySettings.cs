using System.ComponentModel;

using Spectre.Console;
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

        [CommandOption("--library <DIR>")]
        [Description("Write straight into a library laid out as <DIR>\\<year>\\<set>\\ (no mode sub-folder), e.g. ScatoloneQuintet\\Source; images already there are skipped.")]
        public string? Library { get; set; }

        public override ValidationResult Validate()
        {
            if (!string.IsNullOrWhiteSpace(Library) && !Directory.Exists(Library))
            {
                return ValidationResult.Error("Library folder not found: " + Library);
            }

            return ValidationResult.Success();
        }
    }
}
