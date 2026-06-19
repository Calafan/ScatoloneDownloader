using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ScatoloneDownloader.Download;
using ScatoloneDownloader.Enums;
using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Mtg;

using Spectre.Console;

namespace ScatoloneDownloader.Cli
{
	/// <summary>
	/// Orchestrates a single download/analyze run. Replaces the old 11-parameter
	/// Program.GetCards dispatch; the subcommands call the mode-specific entry
	/// points below.
	/// </summary>
	internal sealed class CardService
	{
		internal const int MinYear = 1993;
		internal const int MaxYear = 2050;

		internal Task RunAllAsync(string excludeFile, bool reprints, bool tokens, bool printOnly)
		{
			return GetCardsAsync(Mode.All, reprints, tokens, false, null, null, excludeFile, download: true, analyze: false, printOnly);
		}

		internal async Task RunSetsAsync(IEnumerable<string> sets, bool reprints, bool tokens, bool printOnly)
		{
			foreach (string set in sets)
			{
				await GetCardsAsync(Mode.Set, reprints, tokens, false, set, null, null, download: true, analyze: false, printOnly);
			}
		}

		internal Task RunYearsAsync(IEnumerable<int> years, bool reprints, bool tokens, bool printOnly)
		{
			List<int> validYears = years.Where(year => year >= MinYear && year <= MaxYear).ToList();

			return GetCardsAsync(Mode.Years, reprints, tokens, false, null, validYears, null, download: true, analyze: false, printOnly);
		}

		internal async Task RunFilesAsync(IEnumerable<string> files, bool reprints, bool tokens, bool lands, bool printOnly)
		{
			foreach (string file in files)
			{
				if (File.Exists(file))
				{
					// A Files download also writes the stats file (analyze: true), as before.
					await GetCardsAsync(Mode.Files, reprints, tokens, lands, null, null, file, download: true, analyze: true, printOnly);
				}
				else
				{
					AnsiConsole.MarkupLineInterpolated($"[red]File not found:[/] {file}");
				}
			}
		}

		internal async Task RunAnalyzeAsync(IEnumerable<string> files, bool reprints, bool tokens, bool printOnly)
		{
			foreach (string file in files)
			{
				if (File.Exists(file))
				{
					await GetCardsAsync(Mode.Files, reprints, tokens, false, null, null, file, download: false, analyze: true, printOnly);
				}
				else
				{
					AnsiConsole.MarkupLineInterpolated($"[red]File not found:[/] {file}");
				}
			}
		}


		private async Task GetCardsAsync(Mode mode, bool downloadReprints, bool downloadTokens, bool downloadLands, string set, List<int> years, string file, bool download, bool analyze, bool printOnly)
		{
			GetManager getManager = new();
			CardDownloader downloader = new(getManager);

			string specificText = DescribeRequest(mode, set, years, file);

			AnsiConsole.MarkupLineInterpolated($"Getting {specificText} cards informations.");

			List<Card> cards = await (mode switch
			{
				Mode.All => string.IsNullOrEmpty(file) ? getManager.GetUniqueArtwork() : getManager.GetUniqueArtwork(file),
				Mode.Set => getManager.GetSet(set),
				Mode.Years => getManager.GetYears(years),
				Mode.Files => getManager.GetCardList(file, downloadLands),
				_ => throw new ArgumentOutOfRangeException(nameof(mode)),
			});

			if (mode != Mode.Files)
			{
				AnsiConsole.MarkupLine("Validating cards.");
				cards = CardFilter.Validate(cards, downloadReprints, downloadTokens);
			}

			if (analyze)
			{
				AnsiConsole.MarkupLine("Analyzing cards.");

				Directory.CreateDirectory(OutputPaths.BasePaths[Mode.Files]);

				string path = Path.Combine(OutputPaths.BasePaths[Mode.Files], Path.GetFileNameWithoutExtension(file) + "Stats.txt");

				CardAnalyzer cardAnalyzer = new(cards);
				cardAnalyzer.SaveAnalysis(path);
			}

			if (printOnly)
			{
				AnsiConsole.MarkupLine("Writing list.");

				foreach (Card card in cards)
				{
					downloader.WriteToList(card);
				}
			}
			else if (download)
			{
				await DownloadAllAsync(downloader, cards, mode, file, specificText);
			}
		}

		private static async Task DownloadAllAsync(CardDownloader downloader, List<Card> cards, Mode mode, string file, string specificText)
		{
			await AnsiConsole.Progress()
				.Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
				.StartAsync(async ctx =>
				{
					ProgressTask task = ctx.AddTask($"Downloading {specificText}", maxValue: cards.Count);

					foreach (Card card in cards)
					{
						await downloader.DownloadAsync(card, mode, file);
						task.Increment(1);
					}
				});
		}

		private static string DescribeRequest(Mode mode, string set, List<int> years, string file)
		{
			switch (mode)
			{
				case Mode.All:
					return "Unique Artworks";
				case Mode.Set:
					return set + " set";
				case Mode.Years:
					string joined = string.Join(", ", years);
					return joined + (years.Count == 1 ? " year" : " years");
				case Mode.Files:
					return file + " content";
				default:
					return string.Empty;
			}
		}
	}
}
