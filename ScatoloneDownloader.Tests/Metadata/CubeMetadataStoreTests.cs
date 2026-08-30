using System;
using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Metadata;

/// <summary>
/// Round-trip, ordering, and rating-tier-partition coverage for
/// <see cref="CubeMetadataStore"/> (Phase 1 + Plan Part B). The store is a
/// DIRECTORY of three files — <c>pool.json</c> (rating 3-5), <c>fringe.json</c>
/// (rating 1-2), <c>unrated.json</c> (rating 0) — so besides the original
/// round-trip/ordering guarantees, <see cref="Save"/> must route every entry to
/// the right tier file by its CURRENT rating and <see cref="Load"/> must merge
/// all present tier files back into one document.
/// </summary>
public sealed class CubeMetadataStoreTests : IDisposable
{
    private readonly string tempDir;

    public CubeMetadataStoreTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ScatoloneTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // --- round-trip / ordering (unchanged behavior, now directory-based) ---

    [Fact]
    public void SaveThenLoad_RoundTripsScryfallIdStatusRatingAndEffects()
    {
        CubeMetadata data = new();
        data.Cards["oracle-1"] = new CardMetadataEntry
        {
            Name = "Lightning Bolt",
            Rating = 5,
            ScryfallId = "printing-abc-123",
            Status = "Banned",
            Effects = ["Burn"],
            ReviewedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        CubeMetadataStore.Save(tempDir, data);
        CubeMetadata loaded = CubeMetadataStore.Load(tempDir);

        CardMetadataEntry entry = Assert.Single(loaded.Cards).Value;
        Assert.Equal("Lightning Bolt", entry.Name);
        Assert.Equal(5, entry.Rating);
        Assert.Equal("printing-abc-123", entry.ScryfallId);
        Assert.Equal("Banned", entry.Status);
        Assert.Equal(CardStatus.Banned, entry.StatusValue);
        Assert.Equal(["Burn"], entry.Effects);
        Assert.Equal(CardEffect.Burn, entry.EffectFlags);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), entry.ReviewedAt);
    }

    [Fact]
    public void Save_NullScryfallIdAndStatus_OmittedFromJson_AndRoundTripToNone()
    {
        CubeMetadata data = new();
        data.Cards["oracle-1"] = new CardMetadataEntry
        {
            Name = "Plains",
            Rating = 0,
        };

        CubeMetadataStore.Save(tempDir, data);

        // Rating 0 -> unrated.json.
        string json = File.ReadAllText(Path.Combine(tempDir, "unrated.json"));
        Assert.DoesNotContain("scryfallId", json);
        Assert.DoesNotContain("\"status\"", json);
        Assert.DoesNotContain("reviewedAt", json);

        CubeMetadata loaded = CubeMetadataStore.Load(tempDir);
        CardMetadataEntry entry = Assert.Single(loaded.Cards).Value;
        Assert.Null(entry.ScryfallId);
        Assert.Null(entry.Status);
        Assert.Equal(CardStatus.None, entry.StatusValue);
    }

    [Fact]
    public void Save_OrdersEntriesWithinATierByNameThenOracleId_NotByDictionaryInsertionOrder()
    {
        CubeMetadata data = new();
        // Insert deliberately out of alphabetical order, keyed by an oracle_id
        // that would sort differently than the name, to prove name wins. All
        // default to rating 0, so all three land in the same tier (unrated.json).
        data.Cards["oracle-zzz"] = new CardMetadataEntry { Name = "Ancestral Recall" };
        data.Cards["oracle-aaa"] = new CardMetadataEntry { Name = "Black Lotus" };
        data.Cards["oracle-mmm"] = new CardMetadataEntry { Name = "Ancestral Recall" }; // tie on name

        CubeMetadataStore.Save(tempDir, data);

        List<string> nameOrder = [];
        List<string> keyOrder = [];
        foreach (KeyValuePair<string, CardMetadataEntry> kvp in CubeMetadataStore.Load(tempDir).Cards)
        {
            nameOrder.Add(kvp.Value.Name);
            keyOrder.Add(kvp.Key);
        }

        Assert.Equal(["Ancestral Recall", "Ancestral Recall", "Black Lotus"], nameOrder);
        // Tie on name -> tiebreak by oracle_id ordinal: "oracle-mmm" < "oracle-zzz".
        Assert.Equal(["oracle-mmm", "oracle-zzz", "oracle-aaa"], keyOrder);
    }

    [Fact]
    public void Save_PreservesReviewedAt_NeverReStampsOnSave()
    {
        DateTimeOffset stamped = new(2020, 5, 1, 12, 0, 0, TimeSpan.Zero);
        CubeMetadata data = new();
        data.Cards["oracle-1"] = new CardMetadataEntry { Name = "Counterspell", ReviewedAt = stamped };

        CubeMetadataStore.Save(tempDir, data);
        CubeMetadata firstLoad = CubeMetadataStore.Load(tempDir);

        // Re-save the loaded document unchanged: ReviewedAt must not drift.
        CubeMetadataStore.Save(tempDir, firstLoad);
        CubeMetadata secondLoad = CubeMetadataStore.Load(tempDir);

        Assert.Equal(stamped, Assert.Single(secondLoad.Cards).Value.ReviewedAt);
    }

    [Fact]
    public void Load_MissingDirectory_ReturnsEmptyDocument()
    {
        CubeMetadata loaded = CubeMetadataStore.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")));

        Assert.Empty(loaded.Cards);
        Assert.Equal(1, loaded.Version);
    }

    // --- Plan Part B: rating-tier partition ---------------------------------

    [Theory]
    [InlineData(0, "unrated.json")]
    [InlineData(1, "fringe.json")]
    [InlineData(2, "fringe.json")]
    [InlineData(3, "pool.json")]
    [InlineData(4, "pool.json")]
    [InlineData(5, "pool.json")]
    public void TierFileName_RoutesByRating(int rating, string expectedFile)
    {
        Assert.Equal(expectedFile, CubeMetadataStore.TierFileName(rating));
    }

    [Fact]
    public void Save_PartitionsEntriesAcrossTierFiles_ByCurrentRating()
    {
        CubeMetadata data = new();
        data.Cards["oracle-pool"] = new CardMetadataEntry { Name = "Pool Card", Rating = 4 };
        data.Cards["oracle-fringe"] = new CardMetadataEntry { Name = "Fringe Card", Rating = 2 };
        data.Cards["oracle-unrated"] = new CardMetadataEntry { Name = "Unrated Card", Rating = 0 };

        CubeMetadataStore.Save(tempDir, data);

        CubeMetadata pool = ReadTierFile("pool.json");
        CubeMetadata fringe = ReadTierFile("fringe.json");
        CubeMetadata unrated = ReadTierFile("unrated.json");

        Assert.Equal("Pool Card", Assert.Single(pool.Cards).Value.Name);
        Assert.Equal("Fringe Card", Assert.Single(fringe.Cards).Value.Name);
        Assert.Equal("Unrated Card", Assert.Single(unrated.Cards).Value.Name);
    }

    [Fact]
    public void Save_AlwaysWritesAllThreeTierFiles_EvenWhenSomeAreEmpty()
    {
        CubeMetadata data = new();
        data.Cards["oracle-1"] = new CardMetadataEntry { Name = "Only Pool Card", Rating = 5 };

        CubeMetadataStore.Save(tempDir, data);

        Assert.True(File.Exists(Path.Combine(tempDir, "pool.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, "fringe.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, "unrated.json")));
        Assert.Empty(ReadTierFile("fringe.json").Cards);
        Assert.Empty(ReadTierFile("unrated.json").Cards);
    }

    [Fact]
    public void Save_RatingChange_MovesCardBetweenTierFiles()
    {
        CubeMetadata data = new();
        data.Cards["oracle-1"] = new CardMetadataEntry { Name = "Mover", Rating = 0 };
        CubeMetadataStore.Save(tempDir, data);

        Assert.Single(ReadTierFile("unrated.json").Cards);
        Assert.Empty(ReadTierFile("pool.json").Cards);

        // Load (merge), bump the rating in memory, and save again — this is
        // exactly what the tagger does on every rating change.
        CubeMetadata reloaded = CubeMetadataStore.Load(tempDir);
        reloaded.Cards["oracle-1"].Rating = 4;
        CubeMetadataStore.Save(tempDir, reloaded);

        // The card must have moved: gone from unrated.json, now in pool.json.
        Assert.Empty(ReadTierFile("unrated.json").Cards);
        CardMetadataEntry moved = Assert.Single(ReadTierFile("pool.json").Cards).Value;
        Assert.Equal("Mover", moved.Name);
        Assert.Equal(4, moved.Rating);

        // And the merged view reflects the move too.
        CubeMetadata merged = CubeMetadataStore.Load(tempDir);
        Assert.Equal(4, Assert.Single(merged.Cards).Value.Rating);
    }

    [Fact]
    public void Load_MergesAllPresentTierFiles_IntoOneDocument()
    {
        // Simulate three independently-maintained tier files (as if written by
        // three separate Save calls over time) and confirm Load unions them.
        WriteTierFile("pool.json", ("oracle-pool", new CardMetadataEntry { Name = "Pool Card", Rating = 5 }));
        WriteTierFile("fringe.json", ("oracle-fringe", new CardMetadataEntry { Name = "Fringe Card", Rating = 1 }));
        WriteTierFile("unrated.json", ("oracle-unrated", new CardMetadataEntry { Name = "Unrated Card", Rating = 0 }));

        CubeMetadata merged = CubeMetadataStore.Load(tempDir);

        Assert.Equal(3, merged.Cards.Count);
        Assert.Equal("Pool Card", merged.Cards["oracle-pool"].Name);
        Assert.Equal("Fringe Card", merged.Cards["oracle-fringe"].Name);
        Assert.Equal("Unrated Card", merged.Cards["oracle-unrated"].Name);
    }

    [Fact]
    public void Load_ToleratesMissingIndividualTierFiles()
    {
        // Only pool.json exists; fringe.json/unrated.json are absent entirely.
        WriteTierFile("pool.json", ("oracle-pool", new CardMetadataEntry { Name = "Pool Card", Rating = 5 }));

        CubeMetadata merged = CubeMetadataStore.Load(tempDir);

        Assert.Equal("Pool Card", Assert.Single(merged.Cards).Value.Name);
    }

    [Fact]
    public void Load_DuplicateOracleIdAcrossTierFiles_FirstInPoolFringeUnratedOrderWins()
    {
        // Should never happen from normal Save (which partitions into exactly
        // one tier), but a hand-edited directory could produce it — Load must
        // still resolve deterministically rather than throwing or picking
        // arbitrarily depending on filesystem enumeration order.
        WriteTierFile("unrated.json", ("oracle-1", new CardMetadataEntry { Name = "Stale Copy", Rating = 0 }));
        WriteTierFile("pool.json", ("oracle-1", new CardMetadataEntry { Name = "Authoritative Copy", Rating = 5 }));

        CubeMetadata merged = CubeMetadataStore.Load(tempDir);

        Assert.Equal("Authoritative Copy", Assert.Single(merged.Cards).Value.Name);
    }

    [Fact]
    public void Save_UnchangedTier_SerializesByteIdentical_NoGitChurn()
    {
        CubeMetadata data = new();
        data.Cards["oracle-pool"] = new CardMetadataEntry { Name = "Stable Pool Card", Rating = 5, Effects = ["Ramp"] };
        data.Cards["oracle-unrated"] = new CardMetadataEntry { Name = "Stable Unrated Card", Rating = 0 };

        CubeMetadataStore.Save(tempDir, data);
        byte[] poolBytesBefore = File.ReadAllBytes(Path.Combine(tempDir, "pool.json"));

        // Change only the unrated entry and re-save the whole (merged) document.
        CubeMetadata reloaded = CubeMetadataStore.Load(tempDir);
        reloaded.Cards["oracle-unrated"].Label = "touched";
        CubeMetadataStore.Save(tempDir, reloaded);

        byte[] poolBytesAfter = File.ReadAllBytes(Path.Combine(tempDir, "pool.json"));
        Assert.Equal(poolBytesBefore, poolBytesAfter);
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>Reads exactly one tier file's raw content, bypassing the
    /// cross-file merge in <see cref="CubeMetadataStore.Load"/>, so tests can
    /// assert what actually landed in a specific file.</summary>
    private CubeMetadata ReadTierFile(string fileName)
    {
        string path = Path.Combine(tempDir, fileName);
        if (!File.Exists(path))
        {
            return new CubeMetadata();
        }

        string json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<CubeMetadata>(json) ?? new CubeMetadata();
    }

    private void WriteTierFile(string fileName, params (string OracleId, CardMetadataEntry Entry)[] entries)
    {
        Directory.CreateDirectory(tempDir);

        CubeMetadata data = new();
        foreach (var (oracleId, entry) in entries)
        {
            data.Cards[oracleId] = entry;
        }

        // Write this one tier file directly (not via Save, which would
        // re-partition by rating) so the test controls exactly which file the
        // entry lands in, independent of its rating.
        string json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(tempDir, fileName), json);
    }
}
