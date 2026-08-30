using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Coverage for <see cref="StatusResolver"/>: single-valued (not flags) string
/// &lt;-&gt; <see cref="CardStatus"/> conversion, unknown-safe parsing, and the
/// legacy XMP-label default mapping used once by the <c>import</c> seed command.
/// </summary>
public sealed class StatusResolverTests
{
    [Theory]
    [InlineData("Banned", CardStatus.Banned)]
    [InlineData("banned", CardStatus.Banned)]
    [InlineData("TOKEN", CardStatus.Token)]
    [InlineData("Jolly", CardStatus.Jolly)]
    [InlineData("None", CardStatus.None)]
    public void Parse_RecognizedNames_CaseInsensitive(string raw, CardStatus expected)
    {
        Assert.Equal(expected, StatusResolver.Parse(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("Red")] // raw XMP label strings are not status names
    public void Parse_BlankOrUnknown_ReturnsNone(string? raw)
    {
        Assert.Equal(CardStatus.None, StatusResolver.Parse(raw));
    }

    [Fact]
    public void ToName_None_ReturnsNull_SoJsonOmitsTheProperty()
    {
        Assert.Null(StatusResolver.ToName(CardStatus.None));
    }

    [Theory]
    [InlineData(CardStatus.Banned, "Banned")]
    [InlineData(CardStatus.Token, "Token")]
    [InlineData(CardStatus.Jolly, "Jolly")]
    public void ToName_NonNone_ReturnsCanonicalMemberName(CardStatus status, string expected)
    {
        Assert.Equal(expected, StatusResolver.ToName(status));
    }

    [Theory]
    [InlineData(CardStatus.Banned)]
    [InlineData(CardStatus.Token)]
    [InlineData(CardStatus.Jolly)]
    public void ParseToName_RoundTrips(CardStatus status)
    {
        string? name = StatusResolver.ToName(status);
        Assert.Equal(status, StatusResolver.Parse(name));
    }

    [Theory]
    [InlineData("Red", CardStatus.Banned)]
    [InlineData("red", CardStatus.Banned)]
    [InlineData("Yellow", CardStatus.Token)]
    [InlineData("Green", CardStatus.Jolly)]
    [InlineData("Blue", CardStatus.None)]
    [InlineData("", CardStatus.None)]
    [InlineData(null, CardStatus.None)]
    public void FromXmpLabel_MapsBridgeColorsToStatus(string? label, CardStatus expected)
    {
        Assert.Equal(expected, StatusResolver.FromXmpLabel(label));
    }
}
