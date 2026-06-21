using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
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

			await CardService.RunSetsAsync(settings.Sets, settings.Reprints, settings.Tokens, settings.PrintOnly);

			return 0;
		}
	}
}
