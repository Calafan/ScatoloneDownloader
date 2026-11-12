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

    [Theory]
    // The definition of 2026-11-08 (Counterspell): somebody else's spell or
    // ability taken off the stack by an answer you time. Read the same day: up to
    // four or two targets, any number of targets exiled, all abilities of your
    // opponents countered as the card turns face up, every spell and ability of
    // theirs countered by an instant, and an INSTANT that ends the turn ('Solo
    // istantanei').
    [InlineData("Katara's Reversal", "Instant",
        "Counter up to four target spells and/or abilities.\nUntap up to four target artifacts and/or "
        + "creatures.")]
    [InlineData("Double Negative", "Instant",
        "Counter up to two target spells.")]
    [InlineData("Mindbreak Trap", "Instant — Trap",
        "If an opponent cast three or more spells this turn, you may pay {0} rather than pay this spell's "
        + "mana cost.\nExile any number of target spells.")]
    [InlineData("Kadena's Silencer", "Creature — Snake Wizard",
        "When this creature is turned face up, counter all abilities your opponents control.\nMegamorph {1}{U} "
        + "(You may cast this card face down as a 2/2 creature for {3}. Turn it face up any time for its "
        + "megamorph cost and put a +1/+1 counter on it.)")]
    [InlineData("Summary Dismissal", "Instant",
        "Exile all other spells and counter all abilities.")]
    [InlineData("Whirlwind Denial", "Instant",
        "For each spell and ability your opponents control, counter it unless its controller pays {4}.")]
    [InlineData("Hurkyl's Final Meditation", "Instant",
        "During turns other than yours, this spell costs {3} more to cast.\nReturn all nonland permanents to "
        + "their owners' hands. End the turn. (Exile all spells and abilities from the stack, including this "
        + "card. The player whose turn it is discards down to their maximum hand size. Damage wears off, and "
        + "\"this turn\" and \"until end of turn\" effects end.)")]
    [InlineData("Time Stop", "Instant",
        "End the turn. (Exile all spells and abilities, including this spell. The player whose turn it is "
        + "discards down to their maximum hand size. Damage heals and \"this turn\" and \"until end of turn\" "
        + "effects end.)")]
    public void Classify_TheNovember8Counter_AnAnswerYouTime_IsCounter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Counter));
    }

    [Theory]
    // Not Counter under the definition of 2026-11-08: a trigger that counters by
    // itself whatever is cast is a rule of the table, like ward (Jin-Gitaxias lost
    // its hand tag; Invoke Prejudice, Void Mirror untagged), and the turn ended by
    // a sorcery or by an ability usable only on your turn (Ultima, Obeka, 'Solo
    // istantanei').
    [InlineData("Jin-Gitaxias, Progress Tyrant", "Legendary Creature — Phyrexian Praetor",
        "Whenever you cast an artifact, instant, or sorcery spell, copy that spell. You may choose new "
        + "targets for the copy. This ability triggers only once each turn. (A copy of a permanent spell "
        + "becomes a token.)\nWhenever an opponent casts an artifact, instant, or sorcery spell, counter that "
        + "spell. This ability triggers only once each turn.")]
    [InlineData("Invoke Prejudice", "Enchantment",
        "Whenever an opponent casts a creature spell that doesn't share a color with a creature you control, "
        + "counter that spell unless that player pays {X}, where X is its mana value.")]
    [InlineData("Void Mirror", "Artifact",
        "Whenever a player casts a spell, if no colored mana was spent to cast it, counter that spell.")]
    [InlineData("Ultima", "Sorcery",
        "Destroy all artifacts and creatures. End the turn. (Exile all spells and abilities from the stack, "
        + "including this card. The player whose turn it is discards down to their maximum hand size. Damage "
        + "wears off, and \"this turn\" and \"until end of turn\" effects end.)")]
    [InlineData("Obeka, Brute Chronologist", "Legendary Creature — Ogre Wizard",
        "{T}: The player whose turn it is may end the turn. (Exile all spells and abilities from the stack. "
        + "The player whose turn it is discards down to their maximum hand size. Damage wears off, and \"this "
        + "turn\" and \"until end of turn\" effects end.)")]
    public void Classify_TheNovember8Counter_ARuleOfTheTable_IsNotCounter(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Counter));
    }

    [Theory]
    // Read 2026-11-12: a loyalty ability is an activated one.
    [InlineData("Verdant Command", "Instant",
        "Choose two —\n• Target player creates two tapped 1/1 green Squirrel creature tokens.\n• Counter target "
        + "loyalty ability of a planeswalker.\n• Exile target card from a graveyard.\n• Target player gains 3 "
        + "life.")]
    public void Classify_TheNovember12Readings_ALoyaltyAbility_IsCounter(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Counter));
    }
}
