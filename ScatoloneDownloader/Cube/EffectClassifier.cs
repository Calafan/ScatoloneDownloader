using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// First-pass, rule-based auto-classifier: reads a card's rules text (plus its
    /// keyword abilities) and PROPOSES a set of <see cref="CardEffect"/> flags. It
    /// is deliberately heuristic and precision-leaning — a card can match several
    /// rules (the flags are OR-combined), and a miss is fine because a human
    /// confirms every suggestion in the tagger (the classifier never stamps
    /// <c>reviewedAt</c>). The rule table is a plain list of
    /// <c>(effect, regex...)</c> so it is easy to extend as gaps surface; each
    /// pattern is matched case-insensitively against the lower-cased oracle text.
    /// Not an oracle of truth — a starting point that turns "tag 30k from scratch"
    /// into "review suggestions".
    /// </summary>
    internal static class EffectClassifier
    {
        private static Regex Rx(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static Regex Rx(string pattern, RegexOptions extra) =>
            new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | extra);

        // Mill has to name SOMEBODY ELSE'S library. A bare "mill three cards" is
        // you filling your own graveyard, which is fuel for whatever the card
        // does next (Ooze Patrol's counters, Fell Gravship's recursion) rather
        // than an attack on anybody — the reading settled on 2026-09-15, and the
        // one that makes the tag mean "this is how the deck mills somebody out".
        // Measured against 5035 reviewed cards it is decisive: the old bare-verb
        // rule fired on 79 cards and was wrong on 51 of them; this one fires on
        // 28 and is wrong on none.
        //
        // The run between the player and the verb is what admits "an opponent
        // WOULD mill", "each player MAY mill" and "any number of target players
        // EACH mill". "Mill" only became keyword wording in 2021, so the
        // spelled-out pre-2021 form needs its own pattern or the whole old
        // library goes untagged.
        //
        // Naming the victim also SUBSUMES the old cost guard, which existed to
        // drop Deep Spawn's "unless you mill two cards" and Millikin's
        // "{T}, Mill a card:". Both are things a card does to itself, so neither
        // matches any more and the guard is gone.
        private const string OtherPlayer =
            @"(?:target player|target opponent|each opponent|an opponent|that player|each player"
            + @"|defending player|its controller|opponents?)";

        private static readonly Regex[] MillPatterns =
        [
            Rx(OtherPlayer + @"[\w ,]{0,40}mills?\b"),
            Rx(@"put(?:s)? the top [\w ]{0,25}" + OtherPlayer + @"[\w' ]{0,20}library into"),
            // Same sentence with the victim in front of the verb, which is how
            // Millstone and most pre-2021 mill is actually written.
            Rx(OtherPlayer + @"[\w ,]{0,30}puts? the top [\w ]{0,30}library into"),
        ];

        // The token has to be a CREATURE, and it has to be yours. A Treasure, a
        // Clue, a Food or a Lander is a resource the card hands you on the way
        // past — the tag is for the cards that put bodies on the board, which is
        // what a deck builds around. "Or a copy" covers the token that never
        // says "creature token" because it is a copy of one.
        private static readonly Regex[] TokenPatterns =
        [
            Rx(@"create[s]?\b.*\btoken"),
            Rx(@"put[s]?\b.*\btoken.*onto the battlefield"),
        ];

        private static readonly Regex CreatureTokenWording = Rx(@"creature token|token that's a copy|token copy");

        // Diabolic Edict and Flare of Malice make the OTHER player sacrifice,
        // which empties their board rather than giving you a place to put yours.
        private static readonly Regex SomebodyElseSacrifices = Rx(OtherPlayer + @"[\w ,]{0,40}sacrifices?\b");

        // An outlet has to be usable at will: a cost in front of a colon
        // ("Sacrifice a creature: ..."), or an optional sacrifice a trigger
        // offers you. "As an additional cost to cast this spell" is neither — it
        // is a price paid once, on the way to a different effect.
        private static readonly Regex SacrificeOutlet = Rx(
            @"^[^\n:]{0,60}sacrifices? (?:a|an|another|two|three|\d+)[\w ]*(?:creature|artifact|permanent)[^\n:]{0,40}:"
            + @"|you may sacrifice (?:a|an|another|two|three|\d+)[\w ]*(?:creature|artifact|permanent)",
            RegexOptions.Multiline);

        // Three shapes that LOOK like repeatable or multi-card draw and are not.
        // None of them is a new ruling — each follows from one already made, and
        // together they took CardAdvantage from 135 wrong out of 355 fired to 39
        // out of 263 (precision 62.0% -> 85.2%).
        //
        //   A loot draws and discards in the same breath. "Draw then discard is
        //   Filter alone" was ruled on 2026-09-04 and never applied on this side,
        //   so Bazaar of Baghdad and every "draw a card, then discard a card"
        //   vehicle counted as advantage.
        private static readonly Regex Loot = Rx(
            @"draws? [\w]+ cards?, then discards?"
            + @"|discards? [\w]+ cards?[^\n.]{0,20}(?:if you do, )?draws? [\w]+ cards?"
            + @"|you may discard a card\. if you do, draw");

        //   Cycling pays a card to replace itself: exactly parity. It fired only
        //   because its REMINDER text spells out an activated ability that draws.
        private static readonly Regex Cycling = Rx(@"discard this card: draw a card");

        //   An ability that sacrifices the permanent runs once, so it is not the
        //   repeatable draw the 2026-09-11 ruling asked for — the same reading
        //   that keeps a one-shot sacrifice out of the Sacrifice tag.
        private static readonly Regex DrawBySacrificingItself = Rx(
            @"^[^\n:]{0,60}sacrifice this [\w]+[^\n:]{0,30}:", RegexOptions.Multiline);

        // A Clue and an impulse draw are both "a card the opponent does not get",
        // and neither is a draw, so both are asked the same question: is it one
        // card, once? A single Clue has to be CASHED for {2} and a single impulse
        // replaces the card that made it, so each is a rider rather than a card.
        // They earn the tag when the card makes them REPEATEDLY, and an impulse
        // earns it outright when it exiles more than one card — that is a draw
        // two wearing a different coat. Ruled 2026-09-15.
        private static readonly Regex ClueWording = Rx(@"\binvestigates?\b|\bclue token");

        private static readonly Regex RepeatableClue = Rx(
            @"^[^\n:]{1,70}:[^\n]{0,120}(?:investigate|clue token)"
            + @"|^(?:whenever|at the beginning of)[^\n]{0,160}(?:investigate|clue token)",
            RegexOptions.Multiline);

        private static readonly Regex ImpulseDraw = Rx(
            @"exiles? the top [\w ]{0,20}(?:card|cards) of your library"
            + @"[^\n]{0,90}(?:you may (?:play|cast)|may play (?:it|them|that card))");

        // More than one card off the top is a draw two, whatever it costs to cast.
        private static readonly Regex ImpulseOfSeveralCards = Rx(
            @"exiles? the top (?:two|three|four|five|six|seven|eight|nine|ten|x|\d+) cards? of your library");

        // Repeatable, read loosely ON PURPOSE. The anchored version of this test
        // misses "Battalion — Whenever ...", "Start your engines!" and a trigger
        // written inside a modal bullet, which between them are three of the seven
        // one-card impulses in the reviewed set and all three are tagged. Used only
        // once an impulse has already matched, so the looseness costs nothing.
        private static readonly Regex RepeatableWording = Rx(
            @"\bwhenever\b|at the beginning of|^[^\n:]{1,70}:", RegexOptions.Multiline);

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
        private static readonly Regex KillsAcrossCommas = Rx(
            @"(?:destroy|exile) (?:[\w -]{0,15})?target[\w ,-]{0,45}creature");

        // It comes back: a blink is not an answer.
        private static readonly Regex ReturnsItToPlay = Rx(
            @"return (?:it|them|that card|those cards)[\w ,']{0,40}to the battlefield");

        // A creature card in a graveyard is already dead — that is graveyard hate.
        private static readonly Regex TargetsAGraveyard = Rx(
            @"creature cards? from[\w ']{0,25}graveyard|target creature card");

        // "Deals damage equal to the number of Swamps you control to any target"
        // kills exactly like a fixed number does; the rule only read digits.
        private static readonly Regex DamageEqualTo = Rx(
            @"deals damage equal to [\w' ]{0,45}to (?:any target|target creature|that creature|another target creature)");

        // A shrink is an answer: a creature whose toughness reaches zero dies just
        // as surely as one that is destroyed, and the two vocabularies for it are
        // "gets -N/-N" and a -1/-1 counter. Ruled 2026-09-15.
        //
        // TOUGHNESS is what matters, so the second number has to be non-zero:
        // "-2/-0" takes the attack away and leaves the creature standing, which is
        // Pacify's job, not this one. And the shrink has to be aimed at ONE named
        // creature — measured, reading a mass "-1/-1 to each creature" as Removal
        // costs 38 errors, because that is a Wipe and is tagged as one.
        private static readonly Regex ShrinksOneCreature = Rx(
            @"target creature gets? -[\dX]+/-[1-9X]");

        private static readonly Regex ShrinkCounters = Rx(
            @"put(?:s)? (?:a|an|two|three|four|\d+|x) -1/-1 counters? on (?:target|another target|up to)");

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
        private static readonly Regex CounterOnSomebodyElse = Rx(
            @"\+1/\+1 counters? on (?:target|another|each|up to|one or more)"
            + @"|distribute [\w ]{0,20}\+1/\+1 counters"
            + @"|\bsupport \d");

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
        private const string NotATribe =
            @"(?!(?:All|Each|Other|Those|These|Target|Attacking|Blocking|Untapped|Tapped|Enchanted|Equipped"
            + @"|Then|When|Whenever|If|And|But|Your|Their|Creature|Permanent|Token|Legendary|Multicolored"
            + @"|Colorless|White|Blue|Black|Red|Green|Non[\w-]*)s?\b)";

        // NB case-SENSITIVE, unlike everything else here.
        private static readonly Regex TribalPump = new(
            @"(?:^|\n|\. )(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + NotATribe + @"[A-Z][\w']+s? creatures? (?:you control )?(?:get|have)\b"
            + @"|(?:^|\n|\. )(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + NotATribe + @"[A-Z][\w']+s you control (?:get|have)\b"
            + @"|[Tt]arget " + NotATribe + @"[A-Z][\w']+ creature gets"
            + @"|(?:^|\n|\. )(?:[Aa]ll |[Oo]ther |[Ee]ach )?" + NotATribe + @"[A-Z][\w']+s? creatures? get \+",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);

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
        private static readonly Regex AnyUntapLock = Rx(@"does(n'?t| not) untap");

        // Preventing what a creature DEALS neutralises it without killing it,
        // which is this tag's whole job. The reading was ruled on 2026-09-05 and
        // written into Protection as an EXCLUSION — "not preventing the damage a
        // creature deals, that is Pacify" — but was never added on this side, so
        // Maze of Ith and Gaseous Form were only ever tagged by accident, through
        // the untap bug above. Fixing that bug is what exposed the hole.
        private static readonly Regex PreventsWhatACreatureDeals = Rx(
            @"damage that would be dealt (?:to and dealt )?by (?:target|enchanted|that|all|each)"
            + @"|prevent all combat damage that would be dealt by");

        // The word boundary in front of "tap" is load-bearing: UNTAP target
        // creature contains the letters of tap target creature, and without it
        // every untapper in the library — Fyndhorn Brownie, Jandor's Saddlebags,
        // Elder Druid, Quirion Ranger — was proposed as a tap-down. Found
        // 2026-09-15 by bucketing the over-tags; it was a quarter of them.
        private static readonly Regex[] PacifyOutward =
        [
            Rx(@"can'?t attack or block"),
            Rx(@"can'?t attack(\.|,| unless)"),
            Rx(@"\btap target[\w ,]*creature"),
            Rx(@"detain"),
            PreventsWhatACreatureDeals,
        ];

        // Tapping a creature YOU control is a cost — Energy Tap and Arena buy
        // something with it. Same question as everywhere else: whose creature?
        private static readonly Regex TapsACreatureYouControl = Rx(
            @"\btap target[\w ,]{0,25}creature you control");

        // And "creatures you control can't attack" (Akron Legionnaire, Evil Eye of
        // Orms-by-Gore) is a drawback the card charges you, not a lock on them.
        private static readonly Regex YourOwnCreaturesCantAttack = Rx(
            @"creatures you control can'?t attack");

        private static readonly Regex[] PacifyPatterns = [AnyUntapLock, PreventsWhatACreatureDeals, .. PacifyOutward];

        // See the two guards these feed, down in Classify.
        private static readonly Regex OnlyWhatIsInCombatWithIt = Rx(@"blocking or blocked by");

        private static readonly Regex RestrictedMana = Rx(@"spend this mana only");

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

        // The subjects that make an effect land on something OTHER than the card
        // writing it. Looked for in the text preceding a Buff/Protection match on
        // the same line — see GrantedToSomethingElse.
        //
        // "<noun> you control" has to stay open-ended: the beneficiary is any
        // type line the card cares to name (Wizards, Merfolk, creature tokens,
        // permanents). The stop-word list is what keeps that from swallowing the
        // subordinate clause in "As long as you control an artifact, this
        // creature gets +2/+0", where the card is still only pumping itself.
        // The card talking about itself. Modern oracle text says "this creature";
        // older printings repeat the card's name, which is handled separately
        // because it is per-card data rather than a pattern.
        private static readonly Regex SelfReference = Rx(
            @"\bthis (?:creature|permanent|card|artifact|enchantment|land|token|spell|planeswalker|vehicle|equipment)\b");

        // "X gains hexproof", "creatures with power 2 or less have shroud": a
        // grant verb means SOMETHING is being given the ability, which is enough
        // to keep the tag even when the subject is not vocabulary we recognise.
        private static readonly Regex GrantVerb = Rx(@"\b(?:gains?|have|has|becomes?)\b");

        private static readonly Regex Beneficiary = Rx(
            @"\b(?:target|another|other|each|all|enchanted|equipped|chosen|"
            // "that creature" is the beneficiary a second sentence refers back to:
            // "Gain control of target creature ... that creature gets +2/+0".
            + @"that (?:creature|permanent|player|token|card)|"
            // "attacking" has to MODIFY the beneficiary ("attacking red creatures
            // get +2/+0") — bare, it is just as often the card's own state, as in
            // "As long as this creature is attacking, it gets +2/+0".
            + @"(?:attacking|blocking) (?:[\w-]+ ){0,3}(?:creatures?|permanents?|tokens?)|"
            + @"(?!(?:as|if|unless|while|when|whenever|though|although|because|that|and|or|but|long)\b)\w+ you control)\b");

        // One entry per effect; a card gets the effect if ANY of its patterns hit.
        private static readonly (CardEffect Effect, Regex[] Patterns)[] Rules =
        [
            (CardEffect.Tokens, TokenPatterns),

            // Mass removal first (a wiper also reads as removal; both are fine).
            // Mass DAMAGE is the oldest wiper wording there is and was missing
            // entirely: Earthquake, Crypt Rats and Fire Magic sweep a board
            // without the word "destroy" appearing anywhere. [\dX] because the
            // interesting ones scale — "deals X damage to each creature".
            // Every one of these used to stop at the first adjective, which is
            // where half the sweepers in the game put one: "destroy all WHITE
            // permanents" (Anarchy), "destroy all GREEN creatures" (Perish),
            // "1 damage to each WHITE AND/OR BLUE creature" (Evaporate). Widened
            // 2026-09-15, together with the two other ways a board gets emptied —
            // returning it all to hand, and making everybody sacrifice.
            (CardEffect.Wipe, [
                Rx(@"(?:destroy|exile) all(?: [\w-]{1,15}){0,3} (?:creature|permanent|nonland)"),
                Rx(@"(?:all|each)(?: [\w-]{1,15}){0,3} creatures get -[\dX]+/-[1-9X]"),
                Rx(@"deals? [\dX]+ damage to each(?: [\w/-]{1,20}){0,4} creature"),
                Rx(@"return (?:all|each)[\w -]{0,30}(?:permanent|creature)s?[\w ,'-]{0,40}to (?:their owners'|its owner's) hands?"),
                Rx(@"each player[\w ,'-]{0,80}sacrifices the rest|each player sacrifices[\w ]{0,20}(?:creature|permanent)")]),

            // "Nonland permanent" is how the whole modern O-ring family is worded
            // (Stormplain Detainment, Web Up, Emergency Eject), and reading only
            // the bare "target permanent" left 22 of 31 hand-tagged cards
            // untouched. Ruled 2026-09-15: an answer that can point at ANY
            // permanent is RemovePermanent, not Removal, even when the card that
            // exiles it happens to be aimed at a creature in practice.
            // Recall 29.0% -> 83.9%, 24 wrong -> 12.
            (CardEffect.RemovePermanent, [
                Rx(@"(?:destroy|exile) (?:[\w ]{0,15})?target nonland permanent"),
                Rx(@"(?:destroy|exile) (?:[\w ]{0,15})?target permanent")]),

            // Three deliberate tightenings, each from a card that fooled a looser
            // version of these rules:
            //   \b before "land"   — "islandwalk" and "an Island" contain the
            //                        letters but not the word.
            //   {0,2} filler words — "destroy target Aura attached to a land"
            //                        (Pyramids) puts four words in between, and
            //                        it PROTECTS lands rather than killing them.
            //   a named victim     — "at the beginning of your upkeep, sacrifice
            //                        a land" (Serendib Djinn) is a drawback you
            //                        pay, not an effect you aim at someone.
            (CardEffect.LandDestruction, [
                Rx(@"destroy (?:[\w-]+ ){0,2}target (?:[\w-]+ ){0,2}lands?\b"),
                Rx(@"exile (?:[\w-]+ ){0,2}target (?:[\w-]+ ){0,2}lands?\b"),
                Rx(@"destroy all \blands?\b"),
                Rx(@"(?:target (?:player|opponent)|each player|that player) sacrifices? (?:[\w-]+ ){0,3}lands?\b")]),

            // Damage pointed at something is how most of the game kills a
            // creature, and reading it as Burn alone left 231 of 291 human-tagged
            // Removal cards untouched — by far the widest gap measured. Blaze and
            // Broadside Barrage answer a threat exactly the way Doom Blade does;
            // that the wording says "damage" rather than "destroy" is a detail of
            // the era a card was printed in, not a difference in what it does.
            //
            // "any target" is included deliberately, though it also covers a
            // player. The 2026-09-04 ruling drew the line at target CREATURE, but
            // measured against 3664 reviewed cards the human tags "deals X damage
            // to any target" as Removal in 68 of 75 cases, and the ruling was
            // widened to match that on 2026-09-11 rather than the other way round.
            //
            // Fight is the same act with the damage delegated to a creature you
            // already control, and an edict removes without ever saying "target".
            (CardEffect.Removal, [Rx(@"destroy target[\w ]*creature"), Rx(@"exile target[\w ]*creature"),
                Rx(@"destroy target[\w ]*(creature|planeswalker)"),
                Rx(@"deals? [\dX]+ damage to any target"),
                Rx(@"deals? [\dX]+ damage to [\w ]{0,20}target creature"),
                Rx(@"\bfights?\b"),
                Rx(@"target player sacrifices a creature")]),

            (CardEffect.Counter, [Rx(@"counter target[\w ]*spell")]),

            // The other way to answer something on the stack. "copy target" is
            // required rather than the bare word "copy": a token that enters "as a
            // copy OF target creature" is Tokens, not stack interaction.
            (CardEffect.Redirect, [Rx(@"change the targets? of"),
                Rx(@"cop(?:y|ies) target[\w ]*(?:spell|ability)")]),

            // Bounce has to say WHICH permanent goes back, because the bare
            // sentence is just as often the price the card pays: Ovinomancer's
            // "{T}, Return this creature to its owner's hand:" is a cost and
            // Fleeting Effigy's end-step return is a drawback. Requiring
            // "target" (or a mass "return each/all") took the tag from 74 wrong
            // out of 114 fired down to 8 wrong out of 56.
            //
            // The filler before "target" is what admits "return UP TO ONE OTHER
            // target nonland permanent"; the possessive alternation is what
            // admits the plural "to their owners' hands".
            (CardEffect.Bounce, [
                Rx(@"return (?:[\w' ]{0,30})?target[\w ,']*to (?:its|their) owner(?:'s|s'|s)? hands?"),
                Rx(@"return (?:each|all|every)[\w ,']*to (?:its|their) owner(?:'s|s'|s)? hands?")]),

            (CardEffect.Disenchant, [Rx(@"destroy target[\w ]*(artifact|enchantment)"),
                Rx(@"exile target[\w ]*(artifact|enchantment)")]),

            // Same reading as Mill, and the same result. A bare "discard a card"
            // is overwhelmingly a COST — madness, blitz, cycling reminder text,
            // the second half of looting — rather than an attack on a hand.
            // Naming the victim took Discard from 129 wrong out of 188 fired to
            // 16 wrong out of 69, the largest precision gain on the board.
            (CardEffect.Discard, [Rx(OtherPlayer + @"[\w ,]{0,30}discards?\b")]),

            // Drawing ONE card off a spell you cast replaces the spell — that is
            // card parity, not advantage, which is why Eject and Broadside
            // Barrage do not earn the tag for their trailing "Draw a card". The
            // tag needs a card the opponent does not get: draw two or more, or a
            // draw you can go back to. Decided 2026-09-11 after measuring that
            // firing on any bare "draw a card" cost 179 false positives, the
            // worst precision on the board at 42%.
            //
            // Repeatable means an activated ability (a cost, then a colon) or a
            // recurring trigger. "Whenever" and "At the beginning of" qualify;
            // plain "When this creature enters" does not, because an ETB fires
            // once and is therefore the same one-shot replacement as a cantrip.
            (CardEffect.CardAdvantage, [
                Rx(@"draws? (?:two|three|four|five|six|seven|eight|nine|ten|x|\d+) cards"),
                Rx(@"^[^\n:]{1,70}:[^\n]{0,100}draws? (?:a|one) card", RegexOptions.Multiline),
                Rx(@"^(?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card", RegexOptions.Multiline),
                // "Draw a card for each creature you control" is multi-card draw
                // written the other way round, and the count-first wording was
                // missed entirely: Balance of Power, Baleful Stare, Become the
                // Avalanche. Worth 24 recovered for 2 wrongly fired.
                Rx(@"draws? a card for each|draws? cards equal to")]),

            (CardEffect.Filter, [Rx(@"scry \d"), Rx(@"surveil \d"),
                Rx(@"look at the top \w+ cards? of your library"),
                Rx(@"discard[\w ]* then draw"), Rx(@"draw \w+ cards?, then discard")]),

            (CardEffect.Reanimate, [Rx(@"return target[\w ]*creature card from[\w ]*graveyard to the battlefield"),
                Rx(@"return[\w ]*from (your|a) graveyard to the battlefield"),
                Rx(@"put[\w ]*creature card from[\w ]*graveyard onto the battlefield")]),

            // The sibling of Reanimate: same origin, different destination. The
            // [\w ] runs cannot cross a full stop, so a card that exiles from a
            // graveyard in one sentence and bounces a creature in the next does
            // not accidentally read as recursion.
            (CardEffect.Regrowth, [Rx(@"return[\w ]*from[\w ]*graveyard to[\w ']*hand"),
                Rx(@"put[\w ]*card from[\w ]*graveyard into[\w ]*hand")]),

            (CardEffect.Mill, MillPatterns),

            (CardEffect.Buff, BuffPatterns),

            (CardEffect.Protection, ProtectionPatterns),

            // [\dX] rather than \d so a scaling burn spell counts: Blaze and Fireball
            // say "deals X damage", and matching only literal numbers left them as
            // Removal without being Burn once damage started reading as Removal —
            // an ontology that contradicted itself on the same sentence.
            //
            // Worth 15 recovered against 9 wrongly fired on the reviewed set, which
            // is thin: it is here for the consistency, not for the score. The wider
            // Burn problem is elsewhere and NOT fixed — "each" in the object list
            // below tags mass damage as Burn, which accounts for 37 false positives,
            // but the same wording is hand-tagged Burn on 22 other cards, so the
            // question of whether a sweeper burns is the user's to settle, not a
            // rule to quietly change.
            // Burn is damage aimed at a FACE. Damage pointed at a creature is how
            // the game kills creatures and is already Removal; reading it as Burn
            // as well is what gave the tag 129 false positives, Explosive Shot and
            // Fanged Flames among them. Two shapes qualify: a player named as the
            // target, and the sweeper that catches players on its way past —
            // Inferno's "each creature and each player", which is Wipe AND Burn,
            // while a sweeper that only hits creatures is Wipe alone. Ruled
            // 2026-09-15.
            //
            // "damage to you" is deliberately absent: a painland, Ancient Tomb and
            // Juzam Djinn charge themselves, and self-damage is a price the same
            // way a self-mill or a sacrifice cost is. Precision 58.7% -> 86.3%.
            (CardEffect.Burn, [
                Rx(@"deals? [\dX]+ damage to (?:any target|target player|target opponent|each player|each opponent|that player)\b"),
                Rx(@"deals? [\dX]+ damage to [\w ,]{0,45}each (?:player|opponent)"),
                // "Each opponent loses 2 life" is a Lava Spike at every face at
                // once; the game just declined to call it damage. 18 of the 27
                // reviewed cards that say it are hand-tagged Burn, and every one
                // of them was being missed. Ruled 2026-09-15.
                Rx(@"each opponent loses [\dX]+ life"),
                // Damage sized by a COUNT rather than a digit. The rule for this
                // already existed and fed Removal alone, so Cat-Gator's "damage
                // equal to the number of Swamps to any target" was read as a kill
                // and not as a burn. 11 of 12 such cards are tagged.
                Rx(@"deals damage equal to [\w' ]{0,45}to (?:any target|target player|target opponent|each opponent|each player)")]),

            // Sacrifice is a sacrifice OUTLET: somewhere to put your OWN permanents
            // on demand, which is what makes a stolen creature (see Steal) worth
            // taking. Ruled 2026-09-15. Two things fall out of that and are handled
            // by the guard below: an edict is somebody else sacrificing, and an
            // additional cost to cast is a one-shot price rather than an outlet you
            // can point at anything. Precision 56.5% -> 88.1%, 84 wrong -> 44.
            //
            // The fuel can be a creature, an artifact, or a permanent named as such
            // ("Sacrifice a nontoken permanent:") — Atog, Dwarven Weaponsmith and
            // Infernal Tribute are all Ashnod's Altar with a different thing going
            // in. Artifacts were worth 12 recovered against 7 wrongly fired;
            // "permanent" a further 3 against 0.
            //
            // A LAND is deliberately not on the list, and does not need to be
            // excluded either: "sacrifice a land" is Harrow paying for a fetch and
            // never says "permanent", so it simply does not match.
            (CardEffect.Sacrifice, [Rx(@"sacrifices? (?:a|an|another|two|three|\d+)[\w ]*(?:creature|artifact|permanent)")]),

            (CardEffect.Steal, [Rx(@"gains? control of"), Rx(@"you control (enchanted|target)"),
                Rx(@"untap target creature[\w ]*gain control")]),

            // "a card" (Demonic) or a typed non-land card (creature/instant/...);
            // deliberately NOT land searches, which are Ramp/ManaFixing, not Tutor.
            (CardEffect.Tutor, [Rx(@"search your library for an? card"),
                Rx(@"search your library for[\w ]*(creature|instant|sorcery|artifact|enchantment|planeswalker) card")]),

            (CardEffect.ManaFixing, [Rx(@"add one mana of any color"), Rx(@"mana of any (one )?color"),
                Rx(@"add \{[wubrg]\} or \{[wubrg]\}"), Rx(@"add \{[wubrg]\}, \{[wubrg]\}"),
                // Typecycling fetches the colour you are short of, which is the
                // whole job. Added 2026-09-15; worth 11 on its own.
                Rx(@"\b(?:plains|island|swamp|mountain|forest)cycling\b"),
                // A land with two mana abilities in different colours fixes even
                // though no single line says "or" — Bleachbone Verge, and the
                // whole modern "{T}: Add {B}. / {T}: Add {W}." cycle.
                Rx(@"^\{t\}: add \{([wubrg])\}\.?$[\s\S]{0,120}?^\{t\}: add \{(?!\1)([wubrg])\}", RegexOptions.Multiline)]),

            // Mana ability (dork/rock) or a land-fetch to the battlefield. Lands
            // are stripped below — a land tapping for its own mana is not "ramp".
            (CardEffect.Ramp, [Rx(@"\{t\}: add "),
                Rx(@"search your library for[\w ]*(land|forest|plains|island|swamp|mountain)[\w ,]*put[\w ]*onto the battlefield")]),

            (CardEffect.Pacify, PacifyPatterns),
        ];

        /// <summary>Keyword abilities that map directly to an effect regardless of
        /// oracle wording (e.g. an "Indestructible" creature with no rules text).</summary>
        private static readonly Dictionary<string, CardEffect> KeywordEffects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hexproof"] = CardEffect.Protection,
            ["shroud"] = CardEffect.Protection,
            ["indestructible"] = CardEffect.Protection,
            ["ward"] = CardEffect.Protection,
            ["protection"] = CardEffect.Protection,
            ["detain"] = CardEffect.Pacify,
        };

        /// <summary>Proposes the effect flags for a card from its
        /// <see cref="Card.OracleText"/> and <see cref="Card.Keywords"/>. Returns
        /// <see cref="CardEffect.None"/> when nothing matches (leave it untagged).</summary>
        internal static CardEffect Classify(Card card)
        {
            CardEffect result = CardEffect.None;

            string text = card.OracleText ?? string.Empty;
            if (text.Length > 0)
            {
                foreach ((CardEffect effect, Regex[] patterns) in Rules)
                {
                    foreach (Regex pattern in patterns)
                    {
                        if (pattern.IsMatch(text))
                        {
                            result |= effect;
                            break;
                        }
                    }
                }
            }

            if (card.Keywords != null)
            {
                foreach (string keyword in card.Keywords)
                {
                    if (keyword != null && KeywordEffects.TryGetValue(keyword, out CardEffect kwEffect))
                    {
                        result |= kwEffect;
                    }
                }
            }

            // A land tapping for its own mana is not Ramp (it is just a land —
            // structural, tracked by MacroType). Its ManaFixing, if any, still stands.
            if (card.MacroType == MacroType.Land)
            {
                result &= ~CardEffect.Ramp;

                // …unless it makes more mana than it costs, every turn. See the
                // pattern above for why the self-sacrificing ones stay out.
                if (LandTapsForMoreThanOne.IsMatch(text))
                {
                    result |= CardEffect.Ramp;
                }
            }

            // Added AFTER the land strip, because Crystal Vein is a land and is
            // exactly the card this rule is for. See the patterns above.
            if ((SacrificesForMana.IsMatch(text) || RitualAddsMana.IsMatch(text))
                && !LandEntersTapped.IsMatch(text))
            {
                result |= CardEffect.Ramp;
            }

            // Protection is an INTERACTION you hold up, not a property a card
            // happens to have. A creature printed with hexproof protects only
            // itself, passively, and answers nothing; Mother of Runes protects
            // whatever needs it, in response, on the turn it matters. Only the
            // second is what the tag is for, so the vocabulary above has to clear
            // a timing gate before it counts.
            if (result.HasFlag(CardEffect.Protection) && !IsInstantSpeed(card))
            {
                result &= ~CardEffect.Protection;
            }

            // Tokens is the third reading of the same question the Mill rule
            // asks — what does this card do, and for whom. A Clue or a Treasure
            // is a resource handed over in passing, and a Spirit handed to the
            // creature's OWNER (Afterlife) is a body for the other side, so
            // neither earns the tag that means "this deck goes wide". Written as
            // a guard rather than as more regexes because the evidence sits
            // anywhere on the card, not next to the verb.
            if (result.HasFlag(CardEffect.Tokens)
                && (!CreatureTokenWording.IsMatch(text) || SomebodyElseCreates.IsMatch(text)))
            {
                result &= ~CardEffect.Tokens;
            }

            // Added after the guard, not inside TokenPatterns, because these
            // wordings never say "token" at all and so have nothing for the
            // creature-token check to read.
            if (Earthbend.IsMatch(text) || LandsBecomeCreatures.IsMatch(text))
            {
                result |= CardEffect.Tokens;
            }

            // Removal's two blind spots, added after the table because both need
            // to look at the whole card rather than one sentence.
            if (KillsAcrossCommas.IsMatch(text) && !ReturnsItToPlay.IsMatch(text) && !TargetsAGraveyard.IsMatch(text))
            {
                result |= CardEffect.Removal;
            }

            if (DamageEqualTo.IsMatch(text))
            {
                result |= CardEffect.Removal;
            }

            // A shrink that takes toughness off one named creature. See the two
            // patterns above for why the zero and the single target both matter.
            if (ShrinksOneCreature.IsMatch(text) || ShrinkCounters.IsMatch(text))
            {
                result |= CardEffect.Removal;
            }

            // A Wall is not a Pacify effect. See the patterns above.
            if (result.HasFlag(CardEffect.Pacify) && SelfCantAttack.IsMatch(text) && !OutwardCantAttack.IsMatch(text))
            {
                result &= ~CardEffect.Pacify;
            }

            // The same question one more time, for the two shapes that name a
            // victim and still point the wrong way. See the patterns above.
            if (result.HasFlag(CardEffect.Pacify) && OnlyRestrainsYourOwn(text))
            {
                result &= ~CardEffect.Pacify;
            }

            // "Destroy all creatures blocking or blocked by this creature" reads
            // like a sweeper and only ever touches what is already in combat with
            // it — Abu Ja'far, Kjeldoran Frostbeast, the Glyph cycle. It is a
            // combat trick, and it was every false positive the widened Wipe
            // patterns above introduced.
            if (result.HasFlag(CardEffect.Wipe) && OnlyWhatIsInCombatWithIt.IsMatch(text))
            {
                result &= ~CardEffect.Wipe;
            }

            // Mana you may only spend on one thing is not fixing — it buys the one
            // card the designer had in mind. The test is per LINE, because a card
            // with one restricted ability and one free one (Hermitic Herbalist)
            // still fixes.
            if (result.HasFlag(CardEffect.ManaFixing) && AllManaIsRestricted(text))
            {
                result &= ~CardEffect.ManaFixing;
            }

            // Card parity dressed as card advantage. See the three patterns above
            // for which ruling each one follows from.
            if (result.HasFlag(CardEffect.CardAdvantage)
                && (Loot.IsMatch(text) || Cycling.IsMatch(text) || DrawBySacrificingItself.IsMatch(text)))
            {
                result &= ~CardEffect.CardAdvantage;
            }

            // Added AFTER the parity guard, because neither is a draw and neither
            // should be withdrawn by a loot or a cycling cost elsewhere on the card.
            bool impulseIsACard = ImpulseDraw.IsMatch(text)
                && (ImpulseOfSeveralCards.IsMatch(text) || RepeatableWording.IsMatch(text));

            if (impulseIsACard || (ClueWording.IsMatch(text) && RepeatableClue.IsMatch(text)))
            {
                result |= CardEffect.CardAdvantage;
            }

            // Sacrifice, asked the same way: whose creature, and can you do it
            // when you want to? A card that only lets an OPPONENT sacrifice is an
            // edict, and one that charges a creature as an additional cost to cast
            // pays once and is gone — neither is an outlet.
            if (result.HasFlag(CardEffect.Sacrifice)
                && (SomebodyElseSacrifices.IsMatch(text) || !SacrificeOutlet.IsMatch(text)))
            {
                result &= ~CardEffect.Sacrifice;
            }

            // Buff and Protection need a beneficiary that is not the card itself.
            // A creature that pumps or shields ONLY itself has a stat line, not an
            // effect: it answers nothing for the rest of the board and changes no
            // deck's plan, whereas Giant Growth and Mother of Runes are cards you
            // hold up for whatever needs them. Same reading as the Mill guard —
            // ask what the text does for someone else.
            // The two differ in what SILENCE means, which is a fact about how the
            // two are written rather than a hedge. A P/T change always names who
            // gets it, so an unrecognised subject ("Nonartifact creatures get
            // +2/+2") is somebody else and the tag stands. Protection words are
            // keyword abilities a card can simply HAVE: a line reading "Ward {2}"
            // or "Protection from red" has no subject precisely because the
            // subject is the card itself.
            if (result.HasFlag(CardEffect.Buff) && AimsOnlyAtItself(text, BuffPatterns, card, bareIsSelf: false))
            {
                result &= ~CardEffect.Buff;
            }

            // Added AFTER that gate, not folded into BuffPatterns: the gate reads
            // the text BEFORE a match for its subject, and a counter names its
            // beneficiary after ("put a +1/+1 counter ON target creature"), so it
            // would be stripped every time. See the patterns above.
            if (CounterOnSomebodyElse.IsMatch(text) && !CounterForAnOpponent.IsMatch(text))
            {
                result |= CardEffect.Buff;
            }

            // Last, so it can take the tag back off whichever rule granted it:
            // a card whose ONLY pump is restricted to one creature type. See the
            // patterns above; the "only" is why the tribal phrases are blanked and
            // the question asked again rather than tested in place.
            if (result.HasFlag(CardEffect.Buff) && OnlyPumpsATribe(text))
            {
                result &= ~CardEffect.Buff;
            }

            if (result.HasFlag(CardEffect.Protection) && AimsOnlyAtItself(text, ProtectionPatterns, card, bareIsSelf: true))
            {
                result &= ~CardEffect.Protection;
            }

            // Prevention is added AFTER the two gates above rather than joining
            // ProtectionPatterns, because it answers the self-versus-other
            // question with its own vocabulary — there is no subject in front of
            // "prevent" to inspect. It still clears the same timing gate: a
            // static "prevent all damage that would be dealt to creatures"
            // (Bubble Matrix) sits on the board rather than being held up, and
            // requiring an instant, flash or an activated ability withdrew five
            // false positives at no cost in recall.
            if (PreventsDamageForSomebodyElse(card) && IsInstantSpeed(card))
            {
                result |= CardEffect.Protection;
            }

            // Pacify is the third reading of the same question, and it is asked
            // the same way: a mana rock that will not untap has taxed ITSELF, and
            // taxing yourself neutralises nobody. Kept separate from
            // AimsOnlyAtItself because the evidence runs the other way round —
            // there is no subject in front of "doesn't untap" to inspect, so the
            // test is whether the clause names the card as the thing held down.
            if (result.HasFlag(CardEffect.Pacify) && OnlyLocksItself(text))
            {
                result &= ~CardEffect.Pacify;
            }

            return result;
        }

        /// <summary>Whether the card prevents damage aimed at something other than
        /// itself — the prevention half of Protection. See the patterns above for
        /// why a fog, a self-shield and "damage dealt BY a creature" are all out.</summary>
        private static bool PreventsDamageForSomebodyElse(Card card)
        {
            string text = card.OracleText ?? string.Empty;

            if (!PreventDamageTo.IsMatch(text) && !PreventFromChosenSource.IsMatch(text))
            {
                return false;
            }

            return !PreventForItself.IsMatch(text)
                && !PreventDealtBy.IsMatch(text)
                && !PreventsItsOwnDamage.IsMatch(text)
                && !PreventsForItselfByName(card);
        }

        /// <summary>"Prevent all combat damage that would be dealt to Diamond
        /// Weapon": an older card names itself where a modern one writes "this
        /// creature".</summary>
        private static bool PreventsForItselfByName(Card card)
        {
            string name = ShortName(card);
            if (name.Length == 0)
            {
                return false;
            }

            // A long name is also abbreviated to its first word in its own rules
            // text: Rasputin Dreamweaver writes "damage that would be dealt to
            // Rasputin". Checking the first word alone is safe because it is
            // matched immediately after "dealt to".
            int space = name.IndexOf(' ');
            string shortest = space > 0 ? name[..space] : name;

            string text = card.OracleText ?? string.Empty;
            int at = text.IndexOf("dealt to ", StringComparison.OrdinalIgnoreCase);
            return at >= 0 && text.AsSpan(at + 9).StartsWith(shortest, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when the only thing that read as Pacify is an untap lock
        /// the card puts on itself. Any outward-aimed Pacify wording elsewhere on
        /// the card keeps the tag, so Time Vault's self-tax is dropped while a card
        /// that both taxes itself and taps an opponent's creature is not.</summary>
        private static bool OnlyLocksItself(string text)
        {
            foreach (Regex outward in PacifyOutward)
            {
                if (outward.IsMatch(text))
                {
                    return false;
                }
            }

            return SelfUntapClause.IsMatch(text);
        }

        /// <summary>True when the only thing that read as Pacify restrains the
        /// player's OWN board — tapping a creature you control as a cost, or a
        /// card that stops your own creatures attacking. Written by blanking those
        /// phrases and asking the outward patterns again, so a card that taps one
        /// of yours AND one of theirs (Arena taps both, but only names the second
        /// obliquely) is judged on what is left rather than on which clause came
        /// first.</summary>
        private static bool OnlyRestrainsYourOwn(string text)
        {
            if (!TapsACreatureYouControl.IsMatch(text) && !YourOwnCreaturesCantAttack.IsMatch(text))
            {
                return false;
            }

            string rest = YourOwnCreaturesCantAttack.Replace(TapsACreatureYouControl.Replace(text, " "), " ");
            return !PacifyOutward.Any(p => p.IsMatch(rest)) && !AnyUntapLock.IsMatch(rest);
        }

        /// <summary>True when every Buff wording on the card sits inside a pump
        /// restricted to one creature type. Blank the tribal phrases out and ask
        /// again, so a lord that also pumps something unrestricted keeps the tag.
        /// </summary>
        private static bool OnlyPumpsATribe(string text)
        {
            if (!TribalPump.IsMatch(text))
            {
                return false;
            }

            string rest = TribalPump.Replace(text, " ");
            return !BuffPatterns.Any(p => p.IsMatch(rest))
                && !(CounterOnSomebodyElse.IsMatch(rest) && !CounterForAnOpponent.IsMatch(rest));
        }

        /// <summary>True when EVERY line that adds mana restricts what it may be
        /// spent on. A card with one restricted ability and one free one still
        /// fixes, which is why this is per line rather than a match anywhere.
        /// </summary>
        private static bool AllManaIsRestricted(string text)
        {
            bool addsAny = false;
            foreach (string line in text.Split('\n'))
            {
                if (!line.Contains("add ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                addsAny = true;
                if (!RestrictedMana.IsMatch(line))
                {
                    return false;
                }
            }

            return addsAny;
        }

        /// <summary>Whether every match of <paramref name="patterns"/> lands on the
        /// card itself, judged by the subject written in front of it. The window is
        /// the current LINE only (oracle text puts one ability per line), so a
        /// "target" in an unrelated ability cannot vouch for a self-buff two lines
        /// down. One match aimed elsewhere is enough to keep the tag.</summary>
        private static bool AimsOnlyAtItself(string text, Regex[] patterns, Card card, bool bareIsSelf)
        {
            string ownName = ShortName(card);
            bool sawSelf = false;

            foreach (Regex pattern in patterns)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    int lineStart = text.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
                    int from = Math.Max(lineStart, match.Index - 80);
                    string before = text.Substring(from, match.Index - from);

                    if (Beneficiary.IsMatch(before) || Beneficiary.IsMatch(AfterCounterClause(text, match)))
                    {
                        return false;
                    }

                    if (SelfReference.IsMatch(before)
                        || (ownName.Length > 0 && before.Contains(ownName, StringComparison.OrdinalIgnoreCase)))
                    {
                        sawSelf = true;
                        continue;
                    }

                    if (GrantVerb.IsMatch(before) || !bareIsSelf)
                    {
                        return false;
                    }

                    sawSelf = true;
                }
            }

            return sawSelf;
        }

        /// <summary>The one wording that names its beneficiary AFTER the ability:
        /// "put an indestructible counter on target creature". Deliberately gated
        /// on the word "counter" rather than reading ahead in general — a general
        /// look-ahead reads "gets +1/+1 for each other creature you control" as a
        /// gift to those creatures, when it is a self-buff that merely counts
        /// them. Empty for every other shape, and stops at "." and "(" so
        /// reminder text ("Ward {2} (Whenever this creature becomes the target
        /// ...)") cannot vouch for itself.</summary>
        private static string AfterCounterClause(string text, Match match)
        {
            int start = match.Index + match.Length;
            int end = Math.Min(text.Length, start + 50);

            if (!text.AsSpan(start, end - start).StartsWith(" counter", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            for (int i = start; i < end; i++)
            {
                if (text[i] is '\n' or '(' or '.')
                {
                    return text.Substring(start, i - start);
                }
            }

            return text.Substring(start, end - start);
        }

        /// <summary>The name a card uses to refer to itself in its own rules text:
        /// the front face, cut at the comma ("Multani, Yavimaya's Avatar" writes
        /// "Multani gets +1/+1").</summary>
        private static string ShortName(Card card)
        {
            string name = card.Name ?? string.Empty;

            int slash = name.IndexOf(" //", StringComparison.Ordinal);
            if (slash > 0)
            {
                name = name.Substring(0, slash);
            }

            int comma = name.IndexOf(',');
            if (comma > 0)
            {
                name = name.Substring(0, comma);
            }

            return name.Trim();
        }

        /// <summary>Whether the card's effect can be deployed in response: an
        /// instant, a card with flash, or anything with an activated ability (a
        /// cost, then a colon — Mother of Runes' <c>{T}:</c>, a Circle of
        /// Protection's <c>{1}:</c>). Sorceries, static abilities and
        /// enter-the-battlefield triggers do not qualify.</summary>
        private static bool IsInstantSpeed(Card card)
        {
            if ((card.TypeLine ?? string.Empty).Contains("Instant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (string keyword in card.Keywords ?? [])
            {
                if (string.Equals(keyword, "Flash", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            string text = card.OracleText ?? string.Empty;

            foreach (Regex pattern in ActivatedAbility)
            {
                if (pattern.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }

        // The two shapes an activated ability's cost takes before its colon. Both
        // are bounded to a single line so an unrelated symbol cannot pair with a
        // later colon.
        //
        // Loyalty abilities ("+1:", "-2:") are deliberately NOT here: a
        // planeswalker activates at sorcery speed, so it fails the gate the same
        // way a sorcery does. That is also why this cannot simply look for "any
        // short prefix then a colon".
        private static readonly Regex[] ActivatedAbility =
        [
            Rx(@"\{[^}\n]+\}[^:\n]{0,40}:"),
            Rx(@"^(?:sacrifice|discard|pay|exile|tap|untap|remove|return|reveal)\b[^:\n]{0,50}:",
                RegexOptions.Multiline),
        ];
    }
}
