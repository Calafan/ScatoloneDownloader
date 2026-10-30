using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Cli.Download;

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
                    .WithDescription("Download cards listed in file(s), copying from the library what it already holds.");

                config.AddCommand<LandsCommand>("lands")
                    .WithDescription("Download every basic land artwork, grouped by land type.");

                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Bring the library up to the library rules: download the missing cards into Source, register them and propose their effects, restamp the scryfallId of entries whose printing changed; list (never delete) extra files and store entries with no image.");

                config.AddCommand<AnalyzeCommand>("analyze")
                    .WithDescription("Analyze list file(s) without downloading images.");

                config.AddCommand<TagCommand>("tag")
                    .WithDescription("Launch the local web tagger to assign card rating/status/effects and autosave to the metadata directory.");

                config.AddCommand<RestoreCommand>("restore")
                    .WithDescription("Rebuild an image folder from the metadata directory + Scryfall bulk data.");

                config.AddCommand<MakeListCommand>("make-list")
                    .WithDescription("Write a download list (for the `files` command) of the pool (rating 3-5) from the metadata directory, with Banned/Token/Jolly in their own sections.");

                config.AddCommand<ClassifyCommand>("classify")
                    .WithDescription("Auto-propose effect tags from Scryfall rules text into the metadata (unreviewed suggestions; confirm them in the tagger). Never touches reviewed entries.");

                config.AddCommand<AuditCommand>("audit")
                    .WithDescription("Report reviewed cards that say the same thing and were tagged differently — the slips repetitive tagging produces. Read-only.");
            });

            return await app.RunAsync(args);
        }
    }
}
