using System.ComponentModel;

using ScatoloneDownloader.Metadata;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Download
{
    internal sealed class FilesSettings : DownloadSettings
    {
        [CommandArgument(0, "<FILES>")]
        [Description("One or more list files to download from.")]
        public string[] Files { get; set; } = [];

        [CommandOption("--root <DIR>")]
        [Description("The library root whose Source\\ folder is copied from instead of downloading the cards it already holds. Defaults to \"root\" in scatolone.json beside the executable.")]
        public string? Root { get; set; }

        /// <summary>Where <c>scatolone.json</c> is looked up. Tests point it at a temp folder.</summary>
        internal string SettingsDirectory { get; init; } = AppContext.BaseDirectory;

        /// <summary>
        /// The library's <c>Source</c> folder, or <c>null</c> with a one-line
        /// <paramref name="error"/> when there is no usable root or it has no
        /// <c>Source</c>. Unlike the cube commands, a missing library is not fatal here:
        /// the cards are then all downloaded, as before the library existed.
        /// </summary>
        internal string? ResolveLibrarySource(out string? error)
        {
            string? root = LibraryRoot.Resolve(Root, SettingsDirectory, out error);
            if (root is null)
            {
                return null;
            }

            string source = LibraryRoot.Source(root);
            if (!Directory.Exists(source))
            {
                error = $"Source folder not found: {source}";
                return null;
            }

            return source;
        }
    }

    internal sealed class FilesCommand : AsyncCommand<FilesSettings>
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, FilesSettings settings, CancellationToken cancellationToken)
        {
            if (settings.Clear)
            {
                FolderCleaner.Clear();
            }

            // --print-only copies and downloads nothing, so it has no use for the library.
            string? librarySource = null;
            if (!settings.PrintOnly)
            {
                librarySource = settings.ResolveLibrarySource(out string? error);
                if (librarySource is null)
                {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]No library root ({error}): every card is downloaded.[/]");
                }
            }

            await CardService.RunFilesAsync(settings.Files, settings.Lands, settings.PrintOnly, librarySource);

            return 0;
        }
    }
}
