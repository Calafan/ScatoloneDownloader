using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Extracts colored mana pips from a Scryfall <c>mana_cost</c> string.
    /// Scryfall format: <c>{2}{R}{R}</c> where <c>{N}</c> is generic, <c>{X}</c> is
    /// variable, and <c>{W|U|B|R|G}</c> are colored pips. Also handles hybrid /
    /// Phyrexian pips like <c>{W/U}</c> (both colors count) and <c>{W/P}</c>.
    /// </summary>
    internal static partial class ManaPipsParser
    {
        private static readonly string[] ColoredSymbols = ["W", "U", "B", "R", "G"];


        /// <summary>Returns the count of colored mana pips in the cost.</summary>
        internal static int CountColoredPips(string? manaCost)
        {
            return GetColoredPips(manaCost).Count;
        }

        /// <summary>Returns the list of colored pips (e.g. ["R","R"] for "{2}{R}{R}").</summary>
        internal static List<string> GetColoredPips(string? manaCost)
        {
            List<string> pips = [];

            if (string.IsNullOrEmpty(manaCost))
            {
                return pips;
            }

            // Each pip is enclosed in braces: {R}, {2}, {X}, {W/U}, {W/P}
            foreach (Match match in PipPattern().Matches(manaCost))
            {
                string symbol = match.Groups[1].Value;

                foreach (string color in ColoredSymbols)
                {
                    if (symbol.Contains(color))
                    {
                        pips.Add(color);
                    }
                }
            }

            return pips;
        }


        [GeneratedRegex(@"\{([^}]+)\}", RegexOptions.Compiled)]
        private static partial Regex PipPattern();
    }
}