using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Two ways a card burns a face that the rules were not reading. Ruled
    // 2026-09-15; together they were 29 of Burn's 101 misses.
    [InlineData("Corroding Dragonstorm", "Enchantment",
        "When this enchantment enters, each opponent loses 2 life and you gain 2 life.")]
    [InlineData("Cat-Gator", "Creature — Cat Crocodile",
        "Lifelink\nWhen this creature enters, it deals damage equal to the number of Swamps you control to any target.")]
    public void Classify_LifeLossAndCountedDamage_AreBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Fact]
    public void Classify_AHastyCreatureBackInHandAtYourEndStep_IsBurnNotBounce()
    {
        // "Come il viashino" (2026-09-29): the 2026-09-20 ruling on haste that
        // is gone at the end of the turn, read for "YOUR end step" too.
        CardEffect result = EffectClassifier.Classify(MakeCard(
            "Fleeting Effigy", "Creature — Elemental",
            "Haste\nAt the beginning of your end step, return this creature to its owner's hand. (Return it only "
            + "if it's on the battlefield.)\n{2}{R}: This creature gets +2/+0 until end of turn."));

        Assert.True(result.HasFlag(CardEffect.Burn));
        Assert.False(result.HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // "Any target" is the one wording that is both at once, because it can be
    // pointed at a face or at a creature. Scaling and fixed amounts read the
    // same, which is why [\dX] is in the pattern. Disintegrate's CURRENT oracle
    // text says "any target" — the creature-only wording is the Alpha printing,
    // and using it here once pinned the wrong behaviour.
    [InlineData("Blaze", "Sorcery", "Blaze deals X damage to any target.")]
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.")]
    [InlineData("Disintegrate", "Sorcery",
        "Disintegrate deals X damage to any target. If it's a creature, it can't be regenerated this turn, and if it would die this turn, exile it instead.")]
    public void Classify_DamageAtAnyTarget_IsBothBurnAndRemoval(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Burn));
        Assert.True(result.HasFlag(CardEffect.Removal));
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
    // Seven rulings on 2026-09-20, each read off a family that had split in the
    // hand tags. Damage divided among bare "TARGETS" can pick a face; the word
    // order runs both ways and the rule could only read one of them; the old
    // punishers name a face as "that permanent's controller"; life loss counts
    // from TWO; "loses life equal to" is a burn when somebody else is losing it;
    // the upkeep tax is a burn however slowly it burns; and life paid to stop a
    // card is life lost.
    [InlineData("Rolling Thunder", "Sorcery",
        "Rolling Thunder deals X damage divided as you choose among any number of targets.")]
    [InlineData("Storm Seeker", "Instant",
        "Storm Seeker deals damage to target player equal to the number of cards in that player's hand.")]
    [InlineData("Psychic Venom", "Enchantment — Aura",
        "Enchant land\nWhenever enchanted land becomes tapped, this Aura deals 2 damage to that land's controller.")]
    [InlineData("Vein Ripper", "Creature — Vampire Horror",
        "Flying\nWard—Sacrifice a creature.\nWhenever a creature dies, target opponent loses 2 life and you gain 2 life.")]
    [InlineData("Death Watch", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, its controller loses life equal to its power and "
        + "you gain life equal to its toughness.")]
    [InlineData("Karma", "Enchantment",
        "At the beginning of each player's upkeep, this enchantment deals damage to that player equal to "
        + "the number of Swamps they control.")]
    [InlineData("Breathstealer's Crypt", "Enchantment",
        "If a player would draw a card, instead they draw a card and reveal it. If it's a creature card, "
        + "that player discards it unless they pay 3 life.")]
    public void Classify_TheSevenWaysACardBurnsAFace_AreBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Two more the same day. A creature with HASTE that is gone at the end of
    // the turn it arrived attacks once and then is not there any more, so what
    // it really did was put its power on a face — whether it dies (Ball
    // Lightning) or bounces (Viashino Sandstalker). Icequake, the LAND-DESTROYER
    // whose charge on the controller was ruled Burn the same day, was pinned
    // here until 2026-11-06, when the charge on a kill was ruled a rider for
    // lands too ("Mai Burn, terre comprese"); it moved to
    // Classify_TheNovember6Definition_IsNotBurn.
    [InlineData("Ball Lightning", "Creature — Elemental",
        "Trample\nHaste\nAt the beginning of the end step, sacrifice this creature.")]
    [InlineData("Viashino Sandstalker", "Creature — Viashino Warrior",
        "Haste\nAt the beginning of the end step, return this creature to its owner's hand.")]
    public void Classify_AShotWearingLegs_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Fact]
    // …and the drawback handed to SOMEBODY ELSE'S creature is not the card
    // shooting anything. Strago and Relm, Skirk Alarmist and Apprentice
    // Necromancer all print those words inside quotes, about a creature they
    // just gave you.
    public void Classify_AnEndOfTurnDrawbackGrantedAway_IsNotBurn()
    {
        Card card = MakeCard("Strago and Relm", "Legendary Creature — Human Wizard",
            "Sketch and Lore — {2}{R}, {T}: Target opponent exiles cards from the top of their library "
            + "until they exile an instant, sorcery, or creature card. You may cast that card without "
            + "paying its mana cost. If you cast a creature spell this way, it gains haste and \"At the "
            + "beginning of the end step, sacrifice this creature.\" Activate only as a sorcery.");

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // And the seven edges each of those rulings has to leave alone. Damage
    // divided among "target CREATURES" can never reach a player; Fiery Justice
    // hands the five life straight back, which the human ruled an exception on
    // purpose; ONE point of life ONCE is a rider (a repeated one is Burn since
    // 2026-09-29, see Classify_ARepeatedOnePointDrain_IsBurn, where Sanguine
    // Syphoner moved); "YOU lose life equal to" is the price of the card; a
    // charge attached to a kill is a removal spell with a bonus; and what a TOKEN
    // does is the token's.
    [InlineData("Pyrokinesis", "Instant",
        "You may exile a red card from your hand rather than pay this spell's mana cost.\n"
        + "Pyrokinesis deals 4 damage divided as you choose among any number of target creatures.")]
    [InlineData("Fiery Justice", "Sorcery",
        "Fiery Justice deals 5 damage divided as you choose among any number of targets. "
        + "Target opponent gains 5 life.")]
    [InlineData("Reanimate", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control. "
        + "You lose life equal to that card's mana value.")]
    [InlineData("Reign of Terror", "Sorcery",
        "Destroy all green creatures or all white creatures. They can't be regenerated. "
        + "You lose 2 life for each creature that died this way.")]
    [InlineData("Detonate", "Sorcery",
        "Destroy target artifact with mana value X. It can't be regenerated. "
        + "Detonate deals X damage to that artifact's controller.")]
    [InlineData("Mysidian Elder", "Creature — Human Wizard",
        "When this creature enters, create a 0/1 black Wizard creature token with \"Whenever you cast a "
        + "noncreature spell, this token deals 1 damage to each opponent.\"")]
    // One point of life drained ONCE is still a rider (a spell's mode), and a
    // drain printed inside the quotes of a token the card creates is the token's
    // (Keimi), read with the repeated drain on 2026-09-29.
    [InlineData("Ebony Charm", "Instant",
        "Choose one —\n• Target opponent loses 1 life and you gain 1 life.\n• Exile up to three target cards "
        + "from a single graveyard.\n• Target creature gains fear until end of turn. (It can't be blocked except "
        + "by artifact creatures and/or black creatures.)")]
    [InlineData("Tatsunari, Toad Rider", "Legendary Creature — Human Ninja",
        "Whenever you cast an enchantment spell, if you don't control a creature named Keimi, create Keimi, a "
        + "legendary 3/3 black and green Frog creature token with \"Whenever you cast an enchantment spell, each "
        + "opponent loses 1 life and you gain 1 life.\"\n{1}{G/U}: Tatsunari and target Frog you control can't be "
        + "blocked this turn except by creatures with flying or reach.")]
    public void Classify_WhatOnlyLooksLikeBurn_IsNot(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // ONE point of life that comes AGAIN AND AGAIN is Burn, revised 2026-09-29
    // ("ripetuto sì, singolo no") from the 09-20 threshold of two: a trigger, an
    // activation, or a keyword whose reminder says so (extort).
    [InlineData("Sanguine Syphoner", "Creature — Vampire Warlock",
        "Whenever this creature attacks, each opponent loses 1 life and you gain 1 life.")]
    [InlineData("Vengeful Bloodwitch", "Creature — Vampire Warlock",
        "Whenever this creature or another creature you control dies, target opponent loses 1 life and you "
        + "gain 1 life.")]
    [InlineData("Mirkwood Bats", "Creature — Bat",
        "Flying\nWhenever you create or sacrifice a token, each opponent loses 1 life.")]
    [InlineData("Blood Hustler", "Creature — Vampire Rogue",
        "Whenever you commit a crime, put a +1/+1 counter on this creature. This ability triggers only once "
        + "each turn. (Targeting opponents, anything they control, and/or cards in their graveyards is a "
        + "crime.)\n{3}{B}: Target opponent loses 1 life and you gain 1 life.")]
    [InlineData("Syndicate Heavy", "Creature — Giant Rogue",
        "Extort (Whenever you cast a spell, you may pay {W/B}. If you do, each opponent loses 1 life and you "
        + "gain that much life.)\nAt the beginning of each end step, if you gained 4 or more life this turn, "
        + "investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    // A drain on a TRIBE the card makes itself, or IS and fires on itself
    // ("a", not "another"), enables itself (2026-10-13).
    [InlineData("Aatchik, Emerald Radian", "Legendary Creature — Insect Druid",
        "When Aatchik enters, create a 1/1 green Insect creature token for each artifact and/or creature card "
        + "in your graveyard.\nWhenever another Insect you control dies, put a +1/+1 counter on Aatchik. Each "
        + "opponent loses 1 life.")]
    [InlineData("Leeching Sliver", "Creature — Sliver",
        "Whenever a Sliver you control attacks, defending player loses 1 life.")]
    public void Classify_ARepeatedOnePointDrain_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // …but a drain waiting on OTHER members of a tribe is not, ruled
    // 2026-10-13 ("dipendono da condizioni esterne").
    [InlineData("Obyra, Dreaming Duelist", "Legendary Creature — Faerie Warrior",
        "Flash\nFlying\nWhenever another Faerie you control enters, each opponent loses 1 life.")]
    // Read 2026-11-10 under Q7: damage behind a card OF A TRIBE revealed from the
    // hand waits on a tribal deck (Filter alone by hand, 11-06). A Fact until
    // that day, when it became a Theory to take the second card.
    [InlineData("Invasion of the Giants", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Scry "
        + "2.\nII — Draw a card. Then you may reveal a Giant card from your hand. When you do, this Saga deals 2 "
        + "damage to target opponent or planeswalker.\nIII — The next Giant spell you cast this turn costs {2} "
        + "less to cast.")]
    public void Classify_ADrainOnSomebodyElseOfTheTribe_IsNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // "Each of UP TO TWO targets", and a battle beside the opponent.
    [InlineData("Chandra, Hope's Beacon", "Legendary Planeswalker — Chandra",
        "Whenever you cast an instant or sorcery spell, copy it. You may choose new targets for the copy. This "
        + "ability triggers only once each turn.\n+2: Add two mana in any combination of colors.\n+1: Exile the "
        + "top five cards of your library. Until the end of your next turn, you may cast an instant or sorcery "
        + "spell from among those exiled cards.\n−X: Chandra deals X damage to each of up to two targets.")]
    [InlineData("Invasion of Regatha // Disciples of the Inferno", "Battle — Siege // Creature — Human Monk",
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's "
        + "defeated, exile it, then cast it transformed.)\nWhen this Siege enters, it deals 4 damage to another "
        + "target battle or opponent and 1 damage to up to one target creature.")]
    // The owner of a spell cast from an opponent's library.
    [InlineData("The Ruinous Powers", "Enchantment",
        "At the beginning of your upkeep, choose an opponent at random. Exile the top card of that player's "
        + "library. Until end of turn, you may play that card and you may spend mana as though it were mana of "
        + "any color to cast it. When you cast a spell this way, its owner loses life equal to its mana value.")]
    // A relative clause longer than the window.
    [InlineData("Gollum, Obsessed Stalker", "Legendary Creature — Halfling Horror",
        "Skulk (This creature can't be blocked by creatures with greater power.)\nAt the beginning of your end "
        + "step, each opponent dealt combat damage this game by a creature named Gollum, Obsessed Stalker loses "
        + "life equal to the amount of life you gained this turn.")]
    // Excess damage is the burn that goes past the kill.
    [InlineData("Gandalf's Sanction", "Sorcery",
        "Gandalf's Sanction deals X damage to target creature, where X is the number of instant and sorcery "
        + "cards in your graveyard. Excess damage is dealt to that creature's controller instead.")]
    // The life the enchanted creature's controller pays every upkeep.
    [InlineData("Morgul-Knife Wound", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets -3/-0 and has \"At the beginning of your upkeep, exile this "
        + "creature unless you pay 2 life.\"")]
    // A hasty token gone at the next end step.
    [InlineData("Urabrask's Forge", "Artifact",
        "At the beginning of combat on your turn, put an oil counter on this artifact, then create an X/1 red "
        + "Phyrexian Horror creature token with trample and haste, where X is the number of oil counters on this "
        + "artifact. Sacrifice that token at the beginning of the next end step.")]
    [InlineData("Daring Piracy", "Enchantment",
        "At the beginning of combat on your turn, create a 1/1 red Pirate creature token with menace and haste. "
        + "Exile it at the beginning of the next end step.")]
    [InlineData("Feral Lightning", "Sorcery",
        "Create three 3/1 red Elemental creature tokens with haste. Exile them at the beginning of the next end "
        + "step.")]
    [InlineData("Thatcher Revolt", "Sorcery",
        "Create three 1/1 red Human creature tokens with haste. Sacrifice those tokens at the beginning of the "
        + "next end step.")]
    // "Each opponent WHO …" past the window.
    [InlineData("Grim Reminder", "Instant",
        "Search your library for a nonland card and reveal it. Each opponent who cast a spell this turn with the "
        + "same name as that card loses 6 life. Then shuffle.\n{B}{B}: Return this card from your graveyard to "
        + "your hand. Activate only during your upkeep.")]
    public void Classify_TheOctoberBurnGaps_AreBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // The life a token's quoted ability takes is the token's.
    [InlineData("Kaito, Dancing Shadow", "Legendary Planeswalker — Kaito",
        "Whenever one or more creatures you control deal combat damage to a player, you may return one of them "
        + "to its owner's hand. If you do, you may activate loyalty abilities of Kaito twice this turn rather "
        + "than only once.\n+1: Up to one target creature can't attack or block until your next turn.\n0: Draw "
        + "a card.\n−2: Create a 2/2 colorless Drone artifact creature token with deathtouch and \"When this "
        + "token leaves the battlefield, each opponent loses 2 life and you gain 2 life.\"")]
    // Scarring Memories and Kiki-Jiki were here until 2026-10-23, when the list
    // that ends in a life loss and the hasty token copy were both ruled Burn —
    // see Classify_TheOctober23BurnPass_IsBurn.
    public void Classify_TheOctoberBurnMisreadings_AreNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-10-16 (B2): an opponent or a target player losing half their life
    // is always Burn ("Sempre Burn"). Readings of the same pass: damage to each of
    // your opponents, a life gain answered with that much life lost, a drain on
    // entering that returns itself to hand, damage divided among up to N targets,
    // and Ob Nixilis's +1 drain, which B1 leaves standing.
    [InlineData("Alpharael, Stonechosen", "Legendary Creature — Human Cleric",
        "Ward—Discard a card at random.\nVoid — Whenever Alpharael attacks, if a nonland permanent left the "
        + "battlefield this turn or a spell was warped this turn, defending player loses half their life, "
        + "rounded up.")]
    [InlineData("Betor, Kin to All", "Legendary Creature — Spirit Dragon",
        "Flying\nAt the beginning of your end step, if creatures you control have total toughness 10 or "
        + "greater, draw a card. Then if creatures you control have total toughness 20 or greater, untap each "
        + "creature you control. Then if creatures you control have total toughness 40 or greater, each "
        + "opponent loses half their life, rounded up.")]
    [InlineData("Aurelia, the Law Above", "Legendary Creature — Angel",
        "Flying, vigilance, haste\nWhenever a player attacks with three or more creatures, you draw a "
        + "card.\nWhenever a player attacks with five or more creatures, Aurelia deals 3 damage to each of your "
        + "opponents and you gain 3 life.")]
    [InlineData("Enduring Tenacity", "Enchantment Creature — Snake Glimmer",
        "Whenever you gain life, target opponent loses that much life.\nWhen Enduring Tenacity dies, if it was "
        + "a creature, return it to the battlefield under its owner's control. It's an enchantment. (It's not a "
        + "creature.)")]
    [InlineData("Intimidation Campaign", "Enchantment",
        "When this enchantment enters, each opponent loses 1 life, you gain 1 life, and you draw a "
        + "card.\nWhenever you commit a crime, you may return this enchantment to its owner's hand. (It returns "
        + "only from the battlefield. Targeting opponents, anything they control, and/or cards in their "
        + "graveyards is a crime.)")]
    [InlineData("Blaster Hulk", "Artifact Creature — Pirate",
        "This spell costs {1} less to cast for each {E} (energy counter) you've paid or lost this "
        + "turn.\nHaste\nWhenever this creature attacks, you get {E}{E}, then you may pay eight {E}. When you do, "
        + "this creature deals 8 damage divided as you choose among up to eight targets.")]
    [InlineData("Ob Nixilis, the Adversary", "Legendary Planeswalker — Nixilis",
        "Casualty X. The copy isn't legendary and has starting loyalty X. (As you cast this spell, you may "
        + "sacrifice a creature with power X. When you do, copy this spell. The copy becomes a token.)\n+1: Each "
        + "opponent loses 2 life unless they discard a card. If you control a Demon or Devil, you gain 2 "
        + "life.\n−2: Create a 1/1 red Devil creature token with \"When this token dies, it deals 1 damage to any "
        + "target.\"\n−7: Target player draws seven cards and loses 7 life.")]
    public void Classify_TheOctober16BurnPass_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-10-16: a player who draws N and loses N life is never burnt (B1,
    // "Mai Burn"), and Blooming Blast's gift is no Burn (B3). Monumental
    // Corruption was pinned here until 2026-11-05, when "target player draws X
    // cards and loses X life" with a VARIABLE X was ruled Burn ("Sì: target
    // player + X = Burn"); it moved to
    // Classify_TheNovember5Ruling_TargetPlayerDrawsXAndLosesX_IsBurn. Half the
    // life of its owner or of each player stays out of B2. A ward paid in life
    // taxes their spell (0 of the 6 reviewed). Vengeful Tracker, a drain that
    // punishes an opponent's sacrifice, was pinned here until 2026-11-06, when it
    // was ruled Burn like every punisher; it moved to
    // Classify_TheNovember6Definition_IsBurn.
    [InlineData("Blooming Blast", "Instant",
        "Gift a Treasure (You may promise an opponent a gift as you cast this spell. If you do, they create a "
        + "Treasure token before its other effects. It's an artifact with \"{T}, Sacrifice this token: Add one "
        + "mana of any color.\")\nBlooming Blast deals 2 damage to target creature. If the gift was promised, "
        + "Blooming Blast also deals 3 damage to that creature's controller.")]
    [InlineData("Personal Incarnation", "Creature — Avatar Incarnation",
        "{0}: The next 1 damage that would be dealt to this creature this turn is dealt to its owner instead. "
        + "Only this creature's owner may activate this ability.\nWhen this creature dies, its owner loses half "
        + "their life, rounded up.")]
    [InlineData("Shahrazad", "Sorcery",
        "Players play a Magic subgame, using their libraries as their decks. Each player who doesn't win the "
        + "subgame loses half their life, rounded up.")]
    [InlineData("Zul Ashur, Lich Lord", "Legendary Creature — Zombie Warlock",
        "Ward—Pay 2 life. (Whenever this creature becomes the target of a spell or ability an opponent "
        + "controls, counter it unless that player pays 2 life.)\n{T}: You may cast target Zombie creature card "
        + "from your graveyard this turn.")]
    public void Classify_TheOctober16BurnPass_IsNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-10-23: haste and gone at the next end step is Burn for the card
    // itself and for every token or copy it makes ("tutto allineato"), which
    // overturned the Kiki-Jiki exclusion of 2026-10-14: a copy (Kiki-Jiki), a
    // plain hasty token (Lagomos, Chandra), the populate token (Determined
    // Iteration), encore and unearth and blitz handed to other cards (Graywater's
    // Fixer, Sedris, Henzie). A life loss at the end of a list is Burn (Dusk
    // Mangler, Scarring Memories corrected with it). Readings of the same day: a
    // source's controller in a punisher, damage equal to a count divided among any
    // number of targets, a who clause before equal to, an emblem handed to an
    // opponent, an upkeep drain handed to an enchanted permanent, and/or players,
    // a creature token or player.
    [InlineData("Kiki-Jiki, Mirror Breaker", "Legendary Creature — Goblin Shaman",
        "Haste\n{T}: Create a token that's a copy of target nonlegendary creature you control, except it has "
        + "haste. Sacrifice it at the beginning of the next end step.")]
    [InlineData("Lagomos, Hand of Hatred", "Legendary Creature — Human Shaman",
        "At the beginning of combat on your turn, create a 2/1 red Elemental creature token with trample and "
        + "haste. Sacrifice it at the beginning of the next end step.\n{T}: Search your library for a card, put "
        + "it into your hand, then shuffle. Activate only if five or more creatures died this turn.")]
    [InlineData("Chandra, Acolyte of Flame", "Legendary Planeswalker — Chandra",
        "0: Put a loyalty counter on each red planeswalker you control.\n0: Create two 1/1 red Elemental "
        + "creature tokens. They gain haste. Sacrifice them at the beginning of the next end step.\n−2: You may "
        + "cast target instant or sorcery card with mana value 3 or less from your graveyard. If that spell "
        + "would be put into your graveyard, exile it instead.")]
    [InlineData("Determined Iteration", "Enchantment",
        "At the beginning of combat on your turn, populate. The token created this way gains haste. Sacrifice "
        + "it at the beginning of the next end step. (To populate, create a token that's a copy of a creature "
        + "token you control.)")]
    [InlineData("Graywater's Fixer", "Creature — Lizard Mercenary",
        "Each outlaw creature card in your graveyard has encore {X}, where X is its mana value. (Exile it and "
        + "pay its encore cost: For each opponent, create a token copy that attacks that opponent this turn if "
        + "able. They gain haste. Sacrifice them at the beginning of the next end step. Activate only as a "
        + "sorcery.)")]
    [InlineData("Sedris, the Traitor King", "Legendary Creature — Zombie Warrior",
        "Each creature card in your graveyard has unearth {2}{B}. ({2}{B}: Return the card to the "
        + "battlefield. The creature gains haste. Exile it at the beginning of the next end step or if it would "
        + "leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Henzie \"Toolbox\" Torre", "Legendary Creature — Devil Rogue",
        "Each creature spell you cast with mana value 4 or greater has blitz. The blitz cost is equal to its "
        + "mana cost. (You may choose to cast that spell for its blitz cost. If you do, it gains haste and "
        + "\"When this creature dies, draw a card.\" Sacrifice it at the beginning of the next end step.)\nBlitz "
        + "costs you pay cost {1} less for each time you've cast your commander from the command zone this "
        + "game.")]
    [InlineData("Dusk Mangler", "Creature — Horror",
        "As an additional cost to cast this spell, sacrifice a creature, discard a card, or pay 4 life.\nWhen "
        + "this creature enters, each opponent sacrifices a creature of their choice, discards a card, and "
        + "loses 4 life.")]
    [InlineData("Scarring Memories", "Sorcery — Lesson",
        "You may cast this spell as though it had flash if you control an attacking legendary "
        + "creature.\nTarget opponent sacrifices a creature of their choice, discards a card, and loses 3 life.")]
    [InlineData("Archfiend of Spite", "Creature — Demon",
        "Flying\nWhenever a source an opponent controls deals damage to this creature, that source's "
        + "controller loses that much life unless they sacrifice that many permanents of their choice.\nMadness "
        + "{3}{B}{B} (If you discard this card, discard it into exile. When you do, cast it for its madness "
        + "cost or put it into your graveyard.)")]
    [InlineData("Orca, Siege Demon", "Legendary Creature — Demon",
        "Trample\nWhenever another creature dies, put a +1/+1 counter on Orca.\nWhen Orca dies, it deals damage "
        + "equal to its power divided as you choose among any number of targets.")]
    [InlineData("Spiteful Repossession", "Sorcery",
        "Spiteful Repossession deals damage to each opponent who controls more lands than you equal to the "
        + "difference. Then create a number of Treasure tokens equal to the damage dealt this way.")]
    [InlineData("Chandra, Awakened Inferno", "Legendary Planeswalker — Chandra",
        "This spell can't be countered.\n+2: Each opponent gets an emblem with \"At the beginning of your "
        + "upkeep, this emblem deals 1 damage to you.\"\n−3: Chandra deals 3 damage to each non-Elemental "
        + "creature.\n−X: Chandra deals X damage to target creature or planeswalker. If a permanent dealt damage "
        + "this way would die this turn, exile it instead.")]
    [InlineData("Clawing Torment", "Enchantment — Aura",
        "Enchant artifact or creature\nAs long as enchanted permanent is a creature, it gets -1/-1 and can't "
        + "block.\nEnchanted permanent has \"At the beginning of your upkeep, you lose 1 life.\"")]
    [InlineData("Soulfire Eruption", "Sorcery",
        "Choose any number of target creatures, planeswalkers, and/or players. For each of them, exile the "
        + "top card of your library, then Soulfire Eruption deals damage equal to that card's mana value to "
        + "that permanent or player. You may play the exiled cards until the end of your next turn.")]
    [InlineData("Coalborn Entity", "Creature — Elemental",
        "{2}{R}: This creature deals 1 damage to target creature token, player, or planeswalker.")]
    // Widened 2026-10-27 ('Allarga la regola alla libreria'): a creature out of
    // your LIBRARY counts as made, milled, exiled off the top or searched for.
    [InlineData("Random Encounter", "Sorcery",
        "Shuffle your library, then mill four cards. Put each creature card milled this way onto the "
        + "battlefield. They gain haste. At the beginning of the next end step, return those creatures to their "
        + "owner's hand.\nFlashback {6}{R}{R} (You may cast this card from your graveyard for its flashback "
        + "cost. Then exile it.)")]
    [InlineData("Anzrag's Rampage", "Sorcery",
        "Destroy all artifacts you don't control, then exile the top X cards of your library, where X is the "
        + "number of artifacts that were put into graveyards from the battlefield this turn. You may put a "
        + "creature card exiled this way onto the battlefield. It gains haste. Return it to your hand at the "
        + "beginning of the next end step.")]
    [InlineData("Zirilan of the Claw", "Legendary Creature — Lizard Shaman",
        "{1}{R}{R}, {T}: Search your library for a Dragon permanent card, put that card onto the battlefield, "
        + "then shuffle. That Dragon gains haste until end of turn. Exile it at the beginning of the next end "
        + "step.")]
    public void Classify_TheOctober23BurnPass_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-10-23. Corpse Dance (a graveyard) and Through the Breach (a
    // hand), the creatures the card only TAKES, were pinned here as not burn
    // until the graveyard part was overturned on 2026-11-05 and the hand on
    // 2026-11-06; they moved to the two IsBurn theories of those days.
    // The shield that counters a spell unless its player pays
    // life is Parnesse's Protection, as ward is. "If it has unearth" grants
    // nothing, an emblem you get is yours, and one point at the end of a list
    // stays under the threshold.
    [InlineData("Parnesse, the Subtle Brush", "Legendary Creature — Vampire Wizard",
        "Whenever you or a permanent you control becomes the target of a spell or ability an opponent "
        + "controls, counter that spell or ability unless that player pays 4 life.\nWhenever you copy a spell, "
        + "up to one target opponent may also copy that spell. They may choose new targets for that copy.")]
    [InlineData("Meticulous Excavation", "Enchantment",
        "{2}{W}: Return target permanent you control to its owner's hand. If it has unearth, instead exile "
        + "it, then return that card to its owner's hand. Activate only during your turn.")]
    [InlineData("Ajani Steadfast", "Legendary Planeswalker — Ajani",
        "+1: Until end of turn, up to one target creature gets +1/+1 and gains first strike, vigilance, and "
        + "lifelink.\n−2: Put a +1/+1 counter on each creature you control and a loyalty counter on each other "
        + "planeswalker you control.\n−7: You get an emblem with \"If a source would deal damage to you or a "
        + "planeswalker you control, prevent all but 1 of that damage.\"")]
    [InlineData("Mind Drain", "Sorcery",
        "Target opponent discards two cards, mills a card, and loses 1 life. You gain 1 life. (To mill a "
        + "card, a player puts the top card of their library into their graveyard.)")]
    public void Classify_TheOctober23BurnPass_IsNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // The same ruling by KEYWORD, because these four print unearth, blitz, encore
    // and dash without the reminder text. Mask of the Jadecrafter's unearth
    // returns an artifact nobody attacks with, so the keyword asks for a creature.
    [InlineData("Phyrexian Dragon Engine", "Artifact Creature — Phyrexian Dragon",
        "Double strike\nWhen this creature enters from your graveyard, you may discard your hand. If you do, "
        + "draw three cards.\nUnearth {3}{R}{R}\n(Melds with Mishra, Claimed by Gix.)")]
    [InlineData("Ziatora's Envoy", "Creature — Lizard Warrior",
        "Trample\nWhenever this creature deals combat damage to a player, look at the top card of your "
        + "library. You may play a land from the top of your library or cast a spell with mana value less than "
        + "or equal to the damage dealt from the top of your library without paying its mana cost. If you "
        + "don't, put that card into your hand.\nBlitz {2}{B}{R}{G}")]
    [InlineData("Broodmate Tyrant", "Creature — Dragon",
        "Flying\nWhen this creature enters, create a 5/5 red Dragon creature token with flying.\nEncore "
        + "{5}{B}{R}{G}")]
    [InlineData("Death-Greeter's Champion", "Creature — Human Warrior",
        "Dash {3}{R} (You may cast this spell for its dash cost. If you do, it gains haste, and it's returned "
        + "from the battlefield to its owner's hand at the beginning of the next end step.)\nBackup 1 (When this "
        + "creature enters, put a +1/+1 counter on target creature. If that's another creature, it gains the "
        + "following ability until end of turn.)\nDouble strike")]
    [InlineData("Mask of the Jadecrafter", "Artifact",
        "{X}, {T}, Sacrifice this artifact: Create an X/X colorless Golem artifact creature token. Activate "
        + "only as a sorcery.\nUnearth {2}{G} ({2}{G}: Return this card from your graveyard to the battlefield. "
        + "Exile it at the beginning of the next end step or if it would leave the battlefield. Unearth only as "
        + "a sorcery.)")]
    public void Classify_AHastyAndGoneKeyword_IsBurnOnlyOnACreature(string name, string typeLine, string oracle)
    {
        string keyword = name switch
        {
            "Phyrexian Dragon Engine" or "Mask of the Jadecrafter" => "Unearth",
            "Ziatora's Envoy" => "Blitz",
            "Broodmate Tyrant" => "Encore",
            _ => "Dash",
        };
        bool creature = typeLine.Contains("Creature", StringComparison.Ordinal);

        Assert.Equal(creature, EffectClassifier.Classify(MakeCard(name, typeLine, oracle, [keyword])).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Read 2026-11-05 under the 2026-09-20 ruling (damage divided among bare
    // targets is Burn): one or two targets (Ral, Monsoon Mage, tagged by hand),
    // and a player in an each-of-up-to list of separate targets (Volatile
    // Arsonist).
    [InlineData("Ral, Monsoon Mage // Ral, Leyline Prodigy", "Legendary Creature — Human Wizard // Legendary Planeswalker — Ral",
        "Instant and sorcery spells you cast cost {1} less to cast.\nWhenever you cast an instant or sorcery "
        + "spell during your turn, flip a coin. If you lose the flip, Ral deals 1 damage to you. If you win the "
        + "flip, you may exile Ral. If you do, return him to the battlefield transformed under his owner's "
        + "control.\nRal enters with an additional loyalty counter on him for each instant and sorcery spell "
        + "you've cast this turn.\n+1: Until your next turn, instant and sorcery spells you cast cost {1} less "
        + "to cast.\n−2: Ral deals 2 damage divided as you choose among one or two targets. Draw a card if you "
        + "control a blue permanent other than Ral.\n−8: Exile the top eight cards of your library. You may cast "
        + "instant and sorcery spells from among them this turn without paying their mana costs.")]
    [InlineData("Forked Bolt", "Sorcery",
        "Forked Bolt deals 2 damage divided as you choose among one or two targets.")]
    [InlineData("Electrolyze", "Instant",
        "Electrolyze deals 2 damage divided as you choose among one or two targets.\nDraw a card.")]
    [InlineData("Fire // Ice", "Instant // Instant",
        "Fire deals 2 damage divided as you choose among one or two targets.\nTap target permanent.\nDraw a "
        + "card.")]
    [InlineData("Volatile Arsonist // Dire-Strain Anarchist", "Creature — Human Werewolf // Creature — Werewolf",
        "Menace, haste\nWhenever this creature attacks, it deals 1 damage to each of up to one target "
        + "creature, up to one target player, and/or up to one target planeswalker.\nDaybound (If a player casts "
        + "no spells during their own turn, it becomes night next turn.)\nMenace, haste\nWhenever this creature "
        + "attacks, it deals 2 damage to each of up to one target creature, up to one target player, and/or up "
        + "to one target planeswalker.\nNightbound (If a player casts at least two spells during their own turn, "
        + "it becomes day next turn.)")]
    public void Classify_TheNovember5Readings_DamageDividedAmongOneOrTwoTargets_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Read 2026-11-10, two faces BurnTarget did not name, both Burn by hand on
    // 11-06: UP TO ONE target player on a loyalty ability that comes again, and an
    // opponent CHOSEN AT RANDOM.
    [InlineData("Chandra, Dressed to Kill", "Legendary Planeswalker — Chandra",
        "+1: Add {R}. Chandra deals 1 damage to up to one target player or planeswalker.\n+1: Exile the top "
        + "card of your library. If it's red, you may cast it this turn.\n−7: Exile the top five cards of your "
        + "library. You may cast red spells from among them this turn. You get an emblem with \"Whenever you "
        + "cast a red spell, this emblem deals X damage to any target, where X is the amount of mana spent to "
        + "cast that spell.\"")]
    [InlineData("Zaffai, Thunder Conductor", "Legendary Creature — Human Shaman",
        "Magecraft — Whenever you cast or copy an instant or sorcery spell, scry 1. If that spell's mana "
        + "value is 5 or greater, create a 4/4 blue and red Elemental creature token. If that spell's mana "
        + "value is 10 or greater, Zaffai deals 10 damage to an opponent chosen at random.")]
    public void Classify_TheNovember10Readings_UpToOnePlayerOrOneAtRandom_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Read 2026-11-05: the player a card chose earlier is a player the damage
    // lands on (The Lord of Pain, tagged by hand).
    [InlineData("The Lord of Pain", "Legendary Creature — Human Assassin",
        "Menace\nYour opponents can't gain life.\nWhenever a player casts their first spell each turn, choose "
        + "another target player. The Lord of Pain deals damage equal to that spell's mana value to the chosen "
        + "player.")]
    [InlineData("Saskia the Unyielding", "Legendary Creature — Human Soldier",
        "Vigilance, haste\nAs Saskia enters, choose a player.\nWhenever a creature you control deals combat "
        + "damage to a player, it deals that much damage to the chosen player.")]
    [InlineData("Stuffy Doll", "Artifact Creature — Construct",
        "Indestructible\nAs this creature enters, choose a player.\nWhenever this creature is dealt damage, it "
        + "deals that much damage to the chosen player.\n{T}: This creature deals 1 damage to itself.")]
    public void Classify_TheNovember5Readings_DamageToTheChosenPlayer_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Sawhorn Nemesis only doubles damage dealt to the chosen player. Dorothea,
    // Vengeful Victim: named 2026-11-05, a slip: the attacking token sacrificed at
    // end of combat is not Burn (1 tagged of 13 reviewed cards of that wording).
    [InlineData("Sawhorn Nemesis", "Creature — Dinosaur",
        "As this creature enters, choose a player.\nIf a source would deal damage to the chosen player or a "
        + "permanent they control, it deals double that damage instead.")]
    [InlineData("Dorothea, Vengeful Victim // Dorothea's Retribution", "Legendary Creature — Spirit // Enchantment — Aura",
        "Flying\nWhen Dorothea attacks or blocks, sacrifice it at end of combat.\nDisturb {1}{W}{U} (You may "
        + "cast this card from your graveyard transformed for its disturb cost.)\nEnchant creature\nEnchanted "
        + "creature has \"Whenever this creature attacks, create a 4/4 white Spirit creature token with flying "
        + "that's tapped and attacking. Sacrifice that token at end of combat.\"\nIf Dorothea's Retribution would "
        + "be put into a graveyard from anywhere, exile it instead.")]
    [InlineData("Kavaron Harrier", "Artifact Creature — Robot Soldier",
        "Whenever this creature attacks, you may pay {2}. If you do, create a 2/2 colorless Robot artifact "
        + "creature token that's tapped and attacking. Sacrifice that token at end of combat.")]
    [InlineData("Mordor Trebuchet", "Artifact Creature — Wall",
        "Defender\nWhenever you attack with one or more Goblins and/or Orcs, create a 2/1 colorless Construct "
        + "artifact creature token with flying named Ballistic Boulder that's tapped and attacking. Sacrifice "
        + "that token at end of combat.")]
    public void Classify_TheNovember5Readings_DoublingDamageOrTokensSacrificedAtEndOfCombat_AreNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-11-05 ("Sì: target player + X = Burn", asked on Eldritch Pact
    // against Pact of the Serpent and Monumental Corruption): a VARIABLE X pointed
    // at a player can be large and can be an opponent. Eldritch Pact is named;
    // Pact of the Serpent and Monumental Corruption are reached and handed back
    // with the tag.
    [InlineData("Eldritch Pact", "Sorcery",
        "Target player draws X cards and loses X life, where X is the number of cards in their graveyard.")]
    [InlineData("Pact of the Serpent", "Sorcery",
        "Choose a creature type. Target player draws X cards and loses X life, where X is the number of "
        + "creatures they control of the chosen type.")]
    [InlineData("Monumental Corruption", "Sorcery",
        "Target player draws X cards and loses X life, where X is the number of artifacts you control.")]
    [InlineData("Damnable Pact", "Sorcery",
        "Target player draws X cards and loses X life.")]
    public void Classify_TheNovember5Ruling_TargetPlayerDrawsXAndLosesX_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // B1 (2026-10-16, "Mai Burn") still holds for a fixed N pointed at a player
    // (Sign in Blood, Blood Pact, Insatiable Avarice, Vault Plunderer) and for YOU
    // drawing X and losing X (Estinien Varlineau, The Speed Demon, Painful
    // Truths).
    [InlineData("Sign in Blood", "Sorcery",
        "Target player draws two cards and loses 2 life.")]
    [InlineData("Blood Pact", "Instant",
        "Target player draws two cards and loses 2 life.")]
    [InlineData("Insatiable Avarice", "Sorcery",
        "Spree (Choose one or more additional costs.)\n+ {2} — Search your library for a card, then shuffle "
        + "and put that card on top.\n+ {B}{B} — Target player draws three cards and loses 3 life.")]
    [InlineData("Vault Plunderer", "Creature — Human Rogue",
        "When this creature enters, target player draws a card and loses 1 life.")]
    [InlineData("Estinien Varlineau", "Legendary Creature — Elf Warrior",
        "Whenever you cast a noncreature spell, put a +1/+1 counter on Estinien Varlineau. It gains flying "
        + "until end of turn.\nAt the beginning of your second main phase, you draw X cards and lose X life, "
        + "where X is the number of your opponents who were dealt combat damage by Estinien Varlineau or a "
        + "Dragon this turn.")]
    [InlineData("The Speed Demon", "Legendary Creature — Demon",
        "Flying, trample\nStart your engines! (If you have no speed, it starts at 1. It increases once on each "
        + "of your turns when an opponent loses life. Max speed is 4.)\nAt the beginning of your end step, you "
        + "draw X cards and lose X life, where X is your speed.")]
    [InlineData("Painful Truths", "Sorcery",
        "Converge — You draw X cards and lose X life, where X is the number of colors of mana spent to cast "
        + "this spell.")]
    public void Classify_TheNovember5Ruling_AFixedNOrYouDrawingX_StaysNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-11-05 ("Anche preso è Burn", asked on Balduvian Atrocity against
    // Shallow Grave and Corpse Dance): a creature card returned FROM A GRAVEYARD
    // with haste and sacrificed or exiled at the next end step is Burn beside its
    // Reanimate. This overturns the graveyard part of the 2026-10-23 rule (Corpse
    // Dance was pinned as not Burn until now); Balduvian Atrocity is named, Corpse
    // Dance, Shallow Grave, Push // Pull and Coalstoke Gearhulk are reached and
    // handed back with the tag.
    [InlineData("Balduvian Atrocity", "Creature — Phyrexian Berserker",
        "Kicker {R} (You may pay an additional {R} as you cast this spell.)\nMenace\nWhen this creature enters, "
        + "if it was kicked, return target creature card with mana value 3 or less from your graveyard to the "
        + "battlefield. It gains haste. Sacrifice it at the beginning of the next end step.")]
    [InlineData("Corpse Dance", "Instant",
        "Buyback {2} (You may pay an additional {2} as you cast this spell. If you do, put this card into "
        + "your hand as it resolves.)\nReturn the top creature card of your graveyard to the battlefield. That "
        + "creature gains haste until end of turn. Exile it at the beginning of the next end step.")]
    [InlineData("Shallow Grave", "Instant",
        "Return the top creature card of your graveyard to the battlefield. That creature gains haste until "
        + "end of turn. Exile it at the beginning of the next end step.")]
    [InlineData("Push // Pull", "Sorcery // Sorcery",
        "Destroy target tapped creature.\nPut up to two target creature cards from a single graveyard onto the "
        + "battlefield under your control. They gain haste until end of turn. Sacrifice them at the beginning "
        + "of the next end step.")]
    [InlineData("Coalstoke Gearhulk", "Artifact Creature — Construct",
        "Menace, deathtouch\nWhen this creature enters, put target creature card with mana value 4 or less "
        + "from a graveyard onto the battlefield under your control with a finality counter on it. That "
        + "creature gains menace, deathtouch, and haste. At the beginning of your next end step, exile that "
        + "creature.")]
    [InlineData("Macabre Mockery", "Instant",
        "Put target creature card from an opponent's graveyard onto the battlefield under your control. It "
        + "gets +2/+0 and gains haste until end of turn. Sacrifice it at the beginning of the next end step.")]
    [InlineData("Whip of Erebos", "Legendary Enchantment Artifact",
        "Creatures you control have lifelink.\n{2}{B}{B}, {T}: Return target creature card from your graveyard "
        + "to the battlefield. It gains haste. Exile it at the beginning of the next end step. If it would "
        + "leave the battlefield, exile it instead of putting it anywhere else. Activate only as a sorcery.")]
    public void Classify_TheNovember5Ruling_ACreatureFromAGraveyardWithHasteAndGone_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // What stands of 2026-10-23: a card that is no creature card is not burn
    // (Kami of Industry's artifact, Moira and Teshar's nonland permanent).
    // Through the Breach (a hand) and Swift Warkite (a return to hand) were
    // pinned here until 2026-11-06, when the hasty body gone at the end step was
    // ruled Burn wherever it came from; they moved to
    // Classify_TheNovember6Definition_IsBurn.
    [InlineData("Kami of Industry", "Creature — Spirit",
        "When this creature enters, return target artifact card with mana value 3 or less from your graveyard "
        + "to the battlefield. It gains haste. Sacrifice it at the beginning of the next end step.")]
    [InlineData("Moira and Teshar", "Legendary Creature — Phyrexian Spirit Bird",
        "Flying\nWhenever you cast a historic spell, return target nonland permanent card from your graveyard "
        + "to the battlefield. It gains haste. Exile it at the beginning of the next end step. If it would "
        + "leave the battlefield, exile it instead of putting it anywhere else. (Artifacts, legendaries, and "
        + "Sagas are historic.)")]
    public void Classify_TheNovember5Ruling_NotACreatureCard_StaysNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Burn redefined 2026-11-06 from Lightning Bolt: an OPPONENT loses life to
    // what the card does. A hasty body gone at the end step is the damage wherever
    // it came from, a hand (Through the Breach, Sneak Attack, Terminal Velocity's
    // quoted sacrifice), the command zone (Hellkite Courser) or a return to hand
    // (Swift Warkite). A punisher on an opponent's own sacrifice is Burn like
    // every punisher, the veto of 2026-10-16 removed (Vengeful Tracker, named;
    // Nazgul Battle-Mace's life paid to stop the card, tagged by hand and read
    // again).
    [InlineData("Through the Breach", "Instant — Arcane",
        "You may put a creature card from your hand onto the battlefield. That creature gains haste. "
        + "Sacrifice that creature at the beginning of the next end step.\nSplice onto Arcane {2}{R}{R} (As you "
        + "cast an Arcane spell, you may reveal this card from your hand and pay its splice cost. If you do, "
        + "add this card's effects to that spell.)")]
    [InlineData("Sneak Attack", "Enchantment",
        "{R}: You may put a creature card from your hand onto the battlefield. That creature gains haste. "
        + "Sacrifice the creature at the beginning of the next end step.")]
    [InlineData("Terminal Velocity", "Sorcery",
        "You may put an artifact or creature card from your hand onto the battlefield. That permanent gains "
        + "haste, \"When this permanent leaves the battlefield, it deals damage equal to its mana value to each "
        + "creature,\" and \"At the beginning of your end step, sacrifice this permanent.\"")]
    [InlineData("Hellkite Courser", "Creature — Dragon",
        "Flying\nWhen this creature enters, you may put a commander you own from the command zone onto the "
        + "battlefield. It gains haste. Return it to the command zone at the beginning of the next end step.")]
    [InlineData("Swift Warkite", "Creature — Dragon",
        "Flying\nWhen this creature enters, you may put a creature card with mana value 3 or less from your "
        + "hand or graveyard onto the battlefield. That creature gains haste. Return it to your hand at the "
        + "beginning of the next end step.")]
    [InlineData("Vengeful Tracker", "Creature — Human Detective",
        "Whenever an opponent sacrifices an artifact, this creature deals 2 damage to them.")]
    [InlineData("Nazgûl Battle-Mace", "Artifact — Equipment",
        "Equipped creature has menace, deathtouch, annihilator 1, and \"Whenever an opponent sacrifices a "
        + "nontoken permanent, put that card onto the battlefield under your control unless that player pays 3 "
        + "life.\" (Whenever a creature with annihilator 1 attacks, defending player sacrifices a permanent of "
        + "their choice.)\nEquip {3}")]
    public void Classify_TheNovember6Definition_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // The rest of 2026-11-06. The charge on the controller of what the card just
    // destroyed is a rider for a LAND too (Choking Sands, Icequake, Orcish Mine,
    // handed back), as it was for every other permanent. A body that joins an
    // attack already declared, tapped and attacking and gone at end of combat, is
    // an extra attacker and no burn (Arthur, Marigold Knight, named). A creature
    // STOLEN and sacrificed attacks the way Act of Treason does: Steal and
    // Removal, no Burn (Slave of Bolas).
    [InlineData("Choking Sands", "Sorcery",
        "Destroy target non-Swamp land. If that land was nonbasic, Choking Sands deals 2 damage to the land's "
        + "controller.")]
    [InlineData("Icequake", "Sorcery",
        "Destroy target land. If that land was a snow land, Icequake deals 1 damage to that land's "
        + "controller.")]
    [InlineData("Orcish Mine", "Enchantment — Aura",
        "Enchant land\nThis Aura enters with three ore counters on it.\nAt the beginning of your upkeep and "
        + "whenever enchanted land becomes tapped, remove an ore counter from this Aura.\nWhen the last ore "
        + "counter is removed from this Aura, destroy enchanted land and this Aura deals 2 damage to that "
        + "land's controller.")]
    [InlineData("Arthur, Marigold Knight", "Legendary Creature — Mouse Knight",
        "Haste\nWhenever Arthur and at least one other creature attack, look at the top six cards of your "
        + "library. You may put a creature card from among them onto the battlefield tapped and attacking. Put "
        + "the rest on the bottom of your library in a random order. Return that creature to its owner's hand "
        + "at end of combat.")]
    [InlineData("Slave of Bolas", "Sorcery",
        "Gain control of target creature. Untap that creature. It gains haste until end of turn. Sacrifice it "
        + "at the beginning of the next end step.")]
    // Ruled the same day ('regola tribale anche qui'): a hasty creature card of
    // one TYPE put from the hand and gone is fed only by a tribal deck, the
    // exception the drain on a tribe keeps.
    [InlineData("Incandescent Soulstoke", "Creature — Elemental Shaman",
        "Other Elemental creatures you control get +1/+1.\n{1}{R}, {T}: You may put an Elemental creature card "
        + "from your hand onto the battlefield. That creature gains haste until end of turn. Sacrifice it at "
        + "the beginning of the next end step.")]
    public void Classify_TheNovember6Definition_IsNotBurn(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }

    [Theory]
    // Ruled 2026-11-08 with the Steal definition ('Burn'): an exchange of life
    // totals is life the opponent loses, no permanent taken. Steal by hand until
    // then, all four named.
    [InlineData("Mirror Universe", "Artifact",
        "{T}, Sacrifice this artifact: Exchange life totals with target opponent. Activate only during your "
        + "upkeep.")]
    [InlineData("Mister Negative", "Legendary Creature — Human Villain",
        "Vigilance, lifelink\nDarkforce Inversion — When Mister Negative enters, you may exchange life totals "
        + "with target opponent. If you lost life this way, draw that many cards.")]
    [InlineData("Psychic Transfer", "Sorcery",
        "If the difference between your life total and target player's life total is 5 or less, exchange life "
        + "totals with that player.")]
    [InlineData("Profane Transfusion", "Sorcery",
        "Two target players exchange life totals. You create an X/X colorless Horror artifact creature token, "
        + "where X is the difference between those players' life totals.")]
    public void Classify_TheNovember8Ruling_AnExchangeOfLifeTotals_IsBurnNotSteal(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Burn));
        Assert.False(result.HasFlag(CardEffect.Steal));
    }

    [Theory]
    // Read 2026-11-12 under the 2026-11-05 ruling: the creature cards chosen first
    // and 'the other' returned with haste and exiled at the next end step.
    [InlineData("Wake to Slaughter", "Sorcery",
        "Choose up to two target creature cards in your graveyard. An opponent chooses one of them. Return "
        + "that card to your hand. Return the other to the battlefield under your control. It gains haste. "
        + "Exile it at the beginning of the next end step.\nFlashback {4}{B}{R} (You may cast this card from "
        + "your graveyard for its flashback cost. Then exile it.)")]
    public void Classify_TheNovember12Readings_TheOtherReturnedWithHaste_IsBurn(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Burn));
    }
}
