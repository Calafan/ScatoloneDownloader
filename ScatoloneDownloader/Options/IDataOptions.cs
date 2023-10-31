using CommandLine;
using System.Collections.Generic;

namespace ScatoloneDownloader.Options
{
	public interface IDataOptions
	{
		[Option('d', "data", HelpText = "Analyze the list of cards to get statistics. Included in files option.\nCreatures with multiple types counts only as creature.", SetName = "Statistics", Required = true)]
		public IEnumerable<string> FilesToAnalyze { get; set; }
	}
}
