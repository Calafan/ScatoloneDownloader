using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// <see cref="CostBucket.Of"/> feeds the tagger's cost picker, whose page code
/// matches on these exact strings.
/// </summary>
public sealed class CostBucketTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(2.5, "2")]
    [InlineData(5, "5")]
    [InlineData(5.9, "5")]
    [InlineData(6, "6_Plus")]
    [InlineData(16, "6_Plus")]
    public void Of_BucketsExactToFiveThenSixPlus(double cmc, string expected)
    {
        Assert.Equal(expected, CostBucket.Of(cmc));
    }
}
