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
}
