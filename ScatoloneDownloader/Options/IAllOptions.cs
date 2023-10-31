using CommandLine;

namespace ScatoloneDownloader.Options
{
	public interface IAllOptions
	{
		[Option('a', "all", HelpText = "Download all unique artwork cards grouped by released year and set.", SetName = "All", Required = true)]
		public bool All { get; set; }

		[Option('e', "exclude", HelpText = "As <all> but exlude cards in selected file.", SetName = "All", Default = "")]
		public string ExcludeFile { get; set; }
	}
}
