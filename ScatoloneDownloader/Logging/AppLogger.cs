using Microsoft.Extensions.Logging;

namespace ScatoloneDownloader.Logging
{
	/// <summary>
	/// Process-wide access to the logging abstraction. Backed by an
	/// <see cref="ILoggerFactory"/> that writes single-line entries to the console.
	/// </summary>
	internal static class AppLogger
	{
		private static readonly ILoggerFactory factory = LoggerFactory.Create(builder =>
		{
			builder.AddSimpleConsole(options =>
			{
				options.SingleLine = true;
				options.TimestampFormat = "HH:mm:ss ";
			});
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
