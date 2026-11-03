using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Fact]
    // The owner's choice of top or bottom of the library: 9 of 9 reviewed
    // cards are tagged Bounce (2026-09-25).
    public void Classify_TopOrBottomOfTheLibrary_IsBounce()
    {
        Assert.True(EffectClassifier.Classify(MakeCard("Trip Up", "Instant",
            "Target nonland permanent's owner puts it on their choice of the top or bottom of their "
            + "library.\nCycling {2} ({2}, Discard this card: Draw a card.)")).HasFlag(CardEffect.Bounce));
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

    [Theory]
    // Read 2026-09-29, each shape tagged on every reviewed card using it: a
    // target put on TOP of its owner's library (Time Ebb, Ether Well), SECOND or
    // THIRD from the top (Deem Inferior, Riptide Gearhulk), "the OWNER OF TARGET
    // … puts it on their choice of the top or bottom" (Riverwalk Technique), the
    // owner as the target (Hurkyl's Recall), creatures the opponents chose
    // (Summon: Valefor). A permanent of YOURS in the same clause as one of theirs
    // still bounces theirs (Aether Tradewinds), the Auras you own on the target
    // are not the target (Word of Undoing), and lands paid as a cost do not
    // hide the creature bounced (Flooded Shoreline).
    [InlineData("Time Ebb", "Sorcery",
        "Put target creature on top of its owner's library.")]
    [InlineData("Ether Well", "Instant",
        "Put target creature on top of its owner's library. If that creature is red, you may put it on the "
        + "bottom of its owner's library instead.")]
    [InlineData("Deem Inferior", "Sorcery",
        "This spell costs {1} less to cast for each card you've drawn this turn.\nThe owner of target nonland "
        + "permanent puts it into their library second from the top or on the bottom.")]
    [InlineData("Riptide Gearhulk", "Artifact Creature — Construct",
        "Double strike\nProwess (Whenever you cast a noncreature spell, this creature gets +1/+1 until end of "
        + "turn.)\nWhen this creature enters, for each opponent, put up to one target nonland permanent that "
        + "player controls into its owner's library third from the top.")]
    [InlineData("Riverwalk Technique", "Instant",
        "Choose one —\n• The owner of target nonland permanent puts it on their choice of the top or bottom of "
        + "their library.\n• Counter target noncreature spell.")]
    [InlineData("Hurkyl's Recall", "Instant",
        "Return all artifacts target player owns to their hand.")]
    [InlineData("Summon: Valefor", "Enchantment Creature — Saga Drake",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI — Sonic "
        + "Wings — Each opponent chooses a creature with the greatest mana value among creatures they control. "
        + "Return those creatures to their owners' hands.\nII, III, IV — Tap up to one target creature and put a "
        + "stun counter on it.\nFlying")]
    [InlineData("Word of Undoing", "Instant",
        "Return target creature and all white Auras you own attached to it to their owners' hands.")]
    [InlineData("Aether Tradewinds", "Instant",
        "Return target permanent you control and target permanent you don't control to their owners' hands.")]
    [InlineData("Flooded Shoreline", "Enchantment",
        "{U}{U}, Return two Islands you control to their owner's hand: Return target creature to its owner's "
        + "hand.")]
    public void Classify_AReturnThatAnswersSomething_IsBounce(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Returns that answer nobody, each untagged on (nearly) every reviewed card:
    // your OWN permanent (3 of 31 tagged, taken to the human as slips), a LAND
    // (0 of 5), the Auras on your own permanent, a card EXILED WITH the card;
    // and the library placements of a creature of YOURS (Civic Guildmage), of
    // the card ITSELF (God-Eternal Kefnet) and of a card from a HAND (Lost Hours).
    [InlineData("Exosuit Savior", "Creature — Human Soldier",
        "Flying\nWhen this creature enters, return up to one other target permanent you control to its owner's "
        + "hand.")]
    [InlineData("Essence Reliquary", "Artifact",
        "{T}: Return another target permanent you control and all Auras you control attached to it to their "
        + "owner's hand. Activate only during your turn.")]
    [InlineData("Scarab of the Unseen", "Artifact",
        "{T}, Sacrifice this artifact: Return all Auras attached to target permanent you own to their owners' "
        + "hands. Draw a card at the beginning of the next turn's upkeep.")]
    [InlineData("Active Volcano", "Instant",
        "Choose one —\n• Destroy target blue permanent.\n• Return target Island to its owner's hand.")]
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Mole Beast",
        "When Nyla enters, exile up to one target creature card from your graveyard. If you do, you lose X "
        + "life and create X Clue tokens, where X is that card's mana value. (A Clue token is an artifact with "
        + "\"{2}, Sacrifice this token: Draw a card.\")\nAt the beginning of your end step, if you control no "
        + "Clues, return target card exiled with Nyla to its owner's hand.")]
    [InlineData("Civic Guildmage", "Creature — Human Wizard",
        "{G}, {T}: Target creature gets +0/+1 until end of turn.\n{U}, {T}: Put target creature you control on "
        + "top of its owner's library.")]
    [InlineData("God-Eternal Kefnet", "Legendary Creature — Zombie God",
        "Flying\nYou may reveal the first card you draw each turn as you draw it. Whenever you reveal an "
        + "instant or sorcery card this way, copy that card and you may cast the copy. That copy costs {2} less "
        + "to cast.\nWhen God-Eternal Kefnet dies or is put into exile from the battlefield, you may put it into "
        + "its owner's library third from the top.")]
    [InlineData("Lost Hours", "Sorcery",
        "Target player reveals their hand. You choose a nonland card from it. That player puts that card into "
        + "their library third from the top.")]
    public void Classify_AReturnThatAnswersNobody_IsNotBounce(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }

    [Theory]
    // Ruled 2026-09-29: AIRBEND aimed where an opponent's permanent can be is
    // Bounce (Airbending Lesson, Aang the Last Airbender, Avatar's Wrath); an
    // Aura's creature returned is Bounce whoever pays (Phantom Wings, Sun Clasp —
    // "bounce entrambe"); and a permanent exiled until the card leaves or dies,
    // then returned to its owner's hand, is Bounce and — for the graveyard cards
    // exiled with it — Regrowth (Aurelia's Vindicator, The Spot).
    [InlineData("Airbending Lesson", "Instant — Lesson",
        "Airbend target nonland permanent. (Exile it. While it's exiled, its owner may cast it for {2} rather "
        + "than its mana cost.)\nDraw a card.")]
    [InlineData("Aang, the Last Airbender", "Legendary Creature — Human Avatar Ally",
        "Flying\nWhen Aang enters, airbend up to one other target nonland permanent. (Exile it. While it's "
        + "exiled, its owner may cast it for {2} rather than its mana cost.)\nWhenever you cast a Lesson spell, "
        + "Aang gains lifelink until end of turn.")]
    [InlineData("Avatar's Wrath", "Sorcery",
        "Choose up to one target creature, then airbend all other creatures. (Exile them. While each one is "
        + "exiled, its owner may cast it for {2} rather than its mana cost.)\nUntil your next turn, your "
        + "opponents can't cast spells from anywhere other than their hands.\nExile Avatar's Wrath.")]
    [InlineData("Sun Clasp", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets +1/+3.\n{W}: Return enchanted creature to its owner's hand.")]
    [InlineData("Phantom Wings", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has flying.\nSacrifice this Aura: Return enchanted creature to "
        + "its owner's hand.")]
    [InlineData("Aurelia's Vindicator", "Creature — Angel",
        "Flying, lifelink, ward {2}\nDisguise {X}{3}{W}\nWhen this creature is turned face up, exile up to X "
        + "other target creatures from the battlefield and/or creature cards from graveyards.\nWhen this "
        + "creature leaves the battlefield, return the exiled cards to their owners' hands.")]
    [InlineData("The Spot, Living Portal", "Legendary Creature — Human Scientist Villain",
        "When The Spot enters, exile up to one target nonland permanent and up to one target nonland "
        + "permanent card from a graveyard.\nWhen The Spot dies, put him on the bottom of his owner's library. "
        + "If you do, return the exiled cards to their owners' hands.")]
    public void Classify_ABounceRuledIn_IsBounce(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }

    [Fact]
    // A spell returned to hand is Counter and not Bounce; "spell OR nonland
    // permanent" keeps Bounce for its permanent (2026-10-13).
    public void Classify_ASpellReturnedToHand_IsNotBounceUnlessAPermanentIsToo()
    {
        Assert.False(EffectClassifier.Classify(MakeCard("Reprieve", "Instant",
            "Return target spell to its owner's hand.\nDraw a card.")).HasFlag(CardEffect.Bounce));
        Assert.True(EffectClassifier.Classify(MakeCard("Sink into Stupor", "Instant",
            "Return target spell or nonland permanent an opponent controls to its owner's hand."))
            .HasFlag(CardEffect.Bounce));
    }

    [Fact]
    public void Classify_AnExileItsOwnerMayPlayFrom_IsBounceNotRemoval()
    {
        // The card is as good as back in its owner's hand (2026-10-09).
        var effect = EffectClassifier.Classify(MakeCard("Extraordinary Journey", "Enchantment",
            "When this enchantment enters, exile up to X target creatures. For each of those cards, its owner "
            + "may play it for as long as it remains exiled.\nWhenever one or more nontoken creatures enter, if "
            + "one or more of them entered from exile or was cast from exile, you draw a card. This ability "
            + "triggers only once each turn."));
        Assert.True(effect.HasFlag(CardEffect.Bounce));
        Assert.False(effect.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The owner may play it 'for as long as that card remains exiled', the other
    // word order of Extraordinary Journey (Prowl, Bounce by hand), read
    // 2026-10-27.
    [InlineData("Prowl, Stoic Strategist // Prowl, Pursuit Vehicle", "Legendary Artifact Creature — Robot // Legendary Artifact — Vehicle",
        "More Than Meets the Eye {2}{W} (You may cast this card converted for {2}{W}.)\nWhenever Prowl "
        + "attacks, exile up to one other target tapped creature or Vehicle. For as long as that card remains "
        + "exiled, its owner may play it.\nWhenever a player plays a card exiled with Prowl, you draw a card and "
        + "convert Prowl.\nLiving metal (During your turn, this Vehicle is also a creature.)\nWhenever another "
        + "creature or Vehicle you control enters, put a +1/+1 counter on Prowl. If this is the second time "
        + "this ability has resolved this turn, convert Prowl.")]
    public void Classify_TheOctober27RemovalPass_OwnerMayPlayWhileExiled_IsBounce(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Bounce));
    }
}
