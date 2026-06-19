using System;
using System.IO;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;

namespace ScatoloneDownloader.Cli
{
	/// <summary>Deletes the per-mode output folders.</summary>
	internal static class FolderCleaner
	{
		internal static void Clear()
		{
			foreach (string path in OutputPaths.BasePaths.Values)
			{
				if (Directory.Exists(path))
				{
					Directory.Delete(path, true);
				}
			}
		}

		internal static void ClearWithPrompt()
		{
			Console.Write("Press any key to delete the existing output folders and start.");
			Console.ReadKey();
			Console.Clear();

			Clear();
		}
	}
}
