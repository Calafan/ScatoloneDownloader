using System.ComponentModel;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	/// <summary>Options shared by every subcommand, including <c>lands</c>.</summary>
	internal class CommonSettings : CommandSettings
	{
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
