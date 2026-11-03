using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Every way a spell or an ability leaves the stack, read 2026-10-13: an
    // ability countered ("Sì, tutti Counter"), every spell at once, a spell
    // returned to hand, chosen and countered later, exiled, put into the
    // library, airbent; and the counter across commas or of "up to one".
    [InlineData("Tishana's Tidebinder", "Creature — Merfolk Wizard",
        "Flash\nWhen this creature enters, counter up to one target activated or triggered ability. If an "
        + "ability of an artifact, creature, or planeswalker is countered this way, that permanent loses all "
        + "abilities for as long as this creature remains on the battlefield. (Mana abilities can't be targeted.)")]
    [InlineData("Reverse the Polarity", "Instant",
        "Choose one —\n• Counter all other spells.\n• Switch each creature's power and toughness until end of "
        + "turn.\n• Creatures can't be blocked this turn.")]
    [InlineData("Reprieve", "Instant", "Return target spell to its owner's hand.\nDraw a card.")]
    [InlineData("Aether Spike", "Instant",
        "Choose target spell. You get {E}{E} (two energy counters), then you may pay any amount of {E}. "
        + "Counter that spell unless its controller pays {1} for each {E} paid this way.")]
    [InlineData("Ertai's Meddling", "Instant",
        "X can't be 0.\nTarget spell's controller exiles it with X delay counters on it.")]
    [InlineData("Smirking Spelljacker", "Creature — Djinn Wizard",
        "Flash\nFlying\nWhen this creature enters, exile target spell an opponent controls.\nWhenever this "
        + "creature attacks, if a card is exiled with it, you may cast the exiled card without paying its mana cost.")]
    [InlineData("Sudden Setback", "Instant",
        "The owner of target spell or nonland permanent puts it on their choice of the top or bottom of their library.")]
    [InlineData("Aether Gust", "Instant",
        "Choose target spell or permanent that's red or green. Its owner puts it on the top or bottom of their library.")]
    [InlineData("Aang, Swift Savior", "Legendary Creature — Human Avatar Ally",
        "Flash\nFlying\nWhen Aang enters, airbend up to one other target creature or spell. (Exile it. While "
        + "it's exiled, its owner may cast it for {2} rather than its mana cost.)")]
    [InlineData("Strix Serenade", "Instant",
        "Counter target artifact, creature, or planeswalker spell. Its controller creates a 2/2 blue Bird "
        + "creature token with flying.")]
    [InlineData("Repulsive Mutation", "Instant",
        "Put X +1/+1 counters on target creature you control. Then counter up to one target spell unless its "
        + "controller pays mana equal to the greatest power among creatures you control.")]
    public void Classify_ASpellOrAbilityTakenOffTheStack_IsCounter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Counter));
    }

    [Theory]
    // Not a counter: your own spell, a spell aimed at the card itself, and the
    // counterspell a created token carries in its quotes.
    [InlineData("Goblin Artisans", "Creature — Goblin Artificer",
        "{T}: Flip a coin. If you win the flip, draw a card. If you lose the flip, counter target artifact "
        + "spell you control that isn't the target of an ability from another creature named Goblin Artisans.")]
    [InlineData("Mistfolk", "Creature — Illusion", "{U}: Counter target spell that targets this creature.")]
    [InlineData("Mage's Attendant", "Creature — Cat Rogue",
        "When this creature enters, create a 1/1 blue Wizard creature token with \"{1}, Sacrifice this token: "
        + "Counter target noncreature spell unless its controller pays {1}.\"")]
    public void Classify_ACounterThatAnswersNothingOfTheirs_IsNotCounter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Counter));
    }
}
