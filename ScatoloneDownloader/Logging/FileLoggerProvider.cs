using System;
using System.IO;

using Microsoft.Extensions.Logging;

namespace ScatoloneDownloader.Logging
{
	/// <summary>
	/// Minimal <see cref="ILoggerProvider"/> that appends single-line entries to a
	/// log file. Restores the persistent run log the app had before the migration to
	/// Microsoft.Extensions.Logging, so warnings like "Missing card" survive the run
	/// instead of only scrolling past on the console.
	/// </summary>
	internal sealed class FileLoggerProvider : ILoggerProvider
	{
		private readonly string path;
		private readonly LogLevel minLevel;
		private readonly object gate = new();

		internal FileLoggerProvider(string path, LogLevel minLevel)
		{
			this.path = path;
			this.minLevel = minLevel;
		}

		public ILogger CreateLogger(string categoryName)
		{
			return new FileLogger(this, categoryName);
		}

		internal void Append(LogLevel level, string category, string message, Exception exception)
		{
			string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category} - {message}";

			if (exception != null)
			{
				line += Environment.NewLine + exception;
			}

			lock (gate)
			{
				File.AppendAllText(path, line + Environment.NewLine);
			}
		}

		public void Dispose()
		{
		}

		private sealed class FileLogger : ILogger
		{
			private readonly FileLoggerProvider provider;
			private readonly string category;

			internal FileLogger(FileLoggerProvider provider, string category)
			{
				this.provider = provider;
				this.category = category;
			}

			public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

			public bool IsEnabled(LogLevel logLevel) => logLevel >= provider.minLevel && logLevel != LogLevel.None;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				if (!IsEnabled(logLevel))
				{
					return;
				}

				string message = formatter(state, exception);

				if (string.IsNullOrEmpty(message) && exception == null)
				{
					return;
				}

				provider.Append(logLevel, category, message, exception);
			}
		}

		private sealed class NullScope : IDisposable
		{
			internal static readonly NullScope Instance = new();

			private NullScope()
			{
			}

			public void Dispose()
			{
			}
		}
	}
}
