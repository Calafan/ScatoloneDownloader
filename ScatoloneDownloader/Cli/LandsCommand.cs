using System.Threading;
using System.Threading.Tasks;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	internal sealed class LandsSettings : CommonSettings
	{
	}

	internal sealed class LandsCommand : AsyncCommand<LandsSettings>
	{
		protected override async Task<int> ExecuteAsync(CommandContext context, LandsSettings settings, CancellationToken cancellationToken)
		{
			if (settings.Clear)
			{
				FolderCleaner.Clear();
			}

			await CardService.RunLandsAsync(settings.PrintOnly);

			return 0;
		}
	}
}
