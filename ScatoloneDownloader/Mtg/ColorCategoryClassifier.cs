#nullable enable annotations

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

        /// <summary>Human-readable name per ColorCategory code: single-color words,
        /// guild names, shard/wedge names, plus the 4-5 and Colorless buckets.
        /// Shared by the analysis report and the by-color view folders.</summary>
        private static readonly Dictionary<string, string> DisplayNames = new()
        {
            { "W", "White" }, { "U", "Blue" }, { "B", "Black" }, { "R", "Red" }, { "G", "Green" },
            { "WU", "Azorius" }, { "UB", "Dimir" }, { "BR", "Rakdos" }, { "RG", "Gruul" }, { "GW", "Selesnya" },
            { "WB", "Orzhov" }, { "UR", "Izzet" }, { "BG", "Golgari" }, { "RW", "Boros" }, { "GU", "Simic" },
            { "WUB", "Esper" }, { "UBR", "Grixis" }, { "BRG", "Jund" }, { "RGW", "Naya" }, { "GWU", "Bant" },
            { "WBG", "Abzan" }, { "URW", "Jeskai" }, { "BUG", "Sultai" }, { "RWB", "Mardu" }, { "GUR", "Temur" },
            { "4_5_Colors", "4-5 Colors" }, { "Colorless", "Colorless" },
        };

        /// <summary>Readable name for a ColorCategory code (falls back to the code).</summary>
        internal static string Display(string category)
        {
            return DisplayNames.GetValueOrDefault(category, category);
        }

        /// <summary>Ordering group so a plain name sort yields mono (1) &gt; guilds (2)
        /// &gt; shards/wedges (3) &gt; 4-5 colors (4) &gt; colorless (5). Guild/shard codes
        /// are letters, so their length is their color count.</summary>
        internal static int SortGroup(string category)
        {
            return category switch
            {
                "Colorless" => 5,
                "4_5_Colors" => 4,
                _ => category.Length,
            };
        }

        /// <summary>By-color view folder name: a group-index prefix (so a filesystem
        /// name sort groups by color count) followed by the readable guild/shard name,
        /// e.g. "1 White", "2 Azorius", "3 Esper", "5 Colorless".</summary>
        internal static string ViewFolderName(string category)
        {
            return $"{SortGroup(category)} {Display(category)}";
        }
    }
}