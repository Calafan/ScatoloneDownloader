using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// The rule table — one entry per effect, a card gets the effect if ANY of its
    /// patterns hit — and the guards that are asked outside it. Part of
    /// <see cref="EffectClassifier"/>.
    /// </summary>
    internal static partial class EffectClassifier
    {
        // One entry per effect; a card gets the effect if ANY of its patterns hit.
        private static readonly (CardEffect Effect, Regex[] Patterns)[] Rules;

        // All seven are the SAME ARRAY the table holds, looked up once —
        // hoisted out of it for the same reason as MillPatterns and
        // ReanimatePatterns: the vetoes that strip a tag re-ask its own patterns
        // against the text with the offending shape blanked out, so a card that
        // selects on one line and makes bodies on another keeps what it earned.
        private static readonly Regex[] FilterPatterns;

        private static readonly Regex[] BurnPatterns;

        private static readonly Regex[] WipePatterns;

        private static readonly Regex[] DiscardPatterns;

        private static readonly Regex[] BouncePatterns;

        private static readonly Regex[] ManaFixingPatterns;

        private static readonly Regex[] CheatPatterns;

        // A STATIC CONSTRUCTOR rather than three field initialisers, and that is
        // forced by the split: initialisers run in textual order within a file but
        // in UNSPECIFIED order across the files of a partial class, so this table —
        // which reads two dozen pattern arrays that now live in the other files —
        // could be built from nulls. Every field initialiser is guaranteed to have
        // run before a static constructor body, so this is the one place the order
        // is safe.
        static EffectClassifier()
        {
            Rules =
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
                //
                // Widened again 2026-09-27, where 46 of the 73 disagreements on
                // 6,321 reviewed cards were: the sweeper was there and the words
                // around it were one more than the rules allowed.
                //   a TYPE LIST     — "destroy all artifacts, CREATURES, and
                //                     lands" (Jokulhaups, Nevinyrral's Disk) put a
                //                     comma before the noun;
                //   EACH            — "destroy EACH creature with mana value X or
                //                     less" (Wave of Terror, Wrath of the Skies,
                //                     Selective Obliteration) is "all" by another
                //                     word, and so is "destroy THOSE creatures";
                //   a BARE PLURAL   — "white creatures get -1/-1" (Dread of Night),
                //                     "creatures your opponents control get -1/-1"
                //                     (Seeker's Folly, Sephiroth, Wail of War) and
                //                     "other creatures get -2/-2" (Harvester of
                //                     Misery) never say all or each, and the
                //                     singular "each non-Dragon creature GETS -X/-X"
                //                     (Exude Toxin, Nuclear Fallout) nor the -1/-1
                //                     COUNTER on each creature (Harbinger of Night);
                //   the AMOUNT      — "damage EQUAL TO ITS POWER to each other
                //                     creature" (Showstopping Surprise, Waltz of
                //                     Rage), "THAT MUCH damage" (Territorial
                //                     Aetherkite), "damage to each creature ... equal
                //                     to" (Volcanic Eruption), X "DIVIDED among all
                //                     creatures" (Dwarven Catapult), and a player
                //                     hit "AND EACH CREATURE THAT PLAYER CONTROLS"
                //                     (Mob Verdict, Heart of Bogardan);
                //   AIRBEND ALL     — Avatar's Wrath;
                //   the SACRIFICE   — "sacrifices ALL creatures they control"
                //                     (Living Death, Destined Confrontation), "HALF"
                //                     or "A THIRD" of them (Zodiark, Pox), Balance's
                //                     "sacrifice creatures THE SAME WAY".
                // Four shapes match the words and are not this tag, each read where
                // it is asked: the plural shrink stops at "target creatures" (that
                // is Miasma Demon's Removal), at "creatures YOU CONTROL" (Vampirism,
                // Vibrating Sphere) and at ATTACKING creatures (Wind Shear); damage
                // to each other creature YOU CONTROL is Cinder Giant's price; CARDS
                // in a graveyard are not the board (Yggdrasil, Zombie Mob). The
                // combat-only and the dexterity sweepers are read after the
                // table, in Classify.
                (CardEffect.Wipe, [
                    Rx(@"(?:destroy|exile) (?:all|each)(?: (?!attached\b)[\w-]{1,15},?){0,4}(?: and| or)? (?:creature|permanent)"
                        // The creatures that CREWED it are your own, exiled and
                        // returned (Golden Argosy, untagged; 2026-10-14).
                        + @"(?![\w ]{0,12}\bcards?\b)(?!s? (?:of the chosen type|you control|chosen this way|that crewed))"
                        + @"(?!s? with (?:the same name|that name|the most votes))"),
                    // "Those creatures" is not a board when they are ONE per
                    // opponent: Highcliff Felidar's "for each opponent, choose a
                    // creature … Destroy those creatures" is Removal alone by hand
                    // (2026-10-20), read 2026-10-27.
                    Rx(@"(?<!don't )(?<!for each opponent, choose a creature [^.\n]{0,80}\. )destroy those creatures"
                        + @"|airbend all(?: [\w-]{1,15}){0,3} creatures"),
                    Rx(@"(?<!target |attacking |blocking )\bcreatures\b(?:(?! you control\b| with the same name\b)[^.\n]){0,60}? get -[\dX]+/-[1-9X]"),
                    Rx(@"\beach(?: [\w-]{1,15}){0,3} creature\b(?:(?! you control\b)[^.\n]){0,60}? gets (?:twice )?-[\dX]+/-[1-9X]"),
                    Rx(@"-1/-1 counters? on each(?: [\w-]{1,15}){0,3} creature\b(?! you control)"),
                    Rx(@"deals? (?:[\dX]+ |that much )?damage (?:equal to [^.\n]{1,60}? )?to each(?: [\w/-]{1,20}){0,4} creature\b(?!s? you control)"),
                    Rx(@"damage to [^.\n]{1,60}\band each creature (?:that player|they)\b[^.\n]{0,40}controls?"
                        + @"|damage divided [^.\n]{0,30}among all creatures"
                        // …and the same side swept after a kill with a count of
                        // its own: Fear, Fire, Foes!'s "X damage to target
                        // creature AND 1 DAMAGE TO EACH OTHER CREATURE WITH THE
                        // SAME CONTROLLER", tagged Wipe by the human on 2026-09-29.
                        + @"|\band [\dX]+ damage to each other creature (?:with the same controller|that player controls)"),
                    // "+1/+1" may stand between the creature and the hand (Wave
                    // Goodbye's "each creature without a +1/+1 counter on it",
                    // 2026-10-14).
                    Rx(@"return (?:all|each)(?![\w ,-]{0,30}attached to)[\w ,-]{0,30}(?:permanent|creature)s?[\w ,'+/-]{0,40}to (?:their owners'|its owner's) hands?"),
                    // Three more ways to empty the board, read 2026-10-14: every
                    // permanent of a player SHUFFLED into the library (Teferi,
                    // Temporal Pilgrim's -12), all but the creatures chosen
                    // destroyed (Mount Doom, Duneblast, Divine Reckoning), and any
                    // number of tapped creatures turned face down (Illithid
                    // Harvester).
                    Rx(@"shuffles? (?:each|all) [\w ]{0,20}(?:permanents?|creatures?)[^.\n]{0,40} into (?:its owner's|their owners') librar"
                        + @"|\bdestroy the rest\b"
                        + @"|turn any number of target [\w ]{0,30}creatures face down"),
                    // ONE creature each is an edict and is Removal, ruled 2026-09-27
                    // after the hand tags split three to two on the same sentence
                    // (Abyssal Gatekeeper against Accursed Marauder): "un singolo
                    // sacrifice lo mettiamo removal". Two or more each, or all, or
                    // the rest, empties the board. Two wordings read on 2026-10-17,
                    // each of which had held Wipe only through the land-sweep
                    // alternative overturned that day: Restore Balance puts
                    // Balance's line the other way round ("sacrifice creatures and
                    // discard cards the same way"), and Death Cloud's "sacrifices X
                    // creatures" closes a list of what each player loses.
                    Rx(@"each player[^.\n]{0,120}\b(?:creatures?|permanents?)\b[^.\n]{0,120}sacrifices the rest"
                        + @"|sacrifice creatures (?:and discard cards )?the same way"
                        + @"|each player[^.\n]{0,160}\bsacrifices? (?:all|half|a third of) (?:the |other )?(?:[\w-]+ )?creatures"
                        + @"|each player sacrifices (?!(?:a|an|one|the)\b)[\w ]{0,20}(?:creature|permanent)"
                        + @"|each player [^.\n]{1,100}, (?:then )?sacrifices (?!(?:a|an|one|the|that)\b)[\w ]{0,20}creatures\b"
                        + @"|each player chooses (?:two|three|\d+|x)\b[^.\n]{0,40}creatures they control\. (?:exile|destroy) them"),
                    // …but the SAME edict every turn, for every player, grinds the
                    // board away and is Wipe: Dystopia, The Abyss, ruled 2026-09-27.
                    // One LAND or one ARTIFACT a turn (Mana Vortex, Destructive
                    // Flow, Molder Slug) is the other tags' business, and so is
                    // Nature's Wrath, "that player sacrifices an Island or blue
                    // permanent" whenever one enters: Wipe on 2026-09-27, ruled
                    // LandDestruction alone on 2026-10-17, which took out the only
                    // alternative that read it.
                    Rx(@"at the beginning of each (?:player's )?upkeep, (?:that player sacrifices [\w ,-]{0,40}(?:creature|permanent)"
                        + @"|destroy target[^.\n]{0,40}creature that player controls)"),
                    // The old-set sweepers take everything printed in one expansion,
                    // however they word the kill — Golgothian Sylex and City in a
                    // Bottle make the controllers sacrifice. Ruled 2026-09-27.
                    Rx(@"permanents? with a name originally printed in the"),
                    // A permanent sacrificed FOR EACH permanent of a kind is every
                    // one of that kind: Omen of Fire takes all the white ones, ruled
                    // Wipe on 2026-09-28. A LAND paid per creature is a tax (Flooded
                    // Woodlands, Reclamation) and names no permanent here.
                    Rx(@"sacrifices? [^.\n]{0,60}\bpermanent\b[^.\n]{0,30} for each [\w ]{0,20}permanent they control"),
                    // The LANDS swept were a board too from 2026-10-16 ("mettiamo
                    // wipe a tutti questi spacca terra ad area", on Omen of Fire
                    // against Boil) until the ruling was overturned on 2026-10-17
                    // ("ribaltiamo il lavoro sui soli LandDestruction Wipe"), once
                    // the cube report counted Wipe among the creature answers: a
                    // sweep that takes ONLY lands — all lands or all of a basic type
                    // destroyed, exiled or returned (Armageddon, Boil, Sunder), every
                    // land sharing the name (Wake of Destruction), a pile of lands
                    // kept (Natural Balance, Global Ruin), lands sacrificed two or
                    // more each (Tectonic Break, Thoughts of Ruin) — answers no
                    // creature and is LandDestruction alone. The alternative that
                    // read them is gone, and 31 cards left the tag with it, 7 of
                    // them reviewed (Armageddon, Boil, Natural Balance …), which
                    // the human had tagged LandDestruction alone before that day.
                    // A sweep that takes the creatures WITH the lands (Jokulhaups,
                    // Devastation, Balance, Apocalypse, Wildfire's damage) is still
                    // Wipe through the patterns above.
                ]),

                // "Nonland permanent" is how the whole modern O-ring family is worded
                // (Stormplain Detainment, Web Up, Emergency Eject), and reading only
                // the bare "target permanent" left 22 of 31 hand-tagged cards
                // untouched. Ruled 2026-09-15: an answer that can point at ANY
                // permanent is RemovePermanent, not Removal, even when the card that
                // exiles it happens to be aimed at a creature in practice.
                // Recall 29.0% -> 83.9%, 24 wrong -> 12.
                // Read 2026-10-15: a permanent YOU OWN or control is no answer
                // (Despotic Scepter, Staff of Compleation, Telim'Tor's Edict, and
                // the blinks — Felidar Guardian, Displacer Kitten); "destroy up to
                // three OTHER target nonland permanents" is (Soul of Emancipation);
                // and so is the edict whose victim must share the card type of a
                // permanent you chose (Fatal Grudge) — the plain edict of "a
                // permanent of their choice" stays out, 3 reviewed against it
                // (Kefka, Throne of the Grim Captain, annihilator).
                //
                // Read later the same day: the permanent may be narrowed by more
                // than "nonland" — "target LEGENDARY permanent" (Commander Sofia
                // Daguerre), "NONCREATURE permanent" (Extinguisher Battleship),
                // "noncreature, nonland permanent" (Thief of Existence; Molten
                // Collapse, 3:1, handed back) — or be a TOKEN (Kraul
                // Whipcracker). A permanent CARD is in a graveyard (Savvy Trader,
                // Split the Spoils). A blink is no answer when the return follows
                // straight on, ", then return it" or ". Return that card"
                // (Eldrazi Confluence, Mistmeadow Vanisher) — a return further
                // off is somebody else's (Unyielding Gatekeeper returns only your
                // own; Dion destroys before it exiles). And The Spot hands its
                // exile back to the owners' hands, which the human tags Bounce.
                (CardEffect.RemovePermanent, [
                    Rx(@"(?:destroy|exile) (?:[\w ]{0,15})?target (?:(?:non[\w-]+|legendary),? )*permanent"
                        + @"(?!s? you (?:own|control)\b)(?!s? cards?\b)"
                        + @"(?![\w ,]{0,30}(?:,? then return it|\. return (?:it|that card)) to the battlefield)"
                        + @"(?![\s\S]*\breturn the exiled cards? to (?:their|its) owners?'? hands?)"
                        + @"|destroy up to \w+ other target nonland permanents"),
                    Rx(@"(?:destroy|exile) (?:[\w ]{0,15})?target tokens?\b"),
                    // A permanent SHUFFLED into its owner's library, as a creature
                    // is for Removal (2026-10-14): "the owner of target
                    // nonenchantment permanent shuffles it into their library"
                    // (Audacious Swap, Removal by hand) ruled RemovePermanent alone
                    // on 2026-10-27 ("Solo RemovePermanent"); Chaos Warp and
                    // Oblation follow.
                    Rx(@"owner of target (?:(?:non[\w-]+|legendary),? )*permanent[\w ,-]{0,40} shuffles it into"),
                    Rx(@"\bchooses an? permanent they control that shares a card type\b[^.\n]{0,60}\bsacrifices it\b"),
                    // The victim named by its mana value, then destroyed on the
                    // reflexive trigger (Behemoth of Vault 0), and the permanents
                    // each player chooses among yours (The Legend of Yangchen).
                    Rx(@"target nonland permanent's [\w ]{0,20}\. when you do, destroy that permanent"
                        + @"|chooses up to one permanent [^.\n]{0,60}\bpermanents your opponents control\. exile those permanents"),
                    // A COLOUR-restricted permanent kill belongs here rather than to
                    // Removal, ruled 2026-09-20. It was tried as Removal the day
                    // before, on the reading that "destroy target red permanent" is
                    // aimed at a creature in practice — but the BREADTH is what this
                    // tag is for, and it is the same reading that keeps Vindicate
                    // here even though you usually point it at a creature. Active
                    // Volcano, Flash Flood and both Paladins.
                    //
                    // NB the colours are listed one by one on purpose. A "non\w+"
                    // alternation written to catch "nonblack permanent" also catches
                    // "NONLAND permanent", which the first rule above already reads —
                    // 26 false positives in one measurement while it sat on Removal.
                    Rx(@"(?:destroy|exile) target (?:white|blue|black|red|green|colorless|multicolored"
                        + @"|nonwhite|nonblue|nonblack|nonred|nongreen)[\w ]{0,15}permanent")]),

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
                //
                // Widened 2026-09-19, where all 23 of the misses were: the land is
                // almost never alone in the sentence. It sits in a TYPE LIST
                // ("destroy target artifact, creature, or land" — Aftershock, Creeping
                // Mold, Amulet of Unmaking), or behind a count ("destroy UP TO ONE
                // target artifact, up to one target creature, and up to one target
                // land" — Boom Box), or beside a second target ("destroy target
                // creature AND target land" — Fumarole), and the {0,2} filler words
                // could not cross a comma to reach any of them. The mass form has the
                // same problem: "destroy all CREATURES AND lands" (Devastation).
                //
                // A BASIC LAND TYPE is a land by another name, and the only wording
                // Boil, Boiling Seas, Acid Rain and Reign of Chaos ever use. The \b
                // that keeps "islandwalk" out is still doing its job — the letters of
                // "land" inside "Island" have a word character in front of them.
                // Four shapes match the words and are not this tag; they are read line
                // by line in AnswersALand below.
                (CardEffect.LandDestruction, LandDestructionPatterns),

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
                // The lookbehind is load-bearing in every one of these: NONCREATURE
                // ends in "creature", so "destroy target noncreature artifact"
                // (Gorilla Shaman, Joven) read as a kill. Same shape as tap/untap.
                (CardEffect.Removal, [
                    // The exile filler may not cross a CARD: "exile target card
                    // from a graveyard and put an oil counter on this creature"
                    // (Armored Scrapgorger) found its creature at the far end.
                    Rx(@"destroy target[\w ]*(?<!non)creature"), Rx(@"exile target(?:(?! cards?\b)[\w ])*(?<!non)creature"),
                    Rx(@"destroy target[\w ]*((?<!non)creature|planeswalker)"),
                    // The AMOUNT is written as freely as the target: "half X damage,
                    // rounded down" (Banshee), "X plus 1 damage" (Meteor Shower). And
                    // "any target" has two more spellings — "any OTHER target"
                    // (Self-Destruct) and "EACH OF X targets" (Firestorm).
                    // "Each of UP TO two targets" too (Chandra, Hope's Beacon's −X),
                    // read 2026-10-14.
                    Rx(@"deals? (?:half )?[\dX]+(?: plus \d+)? damage(?:, rounded (?:down|up),)? "
                        + @"to (?:any (?:other )?target|that creature(?!'s)|each of (?:up to )?(?:[\dX]+|two|three) targets)"),
                    // The SECOND half of a split shot, which has no "deals" of its
                    // own: "4 damage to another target battle or opponent AND 1
                    // damage to up to one target creature" (Invasion of Regatha).
                    Rx(@"\band [\dX]+ damage to (?:up to one )?target [\w ]{0,20}(?<!non)creature"),
                    // The damage rules could not read a QUALIFIED target — "damage to
                    // target ATTACKING creature", "to target creature AN OPPONENT
                    // CONTROLS" — which was 23 of the misses on its own, and the
                    // amount is allowed to come after the target as well.
                    // NB the qualifier deliberately cannot cross a COMMA. Letting it
                    // reach "4 damage to target ATTACKING, BLOCKING, OR TAPPED
                    // creature" (Sonar Strike) also lets it run past a sentence into
                    // the next clause: measured 2026-09-19, two right for two wrong.
                    // "Deals TWICE X damage to target creature" (Torch the Witness),
                    // read 2026-10-16.
                    Rx(@"deals? (?:twice )?[\dX]+ damage to [\w ]{0,20}target [\w -]{0,28}(?<!non)creature"),
                    // …and the ONE qualifier that does cross commas, spelled out
                    // so the qualifier above need not: Sonar Strike's "4 damage
                    // to target attacking, blocking, or tapped creature" (read
                    // 2026-10-16).
                    Rx(@"deals? [\dX]+ damage to target attacking, blocking, or tapped creature"),
                    // Creatures NAMED by an earlier clause and hit as a group, read
                    // 2026-10-16: Winter Blast's "Tap X target creatures. Winter
                    // Blast deals 2 damage to each of those creatures with flying".
                    Rx(@"\btap x target creatures\.[^\n]*deals? [\dX]+ damage to each of those creatures"),
                    // "That much damage" is an amount too — Screaming Nemesis hands
                    // back whatever it was dealt.
                    // And at a target CREATURE, however the amount was reached —
                    // "twice that much" for the lands discarded (Borborygmos and
                    // Fblthp), the counters removed (Arcee, Sharpshooter), the
                    // combat damage just dealt (The Sea Devils). Ruled 2026-10-14,
                    // "Tutte Removal".
                    Rx(@"deals? that much damage to any (?:other )?target"
                        // "Deals THAT AMOUNT of damage to target creature that
                        // player controls" (Fear of Burning Alive), read 2026-10-16.
                        + @"|deals? (?:twice )?that (?:much|amount of) damage to target [\w ]{0,20}(?<!non)creature"
                        // "It deals THAT damage to target creature INSTEAD" (Soltari
                        // Guerrillas), read 2026-10-16.
                        + @"|deals? that damage to target [\w ]{0,20}(?<!non)creature instead"),
                    Rx(@"deals damage to [\w ]{0,20}target [\w -]{0,28}(?<!non)creature equal to"),
                    // A target CHOSEN first and hit after, read 2026-10-16:
                    // "Choose target creature or planeswalker. … deals that much
                    // damage to that permanent" (Galvanic Discharge).
                    // …and "deals 3 damage TO IT" (Grisly Sigil), read 2026-10-27.
                    Rx(@"choose target creature or planeswalker\.[^\n]*deals? (?:that much damage to that permanent|[\dX]+ damage to it\b)"),
                    // An Aura that taxes the creature out of the game, read
                    // 2026-10-16: Mana Chains' "Enchanted creature has
                    // 'Cumulative upkeep {1}.'" (the one card written so). Paid in
                    // LIFE it is the same Aura, ruled the same day ("Removal, no
                    // Pacify"): Decomposition's "Cumulative upkeep—Pay 1 life"
                    // was Burn and Pacify by hand and is now Burn and Removal —
                    // a tax the controller can pay is not a lock (2026-09-16).
                    Rx(@"enchanted creature has ""cumulative upkeep(?: \{|—pay \d+ life)"),
                    // A fireball split between several things still kills one of them.
                    // The amount may be "equal to" something, read 2026-10-27 (2 of
                    // 2 reviewed: Captain America, First Avenger's Equipment's mana
                    // value, Orca, Siege Demon's power).
                    Rx(@"deals? (?:half )?[\dX]+(?: plus \d+)? damage divided "
                        + @"(?:evenly, rounded down, |as you choose )?among"
                        + @"|deals? damage equal to [^.\n]{0,80}? divided (?:evenly, rounded down, |as you choose )?among"),
                    // A fight has to name what it fights. The bare word also appears
                    // in the NAME of a mode — School Daze offers "Fight Crime", which
                    // counters a spell and draws a card.
                    Rx(@"\bfights? [\w ,'-]{0,30}target|\bfights? (?:each|it)\b"),
                    // A pump that SENTENCES the creature, read 2026-10-16: Blood
                    // Frenzy's "Target attacking or blocking creature gets +4/+0
                    // until end of turn. Destroy that creature at the beginning
                    // of the next end step." The target is directly "creature
                    // gets", so Mogg Cannon and Stone Giant ("target creature
                    // YOU CONTROL gets …") stay out.
                    Rx(@"target [\w -]{0,28}(?<!non)creature gets [+-][\dX]+/[+-][\dX]+ until end of turn\. destroy that creature at the beginning of the next end step"),
                    // …and the shrink that KILLS only if the power runs out,
                    // read 2026-10-16: Depressurize's "Target creature gets
                    // -3/-0 until end of turn. Then if that creature's power
                    // is 0 or less, destroy it." (-N/-0 alone is Pacify's.)
                    Rx(@"target [\w -]{0,28}(?<!non)creature gets -[\dX]+/-0 until end of turn\. then if that creature's power is 0 or less, destroy it"),
                    // An edict by STATISTIC, read 2026-10-16: Drop of Honey's "At
                    // the beginning of your upkeep, destroy the creature with the
                    // least power" kills whoever is smallest, every turn.
                    Rx(@"destroy the creature with the (?:least|lowest|greatest|highest|most) (?:power|toughness)"),
                    // The creature the table VOTES out, read 2026-10-16: Vault 11:
                    // Voter's Dilemma's "destroy each creature with the most votes
                    // or tied for most votes". Sail into the West's votes pick a
                    // mode, not a victim, and stay out.
                    Rx(@"destroy each creature with the most votes"),
                    // A creature CHOSEN first and killed later, read 2026-10-16:
                    // "Choose target creature with mana value 3 or less … Exile
                    // the chosen creature" (Expel the Unworthy), "Choose any number
                    // of target creatures with equal toughness. Destroy the chosen
                    // creatures" (V.A.T.S.), and "each player chooses a creature
                    // you don't control. Destroy the chosen creatures" (Sadistic
                    // Shell Game). Not Plasma Caster, whose "chosen creature" is
                    // only the one blocking its wearer.
                    // Read 2026-10-27 from the sitting of 10-20..10-26, one card
                    // each, all tagged: "destroy the chosen PERMANENT" after
                    // "choose target creature, planeswalker, or Vehicle" (Quag
                    // Feast); one target per opponent hit "to each of the chosen
                    // creatures" (Mega Flare); ONE creature each player chooses,
                    // which is the edict of 2026-09-27 (The Horus Heresy — Call to
                    // the Void's two each is Wipe by hand); and one creature per
                    // opponent named by its power and "destroy those creatures"
                    // (Highcliff Felidar, Removal and not Wipe by hand).
                    Rx(@"choose (?:any number of )?target [\w ]{0,40}creatures?(?! that's)[^\n]*?(?:exile|destroy) the chosen (?:creature|permanent)s?"
                        + @"|each player chooses a creature you don't control\. destroy the chosen creatures"
                        + @"|choose up to one target creature that player controls\.[^\n]*? damage [^.\n]{0,80}?to each of the chosen creatures"
                        + @"|each player chooses a creature\. destroy each creature chosen this way"
                        + @"|for each opponent, choose a creature [^.\n]{0,80}\. destroy those creatures"),
                    // An edict, in every wording. "EACH PLAYER sacrifices A creature"
                    // costs you one too, and was declined here until 2026-09-27, when
                    // the hand tags were found split three to two between this and
                    // Wipe on the identical sentence: ONE creature each is an edict,
                    // ruled Removal (Fleshbag Marauder, Plaguecrafter, Tariff). Two or
                    // more each is Wipe's. The edict can EXILE instead of sacrificing,
                    // and then says "chooses … and exiles it": Sothera does it every
                    // time one of yours dies, and the human ruled it Removal on
                    // 2026-09-28 because it only ever lands on the opponents.
                    // DEFENDING PLAYER is the same opponent named by the attack:
                    // Witch-king's edict on every attack, tagged Removal by the
                    // human on 2026-09-29. "UNLESS defending player sacrifices"
                    // is the opponent's choice of a price (Ogre Marauder), not
                    // an edict, and stays out.
                    // Read 2026-10-14 from the sitting of 10-06..10-10: the edict
                    // that EXILES ("target opponent exiles a creature they
                    // control" — Blot Out, Debt to the Kami), the one offered as
                    // a VILLAINOUS CHOICE (Midnight Crusader Shuttle, The Dalek
                    // Emperor), and the life-or-creature price EACH PLAYER pays
                    // every turn (Bellowing Mauler, Removal and Burn by hand) —
                    // of a creature alone, so Tomb of Annihilation's "a
                    // creature, artifact, or land" stays out. MEASURED AND
                    // DROPPED: keeping "exiles a creature CARD" out of the
                    // exile edict — the one card it reaches (Valki's "for each
                    // opponent, exile a creature card") is Removal by its back
                    // face anyway.
                    Rx(@"(?:target player|target opponent|each opponent|(?<!unless )defending player)[\w ,]{0,30}(?:sacrifices?|exiles?) (?:a|an|one|two|\d+)[\w ]{0,25}(?<!non)creature"
                        + @"|each player sacrifices (?:a|an|one|the) [\w ]{0,25}(?<!non)creature"
                        + @"|(?:each|target) opponent chooses (?:a|an|one) [\w ,-]{0,30}creature they control and (?:exiles|sacrifices) it"
                        + @"|villainous choice — that player sacrifices a [\w ]{0,25}(?<!non)creature"
                        + @"|loses? [\dX]+ life unless they sacrifice a (?:nontoken )?creature\b(?!,)"
                        // PUNISHER = Removal, ruled 2026-10-16: an opponent (or each
                        // player) choosing between losing a creature and something
                        // else is an edict like Bellowing Mauler's. "Unless THEY /
                        // THAT PLAYER sacrifice a creature" reaches Mogis, Tomb
                        // Blade, Unnatural Hunger and Demanding Dragon; Pillar Tombs
                        // of Aku asks the same as "may sacrifice … If that player
                        // doesn't". Never "unless YOU sacrifice" (your own cost),
                        // never "defending player" (Ogre Marauder's price for
                        // evasion), and the comma keeps Tomb of Annihilation's "a
                        // creature, artifact, or land" out.
                        // Read 2026-10-16: the edict between TWO named creatures —
                        // Retribution's "Choose two target creatures controlled by
                        // the same opponent. That player chooses and sacrifices one
                        // of those creatures."
                        // …and HALF of them, read 2026-10-16: Rush of Dread's "Target
                        // opponent sacrifices half the creatures they control".
                        + @"|target opponent sacrifices half the creatures they control"
                        // …and the edict that takes a permanent FIRST, read 2026-10-16:
                        // Vile Mutilator's "each opponent sacrifices a nontoken
                        // enchantment of their choice, then sacrifices a nontoken
                        // creature of their choice".
                        + @"|each opponent sacrifices an? [\w ]{0,30} of their choice, then sacrifices an? (?:nontoken )?creature"
                        // …"the same PLAYER … sacrifices one of THEM" (Incriminate),
                        // and the creature you and the opponent each choose secretly
                        // among theirs (Malik, Grim Manipulator), read 2026-10-27.
                        // Not ANNIHILATOR, whose victim is a permanent of their
                        // choice: ruled 2026-10-27 ("No, togli Removal"), 6 of the
                        // 7 reviewed cards untagged, Nazgûl Battle-Mace corrected
                        // by name.
                        + @"|choose two target creatures controlled by the same (?:opponent|player)\. that player (?:chooses and )?sacrifices one of (?:those creatures|them)"
                        + @"|secretly choose a creature that player controls\.[^\n]*?that player sacrifices those creatures"
                        + @"|\bunless (?:they|that player) sacrifices? (?:a|an|another) (?:nontoken )?creature\b(?!,)"
                        + @"|that player may sacrifice a [\w ]{0,25}(?<!non)creature of their choice\. if that player doesn't"),
                    // A creature that ends up in a LIBRARY is as answered as one that
                    // is destroyed, and the Auras are the only place this wording
                    // appears: The Spot's Portal puts it on the bottom, Dramatic
                    // Accusation, Stay Hidden Stay Silent and Watery Grasp shuffle it
                    // in. Added 2026-09-19.
                    Rx(@"put target[\w ,'-]{0,30}(?<!non)creature[\w ,'-]{0,25}on the bottom of[\w ']{0,25}library"),
                    // Read 2026-10-16: "Shuffle THIS CREATURE and target creature
                    // with a stun counter on it into their owners' libraries"
                    // (Floodpits Drowner).
                    Rx(@"shuffle this creature and target [\w ,-]{0,40}(?<!non)creature[\w ,-]{0,40} into their owners' libraries"
                        + @"|shuffles? enchanted creature into[\w ']{0,25}library"
                        + @"|enchanted creature'?s owner shuffles it into"
                        // …and from a spell or an ability, read 2026-10-14 (4 of 4 in
                        // the sitting of 10-09): "target creature's owner shuffles it
                        // into" (This Is How It Ends, Ominous Cemetery), "the owner of
                        // target artifact or creature … shuffles it" (Zoyowa's
                        // Justice), "choose target creature. Its owner shuffles it"
                        // (Blink).
                        + @"|target [\w ,-]{0,40}(?<!non)creature's owner shuffles it into"
                        + @"|owner of target [\w ,-]{0,30}(?<!non)creature[\w ,-]{0,40} shuffles it into"
                        + @"|choose target [\w ,-]{0,30}(?<!non)creature\. its owner shuffles it into"),
                    // A creature that stops being one is as answered: it becomes a
                    // Treasure, a Clue or a Food (Vraska, Betrayal's Sting's −2,
                    // Kitesail Larcenist, In Too Deep), or a face-down 2/2 with
                    // nothing printed on it (Cyber Conversion). Read 2026-10-14.
                    Rx(@"target [\w ,-]{0,40}creature becomes a (?:Treasure|Clue|Food)\b"
                        + @"|chosen permanents become (?:Treasure|Clue|Food)\b"
                        + @"|enchanted (?:permanent|creature) is a colorless (?:Treasure|Clue|Food)\b"
                        + @"|turn target [\w ,-]{0,30}(?<!non)creature face down"),
                    // Phased out for as long as the card that did it stays, the
                    // way an O-ring exiles (Oubliette, The Phasing of Zhalfir).
                    // "For as long as The Pandorica REMAINS TAPPED" is a tapper's
                    // lock and Pacify's, by hand.
                    Rx(@"target [\w ,-]{0,30}(?:creature|permanent) phases out"
                        + @"(?: until [\w' ]{0,40}leaves the battlefield|\. it can't phase in for as long as you control)"),
                    // An Aura that can EXILE what it holds, for a price (Cooped
                    // Up, Redemption Arc, Utter Insignificance: 3 of 4 by hand).
                    // The full stop keeps Flickerform's "exile enchanted creature
                    // AND ALL AURAS" — a blink of your own — out.
                    Rx(@": exile enchanted creature\.")]),

                // The stack answer, whatever it answers. Read 2026-10-13 from the
                // sitting of 10-06..10-11: an ABILITY countered is the same
                // answer ("Sì, tutti Counter" — Tishana's Tidebinder, Emerald
                // Dragon, Brown Ouphe, Rust, Interdict, with Adric and Ayesha
                // Tanaka's "unless that ability's controller pays" among them);
                // "counter ALL other spells" is the mass one (Reverse the
                // Polarity); and "return TARGET spell to its owner's hand" takes
                // it off the stack as surely — 6 of 6 reviewed (Reprieve,
                // Spellscorn Coven, Obscura Interceptor, and beside the Bounce of
                // their permanent half Press the Enemy, Sink into Stupor, Jeskai
                // Revelation). Krark's "return THAT spell" is a coin flip on its
                // own and stays out. The same sitting reads the other ways a
                // spell leaves the stack, every one tagged by hand: a spell CHOSEN
                // first and countered later in the text (Aether Spike, Swallowed
                // by Leviathan, Invert Polarity), EXILED (Smirking Spelljacker,
                // Ertai's Meddling), put into its owner's LIBRARY (Sudden
                // Setback), AIRBENT (Aang, Swift Savior); and the counter of
                // "artifact, creature, or planeswalker spell" across its commas
                // (Strix Serenade) and of "up to one" (Repulsive Mutation).
                //
                // Not a counter: one of YOUR OWN spells (Goblin Artisans), and
                // one that targets THIS creature, which the card protects itself
                // with (Mistfolk) — both untagged. A counterspell printed in a
                // created TOKEN's quotes is the token's (Mage's Attendant's
                // Wizard), as its damage is (DamageFromSomethingItMade).
                (CardEffect.Counter, [
                    Rx(@"(?<!\btokens? with ""[^""]{0,120})counter (?:up to one )?target [\w ,]{0,60}?spell(?! you control| that targets this\b)"),
                    Rx(@"counter (?:up to one )?target (?:activated|triggered)\b"),
                    Rx(@"counter all (?:other )?spells\b"),
                    Rx(@"return (?:up to one )?target spells?\b[^.\n]{0,60}?to its owner's hand"),
                    Rx(@"\bchoose target spell\b[\s\S]{0,250}?\bcounter (?:that|the chosen) spell\b"),
                    Rx(@"\bexile (?:up to one )?target spells?\b|\btarget spell's controller exiles it\b"),
                    Rx(@"\bowner of target spell\b[^.\n]{0,60}\bputs it\b"
                        + @"|\bchoose target spell\b[^.\n]{0,60}\.\s*its owner puts it\b"),
                    Rx(@"(?<!\bto )\bairbend [^.\n]{0,40}\bspells?\b")]),

                // The other way to answer something on the stack. "copy target" is
                // required rather than the bare word "copy": a token that enters "as a
                // copy OF target creature" is Tokens, not stack interaction.
                // Widened 2026-09-19 for the possessive: Meddle says "change THAT
                // SPELL'S TARGET to another creature" where Deflection says "change
                // the target of", and no rule reading the second could see the first.
                //
                // The DELAYED COPY was tried the same day and ruled out: "when you
                // next cast an instant or sorcery spell this turn, copy it … you may
                // choose new targets for the copy" is a card doubling a spell of YOUR
                // OWN, which is not acting on somebody else's spell on the stack. It
                // matched 8 reviewed cards, 4 tagged and 4 not, and Ether reads word
                // for word like Jeong Jeong; the ruling took the tag off all eight
                // rather than keep a rule that could not tell them apart. What stays
                // is the aimed copy — "copy TARGET instant or sorcery spell" — which
                // is Fork, Reverberate and Twincast.
                (CardEffect.Redirect, [Rx(@"change the targets? of"),
                    Rx(@"change (?:that|target) spell's targets?"),
                    // "Copy ANY NUMBER OF target instant AND/OR sorcery spells"
                    // (Display of Power), read 2026-10-15. A copy of an ABILITY of
                    // your own is not this: "you may copy that ability. You may
                    // choose new targets for the copy" carries no tag on 6 of 7
                    // reviewed cards (Ertha Jo, Rowan's Talent, Verrak, Firebender
                    // Ascension; Ashnod handed back).
                    Rx(@"cop(?:y|ies) (?:any number of )?target[\w /]*(?:spell|ability)"),
                    // Two more ways to act on a spell already on the stack, both
                    // tagged and neither read before 2026-09-28: choosing NEW
                    // TARGETS for other spells outright (Boltbender), and taking
                    // CONTROL of the spell (Invert Polarity), which is not Steal —
                    // a spell is not a permanent.
                    Rx(@"choose new targets for (?:any number of )?(?:other |target )?spells?"
                        + @"|gains? control of (?:target|that) spell"),
                    // Copying a spell of YOUR OWN is this tag too, ruled 2026-09-28,
                    // which OVERTURNS the 2026-09-19 ruling that took it off: every
                    // such card reviewed since 09-23 had been tagged (Sword of Wealth
                    // and Power, Cursed Recording, Breeches, Jace Reawakened, Leyline
                    // of Resonance, Sunken Palace), and Ether reads word for word like
                    // Sword. Read two ways — the copy that gets NEW TARGETS, and the
                    // trigger that copies "your second spell" without saying so
                    // (Taigam). A spell that copies ITSELF is not this: a keyword's
                    // own reminder says "when you cast THIS SPELL, copy it" (Storm on
                    // Tempest Technique) and so do Mentor's Guidance, Show of
                    // Confidence and Banish into Fable. A keyword GIVEN to your other
                    // spells is the same copy and is read through its reminder, which
                    // says "that spell" or "it" instead — storm (Crackling
                    // Spellslinger), replicate (Djinn Illuminatus), conspire (Wort).
                    // A bracket guard and a dedicated storm rule were both tried and
                    // removed: the first kept those grants out, the second never fired
                    // once the reminder was read. A copy counted by COMMANDER casts is
                    // none at all in the cube (Thunderclap Drake). A copy of a
                    // CREATURE spell is a token and is Tokens, not this — see
                    // CopiesYourCreatureSpell.
                    Rx(@"(?<!\bcast this spell\b[^.\n]{0,80})\bcop(?:y|ies) (?:it|that spell(?: or ability)?|them)\b"
                        + @"[^.\n]{0,60}?(?:\.|,| and) you may choose new targets for the cop(?:y|ies)"),
                    Rx(@"\b(?:whenever|when) you (?:next )?cast "
                        + @"(?!this spell\b)(?![^.\n,]{0,60}\b(?:creature|outlaw|permanent) spell)[^.\n,]{0,60}\bspells?\b[^.\n,]{0,40}, "
                        + @"copy (?:it|that spell)\b(?! for each time you've cast your commander)")]),

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
                //
                // A SPELL returned and nothing else is Counter instead (Reprieve,
                // Spellscorn Coven, 2026-10-13); "target spell OR nonland
                // permanent" keeps this for its permanent.
                (CardEffect.Bounce, [
                    Rx(@"return (?:[\w' ]{0,30})?target(?! spells?(?: you don't control)? to )[\w ,']*to (?:its|their) owner(?:'s|s'|s)? hands?"),
                    // …but not LANDS alone: returning every Island is a mana base
                    // set back, which the human tags LandDestruction and Wipe and not
                    // this (Omen of Fire, 2026-09-28).
                    // A hyphen and "+1/+1" may stand in the list, 2026-10-14
                    // (Jin-Gitaxias's "all non-Phyrexian creatures", Wave Goodbye).
                    Rx(@"return (?:each|all|every)(?! (?:lands|islands|plains|swamps|mountains|forests)\b)[\w ,'+/-]*to (?:its|their) owner(?:'s|s'|s)? hands?"
                        // Hurkyl's Recall names the owner as the TARGET instead.
                        + @"|return all [\w ]{0,30}target player owns to their hand"),
                    // The owner's choice of TOP OR BOTTOM of the library is the
                    // same tempo answer as a return to hand: 9 of 9 reviewed
                    // cards that say it are tagged Bounce (Trip Up, Ice Magic,
                    // Jailbreak Scheme), and none was read before 2026-09-25.
                    // Widened 2026-09-29 for "THE OWNER OF TARGET nonland permanent
                    // puts it on their choice" (Riverwalk Technique, Sudden Setback).
                    Rx(@"owner (?:of target [^.\n]{0,40})?puts it on their choice of the top or bottom of their library"),
                    // …and the other library placements the hand tags carry on every
                    // reviewed card (8 of 8), read the same day: ON TOP of its
                    // owner's library (Time Ebb, Ether Well — "creature" must meet
                    // "on top" directly, which is what keeps Civic Guildmage's
                    // "target creature YOU CONTROL on top", a cost, out; a guard for
                    // it was written and turned nothing red), and SECOND or THIRD
                    // FROM THE TOP (Deem Inferior, Lost Days, Wan Shi Tong, Riptide
                    // Gearhulk) — of a TARGET, because the God-Eternals, Borborygmos
                    // and Fblthp and Braided Net put THEMSELVES there, and Lost Hours
                    // a card from a hand.
                    Rx(@"put target [\w ]{0,30}creature on top of its owner's library"
                        // FOURTH joined 2026-10-14 (Lost to Legend).
                        + @"|\btarget [^.\n]{0,60}\binto (?:their|its owner's) library (?:second|third|fourth) from the top"),
                    // Creatures the OPPONENTS chose, returned (Summon: Valefor),
                    // and the permanent a target opponent chooses (Teferi,
                    // Temporal Pilgrim's -12, 2026-10-14).
                    Rx(@"return those (?:creatures|permanents) to their owners' hands"
                        + @"|chooses an? [\w ]{0,20}(?:permanent|creature) they control and returns it to its owner's hand"),
                    // Ruled 2026-09-29. AIRBEND aimed where an opponent's permanent
                    // can be is this tag ("airbend sull'opponent è bounce"):
                    // Airbending Lesson, Glider Staff, Aang, the Last Airbender,
                    // Avatar's Wrath's "all other creatures". Airbending YOUR OWN is
                    // Protection instead (Airbender's Reversal, Appa, Steadfast
                    // Guardian, Monk Gyatso's "that creature"), and "to airbend a
                    // creature" is only the reminder.
                    Rx(@"(?<!\bto )\bairbend (?!that creature)(?:up to \w+ |any number of |all |another |each )?(?:other )?(?:target )?"
                        + @"(?![^.\n]{0,50}\byou control)[^.\n]{0,40}?\b(?:creatures?|permanents?|spells?)\b"),
                    // An Aura's creature returned, however it is paid for (Phantom
                    // Wings, Sun Clasp — "bounce entrambe", 2026-09-29).
                    Rx(@"return enchanted creature to its owner's hand"),
                    // A permanent exiled until the card leaves or dies and then
                    // returned to its owner's HAND is bounced, if late (Aurelia's
                    // Vindicator, The Spot — ruled Bounce and Regrowth, 2026-09-29).
                    Rx(@"exile (?:up to (?:\w+|x) )?(?:other )?target (?:nonland permanents?|creatures?)[\s\S]{0,250}"
                        + @"return the exiled cards to their owners' hands"),
                    // …and one its OWNER MAY PLAY for as long as it stays exiled is
                    // the same card back in hand (Extraordinary Journey, Bounce by
                    // hand, 2026-10-09). Read 2026-10-14. The other word order,
                    // "For as long as that card remains exiled, its owner may play
                    // it", on "up to one other target tapped creature or Vehicle"
                    // (Prowl, Stoic Strategist, Bounce by hand), read 2026-10-27.
                    Rx(@"exile (?:up to (?:\w+|x) )?target [\w ]{0,30}creatures?\.[^.\n]{0,30}\bits owner may play it for as long as it remains exiled"
                        + @"|exile (?:up to (?:\w+|x) )?(?:other )?target [\w ]{0,30}creatures?(?: or vehicles?)?\. for as long as that card remains exiled, its owner may play it")]),

                // The old rule was two lines of `destroy target[\w ]*(artifact|
                // enchantment)`, and that `[\w ]*` was free to run the length of the
                // sentence. It is where 11 of the 15 false positives came from, in two
                // shapes: "destroy target NONartifact, nonblack creature" (Terror,
                // Nekrataal, The Abyss) reaches the noun straight through "non", and
                // "exile target nonland permanent an opponent controls until this
                // ENCHANTMENT leaves the battlefield" (Web Up, Seam Rip, White
                // Auracite) reaches it through the reminder of where the card comes
                // back from. The filler is bounded now, and "non"/"non-" cannot be
                // crossed to get to the noun.
                //
                // The misses were counting words. The rule wanted "destroy target
                // <noun>" and nothing else, so "destroy UP TO ONE target artifact",
                // "destroy X target artifacts", "exile TWO target artifacts",
                // "destroy ANOTHER target artifact" and every mass "destroy all
                // enchantments" walked past it — 20 of the 28 misses between them.
                // AURA is the third noun: Serene Heart and Hope Charm answer the same
                // cards this tag exists for.
                //
                // An ARTIFACT CREATURE is a creature, and the human tags it Removal:
                // Chandler and Hearth Charm both read "destroy target artifact
                // creature" and neither is tagged here, so the noun refuses to be
                // followed by "creature".
                //
                // An AURA ATTACHED TO something named is about that thing, not about
                // the Aura: Savaen Elves and Pyramids ("destroy target Aura attached
                // to a land") are land protection, Miracle Worker pulls an Aura off
                // your own creature, and Hakim and Gauntlets of Chaos strip the ones
                // on a permanent the card itself just touched. A bare "destroy target
                // Aura" (Hope Charm) or "destroy all Auras" (Serene Heart) still
                // counts, which is why the noun tests only for the words that follow.
                //
                // Three more shapes match the words and are not this tag; they are
                // read line by line in AnswersAnArtifactOrEnchantment below.
                (CardEffect.Disenchant, DisenchantPatterns),

                // Same reading as Mill, and the same result. A bare "discard a card"
                // is overwhelmingly a COST — madness, blitz, cycling reminder text,
                // the second half of looting — rather than an attack on a hand.
                // Naming the victim took Discard from 129 wrong out of 188 fired to
                // 16 wrong out of 69, the largest precision gain on the board.
                // And the hand attack that EXILES rather than discards: "target
                // opponent reveals their hand. You choose a nonland card from it
                // and exile that card" — 4 of 5 reviewed cards tagged (Aggressive
                // Negotiations, Severance Priest, Soul Search, Cruelclaw's Heist;
                // Intimidation Tactics is not). Added 2026-09-25.
                //
                // Widened 2026-09-28 for five wordings, each tagged on every
                // reviewed card that uses it and none read: the PUNISHER, "loses 3
                // life unless that player sacrifices a nonland permanent or
                // DISCARDS A CARD" (Perforating Artist, Starseer Mentor, Forbidden
                // Ritual — 6 of 6); the chosen card discarded by a PRONOUN, "you
                // choose a nonland card from it … THEY discard that card" (Binding
                // Negotiation, Leshrac's Sigil, Thought-Stalker Warlock — 11 of 11);
                // "they discard" after a player was named (Sonic Shrieker, 4 of 4);
                // a LIST, "target opponent sacrifices a creature, DISCARDS A CARD,
                // and loses 3 life" (Scarring Memories); and the name SEARCH through
                // a hand, "search target opponent's graveyard, HAND, and library …
                // exile them" (Lobotomy, Ancient Vendetta, The Rise of Sozin).
                // Four shapes match the words and are not this tag; they are
                // blanked and the question asked again in Classify.
                (CardEffect.Discard, [
                    Rx(OtherPlayer + @"[\w ,]{0,30}discards?\b"),
                    Rx(@"reveals? their hand[^\n]{0,60}choose [^\n]{0,60}(?:from it|from among)[^\n]{0,40}exile"),
                    // …and the punisher that offers the OPPONENTS the choice outright,
                    // "each opponent MAY sacrifice a nonland permanent or discard a
                    // card", then punishes who did neither (Osseous Sticktwister).
                    // Confirmed 2026-09-29 against "each player may discard", which
                    // is not this: there everybody chooses, you included.
                    Rx(@"unless (?:that player|they|he or she)[^.\n]{0,80}\bdiscards? (?:a|two) cards?"
                        + @"|(?:each|target) opponent may [^.\n]{0,80}\bor discard (?:a|two) cards?"),
                    Rx(@"choose (?:a|an|one|two|up to \w+)[^.\n]{0,60}from it[^\n]{0,60}\b(?:they|the player) discards?\b"
                        + @"|\bthey discard (?:a|two|three|that|their)\b"),
                    Rx(@"(?:target opponent|each opponent|target player)[^.\n]{0,50}, discards? a card"),
                    Rx(@"search (?:target (?:player|opponent)'s|that player's) [\w ,]{0,30}\bhand\b[^.\n]{0,100}\bexile"),
                    // Three hand attacks ruled 2026-09-28, each settling a split in
                    // the hand tags: a card EXILED from their hand, however many and
                    // however chosen (Ruthless Negotiation against Unscrupulous Agent,
                    // same sentence; Crabomination's card at random), but not one
                    // they may still PLAY (Lightstall Inquisitor, Elkin Lair); a card
                    // chosen from their hand and put back ON THEIR LIBRARY (Agonizing
                    // Memories against Painful Memories); and the whole hand looked at
                    // and EXILED (Apple of Eden, "come Wheel of Fortune e sorelle").
                    // "They" once the player is named (Isildur's Fateful Strike,
                    // 2026-10-15).
                    Rx(@"(?:target opponent|each opponent|target player|that player|\bthey) exiles? [^.\n]{0,100}?\bcards?(?: at random)? "
                        + @"from their hand(?![^\n]{0,60}\bmay play)"),
                    Rx(@"look at target (?:player|opponent)'s hand and choose [^.\n]{0,40}\. put (?:them|it|that card) on (?:top|the bottom)"
                        + @"|look at target (?:player|opponent)'s hand and exile"
                        // The same exile as a second sentence, "look at target
                        // opponent's hand. You may exile a nonland card from it"
                        // (Deep-Cavern Bat, tagged by the human on 2026-09-29),
                        // even when its owner may still play it: the card YOU
                        // chose from a hand you looked at, ruled 2026-10-15
                        // (Invasion of Gobakhan, Elite Spellbinder). The card
                        // THEY choose or lose at random and may play stays out
                        // above (Lightstall Inquisitor, Elkin Lair).
                        + @"|look at target (?:player|opponent)'s hand\. you may exile [^.\n]{0,40}\bfrom it")]),

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
                    // The station prefix is part of the anchor: a Spacecraft
                    // prints its abilities behind "3+ | " and "12+ | ", and the
                    // trigger word is no longer first on the line. Uthros
                    // Research Craft draws on every artifact you cast.
                    Rx(@"^(?:\d+\+ \| )?(?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card", RegexOptions.Multiline),
                    // …and two draws of YOURS that a long trigger puts past the
                    // 160: "Otherwise, you draw a card" (Gandalf, Westward
                    // Voyager) and a villainous choice whose first branch is your
                    // draw (Davros), both tagged by the human on 2026-09-29.
                    // Widening the window to 260 instead was measured the same
                    // day: 3 right and 3 wrong (Captain Howler, Charitable Levy,
                    // Finneas), because past 160 the draw is often somebody
                    // else's.
                    Rx(@"^(?:whenever|at the beginning of)[^\n]*(?:\botherwise, you draw a card|villainous choice — you draw a card)",
                        RegexOptions.Multiline),
                    // The same trigger with ONE SENTENCE in front of it. Rowen
                    // says "Reveal the first card you draw each turn. Whenever
                    // you reveal a basic land card this way, draw a card", and
                    // the anchor above wants the trigger word first. Reading the
                    // trigger word ANYWHERE was measured on 2026-09-18 and loses
                    // 11, so this allows exactly one short sentence and no more.
                    Rx(@"^[^\n]{0,70}\. (?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card",
                        RegexOptions.Multiline),
                    // Three more shapes of the same trigger, each tagged by hand
                    // on the one reviewed card that says it (2026-10-14): an
                    // ENTERS trigger joined to a repeating one, "When this
                    // enchantment enters and whenever you cast a spell …, draw a
                    // card" (Up the Beanstalk); and a draw of YOURS past the 160,
                    // said as "and YOU draw a card" (The Toymaker's Trap) or as a
                    // closing sentence of its own, "Draw a card." (Erestor of the
                    // Council). Not the 260 window rejected above. Orcish
                    // Bowmasters' "whenever an opponent DRAWS" is not "draw a
                    // card"; Ivora's Blood reminder is kept out by the Blood
                    // guard, so a comma written in front of "draw" for it was
                    // MEASURED AND DROPPED.
                    Rx(@"^when [^\n,]{0,60} and (?:whenever|at the beginning of)[^\n]{0,160}\bdraw (?:a|one) card"
                        // …and "Then draw a card." closing a long upkeep trigger
                        // (Fisher's Talent, read 2026-10-16).
                        + @"|^(?:whenever|at the beginning of)[^\n]*(?:\band you draw (?:a|one) card|\. (?:then )?draw (?:a|one) card\.$"
                        // …and the same closing draw behind a condition, "If that
                        // creature's power is 4 or greater, draw a card." (Aradesh,
                        // the Founder, read 2026-10-27: 12 of 14 reviewed written
                        // this way are tagged; Finneas, Ace Archer was corrected by
                        // name and Nael, Avizoa Aeronaut stays Filter). Vetoes for "if
                        // you do" (a loot: the parity guard reads it) and "if you
                        // don't" (Shiko and Narset, behind an ability word) were
                        // tried and moved no card.
                        + @"|\. (?:then )?if [^.\n]{0,80}, draw (?:a|one) card\.$)",
                        RegexOptions.Multiline),
                    // A combat trigger of your own whose draw sits past the 160 as a
                    // closing sentence, "Then if you control six or more Humans,
                    // draw a card." (Éowyn, Shieldmaiden), or as the vote's tied
                    // branch (Galadriel, Elven-Queen), both tagged by hand (read
                    // 2026-10-16). Oil-Gorger Troll's draw is an enters trigger.
                    Rx(@"^(?:will of the council — )?at the beginning of combat on your turn,[^\n]*(?:\. then if you control [^,\n]{0,40}, draw (?:a|one) card\.$"
                        + @"|guidance gets more votes or the vote is tied, draw (?:a|one) card\.$)",
                        RegexOptions.Multiline),
                    // Arcane Bombardment copies what it exiled and casts any number
                    // of the copies, every first spell of the turn (read 2026-10-16).
                    Rx(@"then copy each card exiled with this \w+\. you may cast any number of the copies"),
                    // An extra upkeep, draw step and everything else every turn:
                    // Shadow of the Second Sun, tagged by hand (read 2026-10-16).
                    Rx(@"there is an additional beginning phase"),
                    // "An ADDITIONAL card" is a card, ruled 2026-09-21. Howling
                    // Mine and Sylvan Library say it and are tagged; the one
                    // card that says it and is not — Anvil of Bogardan — takes
                    // the card straight back ("then discards a card") and the
                    // parity guard already reads that.
                    //
                    // "Draws UP TO n cards" joins it without the "may": that
                    // word is what separates Diminishing Returns, which is
                    // tagged, from Truce and Temporary Truce, where every player
                    // MAY decline and the two of them are not.
                    Rx(@"draws? (?:an|\w+) additional cards?|(?<!may )draws? up to \w+ cards"),
                    // "Draw a card for each creature you control" is multi-card draw
                    // written the other way round, and the count-first wording was
                    // missed entirely: Balance of Power, Baleful Stare, Become the
                    // Avalanche. Worth 24 recovered for 2 wrongly fired.
                    Rx(@"draws? a card for each|draws? cards equal to"),
                    // Mill a few and put ALL of a kind from among them into your
                    // hand: more than one card kept, a card even once (the
                    // "take more than one" reading). Tazri and Beluna Grandsquall,
                    // both tagged by the human on 2026-09-29, and the only two
                    // cards in the store written this way.
                    Rx(@"\bmills? (?:\w+|x) cards\.? (?:then )?put all [^.\n]{0,100}from among the milled cards into your hand"),
                    // …and the count-first version of the same sentence, where
                    // the "for each" opens it and the draw closes it: Mob
                    // Verdict pays a card per vote you took, Tempt with Bunnies
                    // one per opponent who accepted. Both tagged, and they are
                    // the only two reviewed cards written this way.
                    //
                    // Not "for each opponent who CAN'T, you draw a card", read
                    // 2026-10-16: it follows a discard and pays you only when an
                    // opponent has no card to give, a conditional rider
                    // (Refurbished Familiar, 0 of 1 tagged; hand: Discard alone).
                    // A mana symbol is a word here too, read 2026-10-23: "for each
                    // {U}{U} spent to cast it, draw a card" (Bladecoil Serpent).
                    Rx(@"for each (?![\w '{}]{0,40}\bcan't, )[\w '{}]{0,40}, (?:you )?draws? (?:a|one) card"),
                    // ONE trigger word covering TWO events is two cards, not
                    // one: Cryogen Relic draws when it enters and again when it
                    // leaves, so the artifact replaces itself and pays a card
                    // on the way out. One reviewed card says it, tagged.
                    Rx(@"enters or leaves the battlefield, draws? (?:a|one) card"),
                    // CUMULATIVE UPKEEP paid in CARDS draws one more every turn
                    // it survives, and the draw is written where a cost goes so
                    // no rule was looking there. Psychic Vortex is the only
                    // reviewed card that charges cards this way and it is
                    // tagged (ruled 2026-09-22).
                    Rx(@"cumulative upkeep—draws? "),
                    // BOTH PLAYERS reveal the top of their library and BOTH
                    // take it, every end step. Symmetry is not parity when
                    // everybody gains — this is Howling Mine's reading, which
                    // the human gave by name on 2026-09-22, and the guard that
                    // reads a draw for somebody else has nothing to withdraw
                    // because you are one of the somebodies. Parker Luck is the
                    // one reviewed card written this way and it is tagged.
                    Rx(@"each reveal the top card of their library"
                        + @"[^\n]{0,140}each put the card they revealed into their hand"),
                    // A trigger hiding behind a LABEL. The anchored rule above wants
                    // "whenever" at the start of the line, and modern cards put an
                    // ability word or a Siege bullet in front of it — "Eerie —
                    // Whenever ...", "• Jeskai — Whenever ...". Same bug as the
                    // impulse had, and the same fix; reading the trigger word ANYWHERE
                    // instead was measured and loses 11.
                    Rx(@"^(?:• )?[\w' ]{1,28}— ?(?:whenever|at the beginning of)[^\n]{0,160}draws? (?:a|one) card",
                        RegexOptions.Multiline),
                    // A Saga chapter that names TWO numbers fires twice, so the card
                    // it draws was bought once and drawn again — the same reading
                    // ChapterFiresTwice gives the put-into-hand rule. Found
                    // 2026-09-20 on Jecht, Reluctant Guardian, whose "I, II — Jecht
                    // Beam — Each opponent discards a card and you draw a card" is
                    // hand-tagged CardAdvantage and was being missed.
                    // Widened 2026-09-21 to ANY number of numbers and to the draw
                    // written without "you": "I, II, III — Pain — You draw a card"
                    // (Summon: Anima), "I, II — Scry 2, then draw a card" (The
                    // Legend of Kuruk), "II, III — Until end of turn, whenever a
                    // Kraken ... attacks, draw a card" (Summon: Leviathan).
                    //
                    // A chapter naming ONE number stays out. "A Saga chapter that
                    // draws is CardAdvantage" was measured whole on 2026-09-21 and
                    // REJECTED at 7 tagged out of 13 — Founding of Omashu, Leaves
                    // from the Vine, Summon: G.F. Ifrit, The Clone Saga, The Tale
                    // of Tamiyo and Vault 11 all draw a chapter's worth and none is
                    // tagged. So it is the REPETITION that earns the tag here, the
                    // same as everywhere else, and not the chapter.
                    //
                    // And the draw has to be UNCONDITIONAL, which is why the
                    // window forbids the word "if" rather than allowing any
                    // character. A chapter that draws only when something else
                    // went its way is the conditional rider the tag has no
                    // ruling on yet, and both reviewed cards written that way
                    // are untagged: The Tale of Tamiyo draws when two milled
                    // cards happen to share a type, and Vault 11 draws only
                    // when a vote produced no creature — and pays every player
                    // when it does.
                    Rx(@"^[ivx]+(?:, [ivx]+)+ (?:(?!\bif\b)[^\n]){0,140}\b(?:you )?draws? (?:a|one) card",
                        RegexOptions.Multiline),
                    // The top of your library is a second hand, ruled 2026-09-16:
                    // Fblthp, Glarb and the Traveling Chocobo never run out of cards
                    // to play even though they never draw one.
                    Rx(@"you may (?:play|cast)[^\n]{0,60}top card of your library"
                        + @"|play (?:lands|cards|the top card)[^\n]{0,40}from the top of your library"),
                    // Looking at N and taking MORE THAN ONE is a draw with selection.
                    // Taking exactly one is Filter, which the Filter rules say and
                    // this deliberately does not contradict. Ruled 2026-09-16.
                    // Widened 2026-09-21: the count can be X, the cards can be
                    // named "from among them", and the look can be written the
                    // other way round — Stargaze says "Look at TWICE X cards
                    // FROM THE TOP of your library. Put X cards from among them
                    // into your hand". "Put UP TO two of them" is the same, read
                    // 2026-10-23 (The Mana Rig).
                    Rx(@"look at (?:the top \w+ cards?|twice \w+ cards?) (?:of|from the top of) your library"
                        + @"[^\n]{0,60}put (?:up to )?(?:two|three|four|five|x|\d+) "
                        + @"(?:of them|of those cards|cards? from among them) into your hand"),
                    // …and taking ALL of a kind is more than one too, however many
                    // that turns out to be: Marina Vendrell reveals seven and keeps
                    // every enchantment. Added 2026-09-27, when milling and
                    // revealing to keep one became Filter and this card with it.
                    Rx(@"(?:look at|reveal) the top \w+ cards of your library[^\n]{0,60}"
                        + @"put all [\w ]{1,30}cards from among them into your hand"),
                    // So is "up to two" or "any number of", revealed rather than
                    // looked at (Torsten, Pieces of the Puzzle, Kruphix's Insight):
                    // no reviewed card says it, and without this they would read
                    // as the keep-one shape the same day made Filter.
                    Rx(@"(?:look at|reveal) the top \w+ cards of your library[^\n]{0,80}"
                        + @"put (?:up to (?:two|three|four|five|x|\d+)|any number of) [\w /,'-]{0,60}cards? "
                        + @"from among them into your hand"),
                    // The same thing counted one card at a time. Memories Returning
                    // says "Put one of them into your hand", then "Then you put one
                    // into your hand", then "Put the other into your hand" — three
                    // cards for a spell, which is advantage by the count and not by
                    // repetition. Written narrowly against the second "Then YOU":
                    // every other reviewed card that reaches into its hand twice
                    // does it CONDITIONALLY ("put two of those cards into your hand
                    // INSTEAD if this spell was kicked" — Consult the Star Charts,
                    // Accumulate Wisdom), and those are Filter and stay Filter.
                    Rx(@"into your hand[^\n]{0,120}then you put one into your hand"),
                    // The top of your library kept as a second hand, and several
                    // cards off a top that you may then play. See SecondHandOnTop
                    // and ExileSeveralAndPlayThem for the measurements.
                    SecondHandOnTop]),

                // Filter is selection at NO net gain: look at some and take one, or
                // hand back exactly what you drew. The rummage family — "you may
                // discard a card. If you do, draw a card" — is the plainest shape
                // there is and had never been read: 14 cards fire on the first
                // pattern below and 12 of them were already tagged by hand. The
                // third pattern is the activated version ("Discard a card: Draw a
                // card"); the lookahead is what keeps CYCLING reminder text out,
                // because cycling pays a card to replace itself and attacks nobody.
                // Added 2026-09-19.
                // Scry and surveil ARE Filter, unconditionally, ruled 2026-09-19 —
                // it does not matter what else the card does or how small the
                // number is. Read across every printed form, which "scry \d" was
                // not: the bulk carries "scry X" on 17 cards and "surveil X" on 6,
                // and a handful say "scries"/"surveils" because the subject is a
                // player. The count is required so that "whenever you scry OR
                // surveil" — a trigger that watches one happen — stays out.
                // EMPOWER JACE N makes a Jace planeswalker token whose whole job is
                // "[-1]: Surveil 1" and "[-3]: Draw a card", so the keyword is a
                // Filter by itself. Ruled 2026-09-19. Thirty-five cards print it and
                // 31 carry the reminder text, which the surveil rule below already
                // reads; this is for the four that do not — Sanctum Lurker,
                // Theorist's Sanctum, Jace Reality Sculptor and Fatehold Charm.
                // The token is a PLANESWALKER, so Tokens stays out of it.
                (CardEffect.Filter, [Rx(@"\bempowers? jace\b"),
                    Rx(@"\b(?:scry|scries|surveil|surveils) (?:\d+|x)\b"),
                    Rx(@"look at the top \w+ cards? of your library"),
                    Rx(@"discard[\w ]* then draw"), Rx(@"draws? [\w ]{0,20}cards?[.,] ?(?:then |and )?(?:you may )?discards?"),
                    // The same exchange written discard-first WITH a count:
                    // "Discard a card, then draw two cards" (Romantic Rendezvous,
                    // Summon: Kujata's third chapter), which the two patterns above
                    // both miss — one wants the draw first, the other cannot cross
                    // the comma. The lookahead keeps CYCLING out, which is what an
                    // earlier and looser version of this pattern swallowed: it fired
                    // on 45 cards of which only 4 were tagged.
                    // Not THEIR loot: Memory Worm makes the player it hit discard
                    // and draw (untagged Filter by hand, 2026-10-14). "Target
                    // player" can be you, and Forget is Filter by hand.
                    Rx(@"(?<!\bthat player )discards? (?!this card)(?:a|one|two|three|four|five|x|\d+) cards?,? (?:then |and )(?:you )?draws?"),
                    Rx(@"you may discard (?:a|one|up to \w+|any number of) cards?\.? ?(?:if you do, )?draws?"),
                    Rx(@"discards? (?:a|one|up to \w+|any number of|that many) cards?, then draws? that many"),
                    // The card discarded may be NAMED by a word — "Discard a
                    // Mountain card or a red card: Draw a card" (Chandra's
                    // Regulator, tagged 2026-10-20). One card only: Sphinx of the
                    // Chimes discards two named cards for four. A veto for a LAND
                    // card (Soul of Windgrace, CardAdvantage alone) was tried and
                    // moved no card.
                    Rx(@"^[^\n:]{0,50}discard (?!this card)(?:(?:a|an|one) [\w-]+ card|(?:a|one|\w+) cards?)[^\n:]{0,30}: ?draw",
                        RegexOptions.Multiline),
                    // The two halves change hands without the word "discard": you may
                    // draw and then hand one back (Oblivious Bookworm, Rook Turret),
                    // or pay the card by some other verb — Brainstorm and Dream Cache
                    // put cards back on top, Lat-Nam's Legacy shuffles one in,
                    // Jandor's Ring hands back exactly what it just drew. Each is a
                    // card changing places at no net gain, which is Filter exactly.
                    //
                    // These are spelled out rather than reusing DrawPaidForWithACard
                    // and Loot wholesale, which was tried and measured: Loot's middle
                    // alternative reads CYCLING reminder text as a loot and fires on
                    // 45 cards of which 4 are tagged, costing 42 false positives on
                    // its own. The alternatives kept here fire on 3, 1, 1, 5 and 1.
                    Rx(@"(?:you may )?draws? (?:a|one|\w+) cards?\. if you do, discard"),
                    // The card can be a LAND instead, either way round: Highway
                    // Robbery's "discard a card or sacrifice a land. If you do,
                    // draw two cards" is level with the spell counted, and was
                    // read as a draw two. Named Filter by the human 2026-09-27.
                    Rx(@"(?:sacrifice[\w ]{0,25}or )?discard (?:a|one|\w+) cards?(?: or sacrifice (?:a|an) \w+)?\. if you do, draw"),
                    // N for N: Horrid Shadowspinner draws its power and hands the
                    // same number back, which is a rummage however big.
                    Rx(@"draw cards equal to [^.\n]{1,40}\. if you do, discard that many cards"),
                    Rx(@"put \w+ cards? from your hand[^\n]{0,30}on top"),
                    Rx(@"shuffle (?:a|\w+) cards? from your hand into your library\. if you do, draw"),
                    Rx(@"discard the last card you drew"),
                    Rx(@"then discard \w+ cards? unless"),
                    Rx(@"discards? a card\. then draws? a card"),
                    // A discard charged as an ADDITIONAL COST is still a card
                    // changing places — Grab the Prize pays one to draw two, which
                    // with the spell itself is exactly level, and is functionally
                    // Abandon Attachments with the discard moved into the cost line.
                    // Ruled 2026-09-19.
                    Rx(@"as an additional cost to cast this spell,[^\n.]{0,60}discard"
                        + @"[\s\S]{0,120}draws? (?:a|one|two|three|four|five|x|\d+) cards?"),
                    // SOMEBODY ELSE'S TOP, rearranged or trimmed, ruled 2026-09-27
                    // ("sì, guardare e basta no"), overturning the 2026-09-19
                    // measurement that had it out at 2 of 8. What counts is doing
                    // something to the cards — back in any order, one into their
                    // graveyard, one to the bottom, one exiled: Natural Selection
                    // and Elemental Augury print the same words and were tagged
                    // both ways. Only LOOKING is not this (Orcish Spy, Visions).
                    // A card that then lets you CAST from that pile is Steal (Black
                    // Cat, Laughing Jasper Flint). Not inside brackets: manifest
                    // dread's reminder has the victim look at their own library.
                    Rx(@"(?<!\([^()\n]*)looks? at the top (?:\w+ )?cards? of "
                        + @"(?:target (?:player|opponent)'s|their|that player's|each opponent's) library"
                        + @"(?![^\n]{0,300}\byou may (?:play|cast))[^\n]{0,200}"
                        + @"(?:in any order|into (?:their|that player's) graveyard|on the bottom of (?:their|that player's) library"
                        + @"|exile (?:one|any number) of (?:them|those cards)|puts? one of them back)"),
                    // MILL OR REVEAL N AND KEEP ONE is the look-at-N shape, ruled
                    // 2026-09-27: "sì, ma in caso di terre è ManaFix". Cache Grab,
                    // Malevolent Rumble, Rosheen and Wood Sage were tagged, Eerie
                    // Gravestone was the human's slip. A LAND kept this way is
                    // ManaFixing instead — see LandFromAmongThemToHand. "From among
                    // the MILLED CARDS" is the same words (Seed of Hope, Picklock
                    // Prankster, Wrenn and Realmbreaker's −2, 2026-10-14).
                    // "Put up to one enchantment card MILLED THIS WAY into your
                    // hand" (Terra, Magical Adept, 2026-10-16). "Then RETURN a
                    // creature card milled this way to your hand" (Monster
                    // Manual's Zoological Study, 2026-10-23).
                    Rx(@"(?:\bmill|reveal the top) (?:\w+|x) cards?[^\n]{0,120}?"
                        + @"(?:put [^\n]{0,60}from among (?:them|the cards milled this way|the milled cards) into your hand"
                        + @"|put all of them with that name into your hand"
                        + @"|put (?:up to one|a|an|one) [\w ,'\-]{0,40}cards? milled this way into your hand"
                        + @"|return (?:up to one|a|an|one) [\w ,'\-]{0,40}cards? milled this way to your hand)"),
                    // …and the one kept CAST free or put ONTO THE BATTLEFIELD, read
                    // 2026-10-16, each the one reviewed card that says it: "mill X
                    // cards. You may cast an instant or sorcery spell … from among
                    // them without paying" (Summons of Saruman), "mill seven cards.
                    // Then put a creature, enchantment, or land card from among the
                    // milled cards onto the battlefield" (Bramble Familiar), and
                    // "reveal the top X cards … put any number of Aura cards … from
                    // among them onto the battlefield" (Knickknack Ouphe; the
                    // look-at form of it is 7 of 7 reviewed). Not a LAND alone,
                    // which is Ramp (Shigeki, the 2026-09-20 ruling below).
                    // Without the "may" and with a long cap: Smelting Vat puts
                    // "up to two noncreature artifact cards with total mana value
                    // less than or equal to the sacrificed artifact's mana value"
                    // (tagged 2026-10-22). Not ALL of a kind, which is no choice
                    // (Animist's Awakening's lands, Muxus's Goblins), nor A
                    // RANDOM one (Getaway Barrel).
                    Rx(@"\bmill (?:\w+|x) cards?\. you may cast [^.\n]{0,80}from among them without paying"
                        + @"|\bmill (?:\w+|x) cards?\. then put [^.\n]{0,60}from among the milled cards onto the battlefield"
                        + @"|reveal the top (?:\w+|x) cards? of your library\. (?:you may )?put (?!all\b|a random\b|(?:a|up to one) (?:basic )?land cards?\b)"
                        + @"[^.\n]{0,120}from among them onto the battlefield"),
                    // …and CHOSEN from among them before they go to hand: Green
                    // Sun's Twilight reveals X plus one and keeps a creature
                    // and/or a land (Filter by hand, 2026-10-14).
                    Rx(@"reveal the top (?:\w+|x)(?: plus one)? cards? of your library\. choose [^.\n]{0,60}from among them\. "
                        + @"put (?:it|that card|those cards) into your hand"),
                    // Four more shapes named Filter by the human on 2026-09-27, one
                    // card each in the reviewed set: a WHEEL that gives back what
                    // it took (Winds of Change), a search that EXILES what it finds
                    // to thin the library (Foresight, Mana Severance), and Scroll
                    // Rack trading cards from hand with the top.
                    Rx(@"shuffles? (?:the cards from )?(?:their|your) hand into (?:their|your) library, then draws? that many cards"),
                    // The same wheel through the graveyard: Decaying Time Loop
                    // discards your hand and draws that many, Path of the
                    // Pyromancer that many plus one (both Filter by hand,
                    // 2026-10-14). YOUR hand only — Shocker and Sycorax Commander
                    // make the OPPONENT do it, and are Discard.
                    Rx(@"discard all the cards in your hand[^\n]{0,60}then draw that many"),
                    // …but not cards exiled to be CAST, which is no thinning of
                    // the library (Ugin, Eye of the Storms' −11, 2026-09-29).
                    Rx(@"search your library for [^.\n]{0,40}cards?, exile them, then shuffle"
                        + @"(?!\.[^\n]{0,30}you may (?:cast|play) (?:those cards|them))"),
                    Rx(@"put that many cards from the top of your library into your hand"),
                    // Vault 112: Sadistic Simulation's chapter III exiles that
                    // many off the top and lets you play ONE of them free: a
                    // look and one kept, so Filter, and also Cheat (next to
                    // the Cheat patterns). Ruled 2026-10-16; the one card
                    // written this way, 0 of 1 tagged by rule.
                    Rx(@"exile that many cards from the top\. you may play one of those cards without paying its mana cost")]),

                // The [\w ] runs still cannot cross a FULL STOP, which is what keeps
                // a card that exiles from a graveyard in one sentence and bounces a
                // creature in the next from reading as recursion. But they could not
                // cross a COMMA or a SLASH either, and that is where modern cards
                // put their card-type lists: "one or two target creature AND/OR
                // planeswalker cards", "non-Assassin historic card", "up to one
                // target creature card, up to one target Mount card, …". Widened
                // 2026-09-19 to allow punctuation inside the run but not a stop —
                // worth 4 more Regrowth and 2 more Reanimate, at no cost.
                (CardEffect.Reanimate, ReanimatePatterns),

                // The sibling of Reanimate: same origin, different destination.
                (CardEffect.Regrowth, RegrowthPatterns),

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
                    // The AMOUNT and the FACE, both read as widely as the game
                    // writes them. Every widening here was a card the rule already
                    // agreed with and simply could not read: "half X damage,
                    // rounded down" (Banshee), "each of X targets" (Firestorm),
                    // "any OTHER target" (Screaming Nemesis, Self-Destruct), "each
                    // OTHER opponent" (Parapet Thrasher), "defending player"
                    // (Ghost-Spider), "the player" (Monsoon), "them" (Vexing
                    // Arcanix) and "the controller of" (Suffocation).
                    Rx(@"deals? (?:half )?[\dX]+(?: plus \d+)? damage(?:, rounded (?:down|up),)? to " + BurnTarget + @"\b"),
                    Rx(@"deals? that much damage to " + BurnTarget + @"\b"),
                    Rx(@"deals? [\dX]+ damage to [\w ,]{0,45}each (?:player|opponent)"),
                    // "Each opponent loses 2 life" is a Lava Spike at every face at
                    // once; the game just declined to call it damage. 18 of the 27
                    // reviewed cards that say it are hand-tagged Burn, and every one
                    // of them was being missed. Ruled 2026-09-15.
                    //
                    // ONE point of life is a rider, not a burn. Ruled 2026-09-20
                    // after measuring the family: eleven reviewed cards drain
                    // exactly one and carry no Burn tag (Agate-Blade Assassin,
                    // Al Bhed Salvagers, Ebony Charm, Nafs Asp, Sanguine Syphoner,
                    // Venerated Stormsinger and five more), against two that do —
                    // and one of those two, Susurian Voidborn, is word for word
                    // Al Bhed Salvagers. From TWO the tag stands, and X counts
                    // because X is not one (Northern Air Temple). The subject list
                    // is spelled out so that "you lose 2 life" stays a price.
                    Rx(LosesLifeSubject + @" ?loses? (?:[2-9]|\d\d+|x) life"),
                    // …unless the one point comes AGAIN AND AGAIN. Revised
                    // 2026-09-29 ("ripetuto sì, singolo no"): since 09-23 the
                    // human had tagged twelve of sixteen repeated one-point drains
                    // Burn (Vengeful Bloodwitch, Raven of Fell Omens, Wispdrinker
                    // Vampire, Mirkwood Bats …), against the 09-20 threshold. A
                    // trigger or an activation on the same LINE makes it repeated;
                    // an enters or dies trigger, or a spell, drains once and stays
                    // the rider the threshold refuses.
                    Rx(@"(?:\bwhenever\b|\bat the beginning of\b|^[^\n:""]{1,70}:)[^\n]*?"
                        + LosesLifeSubject + @" ?loses? 1 life", RegexOptions.Multiline),
                    // An enters drain on a permanent that RETURNS ITSELF to hand
                    // is cast again, and so repeats — the dash reading of Riders
                    // of Rohan (2026-10-13), applied to Intimidation Campaign on
                    // 2026-10-16.
                    Rx(@"\benters, [^\n]*?" + LosesLifeSubject + @" ?loses? 1 life[\s\S]*"
                        + @"\breturn this \w+ to its owner's hand"),
                    // The same life loss sized by a COUNT. Ruled 2026-09-20 and it
                    // splits cleanly on WHOSE life it is: four reviewed cards say
                    // "YOU lose life equal to" (Reanimate, Lich, Teval, Darkstar
                    // Augur) and none is tagged, because that is the price the card
                    // charges; seven say somebody else does and six are tagged.
                    Rx(LosesLifeSubject + @" ?loses? life equal to"),
                    // …and sized by the life just gained: "whenever you gain life,
                    // target opponent loses THAT MUCH life" (Enduring Tenacity;
                    // the gain-life family is 2 of 2 reviewed, 2026-10-16). Only
                    // after a life GAIN: "that much" sized by anything else was
                    // measured the same day at 1 tagged of 3 — Barbed Servitor's
                    // damage dealt to it, Caustic Bronco's revealed card.
                    Rx(@"whenever you gain life\b[^\n]*?" + LosesLifeSubject + @" ?loses? that much life"),
                    // HALF their life, aimed at an opponent, is always Burn —
                    // ruled 2026-10-16 ("sempre Burn") on Aphelia, Rush of Dread,
                    // Unstoppable Slasher and Grievous Wound against Alpharael and
                    // Betor, which were handed back. The subjects are only the ones
                    // that point at somebody else: "its OWNER" is Personal
                    // Incarnation charging the player who owns it, and "EACH
                    // player" is Shahrazad, which halves you as well.
                    Rx(@"(?:each opponent|target opponent|target player|that player|\bthey|defending player)"
                        + @" loses? half their life"),
                    // Damage DIVIDED among "targets" — bare, so a face can be one of
                    // them. Ruled 2026-09-20: five of the six reviewed cards written
                    // that way are tagged (Fireball, Rolling Thunder, Pyrotechnics,
                    // Meteor Shower, Mogg Mob), while all seven that say "among
                    // target CREATURES" are not, because those can never reach a
                    // player. The lookahead is Fiery Justice, the sixth, which the
                    // human ruled an exception ON PURPOSE: it hands the opponent
                    // back exactly the five life it dealt, so the count is zero.
                    // The amount may be a count: Orca, Siege Demon's "damage equal
                    // to its power divided … among any number of targets"
                    // (2026-10-23, tagged by hand).
                    Rx(@"damage (?:equal to [\w' ,+/]{0,70}? )?divided (?:evenly,? )?(?:rounded down,? )?(?:as you choose )?"
                        // "among UP TO EIGHT targets" (Blaster Hulk, 2026-10-16).
                        + @"among (?:any number of|one, two, or three|up to \w+|\w+) targets\b"
                        + @"(?![^\n]{0,40}gains? [\dX]+ life)"),
                    // The same damage with the word order the other way round —
                    // "deals damage TO that player EQUAL TO the number of artifacts
                    // they control" — which the rule below could not read at all.
                    // Twelve of the eighteen reviewed cards written this way were
                    // already tagged and the human ruled the other six in on
                    // 2026-09-20, the upkeep punishers among them.
                    // A "who" clause may run longer: Spiteful Repossession's "to
                    // each opponent WHO CONTROLS MORE LANDS THAN YOU equal to the
                    // difference" (2026-10-23).
                    Rx(@"deals? damage to [\w ,'-]{0,40}" + BurnTarget + @"(?: who\b[\w ,'-]{0,50}|[\w ,'-]{0,20}) equal to"),
                    // Damage sized by a COUNT rather than a digit. The rule for this
                    // already existed and fed Removal alone, so Cat-Gator's "damage
                    // equal to the number of Swamps to any target" was read as a kill
                    // and not as a burn. 11 of 12 such cards are tagged. The window
                    // was 45 characters and Summon: Bahamut puts 52 between the two
                    // halves ("the total mana value of other permanents you
                    // control"), and Cyclone names the creatures before the players.
                    // The count may be of +1/+1 COUNTERS, whose signs and slash the
                    // window did not admit, and the creatures may be qualified before
                    // the players — "to each creature WITHOUT FLYING and each player"
                    // runs to 33 characters (Magmasaur, ruled Wipe and Burn with the
                    // rest of the symmetric sweepers on 2026-09-27).
                    // A player named only as the CONTROLLER of the creature hit
                    // is no face (Lothlórien Blade's "to target creature
                    // defending player controls", untagged by the human on
                    // 2026-09-29), hence the lookahead.
                    Rx(@"deals damage equal to [\w' ,+/]{0,70}to [\w ,'-]{0,40}" + BurnTarget + @"\b(?! controls?\b)"),
                    // Life paid to keep something from happening is life lost.
                    // Ruled 2026-09-20 with the rest of Burn: Breathstealer's Crypt,
                    // Sirocco and Cleansing all charge a player life to stop the
                    // card, and all three are tagged. Restricted to SOMEBODY ELSE
                    // paying, so an additional cost you pay stays a price.
                    // The threshold applies here too, with one escape: a single
                    // point charged FOR EACH of something is not one point at all
                    // (Cleansing charges one per land destroyed).
                    // A WARD paid in life is the reminder "counter it unless that
                    // player pays 2 life", and protects the creature rather than
                    // burning anybody: 0 of the 6 reviewed ward-life cards are
                    // tagged for it (Zul Ashur, Sire of Seven Deaths, Nine-Fingers
                    // Keene …), Gev and Invasion of Karsus for other lines. 2026-10-16.
                    // The same shield written out for the player and every permanent
                    // ("counter THAT SPELL OR ABILITY unless that player pays 4
                    // life") is Parnesse, the Subtle Brush, tagged Protection only
                    // (2026-10-23).
                    Rx(@"(?<!counter (?:it|that spell(?: or ability)?) )unless (?:that player|they|any player|its controller) pays? (?:[2-9]|\d\d+|x) life"
                        + @"|for each[^\n.]{0,70}unless (?:that player|they|any player|its controller) pays? [\dX]+ life"),
                    // …and the same charge handed in quotes to the creature an
                    // Aura is on, whose controller is the "you" who pays every
                    // upkeep (Morgul-Knife Wound, 2026-10-14). The plain upkeep
                    // drain handed over the same way is the repeated point of
                    // 2026-09-29: Clawing Torment's enchanted PERMANENT has "At the
                    // beginning of your upkeep, you lose 1 life" (tagged 2026-10-23;
                    // Relic Bane gives it to an artifact).
                    Rx(@"enchanted (?:creature|permanent|artifact|land)\b[^""\n]{0,40}\bhas ""[^""\n]*"
                        + @"\b(?:unless you pay (?:[2-9]|\d\d+|x) life|you lose [\dX]+ life)"),
                    // An EMBLEM handed to an opponent burns them on every upkeep
                    // (Chandra, Awakened Inferno, tagged 2026-10-20). One that YOU
                    // get is your own (Ajani Steadfast's prevention).
                    Rx(@"(?:each|target) (?:opponent|player) gets an emblem with ""[^""]*"
                        + @"\b(?:deals? [\dX]+ damage to you|you lose [\dX]+ life)"),
                    // Life or a sacrifice, whoever is asked: the punisher above with
                    // the source's controller named as the victim (Archfiend of
                    // Spite, "loses that much life unless they sacrifice that many
                    // permanents", 2026-10-20). Seven reviewed punishers written
                    // this way, all seven tagged.
                    Rx(@"\bloses? (?:that much|[\dX]+) life unless (?:they|that player) sacrifices?\b"),
                    // The life loss at the END of a list of things the opponent
                    // does: "sacrifices a creature of their choice, discards a
                    // card, and loses 4 life" (Dusk Mangler). Ruled 2026-10-23
                    // ("Sì, Burn"), Scarring Memories corrected with it. The
                    // threshold holds: Mind Drain's one point is not here.
                    Rx(@"\b(?:each|target|an) (?:opponent|player)s? (?:sacrifices?|discards?|mills?)\b[^.\n]{0,90}, and loses? (?:[2-9]|\d\d+|x) life"),
                    // "Any number of target creatures, planeswalkers, AND/OR
                    // PLAYERS", then the damage (Soulfire Eruption, 2026-10-20).
                    Rx(@"\btarget [\w ,]{0,40}\band/or players\b[^\n]{0,200}\bdeals? damage\b"),
                    // A hasty TOKEN gone at the next end step is the Viashino's
                    // burn made by another card, ruled 2026-10-14 for the whole
                    // family ("tutta la famiglia Burn"): Urabrask's Forge, and
                    // Balduvian Dead and Daring Piracy handed back with it.
                    // A token COPY given haste is the same since 2026-10-23
                    // ("tutto allineato" with Ball Lightning, encore and unearth):
                    // Kiki-Jiki, Hate Mirage, Mimic Vat, encore handed out by
                    // Graywater's Fixer, and the token populate makes (Determined
                    // Iteration). It has to be MADE: a creature the card only
                    // TAKES — from a graveyard, a hand, a library or an opponent
                    // (Corpse Dance, Through the Breach, Slave of Bolas) — keeps
                    // its Reanimate, Cheat or Steal and is not burn ("solo ciò
                    // che la carta crea o è", same day).
                    Rx(@"\b(?:create\b[^\n]{0,200}?\btokens?|populate)\b[^\n]{0,200}?\bhaste\b[^\n]{0,160}?"
                        + @"\b(?:(?:sacrifice|exile) (?:it|them|that token|those tokens|the token|the tokens)"
                        + @"|return (?:it|them|that token|those tokens) to (?:its|their) owner'?s hand)"
                        + @" at the beginning of the next end step"),
                    // …and the keyword that does it, handed to OTHER cards: unearth
                    // in the graveyard (Dregscape Sliver, Sedris, Ghost Ark), blitz
                    // on the creature spells you cast (Henzie). The cards that have
                    // it themselves are asked by keyword (HastyAndGoneKeywords).
                    // "If it has unearth" (Meticulous Excavation) gives nothing.
                    Rx(@"\b(?:cards?|spells?)\b[^.\n]{0,60}?\b(?:has|have|gains?) (?:unearth|blitz|encore|dash)\b"),
                    // "Loses 1 life FOR EACH card type" is not the one-point rider
                    // the threshold above refuses — the count is what it is
                    // multiplied by (Polluted Cistern, Fandaniel). The subject is
                    // asked for the same reason as everywhere else in this tag:
                    // Reign of Terror charges YOU two life for each creature it
                    // killed, and that is the price of a Wrath.
                    Rx(LosesLifeSubject + @" ?loses? [\dX]+ life for each")]),

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
                //
                // Only a REPEATABLE outlet counts, ruled 2026-09-25 (see
                // IsSacrificeOutlet for the shapes). The candidates below read every
                // way a card lets you feed your own creatures and artifacts ("any
                // number of" needs nothing: the unbounded "a" already takes the "a"
                // of "any" — measured; "one or more" and "X" do, or Radiant Lotus's
                // "Sacrifice one or more artifacts:" is never asked about), exploit
                // handed to others (Colonel Autumn), a
                // card cast again by sacrificing (Wickerfolk Indomitable), an equip
                // cost (Dissection Tools), and destroying or exiling your OWN
                // permanent at will, which the human ruled an outlet by another name
                // (Despotic Scepter, Rats of Rath, City of Shadows).
                (CardEffect.Sacrifice, [
                    // An Aura "ATTACHED TO this creature" is not a creature
                    // sacrificed (Faunsbane Troll, 2026-10-15).
                    Rx(@"sacrifices? (?:a|an|another|two|three|\d+|x\b|one or more\b)(?:(?!\battached to\b)[\w ])*(?:creature|artifact|permanent)"),
                    Rx(SacrificeKeywordGranted + @"|\bsacrificing (?:a|an|another)[\w ]*(?:creature|artifact|permanent)"),
                    Rx(SacrificesYourOwnByAnotherVerb, RegexOptions.Multiline)]),

                // Three patterns of "gain control" saw one third of this tag. The rest
                // is in StealPatterns above: an EXCHANGE is a theft you paid for
                // (Juxtapose, Legerdemain, Political Trickery, Trade the Helm,
                // Gauntlets of Chaos), a card taken out of somebody else's LIBRARY,
                // HAND or GRAVEYARD and played is a theft of a card rather than of a
                // permanent (15 of the 28 misses, from Outrageous Robbery to Gonti),
                // and "onto the battlefield UNDER YOUR CONTROL" is how Bone Dancer,
                // Helm of Obedience and Desertion say it.
                (CardEffect.Steal, StealPatterns),

                // Widened 2026-09-19. The old pair read "search your library for a
                // card" and a list of five card TYPES, and the game names what it
                // fetches in every other way there is: a SUBTYPE (Equipment, Demon,
                // Vehicle, Dragon), a COLOUR ("a black card, a green card, and a blue
                // card"), a COUNT ("three cards", "five cards"), a NEGATION ("a
                // nonland permanent card"), a second ZONE ("your library and/or
                // graveyard"), and every one of those behind an optional "up to two".
                // Twenty-one of the 48 hand-tagged cards went untouched.
                //
                // Still deliberately NOT a land search, which is Ramp or ManaFixing —
                // that reading costs Elemental Teachings, whose four land cards end up
                // split between a graveyard and the battlefield by an opponent, and it
                // is one card against the family.
                (CardEffect.Tutor, TutorPatterns),

                (CardEffect.ManaFixing, [Rx(@"add one mana of any color"), Rx(@"mana of any (one )?color"),
                    // Read 2026-10-13: Great Hall of the Citadel and Wizard's
                    // Rockets, both ManaFixing by hand; a free source that says
                    // it is withdrawn by EveryFixerIsASource like any other.
                    Rx(@"mana in any combination of colou?rs"),
                    Rx(@"add \{[wubrg]\} or \{[wubrg]\}"), Rx(@"add \{[wubrg]\}, \{[wubrg]\}"),
                    // Typecycling fetches the colour you are short of, which is the
                    // whole job. Added 2026-09-15; worth 11 on its own.
                    TypeCycling,
                    // A land fetched to HAND fixes the colour you are short of and
                    // ramps nothing — the land still has to be played, off your one
                    // land drop. The same search that puts it onto the BATTLEFIELD is
                    // Ramp instead, and deliberately stays out: measured 2026-09-18
                    // over the 5,035 reviewed cards, to-hand was tagged 28 of 30
                    // (93.3%) and to-battlefield 8 of 51 (15.7%). Worth 17 on its own.
                    LandSearchToHand, LandSearchToTop,
                    // …and dug up to hand, "reveal cards from the top of your
                    // library until you reveal a land card. Put that card into
                    // your hand" (House Cartographer, 2026-10-16).
                    Rx(@"until you reveal a land card\. put that card into your hand"),
                    // A land made the BASIC TYPE you choose is the colour you are
                    // short of, ruled 2026-10-16 ("MF a entrambe") on Mystic
                    // Compass, which repeats it, and Jinx, which does it once.
                    Rx(@"\btarget land becomes the basic land type of your choice"),
                    // A colour filter: you feed it mana and it hands back a colour
                    // you did not have. Ruled 2026-09-18, the mirror of the Ramp
                    // ruling — the same ability that adds no mana converts colour,
                    // and that IS the job. Two shapes, and the difference matters:
                    //
                    //   GENERIC in, colour out  — Farrelite Priest "{1}: Add {W}",
                    //   Sea Scryer, Coal Golem. Always a filter.
                    //
                    //   COLOUR in, a DIFFERENT colour out — Fire Sprites "{G}, {T}:
                    //   Add {R}", Agent of Stromgald. The backreference is the whole
                    //   point: Evendo's "{G}, {T}: Add {G} for each creature you
                    //   control" pays green for more green, which is Ramp and not a
                    //   filter, and it was the only thing the first pattern got
                    //   wrong.
                    //
                    // {C} is deliberately not an output colour — colourless fixes
                    // nothing.
                    Rx(@"^[^\n:]*\{\d+\}[^\n:]*: add [\w ]{0,20}\{[wubrg]\}", RegexOptions.Multiline),
                    Rx(@"^[^\n:]*\{([wubrg])\}[^\n:]*: add [\w ]{0,20}\{(?!\1)[wubrg]\}", RegexOptions.Multiline),
                    // A Treasure is one mana of whatever colour you were short of.
                    // It fixes however many you get; whether it also RAMPS is the
                    // separate question TreasureAlsoRamps asks. Ruled 2026-09-18.
                    MakesATreasure,
                    // A land with two mana abilities in different colours fixes even
                    // though no single line says "or" — Bleachbone Verge, and the
                    // whole modern "{T}: Add {B}. / {T}: Add {W}." cycle.
                    Rx(@"^\{t\}: add \{([wubrg])\}\.?$[\s\S]{0,120}?^\{t\}: add \{(?!\1)([wubrg])\}", RegexOptions.Multiline)]),

                // Mana ability (dork/rock) or a land-fetch to the battlefield. Lands
                // are stripped below — a land tapping for its own mana is not "ramp".
                // The land-search rule that used to sit here moved into the guarded
                // branch in Classify, where LandOntoTheBattlefield says the same
                // thing more widely AND asks the two questions this one could not:
                // whose land is it, and did you pay a land for it.
                (CardEffect.Ramp, [TapAddsMana]),

                (CardEffect.Pacify, PacifyPatterns),

                // Cheat: the permanent arrives without being cast. Ruled 2026-09-18;
                // see CardEffect.Cheat for the three edges and the numbers.
                (CardEffect.Cheat, [
                    // Out of hand. The lookahead keeps LANDS out — "put a basic
                    // Forest card from your hand onto the battlefield" is Gaea's
                    // Touch and is Ramp — and "their hand" is in because Show and
                    // Tell is the card the whole family is named after.
                    // The window is 60 and not 40 because Show and Tell lists four
                    // card types before it gets to the noun. The land lookahead is
                    // narrower on purpose: it uses [\w ], which stops at the first
                    // comma, so a LIST that happens to mention lands (Show and Tell
                    // again) still counts while "put a basic Forest card" does not.
                    // Widened 2026-10-13 to "any number of" and to a card named
                    // with a cap after the noun (Ghalta, Last March of the Ents,
                    // Jhoira, Paladin Elizabeth Taggerdy, Vault 101's "hand or
                    // graveyard", Yuna's Decision) — but not a LESSER cap, ruled
                    // the same day (Shadowfax, untagged: "lesser = niente"), and
                    // not an OPPONENT putting from their own hand (Wumpus
                    // Aberration). A noun LIST that ends in a land is no land
                    // card either, read 2026-10-25 as Bramble Familiar's out of
                    // the library: Court of Bounty's "a creature or land card",
                    // the one reviewed card it moves.
                    // A cap the BOARD sets is the X cap, ruled 2026-10-25 on Covert
                    // Technician ("Cheat", against its untagged review, the same
                    // words as Broodcaller Scourge's): the damage dealt, the lands
                    // you control (Anticausal Vestige, handed back), the counters
                    // (Aether Vial) or a creature's power (Minn), which the window
                    // of 50 and no apostrophe had cut off. A strict "less than" is
                    // the lesser cap (Arni Metalbrow, untagged by hand).
                    Rx(@"(?<!opponent may )put (?:an?|any number of|up to \w+|one or more) (?![^.\n]{0,80}\b(?:lesser\b|less than (?!or equal)))"
                        + @"(?![\w ]{0,25}(?<!(?:creature|artifact|enchantment|planeswalker|permanent),? or )(?:land|forest|island|swamp|mountain|plains) cards? from)"
                        + @"[\w ,/-]{0,60}cards?[\w ,/'-]{0,80} from (?:your|their) hand(?: or graveyard)? onto the battlefield"),
                    // Out of the library. Natural Order, Eldritch Evolution's cousins,
                    // and since 2026-10-13 "any number of" (Bilbo). Not a copy of a
                    // creature already on the battlefield, ruled 2026-10-25 on March
                    // of Burgeoning Life ("stesso nome"; Tutor alone by hand).
                    Rx(@"search your library for (?:an?|any number of|up to \w+) [\w ]{0,30}creature cards?(?! with the same name)"
                        + @"[\w ,']{0,40}put (?:it|that card|them|those cards) onto the battlefield"),
                    // Out of the library from among the cards LOOKED AT, REVEALED
                    // or MILLED, ruled 2026-10-13: a nonland permanent with NO
                    // mana-value cap is Cheat (Arthur, Ojer Kaslem, Majestic
                    // Genesis, Bramble Familiar and Vorinclex out of a mill —
                    // not Reanimate), and one under a cap is selection, Filter
                    // alone ("qualunque tetto = Filter": Emergent Woodwurm,
                    // Kayla's Reconstruction, Wilfred Mott, Knickknack Ouphe).
                    // The noun may be a LIST that ends in a land, read
                    // 2026-10-16: Bramble Familiar's "a creature, enchantment,
                    // or land card" is the ruled card.
                    Rx(@"put (?:a|an|up to \w+|any number of|all|one|two) (?:(?!mana value|lesser|power)[^.\n]){0,100}?"
                        + @"\b(?:creature|permanent|artifact|enchantment|nonland|equipment|aura|planeswalker)"
                        + @"(?:,? (?:or )?(?:creature|artifact|enchantment|land|planeswalker|battle))* cards? "
                        + @"(?:(?!mana value|lesser|power)[^.\n]){0,60}?from among (?:them|those cards|the (?:milled|revealed|exiled) cards)"
                        + @"[^.\n]{0,20} onto the battlefield"),
                    // …and the same out of a reveal or an exile named "this way"
                    // (Doors of Durin, Eladamri, Anzrag's Rampage, Hew the Entwood,
                    // Thunderous Debut bargained, The Creation of Avacyn), and
                    // since 2026-10-25 every player's permanents revealed this way
                    // (Over the Top, the one card in the store that says it).
                    Rx(@"library[^\n]{0,80}?if (?:it's a|you reveal a|a) creature card(?: is revealed)?(?: this way)?, put it onto the battlefield"
                        + @"|put (?:a|all) (?:creature|nonland)[^.\n]{0,30}cards? (?:exiled|chosen) this way onto the battlefield"
                        + @"|put the revealed cards onto the battlefield"
                        + @"|put the exiled card onto the battlefield if it's a creature card"
                        + @"|puts? all permanent cards (?:they |you )?revealed this way onto the battlefield"),
                    // …and a reveal UNTIL the first nonland permanent of a kind,
                    // put onto the battlefield, read 2026-10-25 by the same
                    // ruling (no mana-value cap): Aspiring Champion and Atla
                    // Palani by hand, 2 of 2 reviewed, and Polymorph's family
                    // proposed (11 unreviewed). A land is Ramp (Clifftop
                    // Lookout, The Regalia), a lesser cap nothing (Kethek), and a
                    // type before the noun is the tribe the tribal guard reads
                    // (Time Lord Regeneration, Tom Bombadil's Saga, Hei Bai's
                    // Shrine): none of them starts with the words below. A guard
                    // against a cap in the noun was tried and matched no card.
                    Rx(@"until you reveal an? (?:creature|permanent|nonland|artifact|planeswalker)[^.\n]{0,40}card\. "
                        + @"put (?:that card|it) onto the battlefield"),
                    // A STANDING permission to cast free, and the discriminator is
                    // GRAMMATICAL NUMBER. A standing permission covers a class of
                    // spells, so its wording is plural — "Dragon spells … without
                    // paying THEIR MANA COSTS" (Dracogenesis), "creature spells with
                    // mana value 3 or less" (Aluren), "spells from your hand"
                    // (Omniscience). The impulse rider covers the one card this card
                    // just exiled and is always singular: "you may cast a spell from
                    // among them without paying ITS MANA COST". Reading the object
                    // phrase instead was tried first and let ten riders through,
                    // because they name the card ten different ways.
                    // The plural alone still let the MULTI-card rider through — Ugin
                    // and Arcane Bombardment exile several and say "cast them
                    // without paying their mana costs" — so the object phrase is
                    // tempered as well: anything pointing back at cards this card
                    // exiled is the rider, not a permission.
                    Rx(@"(?:you|any player|each player|players) may cast "
                        + @"(?!this card\b|this spell\b)"
                        + @"(?:(?!among them|those cards|exiled|from exile|\bthem\b|copies)[\w ,'-]){0,60}"
                        + @"without paying their mana costs"),
                    // UNCAPPED free casts are Cheat, ruled 2026-10-13 ("carta
                    // grossa"): spells from among a pile of exiled cards, mine or
                    // theirs (Etali, Dance with Calamity, Kylox, Wand of Wonder,
                    // Plargg and Nassari, Ral's −8), not a pile this card filled
                    // itself ("exiled with this", Forger's Foundry) and not copies
                    // (Arcane Bombardment); a spell out of your HAND (Great
                    // Intelligence's Plan, Marvo, One with the Multiverse); a
                    // permission for the next spell (Eye of Ojer Taq); and another
                    // card's SPELL COST paid with something else (Dream Halls,
                    // Cramped Vents). A LESSER-mana-value cap is cascade and stays
                    // CardAdvantage (Jodah, Breaching Dragonstorm) — neither shape
                    // below says it, so a "lesser" guard written for them was
                    // measured, matched no card, and was dropped.
                    // A FIXED cap is no "carta grossa" either, ruled 2026-10-16 on
                    // Perception Bobblehead ("è 3 o meno il costo che ti regala"):
                    // Collected Conjuring's 3 and Hazoret's Undying Fury's 5. An X
                    // cap stays, as big as the damage or the mana paid (Kotis,
                    // Villainous Wealth, Epic Experiment; Kotis Cheat by hand).
                    Rx(@"you may cast (?:(?:any number of|up to \w+) )?(?:[\w/-]+ ){0,5}?spells "
                        + @"(?:with mana value (?!\d)[^.\n]{0,20} )?from among (?![^.\n]{0,60}exiled with this)[^.\n]{0,80}"
                        + @"without paying their mana costs"),
                    // …and the same pile PLAYED free, read 2026-10-25: Ignite the
                    // Future's three exiled cards once it is flashed back.
                    Rx(@"you may play cards this way without paying their mana costs"),
                    Rx(@"cast (?:a|an|up to \w+|any number of) [^.\n]{0,40}spells? from your hand"
                        + @"[^.\n]{0,40}without paying (?:its|their) mana costs?"),
                    Rx(@"the next spell you cast [^.\n]{0,40}can be cast without paying its mana cost"),
                    // The hand's spell may be named by a type and come out of the
                    // graveyard too, read 2026-10-25: Anrakyr the Traveller's
                    // "an artifact spell from your hand or graveyard", Cheat and
                    // Reanimate by hand.
                    Rx(@"rather than pay the mana cost for a spell"
                        + @"|cast (?:a|an) (?:[\w-]+ )?spell from your hand(?: or graveyard)? by paying [^.\n]{0,40} rather than paying its mana cost"),
                    // …and ANY other card's cost paid with something else, wherever
                    // it is cast from, ruled 2026-10-25 on Xander's Pact ("Cheat a
                    // tutti"): life (Bismuth Mindrender, Nashi and Valgavoth handed
                    // back; Gwenom, Bolas's Citadel, Inside Information, and Eye of
                    // Duskmantle out of the graveyard), a discard (The Infamous
                    // Cruelclaw), energy (Amped Raptor) or {1} (Jadzi). Not Hama's
                    // waterbending {X}, which pays the whole mana value ("Hama"
                    // left out of the answer) and is cast "during your turn by",
                    // so the shape never reaches it; not the card casting ITSELF
                    // (Squee, Worldheart Phoenix), nor a copy (Blue Mage's Cane),
                    // nor Primal Prayers' fixed cap of 3, all three untouched.
                    // "The exiled card by" was tried as a third noun and matched
                    // no card.
                    Rx(@"(?:if you cast a spell this way, (?:you )?pay|you may cast (?:that card|it) by)"
                        + @"[^.\n]{0,60}rather than pay(?:ing)? its mana cost"),
                    // GRANTING SUSPEND to a card out of your library or hand is a
                    // free cast on a timer, ruled 2026-10-13 (Sibylline Soothsayer,
                    // The Eleventh Doctor, The Parting of the Ways; The Tenth
                    // Doctor realigned). Not the card suspending ITSELF (Rory
                    // Williams), nor the spell you just cast and paid for (Taigam).
                    Rx(@"(?:top of your library|from your hand|of your library)[^\n]{0,250}gains suspend"),
                    // …and the same suspend spelled out without the keyword, read
                    // 2026-10-25: Alaundo the Seer's time counters on a card from
                    // the hand, cast free when the last one is removed.
                    Rx(@"from your hand and put [^.\n]{0,60}time counters? on it[^\n]{0,200}"
                        + @"when the last time counter is removed[^\n]{0,80}you may cast it without paying its mana cost"),
                    // LOOKING at the top few and casting one of them free puts a
                    // card into play at no cost, named Cheat by the human on
                    // 2026-09-27 (Perception Bobblehead) — Aetherworks Marvel is
                    // the same card. Asked of a LOOK or a REVEAL only: the EXILE
                    // version is the impulse rider above, which is a card. Under
                    // a FIXED cap it is Filter alone, ruled 2026-10-16 on the same
                    // Bobblehead ("è 3 o meno il costo che ti regala"), as the
                    // 10-13 "qualunque tetto = Filter" ruled a permanent put from
                    // among them; the one reviewed card it reaches.
                    Rx(@"(?:look at|reveal) the top [\w ]{1,20} of your library[^\n]{0,200}"
                        + @"cast (?![^\n]{0,80}?mana value \d+ or less)[^\n]{0,80}from among them without paying"),
                    // …and a look that EXILES the one it keeps and casts it free
                    // later, with no mana-value cap, ruled 2026-10-16 ("Hideaway
                    // no, altri sì"): Djeru and Hazoret's legendary creature, The
                    // Dragon-Kami Reborn's hatched creature. Not HIDEAWAY, whose
                    // play waits on a condition and stays Filter alone, 4 of 5 by
                    // hand (Evercoat Ursine handed back) — its reminder joins the
                    // look with a comma, not a full stop. Not under a cap either:
                    // Discover the Impossible's "if it's an instant spell".
                    // Keldon Flamesage's "mana value X or less" never gets here,
                    // its look running on into "where X is"; a guard against a
                    // cap in the exiled noun was tried and matched no card.
                    Rx(@"look at the top \w+ cards of your library\. (?:you may )?exile (?:a|an|one) [^.\n]{0,120}\."
                        + @"[\s\S]{0,300}?cast (?:the exiled card|a [\w ]{0,20}spell from among cards [^.\n]{0,20}in exile with [\w-]+ counters on them)"
                        + @" without paying its mana cost(?! if)"),
                    // Exiling until a NONLAND card and casting it free with no cap
                    // is cascade without its ceiling, Cheat by the 2026-10-13
                    // ruling ("carta grossa": Sarevok's Tome); with a cap after it
                    // stays CardAdvantage (Breaching Dragonstorm, Solstice
                    // Revelations, Ryan Sinclair).
                    Rx(@"until you (?:exile|reveal) a nonland card\. you may cast (?:that card|it|the exiled card) without paying its mana cost(?! if)"),
                    // A card out of your HAND cast free, as Great Intelligence's
                    // Plan, when it is chosen first (The Seventh Doctor), and the
                    // card a loot discarded (Malcolm, named 2026-10-13). One card
                    // each; the impulse rider ("exile the top card … cast it") and
                    // Steal's (Hidetsugu and Kairi) say neither.
                    Rx(@"choose a card in your hand\.[^\n]{0,200}you may cast it without paying its mana cost"),
                    Rx(@"you may cast the discarded card without paying its mana cost"),
                    // Vault 112: Sadistic Simulation plays one of the cards it
                    // exiled for free: Filter and Cheat, not CardAdvantage.
                    // Ruled 2026-10-16.
                    Rx(@"exile that many cards from the top\. you may play one of those cards without paying its mana cost")]),
            ];

            FilterPatterns = Rules.First(rule => rule.Effect == CardEffect.Filter).Patterns;
            BurnPatterns = Rules.First(rule => rule.Effect == CardEffect.Burn).Patterns;
            WipePatterns = Rules.First(rule => rule.Effect == CardEffect.Wipe).Patterns;
            DiscardPatterns = Rules.First(rule => rule.Effect == CardEffect.Discard).Patterns;
            BouncePatterns = Rules.First(rule => rule.Effect == CardEffect.Bounce).Patterns;
            ManaFixingPatterns = Rules.First(rule => rule.Effect == CardEffect.ManaFixing).Patterns;
            CheatPatterns = Rules.First(rule => rule.Effect == CardEffect.Cheat).Patterns;
        }

        // Damage aimed at THAT PERMANENT'S CONTROLLER, which is how the old
        // punishers name a face: Psychic Venom, Ankh of Mishra, Dingus Egg and
        // Staff, Haunting Wind, Seizures, Orcish Mine, Artifact Possession,
        // Stinging Licid. Ten of the seventeen reviewed cards written this way
        // are tagged.
        // "IS DEALT TO that spell's controller" is the passive voice of the
        // same thing: Reverberation turns a sorcery round on the player who
        // cast it, which is a Lava Spike they wrote themselves.
        private static readonly Regex DamageToTheirController = Rx(
            @"(?:deals?|is dealt) (?:[\dX]+|that much|half [\dX]+)? ?damage[\w ,'-]{0,40}to (?:that|the) "
            + @"(?:creature|land|artifact|permanent|spell)'s controller"
            + @"|damage[\w ,'-]{0,80}is dealt to that (?:creature|land|artifact|permanent|spell)'s controller");

        // …and the other seven, which are the same words hanging off an ANSWER.
        // Ruled 2026-09-20: a card that kills the permanent and then charges its
        // controller for it is a removal spell with a bonus, not a burn spell —
        // Detonate, Icequake, Cinder Cloud, Stench of Evil all destroy first,
        // and Misthios's Fury and Wisecrack shoot the creature first. Read
        // across sentences, because the kill and the charge are rarely in the
        // same one.
        // The verb is often ELIDED on the second half — Judgment Bolt says
        // "deals 5 damage to target creature AND X damage to that creature's
        // controller" with no second "deals" — so it is optional here. The
        // opening alternative has already established that a kill came first.
        //
        // A LAND is not in the list, ruled 2026-09-20: on the land-destroyers
        // the human reads the damage as a second effect worth counting, which
        // also settles the one split this family had — Orcish Mine charges the
        // controller two for the land it just destroyed and is tagged, Icequake
        // and Stench of Evil do the same and were not.
        // EXCESS damage handed on to the controller is not a charge for the
        // kill but the burn that goes past it (Gandalf's Sanction, 2026-10-14).
        private static readonly Regex ChargesForAKillItJustMade = Rx(
            @"(?:destroy|exile|deals? [\dX]+ damage to target creature"
            + @"|damage equal to [\w' ]{0,25}to itself)"
            + @"[\s\S]{0,160}(?:deals? )?(?:[\dX]+|that much|half [\dX]+)? ?(?<!\bexcess )damage"
            + @"[\w ,'-]{0,40}to (?:that|the) (?:creature|artifact|permanent|spell)'s controller");

        // Damage a TOKEN does, printed inside the quotes the token is created
        // with. Ruled 2026-09-20 with the opposite answer to the one Tokens
        // gives: the card makes a body, and what the body then does is the
        // body's. Eight reviewed cards create the 0/1 Wizard that pings on every
        // noncreature spell and none of the eight carries Burn. Keyed on "THIS
        // TOKEN" (and the emblem, which is the same thing a planeswalker makes),
        // so an ability granted to a creature you already control keeps the tag:
        // Black Mage's Rod says "this creature" and Fire Whip burns on a line of
        // its own.
        // The LIFE LOSS a created token's quoted ability does is the token's too,
        // read once the repeated one-point drain became Burn (2026-09-29): Keimi,
        // the Frog token Tatsunari makes, drains on every enchantment spell, and
        // that is the Frog's, the same evening the human ruled Smaug's Treasures
        // Smaug's and not There and Back Again's. A keyword may come before the
        // quotes (Kaito, Dancing Shadow's Drone "with deathtouch and", 2026-10-14).
        // An emblem handed to an OPPONENT is not something the card made for
        // itself: it is the burn (Chandra, Awakened Inferno, 2026-10-23).
        private static readonly Regex DamageFromSomethingItMade = Rx(
            @"(?<!\b(?:opponents?|players?|they) gets? an emblem with )""[^""]{0,160}\bthis (?:token|emblem)[^""]{0,60}deals? [\dX]+ damage"
            + @"|\btokens? with (?:[\w ,]{0,40} and )?""[^""]{0,200}\bloses? [\dX]+ life[^""]*""");

        // Removal's side of the same question, narrower: the ONE point a token
        // pings for is the token's (the Devil that dies — Ob Nixilis, the
        // Adversary, Pugnacious Pugilist, Spiked Corridor, 3 of 3 by hand).
        // Two or three points from a token that enters or leaves still kill
        // something and stay (Roxanne's Meteorite, Dragonbroods' Relic,
        // Weapons Manufacturing). Read 2026-10-14; the whole quote is taken,
        // so the card is asked again without it.
        private static readonly Regex TokenPing = Rx(
            @"""[^""]{0,160}\bthis token[^""]{0,60}deals? 1 damage[^""]*""");

        // A SPELL that only sets up damage on the NEXT spells you cast removes
        // nothing by itself, ruled 2026-10-13 on Season of the Bold ("di suo
        // non genera una removal, deve aggiungere un altro effetto"). A
        // PERMANENT with the same trigger is Removal (Flaming Tyrannosaurus,
        // Surtr, Screamer-Killer, Defiler of Instinct: 4 of 4).
        private static readonly Regex SpellDamageOnYourNextCasts = Rx(
            @"[^.\n]*whenever you cast [^,\n]{0,40}spell[^.\n]{0,60}deals? (?:\d+|x) damage[^.\n]*\.?");

        // A drain that fires when ANOTHER MEMBER OF A TRIBE arrives waits on
        // cards the card is not, ruled 2026-10-13 on Obyra, Dreaming Duelist
        // ("Whenever another Faerie you control enters, each opponent loses 1
        // life", untagged): "dipendono da condizioni esterne". The type is
        // the capitalised word, so "another creature you control" — any body
        // at all — is not here (Thunder of Unity keeps Burn). A card that MAKES
        // that tribe itself enables its own drain and keeps it, the mirror of
        // the ruling (Aatchik's Insects, Boggart Mischief's Goblins), and so
        // does one that IS of the tribe and says "a", not "another" — it
        // fires on itself (Leeching Sliver, Sanctum Seeker).
        private static readonly Regex DrainOnATribe = Rx(
            @"^whenever (?<another>another|a|an|one or more(?: other)?) (?!creatures?\b|nontoken\b|tokens?\b)(?<tribe>(?-i:[A-Z])[a-z]+?)s? "
            + @"(?:creatures? )?you control (?:enters?|attacks?|dies|die)[^\n]*\bloses? [\dX]+ life[^\n]*",
            RegexOptions.Multiline);

        /// <summary>"Target player draws three cards and loses 3 life" is never
        /// Burn, ruled 2026-10-16 ("mai Burn"): the life is what the cards cost,
        /// whoever is pointed at. Monumental Corruption was changed by name;
        /// Insatiable Avarice, Unscrupulous Contractor and Vault Plunderer were
        /// already untagged. Blanked and asked again, so Ob Nixilis, the
        /// Adversary keeps Burn through its +1.</summary>
        private static readonly Regex DrawsForTheLife = Rx(
            @"draws? (?:a|an|one|two|three|four|five|six|seven|x|\d+) cards? and loses? [\dX]+ life");

        /// <summary>A punisher on an opponent's OWN sacrifice — "whenever an
        /// opponent sacrifices an artifact, this creature deals 2 damage to
        /// them" (Vengeful Tracker, untagged) — waits on a choice the opponent
        /// makes; 0 of the 3 reviewed cards with the trigger carry Burn
        /// (2026-10-16). The whole line is blanked.</summary>
        private static readonly Regex PunishesTheirSacrifice = Rx(
            @"^[^\n]*\bwhenever an opponent sacrifices\b[^\n]*", RegexOptions.Multiline);

        /// <summary>True when the card has a drain on another member of a
        /// tribe it neither makes nor is. See DrainOnATribe.</summary>
        private static bool DrainsOnATribeItDoesNotMake(Card card, string text) =>
            DrainOnATribe.Matches(text).Any(m =>
            {
                string tribe = Regex.Escape(m.Groups["tribe"].Value);
                bool makesIt = Rx(@"\bcreate[^.\n]*\b" + tribe + @"s?\b[^.\n]*\btokens?\b").IsMatch(text);
                bool isIt = !m.Groups["another"].Value.Contains("other", StringComparison.OrdinalIgnoreCase)
                    && Rx(@"\b" + tribe + @"\b").IsMatch(card.TypeLine ?? string.Empty);
                return !makesIt && !isIt;
            });

        // A creature with HASTE that is gone at the end of the turn it arrived
        // is a burn spell with legs. Ruled 2026-09-20: it attacks once and then
        // it is not there any more, so what it really did was put its power on
        // a face — Ball Lightning, Spark Elemental, Blistering Firecat,
        // Groundbreaker, Lightning Skelemental. The human extended the ruling
        // the same day to the ones that BOUNCE rather than die, which is the
        // same turn and the same attack: Viashino Sandstalker, Archwing Dragon,
        // Glitterfang and the rest of the Viashinos.
        //
        // NB the reviewed set can barely test this — Ball Lightning is tagged,
        // Viashino Sandstalker was not and is realigned by the ruling, and the
        // other thirteen cards written this way have not been reviewed yet. It
        // is recorded here as a ruling applied forward rather than a rule
        // measured against ground truth, which is unusual for this file.
        //
        // Asked of the card with QUOTED text blanked out, because Strago and
        // Relm, Skirk Alarmist and Apprentice Necromancer all hand the drawback
        // to somebody else's creature.
        // "YOUR end step" too, found 2026-09-29 on Fleeting Effigy, which the
        // human had tagged Bounce and ruled "come il viashino".
        private static readonly Regex GoneAtEndOfTheTurn = Rx(
            @"at the beginning of (?:the (?:next )?|your )end step, (?:sacrifice this creature"
            + @"|return this creature to (?:its|their) owner'?s hand)");

        private static readonly Regex Hasty = Rx(@"\bhaste\b");

        // The same creature by keyword, ruled 2026-10-23 ("Sì, sempre Burn" for
        // encore, then "tutto allineato" for the rest): each of these gives the
        // card haste and takes it away at the next end step — dead, exiled or
        // back in hand. Asked by KEYWORD because Phyrexian Dragon Engine,
        // Ziatora's Envoy and Broodmate Tyrant print theirs without the
        // reminder. Creatures only: Mask of the Jadecrafter's unearth returns
        // an artifact nobody attacks with.
        private static readonly HashSet<string> HastyAndGoneKeywords =
            new(["Unearth", "Encore", "Blitz", "Dash"], StringComparer.OrdinalIgnoreCase);

        // Three shapes that look like selection and are not, all ruled
        // 2026-09-20 off the nine cards where the classifier said Filter and the
        // hand did not.
        //
        //   CLOAK AND MANIFEST DREAD turn what you picked FACE DOWN into a 2/2.
        //   Nothing is selected as a card — what the ability gave you is a body,
        //   which is why Curator Beastie and Hide in Plain Sight are Tokens
        //   alone. Named by the keyword rather than by "onto the battlefield",
        //   because a card put onto the battlefield AS ITSELF is still a choice
        //   made: Aang, Gilgamesh, Jet, United Battlefront and Web of Life and
        //   Destiny all do that and all five are tagged Filter.
        //   Both this veto and the land one refuse to fire when the SAME LINE
        //   also puts a card into your hand, because then the card really did
        //   select: Planar Genesis looks at four, takes a land if there is one
        //   and a CARD if there is not, and it is tagged Ramp and Filter both.
        private static readonly Regex LooksAtTopAndMakesBodies = Rx(
            @"look at the top [\w ,']{0,40} of your library(?![^\n]{0,200}into your hand)[^\n]{0,80}"
            + @"(?:\bcloaks?\b|onto the battlefield face down)"
            + @"|manifest dread\.? \(look at the top");

        //   A LAND out of the top few is Ramp, ruled 2026-09-20 on Famished
        //   Worldsire and Ignis Scientia — the same rail that makes a land
        //   search Ramp or ManaFixing rather than Tutor. What you looked at
        //   bought mana, not a choice.
        private static readonly Regex LooksAtTopForALand = Rx(
            @"look at the top [\w ,']{0,40} of your library(?![^\n]{0,200}into your hand)[^\n]{0,80}"
            + @"put (?:a|any number of|up to \w+|\w+) (?:basic |snow )*land cards? from among them");

        //   A LAND KEPT IN HAND out of the few milled, revealed or looked at is
        //   ManaFixing, ruled 2026-09-27 — "in caso di terre è ManaFix". Ainok
        //   Wayfarer, Ostrich-Horse and Town Greeter mill a few and keep a land,
        //   the land fetched to hand the ManaFixing ruling already names.
        private static readonly Regex LandFromAmongThemToHand = Rx(
            @"(?:\bmill|reveal the top|look at the top) (?:\w+|x) cards?[^\n]{0,120}?"
            + @"(?:put|reveal) (?:a|up to \w+|any number of) (?:basic )?land cards? from among (?:them|the cards milled this way|the milled cards)"
            + @"(?: and put (?:it|them))? into your hand");

        //   A card looked at and CAST FOR FREE was vetoed here from 2026-09-27,
        //   on Perception Bobblehead (Ramp and Cheat by hand). Withdrawn
        //   2026-10-16: the 10-13 ruling on Kiora, Sovereign of the Deep made
        //   the same words Cheat AND Filter ("Kiora Cheat+Filter"; the tribal
        //   ruling later took only the Cheat), and Bobblehead was handed back.
        //   The exile-and-cast form stays CardAdvantage (Djeru and Hazoret,
        //   Keldon Flamesage).

        //   A connive on the enters trigger that also sends a SPELL back is the
        //   rider Refute's loot is (2026-09-27): Obscura Interceptor is Counter
        //   alone by hand (2026-10-16). The whole line goes, reminder and all.
        private static readonly Regex ConniveBesideACounter = Rx(
            @"^[^\n]*\bconnives\b[^\n]*\breturn up to one target spell\b[^\n]*", RegexOptions.Multiline);

        // A SORCERER ROLE's scry is the Role's, not the card's, ruled
        // 2026-10-13: Unassuming Sage, Spellbook Vendor and Living Lectern
        // untagged (overturning the 09-29 reading that a scry handed to
        // something that repeats it keeps the tag, which named Unassuming Sage).
        private static readonly Regex SorcererRoleScry = Rx(
            @"sorcerer role token[^\n]*?""whenever this creature attacks, scry 1\.""");

        // An OPPONENT's loot, read 2026-10-24: Balor makes "target opponent
        // draw three cards, then discard three cards at random" (untagged by
        // hand, 2026-10-23) — Memory Worm's reading of 2026-10-14 with the
        // victim named. "Target PLAYER" can still be you (Forget).
        private static readonly Regex TheirLoot = Rx(
            @"\b(?:target|each) opponent draws [^.\n]{0,20}cards?, then discards [^.\n]*");

        // A scry only a TRIBE's spell pays out, the tribal ruling of 2026-10-16:
        // Lapis Orb of Dragonkind's "when you spend this mana to cast a Dragon
        // creature spell, scry 2" (untagged by hand, 2026-10-22). Case-sensitive
        // on the type, as TribalCard is.
        private static readonly Regex ScryForATribe = new(
            @"\b[Ww]hen you spend this mana to cast an? " + ACreatureType
            + @" (?:creature )?spell, (?:scry|surveil) \d+\.?(?: ?\([^()]*\))?",
            RegexOptions.CultureInvariant);

        // Every shape above blanked, for the tag to be asked again.
        private static string WithoutFalseSelection(string text) =>
            ScryForATribe.Replace(TheirLoot.Replace(
            SorcererRoleScry.Replace(ConniveBesideACounter.Replace(LandFromAmongThemToHand.Replace(TheirDiscardYourDraw.Replace(
                LooksAtTopForALand.Replace(LooksAtTopAndMakesBodies.Replace(text, " "), " "), " "), " "), " "), " "), " "), " ");

        //   AND A LOOT THAT IS NOT YOURS. "Each opponent discards a card AND YOU
        //   DRAW a card" is two different players doing two different things,
        //   not one player trading. Jecht, Reluctant Guardian is Discard and
        //   CardAdvantage and nothing else. The carve-out is deliberate and
        //   matches the one CardAdvantage already makes: "TARGET player" and
        //   "EACH player" stay in, because you can point Forget at yourself and
        //   because Flux loots everybody including you — both were put to the
        //   human on 2026-09-20 and both keep the tag.
        private static readonly Regex TheirDiscardYourDraw = Rx(
            @"(?:each opponent|target opponent|an opponent|each other player|each player other than you)"
            + @"[\w ,'-]{0,30}discards?[\w ,'-]{0,40}(?:and|then) you draw");
    }
}
