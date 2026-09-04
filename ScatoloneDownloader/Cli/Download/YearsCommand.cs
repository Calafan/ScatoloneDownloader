using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Download
{
    internal sealed class YearsSettings : DownloadSettings
    {
        [CommandArgument(0, "<YEARS>")]
        [Description("One or more release years to download.")]
        public int[] Years { get; set; } = [];

        public override ValidationResult Validate()
        {
            if (Years.Length == 0)
            {
                return ValidationResult.Error("At least one year is required.");
            }

            if (!Years.Any(year => year >= CardService.MinYear && year <= CardService.MaxYear))
            {
                return ValidationResult.Error($"No year in the supported range {CardService.MinYear}-{CardService.MaxYear}.");
            }

            return ValidationResult.Success();
        }
    }

    internal sealed class YearsCommand : AsyncCommand<YearsSettings>
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, YearsSettings settings, CancellationToken cancellationToken)
        {
            if (settings.Clear)
            {
                FolderCleaner.Clear();
            }

            await CardService.RunYearsAsync(settings.Years, settings.Reprints, settings.Tokens, settings.Lands, settings.PrintOnly);

            return 0;
        }
    }
}
