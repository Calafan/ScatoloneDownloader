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
            Rx(@"(?:destroy|exile) (?:up to \w+ |another |x |two |three |four |\d+ )?target " + DisenchantNoun),
            Rx(@"(?:destroy|exile) (?:all|each) " + DisenchantNoun),
            // An edict aimed at an artifact answers one all the same, and unlike
            // the creature edict it cannot be blanked by a token: Pick Your Poison
            // and Gaius van Baelsar both read "each opponent sacrifices an
            // artifact of their choice" and the human tags both here.
            Rx(OtherPlayer + @" sacrifices?[\w ]{0,20}" + DisenchantNoun),
            // Ultimate Magic: Meteor names no target at all — "for each opponent,
            // choose an artifact or land that player controls. Destroy the chosen
            // permanents." The same clause is LandDestruction's last miss.
            Rx(@"choose an? [\w ,'\-]{0,30}\bartifacts?\b[^\n]{0,40}destroy the chosen"),
        ];

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
            Rx(@"search (?:your|their) library[\w /]{0,25}for (?:up to \w+ |two |three |four |five |\d+ )?"
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

        private static readonly Regex SearchesForSomethingElseToo = Rx(
            @"\b(?:creature|artifact|enchantment|instant|sorcery|planeswalker|equipment|battle"
            + @"|legendary|nonland permanent) cards?\b");

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
            Rx(@"(?:destroy|exile)[^\n]{0,80}target [\w ,'\-]{0,30}\blands?\b"),
            Rx(@"(?:destroy|exile) (?:all|each)[\w ,'\-]{0,30}\blands?\b"),
            Rx(@"(?:destroy|exile) (?:up to \w+ |x |two |three |four |\d+ )?(?:all|each|target) "
                + @"[\w ,'\-]{0,20}" + BasicLandType),
            // "Each player chooses six lands they control, then sacrifices the
            // rest" (Planetary Annihilation) names no victim at all.
            Rx(@"chooses? [\w ]{0,15}\blands?\b[\w ,'\-]{0,30}sacrifices? the rest"),
            // "FOR EACH LAND, destroy that land unless any player pays 1 life"
            // (Cleansing) names its victim with a pronoun. Anchored on the "each
            // land" in front of it, because Erosion says "destroy that land" of
            // the single land it enchants and the human does not tag it.
            Rx(@"for each land[\w ,'\-]{0,20}destroy that land"),
            // A land edict, with the clause that picks the victim in between:
            // "each player WHO TAPPED A LAND FOR MANA THIS TURN sacrifices a land
            // of their choice" (Desolation). Nature's Wrath names a basic type
            // where Desolation says "land".
            Rx(@"(?:target (?:player|opponent)|each player|that player|each opponent)"
                + @"[\w ,'\-]{0,60}sacrifices? (?:[\w-]+ ){0,3}(?:lands?\b|" + BasicLandType + @")"),
            // Fallow Earth answers a land without destroying it — but the
            // destination has to be a library or a graveyard, because Tato Farmer
            // puts one ONTO THE BATTLEFIELD with almost the same words.
            Rx(@"put target land[\w ,'\-]{0,40}(?:on top of|into)[\w ,'\-]{0,25}(?:library|graveyard)"),
            // Ultimate Magic: Meteor names no target at all — "for each opponent,
            // choose an artifact or land that player controls. Destroy the chosen
            // permanents." The same clause is the last Disenchant miss.
            Rx(@"choose an? [\w ,'\-]{0,30}\blands?\b[^\n]{0,40}destroy the chosen"),
        ];

        /// <summary>A land card in a GRAVEYARD is fuel, not a target: Steward of
        /// the Harvest exiles three of them out of yours.</summary>
        private static readonly Regex LandAlreadyInAGraveyard = Rx(
            @"land cards? (?:from|in)[\w ,'\-]{0,25}graveyard");

        /// <summary>"Destroy target Aura attached to a land" PROTECTS the land —
        /// Savaen Elves and Pyramids, the same pair that fooled Disenchant.</summary>
        private static readonly Regex AuraOnALand = Rx(@"aura attached to");

        /// <summary>Rats of Rath destroys "target artifact, creature, or land YOU
        /// CONTROL", which denies nobody anything.</summary>
        private static readonly Regex DestroysYourOwnLand = Rx(
            @"(?:destroy|exile)[^\n]{0,60}target[\w ,'\-]{0,40}you control");

        /// <summary>Natural Balance takes the lands off whoever has six and hands
        /// basics to whoever has four, which is a rebalance rather than denial.
        /// <para>
        /// It only excuses a line that DESTROYS nothing. Sandworm, Price of
        /// Freedom and Magmatic Hellkite all kill a land and offer a basic back as
        /// consolation, and the human tags all three; what makes Natural Balance
        /// different is that the only thing done to the lands is their own
        /// controller sacrificing them.
        /// </para></summary>
        private static readonly Regex HandsTheLandsBack = Rx(
            @"search[\w ']{0,20}library for[\w ,'\-]{0,25}basic land");

        private static readonly Regex AnyDestroyOrExile = Rx(@"\b(?:destroy|exile)s?\b");

        // Hoisted for the same reason as MillPatterns: the donation guard below
        // strips the clause that gives a permanent AWAY and then re-runs these,
        // so a card that steals and donates in the same text keeps the tag.
        private static readonly Regex[] StealPatterns =
        [
            Rx(@"gains? control of"),
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
            // Ruled 2026-09-19.
            Rx(@"attach (?:target|enchanted) aura"),
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
            + @"|(?:library|hand|graveyard) of (?:target |an |each |that )?(?:opponent|player)"
            // A mill IS the zone, and the only way Locke could ever be found: "each
            // player mills a card … you may cast a spell from among those cards"
            // never names a library at all.
            + @"|(?:each|target|an|another|that) (?:player|opponent)[\w ,'\-]{0,20}mills?\b");

        /// <summary>Taking a CARD rather than a permanent — the other half of this
        /// tag, and 15 of the 28 cards it used to miss. It reads as two halves
        /// because either half alone is something else entirely: the zone alone is
        /// Mill, and "you may play" alone is your own impulse draw.</summary>
        private static readonly Regex SomebodyElsesZone = Rx(
            @"(?:target opponent|an opponent|each opponent|that opponent|target player|that player"
            + @"|defending player|another player|each player|opponent'?s|player'?s)"
            + @"[\w ,'\-]{0,60}(?:library|hand|graveyard)"
            + @"|(?:each|target|an|another|that) (?:player|opponent)[\w ,'\-]{0,20}mills?\b"
            + @"|(?:library|hand|graveyard) of (?:target |an |each |that )?(?:opponent|player)"
            + @"|\btheir (?:library|hand|graveyard)\b");

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
        private static readonly Regex PlaysACardYouOwn = Rx(
            @"(?:cards?|spells?) you own"
            + @"|(?:play|cast)[\w ]{0,20}from your (?:graveyard|hand|library)");

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
            + @"|that permanent's controller) (?:may |can't )?gains? control");

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
        private static readonly Regex CreatureTokenWording = Rx(@"creature token");

        private static readonly Regex NamedTokenIsACreature = Rx(
            @"create[s]? (?:a|an|two|three|four|five|x|\d+)[\w ,'\-]{0,30} tokens?\. "
            + @"\(it'?s? [\w ,'\-{}/]{0,40}\d+/\d+[\w ,'\-]{0,30}creature");

        private static readonly Regex AnyTokenCopy = Rx(
            @"token that's a copy|token cop(?:y|ies)|tokens that are copies");

        private static readonly Regex NoncreatureBeingCopied = Rx(
            @"\b(?:artifact|equipment|enchantment|land|vehicle|clue|treasure|food)\b");

        // Four keywords that put a BODY on the board and never say "token" in a
        // shape the rules above can read. Ruled 2026-09-19. Cloak and manifest
        // dread turn a card face down as a 2/2; living weapon and job select
        // hand the Equipment a Germ or a Hero to carry it. 29 reviewed cards
        // carry one of these and 24 were already tagged by hand; plain MANIFEST
        // is deliberately not among them, being one card and untagged.
        private static readonly Regex MakesABodyByKeyword = Rx(
            @"\bcloaks? (?:the|two|three|four|\w+ of them|up to)|manifest dread"
            + @"|\bliving weapon\b|\bjob select\b");

        // A NONCREATURE permanent turned into a 0/0 creature and sized with
        // counters is a body made, the same shape as earthbend: Case of the
        // Filched Falcon "put four +1/+1 counters on target noncreature
        // artifact. It becomes a 0/0 Bird creature". Ruled Tokens 2026-09-25.
        private static readonly Regex AnimatesANoncreature = Rx(
            @"counters? on target noncreature [\w ]{0,20}\. it becomes an? 0/0\b[\w ,]{0,40}creature");

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
            @"\bearthbends?\b|\bmanifest dread\b|\bendures? (?:\d+|x)\b|\bcloaks? (?:the|a)\b");

        private static readonly Regex CloaksSeveral = Rx(@"\bcloaks? (?:two|three|four|\w+ of them)\b");

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
                    || AnyTokenCopy.IsMatch(rest);

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
                // opponent, create a token copy" is one body at a two-player
                // table, and stays one.
                int sentenceStart = Math.Max(
                    ability.LastIndexOfAny(['.', '\n', '•'], Math.Max(m.Index - 1, 0)) + 1, 0);
                if (CountComesFirst.IsMatch(ability[sentenceStart..m.Index]))
                {
                    n = int.MaxValue;
                }

                // A LIST of bodies in one sentence is each of them: Somberwald
                // Beastmaster makes "a 2/2 green Wolf creature token, a 3/3 green
                // Beast creature token, and a 4/4 green Beast creature token".
                n = Math.Max(n, CreatureTokenWording.Matches(rest).Count);

                bodies = Math.Max(bodies ?? 0, n);
            }

            if (CreatesANamedBody.IsMatch(ability))
            {
                bodies = Math.Max(bodies ?? 0, 1);
            }

            // Counted OUTSIDE the brackets: earthbend's reminder text names the
            // keyword again ("(To earthbend 1, target land …)"), which made Dai
            // Li Agents' two earthbends three.
            int keywordBodies = OneBodyByKeyword.Matches(Parenthetical.Replace(ability, " ")).Count;
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
            string own = Quoted.Replace(text, " ");

            if (BodiesOnEveryAttack.IsMatch(own))
            {
                return false;
            }

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
            }

            return makesAny;
        }

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
