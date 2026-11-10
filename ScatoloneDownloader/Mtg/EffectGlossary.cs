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
                "CREATURE bodies on your side besides the card (Lingering Souls): tokens, for a turn too, "
                + "earthbend, manifest, cloak, animated land, artifact, enchantment or token, a copy of a creature "
                + "or creature SPELL. A noncreature card or a spell face needs one; a CREATURE three at once or a "
                + "repeat. No Treasure, Clue or Food, nor a tribe's trigger.",

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
                "A nonland permanent that can be somebody else's leaves the battlefield but stays its owner's, "
                + "who plays it again: back in hand, on or near the top of the library, or exiled where they may "
                + "play it. Unsummon. Not itself as a price, your own alone, lands, spells, cards in a graveyard, "
                + "the bottom or a shuffle.",

            [CardEffect.Ramp] =
                "Mana of yours beyond the land of the turn: one more land in play, a source adding mana or "
                + "discounting other spells every turn, or a burst of mana that is the card's point. Rampant "
                + "Growth. Not a land tapping for one, mana costing mana, a land for a land, mana for others, one "
                + "Treasure per trigger, or mana and lands untapped by an attack.",

            [CardEffect.Disenchant] =
                "An artifact or enchantment that can be somebody else's, named as artifact, enchantment or Aura "
                + "(no other subtype), gone for good: destroyed, exiled, shuffled or sacrificed to an edict, one "
                + "or all. Disenchant. An Aura on a creature or land counts. Not an artifact CREATURE (Removal) "
                + "or a sweeper taking creatures too (Wipe).",

            [CardEffect.Discard] =
                "An opponent loses cards from HAND by your card (Hymn to Tourach): discarded, exiled, put back on "
                + "their library, or a whole hand shuffled away when you choose (Timetwister, Wheel). Not your own "
                + "discard (a cost), a choice every player makes (\"each player MAY\", \"any number\"), or a hand "
                + "reset every turn by itself.",

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
                "Keeps another PERMANENT alive, chosen in response or over an unconditional AREA of yours: "
                + "keyword shield, prevention, redirection, regeneration, return from death, saving blink or "
                + "unsummon, shield counter, phasing. Not chosen at sorcery speed, a tribe's, a fog, its own, a "
                + "spell's, nor damage a creature DEALS (Pacify).",

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
                "A card the searcher CHOOSES from their whole library or the sideboard, by type, NAME or just "
                + "\"a card\", taken anywhere (Demonic Tutor). A land that may be NONBASIC counts (\"a land "
                + "card\"); not a basic land, a land type or a land exiled (Ramp, ManaFixing, Filter), not "
                + "learn's Lesson, not what turns up at random, not the top N.",

            [CardEffect.ManaFixing] =
                "The colour you lack, not more mana: a land of several colours or fetching a land, a land to HAND "
                + "(also beside one put in play) or landcycling, mana PAID that comes back another colour, a Treasure "
                + "of yours, a basic type added to lands. Evolving Wilds. Any colour for a tap, a life, a sacrifice or "
                + "a trigger is Ramp (Birds), unless a land.",

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
                "ANOTHER player loses cards off the top or bottom of their library to your card (Glimpse the Unthinkable): into "
                + "their graveyard, blind or chosen by you, or exiled when nobody plays them; 'each player' and rad "
                + "counters count. Not your own library (fuel), not a reveal they keep part of.",

            [CardEffect.Regrowth] =
                "A card of YOUR graveyard comes back as a card to play: into the HAND (lands too), onto the TOP of "
                + "the library, or CAST from there or from the exile it was moved to, a copy too. Not the card ITSELF "
                + "(flashback, unearth), a creature TYPE only, the BOTTOM of a library, an opponent's graveyard alone. "
                + "Straight to the battlefield is Reanimate.",

            [CardEffect.Cheat] =
                "Skips the mana cost of a card YOU CHOSE: a NONLAND permanent from hand (Sneak Attack) or SEARCHED "
                + "from any library (Natural Order, Bribery), or a standing free cast or fixed alternative cost (Omniscience). "
                + "Nothing off the TOP of a library, under any mana-value cap, tribal, or sent back to hand. "
                + "A graveyard is Reanimate, a land Ramp.",

            [CardEffect.Redirect] =
                "A spell or ability ALREADY ON THE STACK, anybody's, made over without countering it: new targets, "
                + "a new controller, or a copy — aimed, the next one, or each one you cast (Redirect, Fork). Not a card "
                + "copying itself, a copied permanent spell (Tokens), an ability copied on every use, demonstrate, "
                + "or a copy only on itself or yours.",
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
