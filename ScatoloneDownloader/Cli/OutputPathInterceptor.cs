using ScatoloneDownloader.Download;

using Spectre.Console.Cli;

namespace ScatoloneDownloader.Cli
{
	/// <summary>
	/// Applies the shared <c>--output</c> option to <see cref="OutputPaths.Root"/>
	/// before any command runs, so even <c>--clear</c> targets the chosen root.
	/// </summary>
	internal sealed class OutputPathInterceptor : ICommandInterceptor
	{
		public void Intercept(CommandContext context, CommandSettings settings)
		{
			if (settings is DownloadSettings download)
			{
				OutputPaths.UseRoot(download.Output);
			}
		}

		public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
		{
		}
	}
}
