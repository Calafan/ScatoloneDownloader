using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// Patterns for the tags that act on the BOARD and on mana: Ramp, Removal,
    /// Buff, Protection, Pacify and ManaFixing. Part of
    /// <see cref="EffectClassifier"/>.
    /// </summary>
    internal static partial class EffectClassifier
    {
        // The one land shape that IS Ramp, ruled 2026-09-15. A land tapping for
        // its own single mana is just a land, but Ancient Tomb and Mishra's
        // Workshop produce more than they cost EVERY turn, and so do the Karoo
        // lands once they have given a land back. The "{T}, Sacrifice this land:
        // Add {R}{R}" family is deliberately NOT caught — it spends itself for
        // the extra mana once, which is why Dwarven Ruins and Ebon Stronghold
        // are hand-tagged as nothing while Crystal Vein is not.
        private static readonly Regex LandTapsForMoreThanOne = Rx(
            @"^\{t\}: add \{[wubrgc]\}\{", RegexOptions.Multiline);

        // Mana that arrives without a {T} on a permanent you keep: Black Lotus and
        // Lotus Petal and Blood Pet spend THEMSELVES, Dark Ritual and Songs of the
        // Damned are the same trade written as a spell. Ruled 2026-09-15.
        //
        // This family was measured a round earlier and filed as noise. That was
        // wrong, and wrong for an instructive reason: the pattern was sweeping in
        // the five lands that enter tapped, and they were poisoning the number.
        // Separated, it is the largest Ramp win found — 133 errors to 118.
        private static readonly Regex SacrificesForMana = Rx(
            @"^[^\n:]{0,50}sacrifice[^\n:]{0,40}: add ", RegexOptions.Multiline);

        private static readonly Regex RitualAddsMana = Rx(@"(?:^|\n|\. )add [\w ]{0,20}\{[wubrgc]");

        // The exact line the hand-tagging draws. Crystal Vein cracks for two and is
        // tagged; Dwarven Ruins, Ebon Stronghold, Havenwood Battleground, Ruins of
        // Trokair and Svyelunite Temple do the same thing and are not — because
        // entering tapped costs the turn the extra mana was meant to buy.
        private static readonly Regex LandEntersTapped = Rx(@"this land enters tapped\.");

        // The Removal rules read "destroy target creature" but nothing wider,
        // because [\w ] cannot cross a comma — so "destroy target artifact,
        // creature, or land" (Aftershock, Boom Box, Shattered Wings) went unread,
        // and so did every "exile UP TO ONE target creature" O-ring, where the
        // filler sits in front of "target". That was 49 of the 189 misses, and
        // repairing it cost no precision at all once two families are kept out.
        // Widened to {0,20} on 2026-09-19: "destroy UP TO ONE OTHER target
        // creature" is 16 characters of filler, and Faller's Faithful and Koh
        // both fell one word past the old limit.
        private static readonly Regex KillsAcrossCommas = Rx(
            @"(?:destroy|exile) (?:[\w -]{0,20})?target[\w ,-]{0,45}(?<!non)creature");


        // Every clause that points a destroy or an exile at a creature, with
        // enough of the tail to see WHOSE creature it is and WHERE it is. Used by
        // the guard below, which asks whether any of them is a real answer.
        private static readonly Regex AimedAtACreature = Rx(
            @"(?:destroy|exile) (?:[\w -]{0,20})?target[\w ,'-]{0,45}(?<!non)creature[\w ,'-]{0,30}");

        // Two ways a clause that reads like a kill answers nothing. YOUR OWN
        // creature is a blink, a sacrifice outlet or a way to hide it from a
        // Wrath — Cold Storage, Safe Haven, Niko and Y'shtola all exile one and
        // hand it back. And a creature CARD IN A GRAVEYARD is already dead, so
        // Eater of the Dead and Summoner's Sending are graveyard hate.
        private static readonly Regex AnswersNobody = Rx(
            @"you control|cards? (?:from|in)[\w ']{0,25}graveyard");

        // It comes back: a blink is not an answer.
        private static readonly Regex ReturnsItToPlay = Rx(
            @"return (?:it|them|that card|those cards)[\w ,']{0,40}to the battlefield");

        // A creature card in a graveyard is already dead — that is graveyard hate.
        private static readonly Regex TargetsAGraveyard = Rx(
            @"creature cards? from[\w ']{0,25}graveyard|target creature card");

        // "Deals damage equal to the number of Swamps you control to any target"
        // kills exactly like a fixed number does; the rule only read digits.
        // Widened 2026-09-19, and it was the single largest family of misses.
        // "Equal to" is how the game writes a creature hitting another creature,
        // and the rule read one word order, one length of filler and four
        // destinations. All three were short:
        //   the ORDER reverses — "target creature deals damage TO ITSELF EQUAL TO
        //     its power" (Repentance, Cut Propulsion, Wisecrack);
        //   the FILLER runs long — "damage equal to the number of creatures you
        //     control plus the number of Equipment you control to target creature"
        //     (Slash of Light) is 99 characters of it;
        //   and the DESTINATION is qualified as freely as any other target —
        //     "to target creature AN OPPONENT CONTROLS" (Allies at Last, Terrific
        //     Team-Up), "to UP TO ONE OTHER target creature" (Venom Blast), "to
        //     EACH OF TWO OTHER target creatures" (Betrayal at the Vault), "to ANY
        //     OTHER target" (Screaming Nemesis), or back at itself.
        private static readonly Regex DamageEqualTo = Rx(
            @"deals? damage equal to [\w' ,\-]{0,110}to (?:any (?:other )?target|itself"
            + @"|(?:each of )?(?:two |three )?(?:any |another |other |up to \w+ )*target[\w ,'-]{0,30}creature"
            + @"|that creature(?!'s))"
            + @"|deals? damage to (?:itself|any (?:other )?target|that creature(?!'s)"
            + @"|target[\w ,'-]{0,30}creature) equal to");

        // A shrink is an answer: a creature whose toughness reaches zero dies just
        // as surely as one that is destroyed, and the two vocabularies for it are
        // "gets -N/-N" and a -1/-1 counter. Ruled 2026-09-15.
        //
        // TOUGHNESS is what matters, so the second number has to be non-zero:
        // "-2/-0" takes the attack away and leaves the creature standing, which is
        // Pacify's job, not this one. And the shrink has to be aimed at ONE named
        // creature — measured, reading a mass "-1/-1 to each creature" as Removal
        // costs 38 errors, because that is a Wipe and is tagged as one.
        // The qualifier sits in two places and both had to be allowed for: before
        // the noun ("target ATTACKING creature") and after it ("target creature
        // AN OPPONENT CONTROLS gets -X/-X"). Widened 2026-09-17.
        // An AURA is the third place the shrink lives, and it was invisible to
        // both rules because an Aura never says "target": it says "ENCHANTED
        // CREATURE gets -2/-2" (Weakness, Enfeeblement, Swampsnare Trap) or "gets
        // +2/-2" (Immolation, Phyrexian Boon), which kills a two-toughness
        // creature exactly as a spell would. Added 2026-09-19.
        //
        // ANY toughness malus counts, ruled 2026-09-19 — the size of it is not the
        // question, and neither is what happens to the power. "+2/-1" (Funeral
        // Charm), "+1/-1" (Coils of the Medusa) and "-0/-1" (Ironclaw Curse) are
        // all this tag; a threshold tried earlier, requiring real power loss or
        // two whole points of toughness, was reading four hand tags as a rule and
        // they were slips. "-2/-0" remains Pacify, which is the same ruling read
        // from the other side: it is the TOUGHNESS that has to move.
        private static readonly Regex ShrinksOneCreature = Rx(
            @"target [\w -]{0,28}(?<!non)creature[\w ' -]{0,28}(?<!you control )gets? [+-][\dX]+/-[1-9X]"
            + @"|(?:enchanted creature|otherwise, it) gets [+-][\dX]+/-[1-9X]");

        // ...and the counter version has the same blind spot, plus one of its own:
        // Serrated Biskelion puts the first counter on ITSELF and the second on
        // the victim, so the victim is no longer the word after "on".
        // The counter is not always a -1/-1: Contagion distributes "-2/-1
        // counters", and under the 2026-09-19 ruling the size does not matter, so
        // the rule reads any counter that takes toughness. DISTRIBUTE is the other
        // verb the game uses for the same thing.
        private static readonly Regex ShrinkCounters = Rx(
            @"put(?:s)? (?:a|an|two|three|four|\d+|x) -[\dX]+/-[1-9X] counters? on "
            + @"(?:[\w ,'\-/]{0,40}(?:target|up to)|enchanted creature)"
            + @"|distributes? (?:a|an|two|three|four|\d+|x) -[\dX]+/-[1-9X] counters? among");

        // Pacify's own version of the self-versus-other question, and the largest
        // single source of noise on the board: "this creature can't attack" is
        // DEFENDER, printed on the card, and the reminder text spells it out —
        // which is why every Wall was being proposed as pseudo-removal. So is
        // "can't attack unless defending player controls an Island". A card that
        // only restrains ITSELF neutralises nobody. 115 wrong -> 54.
        private static readonly Regex SelfCantAttack = Rx(
            @"th(?:is|e) (?:creature|permanent)[\w ']{0,30}can'?t attack|\(this creature can'?t attack\.?\)");

        private static readonly Regex OutwardCantAttack = Rx(
            @"can'?t attack or block|tap target[\w ,]*creature|\bdetain\b|target creature can'?t attack"
            + @"|creatures? (?:your opponents control|they control)[\w ']{0,20}can'?t attack");

        // A creature body does not have to arrive as a token. Earthbend turns a
        // land into one, and Nature's Revolt turns every land into one; the user
        // ruled on 2026-09-15 that anything GENERATING creatures is Tokens,
        // because a go-wide deck cares about the bodies rather than the rules
        // wording that produced them. Worth 22 recovered against 20 wrongly
        // fired — near enough a wash on total error, but recall 85.8% -> 91.6%,
        // which is the half that matters when a human confirms every proposal.
        private static readonly Regex Earthbend = Rx(@"\bearthbends?\b");

        private static readonly Regex LandsBecomeCreatures = Rx(
            @"lands? (?:you control )?(?:are|become)[\w ]{0,20}\d+/\d+[\w ]{0,20}creatures?");

        // "Its controller creates a 1/1 white Spirit" (Afterlife), "target
        // opponent creates a 1/1 green Hippo" (Phelddagrif): the body is real,
        // but it is not on your side of the table.
        private static readonly Regex SomebodyElseCreates = Rx(
            @"(?:its controller|that player|each player|each opponent|target opponent|an opponent"
            + @"|the exiled card's owner|they|opponents?)\s+(?:may have you |each )?creates?\b"
            + @"|have an opponent create|you and target opponent each create");

        // Hoisted for the same reason as MillPatterns: the beneficiary guard
        // re-walks their matches to see WHO the effect lands on.
        private static readonly Regex[] BuffPatterns =
        [
            Rx(@"gets? \+\d+/\+\d+"),
            Rx(@"creatures you control get \+"),
            Rx(@"\+\d+/\+\d+ until end of turn"),
            // A pump sized by X or by a count is still a pump; the rules only
            // read digits. Added to THIS array rather than OR'd in later, which
            // matters: half the cards it reaches are self-pumps ("this creature
            // gets +1/+0 for each artifact you control") and only the beneficiary
            // guard below can tell those apart. Added 2026-09-18.
            Rx(@"gets? \+[xX]/\+[xX\d]|gets? \+\d+/\+[xX]|gets? \+[xX]/\+\d|gets? \+\d+/\+\d+ for each"),
            // DOUBLE STRIKE is the one keyword that is a pump: it doubles the
            // damage, which is doubling power by another name. Ruled 2026-09-18,
            // and it is the ONLY exception — granting flying, deathtouch,
            // lifelink or even first strike is not Buff, because none of them
            // changes what the numbers are. Measured: any double-strike grant
            // scores 117, this one 107 before the five cards it disagreed with
            // were realigned to match.
            Rx(@"target [\w -]{0,25}creature[\w ' -]{0,25}gains? [\w ,]{0,30}double strike"
                + @"|(?:creatures you control|equipped creature|enchanted creature)[\w ,]{0,30}(?:gains?|have|has) [\w ,]{0,30}double strike"),
            // SETTING base power and toughness is the third vocabulary for
            // raising them, ruled 2026-09-21. Written narrowly: an Aura or an
            // Equipment setting the body of the thing it is attached to, or a
            // card setting somebody's base to X/X. The BARE phrase is not here
            // and was measured — of the 30 reviewed cards that say "base power
            // and toughness", only 6 are tagged, because most of them SHRINK
            // (Turn to Frog, Sorceress Queen, Humility) or animate a land. The
            // two halves split on the NUMBER, and a threshold on the number is
            // exactly the shape this project has refused before, so Sephiroth
            // and Atomic Microsizer stay misses rather than get a guessed rule.
            // NB the second alternative is written as a LOOKBEHIND so the match
            // starts after the beneficiary. The guard below reads the text in
            // FRONT of a match for its subject, and "the base power and
            // toughness OF ANOTHER TARGET CREATURE YOU CONTROL become X/X" puts
            // that subject in the middle — matching from the start of the phrase
            // leaves the guard looking at the trigger and calling it a self-buff.
            Rx(@"(?:enchanted|equipped) creature has base power and toughness"
                + @"|(?<=base power and toughness of [\w ,'-]{0,60})becomes? [xX]/[xX]"),
            // DOUBLING is multiplication where the rest of this tag is addition,
            // and it lands in the same place. Ruled 2026-09-21: Bulk Up doubles
            // a creature's power, Double Trouble doubles everyone's, and Seismic
            // Tutelage doubles the counters already on the enchanted creature.
            Rx(@"double (?:target creature's|that creature's|the) power"
                + @"|double the number of \+1/\+1 counters"),
        ];

        // A +1/+1 COUNTER is Buff, ruled 2026-09-15. It is the second vocabulary
        // for raising power and toughness, and the tag asks only where the stats
        // land, not how long they last: one counter on one creature counts, the
        // same as Giant Growth does.
        //
        // The user made this ruling against the measurement rather than with it.
        // Of the 161 reviewed cards that put a counter on somebody else, 46 were
        // hand-tagged Buff and the two halves are not told apart by any wording —
        // so the number gets worse before it gets better, and the 115 reviewed
        // entries on the wrong side of the new line were realigned by hand the
        // same day. Judged against the realigned tags, this is the rule.
        // See OnlyPumpsInsideQuotes.
        private static readonly Regex Quoted = Rx("\"[^\"\\n]*\"");
        private static readonly Regex PumpInsideQuotes = Rx("\"[^\"\\n]{0,120}gets? \\+[\\dXx]");
        private static readonly Regex PlainPump = Rx(@"gets? \+[\dXx]+/\+[\dXx]+");

        // Reminder text, and the three rulings of 2026-09-21 that read it. A
        // pump printed inside PARENTHESES is explaining a keyword, not doing
        // anything: rampage, melee and prowess all say "it gets +N/+N" in their
        // reminder and none of the four cards carrying one is tagged Buff
        // (Gabriel Angelfire, Depthshaker Titan, Aligned Heart, Otterball
        // Antics).
        private static readonly Regex Parenthetical = Rx(@"\([^)\n]*\)");

        // An AURA that pumps the creature it just stole or reanimated is paying
        // for that creature, not buffing one of yours: Dance of the Dead gives
        // +1/+1 and takes the untap step away, Binding Grasp gives +0/+1 and
        // takes the creature. The pump is the contour of the effect.
        private static readonly Regex PumpsWhatItTook = Rx(
            @"enchanted creature gets \+[\dXx]+/\+[\dXx]+");

        private static readonly Regex TookThatCreature = Rx(
            @"you control enchanted creature|put enchanted creature card onto the battlefield"
            + @"|enchant creature card in a graveyard|gain control of enchanted creature");

        // And a pump bundled with an ANSWER or a SHIELD is a rider on that, not
        // a Buff of its own. Ruled 2026-09-21, the same reading the damage rider
        // on a removal spell got the day before: Gurmag Rakshasa shrinks one of
        // theirs and pumps one of yours, Magic Damper and Octopus Form pump and
        // grant hexproof, Rhino's Rampage pumps and fights. Asked of the LINE,
        // so a card that pumps on one ability and kills on another keeps both.
        private static readonly Regex AnswerOrShieldOnTheSameLine = Rx(
            // "OTHERWISE, IT GETS -1/-2" is the same creature under a condition,
            // not a second victim: Phyrexian Boon and Tahngarth's Rage pump or
            // shrink the one thing they enchant and are hand-tagged both Removal
            // and Buff, so the shrink must not blank the pump beside it.
            @"(?<!otherwise, it )gets? -[\dXx]+/-[\dXx]|\bfights?\b|prevent all (?:combat )?damage");

        private static readonly Regex CounterOnSomebodyElse = Rx(
            // Widened 2026-09-21 under the human's ruling on counters — "i put
            // se il target non è se stesso sì". Four wordings were unreadable:
            // the count can be spelled out between the counter and its
            // destination ("put a number of +1/+1 counters EQUAL TO JENOVA'S
            // POWER on up to one other target creature"), the counter need not
            // be a +1/+1 (Living Armor puts +0/+1), the destination can be the
            // enchanted or the just-named creature (Sadistic Glee, Sphere Grid),
            // and a card can hand the counter over at ENTRY rather than put it
            // (Thunderous Velocipede, Tromell). The last is restricted to OTHER
            // creatures, because "this creature enters with an additional +1/+1
            // counter" is the self-pump this tag has always refused.
            @"\+[\dX]+/\+[\dX]+ counters?(?:, a [\w ]{0,20}counter,? (?:and a [\w ]{0,20}counter )?)?"
            + @" ?(?:equal to [\w' ]{0,30} )?on "
            + @"(?:target|another|each|up to|one or more|enchanted)"
            + @"|(?:each other|other|another) [\w ,'-]{0,40}enters? with "
            + @"(?:an additional|\w+ additional) \+1/\+1 counter"
            + @"|distribute [\w ]{0,20}\+1/\+1 counters"
            // "Support X" as readily as "support 2" — Blitzball Stadium says the
            // X form, and once reminder text stopped vouching for a pump
            // (2026-09-21) the keyword itself is all that is left to read.
            + @"|\bsupport [\dxX]");

        // Same question Buff always asks, in the one place this wording can point
        // the wrong way: a counter on THEIR creatures helps them, not you.
        private static readonly Regex CounterForAnOpponent = Rx(
            @"\+1/\+1 counters? on [\w ]{0,30}(?:each |an |target )?opponent");

        // A pump restricted to one creature TYPE is not Buff, ruled 2026-09-15:
        // "Minotaur creatures get +1/+0" helps a Minotaur deck, and this cube has
        // none, so the tag would promise a payoff the card cannot deliver. A
        // COLOUR is not a tribe — Crusade and Bad Moon keep the tag — and neither
        // is a state, which is why Castle's "untapped creatures you control" and
        // Weakstone's "attacking creatures" stay in.
        //
        // The test is capitalisation, because oracle text capitalises a creature
        // type and nothing else in this slot, minus the English words that can
        // stand there. Scored against a version built from every creature subtype
        // in the Scryfall bulk: this one agrees everywhere and does better on the
        // irregular plurals the type list cannot form (Elves, Allies).
        // A STATE may stand in front of the tribe without making it any less
        // one: Gornog's "Attacking Warriors you control get +X/+0" pumps
        // Warriors. Ruled not Buff 2026-09-25. The state alone is still not a
        // tribe — Weakstone's "attacking creatures" keeps the tag, because
        // NotATribe refuses the word when it is the only one there.
        private const string StateBeforeATribe = @"(?:[Aa]ttacking |[Bb]locking |[Uu]ntapped |[Tt]apped )?";

        private const string NotATribe =
            @"(?!(?:All|Each|Other|Those|These|Target|Attacking|Blocking|Untapped|Tapped|Enchanted|Equipped"
            + @"|Then|When|Whenever|If|And|But|Your|Their|Creature|Permanent|Token|Legendary|Multicolored"
            + @"|Colorless|White|Blue|Black|Red|Green|Non[\w-]*)s?\b)";

        // NB case-SENSITIVE, unlike everything else here. The clause openers were
        // widened on 2026-09-17: a lord can sit behind an activation cost
        // ("{T}: Other Faerie creatures get +2/+0") or inside a condition ("As
        // long as enchanted land is a basic Mountain, Goblin creatures get
        // +1/+2"), and the word "creatures" is optional — Lord of Atlantis says
        // "Other Merfolk get +1/+1".
        private const string ClauseStart = @"(?:^|\n|\. |: |, )";

        private static readonly Regex TribalPump = new(
            ClauseStart + @"(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + StateBeforeATribe + NotATribe + @"[A-Z][\w']+s? creatures? (?:you control )?(?:get|have)\b"
            + @"|" + ClauseStart + @"(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + StateBeforeATribe + NotATribe + @"[A-Z][\w']+s? (?:you control )?(?:get|have)\b"
            + @"|[Tt]arget " + NotATribe + @"[A-Z][\w']+ creature gets"
            + @"|" + ClauseStart + @"(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + StateBeforeATribe + NotATribe + @"[A-Z][\w']+s? creatures? get \+"
            // Four more ways a card names a tribe, found 2026-09-21 when the
            // human ruled that a lord is never this tag: the type can be CHOSEN
            // rather than printed (Patchwork Banner), several types can be
            // listed (The Swarmweaver's "Insects and Spiders", Spider-Ham's
            // eighteen), or the "tribe" can be one card NAME (Rohgahh's Kobolds
            // of Kher Keep, Gary Clone's other Gary Clones).
            // NB every letter of these is case-SENSITIVE like the rest of this
            // pattern, so the leading noun needs both spellings: the phrase is
            // capitalised when it opens a line and lower-case mid-sentence.
            + @"|[Cc]reatures you control of the chosen type (?:get|have)\b"
            + @"|[Cc]reatures? you control named [\w' ,-]{1,40} gets? \+"
            + @"|each creature you control named [\w' ,-]{1,40} gets? \+"
            + @"|" + ClauseStart + @"(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + StateBeforeATribe + NotATribe
            + @"[A-Z][\w']+s?(?:, [A-Z][\w']+s?){0,20},? and [A-Z][\w']+s? you control (?:get|have)\b",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);

        // PHASING OUT is both tags at once, ruled 2026-09-22: "puoi usarlo sia
        // sull'opponent per prendere tempo che su di te per salvare qualcosa".
        // The permanent has to be a CREATURE — Vision Charm phases an artifact
        // and is Mill and Protection — and an O-RING phase-out that lasts until
        // the enchantment leaves is the temporary-exile family instead, which
        // has no ruling yet (Oubliette, hand-tagged Removal).
        //
        // DECLARED HERE, above ProtectionPatterns, and that placement is load
        // bearing: field initialisers run in textual order within a file, so a
        // pattern array that reads this one further down the file is built from
        // a null. The same hazard the static constructor in the Rules file
        // exists to avoid, and it threw the moment it was written the other way
        // round.
        private static readonly Regex PhasesSomethingOut = Rx(
            @"target [\w ,]{0,35}creature[\w ,]{0,25} phases out(?![^\n]{0,20}until)");

        // NB: no bare "regenerate" — "can't be regenerated" (Wrath) would false-positive.
        private static readonly Regex[] ProtectionPatterns =
        [
            Rx(@"hexproof|indestructible|shroud|protection from"),
            Rx(@"can'?t be (countered|the target)"),
            Rx(@"\bward\b"),
        ];

        // Damage prevention is the OTHER half of Protection, ruled 2026-09-15: a
        // Circle of Protection and Mother of Runes do the same job — you hold
        // them up and something survives — and the user's own tagging splits
        // almost evenly between the two vocabularies (47 keyword, 44 prevention
        // out of 115). Two wordings: the effect first ("prevent the next 3
        // damage that would be dealt to target creature") and the source first
        // ("the next time a source of your choice would deal damage to you this
        // turn, prevent that damage").
        private static readonly Regex PreventDamageTo = Rx(
            @"prevent (?:the next|all)[\w ]{0,30}damage that would be dealt to");

        private static readonly Regex PreventFromChosenSource = Rx(
            @"would deal damage to [\w ,]{0,25}prevent (?:that|all)");

        // Three exclusions, each drawn from how the user already tags:
        //   - a shield the card puts on ITSELF is a stat line, as with Buff.
        //     Modern wording says "this creature"; older cards write their name.
        //   - a fog names no recipient at all ("prevent all combat damage that
        //     would be dealt this turn"), and fogs are deliberately untagged.
        //   - preventing what a creature DEALS neutralises it, which is Pacify:
        //     Maze of Ith, Gaseous Form and Demonic Torment are hand-tagged that
        //     way. The "dealt to and dealt by" wording is the same act.
        private static readonly Regex PreventForItself = Rx(
            @"dealt to (?:this creature|this permanent|it|enchanted creature) ");

        private static readonly Regex PreventDealtBy = Rx(
            @"damage that would be dealt (?:to and dealt )?by");

        // The source-first wording has the same trap read the other way round:
        // Mercenaries' "the next time THIS CREATURE would deal damage to you,
        // prevent that damage" blunts the card's own drawback. Nothing is being
        // protected — the damage was never aimed at anything of yours.
        private static readonly Regex PreventsItsOwnDamage = Rx(
            @"th(?:is|e) (?:creature|permanent|artifact|enchantment|land) would deal damage");

        // Pacify split in two so the guard below can tell the shapes apart. An
        // untap lock is the one Pacify wording a card routinely aims at ITSELF —
        // Basalt Monolith, Mana Vault and Time Vault all read "this artifact
        // doesn't untap during your untap step", which is a price they pay, not a
        // lock on anybody. Everything in PacifyOutward names a victim by
        // construction and needs no such check.
        // The PLURAL was missing, and with it the whole classic lock: "CreatureS
        // don't untap during their controllerS' untap steps" is how Meekstone,
        // Marble Titan, Mudslide, Dream Tides, Magnetic Mountain, Thelon's
        // Curse, An-Zerrin Ruins and Wrath of Marit Lage all write it, and the
        // rule only knew the singular "doesn't". Found 2026-09-22.
        private static readonly Regex AnyUntapLock = Rx(@"do(?:es)?(n'?t| not) untap");

        // …and the lock must not be on LANDS ALONE. Ruled 2026-09-22, "per le
        // terre niente Pacify": Choke and Curse of Marit Lage stop Islands
        // untapping and Winter's Night stops a snow land, and none of the three
        // is tagged — a mana lock denies a resource, it does not neutralise a
        // threat. Asked per LINE and phrased as "names a land and nothing
        // else", not as "fails to name a creature", because an Aura says
        // "enchanted PERMANENT doesn't untap" and names neither (Flood the
        // Engine, Tractor Beam, Stuck in Summoner's Sanctum). Exhaustion stops
        // "creatures and lands" and keeps the tag on the creatures.
        private static readonly Regex UntapLockNamesALand = Rx(
            @"\b(?:lands?|islands?|swamps?|mountains?|forests?|plains)\b");

        private static readonly Regex UntapLockNamesAVictim = Rx(@"\b(?:creature|permanent)s?\b");

        // NEUTRALISING WITHOUT KILLING is this tag's other half, ruled
        // 2026-09-22 ("inseriscila"): the creature stays on the board and stops
        // mattering. Four wordings, each measured. BASE POWER 0 is the oldest
        // (Island of Wak-Wak, Singing Tree, Sorceress Queen). Becoming a small
        // NAMED BODY is the modern one — Spider-Man No More turns it into a 1/1
        // Citizen with defender, Honest Work into a 1/1 Humble Merchant, Unable
        // to Scream into a 0/2 Toy; 2 fire and 2 are tagged, and the [0-2] is
        // what keeps Lizard, Connors's Curse (a 4/4) and Titania's Song out.
        // A shrink that also strips the abilities is Fresh Start. And
        // "ATTACKING CREATURES GET -1/-0" is Weakstone, the only card in the
        // reviewed set that taxes the swing itself.
        //
        // A bare "-5/-0" is NOT here and was measured and rejected at 2 tagged
        // out of 12 — Cryoshatter is the one card this costs.
        private static readonly Regex NeutralisesWithoutKilling = Rx(
            @"base power (?:and toughness )?0"
            + @"|is an? [\w ]{0,25}with base power and toughness [0-2]/"
            + @"|attacking creatures get -"
            + @"|gets? -[\dX]+/-0 and loses all abilities"
            + @"|loses all abilities and (?:doesn'?t untap|can'?t attack)");

        // PREVENTING DAMAGE TO THE PLAYER ALONE is this tag and not Protection,
        // ruled 2026-09-22: "le prevenzioni al solo giocatore mettiamole come
        // solo Pacify" — nothing of yours is being saved, the attack simply
        // stops mattering. A prevention that also covers permanents keeps
        // Protection as well (Ultimate Magic: Holy).
        private static readonly Regex PreventsDamageToYouAlone = Rx(
            @"prevent (?:all|the next)[\w \d]{0,25}damage that would be dealt to you");

        // Nobody untaps at all, which is the mass version of the untap lock and
        // says neither "doesn't" nor "don't" (Stasis, Sands of Time).
        private static readonly Regex NobodyUntaps = Rx(
            @"(?:players|each player) skips? their untap step");

        // Preventing what a creature DEALS neutralises it without killing it,
        // which is this tag's whole job. The reading was ruled on 2026-09-05 and
        // written into Protection as an EXCLUSION — "not preventing the damage a
        // creature deals, that is Pacify" — but was never added on this side, so
        // Maze of Ith and Gaseous Form were only ever tagged by accident, through
        // the untap bug above. Fixing that bug is what exposed the hole.
        // The subject after "by" is the whole test, and it has to NAME somebody:
        // Mtenda Lion's "prevent all combat damage that would be dealt by THIS
        // CREATURE" blunts its own attack, which is a price and not a lock.
        //
        // A bare pronoun is deliberately not in the list, and that is a decision
        // rather than an oversight. "Dealt by IT" points back at the card itself
        // in Goblin Snowman and at a creature YOU untapped in Elvish Scout, and
        // both of those are out; nothing in the reviewed set uses the pronoun for
        // somebody else's creature. Adding it bought one card and cost another.
        private static readonly Regex PreventsWhatACreatureDeals = Rx(
            @"damage that would be dealt (?:to and dealt )?by (?:target|enchanted|that|all|each)");

        // And the creature has to be theirs. Ebony Horse and Foxfire say the same
        // sentence, but Ebony Horse unhorses one of YOURS — that is vigilance
        // bought at instant speed, and it neutralises nobody.
        private static readonly Regex PreventionAimedAtYourOwn = Rx(
            @"target [\w ]{0,20}creature you control");

        // A lock the OPPONENT can buy out of is a tax, not a lock: Heroism and
        // Winter's Chill let the attacker pay and come through anyway, so whether
        // anything is neutralised is their decision rather than yours. Ruled
        // 2026-09-16 — "non c'è certezza".
        private static readonly Regex TheyCanPayToIgnoreIt = Rx(
            @"unless (?:its|their) controller pays|unless that player pays"
            + @"|(?:may|doesn'?t|does) pay \{");

        // The word boundary in front of "tap" is load-bearing: UNTAP target
        // creature contains the letters of tap target creature, and without it
        // every untapper in the library — Fyndhorn Brownie, Jandor's Saddlebags,
        // Elder Druid, Quirion Ranger — was proposed as a tap-down. Found
        // 2026-09-15 by bucketing the over-tags; it was a quarter of them.
        private static readonly Regex[] PacifyOutward =
        [
            Rx(@"can'?t attack or block"),
            // "Creatures can't attack YOU unless their controller pays" is the
            // Propaganda tax, and the rule could not read it because it wanted
            // the punctuation immediately after the verb. Propaganda, Koskun
            // Falls and Elephant Grass all say it, and all three are tagged
            // (2026-09-22).
            Rx(@"can'?t attack(?: you)?(\.|,| unless)"),
            // A TAP, in every shape the game prints it. Ruled 2026-09-22 after
            // the hand tags were found contradicting themselves on identical
            // wording — Twiddle against Twitch, Riptide against Blinding Light,
            // Word of Binding against Crashing Wave, Storm Elemental against
            // Sterling Keykeeper. The human resolved all six pairs the same
            // way and rejected both proposed lines: it does not matter how long
            // the lock lasts ("altrimenti saltano il punto e tutti i tappini"),
            // and it does not matter whether tapping is the card's main job.
            // What matters is only that it points at somebody else, which the
            // guards below already ask. 25 reviewed entries were realigned onto
            // the tag the same day.
            //
            // The shapes, in order: a counted or qualified target ("tap up to
            // three target creatures", "tap X target creatures", "tap another
            // target creature"), a bare target, a mass tap, and the tap-or-
            // untap twiddle. A PERMANENT or an ARTIFACT counts as much as a
            // creature — Ring of the Lucii taps a nonland permanent and Sunstar
            // Chaplain an artifact or creature, and both are tagged.
            // The tap has to be able to land on a CREATURE. "Tap target
            // artifact" alone is mana denial and neutralises nobody: Relic
            // Barrier, Touchstone and Phyrexian Gremlins all say it and none is
            // tagged, while Sunstar Chaplain says "artifact OR creature" and
            // Ring of the Lucii "nonland permanent", and both are.
            Rx(@"\btaps? (?:up to [\w ]{0,12}|another|each|[\dXx]+|\w+) target[\w ,'-]{0,35}"
                + @"(?:creature|permanent)"),
            Rx(@"\btaps? target[\w ,'-]{0,35}(?:creature|permanent)"),
            // The mass tap names CREATURES on purpose: "tap all Islands" is the
            // land lock of ruling 7, which is not this tag.
            Rx(@"\btaps? all [\w ,'-]{0,35}creatures"),
            // The twiddle has to reach a creature too: Hyperion Blacksmith taps
            // or untaps an ARTIFACT an opponent controls and is untagged, while
            // Twiddle, Twitch, Jolt and Elder Druid all say "artifact,
            // creature, or land".
            Rx(@"\btap or untap target[\w ,'-]{0,35}(?:creature|permanent|land)"),
            Rx(@"detain"),
            NeutralisesWithoutKilling,
            PhasesSomethingOut,
            PreventsDamageToYouAlone,
            NobodyUntaps,
            PreventsWhatACreatureDeals,
            // A STUN COUNTER is an untap lock that travels with the creature,
            // and it is how every card printed since 2021 writes one. 24
            // reviewed cards carry the word and 18 are tagged; of the six that
            // are not, three put the counter on THEMSELVES as a drawback
            // (Tonberry, Baloth Prime, Ambling Stormshell — see
            // StunsItselfAsADrawback), one is an attack trigger (Vengeful
            // Villagers, read by TapsOnItsOwnAttack), and two hang it off a
            // bigger effect as a rider (Kitnap steals the creature it stuns,
            // Magmatic Hellkite stuns the land it just made an opponent fetch).
            Rx(@"stun counters? on (?!it\b|this )"),
        ];

        // …and the drawback version, which is the same counter aimed inward:
        // the card enters already stunned, or stuns itself to pay for
        // something. Nothing is neutralised but the card itself.
        private static readonly Regex StunsItselfAsADrawback = Rx(
            @"enters tapped with [\w ]{0,12}stun counter"
            + @"|attacks, put [\w ]{0,10}stun counters? on it\b");

        // Tapping a creature as an ATTACK TRIGGER is a combat trick, not a
        // lock: it pushes one blocker out of the way for the swing that is
        // already happening, and by the defender's next untap step it is gone.
        // Seven reviewed cards do exactly this — Seasoned Marshal, Sidar
        // Jabari, Conformer Shuriken, Thunder Lasso, Web-Shooters, Vengeful
        // Villagers, Wayspeaker Bodyguard — and not one of them is tagged.
        //
        // "At the beginning of combat on your turn" is the same trigger written
        // from the other side (Kimahri), and a card may name the victim first
        // and tap it in the next sentence (Vengeful Villagers: "choose target
        // creature an opponent controls. Tap it").
        private static readonly Regex TapsOnItsOwnAttack = Rx(
            @"(?:whenever|when)[^\n]{0,60}attacks?,[^\n]{0,40}tap target[\w ,'-]{0,40}creature"
            + @"|at the beginning of combat on your turn,[^\n]{0,90}tap target[\w ,'-]{0,40}creature"
            + @"|(?:whenever|when)[^\n]{0,60}attacks?, choose target[\w ,'-]{0,40}creature"
            + @"[^\n]{0,20}tap it\b");

        // "This creature can't attack or block UNLESS <condition>" is a price
        // the card pays for its own statline, which is the same reading
        // SelfCantAttack already gives a Wall — it just could not see this
        // wording, because the sentence names the card and then goes on for
        // another forty characters before the verb. Six reviewed cards say it
        // (Hazoret Godseeker, Ketramose, Sab-Sunen, Patchwork Beastie,
        // Tiger-Dillo, The Lion-Turtle) and none is tagged.
        private static readonly Regex CantAttackOrBlockUnless = Rx(@"can'?t attack or block unless");

        // Tapping a creature YOU control is a cost — Energy Tap and Arena buy
        // something with it. Same question as everywhere else: whose creature?
        private static readonly Regex TapsACreatureYouControl = Rx(
            @"\btaps? (?:up to [\w ]{0,12}|another|each|[\dXx]+|\w+ )?target[\w ,'-]{0,30}"
            + @"(?:creature|permanent|artifact)s? you control");

        // And "creatures you control can't attack" (Akron Legionnaire, Evil Eye of
        // Orms-by-Gore) is a drawback the card charges you, not a lock on them.
        private static readonly Regex YourOwnCreaturesCantAttack = Rx(
            @"creatures you control can'?t attack");

        private static readonly Regex[] PacifyPatterns = [AnyUntapLock, PreventsWhatACreatureDeals, .. PacifyOutward];

        // See the two guards these feed, down in Classify.
        // Shared by Wipe and Removal: an answer that can only ever touch what is
        // already fighting the card is a combat trick. "Destroy all creatures
        // blocking or blocked by this creature" reads like a sweeper (Abu Ja'far,
        // the Glyph cycle) and "destroy target creature blocking it" reads like a
        // kill (Knight of Dusk, Flowstone Salamander, Urborg Panther). Neither
        // answers anything you were not already in combat with.
        // Widened 2026-09-19 for the active voice. The rule read the victim's
        // side of the sentence ("target creature BLOCKING IT") and the game just
        // as often writes the card's ("target creature IT'S BLOCKING" — Goblin
        // Snowman, Tinder Wall; "this creature is blocking" — Wall of Corpses),
        // or puts the restriction in the trigger instead ("WHENEVER THIS CREATURE
        // BLOCKS, it deals 1 damage to target attacking creature" — Elite
        // Javelineer, Sawtooth Ogre).
        private static readonly Regex OnlyWhatIsInCombatWithIt = Rx(
            @"blocking or blocked by|blocking (?:this creature|it)\b|blocked by (?:this creature|it)\b"
            + @"|it'?s blocking\b|this creature is blocking\b"
            + @"|whenever this creature blocks\b|whenever this creature becomes blocked\b");

        private static readonly Regex RestrictedMana = Rx(@"spend this mana only");

        // Every LAND flavour of cycling, ruled 2026-09-18. "Basic landcycling" is
        // printed on 125 cards — more than all five named types put together —
        // and was missed entirely. The others (slivercycling, wizardcycling,
        // halflingcycling, affinitycycling) fetch a creature, not a colour, and
        // stay out. NB "islandcycling" contains "landcycling" but no word
        // boundary in front of it, so \b keeps the two apart.
        private static readonly Regex TypeCycling = Rx(
            @"\b(?:basic )?landcycling\b|\b(?:plains|island|swamp|mountain|forest|desert)cycling\b");

        // ---- Ramp, the families ruled 2026-09-18 ----

        // Cost reduction is mana you never had to make. Only for OTHER spells:
        // "this spell costs {1} less to cast" is a discount on itself and was
        // hand-tagged Ramp on 0 of 40 cards, against 20 of 40 for the rest.
        private static readonly Regex CostsLessToCast = Rx(@"costs? \{\d+\} less to cast");
        private static readonly Regex CostsLessForItself = Rx(@"this spell costs \{\d+\} less to cast");

        // Any activated ability on a NON-LAND that adds mana, whatever it asks
        // for: a tap (Llanowar Elves), a counter (Wall of Roots), a card out of
        // hand (Elvish Spirit Guide), charge counters (the Mana Batteries). The
        // land exclusion is the existing structural one — a land making mana is
        // just a land.
        private static readonly Regex ActivatedManaAbility = Rx(
            @"^[^\n:]{1,70}: add [\w ]{0,20}\{", RegexOptions.Multiline);

        private static readonly Regex ExtraLandDrop = Rx(
            @"play an additional land|play any number of lands|play up to \w+ additional lands"
            + @"|additional lands? on each of your turns");

        private static readonly Regex ManaMultiplier = Rx(
            @"adds? an additional \{|for mana, (?:that player|its controller|they)[\w ]{0,12}adds?"
            + @"|tapped for mana[^\n]{0,40}adds?");

        private static readonly Regex LandFromHandToPlay = Rx(
            @"(?<!non)lands? cards? from your hand[\w \/]{0,30}onto the battlefield");

        // (?<!non) because "untap all NONLAND permanents" ends in the same
        // letters — the same word-boundary trap as islandwalk and noncreature.
        private static readonly Regex UntapsLands = Rx(
            @"untap (?:target|all|x target|up to \w+ target)[\w ]{0,20}(?<!non)"
            + @"(?:lands?|forests?|islands?|swamps?|mountains?|plains)\b"
            + @"|untaps all basic lands");

        // (?<!non) again, and for the third time this session: "search your
        // library for a NONLAND permanent card, put it onto the battlefield" is
        // Guardian Sunmare, and it is a tutor, not a fetch.
        private static readonly Regex LandOntoTheBattlefield = Rx(
            @"(?<!non)lands? cards?[\w ,'\/]{0,60}onto the battlefield"
            + @"|search your library for[\w ,'\/]{0,80}(?<!non)(?:land|forest|plains|island|swamp|mountain)"
            + @"[\w ,'\/]{0,60}onto the battlefield");

        // …but the land handed to the player a removal spell was aimed at is
        // their consolation, not your ramp: Emergency Eject, Price of Freedom,
        // Sandworm, Divert Disaster all say "its controller creates a Lander".
        private static readonly Regex LandForSomebodyElse = Rx(
            @"its controller (?:creates|may search|searches|puts)"
            + @"|(?:that|target|defending) player (?:creates|may search|searches|puts)");

        // A Treasure is a Lotus Petal in token form, so it always FIXES. Whether
        // it also RAMPS is a question of how many you get: one, once, is a rider
        // — the same line already drawn for the Clue. Ruled 2026-09-18.
        private static readonly Regex MakesATreasure = Rx(@"treasure token");
        private static readonly Regex SeveralTreasures = Rx(
            @"create (?:two|three|four|five|x|\d+) treasure tokens"
            + @"|creates? that many treasure tokens"
            + @"|treasure tokens? for each|(?:two|three|four|\d+) treasure tokens");

        // The Treasure's own reminder text carries "{T}, Sacrifice this token:",
        // which would make every Treasure card read as repeatable.
        private static readonly Regex TreasureReminderText = Rx(@"\(it's an artifact with[^)]*\)");

        // Mana that costs mana converts colour, it does not add any — unless it
        // hands back more than it took. See EveryManaAbilityIsPaidAndPoor.
        //
        // The cost is any MANA symbol, generic or coloured: Fire Sprites asks
        // {G} for its {R} and is the plainest card in the family, so a pattern
        // that only read {2} would have missed the whole point.
        private static readonly Regex PaidManaAbility = Rx(
            @"^[^\n:]*\{[\dwubrg]\}[^\n:]*: add ", RegexOptions.Multiline);
        private static readonly Regex PaidManaGivesBackMore = Rx(
            @"^[^\n:]*\{[\dwubrg]\}[^\n:]*: add (?:two|three|four|five|x|\d+) "
            + @"|^[^\n:]*\{[\dwubrg]\}[^\n:]*: add \{[wubrgc]\}\{", RegexOptions.Multiline);

        // Swapping ONE land for ONE land moves no mana: Renewal sacrifices a
        // land to fetch a land and was hand-tagged ManaFixing alone. The count
        // is the whole test — Harrow pays one land for TWO and is Ramp, so a
        // veto that only read the cost would have thrown it out.
        //
        // The Lander token is NOT in this family and is deliberately left alone:
        // it fetches a real land onto the battlefield, and the 17 reviewed cards
        // that make one are all hand-tagged Ramp. Blanking the token's reminder
        // text was tried on 2026-09-18 and lost every one of them.
        private static readonly Regex PaysALandForTheLand = Rx(
            @"as an additional cost to cast this spell, sacrifice a land"
            + @"[\s\S]{0,140}search your library for an? basic land card");

        // See the ManaFixing rule-table entry for the ruling these two carry.
        private static readonly Regex LandSearchToHand = Rx(
            @"search your library for[\w ,'-]{0,60}(?:land|plains|island|swamp|mountain|forest)"
            + @"[\w ,'-]{0,40}card[\w ,'-]{0,40}put (?:it|that card|those cards|them) into your hand");
        private static readonly Regex LandSearchToTop = Rx(
            @"search your library for[\w ,'-]{0,60}(?:land|plains|island|swamp|mountain|forest)"
            + @"[\w ,'-]{0,60}put that card on top");

        // A mana ability that costs nothing but the tap is a mana SOURCE: the
        // card is Ramp, and the colour it happens to make is a property of the
        // source rather than a service it performs for you. Pay something on top
        // — mana, a life, the tap of another permanent — and it converts colour
        // instead of making it, which IS the job: Celestial Prism, Mana Prism,
        // Standing Stones, Gene Pollinator. Measured 2026-09-18 over the 5,035
        // reviewed cards: a bare {T} for any colour was tagged 2 of 28 (7.1%),
        // the same line behind a cost 11 of 12 (91.7%); for a choice of two
        // colours, 0 of 3 against 6 of 6. Worth 21 on its own.
        //
        // LANDS are exempt and never asked — "{T}: Add one mana of any color" on
        // a land was tagged 17 of 17, because a land makes no mana you did not
        // already have and the colour is the only thing it gives you.
        private static readonly Regex FixesColourOnThisLine = Rx(
            @"mana of any (?:one )?color|add \{[wubrg]\} or \{[wubrg]\}|add \{[wubrg]\}, \{[wubrg]\}");

        // Anchored at the start of the line, or at the start of a QUOTED ability
        // granted to something else ("Other permanents you control have \"{T}:
        // Add one mana of any color.\"") — which is the same bare tap, one step
        // removed. A cost in front of the tap defeats both, which is the point:
        // "{1}, {T}: Add one mana of any color" contains no such opening.
        private static readonly Regex BareTapAdds = Rx("^\\{t\\}: add |\"\\{t\\}: add ");

        // The untap lock written about the card itself. "Target creature doesn't
        // untap during its controller's next untap step" (Frozen Solid) does NOT
        // match, so a real lock keeps the tag.
        // "During YOUR untap step" is the tell that settles the rest: a real lock
        // reads "during ITS CONTROLLER'S next untap step" (Frozen Solid), so it
        // still keeps the tag. The pronoun form — Apes of Rath's "whenever this
        // creature attacks, IT doesn't untap" — and the card naming itself —
        // Merieke Ri Berit — are the same self-tax written two other ways.
        private static readonly Regex SelfUntapClause = Rx(
            @"th(?:is|e) (?:artifact|creature|permanent|enchantment|land|vehicle)[\w ]{0,25}does(?:n'?t| not) untap"
            + @"|\bit does(?:n'?t| not) untap"
            + @"|does(?:n'?t| not) untap during your (?:next )?untap step");

        // ---- Buff: how big, how often, and whether it is the point ----------
        //
        // Ruled 2026-09-25, from the human's own rule of the day before: "Buff
        // only if repeated, or static/permanent (not tribal), or at instant
        // speed, or over an area, or +5/+5 or more; a single one has to be
        // bigger than +1/+1 or have more than one target." The last clause
        // meant ONE-SHOT, not one target — the human's re-review of Buff that
        // day kept Expanding Ooze and Sample Collector (one counter, one target,
        // every attack) and Fae Flight (+1/+0, permanent). Three rulings then
        // settled what "small" covers, all of them asking what the card is FOR:
        //
        //   B1  a one-shot at sorcery speed, +1/+1 or one counter, is never Buff.
        //       "La carta non ha quello scopo" — most are creatures entering.
        //   B2  at instant speed it is Buff only when the pump IS the card:
        //       Gift of the Viper and Guided Strike, but not Magic Damper or
        //       Lightfoot Technique, which are Protection with a rider. Read as
        //       "the card carries no other effect tag".
        //   B3  a small REPEATED pump on a CREATURE is not Buff either — "an
        //       extra of a creature you judge as a whole". On a noncreature the
        //       pump is the card (Firebreathing, Innkeeper's Talent) and stays.
        //
        // And a BITE is Removal alone at any size: the pump in Felling Blow and
        // Bite Down on Crime is how the removal is aimed, not a Buff of its own.
        //
        // 105 hand tags moved with the ruling (64 B3, 27 B1, 8 B2, 5 bites, and
        // one self-pump found on the way).
        // Asked per ability, with modal bullets read on their own when they
        // carry their own trigger (Hollowmurk Siege's "Abzan — Whenever you
        // attack"). Only the pumps THIS reading recognises are judged: a double
        // strike grant, a base P/T or a doubling is left standing, and the
        // strip-and-re-ask at the end lets it keep the tag.
        private static readonly Regex PumpNumbers = Rx(
            @"\bgets? \+(?<a>\d+|x)/\+(?<b>\d+|x)|\bgets? \+(?<a>\d+|x)/[-+]0\b|\bgets? [-+]0/\+(?<b>\d+|x)");

        private static readonly Regex PumpForEach = Rx(@"\bgets? \+\d+/\+\d+ for each|\bgets? \+\d+/\+0 for each");

        private static readonly Regex PumpCounters = Rx(
            @"\b(?:put|puts|distribute|with)(?: an additional| additional)? "
            + @"(?<n>a|an|one|two|three|four|five|six|seven|x|that many|\d+)(?: additional)? \+1/\+1 counters?");

        private static readonly Regex PumpDoubles = Rx(
            @"double (?:target creature's|that creature's|the) power|double the number of \+1/\+1 counters");

        // "Enters with" is bare on purpose: the card's own counters are written
        // "Ghave enters with five +1/+1 counters on it" as often as "this
        // creature enters with", and the area form ("each other creature …
        // enters with") never reaches here as a single-target pump.
        private static readonly Regex PumpOnItself = Rx(
            @"\bthis (?:creature|permanent|vehicle|spacecraft) gets\b|\+1/\+1 counters? on this\b|\benters with");

        // "It gets" is the card only when the CLAUSE in front of it is about the
        // card: Slimy Piper's "whenever THIS CREATURE attacks, it gets +1/+1",
        // Leonin Den-Guard's "as long as this creature is equipped, it gets",
        // valiant's "whenever this creature becomes the target …, put a +1/+1
        // counter on it", and exert's "when you do, it gets". The same pronoun
        // is somebody else in "whenever ENCHANTED CREATURE becomes blocked, it
        // gets +4/+0" (Bestial Fury), "whenever A CREATURE you control attacks
        // alone" (Team Avatar) and "target creature gets +3/+3 … It gets an
        // additional +2/+2" (Growth Cycle). Read by clause because two looser
        // tries each broke a different set: the bare pronoun took the tag off
        // seven reviewed cards, and "the card is mentioned and nobody else is"
        // gave it to every equipped-only self-pump in the store.
        // Also "… you may pay {E}. IF YOU DO, it gets +2/+2" (Riparian Tiger)
        // and a cost paid out of the card itself: "Remove two +1/+1 counters
        // FROM THIS CREATURE: It gets +4/+4" (Sawtooth Thresher). The same cost
        // written with the card's NAME is asked in EveryPumpIsBesideThePoint.
        private static readonly Regex PronounIsTheCard = Rx(
            @"(?:whenever|when|as long as|if) this (?:creature|permanent|vehicle|spacecraft)(?! or )[^.,]{0,120},\s*"
            + @"(?:you may [^.]{0,60}\.\s*if you do,\s*)?(?:it gets|put [^.]{0,40}counters? on it\b)"
            + @"|exert this (?:creature|vehicle)[^.]{0,60}\.\s*when you do, it gets"
            + @"|from this (?:creature|permanent|vehicle|spacecraft)[^:\n]{0,20}:\s*it gets");

        // The area has to be what GETS the pump. A bare "creatures you control"
        // was in here and read Hundred-Battle Veteran's condition ("three kinds
        // of counters among creatures you control") as the beneficiary of the
        // +2/+4 it gives itself.
        private static readonly Regex PumpOverAnArea = Rx(
            @"\b(?:[\w-]+ )?creatures(?: [\w' ,-]{0,40})? get\b|\beach (?:other )?[\w -]{0,25}creature\b|\bon each\b"
            + @"|\ball creatures\b");

        private static readonly Regex PumpOnSeveral = Rx(
            @"\b(?:two|three|one or two|up to two|up to three|up to x|any number of|x|each of up to \w+) (?:other )?target"
            + @"|\beach of up to\b|\bdistribute\b");

        private static readonly Regex PumpIsPermanent = Rx(@"\b(?:enchanted|equipped) (?:creature|permanent)s? (?:gets?|has)\b");

        private static readonly Regex PumpNamesSomebody = Rx(@"target|another|other");

        // The pump that aims a removal spell: "it deals damage equal to its power
        // to target creature", "then it fights".
        private static readonly Regex Bite = Rx(@"deals? damage equal to (?:its|their|that creature's) power|\bfights?\b");

        // Counters that ARE a body rather than a pump on one, ruled Tokens and
        // not Buff on 2026-09-25: Case of the Filched Falcon puts four on a
        // noncreature artifact that "becomes a 0/0 Bird creature", and
        // Valgavoth's Onslaught puts X on the bodies it has just manifested.
        // "Those creatures" has to be bodies the same sentence MADE: the bare
        // phrase also ends Biogenic Upgrade and Omnivorous Flytrap ("distribute
        // … then double the counters on those creatures") and Smile at Death,
        // which returns two creatures and pumps them — all three Buff.
        private static readonly Regex CountersMakeTheBody = Rx(
            @"(?:manifest dread|\bcloak|\bcreate)[^.\n]{0,80}counters? on (?:each of )?(?:those|these) (?:creatures|tokens)\b"
            + @"|counters? on target noncreature [\w ]{0,20}\. it becomes an? 0/0\b");

        private static readonly Regex AbilityWordPrefix = Rx(@"^[^—\n]{1,40}— ");
        private static readonly Regex OwnTrigger = Rx(@"^(?:when|whenever|at the beginning|at end of combat)|^[^:\n]{1,60}:");
        private static readonly Regex SeveralChapters = Rx(@"^[ivx]+(?:, [ivx]+)+ —");
        private static readonly Regex OneChapter = Rx(@"^[ivx]+ —");
        private static readonly Regex LoyaltyCost = Rx(@"^\[?[+−-]?[\dx]+\]?:");
        private static readonly Regex PaidCost = Rx(@"\{|\btap\b|sacrifice|discard|pay|exile|remove|equip");
        private static readonly Regex CostSpendsTheCard = Rx(@"sacrifice this|exile this card from your graveyard");
        private static readonly Regex RepeatingTrigger = Rx(@"^(?:whenever|at the beginning|at end of combat)|\band whenever\b|\benters or attacks\b");
        private static readonly Regex UntilEndOfTurn = Rx(@"until end of turn");
        private static readonly Regex PutsCounters = Rx(@"\bput\b|\bdistribute\b");
        private static readonly Regex GetsPlus = Rx(@"\bgets? \+|\bget \+");

        private enum PumpTiming { Once, Instant, Repeat, Static }

        private static PumpTiming TimingOfPump(Card card, string head, string? bullet)
        {
            foreach (string raw in bullet != null ? [bullet, head] : new[] { head })
            {
                string line = raw.Trim().TrimStart('•').Trim().ToLowerInvariant();
                if (!line.StartsWith("when") && !line.StartsWith("at "))
                {
                    line = AbilityWordPrefix.Replace(line, string.Empty);
                }

                // A bullet without a trigger of its own takes its head's.
                if (ReferenceEquals(raw, bullet) && !OwnTrigger.IsMatch(line))
                {
                    continue;
                }

                if (SeveralChapters.IsMatch(line) || LoyaltyCost.IsMatch(line)) { return PumpTiming.Repeat; }
                if (OneChapter.IsMatch(line)) { return PumpTiming.Once; }

                int colon = line.IndexOf(':');
                if (colon > 0 && !line.StartsWith("when") && !line.StartsWith("at the")
                    && PaidCost.IsMatch(line[..colon]))
                {
                    return CostSpendsTheCard.IsMatch(line[..colon]) ? PumpTiming.Once : PumpTiming.Repeat;
                }

                if (RepeatingTrigger.IsMatch(line)) { return PumpTiming.Repeat; }
                if (line.StartsWith("when")) { return PumpTiming.Once; }

                string whole = (head + " " + bullet).ToLowerInvariant();
                if (!UntilEndOfTurn.IsMatch(whole) && !PutsCounters.IsMatch(whole) && GetsPlus.IsMatch(whole))
                {
                    return PumpTiming.Static;
                }

                string front = (card.TypeLine ?? string.Empty).Split("//")[0];
                bool flash = card.Keywords?.Contains("Flash", StringComparer.OrdinalIgnoreCase) == true;
                return front.Contains("Instant", StringComparison.OrdinalIgnoreCase) || (flash && !FrontFaceIsACreature(card))
                    ? PumpTiming.Instant
                    : PumpTiming.Once;
            }

            return PumpTiming.Once;
        }

        private static int SizeOfPump(string piece)
        {
            int best = 0;
            foreach (Match m in PumpNumbers.Matches(piece))
            {
                foreach (Group g in new[] { m.Groups["a"], m.Groups["b"] })
                {
                    if (g.Success)
                    {
                        best = Math.Max(best, int.TryParse(g.Value, out int v) ? v : 9);
                    }
                }
            }

            foreach (Match m in PumpCounters.Matches(piece))
            {
                best = Math.Max(best, m.Groups["n"].Value.ToLowerInvariant() switch
                {
                    "a" or "an" or "one" => 1,
                    "two" => 2,
                    "three" => 3,
                    "four" => 4,
                    "five" => 5,
                    "six" => 6,
                    "seven" => 7,
                    string d when int.TryParse(d, out int v) => v,
                    _ => 9,
                });
            }

            if (PumpForEach.IsMatch(piece) || PumpDoubles.IsMatch(piece))
            {
                best = Math.Max(best, 9);
            }

            return best;
        }

        /// <summary>Does this one pump earn Buff under the 2026-09-25 rule? See
        /// the block comment above for B1, B2, B3 and the bite.</summary>
        private static bool PumpCounts(Card card, string piece, PumpTiming timing, CardEffect otherTags)
        {
            bool single = !PumpOverAnArea.IsMatch(piece) && !PumpOnSeveral.IsMatch(piece);
            int size = SizeOfPump(piece);

            if (Bite.IsMatch(piece) && timing is PumpTiming.Once or PumpTiming.Instant)
            {
                return false;
            }

            if (TribalPump.IsMatch(piece) || CountersMakeTheBody.IsMatch(piece))
            {
                return false;
            }

            if (!single || timing == PumpTiming.Static)
            {
                return true;
            }

            if (timing == PumpTiming.Repeat)
            {
                return size >= 2 || !FrontFaceIsACreature(card);
            }

            if (size >= 2)
            {
                return true;
            }

            // Filter does not count as "something else": a scry or a surveil is
            // the rider the human called saturating, and Storm Strike is Guided
            // Strike with a scry 1 on it — the pump is still the card.
            return timing == PumpTiming.Instant && (otherTags & ~CardEffect.Filter) == CardEffect.None;
        }

        /// <summary>True when every pump this reading recognises fails the rule,
        /// and nothing else on the card still reads as one once they are gone.
        /// </summary>
        private static bool EveryPumpIsBesideThePoint(Card card, CardEffect otherTags)
        {
            string own = Quoted.Replace(Parenthetical.Replace(card.OracleText ?? string.Empty, " "), " ");
            string rest = own;
            bool judged = false;
            bool sawItself = false;

            // Older printings call the card by NAME where newer ones say "this
            // creature": "Rashka gets +1/+2", "counters on Lily Bowen".
            string shortName = (card.Name ?? string.Empty).Split(" //")[0].Split(',')[0].Trim();

            // …and a legendary goes by its FIRST name as often as its full one:
            // Rashka the Slayer says "Rashka gets +1/+2".
            List<string> ownNames = [shortName];
            if ((card.TypeLine ?? string.Empty).Contains("Legendary", StringComparison.OrdinalIgnoreCase)
                && shortName.Split(' ')[0] is { Length: >= 3 } firstName && firstName != shortName)
            {
                ownNames.Add(firstName);
            }

            List<(string Head, List<string> Bullets)> blocks = [];
            foreach (string line in own.Split('\n'))
            {
                if (line.TrimStart().StartsWith('•') && blocks.Count > 0)
                {
                    blocks[^1].Bullets.Add(line);
                }
                else
                {
                    blocks.Add((line, []));
                }
            }

            foreach ((string head, List<string> bullets) in blocks)
            {
                foreach ((string piece, string? bullet) in bullets.Select(b => (b, (string?)b)).Prepend((head, null)))
                {
                    if (!PumpNumbers.IsMatch(piece) && !PumpCounters.IsMatch(piece) && !PumpDoubles.IsMatch(piece))
                    {
                        continue;
                    }

                    bool single = !PumpOverAnArea.IsMatch(piece) && !PumpOnSeveral.IsMatch(piece);
                    bool namesItself = ownNames.Any(n => n.Length > 2
                        && (piece.Contains(n + " gets", StringComparison.OrdinalIgnoreCase)
                            || piece.Contains("counters on " + n, StringComparison.OrdinalIgnoreCase)
                            || piece.Contains(n + ": it gets", StringComparison.OrdinalIgnoreCase)));
                    bool pronounIsItself = PronounIsTheCard.IsMatch(piece);
                    bool onItself = single && !PumpIsPermanent.IsMatch(piece)
                        && (((PumpOnItself.IsMatch(piece) || namesItself) && !PumpNamesSomebody.IsMatch(piece))
                            || pronounIsItself);

                    sawItself |= onItself;

                    if (!onItself)
                    {
                        PumpTiming timing = TimingOfPump(card, head, bullet);
                        if (PumpIsPermanent.IsMatch(piece) && timing != PumpTiming.Repeat)
                        {
                            timing = PumpTiming.Static;
                        }

                        if (PumpCounts(card, piece, timing, otherTags))
                        {
                            return false;
                        }

                        judged = true;
                    }

                    rest = rest.Replace(piece, " ");
                }
            }

            // A pump on ITSELF was refused long before this rule (2026-09-18);
            // the gate that asks it earlier reads the subject in front of the
            // numbers and misses "it gets", a name, and "counters on this
            // creature". Blanked here with the rest, so a card whose only pumps
            // were on itself is left with nothing that reads as one.
            return (judged || sawItself)
                && !BuffPatterns.Any(p => p.IsMatch(rest))
                && !(CounterOnSomebodyElse.IsMatch(rest) && !CounterForAnOpponent.IsMatch(rest));
        }
    }
}
