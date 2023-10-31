using System.Collections.Generic;

using CommandLine;

namespace ScatoloneDownloader.Options
{
	public interface ISetOptions
	{
		[Option('s', "sets", HelpText = "Download all specified sets cards grouped by set.", SetName = "Set", Required = true)]
		public IEnumerable<string> Sets { get; set; }
	}
}
