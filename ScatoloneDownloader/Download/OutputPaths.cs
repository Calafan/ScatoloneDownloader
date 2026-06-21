using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Download
{
	/// <summary>
	/// Owns the on-disk output layout: the per-mode base folders, filename
	/// sanitization, and per-card directory building. Paths are built with
	/// <see cref="Path.Combine"/> so they work on every platform.
	/// </summary>
	internal static class OutputPaths
	{
		private static readonly string[] ForbiddenCharacters = ["\\", "/", ":", "*", "?", "\"", "<", ">", "|"];

		internal static readonly IReadOnlyDictionary<Mode, string> BasePaths = new Dictionary<Mode, string>
		{
			{ Mode.All, Path.Combine(".", "All") },
			{ Mode.Set, Path.Combine(".", "Sets") },
			{ Mode.Years, Path.Combine(".", "Years") },
			{ Mode.Files, Path.Combine(".", "Lists") },
		};

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
			string path = BasePaths[mode];

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
