using System.ComponentModel;
using System.IO;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Base settings for every cube command that reads or writes the metadata
    /// directory (<c>import</c>/<c>tag</c>/<c>classify</c>/<c>build-views</c>/
    /// <c>restore</c>/<c>make-list</c>). Owns the single <c>-m|--metadata</c>
    /// option, its help text, and the default resolution, so none of that is
    /// copy-pasted per command.
    /// </summary>
    internal class MetadataSettings : CommandSettings
    {
        internal const string MetadataDescription =
            "Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). "
            + "Defaults to a 'metadata' folder beside the master library, or ./metadata for commands that take no master folder.";

        [CommandOption("-m|--metadata")]
        [Description(MetadataDescription)]
        public string MetadataDirectory { get; set; }

        /// <summary>
        /// The master library this command works against, or <c>null</c> when it
        /// has none. <c>import</c>, <c>build-views</c> and <c>tag</c> override this
        /// with their <c>SOURCE_DIR</c> argument; overriding it (rather than
        /// passing the path in at each call site) means a new command cannot
        /// silently fall back to the wrong default by forgetting an argument.
        /// </summary>
        internal virtual string MasterDirectory => null;

        /// <summary>
        /// The metadata directory as an absolute path. An explicit
        /// <c>-m|--metadata</c> always wins. Otherwise the store sits BESIDE the
        /// master library (<c>&lt;parent of SOURCE_DIR&gt;/metadata</c>, the same
        /// rule <c>build-views</c> uses to place <c>Views/</c>), so the metadata
        /// travels with the images it describes instead of landing wherever the
        /// shell happened to be. Commands that take no master folder
        /// (<c>classify</c>, <c>make-list</c>, <c>restore</c>) have nothing to sit
        /// beside and fall back to <c>./metadata</c> — they print the path they
        /// resolved, so a mismatch with the importing command is visible at once.
        /// </summary>
        internal string ResolveDirectory()
        {
            if (!string.IsNullOrWhiteSpace(MetadataDirectory))
            {
                return Path.GetFullPath(MetadataDirectory);
            }

            if (!string.IsNullOrWhiteSpace(MasterDirectory))
            {
                DirectoryInfo master = new(Path.GetFullPath(MasterDirectory));
                return Path.Combine(master.Parent?.FullName ?? master.FullName, "metadata");
            }

            return Path.GetFullPath("metadata");
        }
    }
}
