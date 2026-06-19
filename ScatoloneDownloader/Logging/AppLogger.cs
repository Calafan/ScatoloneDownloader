using Microsoft.Extensions.Logging;

namespace ScatoloneDownloader.Logging
{
	/// <summary>
	/// Process-wide access to the logging abstraction. Backed by
	/// <see cref="ILoggerFactory"/> so the concrete provider is swappable —
	/// the default writes single-line entries to the console, and
	/// <see cref="Configure"/> lets the composition root replace it.
	/// </summary>
	internal static class AppLogger
	{
		private static ILoggerFactory factory = LoggerFactory.Create(builder =>
		{
			builder.AddSimpleConsole(options =>
			{
				options.SingleLine = true;
				options.TimestampFormat = "HH:mm:ss ";
			});
			builder.SetMinimumLevel(LogLevel.Information);
		});

		public static void Configure(ILoggerFactory loggerFactory)
		{
			factory = loggerFactory;
		}

		public static ILogger CreateLogger<T>()
		{
			return factory.CreateLogger<T>();
		}

		public static ILogger CreateLogger(string categoryName)
		{
			return factory.CreateLogger(categoryName);
		}
	}
}
