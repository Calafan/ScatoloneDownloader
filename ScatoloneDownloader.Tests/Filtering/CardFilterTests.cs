using System.Collections.Generic;

using ScatoloneDownloader.Filtering;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Filtering;

/// <summary>
/// Pure-logic coverage for the named-rule predicates in <see cref="CardFilter"/>.
/// No I/O, no network. Was the #1 documented testing gap in
/// <c>docs/follow-ups/2026-06-21-pre-existing-findings.md</c> ("equivalenza del
/// filtro carte: CardFilter vs il vecchio Card.IsValid").
/// </summary>
public sealed class CardFilterTests
{
    [Fact]
    public void IsDownloadable_Defaults_ExcludeBasicLands_Reprints_Tokens()
    {
        Card land = Card("Plains", layout: "normal", typeLine: "Basic Land — Plains");
        Card reprint = Card("Lightning Bolt", set: "M11", reprint: true);
        Card token = Card("Goblin Token", layout: "token");
        Card fresh = Card("Lightning Bolt", set: "LEA");

        Assert.False(CardFilter.IsDownloadable(land, downloadReprints: false, downloadTokens: false, downloadLands: false));
        Assert.False(CardFilter.IsDownloadable(reprint, downloadReprints: false, downloadTokens: false, downloadLands: false));
        Assert.False(CardFilter.IsDownloadable(token, downloadReprints: false, downloadTokens: false, downloadLands: false));
        Assert.True(CardFilter.IsDownloadable(fresh, downloadReprints: false, downloadTokens: false, downloadLands: false));
    }

    [Fact]
    public void IsDownloadable_LandsFlag_OpensBasicLands_EvenAsReprint()
    {
        // basic lands bypass the reprint gate once lands are requested — see IsDownloadable.
        Card land = Card("Plains", layout: "normal", typeLine: "Basic Land — Plains", reprint: true, borderColor: "borderless");

        Assert.True(CardFilter.IsDownloadable(land, downloadReprints: false, downloadTokens: false, downloadLands: true));
    }

    [Fact]
    public void IsDownloadable_ReprintsFlag_KeepsReprintLikeCards()
    {
        Card reprint = Card("Lightning Bolt", set: "M11", reprint: true);

        Assert.True(CardFilter.IsDownloadable(reprint, downloadReprints: true, downloadTokens: false, downloadLands: false));
    }

    [Fact]
    public void IsDownloadable_TokensFlag_KeepsTokens_ButStillDropsEmblemsSchemes()
    {
        Card token = Card("Goblin", layout: "token");
        Card emblem = Card("Emblem - Chandra", layout: "emblem");
        Card scheme = Card("Plots That Span Centuries", layout: "scheme");

        Assert.True(CardFilter.IsDownloadable(token, downloadReprints: false, downloadTokens: true, downloadLands: false));
        Assert.False(CardFilter.IsDownloadable(emblem, downloadReprints: false, downloadTokens: true, downloadLands: false));
        Assert.False(CardFilter.IsDownloadable(scheme, downloadReprints: false, downloadTokens: true, downloadLands: false));
    }

    [Theory]
    [InlineData("masters")]
    [InlineData("masterpiece")]
    [InlineData("from_the_vault")]
    [InlineData("premium_deck")]
    [InlineData("memorabilia")]
    public void HasValidSetType_RejectsSupplementarySetTypes(string setType)
    {
        Card card = Card("Sol Ring", set: "CMA", setType: setType);

        Assert.False(CardFilter.HasValidSetType(card));
    }

    [Fact]
    public void HasValidBorder_AcceptsBlackSilverBorderless_AndWhiteBorderLegacy()
    {
        Card black = Card("Lightning Bolt", borderColor: "black");
        Card silver = Card("Lightning Bolt", borderColor: "silver");
        Card borderless = Card("Lightning Bolt", borderColor: "borderless");
        Card white = Card("Lightning Bolt", borderColor: "white");
        Card legacy = Card("Sol Ring", set: "ptk", borderColor: "white");

        Assert.True(CardFilter.HasValidBorder(black));
        Assert.True(CardFilter.HasValidBorder(silver));
        Assert.True(CardFilter.HasValidBorder(borderless));
        Assert.False(CardFilter.HasValidBorder(white));
        Assert.True(CardFilter.HasValidBorder(legacy));
    }

    [Fact]
    public void IsEtched_DetectsEtchedFrameEffect()
    {
        Card plain = Card("Sol Ring", frameEffects: []);
        Card etched = Card("Sol Ring", frameEffects: ["etched"]);

        Assert.False(CardFilter.IsEtched(plain));
        Assert.True(CardFilter.IsEtched(etched));
    }

    [Fact]
    public void IsEnglish_OnlyEnPasses()
    {
        Card en = Card("Luce", language: "en");
        Card it = Card("Luce", language: "it");

        Assert.True(CardFilter.IsEnglish(en));
        Assert.False(CardFilter.IsEnglish(it));
    }

    [Fact]
    public void IsPaperGame_AcceptsPaperAndEmpty_RejectsDigital()
    {
        Card paper = Card("Lightning Bolt", games: ["paper"]);
        Card emptyGames = Card("Lightning Bolt", games: []);
        Card mtgo = Card("Lightning Bolt", games: ["mtgo", "astral"]);

        Assert.True(CardFilter.IsPaperGame(paper));
        Assert.True(CardFilter.IsPaperGame(emptyGames));
        Assert.False(CardFilter.IsPaperGame(mtgo));
    }

    [Fact]
    public void IsCollectibleBasicLand_KeepsEnglishPaperBasics_AndDropsForeignNonBasic()
    {
        Card plainsEn = Card("Plains", layout: "normal", typeLine: "Basic Land — Plains");
        Card plainsIt = Card("Pianura", layout: "normal", typeLine: "Basic Land — Plains", language: "it");
        Card plainsMtgo = Card("Plains", layout: "normal", typeLine: "Basic Land — Plains", games: ["mtgo"]);
        Card bear = Card("Grizzly Bears", typeLine: "Creature — Bear");

        Assert.True(CardFilter.IsCollectibleBasicLand(plainsEn));
        Assert.False(CardFilter.IsCollectibleBasicLand(plainsIt));
        Assert.False(CardFilter.IsCollectibleBasicLand(plainsMtgo));
        Assert.False(CardFilter.IsCollectibleBasicLand(bear));
    }

    [Fact]
    public void Validate_FiltersNullAndUndownloadable_KeepsTheRest()
    {
        List<Card?> cards =
        [
            Card("Lightning Bolt", set: "LEA"),
            null,
            Card("Plains", typeLine: "Basic Land — Plains"),
            Card("Lightning Bolt", set: "M11", reprint: true),
        ];

        List<Card> valid = CardFilter.Validate(cards, downloadReprints: false, downloadTokens: false, downloadLands: false);

        Card single = Assert.Single(valid);
        Assert.Equal("LEA", single.Set);
    }

    [Fact]
    public void ValidateBasicLands_KeepsEnglishPaperBasics_Only()
    {
        List<Card?> cards =
        [
            null,
            Card("Island", layout: "normal", typeLine: "Basic Land — Island"),
            Card("Pianura", layout: "normal", typeLine: "Basic Land — Plains", language: "it"),
            Card("Grizzly Bears", typeLine: "Creature — Bear"),
        ];

        List<Card> valid = CardFilter.ValidateBasicLands(cards);

        Card land = Assert.Single(valid);
        Assert.Equal("Island", land.Name);
    }

    // --- promo-treatment / canonical-artwork rule ---------------------------

    [Fact]
    public void IsPromoTreatment_NullOrEmptyPromoTypes_IsNotTreatment()
    {
        Card noTypes = Card("Lightning Bolt", promoTypes: null);
        Card emptyTypes = Card("Lightning Bolt", promoTypes: []);

        Assert.False(CardFilter.IsPromoTreatment(noTypes));
        Assert.False(CardFilter.IsPromoTreatment(emptyTypes));
    }

    [Fact]
    public void IsPromoTreatment_UniversesBeyondOnly_IsNotTreatment()
    {
        // "universesbeyond" is a categorical branding (Warhammer, Lord of the Rings, ...)
        // not a special foil treatment — the card art is ordinary, so it is a valid
        // canonical artwork candidate.
        Card ub = Card("Abaddon the Despoiler", promoTypes: ["universesbeyond"]);

        Assert.False(CardFilter.IsPromoTreatment(ub));
    }

    [Theory]
    [InlineData("surgefoil")]
    [InlineData("thick")]
    [InlineData("textured")]
    [InlineData("halofoil")]
    [InlineData("ripplefoil")]
    [InlineData("stepandcompleat")]
    [InlineData("gilded")]
    [InlineData("oil_slick")]
    public void IsPromoTreatment_DetectsSpecialFoilTreatments(string treatment)
    {
        Card c = Card("Abaddon the Despoiler", promoTypes: [treatment, "universesbeyond"]);

        Assert.True(CardFilter.IsPromoTreatment(c));
    }

    [Fact]
    public void IsPromoTreatment_MixedKnownAndUnknown_StillReturnsTrue_WhenAnySpecialPresent()
    {
        Card c = Card("X", promoTypes: ["mynewhypothetical", "surgefoil"]);

        Assert.True(CardFilter.IsPromoTreatment(c));
    }

    // --- canonical-artwork composite ----------------------------------------

    [Fact]
    public void IsCanonicalArtwork_PlainPrinting_Qualifies()
    {
        // The #171 printing of Abaddon: legendary frame, only "universesbeyond"
        // branding, foil finish, black border, English. Should win the canonical slot.
        Card plain = Card(
            "Abaddon the Despoiler",
            set: "40k",
            setName: "Warhammer 40,000 Commander",
            typeLine: "Legendary Creature — Traitor",
            promoTypes: ["universesbeyond"]);

        Assert.True(CardFilter.IsCanonicalArtwork(plain));
    }

    [Fact]
    public void IsCanonicalArtwork_SurgeFoilPromo_Disqualified_FromCanonicalSlot()
    {
        // The #2 printing of Abaddon: same art, but surgefoil treatment — the
        // digital render doesn't digitize cleanly, so it does NOT win canonical
        // even though it passes IsDownloadable.
        Card surgefoil = Card(
            "Abaddon the Despoiler",
            set: "40k",
            setName: "Warhammer 40,000 Commander",
            typeLine: "Legendary Creature — Traitor",
            promoTypes: ["surgefoil", "universesbeyond"]);

        // Still downloadable on its own (so --reprints can pull it to a numbered slot).
        Assert.True(CardFilter.IsDownloadable(surgefoil, downloadReprints: true, downloadTokens: false, downloadLands: false));
        // But not canonical.
        Assert.False(CardFilter.IsCanonicalArtwork(surgefoil));
    }

    [Fact]
    public void IsCanonicalArtwork_InvertedEtchedFrame_Disqualified()
    {
        // #178/#319 printings have frame_effects "inverted,etched" on top of the
        // surgefoil/thick promo treatment — disqualified by both gates.
        Card special = Card(
            "Abaddon the Despoiler",
            set: "40k",
            setName: "Warhammer 40,000 Commander",
            typeLine: "Legendary Creature — Traitor",
            promoTypes: ["thick", "surgefoil", "universesbeyond"],
            frameEffects: ["legendary", "inverted", "etched"]);

        Assert.False(CardFilter.IsCanonicalArtwork(special));
    }

    [Fact]
    public void IsCanonicalArtwork_BasicLand_Disqualified()
    {
        // Basic lands are excluded from the strict IsDownloadable gate (downloadLands=false),
        // which the composite reuses — so a basic land never wins the canonical slot
        // via the plain-card path. (Basic lands have their own dedup path.)
        Card plains = Card(
            "Plains",
            layout: "normal",
            typeLine: "Basic Land — Plains");

        Assert.False(CardFilter.IsCanonicalArtwork(plains));
    }

    // --- factory -----------------------------------------------------------

    /// <summary>
    /// Builds a single-face <see cref="Card"/> with the named filter-relevant
    /// knobs overridden — all other fields default to "obviously downloadable".
    /// </summary>
    private static Card Card(
        string name,
        string set = "LEA",
        string setName = "Limited Edition Alpha",
        string language = "en",
        string layout = "normal",
        string typeLine = "Instant",
        string setType = "core",
        string borderColor = "black",
        bool reprint = false,
        bool variation = false,
        bool textless = false,
        List<string>? games = null,
        List<string>? frameEffects = null,
        List<string>? promoTypes = null)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = language,
            ReleasedAt = "1993-08-05",
            Layout = layout,
            TypeLine = typeLine,
            Games = games ?? ["paper"],
            FrameEffects = frameEffects ?? [],
            Reprint = reprint,
            Variation = variation,
            Textless = textless,
            Set = set,
            SetName = setName,
            SetType = setType,
            BorderColor = borderColor,
            Cmc = 1,
            Colors = ["R"],
            ImageUris = new JsonImageUris { Png = "https://test/img.png" },
            PromoTypes = promoTypes,
        };

        return ScatoloneDownloader.Mtg.Card.CreateCard(json);
    }
}