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
    internal static partial class EffectClassifier
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

        /// <summary>Every way the game names a FACE as the thing damage lands
        /// on. "Any target" is in it because a card that may point at a player
        /// is Burn as well as Removal, which is the ruling Burn has carried
        /// since 2026-09-15.</summary>
        private const string BurnTarget =
            @"(?:any (?:other )?target|target player|target opponent|each player|each opponent"
            + @"|each other (?:player|opponent)|that player|defending player|the player|them"
            + @"|each of [\dX]+ targets|the controller of|its controller)";

        /// <summary>The players a life-loss clause can name, with room for the
        /// relative clause the modern templating puts between the subject and
        /// the verb — "each opponent WHO DOESN'T loses 2 life" (Fandaniel),
        /// "each opponent sacrifices a creature of their choice AND loses 3
        /// life" (Summon: Anima), "each player WHO OWNS A SPELL YOU CAST THIS
        /// WAY loses life equal to its mana value" (Kefka).</summary>
        private const string LosesLifeSubject =
            @"(?:each opponent|target opponent|target player|that player|that opponent|each player"
            + @"|its controller|they|defending player|the chosen player|that spell's controller)"
            + @"[\w ,'-]{0,45}\b";

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

        // A triggered ability's condition, up to the comma that ends it. What
        // the trigger WATCHES is never what the effect lands on, however much
        // it reads like a beneficiary. See AimsOnlyAtItself for why this is
        // stripped for one of that method's two questions and not the other.
        private static readonly Regex TriggerClause = Rx(
            @"(?:whenever|when|at the beginning of)\b[^,\n]{0,160},");

        private static readonly Regex Beneficiary = Rx(
            @"\b(?:target|another|other|each|all|enchanted|equipped|chosen|"
            // "that creature" is the beneficiary a second sentence refers back to:
            // "Gain control of target creature ... that creature gets +2/+0".
            // …and the TYPE the card just named, which the four nouns above do
            // not cover: Reckless Velocitaur pumps "that Mount or Vehicle", and
            // its own trigger says "this creature", so without this the trigger
            // vouches for an effect that lands somewhere else. Added 2026-09-21.
            + @"that (?:creature|permanent|player|token|card|mount|vehicle|equipment|aura|land"
            + @"|artifact|enchantment|planeswalker|\w+ or \w+)|"
            // "attacking" has to MODIFY the beneficiary ("attacking red creatures
            // get +2/+0") — bare, it is just as often the card's own state, as in
            // "As long as this creature is attacking, it gets +2/+0".
            + @"(?:attacking|blocking) (?:[\w-]+ ){0,3}(?:creatures?|permanents?|tokens?)|"
            + @"(?!(?:as|if|unless|while|when|whenever|though|although|because|that|and|or|but|long)\b)\w+ you control)\b");

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
                // Entering tapped is deliberately NOT asked here, though it is
                // asked of the sacrifice-for-mana rule below. Tried on
                // 2026-09-18 to catch Teferi's Isle and reverted: it took the
                // five Karoo lands with it, and those give a land back, so they
                // tap for two net every turn after the first. One card gained,
                // five lost.
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

            // The eight families ruled 2026-09-18, all after the land strip for
            // the same reason. Each is documented on its own pattern above.
            if (CostsLessToCast.IsMatch(CostsLessForItself.Replace(text, " "))
                || (card.MacroType != MacroType.Land && ActivatedManaAbility.IsMatch(text))
                || ExtraLandDrop.IsMatch(text)
                || ManaMultiplier.IsMatch(text)
                || LandFromHandToPlay.IsMatch(text)
                || UntapsLands.IsMatch(text)
                || (card.MacroType != MacroType.Land
                    && LandOntoTheBattlefield.IsMatch(text)
                    && !LandForSomebodyElse.IsMatch(text) && !PaysALandForTheLand.IsMatch(text))
                || TreasureAlsoRamps(text))
            {
                result |= CardEffect.Ramp;
            }

            // …and mana that costs mana adds nothing. Last, so it can withdraw
            // the tag whichever rule above granted it, and asked of the whole
            // card so that one free ability elsewhere keeps it.
            if (result.HasFlag(CardEffect.Ramp) && EveryManaAbilityIsPaidAndPoor(text))
            {
                result &= ~CardEffect.Ramp;
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
                && (!MakesACreatureBody(text) || SomebodyElseCreates.IsMatch(text)))
            {
                result &= ~CardEffect.Tokens;
            }

            // Added after the guard, not inside TokenPatterns, because these
            // wordings never say "token" at all and so have nothing for the
            // creature-token check to read. Earthbend and the animated land are
            // here by the 2026-09-15 ruling, reaffirmed 2026-09-19 after the
            // hand tags were found split 21 to 14 on the identical wording —
            // none of the tagged ones makes a real token, so the split was an
            // inconsistency rather than a distinction, and the ruling settles it.
            if (Earthbend.IsMatch(text) || LandsBecomeCreatures.IsMatch(text)
                || MakesABodyByKeyword.IsMatch(text))
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

            // Last, so it can take the tag back off whatever granted it: a card
            // that can only answer what is already blocking it. See the pattern
            // above; nothing else on these cards points outward.
            if (result.HasFlag(CardEffect.Removal) && OnlyWhatIsInCombatWithIt.IsMatch(text))
            {
                result &= ~CardEffect.Removal;
            }

            // …and the same question asked of whose creature it is. Every aimed
            // clause on the card is read, and the tag only comes off when ALL of
            // them point at your own creature or at a graveyard — a card that
            // kills one of theirs and blinks one of yours keeps it.
            if (result.HasFlag(CardEffect.Removal) && OnlyAnswersNobody(text))
            {
                result &= ~CardEffect.Removal;
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

            // Prevention aimed at one of your own, asked the same way: blank the
            // prevention out and see whether anything else on the card locks
            // somebody down. See PreventionAimedAtYourOwn above.
            if (result.HasFlag(CardEffect.Pacify)
                && PreventsWhatACreatureDeals.IsMatch(text)
                && (PreventionAimedAtYourOwn.IsMatch(text) || TheyCanPayToIgnoreIt.IsMatch(text))
                && !PacifyOutward.Where(p => p != PreventsWhatACreatureDeals)
                                 .Any(p => p.IsMatch(PreventsWhatACreatureDeals.Replace(text, " ")))
                && !AnyUntapLock.IsMatch(text))
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

            // …and neither is mana you get for nothing but a tap. See
            // BareTapAdds above for the ruling and the numbers behind it.
            if (result.HasFlag(CardEffect.ManaFixing)
                && card.MacroType != MacroType.Land
                && EveryFixerIsABareTap(text))
            {
                result &= ~CardEffect.ManaFixing;
            }

            // A land out of the graveyard rebuilds a mana base; it reanimates
            // nothing. Done by BLANKING the phrase and asking whether the rule
            // still matches, so a card that returns a creature AND a land keeps
            // its tag. See LandOutOfTheGraveyard, and the note above it for the
            // self-recursion veto that was measured here and rejected.
            if (result.HasFlag(CardEffect.Reanimate) || result.HasFlag(CardEffect.Regrowth))
            {
                string aimed = LandOutOfTheGraveyard.Replace(text, " ");

                if (!ReanimatePatterns.Any(p => p.IsMatch(aimed))) { result &= ~CardEffect.Reanimate; }

                if (!RegrowthPatterns.Any(p => p.IsMatch(aimed))) { result &= ~CardEffect.Regrowth; }
            }

            // Added after that guard, and asked as a pair. See
            // ExiledCardOntoTheBattlefield for why it cannot live in the table.
            if (ExiledCardOntoTheBattlefield.IsMatch(text) && !BlinksYourOwnCreature.IsMatch(text))
            {
                result |= CardEffect.Reanimate;
            }

            // Burn, both ways round. The old punishers name a face as "that
            // permanent's controller", and a card that killed the permanent
            // first is charging for the kill rather than burning. See the two
            // patterns for the ruling and the seven cards it turns on.
            if (DamageToTheirController.IsMatch(text) && !ChargesForAKillItJustMade.IsMatch(text))
            {
                result |= CardEffect.Burn;
            }

            // The creature that is really a burn spell. See GoneAtEndOfTheTurn.
            if (card.MacroType == MacroType.Creature)
            {
                string itsOwnText = Quoted.Replace(text, " ");

                if (GoneAtEndOfTheTurn.IsMatch(itsOwnText) && Hasty.IsMatch(itsOwnText))
                {
                    result |= CardEffect.Burn;
                }
            }

            // …and what a TOKEN does is the token's. Asked by stripping and
            // re-asking, so a card that makes a pinging token AND burns on a
            // line of its own keeps the tag it earned on the second.
            if (result.HasFlag(CardEffect.Burn) && DamageFromSomethingItMade.IsMatch(text))
            {
                string itsOwnDamage = DamageFromSomethingItMade.Replace(text, " ");

                if (!BurnPatterns.Any(p => p.IsMatch(itsOwnDamage))
                    && !(DamageToTheirController.IsMatch(itsOwnDamage)
                        && !ChargesForAKillItJustMade.IsMatch(itsOwnDamage)))
                {
                    result &= ~CardEffect.Burn;
                }
            }

            // Three shapes that look like selection and are not. Asked by
            // STRIPPING and re-asking, the same way the draw guard is, so that a
            // card which cloaks on one line and surveils on another keeps the
            // tag it earned on the second. See the three patterns for the
            // rulings and the cards each was read from.
            if (result.HasFlag(CardEffect.Filter))
            {
                string selecting = TheirDiscardYourDraw.Replace(
                    LooksAtTopForALand.Replace(LooksAtTopAndMakesBodies.Replace(text, " "), " "), " ");

                if (selecting != text && !FilterPatterns.Any(p => p.IsMatch(selecting)))
                {
                    result &= ~CardEffect.Filter;
                }
            }

            // Card parity dressed as card advantage. See the three patterns above
            // for which ruling each one follows from.
            //
            // The LOOT is asked of the card with somebody else's discard blanked
            // out, because parity means ONE player trading: "each opponent
            // discards a card AND YOU DRAW a card" reads as a loot to a pattern
            // that does not check whose cards these are, and it is the same bug
            // that had Jecht, Reluctant Guardian tagged Filter. Ruled 2026-09-20.
            string oneSidedLoot = TheirDiscardYourDraw.Replace(text, " ");

            if (result.HasFlag(CardEffect.CardAdvantage)
                && (Loot.IsMatch(oneSidedLoot) || Cycling.IsMatch(text)
                    || (DrawBySacrificingItself.IsMatch(text)
                        && !DrawGrantedToOtherPermanents.IsMatch(text)
                        && !SacrificeRefundedByACopy.IsMatch(text))
                    || DrawByExilingItselfFromGraveyard.IsMatch(text)
                    || AdditionalCostDiscard.IsMatch(text) || ActivationCostDiscard.IsMatch(text)
                    || DrawPaidForWithACard.IsMatch(text)))
            {
                result &= ~CardEffect.CardAdvantage;
            }

            // A draw that is never YOURS, and a trigger that only watches one
            // happen. See TheirDraw and TriggersOffDrawing for the numbers, and
            // note that both are asked only once the card has no draw of its own.
            bool somebodyElsesDraw = TheirDraw.IsMatch(text) || TriggersOffDrawing.IsMatch(text);

            if (result.HasFlag(CardEffect.CardAdvantage) && somebodyElsesDraw
                && !YouDraw.IsMatch(TriggersOffDrawing.Replace(TheirDraw.Replace(text, " "), " ")))
            {
                result &= ~CardEffect.CardAdvantage;
            }

            // …unless the loot can be counted and comes out ahead. See
            // DrawsThenDiscards above for why this is added back rather than
            // written into the guard.
            if (LootDrawsMoreThanItPays(text))
            {
                result |= CardEffect.CardAdvantage;
            }

            // And the other half of the same ruling, 2026-09-19: a loot that
            // comes out ahead has stopped filtering. Filter is selection at no
            // net gain, so the moment the count is provably positive the card
            // belongs to CardAdvantage alone — Casting of Bones and Emmessi Tome
            // were carrying both.
            // Nothing withdraws Filter. The two tags are NOT exclusive, ruled
            // 2026-09-19: a card that selects AND comes out ahead carries both,
            // because it really did both. Casting of Bones draws three and
            // keeps two, which is a card gained and a choice made. An earlier
            // cut of the net ruling read it as either/or and stripped Filter
            // from every profitable loot; that was wrong and is recorded here
            // so it is not re-derived.

            // The top few of your library, one of them into your hand, over and
            // over. See TopFewIntoYourHand for the ruling and the measurement,
            // and note that nothing withdraws Filter: the card really did select.
            if (Abilities(text).Any(a => TopFewIntoYourHand.IsMatch(a) && AbilityRepeatsAtNoCostToItself(a)))
            {
                result |= CardEffect.CardAdvantage;
            }

            // A stream of cards out of the graveyard or exile, and only when you
            // can go back to it. Added here rather than in the table so a loot
            // elsewhere on the card cannot withdraw it — the cards are coming
            // from a different zone and nothing is being paid out of hand.
            if (CastsAStreamFromElsewhere.IsMatch(text)
                && (RepeatableWording.IsMatch(text) || StandingPermission.IsMatch(text)))
            {
                result |= CardEffect.CardAdvantage;
            }

            // Added AFTER the parity guard, because neither is a draw and neither
            // should be withdrawn by a loot or a cycling cost elsewhere on the card.
            bool impulseIsACard = ImpulseDraw.IsMatch(text)
                && (ImpulseOfSeveralCards.IsMatch(text)
                    || Abilities(text).Any(a => ImpulseDraw.IsMatch(a) && AbilityRepeats(a)));

            // Exiling several and playing only one is a count of zero. Asked of
            // the whole card rather than of the ability, because it has to
            // withdraw the several-cards claim that ImpulseOfSeveralCards and
            // ExileSeveralAndPlayThem both make on those same words; no reviewed
            // card carries this shape and a real multi-card impulse as well.
            bool onlyOneOfThem = ExileSeveralAndChooseOne.IsMatch(text);

            if (onlyOneOfThem)
            {
                result &= ~CardEffect.CardAdvantage;
                result |= CardEffect.Filter;
            }

            if ((!onlyOneOfThem
                    && (impulseIsACard || ExileSeveralAndPlayThem.IsMatch(text)))
                || DrawThatMany.IsMatch(text)
                || (ClueWording.IsMatch(text) && (RepeatableClue.IsMatch(text) || SeveralClues.IsMatch(text))))
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
            if (result.HasFlag(CardEffect.Buff) && (OnlyPumpsATribe(text) || OnlyPumpsInsideQuotes(text)))
            {
                result &= ~CardEffect.Buff;
            }

            // Three more ways a pump belongs to something other than this tag,
            // all ruled 2026-09-21. Each is asked by BLANKING the shape and
            // seeing whether any pump is left standing, so a card that explains
            // rampage on one line and pumps on another keeps the tag.
            if (result.HasFlag(CardEffect.Buff))
            {
                string realPump = Parenthetical.Replace(text, " ");

                if (TookThatCreature.IsMatch(text))
                {
                    realPump = PumpsWhatItTook.Replace(realPump, " ");
                }

                realPump = string.Join('\n', realPump.Split('\n')
                    .Select(line => AnswerOrShieldOnTheSameLine.IsMatch(line) ? " " : line));

                if (realPump != text && !BuffPatterns.Any(p => p.IsMatch(realPump))
                    && !(CounterOnSomebodyElse.IsMatch(realPump) && !CounterForAnOpponent.IsMatch(realPump)))
                {
                    result &= ~CardEffect.Buff;
                }
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

            if (result.HasFlag(CardEffect.Disenchant) && !AnswersAnArtifactOrEnchantment(text))
            {
                result &= ~CardEffect.Disenchant;
            }

            if (result.HasFlag(CardEffect.LandDestruction) && !AnswersALand(text))
            {
                result &= ~CardEffect.LandDestruction;
            }

            if (result.HasFlag(CardEffect.Tutor) && !FetchesSomethingWorthFetching(text))
            {
                result &= ~CardEffect.Tutor;
            }

            // Stripping the donation and re-asking, rather than a lookbehind: the
            // subject and the verb are not adjacent ("that player MAY gain control
            // of this artifact"), which is the same trap TheirDraw fell into.
            if (result.HasFlag(CardEffect.Steal) && GivesControlAway.IsMatch(text)
                && !StealPatterns.Any(p => p.IsMatch(GivesControlAway.Replace(text, " "))))
            {
                result &= ~CardEffect.Steal;
            }

            if ((SomebodyElsesZone.IsMatch(text)
                    && ((AndPlaysThem.IsMatch(text) && !PlaysACardYouOwn.IsMatch(text))
                        || OntoYourSideOfTheBoard.IsMatch(text)))
                || (SomebodyElsesNamedZone.IsMatch(text) && AndSomebodyPlaysThem.IsMatch(text)
                    && !PlaysACardYouOwn.IsMatch(text)))
            {
                result |= CardEffect.Steal;
            }

            return result;
        }
    }
}
