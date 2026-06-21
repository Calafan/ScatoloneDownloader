using System.Threading.Tasks;

using ScatoloneDownloader.Cli;

using Spectre.Console.Cli;

namespace ScatoloneDownloader
{
	internal static class Program
	{
		static async Task<int> Main(string[] args)
		{
			CommandApp app = new();

			app.Configure(config =>
			{
				config.SetApplicationName("ScatoloneDownloader");
				config.SetInterceptor(new OutputPathInterceptor());

				config.AddCommand<AllCommand>("all")
					.WithDescription("Download all unique-artwork cards, grouped by released year and set.");

				config.AddCommand<SetCommand>("set")
					.WithDescription("Download the given set codes.");

				config.AddCommand<YearsCommand>("years")
					.WithDescription("Download cards released in the given years.");

				config.AddCommand<FilesCommand>("files")
					.WithDescription("Download cards listed in file(s) and write a stats file.");

				config.AddCommand<AnalyzeCommand>("analyze")
					.WithDescription("Analyze list file(s) without downloading images.");
			});

			return await app.RunAsync(args);
		}
	}
}
