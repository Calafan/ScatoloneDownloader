using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	internal sealed class AllSettings : DownloadSettings
	{
		[CommandOption("-e|--exclude <FILE>")]
		[Description("Exclude the cards listed in the given file.")]
		public string ExcludeFile { get; set; }

		public override ValidationResult Validate()
		{
			if (!string.IsNullOrEmpty(ExcludeFile) && !File.Exists(ExcludeFile))
			{
				return ValidationResult.Error("File not found: " + ExcludeFile);
			}

			return ValidationResult.Success();
		}
	}

	internal sealed class AllCommand : AsyncCommand<AllSettings>
	{
		protected override async Task<int> ExecuteAsync(CommandContext context, AllSettings settings, CancellationToken cancellationToken)
		{
			if (settings.Clear)
			{
				FolderCleaner.Clear();
			}

			await new CardService().RunAllAsync(settings.ExcludeFile, settings.Reprints, settings.Tokens, settings.PrintOnly);

			return 0;
		}
	}
}
