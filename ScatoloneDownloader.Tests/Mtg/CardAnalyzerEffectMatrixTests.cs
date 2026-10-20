using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Metadata;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Covers the effect-by-colour matrix, the reviewed share, the answers-vs-targets
/// groups and their rendering in the cube report, plus the
/// <see cref="Card.Reviewed"/> flag the metadata sync feeds them.
/// </summary>
public sealed class CardAnalyzerEffectMatrixTests : IDisposable
{
    private readonly string tempFile = Path.Combine(Path.GetTempPath(), "ScatoloneMatrix_" + Guid.NewGuid().ToString("N") + ".md");

    public void Dispose()
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ColumnMapping_MonoGuildColorlessAndFiveColor_LandInTheirColumns()
    {
        List<Card> cards =
        [
            MakeCard("Mono W", ["W"], "Instant", CardEffect.Removal),
            MakeCard("Guild WU", ["W", "U"], "Instant", CardEffect.Removal),
            MakeCard("Colorless Rock", [], "Artifact", CardEffect.Removal),
            MakeCard("Four Colors", ["W", "U", "B", "R"], "Instant", CardEffect.Removal),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();

        Assert.Equal(1, report.CardsByColumn["W"]);
        Assert.Equal(2, report.CardsByColumn["Multi"]);
        Assert.Equal(1, report.CardsByColumn["Colorless"]);
        Assert.False(report.CardsByColumn.ContainsKey("U"));
        Assert.Equal(1, report.EffectCountsByColumn["W"][CardEffect.Removal]);
        Assert.Equal(2, report.EffectCountsByColumn["Multi"][CardEffect.Removal]);
        Assert.Equal(1, report.EffectCountsByColumn["Colorless"][CardEffect.Removal]);
    }

    [Fact]
    public void ReviewedAndUntaggedByColumn_CountPerColumn()
    {
        List<Card> cards =
        [
            MakeCard("Reviewed W", ["W"], "Instant", CardEffect.Removal, reviewed: true),
            MakeCard("Proposed W", ["W"], "Instant", CardEffect.Ramp),
            MakeCard("Untagged W", ["W"], "Instant", CardEffect.None),
            MakeCard("Reviewed Untagged U", ["U"], "Instant", CardEffect.None, reviewed: true),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();

        Assert.Equal(3, report.CardsByColumn["W"]);
        Assert.Equal(1, report.ReviewedByColumn["W"]);
        Assert.Equal(1, report.UntaggedByColumn["W"]);
        Assert.Equal(1, report.ReviewedByColumn["U"]);
        Assert.Equal(1, report.UntaggedByColumn["U"]);
    }

    [Fact]
    public void AnswerGroups_CountDistinctCards_NotFlagSums()
    {
        List<Card> cards =
        [
            MakeCard("Removal And Wipe", ["R"], "Sorcery", CardEffect.Removal | CardEffect.Wipe),
            MakeCard("Exile Anything", ["W"], "Instant", CardEffect.RemovePermanent),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();

        AnswerGroupCount creatureRemoval = report.AnswerGroups.Single(g => g.Label == "Creature removal");
        AnswerGroupCount artifactRemoval = report.AnswerGroups.Single(g => g.Label == "Artifact/enchantment removal");

        // Two flags of the group on one card: still one card.
        Assert.Equal(2, creatureRemoval.Total);
        Assert.Equal(1, creatureRemoval.ByColumn["R"]);
        Assert.Equal(1, creatureRemoval.ByColumn["W"]);

        // RemovePermanent answers both families.
        Assert.Equal(1, artifactRemoval.Total);
        Assert.Equal(1, artifactRemoval.ByColumn["W"]);
    }

    [Fact]
    public void AnswerGroups_ArtifactEnchantmentTargets_ExcludeCreaturesAndLands()
    {
        List<Card> cards =
        [
            MakeCard("Golem", ["W"], "Artifact Creature — Golem"),
            MakeCard("Rock", [], "Artifact"),
            MakeCard("Aura", ["U"], "Enchantment — Aura"),
            MakeCard("Bear", ["G"], "Creature — Bear"),
            MakeCard("Bolt", ["R"], "Instant"),
            MakeCard("Artifact Land", [], "Artifact Land"),
        ];

        AnalysisReport report = new CardAnalyzer(cards).Analyze();

        Assert.Equal(2, report.AnswerGroups.Single(g => g.Label == "Artifact/enchantment removal").Targets);
        Assert.Equal(2, report.AnswerGroups.Single(g => g.Label == "Creature removal").Targets);
        Assert.Equal(2, report.AnswerGroups.Single(g => g.Label == "Soft creature answers").Targets);
        Assert.Null(report.AnswerGroups.Single(g => g.Label == "Counterspells").Targets);
        Assert.Equal(1, report.AnswerGroups.Single(g => g.Label == "Land destruction").Targets);
    }

    [Fact]
    public void SyncFromJson_ReviewedAt_SetsReviewed_AndMissingEntryResetsIt()
    {
        Card reviewed = MakeCard("Reviewed", ["W"], "Instant", oracleId: "oracle-reviewed");
        Card proposed = MakeCard("Proposed", ["W"], "Instant", oracleId: "oracle-proposed");
        Card gone = MakeCard("Gone", ["W"], "Instant", oracleId: "oracle-gone");
        gone.Reviewed = true;

        CubeMetadata data = new();
        data.Cards["oracle-reviewed"] = new CardMetadataEntry { ReviewedAt = DateTimeOffset.UtcNow };
        data.Cards["oracle-proposed"] = new CardMetadataEntry { ReviewedAt = null };

        MetadataJsonSynchronizer.SyncFromJson([reviewed, proposed, gone], data);

        Assert.True(reviewed.Reviewed);
        Assert.False(proposed.Reviewed);
        Assert.False(gone.Reviewed);
    }

    [Fact]
    public void SaveAnalysis_WithEffects_RendersColorMatrixAndAnswersTable()
    {
        List<Card> cards =
        [
            MakeCard("White Removal", ["W"], "Instant", CardEffect.Removal, reviewed: true, rating: 4),
            MakeCard("Bench Ramp", ["G"], "Sorcery", CardEffect.Ramp, rating: 2),
        ];

        CardAnalyzer.ForPool(cards).SaveAnalysis(tempFile);
        string text = File.ReadAllText(tempFile);

        Assert.Contains("## 5. Effects by Color (Excluding Lands)", text);
        Assert.Contains("| Effect | White | Blue | Black | Red | Green | Multi | Colorless | Total |", text);
        Assert.Contains("| **Reviewed** | 1 (100%) | - | - | - | - | - | - | 1 (100%) |", text);
        Assert.Contains("| Removal | 1 (100%) | - | - | - | - | - | - | 1 (100%) |", text);
        Assert.Contains("## 6. Answers vs Targets", text);
        Assert.Contains("| Answers | Tags | White | Blue | Black | Red | Green | Multi | Colorless | Total | Targets | Targets per answer |", text);
        Assert.Contains("| **Counterspells** | Counter | - | - | - | - | - | - | - | - | - | - |", text);
        Assert.Contains("## 7. Bench Availability", text);
        Assert.Contains("### Available by Effect and Color", text);
        Assert.Contains("| Ramp | - | - | - | - | 1 (100%) | - | - | 1 (100%) |", text);
    }

    [Fact]
    public void SaveAnalysis_TargetsPerAnswer_IsTargetsOverAnsweringCards()
    {
        // One removal spell against three creatures: 3 targets per answer, not 0,3.
        // Integral on purpose, so the assertion holds in any culture.
        List<Card> cards =
        [
            MakeCard("White Removal", ["W"], "Instant", CardEffect.Removal, rating: 4),
            MakeCard("Bear One", ["G"], "Creature — Bear", rating: 3),
            MakeCard("Bear Two", ["G"], "Creature — Bear", rating: 3),
            MakeCard("Bear Three", ["G"], "Creature — Bear", rating: 3),
        ];

        CardAnalyzer.ForPool(cards).SaveAnalysis(tempFile);
        string text = File.ReadAllText(tempFile);

        Assert.Contains("| **Creature removal** | Removal, RemovePermanent, Wipe | 1 | - | - | - | - | - | - | 1 | 3 | 3 |", text);
    }

    [Fact]
    public void SaveAnalysis_NoEffectAnywhere_OmitsTheAnswersSectionAndTheColorMatrix()
    {
        List<Card> cards = [MakeCard("Plain", ["W"], "Instant", rating: 4)];

        CardAnalyzer.ForPool(cards).SaveAnalysis(tempFile);
        string text = File.ReadAllText(tempFile);

        Assert.DoesNotContain("## 6. Answers vs Targets", text);
        Assert.DoesNotContain("Effects by Color", text);
    }

    private static Card MakeCard(
        string name,
        string[] colors,
        string typeLine,
        CardEffect effects = CardEffect.None,
        bool reviewed = false,
        int rating = 4,
        string? oracleId = null)
    {
        JsonCard json = new()
        {
            Name = name,
            OracleId = oracleId,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = typeLine,
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 2,
            ColorIdentity = [.. colors],
            ManaCost = colors.Length == 0 ? "{2}" : "{1}{" + colors[0] + "}",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        Card card = Card.CreateCard(json);
        card.Rating = rating;
        card.Status = CardStatus.None;
        card.Effects = effects;
        card.Reviewed = reviewed;
        return card;
    }
}
