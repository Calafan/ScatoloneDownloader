using System.ComponentModel;
using System.IO;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli.Cube
{
    /// <summary>
    /// Base settings for every cube command that reads or writes the metadata
    /// directory (<c>import</c>/<c>tag</c>/<c>classify</c>/<c>build-views</c>/
    /// <c>restore</c>/<c>make-list</c>). Owns the single <c>-m|--metadata</c>
    /// option, its help text, and the default-to-<c>./metadata</c> resolution, so
    /// none of that is copy-pasted per command.
    /// </summary>
    internal class MetadataSettings : CommandSettings
    {
        internal const string MetadataDescription =
            "Path to the git-tracked metadata directory (pool.json/fringe.json/unrated.json). Defaults to ./metadata.";

        [CommandOption("-m|--metadata")]
        [Description(MetadataDescription)]
        public string MetadataDirectory { get; set; }

        /// <summary>The metadata directory as an absolute path, defaulting to
        /// <c>./metadata</c> when the option is not given.</summary>
        internal string ResolveDirectory()
        {
            return string.IsNullOrWhiteSpace(MetadataDirectory)
                ? Path.GetFullPath("metadata")
                : Path.GetFullPath(MetadataDirectory);
        }
    }
}
