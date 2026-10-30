using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Download
{
    /// <summary>Options shared by the card-download subcommands.</summary>
    internal class DownloadSettings : CommonSettings
    {
        [CommandOption("-l|--lands")]
        [Description("Include basic lands (excluded by default).")]
        public bool Lands { get; set; }
    }

    /// <summary>Options of the subcommands that select printings (<c>all</c>, <c>set</c>
    /// and <c>years</c>); <c>files</c> downloads exactly the cards it lists.</summary>
    internal class PrintingSettings : DownloadSettings
    {
        [CommandOption("-r|--reprints")]
        [Description("Include reprints (ignored by default).")]
        public bool Reprints { get; set; }

        [CommandOption("-t|--tokens")]
        [Description("Include tokens (ignored by default).")]
        public bool Tokens { get; set; }
    }
}
