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
            Rx(@"counter target spell[\s\S]{0,180}onto the battlefield under your control"),
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
        private static readonly Regex OntoYourSideOfTheBoard = Rx(
            @"onto the battlefield under your control");

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
    }
}
