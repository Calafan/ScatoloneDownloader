namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// One short line per <see cref="CardEffect"/>, shown as the tooltip on the
    /// tagger's effect buttons.
    /// <para>
    /// The rulings live in <see cref="CardEffect"/>'s own comments, but those are
    /// paragraphs and they are in the source tree — which is exactly where they
    /// are no use, because the moment a ruling is needed is the moment a card is
    /// on screen and a key is about to be pressed. Tagging tens of thousands of
    /// cards is repetitive, and the slips it produces (a self-pump tagged Buff, a
    /// self-mill tagged Mill) are all cases where the boundary was known and not
    /// in front of anyone. This is that boundary, at the point of use.
    /// </para>
    /// <para>
    /// Each line therefore leads with what the tag IS and then names the trap —
    /// the shape that looks like the tag and is not. Keep them to a sentence or
    /// two: a tooltip nobody finishes reading is a tooltip nobody reads.
    /// </para>
    /// </summary>
    internal static class EffectGlossary
    {
        private static readonly Dictionary<CardEffect, string> Descriptions = new()
        {
            [CardEffect.Tokens] =
                "Puts CREATURE bodies on the board, and yours. A Treasure, Clue, Food or Lander is a resource, not a body; "
                + "a token an opponent creates is not yours. Earthbend and \"all lands are 1/1 creatures\" do count.",

            [CardEffect.Removal] =
                "Answers one creature or planeswalker: destroy it, exile it, point damage at it, fight it, make its "
                + "controller sacrifice it, or shrink its toughness to nothing (-3/-3, a -1/-1 counter). A \"-2/-0\" "
                + "leaves it standing, so that is Pacify; \"each creature gets -1/-1\" is a Wipe.",

            [CardEffect.Counter] =
                "Answers a spell on the stack by countering it.",

            [CardEffect.RemovePermanent] =
                "Answers ANY permanent — \"destroy/exile target (nonland) permanent\", including the O-ring family. The "
                + "breadth is the point, so it stays this tag even when you would usually aim it at a creature.",

            [CardEffect.Wipe] =
                "Mass removal: destroys, exiles or damages every creature at once.",

            [CardEffect.Bounce] =
                "Returns a NAMED permanent to its owner's hand (\"return target …\", or a mass \"return each/all\"). "
                + "Returning itself as a cost or an end-step drawback is a price the card pays, not an answer.",

            [CardEffect.Ramp] =
                "More mana, or sooner: a mana ability on a creature or rock, a land put onto the battlefield, a "
                + "permanent that spends itself for mana (Black Lotus, Blood Pet), a ritual. A land tapping for its "
                + "own single mana is just a land, and a land that enters tapped never counts.",

            [CardEffect.Disenchant] =
                "Destroys or exiles a targeted artifact or enchantment.",

            [CardEffect.Discard] =
                "Empties somebody ELSE'S hand. A bare \"discard a card\" is a cost you pay — madness, blitz, cycling, the "
                + "back half of a loot — and paying it attacks nobody.",

            [CardEffect.CardAdvantage] =
                "A card the opponent does not get: draw two or more, a draw you can go back to (an activated ability or a "
                + "recurring trigger), or \"draw a card for each X\". A loot, a cycle, an ETB cantrip and a one-shot "
                + "sacrifice are all parity. One Clue or one card off the top is a rider; repeatable, or two cards at "
                + "once, is a card.",

            [CardEffect.Filter] =
                "Selection without net cards: scry, surveil, look at the top few, or a loot that draws and discards.",

            [CardEffect.Reanimate] =
                "Returns a creature from a graveyard straight to the BATTLEFIELD, cheating its cost.",

            [CardEffect.Buff] =
                "Raises power and/or toughness for something OTHER than the card itself, in either vocabulary: "
                + "+2/+2 until end of turn, or a +1/+1 counter. One counter on one creature counts. A creature that "
                + "pumps only itself has a stat line, not an effect, and counters on THEIR creatures help them.",

            [CardEffect.Protection] =
                "Keeps something ELSE alive, and can be held up in response. Two vocabularies count equally: a granted "
                + "keyword shield (hexproof, indestructible, ward), and damage prevention. Not a fog, not a shield the "
                + "card puts on itself, and not preventing the damage a creature DEALS — that is Pacify.",

            [CardEffect.Burn] =
                "Damage aimed at a FACE. Damage aimed at a creature is Removal instead; \"any target\" is both. A sweeper "
                + "counts only if it catches the players too. Damage to YOURSELF is a price the card charges.",

            [CardEffect.Sacrifice] =
                "An outlet you can feed your OWN creatures, artifacts or permanents at will — the half of the combo that "
                + "makes a stolen creature worth taking. Not an edict (that empties their board), not a land, and not "
                + "\"as an additional cost to cast\", which pays once.",

            [CardEffect.Steal] =
                "Takes control of a permanent somebody else controls, for a turn or for good.",

            [CardEffect.Tutor] =
                "Searches the library for a specific nonland card. A land search is Ramp or ManaFixing.",

            [CardEffect.ManaFixing] =
                "Fixes colours: mana of any colour, or a choice between two. Distinct from Ramp, and a card can be both.",

            [CardEffect.Pacify] =
                "Neutralises a creature without killing it: can't attack or block, tapped down, detained, locked from "
                + "untapping, or its combat damage prevented. A card that only taxes ITSELF neutralises nobody.",

            [CardEffect.LandDestruction] =
                "Destroys or exiles lands. Deliberately narrow: the wider mana-denial family (Winter Orb, Blood Moon, "
                + "Spheres) stays untagged.",

            [CardEffect.Mill] =
                "Puts cards from ANOTHER player's library into their graveyard. Filling your own graveyard is fuel for "
                + "what the card does next, not an attack — dredge is a graveyard card, not a mill card.",

            [CardEffect.Regrowth] =
                "Returns a card from a graveyard to HAND. Straight to the battlefield is Reanimate instead.",

            [CardEffect.Redirect] =
                "Acts on a spell already on the stack WITHOUT countering it: changes its target, or copies it.",
        };

        /// <summary>The line for one effect, or an empty string when a member has
        /// no entry yet — a missing tooltip must never break the page.</summary>
        internal static string Describe(CardEffect effect) =>
            Descriptions.TryGetValue(effect, out string? text) ? text : string.Empty;

        /// <summary>The lines for the supplied effect NAMES, in the same order, so
        /// the page can index them alongside the names it already receives.</summary>
        internal static string[] DescribeAll(IEnumerable<string> effectNames) =>
            [.. effectNames.Select(name =>
                Enum.TryParse(name, ignoreCase: true, out CardEffect effect) ? Describe(effect) : string.Empty)];
    }
}
