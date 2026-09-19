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
                "Puts CREATURE bodies on the board, and yours — however worded. Earthbend, an animated land, cloak, "
                + "manifest dread, living weapon and job select all count, and so does a token copy OF A CREATURE. "
                + "A Treasure, Clue, Food or Lander is a resource, not a body; a copy of an artifact is not a body "
                + "either; a token an opponent creates is not yours.",

            [CardEffect.Removal] =
                "Answers ONE creature: destroy, exile, damage, fight, an edict aimed at THEM, or shrink its toughness "
                + "to nothing. Not \"-2/-0\" (Pacify), not \"each creature\" (Wipe), not \"each player sacrifices\", "
                + "and not what only answers a creature already blocking you.",

            [CardEffect.Counter] =
                "Answers a spell on the stack by countering it.",

            [CardEffect.RemovePermanent] =
                "Answers ANY permanent — \"destroy/exile target (nonland) permanent\", including the O-ring family. The "
                + "breadth is the point, so it stays this tag even when you would usually aim it at a creature.",

            [CardEffect.Wipe] =
                "Mass removal: the board is emptied, by any verb — destroyed, exiled, damaged, shrunk, or all "
                + "returned to hand. An adjective does not narrow it: \"all white permanents\" still counts. "
                + "\"Destroy all creatures blocking or blocked by this creature\" is a combat trick.",

            [CardEffect.Bounce] =
                "Returns a NAMED permanent to its owner's hand (\"return target …\", or a mass \"return each/all\"). "
                + "Returning itself as a cost or an end-step drawback is a price the card pays, not an answer.",

            [CardEffect.Ramp] =
                "More mana, or sooner: a mana ability on a creature or rock, a land onto the battlefield, a ritual, "
                + "a cost reduction, an extra land drop, untapping lands, several Treasures. Mana that COSTS mana "
                + "fixes colour instead. A land tapping for its own one mana is just a land.",

            [CardEffect.Disenchant] =
                "Destroys or exiles a targeted artifact or enchantment.",

            [CardEffect.Discard] =
                "Empties somebody ELSE'S hand. A bare \"discard a card\" is a cost you pay — madness, blitz, cycling, the "
                + "back half of a loot — and paying it attacks nobody.",

            [CardEffect.CardAdvantage] =
                "A card the opponent does not get. COUNT IT, and the card ITSELF counts: Ponder is -1 +1 = 0. A "
                + "one-shot must draw two more than it pays; a repeatable ability bought the card once, so one is "
                + "enough. Draws you can go back to, \"for each X\", the top of your library as a second hand. Not a "
                + "draw for THEM, not a trigger off drawing.",

            [CardEffect.Filter] =
                "Cards changing places. ANY scry or surveil counts, however small and whatever else the card does; so "
                + "does looking at the top few and taking one, and every rummage — draw and hand one back, either "
                + "order, cost line included. One that ALSO comes out ahead keeps this AND CardAdvantage. Cycling is "
                + "neither: it replaces itself.",

            [CardEffect.Reanimate] =
                "A creature back from the dead and straight onto the BATTLEFIELD, cheating its cost. A card this card "
                + "EXILED counts, and so does a token copy of a creature in a graveyard. Blinking your OWN creature "
                + "does not — that is protection. A land coming back is Ramp.",

            [CardEffect.Buff] =
                "Raises POWER and/or TOUGHNESS for something other than the card itself: +2/+2, a +1/+1 counter, "
                + "+X/+X. A granted keyword is NOT Buff — flying, deathtouch, first strike — except DOUBLE STRIKE, "
                + "which doubles the damage. Not a self-pump, not counters on THEIR creatures, not a tribal lord.",

            [CardEffect.Protection] =
                "Keeps something ELSE alive, and can be held up in response. Two vocabularies count equally: a granted "
                + "keyword shield (hexproof, indestructible, ward), and damage prevention. Not a fog, not a shield the "
                + "card puts on itself, and not preventing the damage a creature DEALS — that is Pacify.",

            [CardEffect.Burn] =
                "Damage aimed at a FACE, however counted — \"damage equal to the number of Swamps\" burns, and so "
                + "does \"each opponent loses 2 life\". Damage aimed at a creature is Removal; \"any target\" is "
                + "both. A sweeper counts only if it catches the players. Damage to YOURSELF is a price.",

            [CardEffect.Sacrifice] =
                "An outlet you can feed your OWN creatures, artifacts or permanents at will — the half of the combo that "
                + "makes a stolen creature worth taking. Not an edict (that empties their board), not a land, and not "
                + "\"as an additional cost to cast\", which pays once.",

            [CardEffect.Steal] =
                "Takes control of a permanent somebody else controls, for a turn or for good.",

            [CardEffect.Tutor] =
                "Searches the library for a specific nonland card. A land search is Ramp or ManaFixing.",

            [CardEffect.ManaFixing] =
                "Fixes colours: a choice that COSTS you something, any landcycling, a land with two abilities, a "
                + "land fetched to HAND, a Treasure. Free for a tap is just Ramp (Birds), unless it's a land. Not a "
                + "land put onto the battlefield, not mana you may only spend on one thing.",

            [CardEffect.Pacify] =
                "Neutralises somebody else's creature without killing it: can't attack or block, tapped down, "
                + "detained, locked from untapping, or the damage it DEALS prevented. UNTAPPING one is not this. "
                + "Nor is tapping one of yours as a cost, or a card that only taxes ITSELF.",

            [CardEffect.LandDestruction] =
                "Destroys or exiles lands. Deliberately narrow: the wider mana-denial family (Winter Orb, Blood Moon, "
                + "Spheres) stays untagged.",

            [CardEffect.Mill] =
                "Puts cards from ANOTHER player's library into their graveyard. Filling your own graveyard is fuel for "
                + "what the card does next, not an attack — dredge is a graveyard card, not a mill card.",

            [CardEffect.Regrowth] =
                "Returns a card from a graveyard to HAND, or to the TOP of a library, which is a hand you wait a turn "
                + "for. The BOTTOM is graveyard hate, not recursion, and an opponent's graveyard is not yours to "
                + "rebuy. Straight to the battlefield is Reanimate; casting it in place is neither.",

            [CardEffect.Cheat] =
                "Puts a NONLAND permanent onto the battlefield without casting it — from hand (Sneak Attack, "
                + "Elvish Piper) or library (Natural Order) — or lets you cast free STANDING (Omniscience). Out of "
                + "a graveyard is Reanimate; a land is Ramp; one free exiled card is CardAdvantage.",

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
