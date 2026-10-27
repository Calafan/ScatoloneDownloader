using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// Patterns for the tags that answer or make a PERMANENT: Disenchant, Tutor,
    /// LandDestruction, Steal, Mill, Tokens and Sacrifice. Part of
    /// <see cref="EffectClassifier"/>; the pipeline that uses them is in
    /// EffectClassifier.cs and the rule table in EffectClassifier.Rules.cs.
    /// </summary>
    internal static partial class EffectClassifier
    {
        /// <summary>The noun Disenchant answers, with the traps built in: the
        /// filler in front of it is short enough that it cannot reach across a
        /// sentence, "non"/"non-" may not be crossed to get to it, an artifact
        /// CREATURE is a creature rather than an artifact, and an Aura ATTACHED TO
        /// a named thing is about that thing rather than about the Aura. See the
        /// rule table and <see cref="AnswersAnArtifactOrEnchantment"/> for the
        /// cards that taught each clause.</summary>
        private const string DisenchantNoun =
            @"[\w '\-]{0,14}(?<!non)(?<!non-)(?:artifact|enchantment|aura)s?\b(?! creature)(?! attached to)";

        // Hoisted for the same reason as MillPatterns: the guard below re-runs
        // them one line at a time, so a card with two modes is judged mode by
        // mode rather than on the words its whole text happens to contain.
        private static readonly Regex[] DisenchantPatterns =
        [
            // The noun may come second in a type list, "destroy target creature,
            // enchantment, or planeswalker" (Get Lost), and the count may say
            // "up to one OTHER target" (Vault 13), both read 2026-10-15.
            // "Destroy ANY NUMBER OF target artifacts and/or enchantments"
            // (Rampaging Yao Guai, 2026-10-16).
            Rx(@"(?:destroy|exile) (?:up to \w+ (?:other )?|another |any number of |x |two |three |four |\d+ )?target (?:[\w '\-]{1,14}, )?" + DisenchantNoun),
            Rx(@"(?:destroy|exile) (?:all|each) " + DisenchantNoun),
            // An edict aimed at an artifact answers one all the same, and unlike
            // the creature edict it cannot be blanked by a token: Pick Your Poison
            // and Gaius van Baelsar both read "each opponent sacrifices an
            // artifact of their choice" and the human tags both here.
            // A TRIGGER on the opponent's own sacrifice is no edict: "whenever an
            // opponent sacrifices an artifact, this creature deals 2 damage to
            // them" (Vengeful Tracker, untagged; 2026-10-16).
            Rx(OtherPlayer + @"(?<!\bwhenever [\w ]{1,20}) sacrifices?[\w ]{0,20}" + DisenchantNoun),
            // …and so is the one EXILED by its controller, "target opponent
            // exiles an enchantment they control" (Debt to the Kami, Early
            // Winter), read 2026-10-15.
            Rx(OtherPlayer + @" exiles? an? " + DisenchantNoun),
            // Ultimate Magic: Meteor names no target at all — "for each opponent,
            // choose an artifact or land that player controls. Destroy the chosen
            // permanents." The same clause is LandDestruction's last miss.
            Rx(@"choose an? [\w ,'\-]{0,30}\bartifacts?\b[^\n]{0,40}destroy the chosen"),
            // …and the TYPE chosen rather than the permanent: "Choose artifact or
            // enchantment. Destroy all permanents of the chosen type" (Season of
            // Gathering, 2026-10-16).
            Rx(@"choose (?:artifact|enchantment)[\w ,]{0,25}\. (?:destroy|exile) all permanents of the chosen type"),
            // A tuck answers one too: "the owner of target artifact or
            // enchantment an opponent controls SHUFFLES IT INTO their library"
            // (Cathartic Parting, 2026-10-16).
            Rx(@"target " + DisenchantNoun + @"[^.\n]{0,40}shuffles? it into"),
        ];

        /// <summary>A CARD in a graveyard is graveyard hate, not an answer to a
        /// permanent: "exile target artifact card from a graveyard" (Grave
        /// Robbers), "exile target enchantment, instant, or sorcery card … from
        /// an opponent's graveyard" (Saruman of Many Colors). 0 of 31 reviewed
        /// cards that say it are tagged for it (2026-10-16). Blanked on the line
        /// before the patterns are asked.</summary>
        private static readonly Regex TargetCardInAGraveyard = Rx(
            @"target [^.\n]{0,60}?\bcards?\b[^.\n]{0,60}?from (?:a|an opponent's|your|that player's|their|target player's) graveyard");

        /// <summary>A sweeper that happens to name artifacts is Wipe: Jokulhaups,
        /// Nevinyrral's Disk, Ultima and Death Begets Life all take the creatures
        /// in the same breath and the human tags none of them Disenchant, while
        /// every mass Disenchant that IS tagged — Shatterstorm, Tranquility,
        /// Serenity, Seeds of Innocence — leaves creatures alone.</summary>
        private static readonly Regex SweepsCreaturesToo = Rx(
            @"(?:destroy|exile) (?:all|each)[\w ,'\-]{0,40}creatures?\b");

        /// <summary>Exile that hands the card straight back is a blink, not an
        /// answer: Hide on the Ceiling and Explosive Getaway both "return … to the
        /// battlefield … at the beginning of the next end step".
        /// <para>
        /// "Exile … UNTIL THIS LEAVES THE BATTLEFIELD" deliberately does NOT
        /// appear here, although it was tried. The O-ring family splits on what it
        /// names, not on the exile coming back: Mystical Tether, Detention Chariot
        /// and Trapped in the Screen all say "exile target ARTIFACT or creature"
        /// and the human tags all three, while Web Up, Seam Rip, White Auracite
        /// and Perilous Snare say "target nonland permanent" and are tagged
        /// RemovePermanent instead — and the noun above already refuses to reach a
        /// generic permanent. Vetoing the wording cost those three and bought only
        /// Earth Kingdom Jailer, which names an artifact and reads like a slip.
        /// </para></summary>
        private static readonly Regex ExileThatComesBack = Rx(
            @"return (?:it|them|the exiled cards?|that card)[\w ,'\-]{0,40}to the battlefield");

        /// <summary>Destroying your own is never an answer: Rats of Rath reads
        /// "destroy target artifact, creature, or land you control".</summary>
        private static readonly Regex DisenchantsYourOwn = Rx(
            @"(?:destroy|exile) (?:up to \w+ |another |x |\d+ )?target[\w ,'\-]{0,40}you control");

        private const string BasicLandType = @"\b(?:plains|islands?|swamps?|mountains?|forests?)\b";

        // Hoisted for the same reason as MillPatterns: the guard below re-runs
        // them one line at a time, so a card that fetches a land on one mode and a
        // creature on another is judged mode by mode.
        private static readonly Regex[] TutorPatterns =
        [
            // The zones may be LISTED with commas — "search your library,
            // graveyard, and/or outside the game for" (Invasion of Arcavios,
            // tagged by the human on 2026-09-29) — hence the comma and the 40.
            Rx(@"search (?:your|their) library[\w ,/]{0,40}for (?:up to \w+ |two |three |four |five |\d+ )?"
                + @"[\w ,'\-/]{0,50}cards?\b"),
            // Demonic Consultation never searches: it names a card and then digs
            // until the NAME turns up. The name is the whole point — "reveal cards
            // until you reveal a LAND card" (The Regalia, House Cartographer) or
            // "a creature card" (Yuna's Whistle) or "a white card" (Sacred Guide)
            // hands you whichever one happened to be nearest the top, and the
            // human tags none of those five.
            Rx(@"reveal cards? from the top of your library until you reveal[^\n]{0,30}chosen name"),
            // Ring of Ma'ruf fetches from the sideboard, which is a library the
            // rules text has no other word for.
            Rx(@"card you own from outside the game"),
        ];

        /// <summary>A land search is Ramp or ManaFixing — the ruling this tag was
        /// written around, and the only reason the wide rule above is safe. The
        /// word boundary is what keeps "a NONLAND permanent card" out, and
        /// <see cref="SearchesForSomethingElseToo"/> is what keeps Starfield
        /// Shepherd's "a basic Plains card OR a creature card" in.</summary>
        private static readonly Regex SearchesOnlyForALand = Rx(
            @"library[^.\n]{0,40}for [^.\n]{0,25}(?:basic |snow |nonbasic )*(?:\blands?\b|" + BasicLandType + ")");

        // "A creature OR LAND card" too: Analyze the Pollen searches for a basic
        // land, and "instead" for either once evidence is collected (2026-10-16).
        private static readonly Regex SearchesForSomethingElseToo = Rx(
            @"\b(?:creature|artifact|enchantment|instant|sorcery|planeswalker|equipment|battle"
            + @"|legendary|nonland permanent)(?: or land)? cards?\b");

        // "Search your library for a card NAMED Llanowar Sentinel" was vetoed for
        // half a day, on the evidence that five reviewed cards said it and none
        // was tagged. Ruled the other way on 2026-09-19: naming the card you want
        // is the purest form of this tag, whether it finds another copy of itself
        // (Llanowar Sentinel, Magitek Infantry, Tempest Hawk), a combo piece
        // (Kyscu Drake, Urborg Panther) or one of two payoffs (Dragonstorm
        // Forecaster, which was tagged and which the veto was costing). The five
        // were slips, and the veto is gone.

        // Hoisted for the same reason as MillPatterns: the guard below re-runs
        // them one line at a time, so a modal card is judged mode by mode.
        private static readonly Regex[] LandDestructionPatterns =
        [
            // Not "EXILE THIS CARD from your hand: target land gains …", a cost
            // paid with the card itself (Spara's Adjudicators, 2026-10-16).
            Rx(@"(?:destroy|exile)(?! this card)[^\n]{0,80}target [\w ,'\-]{0,30}\blands?\b"),
            // A NONBASIC land closes a longer list, "exile target creature,
            // Vehicle, or nonbasic land" (Ray of Ruin, 2026-10-15). Widening the
            // run above to 40 for every land was measured the same day and
            // reaches "the number of lands you control" (Lasyd Prowler) and
            // "other than basic land cards" (Haunting Echoes).
            Rx(@"(?:destroy|exile)[^\n]{0,80}target [\w ,'\-]{0,40}\bnonbasic lands?\b"),
            // Not "all other permanents EXCEPT FOR LANDS" (Elspeth Tirel) nor
            // "all Swamps YOU CONTROL" (Return of the Nightstalkers), 2026-10-16.
            // "All lands you control" was guarded the same way and moved no card
            // (Desolation Angel destroys them all when kicked), so it is not.
            Rx(@"(?:destroy|exile) (?:all|each)(?:(?!except\b)[\w ,'\-]){0,30}\blands?\b"),
            Rx(@"(?:destroy|exile) (?:up to \w+ |x |two |three |four |\d+ )?(?:all|each|target) "
                + @"[\w ,'\-]{0,20}" + BasicLandType + @"(?! you control)"),
            // Every sweep that takes the opponents' lands is this tag, whether or
            // not it says "land", ruled 2026-10-16 ("sì, sempre") on Balance
            // against Planetary Annihilation and Apocalypse against Jokulhaups:
            // all the PERMANENTS taken (Apocalypse, Upheaval, Worldfire, Urza's
            // Sylex), and a pile kept that may hold lands or IS lands, the rest
            // sacrificed (Balance, Cataclysm, Global Ruin, Balancing Act). Not
            // what a NAME, a COLOUR, a mana value or a chosen type picks (Wirefly
            // Hive, Wash Out, Celestial Kirin, Season of Gathering), and not a
            // pile of NONLAND permanents (Cataclysmic Gearhulk).
            Rx(@"(?:destroy|exile|return) all (?:other )?permanents\b"
                + @"(?! (?:you control|you own|of |with |named |attached |that ))"
                + @"(?![^.\n]{0,30}except for [^.\n]{0,40}\blands)"),
            Rx(@"\bchooses? (?![^.\n]*\bnonland\b)[^.\n]{0,100}?\b(?:lands?|permanents?)\b[^.\n]{0,120}?"
                + @"\bsacrifices? (?:the rest|all other permanents)"),
            // Lands handed back to their owners are as gone as destroyed ones:
            // Sunder, and Omen of Fire's Islands.
            Rx(@"return (?:all|each) (?:[\w-]+ )?(?:lands|" + BasicLandType + @")(?! you control)"
                + @"[^.\n]{0,30}to (?:their owners'|its owner's) hands?"),
            // "FOR EACH LAND, destroy that land unless any player pays 1 life"
            // (Cleansing) names its victim with a pronoun. Anchored on the "each
            // land" in front of it, because Erosion says "destroy that land" of
            // the single land it enchants and the human does not tag it.
            Rx(@"for each land[\w ,'\-]{0,20}destroy that land"),
            // A land edict, with the clause that picks the victim in between:
            // "each player WHO TAPPED A LAND FOR MANA THIS TURN sacrifices a land
            // of their choice" (Desolation). Nature's Wrath names a basic type
            // where Desolation says "land".
            // The victim may close a TYPE LIST, "sacrifices all artifacts,
            // enchantments, and nonbasic lands" (Wave of Vitriol), but not as the
            // last of a CHOICE, "an artifact, creature, or land of their choice"
            // (Braids, Cabal Minion), which never has to be a land. And it may
            // follow a creature edict, "…, then sacrifices a land of their
            // choice" (Din of the Fireherd, Smallpox), 2026-10-16.
            Rx(@"(?:target (?:player|opponent)|each player|that player|each opponent)"
                + @"[\w ,'\-]{0,60}sacrifices? (?:[\w-]+,? ){0,5}(?<!\bor )(?:lands?\b|" + BasicLandType + @")"
                + @"|, then sacrifices (?:[\w-]+ ){0,3}(?:lands?\b|" + BasicLandType + @")"),
            // Fallow Earth answers a land without destroying it — but the
            // destination has to be a library or a graveyard, because Tato Farmer
            // puts one ONTO THE BATTLEFIELD with almost the same words.
            Rx(@"put target land[\w ,'\-]{0,40}(?:on top of|into)[\w ,'\-]{0,25}(?:library|graveyard)"),
            // Ultimate Magic: Meteor names no target at all — "for each opponent,
            // choose an artifact or land that player controls. Destroy the chosen
            // permanents." The same clause is the last Disenchant miss.
            Rx(@"choose an? [\w ,'\-]{0,30}\blands?\b[^\n]{0,40}destroy the chosen"),
            // A land named by its CONTROLLER and no target: "for each player,
            // destroy up to one nonbasic land that player controls" (Krenko's
            // Buzzcrusher, 2026-10-16).
            Rx(@"(?:destroy|exile) (?:up to )?(?:a|an|one|two|three) (?:nonbasic |basic )?lands? "
                + @"(?:that player|target player|target opponent|an opponent|each opponent) controls"),
        ];

        /// <summary>A land card in a GRAVEYARD is fuel, not a target: Steward of
        /// the Harvest exiles three of them out of yours.</summary>
        private static readonly Regex LandAlreadyInAGraveyard = Rx(
            @"land cards? (?:from|in)[\w ,'\-]{0,25}graveyard");

        /// <summary>"Destroy target Aura attached to a land" PROTECTS the land —
        /// Savaen Elves and Pyramids, the same pair that fooled Disenchant.</summary>
        private static readonly Regex AuraOnALand = Rx(@"auras? attached to");

        /// <summary>Rats of Rath destroys "target artifact, creature, or land YOU
        /// CONTROL", which denies nobody anything.</summary>
        private static readonly Regex DestroysYourOwnLand = Rx(
            @"(?:destroy|exile)[^\n]{0,60}target[\w ,'\-]{0,40}you control");

        // A rebalance that hands basics back was excused here until 2026-10-16
        // (Natural Balance, on a line that destroys nothing). Overturned with the
        // sweeps: every sweep that takes the opponents' lands is this tag, and a
        // basic offered back is consolation (Wave of Vitriol, From the Ashes).

        // Hoisted for the same reason as MillPatterns: the donation guard below
        // strips the clause that gives a permanent AWAY and then re-runs these,
        // so a card that steals and donates in the same text keeps the tag.
        private static readonly Regex[] StealPatterns =
        [
            // …but not of a SPELL, which is Redirect's (Invert Polarity).
            Rx(@"gains? control of(?! (?:target|that) spell)"),
            Rx(@"you control (enchanted|target)"),
            Rx(@"untap target creature[\w ]*gain control"),
            // An exchange is a theft you paid for. Five cards, and the only way
            // any of them was ever going to be found.
            Rx(@"exchange control of"),
            // Word of Command takes the player rather than the permanent.
            Rx(@"you control that player"),
            // And Mister Negative and Psychic Transfer take the life total.
            // Ruled 2026-09-19.
            Rx(@"exchange life totals"),
            // Desertion takes the spell it just countered, which is the only place
            // "under your control" appears without a zone to read it against.
            Rx(@"counter target spell[\s\S]{0,180}onto the battlefield (?:tapped )?under your control"),
            // Enchantment Alteration moves somebody else's Aura onto a permanent
            // of your choosing — theft of the Aura's job if not of its control.
            // Ruled 2026-09-19. Moving an Aura "you control" is your own, though:
            // Codsworth re-equips, and was tagged Ramp alone.
            Rx(@"attach (?:target|enchanted) aura(?![\w ]{0,40} you control to )"),
            // A permanent exiled and then CAST by you is taken twice over:
            // "Exile target nonland permanent. You may cast that card for as
            // long as it remains exiled" (Abstruse Appropriation, 2026-10-16).
            // A permanent CARD is your own graveyard's (Savvy Trader).
            Rx(@"exile target (?:nonland )?permanent\b(?! cards?\b)[^.\n]{0,30}\. you may (?:cast|play) that card\b"),
        ];

        /// <summary>Reanimating out of an OPPONENT'S graveyard is both Reanimate
        /// and Steal — ruled 2026-09-19, after the tags split on cards that share
        /// a sentence: Bone Dancer and Helm of Obedience were tagged Steal against
        /// Ashen Powder and Chorale of the Void, which were not.
        /// <para>
        /// It is read as two halves for the same reason the stolen-card family is.
        /// "Under your control" alone is how ordinary reanimation is worded —
        /// Reanimate, Hymn of Rebirth and Coffin Queen all say "from A graveyard"
        /// and belong to nobody in particular — so the theft only exists once the
        /// card names whose graveyard it is.
        /// </para></summary>
        // "tapped" may sit INSIDE the phrase: an Oracle update between 22 and 24
        // Sep 2026 rewrote Geth, Lord of the Vault from "onto the battlefield
        // under your control tapped" to "onto the battlefield tapped under your
        // control", and the card lost Steal with no change to this file.
        private static readonly Regex OntoYourSideOfTheBoard = Rx(
            @"onto the battlefield (?:tapped )?under your control");

        /// <summary>Somebody else's zone, NAMED — "target opponent's library",
        /// "defending player's graveyard". Kept apart from the pronoun form below
        /// because it is the only one "THEY may cast" can be read against: in
        /// Transforming Flourish the player who exiles from "THEIR library" and
        /// the one who then casts are the same person, so no theft happens, while
        /// Gonti exiles from "that OPPONENT'S library" and hands the card to the
        /// creature's controller.</summary>
        private static readonly Regex SomebodyElsesNamedZone = Rx(
            @"(?:target opponent|an opponent|each opponent|that opponent|target player|that player"
            + @"|defending player|another player|each player|opponent'?s|player'?s)"
            + @"[\w ,'\-]{0,60}(?:library|hand|graveyard)"
            + @"|(?:library|hand|graveyard) of (?:target |an |each |that )?(?:opponent|player)");

        /// <summary>A mill IS the zone, and the only way Locke could ever be found:
        /// "each player mills a card … you may cast a spell from among those cards"
        /// never names a library at all. It only pairs with YOU playing the card,
        /// though — "each player mills two cards. Then you put a creature card from
        /// a graveyard onto the battlefield" is Path of the Schemer reanimating
        /// from any graveyard, which no victim was named for.</summary>
        private static readonly Regex MillsSomebody = Rx(
            @"(?:each|target|an|another|that) (?:player|opponent)[\w ,'\-]{0,20}mills?\b");

        /// <summary>Taking a CARD rather than a permanent — the other half of this
        /// tag, and 15 of the 28 cards it used to miss. It reads as two halves
        /// because either half alone is something else entirely: the zone alone is
        /// Mill, and "you may play" alone is your own impulse draw.</summary>
        // The filler stops at "your": Virtue of Courage, Fireglass Mentor and
        // Powerbalance name an opponent only as the trigger ("an opponent casts a
        // spell, you may reveal the top card of YOUR library") and then play their
        // own card. And "their library. They may play it" is the opponent playing
        // their own: Urabrask makes them exile instead of drawing.
        private static readonly Regex SomebodyElsesZone = Rx(
            @"(?:target opponent|an opponent|each opponent|that opponent|target player|that player"
            + @"|defending player|another player|each player|opponent'?s|player'?s)"
            + @"(?:(?!\byour\b)[\w ,'\-]){0,60}(?:library|hand|graveyard)"
            + @"|(?:library|hand|graveyard) of (?:target |an |each |that )?(?:opponent|player)"
            + @"|\btheir (?:library|hand|graveyard)\b(?!\. they may (?:play|cast))");

        /// <summary>A LAND out of their graveyard is Ramp, not theft — Realmbreaker
        /// mills the opponent to find one, and was tagged Ramp alone.</summary>
        private static readonly Regex LandCardFromAGraveyard = Rx(
            @"land cards? from [\w' ]{0,30}graveyard");

        /// <summary>…and then YOU play it. Everything excluded here is a card of
        /// your own that happens to share a text with somebody else's zone: an
        /// extra land drop (Ramp), a card giving ITSELF a second cast (Regrowth),
        /// the back face Jidoor plays out of exile, and the "cards you own" that
        /// Triple Triad and Wheel of Potential hand back after a symmetric exile.
        /// "They may cast" is excluded for the same reason — Transforming Flourish
        /// makes the OPPONENT cast the card it dug up.</summary>
        private static readonly Regex AndPlaysThem = Rx(
            @"you may (?:look at and )?(?:play|cast) (?!an additional land|this spell|this card|the land\b)");

        /// <summary>The same clause with the thief named in the third person. It
        /// only counts against a NAMED zone — see
        /// <see cref="SomebodyElsesNamedZone"/> for why.</summary>
        private static readonly Regex AndSomebodyPlaysThem = Rx(
            @"they may (?:look at and )?(?:play|cast) (?!an additional land|this spell|this card|the land\b)");

        /// <summary>Playing what was always yours. "Cards you own" is how a
        /// symmetric exile hands everything back, and "from YOUR graveyard" is
        /// Regrowth's business — Glacierwood Siege mills a player on one half and
        /// plays your own lands on the other, and only a rule reading the whole
        /// card could mistake that for a theft.</summary>
        // Press the Enemy casts "an instant or sorcery spell with equal or lesser
        // mana value FROM YOUR HAND", which no twenty letters could reach.
        // "Cast a spell from among cards IN your graveyard that were milled this
        // turn" is your own as well (Raul, Trouble Shooter, whose tap mills
        // every player; 2026-10-16).
        private static readonly Regex PlaysACardYouOwn = Rx(
            @"(?:cards?|spells?) you own"
            + @"|(?:play|cast)[\w ]{0,70}(?:from|in) your (?:graveyard|hand|library)");

        /// <summary>The mirror image of Steal, and 12 of its 14 false positives:
        /// the card hands a permanent to somebody ELSE. It is the price Jinxed
        /// Idol, Rainbow Vale, Chaos Lord and Stiltzkin pay, or the drawback
        /// Rohgahh and Emberwilde Djinn suffer for not paying upkeep, and Guardian
        /// Beast is the negation ("other players CAN'T gain control").
        /// <para>
        /// The subject sits IMMEDIATELY in front of the verb — all twelve read
        /// "&lt;somebody&gt; gains control", with nothing between but an optional
        /// "may" or "can't". Letting any filler in cost Ray of Command and Magus
        /// of the Unseen, where "untap target creature AN OPPONENT CONTROLS and
        /// gain control of it" puts the victim in front of a verb that is yours.
        /// </para></summary>
        private static readonly Regex GivesControlAway = Rx(
            @"(?:target opponent|an opponent|each opponent|that opponent|another player|target player"
            + @"|that player|other players|they|the player with the most life"
            + @"|that permanent's controller|the attacking player|the player to your right)"
            + @" (?:may |can't )?gains? control");

        /// <summary>A token that steals is the token's, not the card's: Wake the
        /// Dragon makes a Dragon whose combat damage takes an artifact, and was
        /// tagged Tokens alone. The same reading as a token's ping for Removal and
        /// Burn.</summary>
        private static readonly Regex TokenSteals = Rx(
            @"\btoken with [\w ,]{0,40}""[^""]*gains? control of[^""]*""");

        /// <summary>Taking somebody else's CARD: their zone and you playing out of
        /// it, or their graveyard onto your side of the board. Read one MODE at a
        /// time — Riveteers Charm exiles a player's graveyard in one bullet and
        /// plays your own library in another — and outside reminder text, where
        /// an Adventure's "you may cast the creature later" and a legendary
        /// sorcery's "you may cast" stood in for a play that never happens.</summary>
        private static bool TakesSomebodyElsesCard(string text)
        {
            string spoken = Parenthetical.Replace(text, " ");
            string[] lines = spoken.Split('\n');
            string[] modes = lines.Where(l => l.TrimStart().StartsWith('•')).ToArray();
            if (modes.Length == 0)
            {
                return TakesSomebodyElsesCard(spoken, text);
            }

            string rest = string.Join('\n', lines.Where(l => !l.TrimStart().StartsWith('•')));
            return modes.Any(mode => TakesSomebodyElsesCard(rest + "\n" + mode, text));
        }

        // The card you own is read on the WHOLE text, reminders included: Lidless
        // Gaze plays the top of each player's library and was tagged CardAdvantage
        // alone, and its flashback reminder is the only thing that says so.
        private static bool TakesSomebodyElsesCard(string spoken, string text)
        {
            bool yours = PlaysACardYouOwn.IsMatch(text);
            return ((SomebodyElsesZone.IsMatch(spoken) || MillsSomebody.IsMatch(spoken))
                    && AndPlaysThem.IsMatch(spoken) && !yours)
                || (SomebodyElsesZone.IsMatch(LandCardFromAGraveyard.Replace(spoken, " "))
                    && OntoYourSideOfTheBoard.IsMatch(spoken))
                || (SomebodyElsesNamedZone.IsMatch(spoken) && AndSomebodyPlaysThem.IsMatch(spoken) && !yours)
                || (TheirSpellExiled.IsMatch(spoken) && AndPlaysThem.IsMatch(spoken) && !yours);
        }

        /// <summary>Their SPELL exiled off the stack and cast by you: the card is
        /// somebody else's with no zone named (Smirking Spelljacker, Counter and
        /// Steal by hand). Read 2026-10-16.</summary>
        private static readonly Regex TheirSpellExiled = Rx(@"exile target spell an opponent controls");

        private static readonly Regex[] MillPatterns =
        [
            Rx(OtherPlayer + @"[\w ,]{0,40}mills?\b"),
            Rx(@"put(?:s)? the top [\w ]{0,25}" + OtherPlayer + @"[\w' ]{0,20}library into"),
            // Same sentence with the victim in front of the verb, which is how
            // Millstone and most pre-2021 mill is actually written.
            Rx(OtherPlayer + @"[\w ,]{0,30}puts? the top [\w ]{0,30}library into"),
            // The top of their library EXILED is the same attack, read
            // 2026-10-15 from the three reviewed cards that say it and nothing
            // more (Ulamog, the Defiler; Ashiok, Wicked Manipulator's -7;
            // Malboro). Not when a card is PLAYED, cast or put under your
            // control anywhere on that line or the next — that is Steal, and the
            // seven reviewed cards that say so carry no Mill (Outrageous
            // Robbery, Bloodsoaked Insight, Crabomination; Ensnared by the Mara,
            // whose OTHER villainous branch casts) — unless it is the card
            // itself cast face down (Raven Guild Master's megamorph reminder);
            // not when "each player" exiles, which
            // includes you (Memory Vessel, Triple Triad); and not inside a
            // TOKEN's quoted ability (Ashiok, Nightmare Muse).
            Rx(@"^(?![^\n]*(?:\n[^\n]*)?\b(?:(?:may|can) (?:cast|play)(?! this card\b)|(?:cast|play)s? (?:it|them|that card|those cards)|under your control)\b)"
                + @"(?:[^""\n]|""[^""\n]*"")*?\b(?:target (?:player|opponent)|each opponent|an opponent|that player|defending player|its controller)"
                + @"[\w ,]{0,40}exiles? the top [\w ]{0,30}of their library",
                RegexOptions.Multiline),
            // RAD COUNTERS mill whoever gets them, one card each at the start of
            // their main phase — 13 of 13 reviewed cards that hand them to a
            // player carry this tag, "each player" included (Nuclear Fallout,
            // Vault 12), read 2026-10-15. Not the ones YOU get (Contaminated
            // Drink, Mariposa Military Base, Strong, Tato Farmer).
            Rx(@"(?<!\byou (?:may )?)\bgets? [\w ]{0,25}\brad counters?|\bgive each (?:player|opponent) [\w ]{0,10}\brad counters?"),
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
            // The same sentence with the verb at the END, which the two rules
            // above cannot read: Stridehangar Automaton says "those tokens plus
            // an additional 1/1 Thopter … ARE CREATED instead".
            Rx(@"tokens?[\w ,'\-/]{0,80}(?:are|is) created"),
        ];

        // A token names a CREATURE in one of three ways, and only the first was
        // being read. The second is a token with a proper name whose creature
        // type lives in the reminder text — "Create a Spellgorger Weird token.
        // (It's a {2}{R} 2/2 Weird creature with …)" never says "creature
        // token" anywhere. The third is a COPY.
        // (?<!non): "an opponent sacrifices a NONCREATURE token" makes nothing
        // (Bloodroot Apothecary, The Third Doctor), read 2026-10-13.
        private static readonly Regex CreatureTokenWording = Rx(@"(?<!non)creature token");

        // Also inside a QUOTED grant, with the power left to the reminder's
        // own quote: Tarmogoyf Nest's land has "{1}{G}, {T}: Create a
        // Tarmogoyf token." (It's a {1}{G} Lhurgoyf creature with …), tagged,
        // read 2026-10-15. And the same Tarmogoyf printed with no reminder at
        // all: Disa the Restless "create[s] a Tarmogoyf token" on every combat
        // hit, tagged by hand, the one named token in the store that says no
        // type anywhere, read 2026-10-27.
        private static readonly Regex NamedTokenIsACreature = Rx(
            @"create[s]? (?:a|an|two|three|four|five|x|\d+)[\w ,'\-]{0,30} tokens?"
            + @"(?:\. \(it'?s? [\w ,'\-{}/]{0,40}\d+/\d+[\w ,'\-]{0,30}creature"
            + @"|\."" \(it's an? [\w ,'\-{}/]{0,40}creature)"
            + @"|\bcreates? an? tarmogoyf token");

        private static readonly Regex AnyTokenCopy = Rx(
            @"token that's a copy|token cop(?:y|ies)|tokens that are copies");

        // A PERMANENT CARD copied without asking for a creature is no body
        // either, ruled 2026-10-24 ("No Tokens se non chiede creature"):
        // Tamiyo, Compleated Sage copies a "nonland permanent card" from your
        // graveyard, untagged by hand; Nahiri's "creature or Equipment card"
        // names a creature and keeps the tag. Not a permanent on the
        // battlefield: "permanent" alone took the tag from Doppelgang,
        // Extravagant Replication and Moonlit Meditation, all tagged.
        private static readonly Regex NoncreatureBeingCopied = Rx(
            @"\b(?:artifact|equipment|enchantment|land|vehicle|clue|treasure|food|permanent card)\b");

        // Four keywords that put a BODY on the board and never say "token" in a
        // shape the rules above can read. Ruled 2026-09-19. Cloak and manifest
        // dread turn a card face down as a 2/2; living weapon and job select
        // hand the Equipment a Germ or a Hero to carry it. 29 reviewed cards
        // carry one of these and 24 were already tagged by hand. Plain MANIFEST
        // was left out then, being one card and untagged, and is read since
        // 2026-10-15 (FaceDownFromYourSide). Not "cloak THEM" (Expose the
        // Culprit re-cloaks creatures already yours), nor a trigger that only
        // WATCHES a manifest dread (Paranormal Analyst), 2026-10-15. "Cloak
        // THOSE CARDS" is a pile with library cards in it: Become Anonymous
        // exiles your creature and the top two cards of your library and
        // cloaks all three, tagged by hand like the other 7 reviewed cards
        // that cloak a card, read 2026-10-27.
        private static readonly Regex MakesABodyByKeyword = Rx(
            @"\bcloaks? (?:the|two|three|four|\w+ of them|up to|those cards)\b|(?<!whenever you )manifest dread"
            + @"|\bliving weapon\b|\bjob select\b");

        // MANIFEST is a body like cloak, ruled 2026-10-15 ("Tokens come
        // cloak"): Orochi Soul-Reaver and Kozilek, the Broken Reality tagged by
        // hand, Guardian of the Forgotten reached. Asked with reminder text
        // blanked, which says "To manifest a card" on every one of them, and
        // with the cloak Vannifar does from hand ("Cloak a card from your
        // hand", tagged). Not the body an exiled creature's controller gets
        // (Reality Shift), nor a trigger watching one, nor a card's NAME
        // (O-Kagachi Made Manifest), nor cards out of a GRAVEYARD, which are
        // reanimated as FaceDownBodiesFromALibrary says (Ghastly Conscription).
        private static readonly Regex FaceDownFromYourSide = Rx(
            @"(?<!whenever you |its controller |\bthey |\bmade |graveyard[^.\n]{0,100})\b(?:manifests?|cloaks? a card)\b(?! dread)");

        // A NONCREATURE permanent turned into a 0/0 creature and sized with
        // counters is a body made, the same shape as earthbend: Case of the
        // Filched Falcon "put four +1/+1 counters on target noncreature
        // artifact. It becomes a 0/0 Bird creature". Ruled Tokens 2026-09-25.
        // The same with the counters put on the card ITSELF (Haunted Screen's
        // "seven +1/+1 counters on this artifact. It becomes a 0/0 Spirit") or
        // on a permanent of yours named by type (Cosmium Confluence's Cave),
        // both tagged, read 2026-10-15.
        private static readonly Regex AnimatesANoncreature = Rx(
            @"counters? on (?:target noncreature [\w ]{0,20}|this (?:artifact|enchantment|land)|an? [\w ]{1,15} you control)"
            + @"\. it becomes an? 0/0\b[\w ,]{0,40}creature");

        // A LAND or an ARTIFACT that animates ITSELF is a body made too,
        // ruled 2026-10-13: the Restless lands, Cavernous Maw, Stalking Stones
        // (13 of 14 reviewed lands), Levitating Statue and Eye of Malcator;
        // Mishra's Factory, Jade Statue, Xanthic Statue and Chimeric Sphere,
        // untagged before the ruling, were realigned. An ENCHANTMENT joined on
        // 2026-10-15 ("Tokens, come terre e artefatti"): Reverberating Summons,
        // Emergent Haunting and Case of the Gorgon's Kiss tagged by hand, Ride
        // the Shoopuf reached. A VEHICLE is not here: it says "this Vehicle",
        // and the 13 reviewed ones that animate themselves other than by crew
        // are untagged, against 3 (Kylox's Voltstrider, War Balloon, Phoenix
        // Fleet Airship, handed back).
        private static readonly Regex SelfAnimates = Rx(
            @"\bthis (?:land|artifact|enchantment|case) (?:becomes|is) an? [^.\n]{0,40}\d+/\d+[^.\n]{0,60}creature"
            + @"|\bthis (?:land|artifact|enchantment) isn't a creature, it becomes an? \d+/\d+");

        // An ARTIFACT animated by the card is a body like the land, ruled
        // 2026-10-15 ("Tokens, come le terre"): the hand tags were split 6 to
        // 7 on identical wordings — Majestic Metamorphosis tagged, Relic's
        // Roar not; Katsumasa tagged, Tough Cookie not. Target or enchanted,
        // until end of turn or for good. A creature that does it once as it
        // enters is still taken back by the few-bodies rule (Unctus's
        // Retrofitter), which counts it in OneBodyByKeyword.
        private static readonly Regex AnimatesAnArtifact = Rx(
            @"\b(?:target|enchanted) (?:noncreature )?artifacts?(?: token| or creature)?(?: you control)? (?:becomes?|is|are) "
            + @"(?!a copy\b)(?:an? )?[\w ,/]{0,40}?\bcreatures?\b");

        // STORM on a creature card puts its copies on the board as tokens,
        // one for each spell cast before it (Stormscale Scion, tagged,
        // 2026-10-15). Not on an Aura (Tempest Technique, Amphibian Downpour,
        // untagged), and not a creature copying itself once (Sage of the
        // Skies), which the reminder words differently.
        private static readonly Regex StormCopiesBecomeTokens = Rx(
            @"copy it for each spell cast before it this turn\. copies become tokens");

        // A LAND cast face down is a 2/2 creature the land makes of itself,
        // the 2026-10-13 ruling on self-animating lands (Branch of Vitu-Ghazi,
        // tagged, 2026-10-15). Any NONCREATURE card too, ruled 2026-10-24
        // ("Tokens, come le terre"): Gift of Doom tagged, Concealed Weapon and
        // True Identity realigned.
        private static readonly Regex NoncreatureCastFaceDown = Rx(@"^(?:disguise|morph)\b", RegexOptions.Multiline);

        // DASH returns the creature to hand every turn it is cast that way, so
        // its enters trigger repeats, ruled 2026-10-13 on Riders of Rohan
        // (Tokens). The one dash card that makes bodies; warp casts it again
        // only once (Knight Luminary, untagged) and is not here.
        private static readonly Regex DashRepeatsTheEntry = Rx(@"^dash\b", RegexOptions.Multiline);

        // A copy of a CREATURE spell becomes a token, and that is how the human
        // tags it, ruled 2026-09-28: "copia una magia creature è Token" (Case of
        // the Shifting Visage, Double Down's outlaws, Thurid, Jackal). Never says
        // "create", so the token wording above cannot see it. Copying any OTHER
        // spell of yours is Redirect. A PERMANENT spell copied becomes a token
        // too (Archmage of Echoes' Faeries and Wizards, tagged Tokens and not
        // CardAdvantage, read 2026-10-13).
        private static readonly Regex CopiesYourCreatureSpell = Rx(
            @"(?:whenever|when) you (?:next )?cast [^.\n]{0,60}\b(?:creature|outlaw|permanent) spells?\b[^.\n]{0,60}, "
            + @"copy (?:it|that spell)\b");

        // ---- A CREATURE that makes one or two bodies, once ------------------
        //
        // Ruled 2026-09-24: a NONCREATURE card that puts creature bodies on the
        // board is Tokens, always, and a CREATURE card is Tokens only when it
        // makes THREE OR MORE at once or the effect REPEATS. The reason is how
        // the cube is built rather than how the card plays: a spell that makes
        // bodies is filed among the creatures, and a creature already is one,
        // so one Ally or one Spirit on the way in or the way out adds nothing
        // the card was not already counted for.
        //
        // It also settled a split nobody had ruled on. "When this creature
        // enters, create a 1/1 white Ally creature token" was tagged on Katara
        // and Invasion Reinforcements and not on Kyoshi Warriors or Treetop
        // Freedom Fighters — the identical sentence, 2 to 4 among the reviewed
        // cards printing it.
        //
        // Read per ability, and WITH reminder text, because that is where half
        // of these keywords keep their body: offspring, afterlife, fabricate,
        // mobilize, myriad, squad and encore all say "create" only inside the
        // brackets. Quoted text is blanked instead, since it is what the TOKEN
        // does ("with 'Whenever a land you control enters, this token gets
        // +1/+0'"), and a trigger the token carries does not make the card
        // repeat. A card on which nothing here can be read keeps the tag —
        // Stridehangar Automaton and Quina add a body to every token made and
        // say so as a replacement, which this reading has no count for.
        private static readonly Regex CreatesBodies = Rx(
            @"\bcreates? (?<n>a|an|one|two|three|four|five|six|seven|eight|nine|ten|x|\d+|that many|twice that many|a number of)\b"
            + @"(?<rest>[^.\n]{0,160})");

        // A token with a proper name: "create Beau, a legendary blue Ox creature
        // token", "create Primo, the Indivisible, a legendary 0/0 … creature
        // token". Always exactly one.
        private static readonly Regex CreatesANamedBody = Rx(
            @"\bcreates? [^.\n]{1,40}?, a legendary [^.\n]{0,80}creature token");

        // Bodies that never say "create": each mention is ONE body — earthbend
        // animates one land, manifest dread turns one card face down, and to
        // endure is to take the counters or one Spirit.
        private static readonly Regex OneBodyByKeyword = Rx(
            @"\bearthbends?\b|\bmanifest dread\b|\bendures? (?:\d+|x)\b|\bcloaks? (?:the|a)\b|\bincubate (?:\d+|x)\b"
            // An amass printed bare, once (Grishnákh, Sauron, Lord of the Rings,
            // untagged), 2026-10-13.
            + @"|\bamass(?: \w+)? (?:\d+|x)\b"
            // A manifest, and a land or an artifact animated — ONE target
            // (Unctus's Retrofitter as it enters), 2026-10-15.
            + @"|\bmanifests? (?:the top card|a card|one)\b"
            + @"|\b(?:up to one )?target (?:noncreature )?(?:land|artifact)(?: token| or creature)?(?: you control)? becomes\b");

        // …but an incubate REPEATED is as many bodies as it says, read
        // 2026-10-13: Progenitor Exarch incubates "X times" and Elesh Norn's
        // Saga "five times", both tagged, both vetoed as one body once.
        private static readonly Regex IncubatesSeveralTimes = Rx(
            @"\bincubate (?:\d+|x) (?:x|two|three|four|five|six|\d+) times\b");

        // AMASS puts an Army on the board, or grows the one there, and on a
        // card printed bare it never says "token" for the body check to read:
        // Barad-dûr, Gríma Wormtongue and Sauron, the Dark Lord, tagged, read
        // 2026-10-13 (amass is tagged on 25 of the 32 reviewed cards that say
        // it; a creature that amasses once is still taken off by the
        // few-bodies veto).
        private static readonly Regex AmassesAnArmy = Rx(@"\bamass\b");

        // Cards put onto the battlefield FACE DOWN as 2/2s, out of a LIBRARY
        // or a fresh mill, are new bodies (Cybership, The Cyber-Controller,
        // tagged); out of exile or a graveyard they are Reanimate (Death in
        // Heaven, Missy, untagged for this). Read 2026-10-13.
        private static readonly Regex FaceDownBodiesFromALibrary = Rx(
            @"(?:library|milled this way) onto the battlefield face down under your control\. they're 2/2");

        // INCUBATE makes an Incubator that becomes an artifact creature, and
        // never says "token" outside its reminder text. Ruled 2026-09-29 after
        // the human tagged Eyes of Gitaxias Tokens and left Furnace Gremlin
        // untagged the same evening: from a spell or as something enters it is
        // a body made ("solo da magia/ingresso"), while the one a creature
        // leaves when it DIES only replaces it. That second half needs no rule
        // of its own: the four cards that incubate on death are creatures making
        // one body once, which OnlyAFewBodiesOnce (2026-09-24) already takes
        // back — so is a creature that incubates once as it enters (Converter
        // Beast). A dies-exclusion was written and neutralising it turned
        // nothing red, and no card moved without it.
        private static readonly Regex Incubates = Rx(@"\bincubate (?:\d+|x)\b");

        private static readonly Regex CloaksSeveral = Rx(@"\bcloaks? (?:two|three|four|\w+ of them)\b");

        // NOT here: "each manifest" (Kozilek, the Broken Reality) and "any number
        // of target artifacts/lands" (Depthshaker Titan) read as reaching three
        // were tried on 2026-10-15 — both cards keep the tag by other rules,
        // nothing went red without them and no card moved, so they were removed.

        // "Instead create X of those tokens" counts the creature tokens the
        // sentence before named (Adipose Offspring), 2026-10-15.
        private static readonly Regex OfThoseTokens = Rx(@"^\s*of (?:those|these) tokens\b");

        // The count sits right after the token when it is a count at all:
        // "create a 1/1 green Insect creature token FOR EACH artifact" (Aatchik).
        // Asked of the FIRST token word only, because Outlaw Stitcher makes ONE
        // Zombie and then puts counters "on that token for each spell".
        private static readonly Regex CountFollowsTheToken = Rx(
            @"^[^.\n]*?\btokens?(?: [\w ,'-]{0,40})? (?:for each|equal to)\b(?! opponent)");

        private static readonly Regex CountComesFirst = Rx(@"\bfor each\b(?! opponent\b)");

        // What makes an ability run more than once, on top of AbilityRepeats:
        // "at end of combat" (Kjeldoran Home Guard), a trigger on entering OR
        // attacking (Inspirited Vanguard), and exert, which is asked "as it
        // attacks" (Sandstorm Crasher).
        private static readonly Regex BodyEveryCombat = Rx(
            @"\bat end of combat\b|\benters or attacks\b|\bas it attacks\b"
            // …and a REPLACEMENT that answers every death: "If a nontoken
            // creature an opponent controls would die, exile it instead. When
            // you do … create a 1/1 Pest" (Valentin, Dean of the Vein).
            + @"|\bif (?:a|an|another) [\w ,-]{0,40} would die\b");

        // Two keywords that make bodies on EVERY attack and are often printed
        // bare, with no reminder text for the rule above to read: Chittering
        // Dispatcher says only "Myriad".
        private static readonly Regex BodiesOnEveryAttack = Rx(@"\b(?:myriad|mobilize)\b");

        // …and what makes a cost line run ONCE: the card pays with itself.
        // "{2}{B}{B}, Exile this card from your graveyard: Create two tapped 1/1
        // Bats" (Leering Onlooker), and encore, which says the same inside its
        // reminder text.
        private static readonly Regex BodyCostSpendsItself = Rx(
            @"(?:sacrifice this (?:creature|permanent|artifact|card)|exile this card from your graveyard)[^:\n]{0,40}:");

        /// <summary>How many creature bodies one ability makes: null when it
        /// makes none, <see cref="int.MaxValue"/> when the count is X or "for
        /// each", which can always reach three.</summary>
        private static int? BodiesMadeBy(string ability)
        {
            int? bodies = null;

            foreach (Match m in CreatesBodies.Matches(ability))
            {
                string rest = m.Groups["rest"].Value;
                bool isBody = rest.Contains("creature token", StringComparison.OrdinalIgnoreCase)
                    || AnyTokenCopy.IsMatch(rest)
                    || (OfThoseTokens.IsMatch(rest) && ability.Contains("creature token", StringComparison.OrdinalIgnoreCase));

                if (!isBody)
                {
                    continue;
                }

                int n = m.Groups["n"].Value.ToLowerInvariant() switch
                {
                    "a" or "an" or "one" => 1,
                    "two" => 2,
                    "three" => 3,
                    "four" => 4,
                    "five" => 5,
                    "six" => 6,
                    "seven" => 7,
                    "eight" => 8,
                    "nine" => 9,
                    "ten" => 10,
                    string digits when int.TryParse(digits, out int d) => d,
                    _ => int.MaxValue,
                };

                if (CountFollowsTheToken.IsMatch(rest))
                {
                    n = int.MaxValue;
                }

                // …or the count comes FIRST, earlier in the same sentence:
                // "for each nontoken creature you controlled that died this
                // turn, create a 2/2 black Zombie" (Tobias). Encore's "for each
                // opponent, create a token copy" stays one: the copies are no
                // bodies (2026-10-27), and what they make again is counted in
                // OnlyAFewBodiesOnce.
                int sentenceStart = Math.Max(
                    ability.LastIndexOfAny(['.', '\n', '•'], Math.Max(m.Index - 1, 0)) + 1, 0);
                if (CountComesFirst.IsMatch(ability[sentenceStart..m.Index]))
                {
                    n = int.MaxValue;
                }

                // A LIST of bodies in one sentence is each of them: Somberwald
                // Beastmaster makes "a 2/2 green Wolf creature token, a 3/3 green
                // Beast creature token, and a 4/4 green Beast creature token".
                n = Math.Max(n, CreatureTokenWording.Count(rest));

                bodies = Math.Max(bodies ?? 0, n);
            }

            if (CreatesANamedBody.IsMatch(ability))
            {
                bodies = Math.Max(bodies ?? 0, 1);
            }

            // Counted OUTSIDE the brackets: earthbend's reminder text names the
            // keyword again ("(To earthbend 1, target land …)"), which made Dai
            // Li Agents' two earthbends three.
            int keywordBodies = OneBodyByKeyword.Count(Parenthetical.Replace(ability, " "));
            if (IncubatesSeveralTimes.IsMatch(ability))
            {
                keywordBodies = int.MaxValue;
            }

            if (keywordBodies > 0)
            {
                bodies = Math.Max(bodies ?? 0, keywordBodies);
            }

            if (CloaksSeveral.IsMatch(ability))
            {
                bodies = Math.Max(bodies ?? 0, 2);
            }

            return bodies;
        }

        // A DELAYED trigger fires once: Rukh Egg dies and makes its Bird "at the
        // beginning of THE NEXT end step", which the repeat rule would read as
        // an upkeep engine.
        private static readonly Regex DelayedOnce = Rx(@"at the beginning of (?:the|your) next [\w ]{0,20}");

        private static bool BodyAbilityRepeats(string ability)
        {
            string asked = DelayedOnce.Replace(ability, " ");

            return (AbilityRepeats(asked) || BodyEveryCombat.IsMatch(asked))
                && !BodyCostSpendsItself.IsMatch(asked);
        }

        private static bool FrontFaceIsACreature(Card card) =>
            (card.TypeLine ?? string.Empty).Split("//")[0]
                .Contains("Creature", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the card makes creature bodies and EVERY ability
        /// that does makes one or two of them, once. Asked only of a card whose
        /// FRONT face is a creature — see the call site for why the whole type
        /// line will not do.</summary>
        private static bool OnlyAFewBodiesOnce(string text)
        {
            bool makesAny = false;

            // A quote that ENDS its sentence keeps the full stop, or the next
            // sentence reads as part of the token's: Twisted Sewer-Witch's Rat
            // "with \"This creature can't block.\" Then for each Rat you
            // control, create a Wicked Role" read as a Rat for each, 2026-10-15.
            string own = Quoted.Replace(text, m => m.Value.EndsWith(".\"", StringComparison.Ordinal) ? ". " : " ");

            if (BodiesOnEveryAttack.IsMatch(own) || DashRepeatsTheEntry.IsMatch(own))
            {
                return false;
            }

            int entering = 0;
            int dying = 0;
            foreach (string ability in Abilities(own))
            {
                if (BodiesMadeBy(ability) is not int bodies)
                {
                    continue;
                }

                makesAny = true;
                if (bodies >= 3 || BodyAbilityRepeats(ability))
                {
                    return false;
                }

                // Counted on your side only: Amphin Mutineer's exiled creature
                // comes back as its controller's 4/3 (untagged).
                int yours = BodiesMadeBy(WhatSomebodyElseCreates.Replace(ability, " ")) ?? 0;
                switch (EntersOrDies.Match(ability).Groups["event"].Value.ToLowerInvariant())
                {
                    case "enters": entering += yours; break;
                    case "dies": dying += yours; break;
                }
            }

            if (EncoreKeyword.IsMatch(own) && 3 * (entering + dying) >= 3)
            {
                return false;
            }

            if (UnearthKeyword.IsMatch(own) && 2 * entering >= 3)
            {
                return false;
            }

            return makesAny;
        }

        // A body made on a TRIBE's trigger counts only when the card sets the
        // trigger off by itself, ruled 2026-10-27 ("i tribali contano solo se
        // generano da soli"), as Cheat and Reanimate did on 2026-10-15 and
        // Burn's drain on a tribe on 2026-10-13. Out: a trigger on ANOTHER or a
        // NONTOKEN member (Siege Veteran, Baboon Spirit, Knightfisher, Miirym),
        // one on a tribe the card neither is nor makes (Bishop of Wings'
        // Angels), and every cast of a tribe's SPELL, since the tokens are no
        // spells (Rohgahh, Urtet, Rin and Seri). In: a card of the tribe that
        // says "a" or "one or more" (General Kreat, Zeriam), or one that makes
        // the tribe's tokens itself (Origin of the Hidden Ones). A land type
        // is no tribe (Hazezon's and Yuma's Deserts, Titania's Forests).
        // Blanking the modes such a trigger chooses among ("•" lines) too was
        // tried and moved no card: Black Panther, Vanguard loses its bodies to
        // OnlyAFewBodiesOnce once the trigger line is gone.
        private const string NotACreatureType =
            @"Desert|Forest|Plains|Island|Swamp|Mountain|Gate|Cave|Shrine|Sphere|Town|Lair|Locus|Aura|Equipment"
            + @"|Vehicle|Saga|Treasure|Clue|Food|Blood|Map|Powerstone|Arcane|Lesson|Adventure";

        private static readonly Regex BodyOnATribe = Rx(
            @"^(?:[^—\n]{1,40}— )?whenever (?:(?<another>another|a|an|one or more(?: other)?) (?<nontoken>nontoken )?"
            + @"|(?<cast>you cast (?:a|an|another)) )"
            + @"(?!(?-i:" + NotACreatureType + @")\b)(?<tribe>(?-i:[A-Z])[a-z]+?)(?:s|es)? [^\n]*",
            RegexOptions.Multiline);

        /// <summary>The text with every body-making line on a tribe the card
        /// cannot set off by itself blanked. See BodyOnATribe.</summary>
        private static string BlankTribalBodyTriggers(Card card, string text) =>
            BodyOnATribe.Replace(text, m =>
            {
                if (m.Groups["cast"].Success)
                {
                    return " ";
                }

                // "Elves", "Dwarves": the lazy match stops at "Elv".
                string tribe = m.Groups["tribe"].Value;
                tribe = Regex.Escape(tribe.EndsWith('v') ? tribe[..^1] + "f" : tribe);
                bool isIt = !m.Groups["another"].Value.Contains("other", StringComparison.OrdinalIgnoreCase)
                    && Rx(@"\b" + tribe).IsMatch(card.TypeLine ?? string.Empty);
                bool makesIt = !m.Groups["nontoken"].Success
                    && Rx(@"\bcreate[^.\n]*\b" + tribe + @"[^.\n]*\btokens?\b").IsMatch(text);
                return isIt || makesIt ? m.Value : " ";
            });

        // ENCORE and UNEARTH bring the creature back, and what it makes as it
        // enters is made again: the bodies of every entry are counted
        // together, and three or more is Tokens, ruled 2026-10-27 on Broodmate
        // Tyrant ("Sì, se le due entrate fanno 3+"). The cube is played 2vs2,
        // so encore makes a copy for EACH of two opponents ("encore vale
        // doppio"): the card enters three times, and dies three times, its
        // copies sacrificed at the end step — Exquisite Huntmaster's Elf on
        // death is "1 + 2 token", tagged by name. The copies themselves are no
        // bodies ("le copie non contano"). Unearth returns the card once and
        // exiles it, so only its entry counts: Royal Warden's two Necrons are
        // four. Warp casts it again once too, and no warp card makes two as it
        // enters (Knight Luminary makes one, untagged).
        private static readonly Regex EncoreKeyword = Rx(@"^encore\b", RegexOptions.Multiline);

        private static readonly Regex UnearthKeyword = Rx(@"^unearth\b", RegexOptions.Multiline);

        // An ability word may come first: "Phalanx Commander — When this
        // creature enters" (Royal Warden).
        private static readonly Regex EntersOrDies = Rx(@"^(?:[^—\n]{1,40}— )?when [^,\n]{1,60}\b(?<event>enters|dies)\b");

        // Diabolic Edict and Flare of Malice make the OTHER player sacrifice,
        // which empties their board rather than giving you a place to put yours.
        private static readonly Regex SomebodyElseSacrifices = Rx(OtherPlayer + @"[\w ,]{0,40}sacrifices?\b");

        // Destroying or exiling your OWN permanent at will is an outlet by
        // another verb, ruled 2026-09-25: Despotic Scepter's "{T}: Destroy target
        // permanent you own", Rats of Rath's "… you control", City of Shadows'
        // "{T}, Exile a creature you control:" — all three hand-tagged, none read.
        // Not an Aura ATTACHED to your creature (Miracle Worker saves it), and not
        // the price of a HARNESS, which is paid once (The Soul Stone).
        private const string SacrificesYourOwnByAnotherVerb =
            @"^[^\n:]{0,40}:\s*destroy target (?![\w ]*attached)[\w ,]{0,40}(?:you control|you own)"
            + @"|^[^\n:]{0,40}exile (?:a|another) (?:creature|artifact)[\w ]{0,20}you control[^\n:]{0,20}:(?!\s*harness)";

        // Exploit, casualty, devour and bargain sacrifice ONCE, as the card is
        // cast or enters — and their reminder text is all the classifier sees
        // of them, which is blanked. HANDED ON, the same keyword comes round
        // again: Colonel Autumn gives exploit to every other legendary creature,
        // Anhelo gives casualty to the first spell of each turn, Dragon
        // Broodmother's upkeep token arrives with devour.
        private const string SacrificeKeywordGranted = @"\b(?:have|has|gains?|with) (?:exploit|casualty|devour|bargain)\b";

        private const string WhatAnOutletEats =
            @"sacrifices? (?:a|an|another|two|three|\d+|x|any number of|one or more) [\w ]*(?:creature|artifact|permanent)";

        // Only a REPEATABLE outlet is this tag, ruled 2026-09-25: "sì, togliamo
        // Sacrifice alle carte una tantum". The human had tagged one-shots 36
        // times up to 2026-09-23 and none of the 50 they met on the 25th. So a
        // sacrifice counts only in one of these shapes, and the one-shots —
        // an additional cost to cast, kicker, "rather than pay", an entering
        // "you may sacrifice another creature", a sorcery's "sacrifice a
        // creature. If you do" — do not:
        //   - a COST in front of a colon, including one usable only in your
        //     upkeep (Ebon Praetor, Marjhan: "anche se c'è un sacrificio ad ogni
        //     mantenimento"). What it eats is a creature or an ARTIFACT
        //     ("Sacrifice = creature o artefatti": Orcish Mechanics, Ezrim), but
        //     not an artifact TOKEN, which is Sophia cashing in her own Clues —
        //     the resource tokens are never tagged (0 of 5);
        //   - a REPEATING trigger that sacrifices in its effect, optional or
        //     forced: Shadow's "whenever … you may sacrifice", Kylox's "whenever
        //     Kylox attacks, sacrifice any number", Lord of the Pit's upkeep.
        //     The trigger's own condition is not it — "whenever you sacrifice a
        //     creature, draw" is a payoff, so the sacrifice has to follow the
        //     comma;
        //   - a card that can be CAST AGAIN for it: buyback on an additional cost
        //     (Worthy Cause), "cast … by sacrificing" (Wickerfolk Indomitable,
        //     Into the Pit);
        //   - an EQUIP cost (Dissection Tools), exploit handed to other creatures
        //     (Colonel Autumn), and the other verbs above.
        // Asked of the text with reminder text blanked: exploit's and kicker's
        // reminders both say "you may sacrifice".
        private static readonly Regex SacrificeOutlet = Rx(
            @"^(?!\W*(?:when|whenever|at the|as an additional|kicker))[^\n:]{0,60}" + WhatAnOutletEats + @"(?! token)[^\n:]{0,60}:"
            // In a trigger the sacrifice must be YOURS: Grave Pact's "each other
            // player sacrifices" and Tomb Blade's "unless they sacrifice" are
            // edicts the card-wide guard does not reach. And the trigger must be
            // one you fire: losing life is the opponent's doing, so Oath of
            // Lim-Dûl's "whenever you lose life, … sacrifice a permanent" is a
            // price the card makes you pay, not an outlet — "è un effetto extra
            // non è a comando", ruled 2026-09-25. Lich's Tomb is the same card,
            // and damage is the same price (Phyrexian Negator, Lich, Phyrexian
            // Totem: "whenever … is dealt damage, sacrifice that many") — not
            // guarded here only because "that many" is not read at all yet.
            + @"|^(?:[^—\n]{1,40}— )?(?:whenever(?! you lose life\b)|at the beginning of)[^,\n]*,[^\n]*?"
            + @"(?<!\b(?:they|player|players|opponent|opponents|controller) )\b" + WhatAnOutletEats
            // A LOYALTY ability is used again every turn (Chandra, Spark Hunter),
            // and so is an activated ability whose EFFECT sacrifices (Joo Dee:
            // "{B}, {T}: … then sacrifice an artifact or creature").
            + @"|^\[?[+−-]?[\dx]+\]?:[^\n]*?\b" + WhatAnOutletEats
            + @"|^[^\n:]{0,40}\{[^\n:]{0,40}:[^\n]*?\bthen sacrifice (?:a|an|another) [\w ]*(?:creature|artifact)"
            + @"|\bequip\W{1,3}sacrifice (?:a|an) (?:creature|artifact)"
            + @"|" + SacrificeKeywordGranted + @"|\bcast [^\n.]{0,60}\bby [^\n.]{0,30}sacrificing (?:a|an|another)[\w ]*(?:creature|artifact|permanent)"
            + @"|" + SacrificesYourOwnByAnotherVerb,
            RegexOptions.Multiline);

        private static readonly Regex Buyback = Rx(@"^buyback\b", RegexOptions.Multiline);

        private static readonly Regex SacrificeAsAdditionalCost = Rx(@"as an additional cost to cast this spell,[^\n]{0,40}sacrifice");

        /// <summary>Can you feed your own creatures or artifacts to this card
        /// again and again? See <see cref="SacrificeOutlet"/>.</summary>
        private static bool IsSacrificeOutlet(string text)
        {
            string own = Parenthetical.Replace(text, " ");
            return SacrificeOutlet.IsMatch(own)
                || (Buyback.IsMatch(own) && SacrificeAsAdditionalCost.IsMatch(own));
        }
    }
}
