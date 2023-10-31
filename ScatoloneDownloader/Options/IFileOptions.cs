using System.Collections.Generic;

using CommandLine;

namespace ScatoloneDownloader.Options
{
	public interface IFileOptions
	{
		[Option('f', "files", HelpText = "Download all cards listed in selected files grouped by file. First print of each card is selected.\n Comments count as tags if inline.", SetName = "List", Required = true)]
		public IEnumerable<string> Files { get; set; }
	}
}
