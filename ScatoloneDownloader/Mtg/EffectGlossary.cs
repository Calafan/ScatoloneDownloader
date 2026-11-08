namespace ScatoloneDownloader.Mtg
{
    /// <summary>
    /// One short line per <see cref="CardEffect"/>, shown in the hint box above
    /// the tagger's effect buttons and as the tooltip on the stats page's effect names.
    /// <para>
    /// The rulings live in docs/ontology/ (one file per <see cref="CardEffect"/>
    /// member), but those are paragraphs and they are in the source tree — which
    /// is exactly where they are no use, because the moment a ruling is needed is the moment a card is
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
                "Puts CREATURE bodies on your side — earthbend, manifest, cloak, living weapon, "
                + "an animated land, artifact, enchantment or token, a token copy of a creature or creature SPELL. "
                + "A noncreature card or an Adventure's spell counts for one body; a CREATURE only for three at "
                + "once or a repeating effect. A Treasure, Clue or Food is no body.",

            [CardEffect.Removal] =
                "Answers ONE creature/planeswalker: destroy, exile, damage however counted, fight, an edict (one each, a "
                + "punisher), INTO a library, made a Treasure, or ANY toughness malus (\"+2/-1\"). Not YOUR OWN, a "
                + "creature TYPE, a BLINK, a GRAVEYARD card, \"-2/-0\" (Pacify), \"each creature\" (Wipe), a "
                + "token's 1-point ping, or what only touches its blocker.",

            [CardEffect.Counter] =
                "Somebody else's spell or ability taken off the stack — countered, exiled, back to hand or into "
                + "the library — by an answer you time: a target, or a moment (flash, entering, face up, an "
                + "instant that ends the turn). Counterspell. Not a trigger that counters whatever is cast, nor a "
                + "counter for the card itself.",

            [CardEffect.RemovePermanent] =
                "Somebody else's permanent of ANY type, chosen by you, gone for good: \"destroy/exile target "
                + "(nonland) permanent\", a token, a colour, the O-ring family, shuffled away, made a Treasure. "
                + "Vindicate. Not an edict (they choose), a list of types, your own, a blink, nor an exile its "
                + "owner may play.",

            [CardEffect.Wipe] =
                "Several of somebody else's creatures gone at once, taken by a rule and not target by target: "
                + "all or each, damage or -X/-X to each, returned, shuffled away, two or more or half sacrificed, "
                + "an edict every turn. Wrath of God. Not one creature type, what fights it or didn't attack, your "
                + "own, lands or artifacts alone.",

            [CardEffect.Bounce] =
                "Returns a NAMED permanent to its owner's hand (\"return target …\", or a mass \"return each/all\"), "
                + "or puts it on top of, or near the top of, its owner's library. Returning itself as a cost or a "
                + "drawback is a price, not an answer; so is returning your OWN permanent, and a land returned "
                + "sets a mana base back and is not a bounce.",

            [CardEffect.Ramp] =
                "More mana, or sooner: a mana ability on a creature or rock, a land onto the battlefield, a ritual, "
                + "a cost reduction (not a tribe's), an extra land drop, untapping lands, several Treasures, mana kept "
                + "for good. Mana that COSTS mana fixes colour instead. A land tapping for one is just a land, its own "
                + "mana or one GRANTED to it.",

            [CardEffect.Disenchant] =
                "Answers an artifact or enchantment, at any count — \"up to one\", \"X target\", \"another\", "
                + "\"all\" — and an edict aimed at one counts too. A bare Aura counts; an Aura ATTACHED to a "
                + "named thing does not. An artifact CREATURE is Removal, a sweeper that takes the creatures "
                + "too is Wipe, and your own is never an answer.",

            [CardEffect.Discard] =
                "Empties somebody ELSE'S hand: they discard, you pick, they pay to dodge a punisher, or it is exiled "
                + "or put back on their library. Your own discard is a cost — madness, blitz, cycling, a loot, even "
                + "one handed to a player — and so is \"each player MAY discard\" and a card that only WATCHES a "
                + "discard.",

            [CardEffect.CardAdvantage] =
                "Net count over zero (Ancestral Recall: -1 +3). The card itself and each card paid from HAND "
                + "cost one; each card drawn, put in hand or played from a library, exile or graveyard gains "
                + "one; a repeatable ability pays once. A permanent or graveyard card paid is free; only YOUR "
                + "part counts. Net 0 is Filter if chosen; a sometimes gain is both.",

            [CardEffect.Filter] =
                "YOU choose which cards you will have, not more: see more than you keep (one at most) or hand "
                + "back as many as you take (Brainstorm). Only as the card's JOB: a spell or mode, an ability "
                + "that comes AGAIN or costs the card, a land. Not a scry or loot made ONCE, a LAND kept, an "
                + "OPPONENT's top. A gain only SOMETIMES is CardAdvantage too.",

            [CardEffect.Reanimate] =
                "A nonland PERMANENT back from the dead onto the battlefield under YOUR control, its "
                + "cost skipped (Reanimate): out of a graveyard, out of the exile this card put it in from "
                + "there, or as a token copy; any mana value. Not a card bringing ITSELF back, nor one for a "
                + "creature TYPE only. A land: Ramp only if this card filled the graveyard.",

            [CardEffect.Buff] =
                "Raises POWER/TOUGHNESS for something else, when that is what the card is FOR: permanent, over an "
                + "area, or bigger than +1/+1. A lone +1/+1 counts only on a pump spell or an engine; on a "
                + "creature, only as counters or Roles. Not a bite (Removal), a self-pump, a pump only a tribe (colorless too) or "
                + "tokens get, or a keyword but double strike.",

            [CardEffect.Protection] =
                "Keeps something ELSE alive, held up in response or over an AREA: a keyword shield, prevention, "
                + "redirection, regeneration, a saving blink or unsummon, a shield counter, phasing. Not one "
                + "creature's static, conditional or Aura/Equipment shield, a tribe's, a fog, its own, nor "
                + "damage a creature DEALS or REPEATING prevention to YOU (Pacify).",

            [CardEffect.Burn] =
                "An OPPONENT loses life to what the card does (Lightning Bolt): damage reaching a face, life lost or "
                + "paid to stop the card, from two or one again and again; a hasty body gone at end step. Damage at "
                + "a creature is Removal, \"any target\" both. Not your own life, a charge on a kill, a token's "
                + "ability, nor life that buys cards or a target.",

            [CardEffect.Sacrifice] =
                "An outlet you can feed your OWN creatures or artifacts AGAIN AND AGAIN — a cost, a repeating trigger, "
                + "every upkeep, or a card you recast. Destroying or exiling your own counts. Not once: entering, as "
                + "you cast it, kicker, exploit. Not a price for damage or lost life, not an edict, not a land, "
                + "not an artifact token.",

            [CardEffect.Steal] =
                "Takes control of a PERMANENT somebody else controls, for a turn (Ray of Command) or for good "
                + "(Control Magic), an EXCHANGE included, and the PLAYER for a turn (Mindslaver). A card of theirs "
                + "played is CardAdvantage at most; their graveyard is Reanimate, Bribery Cheat, a life swap Burn. "
                + "Not what you own, nor giving one AWAY.",

            [CardEffect.Tutor] =
                "Searches a library for a card you CHOOSE — by type, subtype, colour, count, by NAME, or just "
                + "\"a card\" — to hand, battlefield, top, graveyard or exile. A land search is Ramp or "
                + "ManaFixing. Revealing until a type turns up is not this: that picks for you.",

            [CardEffect.ManaFixing] =
                "Fixes colours: a choice paid in MANA, any landcycling, a land with two abilities, a land fetched "
                + "to HAND or kept from a few milled, a Treasure of YOURS. Free for a tap, a life, a sacrifice or a trigger "
                + "is just Ramp (Birds), unless it's a land. A land onto the battlefield only if a LAND fetched it, "
                + "restricted mana only if a land makes it.",

            [CardEffect.Pacify] =
                "Neutralises somebody else's creature without killing it: tapped, stunned, phased out, kept from "
                + "untapping, can't attack or block, base power 0 or a -N/-0 that lasts, or the damage it DEALS "
                + "prevented. Not a bare tap on your own turn, a LAND that won't untap, an artifact tap, or a "
                + "price the card pays.",

            [CardEffect.LandDestruction] =
                "Takes a land off the battlefield, however worded: in a type list, behind a count, beside a "
                + "second target, by basic type (\"destroy all Islands\"), or in any sweep that takes them "
                + "(Balance, Apocalypse). A land edict counts. Not a land in a GRAVEYARD, not an Aura ATTACHED to "
                + "one, not your own, not mana denial.",

            [CardEffect.Mill] =
                "Puts cards from ANOTHER player's library into their graveyard, or into exile when nobody plays them "
                + "after, and rad counters handed to them. Filling your own graveyard is fuel for "
                + "what the card does next, not an attack — dredge is a graveyard card, not a mill card.",

            [CardEffect.Regrowth] =
                "Buys a card back out of a graveyard: to HAND, to the TOP of a library, or by CASTING it there or "
                + "out of the exile it was moved to — reanimation for spells. Not a card coming back ITSELF (flashback, unearth), "
                + "one for a creature TYPE only, the BOTTOM of a library or an opponent's graveyard. "
                + "Straight to the battlefield is Reanimate.",

            [CardEffect.Cheat] =
                "Skips the mana cost of a card YOU CHOSE: a NONLAND permanent from hand (Sneak Attack) or SEARCHED "
                + "from any library (Natural Order, Bribery), or a standing free cast or fixed alternative cost (Omniscience). "
                + "Nothing off the TOP of a library, under any mana-value cap, tribal, or sent back to hand. "
                + "A graveyard is Reanimate, a land Ramp.",

            [CardEffect.Redirect] =
                "Acts on a spell without countering it: changes its targets, takes control of it, or copies it — "
                + "anybody's (\"copy TARGET spell\") or YOUR OWN (\"when you next cast… copy that spell\", or "
                + "storm or replicate given to your spells). Not a card copying only itself, and not a copied "
                + "CREATURE spell, which is Tokens.",
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
