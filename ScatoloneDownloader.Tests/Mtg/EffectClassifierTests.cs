using System.Collections.Generic;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Validates the rule-based <see cref="EffectClassifier"/> against real card
/// oracle text. The classifier only PROPOSES (a human confirms), so these pin
/// the high-confidence cases, the deliberate narrow edges (mana denial that
/// destroys nothing, a land sacrifice paid as a drawback) and the wordings
/// that fooled an earlier draft, rather than every card.
/// </summary>
public sealed class EffectClassifierTests
{
    [Theory]
    // name, typeLine, oracleText, expected flags
    // Both, and deliberately: damage is how a red deck answers a creature, so
    // Bolt is as much removal as Doom Blade. Decided 2026-09-11 against 3664
    // reviewed cards, where "deals N damage to any target" is hand-tagged
    // Removal 68 times in 75.
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.", CardEffect.Removal | CardEffect.Burn)]
    [InlineData("Wrath of God", "Sorcery", "Destroy all creatures. They can't be regenerated.", CardEffect.Wipe)]
    [InlineData("Counterspell", "Instant", "Counter target spell.", CardEffect.Counter)]
    [InlineData("Swords to Plowshares", "Instant", "Exile target creature. Its controller gains life equal to its power.", CardEffect.Removal)]
    [InlineData("Vindicate", "Sorcery", "Destroy target permanent.", CardEffect.RemovePermanent)]
    [InlineData("Demonic Tutor", "Sorcery", "Search your library for a card, then shuffle and put that card into your hand.", CardEffect.Tutor)]
    [InlineData("Worldly Tutor", "Instant", "Search your library for a creature card, reveal that card, then shuffle and put it on top.", CardEffect.Tutor)]
    [InlineData("Sol Ring", "Artifact", "{T}: Add {C}{C}.", CardEffect.Ramp)]
    [InlineData("Pacifism", "Enchantment — Aura", "Enchant creature. Enchanted creature can't attack or block.", CardEffect.Pacify)]
    [InlineData("Icy Manipulator", "Artifact", "{1}, {T}: Tap target artifact, creature, or land.", CardEffect.Pacify)]
    [InlineData("Reanimate", "Sorcery", "Return target creature card from your graveyard to the battlefield. You lose life equal to its mana value.", CardEffect.Reanimate)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Mind Rot", "Sorcery", "Target player discards two cards.", CardEffect.Discard)]
    [InlineData("Divination", "Sorcery", "Draw two cards.", CardEffect.CardAdvantage)]
    [InlineData("Control Magic", "Enchantment — Aura", "Enchant creature. You control enchanted creature.", CardEffect.Steal)]
    [InlineData("Stone Rain", "Sorcery", "Destroy target land.", CardEffect.LandDestruction)]
    [InlineData("Wasteland", "Land", "{T}, Sacrifice Wasteland: Destroy target nonbasic land.", CardEffect.LandDestruction)]
    // "Destroy all lands" is land destruction, not a creature/permanent wipe:
    // Wipe's patterns deliberately list creatures, permanents and nonland only.
    [InlineData("Armageddon", "Sorcery", "Destroy all lands.", CardEffect.LandDestruction)]
    [InlineData("Rain of Salt", "Sorcery", "Destroy two target lands.", CardEffect.LandDestruction)]
    [InlineData("Sinkhole", "Sorcery", "Destroy target land.", CardEffect.LandDestruction)]
    [InlineData("Glimpse the Unthinkable", "Sorcery", "Target player mills ten cards.", CardEffect.Mill)]
    [InlineData("Misdirection", "Instant", "Change the target of target spell with a single target.", CardEffect.Redirect)]
    [InlineData("Fork", "Instant", "Copy target instant or sorcery spell. You may choose new targets for the copy.", CardEffect.Redirect)]
    // A token entering as a copy OF something is Tokens, not stack interaction:
    // the rules require "copy target", never the bare word "copy".
    [InlineData("Clone", "Creature — Shapeshifter", "You may have this creature enter as a copy of any creature on the battlefield.", CardEffect.None)]
    // "Mill" is only keyword wording from 2021 on; the pre-2021 library spells
    // the action out, so the old phrasing has to match too.
    [InlineData("Millstone", "Artifact", "{2}, {T}: Target player puts the top two cards of their library into their graveyard.", CardEffect.Mill)]
    // Self-mill is NOT Mill as of 2026-09-15: dredge fills your own graveyard,
    // which is what a graveyard deck runs on rather than an attack on a library.
    // The card ends up with nothing proposed at all, and that is the point — the
    // rest of its text ("destroy that creature") is combat damage, not removal
    // aimed by you, and dredge's "if you would draw a card" is a replacement for
    // a draw rather than an extra one.
    [InlineData("Stinkweed Imp", "Creature — Imp", "Flying\nWhenever this creature deals combat damage to a creature, destroy that creature.\nDredge 5 (If you would draw a card, you may mill five cards instead.)", CardEffect.None)]
    public void Classify_ExactMatch_ForHighConfidenceCards(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

    [Theory]
    // "island" and "islandwalk" contain the letters of "land", so the patterns
    // require a word boundary before it. Without that, every islandwalk creature
    // would be proposed as land destruction.
    [InlineData("Deep Spawn", "Creature — Kraken", "Islandwalk")]
    [InlineData("Sea Serpent", "Creature — Serpent", "Sea Serpent can't attack unless defending player controls an Island.")]
    // Both of these were caught by auditing already-reviewed cards against a
    // looser first draft of the rules. Pyramids PROTECTS lands, and the thing it
    // destroys is an Aura; Serendib Djinn's land sacrifice is a drawback its own
    // controller pays, not an effect aimed at an opponent.
    [InlineData("Pyramids", "Artifact", "{2}: Destroy target Aura attached to a land.\n{2}: The next time target land would be destroyed this turn, remove all damage marked on it instead.")]
    [InlineData("Serendib Djinn", "Creature — Djinn", "Flying\nAt the beginning of your upkeep, sacrifice a land. If you sacrifice an Island this way, this creature deals 3 damage to you.")]
    public void Classify_LandWordings_ThatAreNotLandDestruction(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Mill has to be aimed at somebody else. Everything here fills the caster's
    // own graveyard: as an upkeep tax (Deep Spawn), as an activation cost
    // (Millikin), as a recursion cost (Rot Farm Skeleton), or as the whole point
    // of a graveyard deck (Ooze Patrol). The old rule fired on all four because
    // it only looked for the verb, and needed a separate cost guard to undo the
    // first three; naming the victim covers all four at once.
    [InlineData("Deep Spawn", "Creature — Kraken",
        "Trample\nAt the beginning of your upkeep, sacrifice this creature unless you mill two cards.\n{U}: This creature gains shroud until end of turn.")]
    [InlineData("Millikin", "Artifact Creature — Construct", "{T}, Mill a card: Add {C}.")]
    [InlineData("Rot Farm Skeleton", "Creature — Plant Skeleton",
        "This creature can't block.\n{2}{B}{G}, Mill four cards: Return this card from your graveyard to the battlefield. Activate only as a sorcery.")]
    [InlineData("Ooze Patrol", "Creature — Ooze",
        "When this creature enters, mill two cards, then put a +1/+1 counter on this creature for each creature card in your graveyard.")]
    public void Classify_MillingYourself_IsNotMill(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // The shapes the victim gets named in. Altar of Dementia puts it after a
    // cost, Millstone in front of the spelled-out pre-2021 wording, Specimen
    // Freighter names the defending player, and Bruvac's only mill wording is
    // reminder text — which is part of oracle_text and counts.
    [InlineData("Altar of Dementia", "Artifact", "Sacrifice a creature: Target player mills cards equal to the sacrificed creature's power.")]
    [InlineData("Millstone", "Artifact", "{2}, {T}: Target player puts the top two cards of their library into their graveyard.")]
    [InlineData("Specimen Freighter", "Artifact — Spacecraft", "Whenever this Spacecraft attacks, defending player mills four cards.")]
    [InlineData("Bruvac the Grandiloquent", "Legendary Creature — Human Advisor",
        "If an opponent would mill one or more cards, they mill twice that many cards instead. (To mill a card, a player puts the top card of their library into their graveyard.)")]
    public void Classify_MillingSomebodyElse_IsMill(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // Tokens means creature bodies on YOUR side. A Treasure, a Clue, a Food or a
    // Lander is a resource the card hands over in passing, and Afterlife's
    // Spirit is a body for the other player.
    [InlineData("Ichor Wellspring", "Artifact", "When this artifact enters, create a Treasure token.")]
    [InlineData("Tireless Tracker", "Creature — Human Scout", "Whenever a land you control enters, investigate. (Create a Clue token.)")]
    [InlineData("Afterlife", "Instant", "Destroy target creature. It can't be regenerated. Its controller creates a 1/1 white Spirit creature token with flying.")]
    [InlineData("Phelddagrif", "Legendary Creature — Hippo", "{G}: Phelddagrif gains trample until end of turn. Target opponent creates a 1/1 green Hippo creature token.")]
    public void Classify_TokensThatAreNotYourCreatures_IsNotTokens(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    [InlineData("Lingering Souls", "Sorcery", "Create two 1/1 white Spirit creature tokens with flying.")]
    // The token that never says "creature token" because it is a copy of one.
    [InlineData("Kiki-Jiki, Mirror Breaker", "Legendary Creature — Goblin Shaman",
        "{T}: Create a token that's a copy of another target nonlegendary creature you control. That token gains haste.")]
    public void Classify_CreatureTokensForYou_IsTokens(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Tokens));
    }

    [Theory]
    // Discarding is only an effect when somebody else's hand is emptied. These
    // three PAY a card: as an activation cost, as an alternative cast cost, and
    // as the back half of a loot.
    [InlineData("Wild Mongrel", "Creature — Dog", "Discard a card: This creature gets +1/+1 and becomes the color of your choice until end of turn.")]
    [InlineData("Basking Rootwalla", "Creature — Lizard", "Madness {0} (If you discard this card, discard it into exile.)")]
    [InlineData("Faithless Looting", "Sorcery", "Draw two cards, then discard two cards.")]
    public void Classify_DiscardingYourOwnCards_IsNotDiscard(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Discard));
    }

    [Theory]
    // Returning something to hand is Bounce only when it names what goes back.
    // Ovinomancer pays its own return as an activation cost and Fleeting Effigy
    // pays it as a drawback; neither answers anything.
    [InlineData("Ovinomancer", "Creature — Human Wizard",
        "{T}, Return this creature to its owner's hand: Destroy target creature. It can't be regenerated.")]
    [InlineData("Fleeting Effigy", "Creature — Spirit",
        "Haste\nAt the beginning of your end step, return this creature to its owner's hand.")]
    public void Classify_ReturningItself_IsNotBounce(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    [InlineData("Unsummon", "Instant", "Return target creature to its owner's hand.")]
    // "up to one other" sits between the verb and "target", and the plural form
    // is "to their owners' hands" — both broke a tighter first draft.
    [InlineData("Spider-Byte, Web Warden", "Creature — Spider",
        "When Spider-Byte enters, return up to one target nonland permanent to its owner's hand.")]
    [InlineData("Undo", "Instant", "Return two target creatures to their owners' hands.")]
    [InlineData("Evacuation", "Instant", "Return all creatures to their owners' hands.")]
    public void Classify_ReturningSomethingNamed_IsBounce(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Bounce));
    }

    [Fact]
    public void Classify_ManaDenial_WithoutDestroyingALand_IsNotProposed()
    {
        // The tag is deliberately narrow: Winter Orb attacks the mana base but
        // destroys nothing, so it stays for a human to decide.
        Card card = MakeCard("Winter Orb", "Artifact", "Players can't untap more than one land during their untap steps.");

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.LandDestruction));
    }

    [Fact]
    public void Classify_BirdsOfParadise_RampAndManaFixing()
    {
        Card card = MakeCard("Birds of Paradise", "Creature — Bird", "Flying\n{T}: Add one mana of any color.", keywords: ["Flying"]);

        Assert.Equal(CardEffect.Ramp | CardEffect.ManaFixing, EffectClassifier.Classify(card));
    }

    [Fact]
    public void Classify_DualLand_IsManaFixing_NotRamp()
    {
        // A land tapping for its own (fixing) mana must be ManaFixing but NOT Ramp.
        Card card = MakeCard("Tundra", "Land", "{T}: Add {W} or {U}.");

        CardEffect result = EffectClassifier.Classify(card);

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Protection counts only when it can be deployed in response. Mother of Runes
    // answers a removal spell on the stack; a bogle with hexproof printed on it
    // protects nobody but itself, and holds nothing up.
    [InlineData("Mother of Runes", "Creature — Human Cleric", "{T}: Target creature you control gains protection from the color of your choice until end of turn.", true)]
    [InlineData("Giant Growth", "Instant", "Target creature gets +3/+3 until end of turn. It gains hexproof until end of turn.", true)]
    // NB: a Circle of Protection would pass this gate (its "{1}:" is an activated
    // ability) but never reaches it — the rules' vocabulary has no word for
    // PREVENTION yet, only protection/hexproof/shroud/indestructible. Whether
    // prevention joins them is still open, and deciding it here by side effect
    // would also silently rule on every fog.
    [InlineData("Slippery Bogle", "Creature — Beast", "", false)]
    [InlineData("Darksteel Colossus", "Artifact Creature — Golem", "Trample\nThis creature is indestructible.", false)]
    [InlineData("Asceticism-style aura", "Enchantment — Aura", "Enchanted creature has hexproof.", false)]
    public void Classify_Protection_RequiresInstantSpeed(string name, string typeLine, string oracle, bool expected)
    {
        List<string>? keywords = name == "Slippery Bogle" ? ["Hexproof"] : null;
        Card card = MakeCard(name, typeLine, oracle, keywords);

        Assert.Equal(expected, EffectClassifier.Classify(card).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A creature that pumps or shields only ITSELF has a stat line, not an
    // effect — it answers nothing for the rest of the board. Decided 2026-09-05.
    [InlineData("Adanto Vanguard", "Creature — Vampire Soldier",
        "As long as this creature is attacking, it gets +2/+0.\nPay 4 life: This creature gains indestructible until end of turn.")]
    [InlineData("Advanced Hoverguard", "Creature — Drone",
        "Flying\n{U}: This creature gains shroud until end of turn.")]
    // Its own name is the same self-reference the older printings write.
    [InlineData("Akroma, Angel of Fury", "Legendary Creature — Angel",
        "This spell can't be countered.\nFlying, trample, protection from white and from blue\n{R}: Akroma gets +1/+0 until end of turn.")]
    [InlineData("Armored Armadillo", "Creature — Armadillo",
        "Ward {1} (Whenever this creature becomes the target of a spell or ability an opponent controls, counter it unless that player pays {1}.)")]
    // A condition ON what you control is not a beneficiary: the pump still lands
    // on the creature itself. This is the wording the stop-word list exists for.
    [InlineData("Aerial Engineer", "Artifact Creature — Construct",
        "As long as you control an artifact, this creature gets +2/+0 and has flying.")]
    public void Classify_SelfBuffAndSelfProtection_AreNotEffects(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Buff));
        Assert.False(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // The same creatures, aimed outward, keep the tag. A "target" two lines up
    // must not vouch for a self-buff, which is why the window is one line: the
    // Armored Guardian entry below grants on one line and shields itself on the
    // next, and only the first is why it counts.
    [InlineData("Aerie Mystics", "Creature — Bird Wizard",
        "Flying\n{1}{G}{U}: Creatures you control gain shroud until end of turn.", CardEffect.Protection)]
    [InlineData("Armored Guardian", "Creature — Giant",
        "{1}{W}{W}: Target creature you control gains protection from the color of your choice until end of turn.\n{1}{U}{U}: This creature gains shroud until end of turn.", CardEffect.Protection)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Bonesplitter", "Artifact — Equipment", "Equipped creature gets +2/+0.\nEquip {1}", CardEffect.Buff)]
    // The beneficiary is whatever type line the card names, not just "creatures",
    // and it can be a battlefield state rather than a controller.
    [InlineData("Adeliz, the Cinder Wind", "Legendary Creature — Efreet Wizard",
        "Flying, haste\nWhenever you cast an instant or sorcery spell, Wizards you control get +1/+1 until end of turn.", CardEffect.Buff)]
    [InlineData("Ainok Strike Leader", "Creature — Hound Soldier",
        "Sacrifice this creature: Creature tokens you control gain indestructible until end of turn.", CardEffect.Protection)]
    [InlineData("Agrus Kos, Wojek Veteran", "Legendary Creature — Human Soldier",
        "Whenever Agrus Kos attacks, attacking red creatures get +2/+0 and attacking white creatures get +0/+2 until end of turn.", CardEffect.Buff)]
    public void Classify_EffectsAimedAtSomethingElse_Survive(string name, string typeLine, string oracle, CardEffect expected)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(expected));
    }

    [Fact]
    public void Classify_VanillaCreature_IsNone()
    {
        Card card = MakeCard("Grizzly Bears", "Creature — Bear", "");

        Assert.Equal(CardEffect.None, EffectClassifier.Classify(card));
    }

    [Theory]
    // Graveyard to HAND is Regrowth; graveyard to the BATTLEFIELD stays Reanimate.
    // This used to be a documented gap: the Reanimate rules never matched a return
    // to hand, so Regrowth itself came back untagged.
    [InlineData("Regrowth", "Sorcery", "Return target card from your graveyard to your hand.", CardEffect.Regrowth)]
    [InlineData("Raise Dead", "Sorcery", "Return target creature card from your graveyard to your hand.", CardEffect.Regrowth)]
    [InlineData("Animate Dead", "Enchantment — Aura", "Return target creature card from your graveyard to the battlefield under your control.", CardEffect.Reanimate)]
    public void Classify_GraveyardRecursion_SplitsByDestination(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

    [Theory]
    // Targeted damage answers a threat, so it is Removal as well as Burn. The
    // scaling ones matter most: "deals X damage" is how the whole pre-modern
    // library writes a kill spell.
    [InlineData("Blaze", "Sorcery", "Blaze deals X damage to any target.")]
    [InlineData("Broadside Barrage", "Instant", "Broadside Barrage deals 5 damage to target creature or planeswalker. Draw a card, then discard a card.")]
    [InlineData("Char", "Instant", "Char deals 4 damage to any target and 2 damage to you.")]
    // Fight is the same act with the damage delegated; an edict never says "target".
    [InlineData("Prey Upon", "Sorcery", "Target creature you control fights target creature you don't control.")]
    [InlineData("Diabolic Edict", "Instant", "Target player sacrifices a creature.")]
    public void Classify_DamageAndItsCousins_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // "Any target" is the one wording that is both at once, because it can be
    // pointed at a face or at a creature. Scaling and fixed amounts read the
    // same, which is why [\dX] is in the pattern.
    [InlineData("Blaze", "Sorcery", "Blaze deals X damage to any target.")]
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.")]
    public void Classify_DamageAtAnyTarget_IsBothBurnAndRemoval(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Burn));
        Assert.True(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Damage that can only ever hit a creature answers a threat, and answering a
    // threat is Removal. Reading it as Burn too was worth 129 false positives.
    [InlineData("Disintegrate", "Sorcery", "Disintegrate deals X damage to target creature. If that creature would die this turn, exile it instead.")]
    [InlineData("Explosive Shot", "Instant", "Explosive Shot deals 4 damage to target creature.")]
    public void Classify_DamageAtACreature_IsRemovalNotBurn(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Burn));
    }

    [Theory]
    // A sweeper that catches the players on its way past is Wipe AND Burn; one
    // that only hits creatures is Wipe alone. Ruled 2026-09-15 off Inferno.
    [InlineData("Inferno", "Instant", "Inferno deals 6 damage to each creature and each player.")]
    [InlineData("Earthquake", "Sorcery", "Earthquake deals X damage to each creature without flying and each player.")]
    public void Classify_SweeperThatHitsPlayers_IsWipeAndBurn(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        Assert.True(result.HasFlag(CardEffect.Burn));
    }

    [Fact]
    public void Classify_SweeperThatSparesPlayers_IsWipeAlone()
    {
        CardEffect result = EffectClassifier.Classify(
            MakeCard("Pyroclasm", "Sorcery", "Pyroclasm deals 2 damage to each creature."));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        Assert.False(result.HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Damage to YOURSELF is a price, not an effect — the same reading that keeps
    // a self-mill out of Mill and an additional cost out of Sacrifice.
    [InlineData("Ancient Tomb", "Land", "{T}: Add {C}{C}. This land deals 2 damage to you.")]
    [InlineData("City of Brass", "Land", "Whenever this land becomes tapped, it deals 1 damage to you.\n{T}: Add one mana of any color.")]
    [InlineData("Juzam Djinn", "Creature — Djinn", "At the beginning of your upkeep, this creature deals 1 damage to you.")]
    public void Classify_DamageToYourself_IsNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Sacrifice means an OUTLET for your own creatures — the half of the combo
    // that makes a stolen creature worth taking. An activation cost in front of a
    // colon is the canonical shape; a trigger that OFFERS the sacrifice counts too.
    [InlineData("Ashnod's Altar", "Artifact", "Sacrifice a creature: Add {C}{C}.")]
    [InlineData("Goblin Bombardment", "Enchantment", "Sacrifice a creature: This enchantment deals 1 damage to any target.")]
    [InlineData("Comet Crawler", "Creature — Beast",
        "Lifelink\nWhenever this creature attacks, you may sacrifice another creature or artifact. If you do, this creature gets +2/+0 until end of turn.")]
    public void Classify_SacrificeOutlet_IsSacrifice(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
    }

    [Theory]
    // Three things that are not an outlet: an edict empties somebody ELSE's
    // board, an additional cost to cast is paid once on the way to a different
    // effect, and a land is not a creature.
    [InlineData("Diabolic Edict", "Instant", "Target player sacrifices a creature of their choice.")]
    [InlineData("Natural Order", "Sorcery",
        "As an additional cost to cast this spell, sacrifice a green creature.\nSearch your library for a green creature card, put it onto the battlefield, then shuffle.")]
    [InlineData("Harrow", "Instant",
        "As an additional cost to cast this spell, sacrifice a land.\nSearch your library for up to two basic land cards, put them onto the battlefield, then shuffle.")]
    public void Classify_SacrificeThatIsNotAnOutlet_IsNotSacrifice(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
    }

    [Theory]
    // Mass damage is the oldest board sweeper there is and says "destroy" nowhere.
    [InlineData("Earthquake", "Sorcery", "Earthquake deals X damage to each creature without flying and each player.")]
    [InlineData("Crypt Rats", "Creature — Rat", "{X}: This creature deals X damage to each creature and each player. Spend only black mana on X.")]
    [InlineData("Pyroclasm", "Sorcery", "Pyroclasm deals 2 damage to each creature.")]
    public void Classify_MassDamage_IsWipe(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Wipe));
        // Sweeping the board is not aimed at anybody, so the single-target
        // Removal reading must not come along for the ride.
        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // "This artifact doesn't untap" is a tax the card pays itself. Neutralising
    // nobody is not Pacify — the same self-versus-other reading as Buff.
    [InlineData("Basalt Monolith", "Artifact", "This artifact doesn't untap during your untap step.\n{T}: Add {C}{C}{C}.\n{3}: Untap this artifact.")]
    [InlineData("Mana Vault", "Artifact", "This artifact doesn't untap during your untap step.\nAt the beginning of your upkeep, you may pay {4}. If you do, untap this artifact.")]
    [InlineData("Time Vault", "Artifact", "This artifact enters tapped.\nThis artifact doesn't untap during your untap step.")]
    public void Classify_SelfUntapTax_IsNotPacify(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // A lock put on somebody else keeps the tag, whichever wording carries it.
    [InlineData("Icy Manipulator", "Artifact", "{1}, {T}: Tap target artifact, creature, or land.")]
    [InlineData("Frozen Solid", "Enchantment — Aura", "Enchant creature\nEnchanted creature doesn't untap during its controller's untap step.")]
    [InlineData("Kismet", "Enchantment", "Artifacts, creatures, and lands your opponents control enter tapped.\nEnchanted creature can't attack or block.")]
    public void Classify_LockOnSomebodyElse_StaysPacify(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Two or more cards, or a draw you can come back to.
    [InlineData("Divination", "Sorcery", "Draw two cards.")]
    [InlineData("Ancestral Recall", "Instant", "Target player draws three cards.")]
    // The user's own example: an activated draw on a body is card advantage
    // because nothing stops you doing it again.
    [InlineData("Azure Drake", "Creature — Drake", "Flying\n{3}{U}: Draw a card.")]
    [InlineData("Ophidian", "Creature — Serpent", "Whenever this creature deals combat damage to a player, you may draw a card.")]
    [InlineData("Howling Mine", "Artifact", "At the beginning of each player's draw step, that player draws a card.")]
    public void Classify_RepeatableOrMultipleDraw_IsCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    [Theory]
    // One card off a spell you cast replaces the spell: parity, not advantage.
    [InlineData("Eject", "Instant", "This spell can't be countered.\nReturn target nonland permanent to its owner's hand.\nDraw a card.")]
    [InlineData("Airbending Lesson", "Sorcery", "Airbend target nonland permanent.\nDraw a card.")]
    // An enters-the-battlefield draw fires once, so it is the same one-shot
    // replacement as a cantrip — "whenever" would be a different matter.
    [InlineData("Wall of Omens", "Creature — Wall", "Defender\nWhen this creature enters, draw a card.")]
    public void Classify_SingleOneShotDraw_IsNotCardAdvantage(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.CardAdvantage));
    }

    private static Card MakeCard(string name, string typeLine, string oracleText, List<string>? keywords = null)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = typeLine,
            OracleText = oracleText,
            Keywords = keywords,
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 1,
            Colors = [],
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
