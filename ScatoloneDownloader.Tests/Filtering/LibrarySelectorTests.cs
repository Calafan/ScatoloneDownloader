using System.Text.Json;

using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Filtering;

/// <summary>
/// Pure-logic coverage for the library rules: the new card fields, the dungeon
/// token-layout exemption, <see cref="LibrarySelector"/> exclusions, ranking,
/// fallback and the rules-off <see cref="LibrarySelector.WithFallback"/>.
/// One test per rule so each can be neutralised on its own.
/// </summary>
public sealed class LibrarySelectorTests
{
    // --- JSON + Card fields ---------------------------------------------------

    [Fact]
    public void Json_PopulatesLegalitiesCollectorNumberAndFaceOracleId()
    {
        const string json = """
            {
              "id": "abc", "name": "Test", "released_at": "2020-01-01",
              "collector_number": "12a",
              "legalities": { "standard": "not_legal", "modern": "not_legal" },
              "card_faces": [ { "name": "Front", "oracle_id": "face-oracle" }, { "name": "Back" } ],
              "image_uris": { "png": "https://test/img.png" }
            }
            """;

        JsonCard jsonCard = JsonSerializer.Deserialize<JsonCard>(json)!;
        Card card = Card.CreateCard(jsonCard);

        Assert.Equal("12a", jsonCard.CollectorNumber);
        Assert.Equal(2, jsonCard.Legalities!.Count);
        Assert.Equal("face-oracle", jsonCard.CardFaces![0].OracleId);
        Assert.Equal("12a", card.CollectorNumber);
        Assert.Equal("face-oracle", card.FirstFaceOracleId);
        Assert.True(card.NeverLegal);
    }

    [Fact]
    public void NeverLegal_TrueWhenEveryFormatIsNotLegal()
    {
        Assert.True(Make("A", legalities: new() { ["standard"] = "not_legal", ["modern"] = "not_legal" }).NeverLegal);
    }

    [Theory]
    [InlineData("legal")]
    [InlineData("banned")]
    public void NeverLegal_FalseWhenAnyFormatIsLegalOrBanned(string other)
    {
        Assert.False(Make("A", legalities: new() { ["standard"] = "not_legal", ["modern"] = other }).NeverLegal);
    }

    [Fact]
    public void NeverLegal_FalseForEmptyOrMissingLegalities()
    {
        Assert.False(Make("A", legalities: []).NeverLegal);
        Assert.False(Make("A").NeverLegal);
    }

    [Fact]
    public void FirstFaceOracleId_EmptyWithoutFaces()
    {
        Assert.Equal(string.Empty, Make("A").FirstFaceOracleId);
    }

    [Theory]
    [InlineData("Dungeon — Undercity // Card", true)]
    [InlineData("Dungeon", true)]
    [InlineData("Legendary Creature — Human Dungeon Master", false)]
    [InlineData("Creature — Elf", false)]
    [InlineData("Sorcery // Dungeon — Undercity", false)]
    public void IsDungeon_ReadsOnlyTheFrontTypeBeforeTheSubtypeDash(string typeLine, bool expected)
    {
        Assert.Equal(expected, Make("A", typeLine: typeLine).IsDungeon);
    }

    [Theory]
    [InlineData("Token Enchantment — Role", true)]
    [InlineData("Enchantment", false)]
    public void IsTokenType_StartsWithToken(string typeLine, bool expected)
    {
        Assert.Equal(expected, Make("A", typeLine: typeLine).IsTokenType);
    }

    [Theory]
    [InlineData("Token Enchantment Creature — Spirit Sorcerer", true)]
    [InlineData("Token Artifact — Incubator // Token Artifact Creature — Phyrexian", false)]
    [InlineData("Creature — Elf", false)]
    [InlineData("Token Artifact — Treasure", false)]
    public void IsCreatureToken_ReadsOnlyTheFrontTokenType(string typeLine, bool expected)
    {
        Assert.Equal(expected, Make("A", typeLine: typeLine).IsCreatureToken);
    }

    // --- HasValidLayout -------------------------------------------------------

    [Fact]
    public void HasValidLayout_DungeonOnTokenLayout_PassesWithTokensOff()
    {
        Assert.True(CardFilter.HasValidLayout(Make("Undercity", layout: "token", typeLine: "Dungeon"), downloadTokens: false));
    }

    [Fact]
    public void HasValidLayout_NonDungeonOnTokenLayout_FailsWithTokensOff()
    {
        Assert.False(CardFilter.HasValidLayout(Make("Goblin", layout: "token", typeLine: "Token Creature — Goblin"), downloadTokens: false));
    }

    // --- IsExcluded -----------------------------------------------------------

    [Fact]
    public void IsExcluded_FunnySetType()
    {
        Assert.True(LibrarySelector.IsExcluded(Make("A", setType: "funny")));
    }

    [Fact]
    public void IsExcluded_VanguardSetType()
    {
        Assert.True(LibrarySelector.IsExcluded(Make("A", setType: "vanguard")));
    }

    [Fact]
    public void IsExcluded_PlanarLayout()
    {
        Assert.True(LibrarySelector.IsExcluded(Make("A", layout: "planar")));
    }

    [Fact]
    public void IsExcluded_VanguardLayout()
    {
        Assert.True(LibrarySelector.IsExcluded(Make("A", layout: "vanguard")));
    }

    [Fact]
    public void IsExcluded_NeverLegalPlainCard()
    {
        Assert.True(LibrarySelector.IsExcluded(Make("A", legalities: NotLegal())));
    }

    [Fact]
    public void IsExcluded_KeepsNeverLegalDungeon()
    {
        Assert.False(LibrarySelector.IsExcluded(Make("A", typeLine: "Dungeon", legalities: NotLegal())));
    }

    [Fact]
    public void IsExcluded_KeepsNeverLegalTokenType()
    {
        Assert.False(LibrarySelector.IsExcluded(Make("A", typeLine: "Token Creature — Goblin", legalities: NotLegal())));
    }

    [Fact]
    public void IsExcluded_KeepsNeverLegalKeptToken()
    {
        Assert.False(LibrarySelector.IsExcluded(Make("The Monarch", typeLine: "Card", legalities: NotLegal())));
    }

    [Fact]
    public void IsExcluded_DropsNeverLegalUnlistedReminder()
    {
        Assert.True(LibrarySelector.IsExcluded(Make("Energy Reserve", typeLine: "Card", legalities: NotLegal())));
    }

    [Fact]
    public void IsExcluded_KeepsLegalCard()
    {
        Assert.False(LibrarySelector.IsExcluded(Make("A", legalities: new() { ["modern"] = "legal" })));
    }

    // --- Select: strict ranking -----------------------------------------------

    [Fact]
    public void Select_PrefersNonPromoSetOverEarlierPromoSet()
    {
        Card promo = Make("Bolt", id: "1", oracle: "o", setType: "promo", released: "2000-01-01");
        Card plain = Make("Bolt", id: "2", oracle: "o", setType: "core", released: "2010-01-01");

        Assert.Same(plain, Assert.Single(LibrarySelector.Select([promo, plain], false, false, false)));
    }

    [Fact]
    public void Select_PrefersPlainOverPromoTreatment()
    {
        Card treated = Make("Bolt", id: "1", oracle: "o", promoTypes: ["surgefoil"], released: "2000-01-01");
        Card plain = Make("Bolt", id: "2", oracle: "o", released: "2010-01-01");

        Assert.Same(plain, Assert.Single(LibrarySelector.Select([treated, plain], false, false, false)));
    }

    [Fact]
    public void Select_PrefersEarliestRelease()
    {
        Card late = Make("Bolt", id: "1", oracle: "o", released: "2012-01-01");
        Card early = Make("Bolt", id: "2", oracle: "o", released: "2005-01-01");

        Assert.Same(early, Assert.Single(LibrarySelector.Select([late, early], false, false, false)));
    }

    [Fact]
    public void Select_CollectorNumberComparesDigitsNotStrings()
    {
        Card ten = Make("Bolt", id: "1", oracle: "o", collector: "10");
        Card nine = Make("Bolt", id: "2", oracle: "o", collector: "9");

        Assert.Same(nine, Assert.Single(LibrarySelector.Select([ten, nine], false, false, false)));
    }

    [Fact]
    public void Select_EqualDigitsBrokenByCollectorString()
    {
        Card b = Make("Bolt", id: "1", oracle: "o", collector: "12b");
        Card a = Make("Bolt", id: "2", oracle: "o", collector: "12a");

        Assert.Same(a, Assert.Single(LibrarySelector.Select([b, a], false, false, false)));
    }

    [Fact]
    public void Select_FinalTieBrokenById()
    {
        Card second = Make("Bolt", id: "b", oracle: "o", collector: "1");
        Card first = Make("Bolt", id: "a", oracle: "o", collector: "1");

        Assert.Same(first, Assert.Single(LibrarySelector.Select([second, first], false, false, false)));
    }

    // --- Select: fallback -----------------------------------------------------

    [Fact]
    public void Select_Fallback_PrefersNonReprintVariant()
    {
        Card variant = Make("Bolt", id: "1", oracle: "o", borderColor: "borderless", released: "2020-01-01");
        Card reprintVariant = Make("Bolt", id: "2", oracle: "o", borderColor: "borderless", reprint: true, released: "2015-01-01");

        Assert.Same(variant, Assert.Single(LibrarySelector.Select([reprintVariant, variant], false, false, false)));
    }

    [Fact]
    public void Select_Fallback_PrefersEarliestReleaseEvenInPromoSet()
    {
        Card lateOther = Make("Bolt", id: "1", oracle: "o", borderColor: "borderless", released: "2020-01-01");
        Card earlyPromo = Make("Bolt", id: "2", oracle: "o", borderColor: "borderless", setType: "promo", released: "2018-01-01");

        Assert.Same(earlyPromo, Assert.Single(LibrarySelector.Select([lateOther, earlyPromo], false, false, false)));
    }

    [Fact]
    public void Select_Fallback_SameDayPrefersNonPromoSet()
    {
        Card promo = Make("Bolt", id: "1", oracle: "o", borderColor: "borderless", setType: "promo", collector: "1", released: "2020-01-01");
        Card other = Make("Bolt", id: "2", oracle: "o", borderColor: "borderless", collector: "2", released: "2020-01-01");

        Assert.Same(other, Assert.Single(LibrarySelector.Select([promo, other], false, false, false)));
    }

    [Fact]
    public void Select_Fallback_SameDayPrefersPlainOverPromoTreatment()
    {
        Card treated = Make("Bolt", id: "1", oracle: "o", borderColor: "borderless", promoTypes: ["surgefoil"], collector: "1", released: "2020-01-01");
        Card plain = Make("Bolt", id: "2", oracle: "o", borderColor: "borderless", collector: "2", released: "2020-01-01");

        Assert.Same(plain, Assert.Single(LibrarySelector.Select([treated, plain], false, false, false)));
    }

    [Fact]
    public void Select_Fallback_NoCandidateYieldsNothing()
    {
        Card token = Make("Goblin", oracle: "o", layout: "token", typeLine: "Token Creature — Goblin");

        Assert.Empty(LibrarySelector.Select([token], false, false, false));
    }

    // --- GroupKey -------------------------------------------------------------

    [Fact]
    public void GroupKey_FirstFaceOracleIdJoinsTheOracleGroup()
    {
        Card top = Make("Front // Back", id: "1", oracle: "same", released: "2010-01-01");
        Card faceOnly = Make("Front // Back", id: "2", firstFaceOracle: "same", released: "2005-01-01");

        Assert.Equal(LibrarySelector.GroupKey(top), LibrarySelector.GroupKey(faceOnly));
        Assert.Single(LibrarySelector.Select([top, faceOnly], false, false, false));
    }

    [Fact]
    public void GroupKey_FallsBackToName()
    {
        Card a = Make("Same Name", id: "1");
        Card b = Make("Same Name", id: "2");

        Assert.Equal("Same Name", LibrarySelector.GroupKey(a));
        Assert.Single(LibrarySelector.Select([a, b], false, false, false));
    }

    // --- Select: lands, reprints, exclusions, global --------------------------

    [Fact]
    public void Select_BasicLands_AllPrintingsKeptWithLandsOn()
    {
        Card one = Make("Plains", id: "1", oracle: "p", typeLine: "Basic Land — Plains");
        Card two = Make("Plains", id: "2", oracle: "p", typeLine: "Basic Land — Plains");

        Assert.Equal(2, LibrarySelector.Select([one, two], false, false, true).Count);
    }

    [Fact]
    public void Select_BasicLands_NoneWithLandsOff()
    {
        Card one = Make("Plains", id: "1", oracle: "p", typeLine: "Basic Land — Plains");

        Assert.Empty(LibrarySelector.Select([one], false, false, false));
    }

    [Fact]
    public void Select_WithReprints_KeepsEveryEligiblePrinting()
    {
        Card one = Make("Bolt", id: "1", oracle: "o");
        Card two = Make("Bolt", id: "2", oracle: "o", reprint: true);

        Assert.Equal(2, LibrarySelector.Select([one, two], true, false, false).Count);
    }

    [Fact]
    public void Select_NeverReturnsExcludedCards()
    {
        Card funny = Make("Silly", oracle: "o", setType: "funny");

        Assert.Empty(LibrarySelector.Select([funny], false, false, false));
    }

    [Fact]
    public void Select_DropsCreatureTokenOnANormalLayout()
    {
        Card spirit = Make("Spirit Sorcerer Token", oracle: "o", typeLine: "Token Enchantment Creature — Spirit Sorcerer", legalities: NotLegal());

        Assert.Empty(LibrarySelector.Select([spirit], false, false, false));
    }

    [Fact]
    public void Select_KeepsCreatureTokenWhenTokensAreRequested()
    {
        Card spirit = Make("Spirit Sorcerer Token", oracle: "o", typeLine: "Token Enchantment Creature — Spirit Sorcerer", legalities: NotLegal());

        Assert.Single(LibrarySelector.Select([spirit], false, true, false));
    }

    [Fact]
    public void Select_KeepsListedTokenOnATokenLayout()
    {
        Card monarch = Make("The Monarch", oracle: "o", layout: "token", typeLine: "Card", legalities: NotLegal());

        Assert.Single(LibrarySelector.Select([monarch], false, false, false));
    }

    [Fact]
    public void Select_ListedTokenTakesTheStrictPick()
    {
        // Through the fallback alone the earlier promo-set printing would win.
        Card promo = Make("Treasure", id: "1", oracle: "o", layout: "token", typeLine: "Token Artifact — Treasure", released: "2015-01-01", setType: "promo");
        Card plain = Make("Treasure", id: "2", oracle: "o", layout: "token", typeLine: "Token Artifact — Treasure", released: "2017-09-29", setType: "token");

        Assert.Same(plain, Assert.Single(LibrarySelector.Select([promo, plain], false, false, false)));
    }

    [Fact]
    public void Select_KeepsListedTokenThatExistsOnlyAsAVariant()
    {
        Card treasure = Make("Treasure", oracle: "o", layout: "token", typeLine: "Token Artifact — Treasure", borderColor: "borderless");

        Assert.Single(LibrarySelector.Select([treasure], false, false, false));
    }

    [Fact]
    public void Select_DropsUnlistedTokenOnATokenLayout()
    {
        Card blood = Make("Blood", oracle: "o", layout: "token", typeLine: "Token Artifact — Blood");

        Assert.Empty(LibrarySelector.Select([blood], false, false, false));
    }

    [Fact]
    public void Select_DropsAPrintingNotYetReleased()
    {
        Card preview = Make("Bolt", oracle: "o", released: "2027-04-09");

        Assert.Empty(LibrarySelector.Select([preview], false, false, false, new DateTime(2026, 10, 20)));
        Assert.Single(LibrarySelector.Select([preview], false, false, false));
    }

    [Fact]
    public void Select_PreviewNeverDisplacesAReleasedPrinting()
    {
        // The strict rank prefers a non-promo set, so without the cut-off the
        // unreleased plain printing would win and then be missing on disk.
        Card promo = Make("Bolt", id: "1", oracle: "o", released: "2025-01-01", setType: "promo");
        Card preview = Make("Bolt", id: "2", oracle: "o", released: "2027-04-09");

        Assert.Same(promo, Assert.Single(LibrarySelector.Select([promo, preview], false, false, false, new DateTime(2026, 10, 20))));
    }

    [Fact]
    public void Select_IsGlobal_ThenYearFilterDropsTheLaterReprintYear()
    {
        Card first = Make("Bolt", id: "1", oracle: "o", released: "2010-01-01");
        Card later = Make("Bolt", id: "2", oracle: "o", released: "2024-01-01");

        List<Card> selected = LibrarySelector.Select([first, later], false, false, false);

        Assert.Empty(GetManager.FilterByYear(selected, [2024]));
        Assert.Single(GetManager.FilterByYear(selected, [2010]));
    }

    [Fact]
    public void Select_OutputOrderIsDeterministic()
    {
        Card late = Make("Aaa", id: "1", oracle: "a", released: "2020-01-01");
        Card early = Make("Zzz", id: "2", oracle: "z", released: "2001-01-01");

        List<Card> selected = LibrarySelector.Select([late, early], false, false, false);

        Assert.Same(early, selected[0]);
        Assert.Same(late, selected[1]);
    }

    // --- WithFallback ---------------------------------------------------------

    [Fact]
    public void WithFallback_KeepsFunnySetCardThatPassesValidate()
    {
        Card funny = Make("Silly", oracle: "o", setType: "funny");

        Assert.Same(funny, Assert.Single(LibrarySelector.WithFallback([funny], false, false, false)));
    }

    [Fact]
    public void WithFallback_KeepsTwoValidPrintingsOfTheSameCard()
    {
        Card one = Make("Bolt", id: "1", oracle: "o");
        Card two = Make("Bolt", id: "2", oracle: "o");

        Assert.Equal(2, LibrarySelector.WithFallback([one, two], false, false, false).Count);
    }

    [Fact]
    public void WithFallback_AddsFallbackPickForVariantOnlyCard()
    {
        Card variant = Make("Bolt", id: "1", oracle: "o", borderColor: "borderless");

        Assert.Same(variant, Assert.Single(LibrarySelector.WithFallback([variant], false, false, false)));
    }

    [Fact]
    public void WithFallback_NoFallbackWhenGroupAlreadyHasValidMember()
    {
        Card valid = Make("Bolt", id: "1", oracle: "o");
        Card variant = Make("Bolt", id: "2", oracle: "o", borderColor: "borderless");

        Assert.Same(valid, Assert.Single(LibrarySelector.WithFallback([valid, variant], false, false, false)));
    }

    // --- CollectorDigits ------------------------------------------------------

    [Theory]
    [InlineData("123", 123)]
    [InlineData("12a", 12)]
    [InlineData("A-12", 12)]
    [InlineData("★", 0)]
    [InlineData("", 0)]
    public void CollectorDigits_ReadsTheFirstDigitRun(string collector, long expected)
    {
        Assert.Equal(expected, LibrarySelector.CollectorDigits(collector));
    }

    // --- helpers --------------------------------------------------------------

    private static Dictionary<string, string> NotLegal() => new() { ["standard"] = "not_legal", ["modern"] = "not_legal" };

    private static Card Make(
        string name,
        string id = "id",
        string oracle = "",
        string firstFaceOracle = "",
        string released = "2000-01-01",
        string collector = "1",
        string setName = "Set",
        string layout = "normal",
        string typeLine = "Instant",
        string setType = "core",
        string borderColor = "black",
        bool reprint = false,
        List<string>? promoTypes = null,
        Dictionary<string, string>? legalities = null)
    {
        JsonCard json = new()
        {
            Id = id,
            Name = name,
            OracleId = oracle,
            Language = "en",
            ReleasedAt = released,
            Layout = layout,
            TypeLine = typeLine,
            Games = ["paper"],
            FrameEffects = [],
            Reprint = reprint,
            Set = "set",
            SetName = setName,
            SetType = setType,
            BorderColor = borderColor,
            CollectorNumber = collector,
            PromoTypes = promoTypes,
            Legalities = legalities,
            ImageUris = new JsonImageUris { Png = "https://test/img.png" },
            CardFaces = firstFaceOracle.Length > 0 ? [new JsonCardFace { OracleId = firstFaceOracle }] : null,
        };

        return Card.CreateCard(json);
    }
}
