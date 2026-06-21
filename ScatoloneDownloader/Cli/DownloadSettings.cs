using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	/// <summary>Options shared by every download/analyze subcommand.</summary>
	internal class DownloadSettings : CommandSettings
	{
		[CommandOption("-r|--reprints")]
		[Description("Include reprints (ignored by default).")]
		public bool Reprints { get; set; }

		[CommandOption("-t|--tokens")]
		[Description("Include tokens (ignored by default).")]
		public bool Tokens { get; set; }

		[CommandOption("-p|--print-only")]
		[Description("Only write the card list, without downloading images.")]
		public bool PrintOnly { get; set; }

		[CommandOption("-c|--clear")]
		[Description("Delete the existing output folders before starting (off by default).")]
		public bool Clear { get; set; }

		[CommandOption("-o|--output <DIR>")]
		[Description("Root folder for output (default: ./Output).")]
		public string Output { get; set; }
	}
}
