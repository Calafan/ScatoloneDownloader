using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests;

/// <summary>
/// The one place the tests build a card from. Every parameter has the baseline a
/// plain paper card carries (Tempest, 1997, a 1-cost instant with a png), so a test
/// names only the fields its assertion reads; a file that needs other defaults wraps
/// these in a one-line helper instead of building its own JsonCard.
/// </summary>
internal static class TestCards
{
    /// <summary>
    /// A <see cref="JsonCard"/> with the baseline filled in. Null leaves id, oracle id,
    /// oracle text, power, toughness, keywords, collector number, promo types,
    /// legalities and faces unset. <paramref name="colors"/> sets the colour identity
    /// and, unless <paramref name="manaCost"/> is given, derives the cost from it
    /// ("{2}" for none, "{1}{X}" for a first colour X); without colours the cost is
    /// empty. A null <paramref name="png"/> drops the top-level image, which is what
    /// makes <see cref="Card.CreateCard"/> build a double-faced card.
    /// </summary>
    internal static JsonCard Json(
        string name = "Test Card",
        string typeLine = "Instant",
        string? oracleText = null,
        IEnumerable<string>? colors = null,
        string? manaCost = null,
        double cmc = 1,
        string? power = null,
        string? toughness = null,
        List<string>? keywords = null,
        string? id = null,
        string? oracleId = null,
        string set = "TMP",
        string setName = "Tempest",
        string setType = "expansion",
        string releasedAt = "1997-04-25",
        string layout = "normal",
        string language = "en",
        string borderColor = "black",
        string? collectorNumber = null,
        bool reprint = false,
        bool variation = false,
        bool textless = false,
        List<string>? games = null,
        List<string>? frameEffects = null,
        List<string>? promoTypes = null,
        Dictionary<string, string>? legalities = null,
        List<JsonCardFace>? faces = null,
        string? png = "https://test/x.png")
    {
        string[] identity = colors?.ToArray() ?? [];

        return new JsonCard
        {
            Id = id,
            OracleId = oracleId,
            Name = name,
            Language = language,
            ReleasedAt = releasedAt,
            Layout = layout,
            TypeLine = typeLine,
            OracleText = oracleText,
            Power = power,
            Toughness = toughness,
            Keywords = keywords,
            Games = games ?? ["paper"],
            FrameEffects = frameEffects ?? [],
            Reprint = reprint,
            Variation = variation,
            Textless = textless,
            Set = set,
            SetName = setName,
            SetType = setType,
            BorderColor = borderColor,
            Cmc = cmc,
            ColorIdentity = [.. identity],
            ManaCost = manaCost ?? (colors == null ? "" : identity.Length == 0 ? "{2}" : "{1}{" + identity[0] + "}"),
            PromoTypes = promoTypes,
            Legalities = legalities,
            CollectorNumber = collectorNumber,
            CardFaces = faces,
            ImageUris = png == null ? null : new JsonImageUris { Png = png },
        };
    }

    /// <summary>
    /// A <see cref="Card"/> built from <see cref="Json"/> (same baseline, the most-used
    /// fields as parameters) with the cube metadata set on top: rating, status,
    /// effects and reviewed.
    /// </summary>
    internal static Card Make(
        string name = "Test Card",
        string typeLine = "Instant",
        string? oracleText = null,
        IEnumerable<string>? colors = null,
        string? manaCost = null,
        double cmc = 1,
        string? power = null,
        string? toughness = null,
        List<string>? keywords = null,
        string? id = null,
        string? oracleId = null,
        string set = "TMP",
        string setName = "Tempest",
        string setType = "expansion",
        string releasedAt = "1997-04-25",
        int rating = 0,
        CardStatus status = CardStatus.None,
        CardEffect effects = CardEffect.None,
        bool reviewed = false)
    {
        Card card = Card.CreateCard(Json(
            name, typeLine, oracleText, colors, manaCost, cmc, power, toughness, keywords, id, oracleId,
            set: set, setName: setName, setType: setType, releasedAt: releasedAt));
        card.Rating = rating;
        card.Status = status;
        card.Effects = effects;
        card.Reviewed = reviewed;
        return card;
    }
}
