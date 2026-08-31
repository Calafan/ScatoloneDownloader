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

                config.AddCommand<LandsCommand>("lands")
                    .WithDescription("Download every basic land artwork, grouped by land type.");

                config.AddCommand<AnalyzeCommand>("analyze")
                    .WithDescription("Analyze list file(s) without downloading images.");

                config.AddCommand<BuildViewsCommand>("build-views")
                    .WithDescription("Read rating/status/effects from the metadata directory and build the cube views via symlinks.");

                config.AddCommand<TagCommand>("tag")
                    .WithDescription("Launch the local web tagger to assign card rating/status/effects and autosave to the metadata directory.");

                config.AddCommand<ImportCommand>("import")
                    .WithDescription("One-time seed: read Adobe Bridge XMP rating/label and migrate it into the metadata directory.");

                config.AddCommand<RestoreCommand>("restore")
                    .WithDescription("Rebuild an image folder from the metadata directory + Scryfall bulk data (no XMP written).");

                config.AddCommand<MakeListCommand>("make-list")
                    .WithDescription("Write a download list (for the `files` command) of the pool (rating 3-5) from the metadata directory, with Banned/Token/Jolly in their own sections.");

                config.AddCommand<ClassifyCommand>("classify")
                    .WithDescription("Auto-propose effect tags from Scryfall rules text into the metadata (unreviewed suggestions; confirm them in the tagger). Never touches reviewed entries.");
            });

            return await app.RunAsync(args);
        }
    }
}
