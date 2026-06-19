using System.IO;

using ScatoloneDownloader.Download;

namespace ScatoloneDownloader.Cli
{
	/// <summary>Deletes the per-mode output folders. Opt-in via the --clear flag.</summary>
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
	}
}
