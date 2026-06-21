using System.Collections.Generic;
using System.IO;
using System.Linq;

using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Download
{
	/// <summary>
	/// Owns the on-disk output layout: the output root, per-mode sub-folders,
	/// filename sanitization, and per-card directory building. Paths are built
	/// with <see cref="Path.Combine"/> so they work on every platform.
	/// </summary>
	internal static class OutputPaths
	{
		private static readonly string[] ForbiddenCharacters = ["\\", "/", ":", "*", "?", "\"", "<", ">", "|"];

		// Root holding every per-mode folder. Defaults to ./Output (relative to the
		// working directory) and can be overridden with the --output option.
		internal static string Root { get; private set; } = Path.Combine(".", "Output");

		private static readonly IReadOnlyDictionary<Mode, string> SubFolders = new Dictionary<Mode, string>
		{
			{ Mode.All, "All" },
			{ Mode.Set, "Sets" },
			{ Mode.Years, "Years" },
			{ Mode.Files, "Lists" },
		};

		/// <summary>Overrides the output <see cref="Root"/>; ignores null/blank input.</summary>
		internal static void UseRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
			{
				Root = root;
			}
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
		internal static string BuildCardDirectory(Card card, Mode mode, string fileName)
		{
			string path = BasePath(mode);

			Directory.CreateDirectory(path);

			switch (mode)
			{
				case Mode.All:
				case Mode.Years:
				{
					path = Path.Combine(path, card.ReleasedAt.Year.ToString());
					Directory.CreateDirectory(path);

					path = Path.Combine(path, Sanitize(card.SetName));
					break;
				}
				case Mode.Set:
				{
					path = Path.Combine(path, Sanitize(card.SetName));
					break;
				}
				case Mode.Files:
				{
					path = Path.Combine(path, Path.GetFileNameWithoutExtension(fileName));

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

			Directory.CreateDirectory(path);

			return path;
		}
	}
}
