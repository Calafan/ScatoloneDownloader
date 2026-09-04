using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Download
{
    internal sealed class SetSettings : DownloadSettings
    {
        [CommandArgument(0, "<SETS>")]
        [Description("One or more set codes to download.")]
        public string[] Sets { get; set; }
    }

    internal sealed class SetCommand : AsyncCommand<SetSettings>
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, SetSettings settings, CancellationToken cancellationToken)
        {
            if (settings.Clear)
            {
                FolderCleaner.Clear();
            }

            await CardService.RunSetsAsync(settings.Sets, settings.Reprints, settings.Tokens, settings.Lands, settings.PrintOnly);

            return 0;
        }
    }
}
