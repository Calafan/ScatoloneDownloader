using System.Collections.Generic;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Validates the rule-based <see cref="EffectClassifier"/> against real card
/// oracle text. The classifier only PROPOSES (a human confirms), so these pin
/// the high-confidence cases and a couple of deliberate limitations (e.g.
/// Regrowth returning to hand is not caught) rather than every card.
/// </summary>
public sealed class EffectClassifierTests
{
    [Theory]
    // name, typeLine, oracleText, expected flags
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.", CardEffect.Burn)]
    [InlineData("Wrath of God", "Sorcery", "Destroy all creatures. They can't be regenerated.", CardEffect.Wipe)]
    [InlineData("Counterspell", "Instant", "Counter target spell.", CardEffect.Counter)]
    [InlineData("Swords to Plowshares", "Instant", "Exile target creature. Its controller gains life equal to its power.", CardEffect.Removal)]
    [InlineData("Vindicate", "Sorcery", "Destroy target permanent.", CardEffect.RemovePermanent)]
    [InlineData("Demonic Tutor", "Sorcery", "Search your library for a card, then shuffle and put that card into your hand.", CardEffect.Tutor)]
    [InlineData("Worldly Tutor", "Instant", "Search your library for a creature card, reveal that card, then shuffle and put it on top.", CardEffect.Tutor)]
    [InlineData("Sol Ring", "Artifact", "{T}: Add {C}{C}.", CardEffect.Ramp)]
    [InlineData("Pacifism", "Enchantment — Aura", "Enchant creature. Enchanted creature can't attack or block.", CardEffect.Pacify)]
    [InlineData("Icy Manipulator", "Artifact", "{1}, {T}: Tap target artifact, creature, or land.", CardEffect.Pacify)]
    [InlineData("Reanimate", "Sorcery", "Return target creature card from your graveyard to the battlefield. You lose life equal to its mana value.", CardEffect.Reanimate)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Mind Rot", "Sorcery", "Target player discards two cards.", CardEffect.Discard)]
    [InlineData("Divination", "Sorcery", "Draw two cards.", CardEffect.CardAdvantage)]
    [InlineData("Control Magic", "Enchantment — Aura", "Enchant creature. You control enchanted creature.", CardEffect.Steal)]
    public void Classify_ExactMatch_ForHighConfidenceCards(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

    [Fact]
    public void Classify_BirdsOfParadise_RampAndManaFixing()
    {
        Card card = MakeCard("Birds of Paradise", "Creature — Bird", "Flying\n{T}: Add one mana of any color.", keywords: ["Flying"]);

        Assert.Equal(CardEffect.Ramp | CardEffect.ManaFixing, EffectClassifier.Classify(card));
    }

    [Fact]
    public void Classify_DualLand_IsManaFixing_NotRamp()
    {
        // A land tapping for its own (fixing) mana must be ManaFixing but NOT Ramp.
        Card card = MakeCard("Tundra", "Land", "{T}: Add {W} or {U}.");

        CardEffect result = EffectClassifier.Classify(card);

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Ramp));
    }

    [Fact]
    public void Classify_HexproofKeyword_WithNoRulesText_IsProtection()
    {
        Card card = MakeCard("Slippery Bogle", "Creature — Beast", "", keywords: ["Hexproof"]);

        Assert.Equal(CardEffect.Protection, EffectClassifier.Classify(card));
    }

    [Fact]
    public void Classify_VanillaCreature_IsNone()
    {
        Card card = MakeCard("Grizzly Bears", "Creature — Bear", "");

        Assert.Equal(CardEffect.None, EffectClassifier.Classify(card));
    }

    [Fact]
    public void Classify_Regrowth_ReturnToHand_IsNotCaught_KnownLimitation()
    {
        // Regrowth returns a card to HAND (not the battlefield), which the
        // Reanimate rules deliberately don't match; a human tags it. Documented
        // so a future rule change is a conscious decision, not an accident.
        Card card = MakeCard("Regrowth", "Sorcery", "Return target card from your graveyard to your hand.");

        Assert.Equal(CardEffect.None, EffectClassifier.Classify(card));
    }

    private static Card MakeCard(string name, string typeLine, string oracleText, List<string>? keywords = null)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = typeLine,
            OracleText = oracleText,
            Keywords = keywords,
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 1,
            Colors = [],
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
