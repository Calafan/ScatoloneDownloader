using System.Collections.Generic;
using System.Linq;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Maps a Scryfall <c>color_identity</c> (array of color codes like
    /// ["W","U"]) to a cube-design bucket string. Single colors map to their
    /// letter; 2 colors to the ordered guild code; 3 colors to the canonical WotC
    /// shard/wedge code used by Plan §3.3; 4-5 colors to "4_5_Colors". Empty/null =
    /// "Colorless".
    ///
    /// For 2- and 3-color identities, WUBRG strict sort does not always match the
    /// canonical Magic shard/wedge code (e.g. Temur "GUR" vs strict-sort "URG"),
    /// so the 3-color bucket uses a fixed lookup keyed by the WUBRG-sorted triplet.
    /// </summary>
    internal static class ColorCategoryClassifier
    {
        private static readonly string WubrgOrder = "WUBRG";

        /// <summary>2-color bucket code (guild) by WUBRG-sorted pair key (Plan §3.3 names).
        /// Ally pairs that wrap around the WUBRG ring (W&G, U&G) are listed explicitly
        /// since strict sort disagrees with Plan's canonical guild codes.</summary>
        private static readonly Dictionary<string, string> GuildCodes = new()
        {
            { "WU", "WU" },  // Azorius
			{ "UB", "UB" },  // Dimir
			{ "BR", "BR" },  // Rakdos
			{ "RG", "RG" },  // Gruul
			{ "WG", "GW" },  // Selesnya — strict sort "WG", canonical "GW" (wrap pair)
			{ "WB", "WB" },  // Orzhov
			{ "UR", "UR" },  // Izzet
			{ "BG", "BG" },  // Golgari
			{ "WR", "RW" },  // Boros — strict sort "WR", canonical "RW"
			{ "UG", "GU" },  // Simic — strict sort "UG", canonical "GU"
		};

        /// <summary>3-color bucket code by WUBRG-sorted triplet (Plan §3.3 names).</summary>
        private static readonly Dictionary<string, string> TriColorCodes = new()
        {
			// Shards (3 consecutive WUBRG colors, start at ally).
			{ "WUB", "WUB" },  // Esper
			{ "UBR", "UBR" },  // Grixis
			{ "BRG", "BRG" },  // Jund
			{ "WRG", "RGW" },  // Naya — strict sort "WRG", canonical "RGW"
			{ "WUG", "GWU" },  // Bant — strict sort "WUG", canonical "GWU"
			// Wedges.
			{ "WBG", "WBG" },  // Abzan
			{ "WUR", "URW" },  // Jeskai — strict "WUR", canonical "URW"
			{ "UBG", "BUG" },  // Sultai — strict "UBG", canonical "BUG"
			{ "WBR", "RWB" },  // Mardu — strict "WBR", canonical "RWB"
			{ "URG", "GUR" },  // Temur — strict "URG", canonical "GUR"
		};


        internal static string Classify(IReadOnlyList<string>? colorIdentity)
        {
            if (colorIdentity == null || colorIdentity.Count == 0)
            {
                return "Colorless";
            }

            // Canonical WUBRG sort for the lookup key. Ignore unknown color codes
            // defensively — Scryfall should always send WUBRG, but never trust.
            List<string> sorted = colorIdentity
                .Where(c => WubrgOrder.Contains(c))
                .OrderBy(c => WubrgOrder.IndexOf(c))
                .ToList();

            if (sorted.Count == 0)
            {
                return "Colorless";
            }

            string sortKey = string.Concat(sorted);

            return sorted.Count switch
            {
                1 => sorted[0],
                2 => GuildCodes.TryGetValue(sortKey, out string g) ? g : sortKey,
                3 => TriColorCodes.TryGetValue(sortKey, out string t) ? t : sortKey,
                _ => "4_5_Colors",
            };
        }
    }
}