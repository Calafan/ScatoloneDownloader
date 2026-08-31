using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Pins the single rating-boundary source of truth (<see cref="RatingTierClassifier"/>)
/// and guards that metadata storage agrees with it — the review flagged the 3/1
/// thresholds being duplicated between <see cref="CubeMetadataStore.TierFileName"/>
/// and view generation. (Tier is compared by name because the enum is internal
/// and can't appear in a public xUnit theory signature.)
/// </summary>
public sealed class RatingTierTests
{
    [Theory]
    [InlineData(0, "Unrated")]
    [InlineData(1, "Fringe")]
    [InlineData(2, "Fringe")]
    [InlineData(3, "Pool")]
    [InlineData(4, "Pool")]
    [InlineData(5, "Pool")]
    [InlineData(-1, "Unrated")] // defensive: out-of-range falls back to unrated
    [InlineData(99, "Pool")]
    public void Classify_MapsRatingToTier(int rating, string expectedTier)
    {
        Assert.Equal(expectedTier, RatingTierClassifier.Classify(rating).ToString());
    }

    [Theory]
    [InlineData(0, "unrated.json")]
    [InlineData(1, "fringe.json")]
    [InlineData(2, "fringe.json")]
    [InlineData(3, "pool.json")]
    [InlineData(4, "pool.json")]
    [InlineData(5, "pool.json")]
    public void TierFileName_AgreesWithClassifier(int rating, string expectedFile)
    {
        // Storage routing must follow the same boundaries as the shared classifier.
        Assert.Equal(expectedFile, CubeMetadataStore.TierFileName(rating));

        string expectedFromTier = RatingTierClassifier.Classify(rating).ToString() switch
        {
            "Pool" => "pool.json",
            "Fringe" => "fringe.json",
            _ => "unrated.json",
        };
        Assert.Equal(expectedFromTier, CubeMetadataStore.TierFileName(rating));
    }
}
