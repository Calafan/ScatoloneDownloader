using System.Text.RegularExpressions;

using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Cube
{
    /// <summary>
    /// The questions that a regex cannot ask on its own: who an effect lands on,
    /// whether a loot comes out ahead, whether every pump is a tribe's. Part of
    /// <see cref="EffectClassifier"/>.
    /// </summary>
    internal static partial class EffectClassifier
    {
        /// <summary>Whether some ONE line of the card is a real answer to an
        /// artifact or an enchantment. Read line by line rather than whole, so a
        /// modal card is judged mode by mode: Pyramids offers "destroy target Aura
        /// attached to a land" next to a damage-prevention mode, and neither
        /// should be able to excuse or condemn the other. See
        /// <see cref="SweepsCreaturesToo"/>, <see cref="ExileThatComesBack"/> and
        /// <see cref="DisenchantsYourOwn"/> for what each veto costs and buys.</summary>
        /// <summary>Whether every destroy-or-exile clause on the card points at
        /// something that answers nobody. Returns false when the card has no such
        /// clause at all, because then the tag came from damage, a fight, a shrink
        /// or an edict and this question does not apply to it.</summary>
        private static bool OnlyAnswersNobody(string text)
        {
            bool sawOne = false;

            foreach (Match match in AimedAtACreature.Matches(text))
            {
                sawOne = true;
                if (!AnswersNobody.IsMatch(match.Value))
                {
                    return false;
                }
            }

            return sawOne;
        }

        /// <summary>Whether some ONE line of the card searches for a card that is
        /// not a land and not another copy of itself. See
        /// <see cref="SearchesOnlyForALand"/> and
        /// <see cref="SearchesForACardNamed"/> for what each veto costs.</summary>
        private static bool FetchesSomethingWorthFetching(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                if (!TutorPatterns.Any(p => p.IsMatch(line)))
                {
                    continue;
                }

                if (SearchesOnlyForALand.IsMatch(line) && !SearchesForSomethingElseToo.IsMatch(line))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>Whether some ONE line of the card really takes a land off the
        /// battlefield. Read line by line for the same reason Disenchant is. See
        /// <see cref="LandAlreadyInAGraveyard"/>, <see cref="AuraOnALand"/>,
        /// <see cref="DestroysYourOwnLand"/> and <see cref="HandsTheLandsBack"/>
        /// for the card behind each veto.</summary>
        private static bool AnswersALand(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                if (!LandDestructionPatterns.Any(p => p.IsMatch(line)))
                {
                    continue;
                }

                if (LandAlreadyInAGraveyard.IsMatch(line) || AuraOnALand.IsMatch(line)
                    || DestroysYourOwnLand.IsMatch(line)
                    || (HandsTheLandsBack.IsMatch(line) && !AnyDestroyOrExile.IsMatch(line)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool AnswersAnArtifactOrEnchantment(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                if (!DisenchantPatterns.Any(p => p.IsMatch(line)))
                {
                    continue;
                }

                if (SweepsCreaturesToo.IsMatch(line) || ExileThatComesBack.IsMatch(line)
                    || DisenchantsYourOwn.IsMatch(line))
                {
                    continue;
                }

                return true;
            }

            return false;
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

        /// <summary>The number a card-count word stands for, or 0 when it is not
        /// a number at all ("draws cards equal to", "discards their hand").</summary>
        private static int CardCount(string word) => word.ToLowerInvariant() switch
        {
            "a" or "an" or "one" => 1,
            "two" => 2,
            "three" => 3,
            "four" => 4,
            "five" => 5,
            "six" => 6,
            "seven" => 7,
            _ => int.TryParse(word, out int parsed) ? parsed : 0,
        };

        /// <summary>True when the card PROVABLY draws more cards than it hands
        /// back. Both counts have to parse, so an unreadable one stays parity.
        /// </summary>
        private static bool LootDrawsMoreThanItPays(string text)
        {
            // Counted LINE BY LINE, because repeatability and cost belong to
            // the ability that loots and not to the card as a whole. Uthros
            // Scanship draws two and discards one on an ENTERS trigger and
            // carries Station further down; reading the colon in "Station (Tap
            // another creature you control:" as this loot's activation cost
            // made a one-shot look repeatable and handed it a card it never got.
            foreach (string line in text.Split('\n'))
            {
                int net = int.MinValue;

                foreach (Match m in DrawsThenDiscards.Matches(line))
                {
                    int drew = CardCount(m.Groups[1].Value), paid = CardCount(m.Groups[2].Value);
                    if (drew > 0 && paid > 0) { net = Math.Max(net, drew - paid); }
                }

                foreach (Match m in DiscardsThenDraws.Matches(line))
                {
                    int paid = CardCount(m.Groups[1].Value), drew = CardCount(m.Groups[2].Value);
                    if (drew > 0 && paid > 0) { net = Math.Max(net, drew - paid); }
                }

                if (net == int.MinValue)
                {
                    continue;
                }

                // Everything else this line spends. See TheLootRepeats and
                // AdditionalCostDiscard for the ruling behind each subtraction.
                if (!TheLootRepeats(line)) { net -= 1; }

                if (AdditionalCostDiscard.IsMatch(text) || ActivationCostDiscard.IsMatch(line)) { net -= 1; }

                Match lands = SacrificedLands.Match(line);
                if (lands.Success) { net -= CardCount(lands.Groups[1].Value); }

                if (net > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when every pump on the card is written INSIDE QUOTES —
        /// an ability the card hands to a token it creates (Chocobo Racetrack's
        /// Bird, Gysahl Greens') or to somebody else's creature (Armor Sliver's
        /// Slivers). What the card does is make the token or grant the ability;
        /// the pump belongs to whatever received it.
        /// <para>
        /// A quoted ability that pumps a TARGET used to be the exception and keep
        /// the tag. That exception was REMOVED on 2026-09-21, by the same ruling
        /// Burn got the day before: what a token does is the token's, whoever it
        /// points at. Seven reviewed cards hand a Mercenary or an Equipment a
        /// "{T}: Target creature you control gets +1/+0" and not one of them is
        /// tagged Buff — Mabel, At Knifepoint, Rakish Crew, Nezumi Linkbreaker,
        /// Wanted Griffin and two more.
        /// </para></summary>
        private static bool OnlyPumpsInsideQuotes(string text) =>
            PumpInsideQuotes.IsMatch(text) && !PlainPump.IsMatch(Quoted.Replace(text, " "));

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

            // Asked with PlainPump rather than BuffPatterns on purpose: the tribal
            // phrase stops at the verb, so the numbers it was about ("+2/+0 until
            // end of turn") survive the blanking and the bare "+N/+N until end of
            // turn" pattern would see them and vouch for a pump that is not there.
            string rest = TribalPump.Replace(text, " ");
            return !PlainPump.IsMatch(rest)
                && !Rx(@"creatures you control get \+").IsMatch(rest)
                && !(CounterOnSomebodyElse.IsMatch(rest) && !CounterForAnOpponent.IsMatch(rest));
        }

        /// <summary>True when the card's Treasures come often enough or thick
        /// enough to be extra mana rather than a rider: several at once, or one
        /// on a repeating trigger or an activated ability. The reminder text is
        /// blanked first, because it carries a colon and would make every
        /// Treasure card read as repeatable.</summary>
        /// <summary>True when what the card makes is a CREATURE — said outright,
        /// said in a named token's reminder text, or made as a copy of one.
        /// </summary>
        private static bool MakesACreatureBody(string text) =>
            CreatureTokenWording.IsMatch(text)
            || NamedTokenIsACreature.IsMatch(text)
            || TokenCopiesACreature(text);

        /// <summary>A token copy is a body unless the thing being copied is a
        /// noncreature permanent, ruled 2026-09-19: Esoteric Duplicator copies
        /// an artifact and Firion an Equipment, and neither puts anything on the
        /// board to attack with. Asked of the LINE that makes the copy, because
        /// the type word that answers it sits in the trigger beside the verb.
        /// </summary>
        private static bool TokenCopiesACreature(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                if (!AnyTokenCopy.IsMatch(line))
                {
                    continue;
                }

                if (line.Contains("creature", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // No type word at all means a copy of something unspecified,
                // which is read as a creature; a noncreature type named on the
                // line rules this line out and the next one is tried.
                if (!NoncreatureBeingCopied.IsMatch(line))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TreasureAlsoRamps(string text)
        {
            if (!MakesATreasure.IsMatch(text))
            {
                return false;
            }

            return SeveralTreasures.IsMatch(text)
                || RepeatableWording.IsMatch(TreasureReminderText.Replace(text, " "));
        }

        /// <summary>True when EVERY line that adds mana charges mana for it and
        /// hands back no more than it took, so the card converts colour without
        /// adding any. Per line for the same reason as
        /// <see cref="AllManaIsRestricted"/>: one free ability is enough to make
        /// the card ramp, however many paid ones sit beside it.</summary>
        private static bool EveryManaAbilityIsPaidAndPoor(string text)
        {
            bool addsAny = false;
            foreach (string line in text.Split('\n'))
            {
                if (!line.Contains(": Add ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                addsAny = true;
                if (!PaidManaAbility.IsMatch(line) || PaidManaGivesBackMore.IsMatch(line))
                {
                    return false;
                }
            }

            return addsAny;
        }

        /// <summary>True when EVERY line that fixes colour asks for nothing but a
        /// tap, so the card is a mana source and not a fixer. Per line for the
        /// same reason as <see cref="AllManaIsRestricted"/>: one paid ability is
        /// enough to make the card fix, however many free ones sit next to it
        /// (Mana Prism taps for {C} and pays {1} for the colour).
        /// <para>
        /// Typecycling backs out first: it vouches for the tag on its own, from a
        /// line that mentions no mana at all, and the loop below would not see it.
        /// </para>
        /// </summary>
        private static bool EveryFixerIsABareTap(string text)
        {
            if (TypeCycling.IsMatch(text))
            {
                return false;
            }

            bool sawFixer = false;
            foreach (string line in text.Split('\n'))
            {
                if (!FixesColourOnThisLine.IsMatch(line))
                {
                    continue;
                }

                sawFixer = true;
                if (!BareTapAdds.IsMatch(line))
                {
                    return false;
                }
            }

            return sawFixer;
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
                    // The window was 80 characters and that truncated the trigger
                    // word itself on the longer abilities, which turned the strip
                    // below into a no-op exactly where it was needed: Angelic
                    // Protector's second pattern matched at "+0/+3" and looked
                    // back on "…ever this creature becomes the TARGET of a spell
                    // or ability, this creature gets". The clause cut and the
                    // trigger strip are the real bounds; 240 only stops a window
                    // running off into a neighbouring sentence that has neither.
                    int from = Math.Max(lineStart, match.Index - 240);
                    string before = text.Substring(from, match.Index - from);

                    // The subject of an effect is in its OWN clause, so cut the
                    // window back to the last colon or sentence end. Without this
                    // the ACTIVATION COST supplies a false beneficiary: "Tap an
                    // untapped creature you control: THIS CREATURE gets +1/+1"
                    // pumps nobody but itself, and so do Karplusan Giant, Vodalian
                    // War Machine and Comet Crawler's "sacrifice ANOTHER creature.
                    // If you do, this creature gets +2/+2". Added 2026-09-17.
                    int clause = Math.Max(before.LastIndexOf(':'), before.LastIndexOf(". ", StringComparison.Ordinal));
                    if (clause >= 0)
                    {
                        before = before[(clause + 1)..];
                    }

                    // The TRIGGER supplies a false beneficiary the same way the
                    // cost does, and that was the single biggest source of wrong
                    // Buff tags (found 2026-09-21): "Landfall — Whenever A LAND
                    // YOU CONTROL enters, Ambrosia Whiteheart gets +1/+0" pumps
                    // nobody but Ambrosia, and a dozen more trigger off "ANOTHER
                    // creature you control" or off something happening "EACH
                    // turn".
                    //
                    // It is stripped for the BENEFICIARY question only, not for
                    // the SELF question, and that asymmetry is the whole point:
                    // "Whenever THIS CREATURE attacks, IT gets +1/+1" keeps its
                    // self-reference in the trigger and nowhere else, so cutting
                    // the window for both questions reads it as a gift to
                    // somebody. Measured both ways — cutting for both is 22
                    // false positives worse than doing nothing.
                    string effectOnly = TriggerClause.Replace(before, " ");

                    if (Beneficiary.IsMatch(effectOnly)
                        || Beneficiary.IsMatch(AfterCounterClause(text, match)))
                    {
                        return false;
                    }

                    if (SelfReference.IsMatch(before)
                        || (ownName.Length > 0 && before.Contains(ownName, StringComparison.OrdinalIgnoreCase)))
                    {
                        sawSelf = true;
                        continue;
                    }

                    if (GrantVerb.IsMatch(effectOnly) || !bareIsSelf)
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
