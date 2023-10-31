using System;

namespace ScatoloneDownloader
{
	internal class ConsoleWriter
	{
		internal static void Write(string text)
		{
			Console.Write("\r" + text);
		}

		internal static void WriteLine(string text)
		{
			Write(Environment.NewLine + text + Environment.NewLine);
		}
	}
}
