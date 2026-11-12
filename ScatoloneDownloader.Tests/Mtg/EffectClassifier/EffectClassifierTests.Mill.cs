using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
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
    // Read 2026-11-06 under the 2026-10-15 ruling that the top of their library
    // EXILED is Mill: 'exiles cards from the top of their library until they exile
    // a land card' is the same attack with the words in the other order (Umbris,
    // Fear Manifest, Mill by hand).
    [InlineData("Umbris, Fear Manifest", "Legendary Creature — Nightmare Horror",
        "Umbris gets +1/+1 for each card your opponents own in exile.\nWhenever Umbris or another Nightmare or "
        + "Horror you control enters, target opponent exiles cards from the top of their library until they "
        + "exile a land card.")]
    public void Classify_TheNovember6Readings_ExilingTheTopUntilALandCard_IsMill(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // Read 2026-11-06 under 'a bare mill fills your own graveyard': 'for each
    // opponent, you mill a card' names an opponent and mills YOU (Sivriss,
    // Nightmare Speaker, untagged for Mill by hand).
    [InlineData("Sivriss, Nightmare Speaker", "Legendary Creature — Snake Cleric Warlock",
        "{T}, Sacrifice another creature or an artifact: For each opponent, you mill a card, then return that "
        + "card from your graveyard to your hand unless that player pays 3 life. (To mill a card, put the top "
        + "card of your library into your graveyard.)\nChoose a Background (You can have a Background as a "
        + "second commander.)")]
    public void Classify_TheNovember6Readings_ForEachOpponentYouMill_IsNotMill(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // RULED 2026-11-06 (Sì, entrambe Mill): an opponent who reveals from the top
    // of their library until a card, that card taken, and the rest put into their
    // graveyard is Mill, in both wordings: 'That player puts the rest of the
    // revealed cards into their graveyard' (Curse of Unbinding) and 'and the rest
    // into their graveyard' (Smeagol, Helpful Guide).
    [InlineData("Curse of Unbinding", "Enchantment — Aura Curse",
        "Enchant player\nAt the beginning of enchanted player's upkeep, that player reveals cards from the top "
        + "of their library until they reveal a creature card. Put that card onto the battlefield under your "
        + "control. That player puts the rest of the revealed cards into their graveyard.")]
    [InlineData("Sméagol, Helpful Guide", "Legendary Creature — Halfling Horror",
        "At the beginning of your end step, if a creature died under your control this turn, the Ring tempts "
        + "you.\nWhenever the Ring tempts you, target opponent reveals cards from the top of their library until "
        + "they reveal a land card. Put that card onto the battlefield tapped under your control and the rest "
        + "into their graveyard.")]
    [InlineData("Thought Dissector", "Artifact",
        "{X}, {T}: Target opponent reveals cards from the top of their library until an artifact card or X "
        + "cards are revealed, whichever comes first. If an artifact card is revealed this way, put it onto the "
        + "battlefield under your control and sacrifice this artifact. Put the rest of the revealed cards into "
        + "that player's graveyard.")]
    public void Classify_TheNovember6Ruling_ARevealUntilWithTheRestInTheirGraveyard_IsMill(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // Another player's library only: Oath of Druids has the first player reveal
    // from their own library and put the rest into their own graveyard.
    [InlineData("Oath of Druids", "Enchantment",
        "At the beginning of each player's upkeep, that player chooses target player who controls more "
        + "creatures than they do and is their opponent. The first player may reveal cards from the top of "
        + "their library until they reveal a creature card. If the first player does, that player puts that "
        + "card onto the battlefield and all other cards revealed this way into their graveyard.")]
    public void Classify_TheNovember6Ruling_ARevealUntilOfTheirOwnLibrary_IsNotMill(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // The definition of 2026-11-10 (Glimpse the Unthinkable): ANOTHER player loses
    // cards off the top of their library to your card, into their graveyard, or
    // exiled when the card plays none of them. Read on the reveal wordings (Mind
    // Funeral's 'all cards revealed this way', Telemin Performance's 'all
    // noncreature cards', Destroy the Evidence's 'its controller', Dakra Mystic's
    // 'their owners' graveyards'); Q1 'Mill', a top looked at and the card for the
    // graveyard chosen by you (Wand of Denial, Thief of Sanity); 'each player'
    // exiling (Timesifter); Q2 'Conta, come Burn', a token's quoted ability
    // (Ashiok, Nightmare Muse). Cruel Fate and Eye Spy, one card once, were here
    // until the 2026-11-12 threshold moved them to
    // Classify_TheNovember12Ruling_OneOrTwoCardsMilledOnce_IsNotMill.
    [InlineData("Mind Funeral", "Sorcery",
        "Target opponent reveals cards from the top of their library until four land cards are revealed. That "
        + "player puts all cards revealed this way into their graveyard.")]
    [InlineData("Telemin Performance", "Sorcery",
        "Target opponent reveals cards from the top of their library until they reveal a creature card. That "
        + "player puts all noncreature cards revealed this way into their graveyard, then you put the creature "
        + "card onto the battlefield under your control.")]
    [InlineData("Destroy the Evidence", "Sorcery",
        "Destroy target land. Its controller reveals cards from the top of their library until they reveal a "
        + "land card, then puts those cards into their graveyard.")]
    [InlineData("Dakra Mystic", "Creature — Merfolk Wizard",
        "{U}, {T}: Each player reveals the top card of their library. You may put the revealed cards into "
        + "their owners' graveyards. If you don't, each player draws a card.")]
    [InlineData("Wand of Denial", "Artifact",
        "{T}: Look at the top card of target player's library. If it's a nonland card, you may pay 2 life. If "
        + "you do, put it into that player's graveyard.")]
    [InlineData("Thief of Sanity", "Creature — Specter",
        "Flying\nWhenever this creature deals combat damage to a player, look at the top three cards of that "
        + "player's library, exile one of them face down, then put the rest into their graveyard. You may cast "
        + "that card for as long as it remains exiled, and mana of any type can be spent to cast that spell.")]
    [InlineData("Timesifter", "Artifact",
        "At the beginning of each upkeep, each player exiles the top card of their library. The player who "
        + "exiled the card with the greatest mana value takes an extra turn after this one. If two or more "
        + "players' cards are tied for greatest, the tied players repeat this process until the tie is broken.")]
    [InlineData("Ashiok, Nightmare Muse", "Legendary Planeswalker — Ashiok",
        "+1: Create a 2/3 blue and black Nightmare creature token with \"Whenever this token attacks or "
        + "blocks, each opponent exiles the top two cards of their library.\"\n−3: Return target nonland "
        + "permanent to its owner's hand, then that player exiles a card from their hand.\n−7: You may cast up "
        + "to three spells from among face-up cards your opponents own from exile without paying their mana "
        + "costs.")]
    public void Classify_TheNovember10Definition_AnotherPlayersTopIntoTheirGraveyard_IsMill(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // Ruled 2026-11-10 Q3 'Fuori': the player who reveals their top KEEPS part of
    // it, the named card into their hand (Petra Sphinx, Vexing Arcanix) or the
    // permanents onto their own battlefield (Over the Top, Guild Feud, Pyxis of
    // Pandemonium), so the card hands them something and is no attack on their
    // library.
    [InlineData("Over the Top", "Sorcery",
        "Each player reveals a number of cards from the top of their library equal to the number of nonland "
        + "permanents they control, puts all permanent cards they revealed this way onto the battlefield, and "
        + "puts the rest into their graveyard.")]
    [InlineData("Petra Sphinx", "Creature — Sphinx",
        "{T}: Target player chooses a card name, then reveals the top card of their library. If that card has "
        + "the chosen name, that player puts it into their hand. If it doesn't, the player puts it into their "
        + "graveyard.")]
    [InlineData("Vexing Arcanix", "Artifact",
        "{3}, {T}: Target player chooses a card name, then reveals the top card of their library. If that "
        + "card has the chosen name, that player puts it into their hand. Otherwise, they put it into their "
        + "graveyard and this artifact deals 2 damage to them.")]
    [InlineData("Guild Feud", "Enchantment",
        "At the beginning of your upkeep, target opponent reveals the top three cards of their library, may "
        + "put a creature card from among them onto the battlefield, then puts the rest into their graveyard. "
        + "You do the same with the top three cards of your library. If two creatures are put onto the "
        + "battlefield this way, those creatures fight each other.")]
    [InlineData("Pyxis of Pandemonium", "Artifact",
        "{T}: Each player exiles the top card of their library face down.\n{7}, {T}, Sacrifice this artifact: "
        + "Each player turns face up all cards they own exiled with this artifact, then puts all permanent "
        + "cards among them onto the battlefield.")]
    public void Classify_TheNovember10Ruling_ARevealThePlayerKeepsPartOf_IsNotMill(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // Ruled 2026-11-10, 'Cellar Door è in effetti macina': the bottom card of
    // their library put into their graveyard is the same attack as the top.
    [InlineData("Cellar Door", "Artifact",
        "{3}, {T}: Target player puts the bottom card of their library into their graveyard. If it's a "
        + "creature card, you create a 2/2 black Zombie creature token.")]
    public void Classify_TheNovember10Ruling_TheBottomOfTheirLibrary_IsMill(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // RULED 2026-11-12 ('Almeno 3 o ripetuto'): a mill made once has to take three
    // cards or more. One or two, once, is a rider: 'mills a card' (Ray of
    // Erasure), 'mills two cards' on entering (Watcher of the Wayside) or for each
    // player (Halo-Charged Skaab), a top looked at with one card or that card put
    // into their graveyard (Cruel Fate, Eye Spy, Wu Spy), and three looked at with
    // one put back and the rest binned (Dimir Charm). Cruel Fate and Eye Spy
    // pinned the 11-10 definition as Mill.
    [InlineData("Ray of Erasure", "Instant",
        "Target player mills a card.\nDraw a card at the beginning of the next turn's upkeep.")]
    [InlineData("Watcher of the Wayside", "Artifact Creature — Golem",
        "When this creature enters, target player mills two cards. You gain 2 life. (To mill two cards, a "
        + "player puts the top two cards of their library into their graveyard.)")]
    [InlineData("Halo-Charged Skaab", "Creature — Zombie",
        "When this creature enters, each player mills two cards. Then you may put an instant, sorcery, or "
        + "battle card from your graveyard on top of your library. (To mill two cards, a player puts the top "
        + "two cards of their library into their graveyard.)")]
    [InlineData("Cruel Fate", "Sorcery",
        "Look at the top five cards of target opponent's library. Put one of those cards into that player's "
        + "graveyard and the rest on top of their library in any order.")]
    [InlineData("Eye Spy", "Sorcery",
        "Look at the top card of target player's library. You may put that card into their graveyard.")]
    [InlineData("Wu Spy", "Creature — Human Soldier Rogue",
        "When this creature enters, look at the top two cards of target player's library. Put one of them "
        + "into their graveyard.")]
    [InlineData("Dimir Charm", "Instant",
        "Choose one —\n• Counter target sorcery spell.\n• Destroy target creature with power 2 or less.\n• Look "
        + "at the top three cards of target player's library. Put one back and the rest into that player's "
        + "graveyard.")]
    public void Classify_TheNovember12Ruling_OneOrTwoCardsMilledOnce_IsNotMill(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // The same ruling's other side: three cards once is Mill (Drownyard Amalgam),
    // and so is a mill of one or two that comes again, a trigger (Scrabbling
    // Skullcrab), a saga chapter that comes twice (Death in Heaven's 'I, II'), an
    // activation the card grants in quotes (Stern Mentor), or an amount counted
    // 'for each' (Sanity Grinding).
    [InlineData("Drownyard Amalgam", "Creature — Zombie Horror",
        "When this creature enters, target player mills three cards. (They put the top three cards of their "
        + "library into their graveyard.)\n{2}{U}: This creature can't be blocked this turn.")]
    [InlineData("Scrabbling Skullcrab", "Creature — Crab Skeleton",
        "Eerie — Whenever an enchantment you control enters and whenever you fully unlock a Room, target "
        + "player mills two cards. (They put the top two cards of their library into their graveyard.)")]
    [InlineData("Death in Heaven", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Target player mills two cards, then exiles their graveyard.\nIII — Put all creature cards exiled with "
        + "this enchantment onto the battlefield face down under your control. They're 2/2 Cyberman artifact "
        + "creatures.")]
    [InlineData("Stern Mentor", "Creature — Human Wizard",
        "Soulbond (You may pair this creature with another unpaired creature when either enters. They remain "
        + "paired for as long as you control both of them.)\nAs long as this creature is paired with another "
        + "creature, each of those creatures has \"{T}: Target player mills two cards.\"")]
    [InlineData("Sanity Grinding", "Sorcery",
        "Chroma — Reveal the top ten cards of your library. For each blue mana symbol in the mana costs of "
        + "the revealed cards, target opponent mills a card. Then put the cards you revealed this way on the "
        + "bottom of your library in any order.")]
    public void Classify_TheNovember12Ruling_AMillRepeatedOrOfThreeOrCounted_IsMill(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Mill));
    }
}
