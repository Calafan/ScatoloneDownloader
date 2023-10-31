using System.Collections.Generic;

using CommandLine;

namespace ScatoloneDownloader.Options
{
	public class CommandLineOptions : IAllOptions, IFileOptions, ISetOptions, IYearOptions, IDataOptions
	{
		public bool All { get; set; }

		public string ExcludeFile { get; set; }

		public IEnumerable<string> Files { get; set; }

		public IEnumerable<string> Sets { get; set; }

		public IEnumerable<int> Years { get; set; }

		public IEnumerable<string> FilesToAnalyze { get; set; }

		[Option('r', "reprints", HelpText = "If specified reprints are not ignored.")]
		public bool Reprints { get; set; }

		[Option('t', "tokens", HelpText = "If specified tokens are not ignored.")]
		public bool Tokens { get; set; }

		[Option('l', "lands", HelpText = "Add lands to download lists.")]
		public bool Lands { get; set; }

        [Option('p', "printOnly", HelpText = "Print only the selected list.")]
        public bool PrintOnly { get; set; }
	}
}
