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
}
