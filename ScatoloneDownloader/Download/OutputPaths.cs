using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Download
{
    /// <summary>
    /// Owns the on-disk output layout: the output root, per-mode sub-folders,
    /// filename sanitization, and per-card directory building. Paths are built
    /// with <c>Path.Combine</c> so they work on every platform.
    /// </summary>
    internal static class OutputPaths
    {
        private static readonly string[] ForbiddenCharacters = ["\\", "/", ":", "*", "?", "\"", "<", ">", "|"];

        // Root holding every per-mode folder. Defaults to ./Output (relative to the
        // working directory) and can be overridden with the --output option.
        internal static string Root { get; private set; } = Path.Combine(".", "Output");

        /// <summary>When set (by <c>sync</c>), the <see cref="Mode.All"/> and <see cref="Mode.Years"/>
        /// paths are built straight inside this library
        /// (<c>LibrarySource/year/set</c>, no mode sub-folder). Never part of
        /// <see cref="BasePaths"/>, so <c>--clear</c> cannot touch it.</summary>
        internal static string? LibrarySource { get; private set; }

        private static readonly IReadOnlyDictionary<Mode, string> SubFolders = new Dictionary<Mode, string>
        {
            { Mode.All, "All" },
            { Mode.Set, "Sets" },
            { Mode.Years, "Years" },
            { Mode.Files, "Lists" },
            { Mode.Lands, "BasicLands" },
        };

        /// <summary>Overrides the output <see cref="Root"/>; ignores null/blank input.</summary>
        internal static void UseRoot(string? root)
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                Root = root;
            }
        }

        /// <summary>Sets the <see cref="LibrarySource"/>; ignores null/blank input.</summary>
        internal static void UseLibrary(string? dir)
        {
            if (!string.IsNullOrWhiteSpace(dir))
            {
                LibrarySource = dir;
            }
        }

        /// <summary>Clears the <see cref="LibrarySource"/> (tests restore it with this).</summary>
        internal static void ResetLibrary()
        {
            LibrarySource = null;
        }

        /// <summary>The base folder for a mode, under the configured <see cref="Root"/>.</summary>
        internal static string BasePath(Mode mode)
        {
            return Path.Combine(Root, SubFolders[mode]);
        }

        /// <summary>Every per-mode base folder, for bulk operations like --clear.</summary>
        internal static IEnumerable<string> BasePaths => SubFolders.Values.Select(name => Path.Combine(Root, name));

        internal static string Sanitize(string path)
        {
            if (path.Contains(" // "))
            {
                path = path.Replace(" // ", "_");
            }

            foreach (string character in ForbiddenCharacters)
            {
                if (path.Contains(character))
                {
                    path = path.Replace(character, string.Empty);
                }
            }

            return path;
        }

        /// <summary>Builds (and creates) the directory a card's image belongs in.</summary>
        internal static string BuildCardDirectory(Card card, Mode mode, string? fileName)
        {
            string path = CardDirectory(card, mode, fileName);

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>The directory a card's image belongs in, with no IO: the path
        /// <see cref="BuildCardDirectory"/> creates and returns.</summary>
        internal static string CardDirectory(Card card, Mode mode, string? fileName)
        {
            bool inLibrary = LibrarySource != null && mode is Mode.All or Mode.Years;
            string path = inLibrary ? LibrarySource! : BasePath(mode);

            switch (mode)
            {
                case Mode.All:
                case Mode.Years:
                    {
                        path = Path.Combine(path, card.ReleasedAt.Year.ToString());
                        path = Path.Combine(path, Sanitize(card.SetName));
                        break;
                    }
                case Mode.Set:
                    {
                        path = Path.Combine(path, Sanitize(card.SetName));
                        break;
                    }
                case Mode.Lands:
                    {
                        // Group every printing of a basic land under a folder named after
                        // its type (the card name: Plains, Snow-Covered Island, Wastes, ...).
                        path = Path.Combine(path, Sanitize(card.Name));
                        break;
                    }
                case Mode.Files:
                    {
                        path = Path.Combine(path, Path.GetFileNameWithoutExtension(fileName ?? string.Empty));

                        // Tag comes from the hand-written list, so sanitize it like any other
                        // path segment and neutralize "."/".." so it cannot escape the base folder.
                        string tag = Sanitize(card.Tag ?? string.Empty).Trim().TrimStart('.');

                        if (!string.IsNullOrEmpty(tag))
                        {
                            path = Path.Combine(path, tag);
                        }

                        break;
                    }
            }

            return path;
        }

        /// <summary>The file a card's image has in the library set by <see cref="UseLibrary"/>:
        /// <c>LibrarySource\year\set\Name.png</c>, the path <c>sync</c> expects and <c>restore</c> writes.</summary>
        internal static string LibraryImagePath(Card card)
        {
            if (LibrarySource == null)
            {
                throw new InvalidOperationException("UseLibrary must be called before LibraryImagePath.");
            }

            return Path.Combine(CardDirectory(card, Mode.All, null), Sanitize(card.Name) + ".png");
        }
    }
}
