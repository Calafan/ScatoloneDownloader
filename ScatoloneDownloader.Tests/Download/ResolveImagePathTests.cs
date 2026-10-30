using ScatoloneDownloader.Download;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Download;

/// <summary>Filesystem checks (temp directory) for the target-path decision of
/// <see cref="CardDownloader.ResolveImagePath"/>.</summary>
public sealed class ResolveImagePathTests : IDisposable
{
    private readonly string dir;

    public ResolveImagePathTests()
    {
        dir = Path.Combine(Path.GetTempPath(), "ScatoloneResolve_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SkipExisting_FilePresent_ReturnsNull()
    {
        File.WriteAllBytes(Path.Combine(dir, "Bolt.png"), []);

        Assert.Null(CardDownloader.ResolveImagePath(dir, Make("Bolt", "Instant"), skipExisting: true));
    }

    [Fact]
    public void SkipExisting_FileAbsent_ReturnsUnnumberedPath()
    {
        Assert.Equal(Path.Combine(dir, "Bolt"), CardDownloader.ResolveImagePath(dir, Make("Bolt", "Instant"), skipExisting: true));
    }

    [Fact]
    public void SkipExisting_BasicLand_StillNumbers()
    {
        File.WriteAllBytes(Path.Combine(dir, "Plains.png"), []);

        Assert.Equal(Path.Combine(dir, "Plains1"), CardDownloader.ResolveImagePath(dir, Make("Plains", "Basic Land — Plains"), skipExisting: true));
    }

    [Fact]
    public void NoSkipExisting_FilePresent_ReturnsNumberedPath()
    {
        File.WriteAllBytes(Path.Combine(dir, "Bolt.png"), []);

        // A promo-treatment printing is not canonical, so it takes the numbered slot.
        Card treated = Make("Bolt", "Instant", promoTypes: ["surgefoil"]);

        Assert.Equal(Path.Combine(dir, "Bolt1"), CardDownloader.ResolveImagePath(dir, treated, skipExisting: false));
    }

    private static Card Make(string name, string typeLine, List<string>? promoTypes = null)
    {
        return Card.CreateCard(TestCards.Json(
            name, typeLine, cmc: 0, id: "id", promoTypes: promoTypes,
            set: "set", setName: "Set", setType: "core", releasedAt: "2000-01-01"));
    }
}
