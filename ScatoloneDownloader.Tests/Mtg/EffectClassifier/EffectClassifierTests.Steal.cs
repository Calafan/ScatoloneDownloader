using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Steal, ruled 2026-09-19. Handing a permanent to somebody ELSE is the mirror
    // image of this tag, and was 12 of its 14 false positives.
    [InlineData("Jinxed Idol", "Artifact",
        "At the beginning of your upkeep, this artifact deals 2 damage to you.\n"
        + "Sacrifice a creature: Target opponent gains control of this artifact.")]
    [InlineData("Rainbow Vale", "Land",
        "{T}: Add one mana of any color. An opponent gains control of this land at the beginning of "
        + "the next end step.")]
    [InlineData("Guardian Beast", "Creature — Beast",
        "As long as this creature is untapped, noncreature artifacts you control can't be enchanted, "
        + "they have indestructible, and other players can't gain control of them. This effect doesn't "
        + "remove Auras already attached to those artifacts.")]
    [InlineData("Emberwilde Djinn", "Creature — Djinn",
        "Flying\nAt the beginning of each player's upkeep, that player may pay {R}{R} or 2 life. "
        + "If the player does, they gain control of this creature.")]
    public void Classify_GivingAPermanentAway_IsNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // The veto above reads the subject IMMEDIATELY in front of the verb. These two
    // put the victim in front of a verb that is yours — "untap target creature AN
    // OPPONENT CONTROLS and gain control of it" — and any filler at all loses them.
    [InlineData("Ray of Command", "Instant",
        "Untap target creature an opponent controls and gain control of it until end of turn. "
        + "That creature gains haste until end of turn. When you lose control of the creature, tap it.")]
    [InlineData("Magus of the Unseen", "Creature — Human Wizard",
        "{1}{U}, {T}: Untap target artifact an opponent controls and gain control of it until end of "
        + "turn. It gains haste until end of turn. When you lose control of the artifact, tap it.")]
    // An exchange is a theft you paid for.
    [InlineData("Political Trickery", "Sorcery",
        "Exchange control of target land you control and target land an opponent controls. "
        + "(This effect lasts indefinitely.)")]
    [InlineData("Legerdemain", "Sorcery",
        "Exchange control of target artifact or creature and another target permanent that shares one "
        + "of those types with it. (This effect lasts indefinitely.)")]
    // Taking a CARD out of somebody else's library, hand or graveyard and playing
    // it is a theft of a card rather than of a permanent.
    [InlineData("Outrageous Robbery", "Sorcery",
        "Target opponent exiles the top X cards of their library face down. You may look at and play "
        + "those cards for as long as they remain exiled. If you cast a spell this way, you may spend "
        + "mana as though it were mana of any type to cast it.")]
    [InlineData("Laughing Jasper Flint", "Legendary Creature — Goblin Mercenary",
        "Creatures you control but don't own are Mercenaries in addition to their other types.\n"
        + "At the beginning of your upkeep, exile the top X cards of target opponent's library, where "
        + "X is the number of outlaws you control. Until end of turn, you may cast spells from among "
        + "those cards, and mana of any type can be spent to cast those spells.")]
    [InlineData("Vaan, Street Thief", "Legendary Creature — Human Rogue",
        "Whenever one or more Scouts, Pirates, and/or Rogues you control deal combat damage to a "
        + "player, exile the top card of that player's library. You may cast it. If you don't, create "
        + "a Treasure token.")]
    // Word of Command takes the player rather than the permanent.
    [InlineData("Word of Command", "Sorcery",
        "Look at target opponent's hand and choose a card from it. You control that player until Word "
        + "of Command finishes resolving. The player plays that card if able.")]
    // Reanimating out of an OPPONENT'S graveyard is both Reanimate and this,
    // ruled 2026-09-19.
    [InlineData("Ashen Powder", "Sorcery",
        "Put target creature card from an opponent's graveyard onto the battlefield under your control.")]
    [InlineData("Bone Dancer", "Creature — Zombie",
        "Whenever this creature attacks and isn't blocked, you may put the top creature card of "
        + "defending player's graveyard onto the battlefield under your control. If you do, this "
        + "creature assigns no combat damage this turn.")]
    // "TAPPED" inside the phrase: an Oracle update in September 2026 rewrote
    // Geth from "under your control tapped" to this, and the card quietly lost
    // the tag.
    [InlineData("Geth, Lord of the Vault", "Legendary Creature — Phyrexian Zombie",
        "Intimidate (This creature can't be blocked except by artifact creatures and/or creatures that share a "
        + "color with it.)\n{X}{B}: Put target artifact or creature card with mana value X from an opponent's "
        + "graveyard onto the battlefield tapped under your control. Then that player mills X cards.")]
    // And the life total is a thing you can take.
    [InlineData("Mirror Universe", "Artifact",
        "{T}, Sacrifice this artifact: Exchange life totals with target opponent. "
        + "Activate only during your upkeep.")]
    // "THEY may play" against a NAMED zone: the creature's controller takes a card
    // out of the opponent's library, so the two are different people.
    [InlineData("Gonti, Night Minister", "Legendary Creature — Phyrexian Rogue",
        "Whenever a player casts a spell they don't own, that player creates a Treasure token.\n"
        + "Whenever a creature deals combat damage to one of your opponents, its controller looks at "
        + "the top card of that opponent's library and exiles it face down. They may play that card "
        + "for as long as it remains exiled. Mana of any type can be spent to cast a spell this way.")]
    // A mill IS the zone — Locke never names a library at all.
    [InlineData("Locke, Treasure Hunter", "Legendary Creature — Human Rogue",
        "Locke can't be blocked by creatures with greater power.\n"
        + "Mug — Whenever Locke attacks, each player mills a card. If a land card was milled this "
        + "way, create a Treasure token. Until end of turn, you may cast a spell from among those cards.")]
    // Moving somebody else's Aura is theft of its job if not of its control.
    [InlineData("Enchantment Alteration", "Instant",
        "Attach target Aura attached to a creature or land to another permanent of that type.")]
    [InlineData("Crown of the Ages", "Artifact",
        "{4}, {T}: Attach target Aura attached to a creature to another creature.")]
    public void Classify_TakingWhatIsSomebodyElses_IsSteal(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // A symmetric exile hands everyone their own cards back, and playing YOUR OWN
    // is not theft however much of the opponent's library the text mentions.
    [InlineData("Triple Triad", "Enchantment",
        "At the beginning of your upkeep, each player exiles the top card of their library. Until end "
        + "of turn, you may play the card you own exiled this way and each other card exiled this way "
        + "with lesser mana value than it without paying their mana costs.")]
    [InlineData("Wheel of Potential", "Sorcery",
        "You get {E}{E}{E} (three energy counters), then you may pay any amount of {E}.\n"
        + "Each player may exile their hand and draw a number of cards equal to the amount of {E} paid "
        + "this way. If seven or more {E} was paid this way, you may play cards you own exiled this "
        + "way until the end of your next turn.")]
    // Ordinary reanimation belongs to nobody in particular: "from A graveyard"
    // names no victim, so it stays Reanimate alone.
    [InlineData("Hymn of Rebirth", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control.")]
    [InlineData("Coffin Queen", "Creature — Zombie",
        "You may choose not to untap this creature during your untap step.\n"
        + "{2}{B}, {T}: Put target creature card from a graveyard onto the battlefield under your "
        + "control. When this creature becomes tapped or you lose control of it, exile that card.")]
    // "THEY may cast" against a PRONOUN zone is one player doing both halves: the
    // opponent digs through their own library and casts what they find.
    [InlineData("Transforming Flourish", "Instant",
        "Demonstrate (When you cast this spell, you may copy it.)\n"
        + "Destroy target artifact or creature you don't control. If that permanent is destroyed this "
        + "way, its controller exiles cards from the top of their library until they exile a nonland "
        + "card, then they may cast that card without paying its mana cost.")]
    // One half mills a player, the other plays lands out of YOUR graveyard; only a
    // rule reading the whole card at once could join them into a theft.
    [InlineData("Glacierwood Siege", "Enchantment",
        "As this enchantment enters, choose Temur or Sultai.\n"
        + "• Temur — Whenever you cast an instant or sorcery spell, target player mills four cards.\n"
        + "• Sultai — You may play lands from your graveyard.")]
    // A token copy of their creature is a body you made, not a card you took.
    [InlineData("Echo Chamber", "Artifact",
        "{4}, {T}: An opponent chooses target creature they control. Create a token that's a copy of "
        + "that creature. That token gains haste until end of turn. Exile the token at the beginning "
        + "of the next end step. Activate only as a sorcery.")]
    // And a card pulled out of "that graveyard" at random names no victim.
    [InlineData("Mysterious Stranger", "Creature — Human",
        "Flash\nWhen this creature enters, for each graveyard with an instant or sorcery card in it, "
        + "exile target instant or sorcery card from that graveyard. If two or more cards are exiled "
        + "this way, choose one of them at random and copy it. You may cast the copy without paying "
        + "its mana cost.")]
    public void Classify_WhatIsAlreadyYours_IsNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // Steal's false positives of October 2026, every one tagged without it.
    // The play lives in reminder text: an Adventure, a legendary sorcery.
    [InlineData("Cruel Somnophage // Can't Wake Up", "Creature — Nightmare // Sorcery — Adventure",
        "Cruel Somnophage's power and toughness are each equal to the number of creature cards in all "
        + "graveyards.\nTarget player mills four cards. (Then exile this card. You may cast the creature "
        + "later from exile.)")]
    [InlineData("Isildur's Fateful Strike", "Legendary Instant",
        "(You may cast a legendary instant only if you control a legendary creature or planeswalker.)\n"
        + "Destroy target creature. If its controller has more than four cards in hand, they exile cards "
        + "from their hand equal to the difference.")]
    // The opponent is only the trigger; the library is YOURS.
    [InlineData("Fireglass Mentor", "Creature — Elf Warlock",
        "At the beginning of your second main phase, if an opponent lost life this turn, exile the top "
        + "two cards of your library. Choose one of them. Until end of turn, you may play that card.")]
    [InlineData("Powerbalance", "Enchantment",
        "Whenever an opponent casts a spell, you may reveal the top card of your library. If you do, you "
        + "may cast that card without paying its mana cost if the two spells have the same mana value.")]
    // The opponent exiles from their own library and plays it themselves.
    [InlineData("Urabrask, Heretic Praetor", "Legendary Creature — Phyrexian Praetor",
        "Haste\nAt the beginning of your upkeep, exile the top card of your library. You may play it this "
        + "turn.\nAt the beginning of each opponent's upkeep, the next time they would draw a card this "
        + "turn, instead they exile the top card of their library. They may play it this turn.")]
    // One bullet exiles a graveyard, another plays your own library.
    [InlineData("Riveteers Charm", "Instant",
        "Choose one —\n• Target opponent sacrifices a creature or planeswalker they control with the "
        + "greatest mana value among creatures and planeswalkers they control.\n• Exile the top three cards "
        + "of your library. Until your next end step, you may play those cards.\n• Exile target player's "
        + "graveyard.")]
    // A mill, then reanimation from ANY graveyard: no victim named.
    [InlineData("Path of the Schemer", "Plane — Ravnica",
        "Each player mills two cards. Then you put a creature card from a graveyard onto the battlefield "
        + "under your control. It's an artifact in addition to its other types.")]
    // A land out of their graveyard is Ramp.
    [InlineData("Realmbreaker, the Invasion Tree", "Legendary Artifact",
        "{2}, {T}: Target opponent mills three cards. Put a land card from their graveyard onto the "
        + "battlefield tapped under your control. It gains \"If this land would leave the battlefield, "
        + "exile it instead of putting it anywhere else.\"")]
    // The spell is cast "from your hand", seventy letters on.
    [InlineData("Press the Enemy", "Instant",
        "Return target spell or nonland permanent an opponent controls to its owner's hand. You may cast "
        + "an instant or sorcery spell with equal or lesser mana value from your hand without paying its "
        + "mana cost.")]
    // The playing of everyone's cards is told apart only by its flashback
    // reminder — tagged CardAdvantage alone.
    [InlineData("Lidless Gaze", "Sorcery",
        "Exile the top card of each player's library. Until the end of your next turn, you may play those "
        + "cards, and mana of any type can be spent to cast those spells.\nFlashback {2}{B}{R} (You may "
        + "cast this card from your graveyard for its flashback cost. Then exile it.)")]
    // Handing the artifact on.
    [InlineData("Contested Game Ball", "Artifact",
        "Whenever you're dealt combat damage, the attacking player gains control of this artifact and "
        + "untaps it.\n{2}, {T}: Draw a card and put a point counter on this artifact. Then if it has five "
        + "or more point counters on it, sacrifice it and create a Treasure token.")]
    [InlineData("Hithlain Rope", "Artifact",
        "This artifact can't be sacrificed.\n{2}, {T}: Draw a card. The player to your right gains control "
        + "of this artifact.")]
    // The token steals, not the card.
    [InlineData("Wake the Dragon", "Sorcery",
        "Create a 6/6 black and red Dragon creature token with flying, menace, and \"Whenever this token "
        + "deals combat damage to a player, gain control of target artifact that player controls.\"")]
    // Your own Aura, moved.
    [InlineData("Codsworth, Handy Helper", "Legendary Artifact Creature — Robot",
        "Commanders you control have ward {2}.\n{T}: Add {W}{W}. Spend this mana only to cast Aura and/or "
        + "Equipment spells.\n{T}: Attach target Aura or Equipment you control to target creature you "
        + "control. Activate only as a sorcery.")]
    public void Classify_TheOctoberStealGaps_AreNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Fact]
    public void Classify_TakingControlOfASpell_IsNotSteal()
    {
        // A spell is not a permanent; taking one over is Redirect's (2026-09-28).
        CardEffect result = EffectClassifier.Classify(MakeCard("Invert Polarity", "Instant",
            "Choose target spell, then flip a coin. If you win the flip, gain control of that spell and you may "
            + "choose new targets for it. If you lose the flip, counter that spell."));

        Assert.False(result.HasFlag(CardEffect.Steal));
    }

    [Theory]
    // V3, 2026-10-16: exile target spell an opponent controls and play it is a
    // theft.
    [InlineData("Smirking Spelljacker", "Creature — Djinn Wizard Rogue",
        "Flash\nFlying\nWhen this creature enters, exile target spell an opponent controls.\nWhenever this "
        + "creature attacks, if a card is exiled with it, you may cast the exiled card without paying its mana "
        + "cost.")]
    public void Classify_TheOctober16Pass_TheirSpellTaken_IsSteal(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // A permanent of theirs exiled and then cast by you.
    [InlineData("Abstruse Appropriation", "Instant",
        "Devoid (This card has no color.)\nExile target nonland permanent. You may cast that card for as long "
        + "as it remains exiled, and you may spend colorless mana as though it were mana of any color to cast "
        + "that spell.")]
    public void Classify_TheOctober16StealPass_IsSteal(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }

    [Theory]
    // A card exiled from a graveyard and cast is no permanent taken, and a card
    // cast from your own graveyard is yours.
    [InlineData("Savvy Trader", "Creature — Human Citizen",
        "When this creature enters, exile target permanent card from your graveyard. You may play that card "
        + "for as long as it remains exiled.\nSpells you cast from anywhere other than your hand cost {1} less "
        + "to cast.")]
    [InlineData("Raul, Trouble Shooter", "Legendary Creature — Zombie Mutant Rogue",
        "Once during each of your turns, you may cast a spell from among cards in your graveyard that were "
        + "milled this turn.\n{T}: Each player mills a card. (They each put the top card of their library into "
        + "their graveyard.)")]
    public void Classify_TheOctober16StealPass_IsNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }
}
