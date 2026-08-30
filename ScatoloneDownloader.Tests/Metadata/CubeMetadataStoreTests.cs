using System;
using System.Collections.Generic;
using System.IO;

using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Metadata;

/// <summary>
/// Round-trip and ordering coverage for <see cref="CubeMetadataStore"/>: this
/// file is the git-shareable source of truth (Phase 1), so status/scryfallId/
/// rating/effects/reviewedAt must all survive a Save + Load unchanged, and the
/// on-disk order must be deterministic by <c>(name, oracle_id)</c> — never by
/// insertion order — so re-saves produce minimal diffs.
/// </summary>
public sealed class CubeMetadataStoreTests : IDisposable
{
    private readonly string tempFile;

    public CubeMetadataStoreTests()
    {
        tempFile = Path.Combine(Path.GetTempPath(), "ScatoloneTests_" + Guid.NewGuid().ToString("N") + ".json");
    }

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

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

        CubeMetadataStore.Save(tempFile, data);
        CubeMetadata loaded = CubeMetadataStore.Load(tempFile);

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

        CubeMetadataStore.Save(tempFile, data);

        string json = File.ReadAllText(tempFile);
        Assert.DoesNotContain("scryfallId", json);
        Assert.DoesNotContain("\"status\"", json);
        Assert.DoesNotContain("reviewedAt", json);

        CubeMetadata loaded = CubeMetadataStore.Load(tempFile);
        CardMetadataEntry entry = Assert.Single(loaded.Cards).Value;
        Assert.Null(entry.ScryfallId);
        Assert.Null(entry.Status);
        Assert.Equal(CardStatus.None, entry.StatusValue);
    }

    [Fact]
    public void Save_OrdersEntriesByNameThenOracleId_NotByDictionaryInsertionOrder()
    {
        CubeMetadata data = new();
        // Insert deliberately out of alphabetical order, keyed by an oracle_id
        // that would sort differently than the name, to prove name wins.
        data.Cards["oracle-zzz"] = new CardMetadataEntry { Name = "Ancestral Recall" };
        data.Cards["oracle-aaa"] = new CardMetadataEntry { Name = "Black Lotus" };
        data.Cards["oracle-mmm"] = new CardMetadataEntry { Name = "Ancestral Recall" }; // tie on name

        CubeMetadataStore.Save(tempFile, data);

        List<string> nameOrder = [];
        List<string> keyOrder = [];
        foreach (KeyValuePair<string, CardMetadataEntry> kvp in CubeMetadataStore.Load(tempFile).Cards)
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

        CubeMetadataStore.Save(tempFile, data);
        CubeMetadata firstLoad = CubeMetadataStore.Load(tempFile);

        // Re-save the loaded document unchanged: ReviewedAt must not drift.
        CubeMetadataStore.Save(tempFile, firstLoad);
        CubeMetadata secondLoad = CubeMetadataStore.Load(tempFile);

        Assert.Equal(stamped, Assert.Single(secondLoad.Cards).Value.ReviewedAt);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptyDocument()
    {
        CubeMetadata loaded = CubeMetadataStore.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".json"));

        Assert.Empty(loaded.Cards);
        Assert.Equal(1, loaded.Version);
    }
}
