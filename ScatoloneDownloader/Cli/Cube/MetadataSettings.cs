using System.ComponentModel;

using ScatoloneDownloader.Metadata;

using Spectre.Console;
using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Base settings for every cube command that reads or writes the library
    /// (<c>tag</c>/<c>classify</c>/<c>restore</c>/
    /// <c>make-list</c>/<c>audit</c>/<c>sync</c>). Owns the <c>--root</c> and
    /// <c>-m|--metadata</c> options and their resolution, so none of that is
    /// copy-pasted per command. The library root (see <see cref="LibraryRoot"/>)
    /// holds <c>Source\</c> and <c>metadata\</c>; it comes from
    /// <c>--root</c> or from <c>scatolone.json</c> beside the executable. An
    /// explicit <c>-m</c> overrides only the metadata folder.
    /// </summary>
    internal class MetadataSettings : CommandSettings
    {
        internal const string MetadataDescription =
            "Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). "
            + "Defaults to the 'metadata' folder of the library root.";

        [CommandOption("--root <DIR>")]
        [Description("The library root holding Source\\ and metadata\\. Defaults to \"root\" in scatolone.json beside the executable.")]
        public string? Root { get; set; }

        [CommandOption("-m|--metadata")]
        [Description(MetadataDescription)]
        public string? MetadataDirectory { get; set; }

        /// <summary>Where <c>scatolone.json</c> is looked up. Tests point it at a temp folder.</summary>
        internal string SettingsDirectory { get; init; } = AppContext.BaseDirectory;

        /// <summary>
        /// Whether the command works on the images of <c>Source\</c> (<c>tag</c>
        /// and <c>sync</c> override this to <c>true</c>). Those need a root and its <c>Source</c> folder even when
        /// <c>-m</c> is given; the others need a root only to default the metadata.
        /// </summary>
        internal virtual bool NeedsSource => false;

        private string? resolvedRoot;

        public override ValidationResult Validate()
        {
            if (NeedsSource || string.IsNullOrWhiteSpace(MetadataDirectory))
            {
                string? root = LibraryRoot.Resolve(Root, SettingsDirectory, out string? error);
                if (root is null)
                {
                    return ValidationResult.Error(error ?? "No library root.");
                }

                resolvedRoot = root;

                if (NeedsSource && !Directory.Exists(LibraryRoot.Source(root)))
                {
                    return ValidationResult.Error($"Source folder not found: {LibraryRoot.Source(root)}");
                }
            }

            return ValidationResult.Success();
        }

        /// <summary>The library root as an absolute path (cached after the first resolution).
        /// Throws when there is none: <see cref="Validate"/> reports that first.</summary>
        internal string ResolveRoot()
        {
            if (resolvedRoot is null)
            {
                resolvedRoot = LibraryRoot.Resolve(Root, SettingsDirectory, out string? error)
                    ?? throw new InvalidOperationException(error);
            }

            return resolvedRoot;
        }

        /// <summary>The folder of the master images, <c>&lt;root&gt;\Source</c>.</summary>
        internal string ResolveSourceDirectory()
        {
            return LibraryRoot.Source(ResolveRoot());
        }

        /// <summary>
        /// The metadata directory as an absolute path. An explicit
        /// <c>-m|--metadata</c> always wins (so scripts that pass it keep working
        /// without a root); otherwise it is <c>&lt;root&gt;\metadata</c>. Commands
        /// print the path they resolved, so a mismatch is visible at once.
        /// </summary>
        internal string ResolveDirectory()
        {
            if (!string.IsNullOrWhiteSpace(MetadataDirectory))
            {
                return Path.GetFullPath(MetadataDirectory);
            }

            return LibraryRoot.Metadata(ResolveRoot());
        }
    }
}
