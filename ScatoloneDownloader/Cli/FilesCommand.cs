using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	internal sealed class FilesSettings : DownloadSettings
	{
		[CommandArgument(0, "<FILES>")]
		[Description("One or more list files to download from.")]
		public string[] Files { get; set; }

		[CommandOption("-l|--lands")]
		[Description("Add basic lands to the download list.")]
		public bool Lands { get; set; }
	}

	internal sealed class FilesCommand : AsyncCommand<FilesSettings>
	{
		protected override async Task<int> ExecuteAsync(CommandContext context, FilesSettings settings, CancellationToken cancellationToken)
		{
			if (settings.Clear)
			{
				FolderCleaner.Clear();
			}

			await CardService.RunFilesAsync(settings.Files, settings.Reprints, settings.Tokens, settings.Lands, settings.PrintOnly);

			return 0;
		}
	}
}
