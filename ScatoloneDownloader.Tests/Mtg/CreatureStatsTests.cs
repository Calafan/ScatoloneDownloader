using System.Text.Json;

using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// The creature facts behind the creature profile: power/toughness read from the
/// Scryfall JSON, their integer parsing, and the evasion definition of
/// <c>docs/cube-targets.md</c>.
/// </summary>
public sealed class CreatureStatsTests
{
    [Fact]
    public void Card_ReadsPowerAndToughnessFromTheTopLevel()
    {
        Card card = FromJson("""
            {"name":"Grizzly Bears","released_at":"1993-08-05","type_line":"Creature — Bear",
             "power":"2","toughness":"2","image_uris":{"png":"https://test/bears.png"}}
            """);

        Assert.Equal("2", card.Power);
        Assert.Equal("2", card.Toughness);
    }

    [Fact]
    public void Card_TransformCardReadsTheFrontFace_NotTheFirstFaceThatHasOne()
    {
        // The front face has no power (an artifact side): the back face's 3/3 must
        // not leak into the card.
        Card card = FromJson("""
            {"name":"Front // Back","released_at":"2017-09-29","type_line":"Artifact // Creature — Bear",
             "card_faces":[
               {"name":"Front","type_line":"Artifact"},
               {"name":"Back","type_line":"Creature — Bear","power":"3","toughness":"3"}]}
            """);

        Assert.Equal(string.Empty, card.Power);
        Assert.Equal(string.Empty, card.Toughness);

        Card creatureFront = FromJson("""
            {"name":"Delver of Secrets // Insectile Aberration","released_at":"2011-09-30",
             "type_line":"Creature — Human Wizard // Creature — Human Insect",
             "card_faces":[
               {"name":"Delver of Secrets","power":"1","toughness":"1"},
               {"name":"Insectile Aberration","power":"3","toughness":"2"}]}
            """);

        Assert.Equal("1", creatureFront.Power);
        Assert.Equal("1", creatureFront.Toughness);
    }

    [Fact]
    public void Card_WithNoPowerAnywhere_HasEmptyStrings()
    {
        Card card = FromJson("""
            {"name":"Lightning Bolt","released_at":"1993-08-05","type_line":"Instant",
             "image_uris":{"png":"https://test/bolt.png"}}
            """);

        Assert.Equal(string.Empty, card.Power);
        Assert.Equal(string.Empty, card.Toughness);
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    [InlineData("12", 12)]
    [InlineData("*", null)]
    [InlineData("1+*", null)]
    [InlineData("X", null)]
    [InlineData("", null)]
    [InlineData("-", null)]
    [InlineData("1.5", null)]
    [InlineData("+1", null)]
    public void ParseStat_ReadsPlainIntegersOnly(string value, int? expected)
    {
        Assert.Equal(expected, CreatureStats.ParseStat(value));
    }

    [Theory]
    [InlineData("Flying")]
    [InlineData("Menace")]
    [InlineData("Trample")]
    [InlineData("Shadow")]
    [InlineData("Horsemanship")]
    [InlineData("Fear")]
    [InlineData("Intimidate")]
    [InlineData("Skulk")]
    public void IsEvasive_EachEvasionKeyword(string keyword)
    {
        Assert.True(CreatureStats.IsEvasive(MakeCreature([keyword], "")));
    }

    [Fact]
    public void IsEvasive_CantBeBlockedInTheText()
    {
        Assert.True(CreatureStats.IsEvasive(MakeCreature([], "This creature can't be blocked.")));
    }

    [Fact]
    public void IsEvasive_CantBlockIsNotEvasion_AndAPlainCreatureIsNot()
    {
        Assert.False(CreatureStats.IsEvasive(MakeCreature([], "This creature can't block.")));
        Assert.False(CreatureStats.IsEvasive(MakeCreature(["Vigilance", "Haste"], "")));
    }

    [Fact]
    public void HasteAndDefender_ReadTheKeywords()
    {
        Card card = MakeCreature(["Haste", "Defender"], "");

        Assert.True(CreatureStats.IsHaste(card));
        Assert.True(CreatureStats.IsDefender(card));
        Assert.False(CreatureStats.IsHaste(MakeCreature([], "")));
        Assert.False(CreatureStats.IsDefender(MakeCreature([], "")));
    }

    private static Card FromJson(string json)
    {
        JsonCard card = JsonSerializer.Deserialize<JsonCard>(json)!;

        return Card.CreateCard(card);
    }

    private static Card MakeCreature(List<string> keywords, string oracleText) =>
        TestCards.Make("Test Creature", "Creature — Bear", oracleText, cmc: 0, keywords: keywords, releasedAt: "2020-01-01");
}
