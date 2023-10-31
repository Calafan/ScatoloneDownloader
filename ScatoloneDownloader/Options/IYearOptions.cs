using System.Collections.Generic;

using CommandLine;

namespace ScatoloneDownloader.Options
{
	public interface IYearOptions
	{
		[Option('y', "years", HelpText = "Download all cards released in the specified years grouped by year and set.", SetName = "Year", Required = true)]
		public IEnumerable<int> Years { get; set; }
	}
}
