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
                "Answers ONE creature: destroy, exile, damage however counted, fight, an edict aimed at THEM, "
                + "the BOTTOM of a library, or ANY toughness malus — \"+2/-1\" counts, an Aura counts. Not "
                + "YOUR OWN, not a card in a GRAVEYARD, not \"-2/-0\" (Pacify), not \"each creature\" (Wipe), "
                + "not what can only touch what it is already blocking.",

            [CardEffect.Counter] =
                "Answers a spell on the stack by countering it.",

            [CardEffect.RemovePermanent] =
                "Answers ANY permanent — \"destroy/exile target (nonland) permanent\", the O-ring family, and a kill "
                + "narrowed only by COLOUR (\"destroy target red permanent\"). The breadth is the point, so it stays "
                + "this tag even when you would usually aim it at a creature.",

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
                "Answers an artifact or enchantment, at any count — \"up to one\", \"X target\", \"another\", "
                + "\"all\" — and an edict aimed at one counts too. A bare Aura counts; an Aura ATTACHED to a "
                + "named thing does not. An artifact CREATURE is Removal, a sweeper that takes the creatures "
                + "too is Wipe, and your own is never an answer.",

            [CardEffect.Discard] =
                "Empties somebody ELSE'S hand. A bare \"discard a card\" is a cost you pay — madness, blitz, cycling, the "
                + "back half of a loot — and paying it attacks nobody.",

            [CardEffect.CardAdvantage] =
                "A card the opponent does not get. COUNT IT — the card ITSELF counts, so Ponder nets 0. A one-shot "
                + "must draw two more than it pays; a REPEATABLE ability bought the card once, so one is enough — even "
                + "when it costs a card, itself or another permanent. Two off and ONE played is Filter. Not a draw for "
                + "THEM, nor a trigger you can't reach.",

            [CardEffect.Filter] =
                "Cards changing places at NO net gain: scry, surveil, hideaway, look at the top few and take one, "
                + "exile two and play one, every rummage either way round. Not a card you CLOAK or manifest (Tokens), "
                + "not a LAND off the top (Ramp), not somebody else's loot. One that also comes out ahead, or repeats, "
                + "keeps this AND CardAdvantage.",

            [CardEffect.Reanimate] =
                "A creature back from the dead and straight onto the BATTLEFIELD, cheating its cost. A card this card "
                + "EXILED counts, and so does a token copy of a creature in a graveyard. Blinking your OWN creature "
                + "does not — that is protection. A land coming back is Ramp.",

            [CardEffect.Buff] =
                "Raises POWER and/or TOUGHNESS for something other than the card itself: +2/+2, a +1/+1 counter, "
                + "+X/+X. A granted keyword is NOT Buff — flying, deathtouch, first strike — except DOUBLE STRIKE, "
                + "which doubles the damage. Not a self-pump, not counters on THEIR creatures, not a tribal lord.",

            [CardEffect.Protection] =
                "Keeps something ELSE alive, and can be held up in response. A granted keyword shield (hexproof, "
                + "indestructible, ward), damage prevention, or phasing it out. Not a fog, not a shield the card puts on "
                + "itself, not preventing the damage a creature DEALS, and not prevention aimed at YOU alone — those are Pacify.",

            [CardEffect.Burn] =
                "Damage or life loss aimed at a FACE, however counted and however slow: \"damage equal to the number "
                + "of Swamps\", an upkeep tax, life PAID to stop the card, and losing TWO or more (one point is a "
                + "rider). Damage at a creature is Removal; \"any target\" is both. Not damage to YOURSELF, and not "
                + "what a TOKEN you made deals.",

            [CardEffect.Sacrifice] =
                "An outlet you can feed your OWN creatures, artifacts or permanents at will — the half of the combo that "
                + "makes a stolen creature worth taking. Not an edict (that empties their board), not a land, and not "
                + "\"as an additional cost to cast\", which pays once.",

            [CardEffect.Steal] =
                "Takes what is somebody else's: control of a permanent, an EXCHANGE of two, the player, their "
                + "life total, an AURA moved off their permanent, or a CARD out of their library, hand or "
                + "graveyard — played, or reanimated onto your side. Giving one AWAY is the mirror image, and "
                + "\"from A graveyard\" names no victim: that is Reanimate alone.",

            [CardEffect.Tutor] =
                "Searches a library for a card you CHOOSE — by type, subtype, colour, count, by NAME, or just "
                + "\"a card\" — to hand, battlefield, top, graveyard or exile. A land search is Ramp or "
                + "ManaFixing. Revealing until a type turns up is not this: that picks for you.",

            [CardEffect.ManaFixing] =
                "Fixes colours: a choice that COSTS you something, any landcycling, a land with two abilities, a "
                + "land fetched to HAND, a Treasure. Free for a tap is just Ramp (Birds), unless it's a land. Not a "
                + "land put onto the battlefield, not mana you may only spend on one thing.",

            [CardEffect.Pacify] =
                "Neutralises somebody else's creature without killing it: tapped, stunned, phased out, locked from "
                + "untapping, can't attack or block, shrunk to base power 0, or the damage it DEALS prevented. "
                + "HOW LONG does not matter. Not a LAND that won't untap, not an artifact tap, not a tap on your own "
                + "attack, not a price the card pays itself.",

            [CardEffect.LandDestruction] =
                "Takes a land off the battlefield, however worded: in a type list, behind a count, beside a "
                + "second target, in a sweeper, or named by basic type (\"destroy all Islands\"). A land edict "
                + "counts. Not a land in a GRAVEYARD, not an Aura ATTACHED to one (that protects it), not your "
                + "own, and not the wider mana-denial family.",

            [CardEffect.Mill] =
                "Puts cards from ANOTHER player's library into their graveyard. Filling your own graveyard is fuel for "
                + "what the card does next, not an attack — dredge is a graveyard card, not a mill card.",

            [CardEffect.Regrowth] =
                "Buys a card back out of a graveyard: to HAND, to the TOP of a library, or by CASTING it there — that "
                + "last is reanimation for spells. A card that gives ITSELF a second cast (flashback, escape, unearth) "
                + "is not. Nor is the BOTTOM of a library, or an opponent's graveyard. Straight to the battlefield is "
                + "Reanimate.",

            [CardEffect.Cheat] =
                "Puts a NONLAND permanent onto the battlefield without casting it — from hand (Sneak Attack, "
                + "Elvish Piper) or library (Natural Order) — or lets you cast free STANDING (Omniscience). Out of "
                + "a graveyard is Reanimate; a land is Ramp; one free exiled card is CardAdvantage.",

            [CardEffect.Redirect] =
                "Acts on SOMEBODY ELSE'S spell, already on the stack, without countering it: changes its "
                + "target (\"the target of\", or \"that spell's target\"), or copies it by aiming at it "
                + "(\"copy TARGET instant or sorcery spell\"). Doubling your own next spell is not this, and "
                + "neither is Storm spelling out its own reminder text.",
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
