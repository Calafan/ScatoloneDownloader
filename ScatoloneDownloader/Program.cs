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
                    .WithDescription("Read rating/status/effects from cube-metadata.json and build the cube views via symlinks.");

                config.AddCommand<TagCommand>("tag")
                    .WithDescription("Launch the local web tagger to assign card rating/status/effects and autosave to cube-metadata.json.");

                config.AddCommand<ImportCommand>("import")
                    .WithDescription("One-time seed: read Adobe Bridge XMP rating/label and migrate it into cube-metadata.json.");

                config.AddCommand<RestoreCommand>("restore")
                    .WithDescription("Rebuild an image folder from cube-metadata.json + Scryfall bulk data (no XMP written).");
            });

            return await app.RunAsync(args);
        }
    }
}
