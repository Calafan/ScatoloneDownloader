using System.Collections.Generic;

using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Phase 0 TDD: ColorCategory classifier — maps color_identity to cube-design
/// buckets: monocolor (W/U/B/R/G), Colorless, Lands, 10 Guilds, 10 Shards/Wedges,
/// 4-5 Colors.
/// </summary>
public sealed class ColorCategoryTests
{
    [Theory]
    [InlineData(new string[] { "W" }, "W")]
    [InlineData(new string[] { "U" }, "U")]
    [InlineData(new string[] { "B" }, "B")]
    [InlineData(new string[] { "R" }, "R")]
    [InlineData(new string[] { "G" }, "G")]
    public void Classify_Monocolor_ReturnsSingleLetter(string[] identity, string expected)
    {
        Assert.Equal(expected, ColorCategoryClassifier.Classify(identity));
    }

    [Theory]
    [InlineData(new string[] { "W", "U" }, "WU")]
    [InlineData(new string[] { "U", "W" }, "WU")]
    [InlineData(new string[] { "U", "B" }, "UB")]
    [InlineData(new string[] { "B", "R" }, "BR")]
    [InlineData(new string[] { "R", "G" }, "RG")]
    [InlineData(new string[] { "G", "W" }, "GW")]
    [InlineData(new string[] { "W", "B" }, "WB")]
    [InlineData(new string[] { "U", "R" }, "UR")]
    [InlineData(new string[] { "B", "G" }, "BG")]
    [InlineData(new string[] { "R", "W" }, "RW")]
    [InlineData(new string[] { "G", "U" }, "GU")]
    public void Classify_TwoColors_OrderedGuildCode(string[] identity, string expected)
    {
        Assert.Equal(expected, ColorCategoryClassifier.Classify(identity));
    }

    [Theory]
    [InlineData(new string[] { "W", "U", "B" }, "WUB")]
    [InlineData(new string[] { "U", "B", "R" }, "UBR")]
    [InlineData(new string[] { "B", "R", "G" }, "BRG")]
    [InlineData(new string[] { "R", "G", "W" }, "RGW")]
    [InlineData(new string[] { "G", "W", "U" }, "GWU")]
    [InlineData(new string[] { "W", "B", "G" }, "WBG")]
    [InlineData(new string[] { "U", "R", "W" }, "URW")]
    [InlineData(new string[] { "B", "U", "G" }, "BUG")]
    [InlineData(new string[] { "R", "W", "B" }, "RWB")]
    [InlineData(new string[] { "G", "U", "R" }, "GUR")]
    public void Classify_ThreeColors_OrderedShardOrWedge(string[] identity, string expected)
    {
        Assert.Equal(expected, ColorCategoryClassifier.Classify(identity));
    }

    [Theory]
    [InlineData(new string[] { "W", "U", "B", "R" }, "4_5_Colors")]
    [InlineData(new string[] { "W", "U", "B", "R", "G" }, "4_5_Colors")]
    public void Classify_FourOrFiveColors_GroupedTogether(string[] identity, string expected)
    {
        Assert.Equal(expected, ColorCategoryClassifier.Classify(identity));
    }

    [Fact]
    public void Classify_Empty_ReturnsColorless()
    {
        Assert.Equal("Colorless", ColorCategoryClassifier.Classify([]));
    }

    [Fact]
    public void Classify_Null_ReturnsColorless()
    {
        Assert.Equal("Colorless", ColorCategoryClassifier.Classify(null!));
    }
}