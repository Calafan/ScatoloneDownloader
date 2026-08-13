using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Phase 0 TDD: MacroType priority resolver — Creature > Land > OtherPermanent
/// > Spell. Maps Scryfall TypeLine to one of four cube-design categories.
/// </summary>
public sealed class MacroTypeTests
{
    [Theory]
    [InlineData("Creature — Goblin", MacroType.Creature)]
    [InlineData("Creature — Angel", MacroType.Creature)]
    [InlineData("Legendary Creature — Traitor", MacroType.Creature)]
    [InlineData("Land — Plains", MacroType.Land)]
    [InlineData("Basic Land — Island", MacroType.Land)]
    [InlineData("Artifact", MacroType.OtherPermanent)]
    [InlineData("Artifact Creature — Construct", MacroType.Creature)]
    [InlineData("Enchantment", MacroType.OtherPermanent)]
    [InlineData("Enchantment — Aura", MacroType.OtherPermanent)]
    [InlineData("Planeswalker — Chandra", MacroType.OtherPermanent)]
    [InlineData("Instant", MacroType.Spell)]
    [InlineData("Instant — Arcane", MacroType.Spell)]
    [InlineData("Sorcery", MacroType.Spell)]
    [InlineData("Sorcery — Arcane", MacroType.Spell)]
    public void Resolve_PriorityOrder_CreatureBeatsLandAndArtifact(string typeLine, MacroType expected)
    {
        Assert.Equal(expected, MacroTypeResolver.Resolve(typeLine));
    }

    [Fact]
    public void Resolve_LandCreature_ResolvesToCreature()
    {
        // Dryad Arbor: "Land Creature — Forest Dryad" — Creature wins per priority.
        Assert.Equal(MacroType.Creature, MacroTypeResolver.Resolve("Land Creature — Forest Dryad"));
    }

    [Fact]
    public void Resolve_TribalInstant_ResolvesToSpell()
    {
        Assert.Equal(MacroType.Spell, MacroTypeResolver.Resolve("Tribal Instant — Goblin"));
    }

    [Fact]
    public void Resolve_CreatureArtifactChangeling_ResolvesToCreature()
    {
        // Shapeshifter: "Creature — Shapeshifter" despite being artifact-like.
        Assert.Equal(MacroType.Creature, MacroTypeResolver.Resolve("Artifact Creature — Shapeshifter"));
    }

    [Fact]
    public void Resolve_EmptyOrUnknown_ReturnsSpell()
    {
        // Fallback: cards with no recognizable type go to Spell (lowest priority).
        Assert.Equal(MacroType.Spell, MacroTypeResolver.Resolve(""));
        Assert.Equal(MacroType.Spell, MacroTypeResolver.Resolve("Conspiracy"));
    }
}