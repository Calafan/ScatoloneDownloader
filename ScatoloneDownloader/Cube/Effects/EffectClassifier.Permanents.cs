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
        /// CREATURE is a creature rather than an artifact, and Auras attached to
        /// IT, THEM, THAT creature or a name leave WITH that permanent (Silence
        /// the Believers, Gauntlets of Chaos, Hakim) or in a fight (Treefolk
        /// Mystic), so they are not what the card answers.
        /// <para>
        /// An Aura attached to "a"/"an"/"target" thing IS the answer, ruled
        /// 2026-11-10 ("Disenchant a tutte"), overturning the 2026-09-19 reading
        /// that it was about the thing: the Pacifism on your creature is somebody
        /// else's enchantment. Miracle Worker, Savaen Elves and Pyramids were 0 of
        /// 3 reviewed and are tagged by that ruling; Devout Harpist, Piety Charm,
        /// Street Sweeper and Strip Bare, unreviewed, follow.
        /// </para>
        /// <para>
        /// No other SUBTYPE is read, ruled 2026-11-10 ("nessun sottotipo",
        /// "risposta stretta"): Equipment, Vehicle, Room or Curse is a niche answer
        /// to one kind of permanent (Molten Rebuke, Anthropede, Crash and Burn: 0
        /// of 6 reviewed), while the Aura is the shape in which an enchantment
        /// takes a creature out, the answer Disenchant is for.
        /// </para>
        /// See the rule table and <see cref="AnswersAnArtifactOrEnchantment"/>
        /// for the cards that taught each clause.</summary>
        private const string DisenchantNoun =
            @"[\w '\-]{0,14}(?<!non)(?<!non-)(?:artifact|enchantment|aura)s?\b(?! creature)(?! attached to (?!(?:a|an|target) ))";

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
            //
            // "Each permanent chosen this way" is the same clause, and the choosing
            // may be EVERY player's: Druid of Purification's "starting with you,
            // each player may choose an artifact or enchantment you don't control.
            // Destroy each permanent chosen this way" (Disenchant by hand, read
            // 2026-11-06). Counted broad, "choose an artifact/enchantment ...
            // destroy the chosen / each permanent chosen": 2 reviewed cards, both
            // tagged (Ultimate Magic: Meteor, Druid), no untagged, no unreviewed.
            // "Choose an ENCHANTMENT" first was in the count and moves no card
            // (no card says it), so it is not written.
            Rx(@"choose an? [\w ,'\-]{0,30}\bartifacts?\b[^\n]{0,40}destroy (?:the chosen|each permanent chosen)"),
            // A SECOND target after "and": "destroy up to one target planeswalker
            // and up to one target artifact" (Sword of Sinew and Steel, tagged
            // by hand, 2026-10-20). Counted broad, "destroy/exile ... and
            // (up to N) target <artifact/enchantment>": 2 reviewed cards, both
            // tagged (Convert to Slime reads it already), no untagged, 3
            // unreviewed all proposed.
            Rx(@"(?:destroy|exile) [^.\n]{0,60}? and up to \w+ (?:other )?target (?:[\w '\-]{1,14}, )?" + DisenchantNoun),
            // The edict repeated for another noun: Invoke Despair's "target
            // opponent sacrifices a creature of their choice. If they can't, they
            // lose 2 life and you draw a card. Then repeat this process for an
            // enchantment and a planeswalker" (Removal and Disenchant by hand,
            // 2026-10-22), the edict of Pick Your Poison and Gaius van Baelsar
            // read for the second noun. The only card that says it (1 of 1
            // tagged), so the wording is unique.
            Rx(@"\brepeat this process for an? (?:artifact|enchantment)\b"),
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
        /// Serenity, Seeds of Innocence — leaves creatures alone.
        /// <para>
        /// The creature an Aura is attached to is not swept: "destroy all Auras and
        /// Equipment attached to target creature" (Strip Bare) leaves it where it
        /// is, read with the 2026-11-10 Aura ruling.
        /// </para></summary>
        private static readonly Regex SweepsCreaturesToo = Rx(
            @"(?:destroy|exile) (?:all|each)[\w ,'\-]{0,40}(?<!attached to (?:a|an|target) )creatures?\b");

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
        /// Earth Kingdom Jailer, which names an artifact and was then tagged here
        /// by hand too (2026-10-15).
        /// </para></summary>
        private static readonly Regex ExileThatComesBack = Rx(
            @"return (?:it|them|the exiled cards?|that card)[\w ,'\-]{0,40}to the battlefield");

        /// <summary>Destroying your own is never an answer: Rats of Rath reads
        /// "destroy target artifact, creature, or land you control".
        /// <para>
        /// "You control" after "ATTACHED TO" is the creature's, not the Aura's:
        /// Miracle Worker's "destroy target Aura attached to a creature you
        /// control" takes the opponent's Pacifism off your creature (ruled
        /// 2026-11-10).
        /// </para></summary>
        private static readonly Regex DisenchantsYourOwn = Rx(
            @"(?:destroy|exile) (?:up to \w+ |another |x |\d+ )?target(?:(?!attached to\b)[\w ,'\-]){0,40}you control");

        private const string BasicLandType = @"\b(?:plains|islands?|swamps?|mountains?|forests?)\b";

        // Hoisted for the same reason as MillPatterns: the guard below re-runs
        // them one line at a time, so a card that fetches a land on one mode and a
        // creature on another is judged mode by mode.
        // Redefined 2026-11-08 (Tutor.md): the player who searches CHOOSES one card
        // out of their whole library, or out of the sideboard, and takes it out to
        // wherever the card says. Demonic Tutor is the card. Not what turns up at
        // random, not the top N, not a land.
        private static readonly Regex[] TutorPatterns =
        [
            // The zones may be LISTED with commas — "search your library,
            // graveyard, and/or outside the game for" (Invasion of Arcavios,
            // tagged by the human on 2026-09-29) — hence the comma and the 40.
            // "SEARCHES their library" is the same search said of another player
            // (read 2026-11-08: Scheming Symmetry's "each of them searches", and
            // Varragoth's "target player searches", 2 of 2 tagged by hand), as
            // Noble Benefactor's "each player may search" already was.
            Rx(@"search(?:es)? (?:your|their) library[\w ,/]{0,40}for (?:up to \w+ |two |three |four |five |\d+ )?"
                + @"[\w ,'\-/]{0,50}cards?\b"),
            // Demonic Consultation never searches: it names a card and then digs
            // until the NAME turns up. The name is the whole point — "reveal cards
            // until you reveal a LAND card" (The Regalia, House Cartographer) or
            // "a creature card" (Yuna's Whistle) or "a white card" (Sacred Guide)
            // hands you whichever one happened to be nearest the top, and the
            // human tags none of those five.
            Rx(@"reveal cards? from the top of your library until you reveal[^\n]{0,30}chosen name"),
            // Ring of Ma'ruf fetches from the sideboard, which is a library the
            // rules text has no other word for. NOT the LESSON card of LEARN's
            // reminder text ("You may reveal a Lesson card you own from outside
            // the game and put it into your hand, or discard a card to draw a
            // card"): ruled 2026-11-05 ("learn is not Tutor": Tutor searches a
            // LIBRARY, and learn reveals a Lesson from outside the game or
            // rummages). Gnarled Professor was untagged by hand (reviewed 11-05),
            // Igneous Inspiration is reached and handed back without the tag; the
            // classifier proposed Tutor on 19 of the 20 unreviewed learn cards. The
            // veto is the one word "Lesson": the other outside-the-game cards (the
            // wishes, Vivien, Arkbow Ranger, The Raven's Warning) never name a
            // Lesson and stay, as Tutor.md records the sideboard. Measured as a
            // family on 2026-11-05: learn 1 tagged of 2 reviewed, and the Lesson
            // reminder is on exactly the cards that say learn.
            // Kept on 2026-11-08 as the one exception to the redefinition (Q2,
            // "learn fuori, eccezione"), with a new reason, since the Wishes
            // search no library either: learn is a rider with a fallback, the
            // rummage, and it fetches only a Lesson, a pool the cube has only
            // if somebody drafted it on purpose.
            Rx(@"(?<!\blesson )card you own from outside the game"),
        ];

        /// <summary>A land search is Ramp or ManaFixing — the ruling this tag was
        /// written around, and the only reason the wide rule above is safe. The
        /// word boundary is what keeps "a NONLAND permanent card" out, and
        /// <see cref="SearchesForSomethingElseToo"/> is what keeps Starfield
        /// Shepherd's "a basic Plains card OR a creature card" in.
        /// <para>ANY land was ruled out on 2026-11-08 ("terra mai Tutor") and
        /// is OVERTURNED for a land that may be nonbasic on 2026-11-10: see
        /// <see cref="SearchesForANonbasicLand"/>, which takes a line back from
        /// this veto. What stays here is the basic land, the basic land TYPE and
        /// (narrowed the same day) the nonbasic subtype and the exiled land.
        /// The subtype has to be followed by "card", or "a card named Command
        /// Tower" would read as one; Tower Winder is Tutor through the name
        /// either way.</para></summary>
        private static readonly Regex SearchesOnlyForALand = Rx(
            @"library[^.\n]{0,40}for [^.\n]{0,25}(?:basic |snow |nonbasic )*(?:\blands?\b|" + BasicLandType
            + "|" + NonbasicLandType + ")");

        // Only the subtypes some card in the store searches for: Desert, Cave and
        // Gate (Maze's End, unreviewed). Lair, Locus, Sphere, Urza's and the rest
        // were in the first draft and moved no card, so they are not here.
        private const string NonbasicLandType = @"\b(?:caves?|deserts?|gates?) cards?\b";

        /// <summary>A land that MAY BE NONBASIC is chosen, and is Tutor: ruled
        /// 2026-11-10 ("versione stretta"), overturning "terra mai Tutor" of
        /// 11-08 for these lands. "A land card" not qualified as basic (Elvish
        /// Reclaimer, Golos, Expedition Map, Primeval Titan, Elemental Teachings)
        /// finds a land with something of its own, so the player who searches
        /// chooses one. A BASIC land, a basic land TYPE ("an Island or Swamp
        /// card", the fetch wording, which also finds the typed duals) and "a land
        /// card WITH / OF EACH basic land type" (Boseiju's consolation, Gaea's
        /// Balance) stay Ramp or ManaFixing: they fix, and the choice is the
        /// colour. Measured before the code: 69 cards say it, 65 of them without
        /// the tag, 32 reviewed.
        /// <para>Narrowed the same day, from the human's sitting. A nonbasic
        /// SUBTYPE ("a Desert / Cave / Gate card") is OUT, ruled "escludere i tipi
        /// di terra definiti": the subtype is a pool as narrow as a basic type
        /// (4 reviewed untagged in the sitting, Map the Frontier, Sand Scout; The
        /// Cave of Two Lovers, confirmed before the ruling, realigned). And a land
        /// the search EXILES is gained by nobody (Mana Severance, untagged in the
        /// sitting; Strata Scythe and Trench Gorger say the same). Only for a
        /// land: a card tutored into exile stays Tutor (Beseech the Mirror,
        /// Foresight: 6 reviewed searches of your own library, all 6 tagged).</para>
        /// <para>Read 2026-11-10 (second sitting): "land cards THAT EACH HAVE a
        /// basic land type" is the same pool as "with a basic land type"
        /// (Slimefoot's Survey, Ramp and Filter by hand; the one card that says
        /// it).</para></summary>
        private static readonly Regex SearchesForANonbasicLand = Rx(
            @"library[^.\n]{0,40}for [^.\n]{0,60}?(?<!basic )(?<!snow )\blands? cards?\b"
            + @"(?! (?:with|of each|that each have) (?:a )?basic land type)(?!,? (?:and )?exile\b)");

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
            // …and a land at the end of a list of "up to one" targets too long
            // for the run of 80 (Chaotic Transformation's five, after
            // "and/or"; LandDestruction by hand, read 2026-10-30, 2 of 2 with
            // Boom Box).
            Rx(@"(?:destroy|exile) [^.\n]{0,160}\bup to one target land\b"),
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
            // A COMPARISON names nobody as the victim, ruled 2026-11-03 on the
            // principle of 2026-09-15 (a tag is what the card aims at somebody,
            // never what it pays): Akuta, Born of Ash's "if you have more cards
            // in hand THAN each opponent, you may sacrifice a Swamp" pays its own
            // land. Own-land sacrifice is untagged on 66 of the 69 reviewed
            // cards that make it; the lookbehind keeps "than" off the subject.
            Rx(@"(?<!\bthan )(?:target (?:player|opponent)|each player|that player|each opponent)"
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

        // Steal was REDEFINED 2026-11-08 (docs/ontology/Steal.md, in its head):
        // you take control of a PERMANENT somebody else controls, for a turn (Ray
        // of Command) or for good (Control Magic, the card). A CARD taken out of
        // their library, hand or graveyard and played is CardAdvantage at most,
        // never this tag ("Sì, solo permanenti"), so the family that stood here
        // went that day with its eleven members (TakesSomebodyElsesCard: their
        // zone and you playing out of it, their graveyard onto your side of the
        // board, "they may play" against a named zone, their spell exiled and
        // cast): 78 hand tags of the 234 were a card of theirs played. With it
        // went "exchange life totals" (Burn since that day), Word of Command's
        // "you control that player" (a card of theirs played, no tag), and the
        // Aura moved, "attach target Aura" (Enchantment Alteration, Crown of the
        // Ages: nobody's control changes, no tag). Reanimating out of THEIR
        // graveyard is Reanimate alone and Bribery is Cheat, ruled the same day.
        //
        // Hoisted for the same reason as MillPatterns: the donation guard below
        // strips the clause that gives a permanent AWAY and then re-runs these,
        // so a card that steals and donates in the same text keeps the tag.
        private static readonly Regex[] StealPatterns =
        [
            // …but not of a SPELL, which is Redirect's (Invert Polarity), nor of
            // what is already YOURS, read 2026-11-08 under the definition: "gain
            // control of target permanent you own but don't control" (Coveted
            // Falcon, CardAdvantage alone by hand) and "each player gains control
            // of all permanents they own" (The Fall of Lord Konda, Removal alone)
            // take nothing from anybody.
            Rx(@"gains? control of(?! (?:target|that) spell)(?![^.\n]{0,40}\b(?:you|they) own\b)"),
            // "AS LONG AS you control enchanted creature" is a condition on the
            // Aura's own creature, not a theft (Mishra's Domination, Buff alone
            // by hand, read 2026-11-03; the one card in the store worded so).
            // "You control target PLAYER during that player's next turn" is the
            // one thing kept that is no permanent, ruled 2026-11-08 ("Steal,
            // eccezione col perché"): for that turn every permanent of theirs is
            // yours to run (Mindslaver, Worst Fears, Secret of Bloodbending).
            Rx(@"(?<!as long as )you control (enchanted|target)"),
            Rx(@"untap target creature[\w ]*gain control"),
            // An exchange is a theft you paid for. Five cards, and the only way
            // any of them was ever going to be found.
            Rx(@"exchange control of"),
            // Desertion takes the spell it just countered, which is the only place
            // "under your control" appears without a zone to read it against.
            // Kept 2026-11-08 ("Desertion resta"): the spell ends as a permanent
            // of theirs on your side, where Smirking Spelljacker and Gale's
            // Redirection only cast it, a card of theirs played.
            Rx(@"counter target spell[\s\S]{0,180}onto the battlefield (?:tapped )?under your control"),
            // A permanent exiled and then CAST by you is taken twice over:
            // "Exile target nonland permanent. You may cast that card for as
            // long as it remains exiled" (Abstruse Appropriation, 2026-10-16).
            // A permanent CARD is your own graveyard's (Savvy Trader).
            Rx(@"exile target (?:nonland )?permanent\b(?! cards?\b)[^.\n]{0,30}\. you may (?:cast|play) that card\b"),
        ];

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
        /// "An opponent OF YOUR CHOICE gains control of it" is the same subject,
        /// read 2026-11-08 (Khârn the Betrayer hands itself over and took
        /// nothing: CardAdvantage alone, its Steal handed back).
        /// </para></summary>
        private static readonly Regex GivesControlAway = Rx(
            @"(?:target opponent|an opponent(?: of your choice)?|each opponent|that opponent|another player|target player"
            + @"|that player|other players|they|the player with the most life"
            + @"|that permanent's controller|the attacking player|the player to your right)"
            + @" (?:may |can't )?gains? control");

        /// <summary>A token that steals is the token's, not the card's: Wake the
        /// Dragon makes a Dragon whose combat damage takes an artifact, and was
        /// tagged Tokens alone. The same reading as a token's ping for Removal and
        /// Burn.</summary>
        private static readonly Regex TokenSteals = Rx(
            @"\btoken with [\w ,]{0,40}""[^""]*gains? control of[^""]*""");

        /// <summary>The player who reveals their top KEEPS part of it: the
        /// revealed permanents onto their own battlefield (Over the Top, Guild
        /// Feud, Pyxis of Pandemonium). Ruled 2026-11-10 Q3 "Fuori" for Mill. A
        /// card put "under your control" is yours, not theirs (Curse of
        /// Unbinding, Telemin Performance). The other half of Q3, the named card
        /// into their hand (Petra Sphinx, Vexing Arcanix), needs no branch here:
        /// "Target player chooses a card name, then reveals" puts the player too
        /// far from the verb for the Mill rule to read it. Tried as a branch on
        /// 2026-11-10 and removed: no card in the store moved without it.</summary>
        private const string KeepsPartOfTheReveal =
            @"\b(?:may )?puts? (?:a|an|all|any number of) [\w ]{0,40}?(?:(?:from )?among them|revealed this way) onto the battlefield(?! under your control)";

        private static readonly Regex[] MillPatterns =
        [
            // The run in between may not say "YOU": Sivriss, Nightmare Speaker's
            // "for each opponent, you mill a card, then return that card from your
            // graveyard" fills your own graveyard, which is not this tag
            // (Mill.md: "a bare 'mill three cards' fills your own graveyard"; hand
            // CardAdvantage, Burn, Sacrifice and Regrowth, no Mill). Counted broad,
            // "<a player>, ... you mill": 1 reviewed card, untagged, and no
            // unreviewed one; the 25 reviewed cards that say "you mill" at all carry
            // no Mill.
            Rx(OtherPlayer + @"(?:(?!\byou\b)[\w ,]){0,40}mills?\b"),
            // The BOTTOM of their library is the same attack as the top, ruled
            // 2026-11-10 ("Cellar Door è in effetti macina": "Target player puts
            // the bottom card of their library into their graveyard", the one
            // card that says it, unreviewed; 0 reviewed).
            Rx(@"put(?:s)? the (?:top|bottom) [\w ]{0,25}" + OtherPlayer + @"[\w' ]{0,20}library into"),
            // Same sentence with the victim in front of the verb, which is how
            // Millstone and most pre-2021 mill is actually written.
            Rx(OtherPlayer + @"[\w ,]{0,30}puts? the (?:top|bottom) [\w ]{0,30}library into"),
            // The top of their library EXILED is the same attack, read
            // 2026-10-15 from the three reviewed cards that say it and nothing
            // more (Ulamog, the Defiler; Ashiok, Wicked Manipulator's -7;
            // Malboro). Not when a card is PLAYED, cast or put under your
            // control anywhere on that line or the next — that is CardAdvantage
            // at most, and the twelve reviewed cards with this subject that say
            // so carry no Mill (Outrageous Robbery, Bloodsoaked Insight,
            // Crabomination, Gríma, Ian Malcolm; Ensnared by the Mara, whose
            // OTHER villainous branch casts; measured 2026-11-10) — unless it is
            // the card itself cast face down (Raven Guild Master's megamorph
            // reminder).
            // EACH PLAYER exiling counts as each player milling does, by the
            // definition of 2026-11-10 ("each player" 13 of 13 for "mills"): the
            // five reviewed cards that say it (Memory Vessel, Triple Triad,
            // Rocco, Etali, Plargg and Nassari) all play the cards, so the veto
            // keeps them out, as it did when "each player" was left off for them.
            // A TOKEN's quoted ability counts, ruled 2026-11-10 Q2 "Conta, come
            // Burn" (Ashiok, Nightmare Muse, the one card, unreviewed): the
            // quotes are no longer skipped.
            // Nor when the players keep part of what they exiled, Q3 below
            // (Pyxis of Pandemonium's permanents, unreviewed).
            Rx(@"^(?![^\n]*(?:\n[^\n]*)?(?:\b(?:(?:may|can) (?:cast|play)(?! this card\b)|(?:cast|play)s? (?:it|them|that card|those cards)|under your control)\b|"
                + KeepsPartOfTheReveal + @"))"
                + @"[^\n]*?\b(?:target (?:player|opponent)|each opponent|an opponent|that player|defending player|each player|its controller)"
                // "Exiles CARDS FROM the top of their library until they exile a
                // land card" is the same attack in the other order (Umbris, Fear
                // Manifest, Mill by hand, read 2026-11-06). Counted broad,
                // "<a player> ... exiles cards from the top of their library": 1
                // reviewed card tagged and 5 untagged, and all 5 carry the cast
                // above (Bismuth Mindrender, Gríma, Wand of Wonder, Strago and
                // Relm: Steal then, CardAdvantage since 2026-11-08; Transforming
                // Flourish: "they may cast"), so the
                // veto already keeps them out; 11 unreviewed, 1 of them proposed.
                + @"[\w ,]{0,40}exiles? (?:the top [\w ]{0,30}|cards from the top )of their library",
                RegexOptions.Multiline),
            // ANOTHER player's top REVEALED or LOOKED AT, and cards from it put
            // into THEIR graveyard: the definition of 2026-11-10 read on the
            // reveal and look-at wordings, which no rule above can read.
            // - Reveal until, the card taken by YOU and the rest into their
            //   graveyard: ruled 2026-11-06, "Sì, entrambe Mill" (Curse of
            //   Unbinding; Sméagol, Helpful Guide), the two reviewed cards.
            // - Reveal until, ALL the revealed cards into their graveyard (Mind
            //   Funeral, Mind Grind, Balustrade Spy, Mirko Vosk, Trepanation
            //   Blade, Destroy the Evidence's "its controller"), or all but the
            //   card you take (Telemin Performance's "all noncreature cards"):
            //   read 2026-11-10 with the definition, 0 reviewed cards say it.
            // - A top LOOKED AT or revealed and the cards for the graveyard
            //   CHOSEN by you: ruled 2026-11-10 Q1 "Mill" (Cruel Fate, Eye Spy,
            //   Wand of Denial named; Wu Spy, Dimir Charm, Thief of Sanity,
            //   Bamboozle, Balshan Beguiler follow, unreviewed). A card chosen out
            //   of their top is still a card they lose, as Ray of Erasure's blind
            //   one is.
            // Not when the player KEEPS part of the reveal, into their hand or
            // onto their battlefield: ruled 2026-11-10 Q3 "Fuori", 0 of 3
            // reviewed (Over the Top, Petra Sphinx, Vexing Arcanix; Guild Feud,
            // unreviewed, follows): the card hands them something, so it is no
            // attack on their library. The card taken "under your control" is
            // not theirs (Curse of Unbinding). And not your own library: Oath of
            // Druids' first player and Mirror-Mad Phantasm's "that library" are
            // not reached, nor Hermit Druid's "your graveyard".
            Rx(@"^(?![^\n]*" + KeepsPartOfTheReveal + @")[^\n]*?"
                + @"(?:\b(?:target player|target opponent|each opponent|an opponent|that player|defending player|each player|its controller)"
                + @"[^.\n]{0,20}\breveals?|\blook at) [\w ]{0,30}?\btop\b[\w ]{0,30}?\bof (?:their|(?:target|each|that) (?:player|opponent)'s) library"
                + @"[^\n]*?\binto (?:their|that player's|their owners'?) graveyards?",
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
        // MYRIAD printed bare, with no reminder to say "create": a temporary
        // body is a body, ruled 2026-11-10 ("Contano tutti"), and myriad's
        // copies come on every attack (BodiesOnEveryAttack). 22 of the 25
        // reviewed myriad cards are tagged; Battle Angels of Tyr and The
        // Master, Multiplied, untagged and printing no other body, are handed
        // back, and Ironwill Forger stays out as a Lieutenant. Asked as the
        // keyword, not the word: The Everflowing Well's land face is The
        // Myriad Pools.
        private static readonly Regex MakesABodyByKeyword = Rx(
            @"\bcloaks? (?:the|two|three|four|\w+ of them|up to|those cards)\b|(?<!whenever you )manifest dread"
            + @"|\bliving weapon\b|\bjob select\b|(?:(?m:^)|, |\b(?:has|have|gains?) )myriad\b");

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
            + @"\. it becomes an? 0/0\b[\w ,]{0,40}creature"
            // …and the counters put on a target permanent that may not be a creature
            // yet: "Put two +1/+1 counters on target artifact, creature, or land you
            // control. Untap that permanent. If it isn't a creature, it becomes a 0/0
            // creature in addition to its other types" (Tawnos's Tinkering, Tokens +
            // Buff by hand, reviewed 11-03; ruled 2026-09-25 above). Reached with it:
            // Donatello, Mutant Mechanic, Pizza Face, Gastromancer and Does Machines,
            // unreviewed, which keep Buff and gain the body. Buff stays
            // (CountersMakeTheBody is not touched).
            + @"|\bcounters? on (?:up to one )?(?:other )?target [^.\n]{0,60}\.(?: untap [^.\n]{0,30}\.)? "
            + @"if it isn't a creature, it becomes an? 0/0\b[\w ,]{0,40}creature");

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
        // Read 2026-11-05 under the same ruling, three wordings the rule did not
        // read, each an artifact that animates ITSELF: "you may have this artifact
        // BECOME an X/X Insect artifact creature" (Ebony Fly, Tokens by hand); an X/X
        // body whose size is the X (Chimeric Staff, unreviewed); and the base
        // power and toughness AFTER the word creature, "becomes a Shapeshifter
        // artifact creature with base power and toughness 3/3" (Mimic, Tokens by
        // hand; Dire Mimic, reviewed 11-03 without the tag, has the identical
        // wording at 5/5 and is handed back with it). NOT an X that is the number of
        // your COMMANDERS, which a cube never has (Cactus Preserve, reviewed
        // untagged, as LandsBecomeCreatures reads it).
        private static readonly Regex SelfAnimates = Rx(
            @"\bthis (?:land|artifact|enchantment|case) (?:becomes?|is) (?![^.\n]*\bcommanders?\b)an? [^.\n]{0,40}"
            + @"(?:\d+|x)/(?:\d+|x)[^.\n]{0,60}creature"
            + @"|\bthis (?:land|artifact|enchantment) isn't a creature, it becomes an? \d+/\d+"
            + @"|\bthis (?:land|artifact|enchantment|case) becomes an? [^.\n]{0,60}creature with base power and toughness \d+/\d+");

        // An ARTIFACT animated by the card is a body like the land, ruled
        // 2026-10-15 ("Tokens, come le terre"): the hand tags were split 6 to
        // 7 on identical wordings — Majestic Metamorphosis tagged, Relic's
        // Roar not; Katsumasa tagged, Tough Cookie not. Target or enchanted,
        // until end of turn or for good. A creature that does it once as it
        // enters is still taken back by the few-bodies rule (Unctus's
        // Retrofitter), which counts it in OneBodyByKeyword.
        // Any TOKEN of yours made a creature is the same body, ruled 2026-10-27
        // on Halsin, Emerald Archdruid's 4/4 Bear ("Tokens, niente Buff"): a
        // Treasure or a Clue is an artifact by another name. Not a creature
        // token, which is a body already. "Target NONTOKEN artifact" is the
        // same artifact, read 2026-11-10 (The Blackstaff of Waterdeep, tagged).
        private static readonly Regex AnimatesAnArtifact = Rx(
            @"\b(?:target|enchanted) (?:non(?:creature|token) )?(?:artifacts?(?: token| or creature)?|(?:(?!creature\b)\w+ )?tokens?)"
            + @"(?: you control)? (?:becomes?|is|are) (?!a copy\b)(?:an? )?[\w ,/]{0,40}?\bcreatures?\b");

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
        // the Shifting Visage, Jackal, The Clone Saga). Never says "create", so
        // the token wording above cannot see it. Copying any OTHER spell of
        // yours is Redirect. A PERMANENT spell copied becomes a token too
        // (read 2026-10-13). "You may copy it" is the same copy, read
        // 2026-11-10 (Donal, Herald of Wings).
        // A TRIBE's spell copied is the 2026-10-27 tribal rule, ruled
        // 2026-11-10 ("Fuori, come 10-27"): the card does not make the body by
        // itself. Thurid, Archmage of Echoes and Double Down untagged by name,
        // Tawnos, the Toymaker already was; so the rule is asked of the text
        // BlankTribalBodyTriggers leaves, and "outlaw" is no longer read here.
        // Asked without reminder text too: demonstrate's reminder says "you
        // may copy it" and hands an opponent a copy (Silverquill Lecturer,
        // untagged; Redirect's demonstrate ruling of 2026-11-09).
        private static readonly Regex CopiesYourCreatureSpell = Rx(
            @"(?:whenever|when) you (?:next )?cast [^.\n]{0,60}\b(?:creature|permanent) spells?\b[^.\n]{0,60}, "
            + @"(?:you may )?copy (?:it|that spell)\b");

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
                // opponent, create a token copy" stays one here: its two copies
                // never reach three alone (2026-11-10), and what they make again
                // is counted in OnlyAFewBodiesOnce.
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

        // The reminder of DECAYED, "(It can't block. When it attacks, sacrifice it at
        // end of combat.)", is the token's own life and no repeat of the card's
        // ability: left in, its "at end of combat" read a creature making ONE decayed
        // Zombie as an engine. Read 2026-11-05 under the 2026-09-24 rule (a creature
        // card is Tokens for three or more bodies or an effect that repeats): Falcon
        // Abomination, reviewed untagged. Family: 2 cards make one decayed token once,
        // Falcon Abomination and Hobbling Zombie (unreviewed, loses the tag); the
        // decayed makers on a repeating trigger (Poppet Stitcher, Jadar) keep it.
        private static readonly Regex DecayedReminder = Rx(
            @"\(it can't block\. when it attacks, sacrifice it at end of combat\.\)");

        private static bool BodyAbilityRepeats(string ability)
        {
            string asked = DecayedReminder.Replace(DelayedOnce.Replace(ability, " "), " ");

            return (AbilityRepeats(asked) || BodyEveryCombat.IsMatch(asked))
                && !BodyCostSpendsItself.IsMatch(asked);
        }

        private static bool FrontFaceIsACreature(Card card) =>
            (card.TypeLine ?? string.Empty).Split("//")[0]
                .Contains("Creature", StringComparison.OrdinalIgnoreCase);

        // The SPELL face of an Adventure or an Omen is cast on its own, and its
        // bodies count as a sorcery's do, ruled 2026-10-30 ("l'avventura conta
        // a sé") on Animating Faerie's Bring to Life, Tokens by hand, against
        // Ratcatcher Trainee's Pest Problem, Riling Dawnbreaker's Signaling
        // Roar and Hildibrand Manderville's Zombie, untagged before the ruling
        // and handed back. The face is the type line's second, and its text the
        // last line: the faces are joined in order, and Bring to Life prints no
        // reminder to find it by.
        // Any INSTANT or SORCERY face is cast on its own the same way, read
        // 2026-11-10: a modal double-faced card's back (Extus, Oriq Overlord's
        // Awaken the Blood Avatar, Tokens by hand) and a prepared creature's
        // spell. An Adventure or an Omen is one of them.
        private static bool SpellFaceMakesABody(Card card, string text)
        {
            string[] faces = (card.TypeLine ?? string.Empty).Split(" // ");
            if (faces.Length < 2 || (!faces[1].Contains("Instant", StringComparison.Ordinal)
                && !faces[1].Contains("Sorcery", StringComparison.Ordinal)))
            {
                return false;
            }

            string face = text[(text.LastIndexOf('\n') + 1)..];
            return BodiesMadeBy(Parenthetical.Replace(Quoted.Replace(face, " "), " ")) is not null;
        }

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

            if (BodiesOnEveryAttack.IsMatch(own) || DashRepeatsTheEntry.IsMatch(own) || GrantsEncore.IsMatch(own))
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
        // A LIST of tribes ends its first name with a comma: "a Pegasus,
        // Unicorn, or Horse creature spell" (Thurid), read 2026-11-10.
        private const string NotACreatureType =
            @"Desert|Forest|Plains|Island|Swamp|Mountain|Gate|Cave|Shrine|Sphere|Town|Lair|Locus|Aura|Equipment"
            + @"|Vehicle|Saga|Treasure|Clue|Food|Blood|Map|Powerstone|Arcane|Lesson|Adventure";

        private static readonly Regex BodyOnATribe = Rx(
            @"^(?:[^—\n]{1,40}— )?whenever (?:(?<another>another|a|an|one or more(?: other)?) (?<nontoken>nontoken )?"
            + @"|(?<cast>you cast (?:a|an|another)) )"
            + @"(?!(?-i:" + NotACreatureType + @")\b)(?<tribe>(?-i:[A-Z])[a-z]+?)(?:s|es)?[ ,][^\n]*",
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
        // death is "1 + 2 token", tagged by name. Unearth returns the card once
        // and exiles it, so only its entry counts: Royal Warden's two Necrons
        // are four. Warp casts it again once too, and no warp card makes two as
        // it enters (Knight Luminary makes one, untagged).
        // The copies ARE bodies, ruled 2026-11-10 ("Contano tutti": a temporary
        // body attacks and can be sacrificed), which overturns "le copie non
        // contano" of 2026-10-27. A card's own encore makes two, under three
        // alone, so no card of its own moves; ENCORE GRANTED to every card of a
        // kind in your graveyard repeats (GrantsEncore): Graywater's Fixer,
        // Wire Surgeons and Sliver Gravemother tagged by name.
        private static readonly Regex EncoreKeyword = Rx(@"^encore\b", RegexOptions.Multiline);

        private static readonly Regex GrantsEncore = Rx(@"\bin your graveyard ha(?:s|ve) encore\b");

        private static readonly Regex UnearthKeyword = Rx(@"^unearth\b", RegexOptions.Multiline);

        // The same ruling for the other two tags, 2026-11-05 (Scrapwork Rager
        // against First-Sphere Gargantua, identical text and one tagged;
        // Simian Simulacrum, Kinsbaile Courier): UNEARTH and ENCORE SUM the
        // entries of the card for CardAdvantage and Buff as they do for
        // Tokens. The card enters once, +1 for unearth, +2 for encore (a copy
        // for each of two opponents, "encore vale doppio"); each copy is one
        // more entry of the card. Asked of the text WITHOUT its reminder, which
        // names both keywords again.
        private static int EntriesOf(string text)
        {
            string own = Parenthetical.Replace(text, " ");
            return 1 + (UnearthKeyword.IsMatch(own) ? 1 : 0) + (EncoreKeyword.IsMatch(own) ? 2 : 0);
        }

        // An ability word may come first: "Phalanx Commander — When this
        // creature enters" (Royal Warden).
        private static readonly Regex EntersOrDies = Rx(@"^(?:[^—\n]{1,40}— )?when [^,\n]{1,60}\b(?<event>enters|dies)\b");

        // Diabolic Edict and Flare of Malice make the OTHER player sacrifice,
        // which empties their board rather than giving you a place to put yours.
        private static readonly Regex SomebodyElseSacrifices = Rx(OtherPlayer + @"[\w ,]{0,40}sacrifices?\b");

        // …but not the opponent's reply to YOUR sacrifice: Braids, Arisen
        // Nightmare's "at the beginning of your end step, you may sacrifice an
        // artifact, creature, enchantment, land, or planeswalker. If you do, each
        // opponent may sacrifice a permanent of their choice that shares a card
        // type with it" is an outlet for your own permanents with a punisher
        // riding on it, and empties nobody's board (Sacrifice by hand, 2026-10-23;
        // read 2026-11-06 under "NOT an edict, which empties somebody else's
        // board"). Blanked before SomebodyElseSacrifices is asked, so a card that
        // also makes an opponent sacrifice outright still is an edict. Counted
        // broad, "if you do, <an opponent> may sacrifice": 1 reviewed card,
        // tagged, none unreviewed; the 4 other reviewed cards where "each
        // opponent / target player may sacrifice" appears (Fandaniel, Osseous
        // Sticktwister, Rakdos, Braids's Frightful Return) do not say "if you do"
        // and are untagged, so they stay vetoed.
        private static readonly Regex OpponentSacrificesInReturn = Rx(
            @"\bif you do, " + OtherPlayer + @"[\w ,]{0,40}sacrifices?\b[^.\n]*");

        // Destroying or exiling your OWN permanent at will is an outlet by
        // another verb, ruled 2026-09-25: Despotic Scepter's "{T}: Destroy target
        // permanent you own", Rats of Rath's "… you control", City of Shadows'
        // "{T}, Exile a creature you control:" — all three hand-tagged, none read.
        // Not an Aura ATTACHED to your creature (Miracle Worker saves it), and not
        // the price of a HARNESS, which is paid once (The Soul Stone).
        private const string SacrificesYourOwnByAnotherVerb =
            @"^[^\n:]{0,40}:\s*destroy target (?![\w ]*attached)[\w ,]{0,40}(?:you control|you own)"
            + @"|^[^\n:]{0,40}exile (?:a|another) (?:creature|artifact)[\w ]{0,20}you control[^\n:]{0,20}:(?!\s*harness)";

        // One of YOUR creatures that has just dealt combat damage, every time
        // it happens (Descendants' Fury, Sacrifice by hand, read 2026-10-30;
        // the one card that says it).
        private const string SacrificesOneOfThem = @"\bcreatures you control [^.\n]{0,60}, you may sacrifice one of them\b";

        // Exploit, casualty, devour and bargain sacrifice ONCE, as the card is
        // cast or enters — and their reminder text is all the classifier sees
        // of them, which is blanked. HANDED ON, the same keyword comes round
        // again: Colonel Autumn gives exploit to every other legendary creature,
        // Anhelo gives casualty to the first spell of each turn, Dragon
        // Broodmother's upkeep token arrives with devour.
        private const string SacrificeKeywordGranted = @"\b(?:have|has|gains?|with) (?:exploit|casualty|devour|bargain)\b";

        private const string WhatAnOutletEats =
            @"sacrifices? (?:a|an|another|two|three|\d+|x|any number of|one or more|up to \w+) [\w ]*(?:creature|artifact|permanent)";

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
            + @"|" + SacrificesYourOwnByAnotherVerb
            + @"|" + SacrificesOneOfThem,
            RegexOptions.Multiline);

        private static readonly Regex Buyback = Rx(@"^buyback\b", RegexOptions.Multiline);

        private static readonly Regex SacrificeAsAdditionalCost = Rx(@"as an additional cost to cast this spell,[^\n]{0,40}sacrifice");

        // A cost in front of a colon is paid ONCE when its effect turns the card
        // into something else: Ecstatic Awakener's "{2}{B}, Sacrifice another
        // creature: Draw a card, then transform this creature" is gone after the
        // first time (untagged by hand 2026-11-06; read 2026-11-10 under the
        // 09-25 "una tantum"). Pious Evangel ("Sacrifice another permanent:
        // Transform this creature", unreviewed) is the other card. Not Hostile
        // Hostel's "Then if there are three or more soul counters on it, …
        // transform it": three sacrifices first, so it repeats (a guard for
        // "if" was tried and moved no card, so it is not here). Counted broad
        // 2026-11-10, a cost whose effect transforms, sacrifices, exiles or
        // returns the card itself: 0 reviewed tagged, 1 untagged (Ecstatic
        // Awakener); the sacrifice/exile/return shapes matched no such card.
        // The transform is TransformsItselfAway, the same reading CardAdvantage
        // asks of a draw (2026-09-22), asked here of each sacrifice-cost line.
        private static readonly Regex SacrificeCostLine = Rx(@"^[^\n:]*sacrifices? [^\n:]*:[^\n]*", RegexOptions.Multiline);

        /// <summary>Can you feed your own creatures or artifacts to this card
        /// again and again? See <see cref="SacrificeOutlet"/>.</summary>
        private static bool IsSacrificeOutlet(string text)
        {
            string own = SacrificeCostLine.Replace(
                Parenthetical.Replace(text, " "),
                line => TransformsItselfAway.IsMatch(line.Value) ? " " : line.Value);
            return SacrificeOutlet.IsMatch(own)
                || (Buyback.IsMatch(own) && SacrificeAsAdditionalCost.IsMatch(own));
        }
    }
}
