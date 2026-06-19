using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	internal sealed class YearsSettings : DownloadSettings
	{
		[CommandArgument(0, "<YEARS>")]
		[Description("One or more release years to download.")]
		public int[] Years { get; set; }

		public override ValidationResult Validate()
		{
			if (Years == null || Years.Length == 0)
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
			FolderCleaner.ClearWithPrompt();

			await new CardService().RunYearsAsync(settings.Years, settings.Reprints, settings.Tokens, settings.PrintOnly);

			return 0;
		}
	}
}
