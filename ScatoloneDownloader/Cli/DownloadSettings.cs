using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	/// <summary>Options shared by the card-download/analyze subcommands.</summary>
	internal class DownloadSettings : CommonSettings
	{
		[CommandOption("-r|--reprints")]
		[Description("Include reprints (ignored by default).")]
		public bool Reprints { get; set; }

		[CommandOption("-t|--tokens")]
		[Description("Include tokens (ignored by default).")]
		public bool Tokens { get; set; }

		[CommandOption("-l|--lands")]
		[Description("Include basic lands (excluded by default).")]
		public bool Lands { get; set; }
	}
}
