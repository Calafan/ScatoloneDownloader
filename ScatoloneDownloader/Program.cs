using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;
using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Mtg;
using ScatoloneDownloader.Options;

namespace ScatoloneDownloader
{
	class Program
	{

		static async Task Main(string[] args)
		{
			await Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsedAsync(Run);
		}

		static void ClearFolders()
		{
			Console.Write("Press any key delete to delete folders and start.");
			Console.ReadKey();
			Console.Clear();

			if (Directory.Exists(OutputPaths.BasePaths[Mode.All]))
			{
				Directory.Delete(OutputPaths.BasePaths[Mode.All], true);
			}

			if (Directory.Exists(OutputPaths.BasePaths[Mode.Set]))
			{
				Directory.Delete(OutputPaths.BasePaths[Mode.Set], true);
			}

			if (Directory.Exists(OutputPaths.BasePaths[Mode.Years]))
			{
				Directory.Delete(OutputPaths.BasePaths[Mode.Years], true);
			}

			if (Directory.Exists(OutputPaths.BasePaths[Mode.Files]))
			{
				Directory.Delete(OutputPaths.BasePaths[Mode.Files], true);
			}
		}

		static async Task GetCards(Mode mode, bool downloadReprints, bool downloadTokens, bool downloadLands, string set, List<int> years, string file, bool download, bool analyze, bool printOnly)
		{
			GetManager getManager = new();
			CardDownloader downloader = new(getManager);
			string specificText = string.Empty;

			switch(mode)
			{
				case Mode.All:
					specificText += "Unique Artworks";
					break;
				case Mode.Set:
					specificText += set + " set";
					break;
				case Mode.Years:
					foreach(int year in years)
					{
						specificText += year.ToString() + ", ";
					}
					specificText = specificText.Remove(specificText.Length - 2, 1);
					specificText += years.Count == 1 ? "year" : "years";
					break;
				case Mode.Files:
					specificText += file + " content";
					break;
			}

			Console.WriteLine(string.Format("Getting {0} cards informations.", specificText));
			List<Card> cards = await (mode switch
			{
				Mode.All => string.IsNullOrEmpty(file) ? getManager.GetUniqueArtwork() : getManager.GetUniqueArtwork(file),
				Mode.Set => getManager.GetSet(set),
				Mode.Years => getManager.GetYears(years),
				Mode.Files => getManager.GetCardList(file, downloadLands),
				_ => throw new Exception(),
			});

			if (mode != Mode.Files)
			{
				Console.WriteLine("Validating cards.");
				cards = CardFilter.Validate(cards, downloadReprints, downloadTokens);
			}

			if (analyze)
			{
				Console.WriteLine("Analyzing cards.");

				if (!Directory.Exists(OutputPaths.BasePaths[Mode.Files]))
				{
					Directory.CreateDirectory(OutputPaths.BasePaths[Mode.Files]);
				}

				string path = Path.Combine(OutputPaths.BasePaths[Mode.Files], Path.GetFileNameWithoutExtension(file) + "Stats.txt");

				CardAnalyzer cardAnalyzer = new(cards);
				cardAnalyzer.SaveAnalysis(path);
			}

			if (printOnly)
			{
				Console.WriteLine("Writing list.");
				
				foreach(Card card in cards)
				{
					downloader.WriteToList(card);
				}
			}
			else if (download)
			{
				Console.WriteLine("Downloading cards.");
				int i = 0;
				foreach (Card card in cards)
				{
					await downloader.DownloadAsync(card, mode, file);
					i++;
					ConsoleWriter.Write(string.Format("{0} / {1}   ", i, cards.Count));
				}
			}
		}

		static async Task Run(CommandLineOptions options)
		{
			ClearFolders();

			if (options.All)
			{
				string exludeFile = options.ExcludeFile;

				if (string.IsNullOrEmpty(exludeFile) || File.Exists(exludeFile))
				{
					await GetCards(Mode.All, options.Reprints, options.Tokens, options.Lands, null, null, options.ExcludeFile, true, false, options.PrintOnly);
				}
				else
				{
					Console.WriteLine("File not found: " + exludeFile);
				}
			}
			else
			{
				foreach (string set in options.Sets)
				{
					await GetCards(Mode.Set, options.Reprints, options.Tokens, options.Lands, set, null, null, true, false, options.PrintOnly);
				}

				List<int> years = new();

				foreach (int year in options.Years)
				{
					if (year >= 1993 && year <= 2050)
						years.Add(year);
				}

				if (years.Count > 0)
				{
					await GetCards(Mode.Years, options.Reprints, options.Tokens, options.Lands, null, years, null, true, false, options.PrintOnly);
				}

				foreach (string file in options.Files)
				{
					if (File.Exists(file))
					{
						await GetCards(Mode.Files, options.Reprints, options.Tokens, options.Lands, null, null, file, true, true, options.PrintOnly);
					}
					else
					{
						Console.WriteLine("File not found: " + file);
					}
				}

				foreach (string file in options.FilesToAnalyze)
				{
					if (File.Exists(file))
					{
						await GetCards(Mode.Files, options.Reprints, options.Tokens, false, null, null, file, false, true, options.PrintOnly);
					}
					else
					{
						Console.WriteLine("File not found: " + file);
					}
				}
			}

			Console.WriteLine();
			Console.WriteLine("\nClick any button to exit.");
			Console.ReadKey();
		}
	}
}
