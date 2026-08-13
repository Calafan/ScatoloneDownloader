using System;

namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// Resolves a Scryfall <c>type_line</c> to a <see cref="MacroType"/> by
    /// priority: Creature (1) > Land (2) > OtherPermanent (3) > Spell (4). The
    /// first matching bucket in priority order wins, so "Land Creature" = Creature
    /// and "Artifact Creature" = Creature.
    /// </summary>
    internal static class MacroTypeResolver
    {
        internal static MacroType Resolve(string typeLine)
        {
            if (string.IsNullOrEmpty(typeLine))
            {
                return MacroType.Spell;
            }

            StringComparison cmp = StringComparison.OrdinalIgnoreCase;

            if (typeLine.Contains("Creature", cmp))
            {
                return MacroType.Creature;
            }

            if (typeLine.Contains("Land", cmp))
            {
                return MacroType.Land;
            }

            if (typeLine.Contains("Artifact", cmp)
                || typeLine.Contains("Enchantment", cmp)
                || typeLine.Contains("Planeswalker", cmp))
            {
                return MacroType.OtherPermanent;
            }

            if (typeLine.Contains("Instant", cmp) || typeLine.Contains("Sorcery", cmp))
            {
                return MacroType.Spell;
            }

            return MacroType.Spell;
        }
    }
}