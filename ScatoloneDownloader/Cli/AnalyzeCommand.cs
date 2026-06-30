using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	internal sealed class AnalyzeSettings : DownloadSettings
	{
		[CommandArgument(0, "<FILES>")]
		[Description("One or more list files to analyze (no images downloaded).")]
		public string[] Files { get; set; }
	}

	internal sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
	{
		protected override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
		{
			if (settings.Clear)
			{
				FolderCleaner.Clear();
			}

			await CardService.RunAnalyzeAsync(settings.Files, settings.Reprints, settings.Tokens, settings.Lands, settings.PrintOnly);

			return 0;
		}
	}
}
