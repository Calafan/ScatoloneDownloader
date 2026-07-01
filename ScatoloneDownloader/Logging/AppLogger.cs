using Microsoft.Extensions.Logging;

namespace ScatoloneDownloader.Logging
{
	/// <summary>
	/// Process-wide access to the logging abstraction. Backed by an
	/// <see cref="ILoggerFactory"/> that writes single-line entries to the console
	/// and appends them to a run log file (<see cref="LogFileName"/>), so warnings
	/// like "Missing card" persist after the run.
	/// </summary>
	internal static class AppLogger
	{
		// Kept in the working directory, matching the pre-migration SimpleLogger.
		private const string LogFileName = "ScatoloneDownloader.log";

		private static readonly ILoggerFactory factory = LoggerFactory.Create(builder =>
		{
			builder.AddSimpleConsole(options =>
			{
				options.SingleLine = true;
				options.TimestampFormat = "HH:mm:ss ";
			});
			builder.AddProvider(new FileLoggerProvider(LogFileName, LogLevel.Information));
			builder.SetMinimumLevel(LogLevel.Information);
		});

		internal static ILogger CreateLogger<T>()
		{
			return factory.CreateLogger<T>();
		}

		internal static ILogger CreateLogger(string categoryName)
		{
			return factory.CreateLogger(categoryName);
		}
	}
}
