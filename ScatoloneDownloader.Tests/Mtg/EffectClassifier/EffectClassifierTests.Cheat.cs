using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // A free cast under a FIXED mana-value cap is no Cheat, ruled 2026-10-16 on
    // Perception Bobblehead ("è 3 o meno il costo che ti regala"): Filter alone
    // after a look, CardAdvantage out of an exiled pile.
    [InlineData("Perception Bobblehead", "Artifact — Bobblehead",
        "{T}: Add one mana of any color.\n{3}, {T}: Look at the top X cards of your library, where X is the "
        + "number of Bobbleheads you control. You may cast a spell with mana value 3 or less from among them "
        + "without paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Collected Conjuring", "Sorcery",
        "Exile the top six cards of your library. You may cast up to two sorcery spells with mana value 3 or "
        + "less from among them without paying their mana costs. Put the exiled cards not cast this way on the "
        + "bottom of your library in a random order.")]
    [InlineData("Hazoret's Undying Fury", "Sorcery",
        "Shuffle your library, then exile the top four cards. You may cast any number of spells with mana "
        + "value 5 or less from among them without paying their mana costs. Lands you control don't untap "
        + "during your next untap step.")]
    public void Classify_AFreeCastUnderAFixedCap_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Cheat, ruled 2026-09-18: the permanent arrives without being cast.
    [InlineData("Sneak Attack", "Enchantment",
        "{R}: You may put a creature card from your hand onto the battlefield. That creature gains haste. "
        + "Sacrifice the creature at the beginning of the next end step.")]
    [InlineData("Elvish Piper", "Creature — Elf Shaman",
        "{G}, {T}: You may put a creature card from your hand onto the battlefield.")]
    [InlineData("Show and Tell", "Sorcery",
        "Each player may put an artifact, creature, enchantment, or land card from their hand onto the battlefield.")]
    [InlineData("Natural Order", "Sorcery",
        "As an additional cost to cast this spell, sacrifice a green creature.\n"
        + "Search your library for a green creature card, put it onto the battlefield, then shuffle.")]
    [InlineData("Omniscience", "Enchantment",
        "You may cast spells from your hand without paying their mana costs.")]
    // Read and ruled 2026-10-13. Out of hand: "any number of", and a cap after
    // the noun that is not "lesser".
    [InlineData("Ghalta, Stampede Tyrant", "Legendary Creature — Elder Dinosaur",
        "Trample\nWhen Ghalta enters, put any number of creature cards from your hand onto the battlefield.")]
    // Out of the library by a SEARCH, uncapped, for any number. The look and
    // the reveal pinned here (Doors of Durin) left on 2026-11-03 with
    // everything off the top, see
    // Classify_TheNovember3Redefinition_ACardOffTheTop_IsNotCheat; Arthur,
    // Marigold Knight left the same day because its creature goes back to the
    // hand, see Classify_TheNovember3CheatPass_ACreatureThatGoesBackToTheHand_IsNotCheat.
    [InlineData("Bilbo, Birthday Celebrant", "Legendary Creature — Halfling Rogue",
        "If you would gain life, you gain that much life plus 1 instead.\n{2}{W}{B}{G}, {T}, Exile Bilbo: "
        + "Search your library for any number of creature cards, put them onto the battlefield, then shuffle. "
        + "Activate only if you have 111 or more life.")]
    // Uncapped free casts: a spell from hand, another spell's cost paid
    // otherwise. The pile (Dance with Calamity) and the suspend granted to a
    // card off the top (Sibylline Soothsayer) left on 2026-11-03.
    [InlineData("Great Intelligence's Plan", "Sorcery",
        "Draw three cards. Then target opponent faces a villainous choice — They discard three cards, or you "
        + "may cast a spell from your hand without paying its mana cost.")]
    [InlineData("Dream Halls", "Enchantment",
        "Rather than pay the mana cost for a spell, its controller may discard a card that shares a color "
        + "with that spell.")]
    public void Classify_APermanentThatArrivesWithoutBeingCast_IsCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Three edges of the same ruling. A LAND put onto the battlefield is Ramp —
    // that is the mana, not the cheat.
    [InlineData("Skyshroud Ranger", "Creature — Elf Scout",
        "{T}: You may put a land card from your hand onto the battlefield. Activate only as a sorcery.")]
    // A GRAVEYARD is Reanimate, which keeps its own tag.
    [InlineData("Reanimate", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control. "
        + "You lose life equal to that card's mana value.")]
    // And casting the cards THIS card just exiled is the impulse rider, which is
    // CardAdvantage. Ugin says "those cards … their mana costs", so the plural
    // alone does not separate it — the object phrase has to be read too.
    [InlineData("Ugin, Eye of the Storms", "Legendary Planeswalker — Ugin",
        "−11: Search your library for any number of colorless nonland cards, exile them, then shuffle. "
        + "Until end of turn, you may cast those cards without paying their mana costs.")]
    // Ruled 2026-10-13: a LESSER cap (Shadowfax), an opponent putting from
    // their own hand (Wumpus Aberration), a mana-value cap on the card taken
    // from the library (Emergent Woodwurm: Filter), a graveyard reveal
    // (Deadbridge Chant: Reanimate), a pile the card filled itself (Forger's
    // Foundry), copies (Arcane Bombardment), a card suspending itself (Rory
    // Williams), and cascade under a mana-value cap (Breaching Dragonstorm).
    [InlineData("Shadowfax, Lord of Horses", "Legendary Creature — Horse",
        "Horses you control have haste. (They can attack and {T} as soon as they come under your "
        + "control.)\nWhenever Shadowfax attacks, you may put a creature card with lesser power from your hand "
        + "onto the battlefield tapped and attacking.")]
    [InlineData("Wumpus Aberration", "Creature — Eldrazi Beast",
        "Devoid (This card has no color.)\nWhen you cast this spell, if {C} wasn't spent to cast it, target "
        + "opponent may put a creature card from their hand onto the battlefield.\nTrample")]
    [InlineData("Emergent Woodwurm", "Creature — Wurm",
        "Backup 3 (When this creature enters, put three +1/+1 counters on target creature. If that's another "
        + "creature, it gains the following ability until end of turn.)\nWhenever this creature attacks, look at "
        + "the top X cards of your library, where X is its power. You may put a permanent card with mana value "
        + "X or less from among them onto the battlefield. Put the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Deadbridge Chant", "Enchantment",
        "When this enchantment enters, mill ten cards.\nAt the beginning of your upkeep, choose a card at "
        + "random in your graveyard. If it's a creature card, put it onto the battlefield. Otherwise, put it "
        + "into your hand.")]
    [InlineData("Forger's Foundry", "Artifact",
        "{T}: Add {U}. When you spend this mana to cast an instant or sorcery spell with mana value 3 or "
        + "less, you may exile that spell instead of putting it into its owner's graveyard as it "
        + "resolves.\n{3}{U}{U}, {T}: You may cast any number of spells from among cards exiled with this "
        + "artifact without paying their mana costs. Activate only as a sorcery.")]
    [InlineData("Arcane Bombardment", "Enchantment",
        "Whenever you cast your first instant or sorcery spell each turn, exile an instant or sorcery card at "
        + "random from your graveyard. Then copy each card exiled with this enchantment. You may cast any "
        + "number of the copies without paying their mana costs.")]
    [InlineData("Rory Williams", "Legendary Creature — Human Soldier",
        "Partner with Amy Pond\nFirst strike, lifelink\nThe Last Centurion — When you cast this spell from "
        + "anywhere other than exile, exile it with three time counters on it. It gains suspend. Then "
        + "investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    [InlineData("Breaching Dragonstorm", "Enchantment",
        "When this enchantment enters, exile cards from the top of your library until you exile a nonland "
        + "card. You may cast it without paying its mana cost if that spell's mana value is 8 or less. If you "
        + "don't, put that card into your hand.\nWhen a Dragon you control enters, return this enchantment to "
        + "its owner's hand.")]
    public void Classify_TheOtherWaysAPermanentArrives_AreNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Fact]
    public void Classify_TriassicEgg_IsBothCheatAndReanimate()
    {
        // The card that shows the two tags are one trick from two zones, and
        // why they are not merged: it offers you the choice.
        CardEffect result = EffectClassifier.Classify(MakeCard("Triassic Egg", "Artifact",
            "{3}, {T}: Put a hatchling counter on this artifact.\n"
            + "Sacrifice this artifact: Choose one. Activate only if there are two or more hatchling counters "
            + "on this artifact.\n• You may put a creature card from your hand onto the battlefield.\n"
            + "• Return target creature card from your graveyard to the battlefield."));

        Assert.True(result.HasFlag(CardEffect.Cheat));
        Assert.True(result.HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Cheat pass of 2026-10-16: a card chosen from the hand (The Seventh Doctor)
    // and the card a loot discarded (Malcolm) cast free. The mill (Bramble
    // Familiar), the looks that exile a card and cast it free (Djeru and
    // Hazoret, The Dragon-Kami Reborn) and the exile until a nonland card
    // (Sarevok's Tome) pinned here the same day left on 2026-11-03: they come
    // off the top.
    [InlineData("The Seventh Doctor", "Legendary Creature — Time Lord Doctor",
        "Whenever The Seventh Doctor attacks, choose a card in your hand. Defending player guesses whether "
        + "that card's mana value is greater than the number of artifacts you control. If they guessed wrong, "
        + "you may cast it without paying its mana cost. If you don't cast a spell this way, investigate. "
        + "(Create a Clue token. It's an artifact with \"{2}, Sacrifice this token: Draw a card.\")")]
    [InlineData("Malcolm, Alluring Scoundrel", "Legendary Creature — Siren Pirate",
        "Flash\nFlying\nWhenever Malcolm deals combat damage to a player, put a chorus counter on it. Draw a "
        + "card, then discard a card. If there are four or more chorus counters on Malcolm, you may cast the "
        + "discarded card without paying its mana cost.")]
    public void Classify_TheCheatPassOfOctober16_UncappedFreeCasts_AreCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-10-16 ("Hideaway no, altri sì"): hideaway is Filter alone, its
    // play waiting on a condition (Evercoat Ursine handed back, Collector's Cage).
    // A cap keeps the free cast out: Keldon Flamesage's 'mana value X or less',
    // Discover the Impossible's 'if it's an instant spell', Ryan Sinclair's lesser
    // mana value.
    [InlineData("Evercoat Ursine", "Creature — Elemental Bear",
        "Trample\nHideaway 3, hideaway 3 (When this creature enters, look at the top three cards of your "
        + "library, exile one face down, then put the rest on the bottom in a random order. Then do it "
        + "again.)\nWhenever this creature deals combat damage to a player, if there are cards exiled with it, "
        + "you may play one of them without paying its mana cost.")]
    [InlineData("Collector's Cage", "Artifact",
        "Hideaway 5 (When this artifact enters, look at the top five cards of your library, exile one face "
        + "down, then put the rest on the bottom in a random order.)\n{1}, {T}: Put a +1/+1 counter on target "
        + "creature you control. Then if you control three or more creatures with different powers, you may "
        + "play the exiled card without paying its mana cost.")]
    [InlineData("Keldon Flamesage", "Creature — Human Shaman",
        "Enlist\nWhenever this creature attacks, look at the top X cards of your library, where X is this "
        + "creature's power. You may exile an instant or sorcery card with mana value X or less from among "
        + "them. Put the rest on the bottom of your library in a random order. You may cast the exiled card "
        + "without paying its mana cost.")]
    [InlineData("Discover the Impossible", "Instant",
        "Look at the top five cards of your library. Exile one of them face down and put the rest on the "
        + "bottom of your library in a random order. You may cast the exiled card without paying its mana cost "
        + "if it's an instant spell with mana value 2 or less. If you don't, put that card into your hand.")]
    [InlineData("Ryan Sinclair", "Legendary Creature — Human",
        "Whenever Ryan attacks, exile cards from the top of your library until you exile a nonland card. You "
        + "may cast the exiled card without paying its mana cost if it's a spell with mana value less than or "
        + "equal to Ryan's power. Put the exiled cards not cast this way on the bottom of your library in a "
        + "random order.\nDoctor's companion (You can have two commanders if the other is the Doctor.)")]
    public void Classify_TheCheatPassOfOctober16_HideawayAndCappedCasts_AreNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Read 2026-10-25 by rulings already made, each the one wording a card fell
    // through: a noun list ending in a land out of the hand (Court of Bounty), a
    // typed spell out of the hand or graveyard paid with life (Anrakyr), suspend
    // spelled out with time counters (Alaundo). Ruled the same day on Covert
    // Technician that a cap the board sets is an X cap; overturned 2026-11-03
    // ("Ogni tetto fuori"), Covert Technician and Anticausal Vestige moved to
    // AnyCapOnACardFromTheHand. The reveals UNTIL a creature (Aspiring Champion, Atla
    // Palani), Over the Top's permanents revealed this way and Ignite the
    // Future's pile played free, read the same day, left on 2026-11-03: they
    // come off the top.
    [InlineData("Court of Bounty", "Enchantment",
        "When this enchantment enters, you become the monarch.\nAt the beginning of your upkeep, you may put a "
        + "land card from your hand onto the battlefield. If you're the monarch, instead you may put a creature "
        + "or land card from your hand onto the battlefield.")]
    [InlineData("Anrakyr the Traveller", "Legendary Artifact Creature — Necron",
        "Lord of the Pyrrhian Legions — Whenever Anrakyr the Traveller attacks, you may cast an artifact "
        + "spell from your hand or graveyard by paying life equal to its mana value rather than paying its mana "
        + "cost.")]
    [InlineData("Alaundo the Seer", "Legendary Creature — Human Shaman",
        "{T}: Draw a card, then exile a card from your hand and put a number of time counters on it equal to "
        + "its mana value. It gains \"When the last time counter is removed from this card, if it's exiled, you "
        + "may cast it without paying its mana cost. If you cast a creature spell this way, it gains haste "
        + "until end of turn.\" Then remove a time counter from each other card you own in exile.")]
    public void Classify_TheCheatPassOfOctober25_TheReadings_AreCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-10-25: a tutor for a copy of a creature on the battlefield is
    // Tutor alone (March of Burgeoning Life, 'stesso nome'), and Hama's
    // waterbending {X}, which pays the whole mana value, was left out of the cost
    // paid otherwise. Kept out by the rulings the new wordings sit next to: a
    // strict 'less than' and 'equal or lesser' are the lesser cap (Arni Metalbrow,
    // Kodama of the East Tree, Kethek), a land revealed is Ramp (Clifftop
    // Lookout), and a card casting itself (Squee), a copy (Blue Mage's Cane) or a
    // fixed cap (Primal Prayers) is not a cost paid otherwise.
    [InlineData("March of Burgeoning Life", "Instant",
        "As an additional cost to cast this spell, you may exile any number of green cards from your hand. "
        + "This spell costs {2} less to cast for each card exiled this way.\nChoose target creature with mana "
        + "value less than X. Search your library for a creature card with the same name as that creature, put "
        + "it onto the battlefield tapped, then shuffle.")]
    [InlineData("Hama, the Bloodbender", "Legendary Creature — Human Warlock",
        "When Hama enters, target opponent mills three cards. Exile up to one noncreature, nonland card from "
        + "that player's graveyard. For as long as you control Hama, you may cast the exiled card during your "
        + "turn by waterbending {X} rather than paying its mana cost, where X is its mana value. (While paying "
        + "a waterbend cost, you can tap your artifacts and creatures to help. Each one pays for {1}.)")]
    [InlineData("Arni Metalbrow", "Legendary Creature — Human Berserker",
        "Whenever a creature you control attacks or enters attacking, you may pay {1}{R}. If you do, you may "
        + "put a creature card with mana value less than that creature's mana value from your hand onto the "
        + "battlefield tapped and attacking.")]
    [InlineData("Kodama of the East Tree", "Legendary Creature — Spirit",
        "Reach\nWhenever another permanent you control enters, if it wasn't put onto the battlefield with this "
        + "ability, you may put a permanent card with equal or lesser mana value from your hand onto the "
        + "battlefield.\nPartner (You can have two commanders if both have partner.)")]
    [InlineData("Kethek, Crucible Goliath", "Legendary Creature — Phyrexian Beast",
        "At the beginning of your end step, you may sacrifice another creature. If you do, reveal cards from "
        + "the top of your library until you reveal a nonlegendary creature card with lesser mana value, put it "
        + "onto the battlefield, then put the rest on the bottom of your library in a random order.")]
    [InlineData("Clifftop Lookout", "Creature — Frog Scout",
        "Reach\nWhen this creature enters, reveal cards from the top of your library until you reveal a land "
        + "card. Put that card onto the battlefield tapped and the rest on the bottom of your library in a "
        + "random order.")]
    [InlineData("Squee, Dubious Monarch", "Legendary Creature — Goblin Noble",
        "Haste\nWhenever Squee attacks, create a 1/1 red Goblin creature token that's tapped and "
        + "attacking.\nYou may cast this card from your graveyard by paying {3}{R} and exiling four other cards "
        + "from your graveyard rather than paying its mana cost.")]
    [InlineData("Blue Mage's Cane", "Artifact — Equipment",
        "Job select\nEquipped creature gets +0/+2, is a Wizard in addition to its other types, and has "
        + "\"Whenever this creature attacks, exile up to one target instant or sorcery card from defending "
        + "player's graveyard. If you do, copy it. You may cast the copy by paying {3} rather than paying its "
        + "mana cost.\"\nSpirit of the Whalaqee — Equip {2}")]
    [InlineData("Primal Prayers", "Enchantment",
        "When this enchantment enters, you get {E}{E} (two energy counters).\nYou may cast creature spells "
        + "with mana value 3 or less by paying {E} rather than paying their mana costs. If you cast a spell "
        + "this way, you may cast it as though it had flash.")]
    public void Classify_TheCheatPassOfOctober25_AreNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-11-03 ("toglierei Cheat ad entrambe perché la carta torna in
    // mano"): a creature put onto the battlefield and returned to the hand at the
    // next end step or at end of combat is not Cheat, wherever it came from.
    // Named: Anzrag's Rampage, Random Encounter. Reached: Arthur, Marigold Knight
    // (handed back), Nahiri, the Harbinger, Swift Warkite. From the HAND too
    // ("Sì, da qualunque zona"): Ilharg, the Raze-Boar and Surprise Deployment.
    // And the return granted in a quote to the creature put (Aethermage's Touch).
    [InlineData("Aethermage's Touch", "Instant",
        "Reveal the top four cards of your library. You may put a creature card from among them onto the "
        + "battlefield. It gains \"At the beginning of your end step, return this creature to its owner's hand.\" "
        + "Then put the rest of the cards revealed this way on the bottom of your library in any order.")]
    [InlineData("Anzrag's Rampage", "Sorcery",
        "Destroy all artifacts you don't control, then exile the top X cards of your library, where X is the "
        + "number of artifacts that were put into graveyards from the battlefield this turn. You may put a "
        + "creature card exiled this way onto the battlefield. It gains haste. Return it to your hand at the "
        + "beginning of the next end step.")]
    [InlineData("Random Encounter", "Sorcery",
        "Shuffle your library, then mill four cards. Put each creature card milled this way onto the "
        + "battlefield. They gain haste. At the beginning of the next end step, return those creatures to their "
        + "owner's hand.\nFlashback {6}{R}{R} (You may cast this card from your graveyard for its flashback "
        + "cost. Then exile it.)")]
    [InlineData("Arthur, Marigold Knight", "Legendary Creature — Mouse Knight",
        "Haste\nWhenever Arthur and at least one other creature attack, look at the top six cards of your "
        + "library. You may put a creature card from among them onto the battlefield tapped and attacking. Put "
        + "the rest on the bottom of your library in a random order. Return that creature to its owner's hand "
        + "at end of combat.")]
    [InlineData("Nahiri, the Harbinger", "Legendary Planeswalker — Nahiri",
        "+2: You may discard a card. If you do, draw a card.\n−2: Exile target enchantment, tapped artifact, "
        + "or tapped creature.\n−8: Search your library for an artifact or creature card, put it onto the "
        + "battlefield, then shuffle. It gains haste. Return it to your hand at the beginning of the next end "
        + "step.")]
    [InlineData("Swift Warkite", "Creature — Dragon",
        "Flying\nWhen this creature enters, you may put a creature card with mana value 3 or less from your "
        + "hand or graveyard onto the battlefield. That creature gains haste. Return it to your hand at the "
        + "beginning of the next end step.")]
    [InlineData("Ilharg, the Raze-Boar", "Legendary Creature — Boar God",
        "Trample\nWhenever Ilharg attacks, you may put a creature card from your hand onto the battlefield "
        + "tapped and attacking. Return that creature to your hand at the beginning of the next end step.\nWhen "
        + "Ilharg dies or is put into exile from the battlefield, you may put it into its owner's library third "
        + "from the top.")]
    [InlineData("Surprise Deployment", "Instant",
        "Cast this spell only during combat.\nYou may put a nonwhite creature card from your hand onto the "
        + "battlefield. At the beginning of the next end step, return that creature to your hand. (Return it "
        + "only if it's on the battlefield.)")]
    public void Classify_TheNovember3CheatPass_ACreatureThatGoesBackToTheHand_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Cauldron Dance keeps Cheat through its OTHER sentence: a creature from the
    // hand put onto the battlefield and SACRIFICED at the end step, not returned.
    // Metathran Aerostat and Dermoplasm return themselves, not the creature they
    // put.
    [InlineData("Cauldron Dance", "Instant",
        "Cast this spell only during combat.\nReturn target creature card from your graveyard to the "
        + "battlefield. That creature gains haste. Return it to your hand at the beginning of the next end "
        + "step.\nYou may put a creature card from your hand onto the battlefield. That creature gains haste. "
        + "Its controller sacrifices it at the beginning of the next end step.")]
    [InlineData("Metathran Aerostat", "Creature — Metathran",
        "Flying\n{X}{U}: You may put a creature card with mana value X from your hand onto the battlefield. If "
        + "you do, return this creature to its owner's hand.")]
    [InlineData("Dermoplasm", "Creature — Shapeshifter",
        "Flying\nMorph {2}{U}{U} (You may cast this card face down as a 2/2 creature for {3}. Turn it face up "
        + "any time for its morph cost.)\nWhen this creature is turned face up, you may put a creature card with "
        + "a morph ability from your hand onto the battlefield face up. If you do, return this creature to its "
        + "owner's hand.")]
    public void Classify_TheNovember3CheatPass_ACreatureNotGivenBack_IsStillCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Overturned 2026-11-03 by the redefinition ('1. CA'): Cheat is the cost
    // skipped on a card the player CHOSE, and nothing off the top of a library is.
    // A card off the top cast free, once or again and again, mine or an opponent's
    // (the again-and-again Cheat of the same morning, Caves of Chaos Adventurer
    // and Gríma), and a pile cast free, under an X cap or none (Kotis, Villainous
    // Wealth, the 2026-10-13 'carta grossa'). Fevered Suspicion's 'among those
    // nonland cards' had got past the plural permission.
    [InlineData("Caves of Chaos Adventurer", "Creature — Human Barbarian",
        "Trample\nWhen this creature enters, you take the initiative.\nWhenever this creature attacks, exile "
        + "the top card of your library. If you've completed a dungeon, you may play that card this turn "
        + "without paying its mana cost. Otherwise, you may play that card this turn.")]
    [InlineData("Planetarium of Wan Shi Tong", "Legendary Artifact",
        "{1}, {T}: Scry 2.\nWhenever you scry or surveil, look at the top card of your library. You may cast "
        + "that card without paying its mana cost. Do this only once each turn. (Look at the card after you "
        + "scry or surveil.)")]
    [InlineData("Galvanoth", "Creature — Beast",
        "At the beginning of your upkeep, you may look at the top card of your library. You may cast it "
        + "without paying its mana cost if it's an instant or sorcery spell.")]
    [InlineData("Intet, the Dreamer", "Legendary Creature — Dragon",
        "Flying\nWhenever Intet deals combat damage to a player, you may pay {2}{U}. If you do, exile the top "
        + "card of your library face down. You may look at that card for as long as it remains exiled. You may "
        + "play that card without paying its mana cost for as long as Intet remains on the battlefield.")]
    [InlineData("Oracle's Vault", "Artifact",
        "{2}, {T}: Exile the top card of your library. Until end of turn, you may play that card. Put a brick "
        + "counter on this artifact.\n{T}: Exile the top card of your library. Until end of turn, you may play "
        + "that card without paying its mana cost. Activate only if there are three or more brick counters on "
        + "this artifact.")]
    [InlineData("Yennett, Cryptic Sovereign", "Legendary Creature — Sphinx",
        "Flying, vigilance, menace\nWhenever Yennett attacks, reveal the top card of your library. You may "
        + "cast it without paying its mana cost if its mana value is odd. If you don't cast it, draw a card.")]
    [InlineData("Temporal Aperture", "Artifact",
        "{5}, {T}: Shuffle your library, then reveal the top card. Until end of turn, for as long as that "
        + "card remains on top of your library, play with the top card of your library revealed and you may "
        + "play that card without paying its mana cost. (If it has X in its mana cost, X is 0.)")]
    [InlineData("Djinn of Wishes", "Creature — Djinn",
        "Flying\nThis creature enters with three wish counters on it.\n{2}{U}{U}, Remove a wish counter from "
        + "this creature: Reveal the top card of your library. You may play that card without paying its mana "
        + "cost. If you don't, exile it.")]
    [InlineData("Sarevok's Tome", "Artifact — Book",
        "When this artifact enters, you take the initiative.\n{T}: Add {C}. If you have the initiative, add "
        + "{C}{C} instead.\n{3}, {T}: Exile cards from the top of your library until you exile a nonland card. "
        + "You may cast that card without paying its mana cost. Activate only if you've completed a dungeon.")]
    [InlineData("Gríma, Saruman's Footman", "Legendary Creature — Human Advisor",
        "Gríma can't be blocked.\nWhenever Gríma deals combat damage to a player, that player exiles cards "
        + "from the top of their library until they exile an instant or sorcery card. You may cast that card "
        + "without paying its mana cost. Then that player puts the exiled cards that weren't cast this way on "
        + "the bottom of their library in a random order.")]
    [InlineData("Fallen Shinobi", "Creature — Zombie Ninja",
        "Ninjutsu {2}{U}{B} ({2}{U}{B}, Return an unblocked attacker you control to hand: Put this card onto "
        + "the battlefield from your hand tapped and attacking.)\nWhenever this creature deals combat damage to "
        + "a player, that player exiles the top two cards of their library. Until end of turn, you may play "
        + "those cards without paying their mana costs.")]
    [InlineData("Fire Lord Ozai", "Legendary Creature — Human Noble",
        "Whenever Fire Lord Ozai attacks, you may sacrifice another creature. If you do, add an amount of {R} "
        + "equal to the sacrificed creature's power. Until end of combat, you don't lose this mana as steps "
        + "end.\n{6}: Exile the top card of each opponent's library. Until end of turn, you may play one of "
        + "those cards without paying its mana cost.")]
    [InlineData("Court of Locthwain", "Enchantment",
        "When this enchantment enters, you become the monarch.\nAt the beginning of your upkeep, exile the top "
        + "card of target opponent's library. You may play that card for as long as it remains exiled, and mana "
        + "of any type can be spent to cast it. If you're the monarch, until end of turn, you may cast a spell "
        + "from among cards exiled with this enchantment without paying its mana cost.")]
    [InlineData("Mind's Dilation", "Enchantment",
        "Whenever an opponent casts their first spell each turn, that player exiles the top card of their "
        + "library. If it's a nonland card, you may cast it without paying its mana cost.")]
    [InlineData("Kotis, the Fangkeeper", "Legendary Creature — Zombie Warrior",
        "Indestructible\nWhenever Kotis deals combat damage to a player, exile the top X cards of their "
        + "library, where X is the amount of damage dealt. You may cast any number of spells with mana value X "
        + "or less from among them without paying their mana costs.")]
    [InlineData("Villainous Wealth", "Sorcery",
        "Target opponent exiles the top X cards of their library. You may cast any number of spells with mana "
        + "value X or less from among them without paying their mana costs.")]
    [InlineData("Dance with Calamity", "Sorcery",
        "Shuffle your library. As many times as you choose, you may exile the top card of your library. If "
        + "the total mana value of the cards exiled this way is 13 or less, you may cast any number of spells "
        + "from among those cards without paying their mana costs.")]
    [InlineData("Ignite the Future", "Sorcery",
        "Exile the top three cards of your library. Until the end of your next turn, you may play those "
        + "cards. If this spell was cast from a graveyard, you may play cards this way without paying their "
        + "mana costs.\nFlashback {7}{R} (You may cast this card from your graveyard for its flashback cost. "
        + "Then exile it.)")]
    [InlineData("Fevered Suspicion", "Sorcery",
        "Each opponent exiles cards from the top of their library until they exile a nonland card. You may "
        + "cast any number of spells from among those nonland cards without paying their mana costs.\nRebound "
        + "(If you cast this spell from your hand, exile it as it resolves. At the beginning of your next "
        + "upkeep, you may cast this card from exile without paying its mana cost.)")]
    // Overturned the same day: the cost paid otherwise for a card off the top, the
    // 2026-10-25 Xander's Pact ruling ('Cheat a tutti'): life (Xander's Pact,
    // Bismuth Mindrender, Nashi, Valgavoth, Gwenom), a discard (The Infamous
    // Cruelclaw), {1} (Jadzi), and Eye of Duskmantle's surveilled cards, which
    // keeps Regrowth.
    [InlineData("Xander's Pact", "Sorcery",
        "Casualty 2 (As you cast this spell, you may sacrifice a creature with power 2 or greater. When you "
        + "do, copy this spell.)\nEach opponent exiles the top card of their library. You may cast spells from "
        + "among those cards this turn. If you cast a spell this way, pay life equal to that spell's mana value "
        + "rather than pay its mana cost.")]
    [InlineData("Bismuth Mindrender", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nMenace\nWhenever this creature deals combat damage to a player, that "
        + "player exiles cards from the top of their library until they exile a nonland card. You may cast that "
        + "card by paying life equal to the spell's mana value rather than paying its mana cost.")]
    [InlineData("Nashi, Moon Sage's Scion", "Legendary Creature — Rat Ninja",
        "Ninjutsu {3}{B} ({3}{B}, Return an unblocked attacker you control to hand: Put this card onto the "
        + "battlefield from your hand tapped and attacking.)\nWhenever Nashi deals combat damage to a player, "
        + "exile the top card of each player's library. Until end of turn, you may play one of those cards. If "
        + "you cast a spell this way, pay life equal to its mana value rather than paying its mana cost.")]
    [InlineData("Valgavoth, Terror Eater", "Legendary Creature — Elder Demon",
        "Flying, lifelink\nWard—Sacrifice three nonland permanents.\nIf a card you didn't control would be put "
        + "into an opponent's graveyard from anywhere, exile it instead.\nDuring your turn, you may play cards "
        + "exiled with Valgavoth. If you cast a spell this way, pay life equal to its mana value rather than "
        + "pay its mana cost.")]
    [InlineData("Gwenom, Remorseless", "Legendary Creature — Symbiote Spider Hero",
        "Deathtouch, lifelink\nWhenever Gwenom attacks, until end of turn, you may look at the top card of "
        + "your library any time and you may play cards from the top of your library. If you cast a spell this "
        + "way, pay life equal to its mana value rather than pay its mana cost.")]
    [InlineData("The Infamous Cruelclaw", "Legendary Creature — Weasel Mercenary",
        "Menace\nWhenever The Infamous Cruelclaw deals combat damage to a player, exile cards from the top of "
        + "your library until you exile a nonland card. You may cast that card by discarding a card rather than "
        + "paying its mana cost.")]
    [InlineData("Jadzi, Oracle of Arcavios // Journey to the Oracle", "Legendary Creature — Human Wizard // Sorcery",
        "Discard a card: Return Jadzi to its owner's hand.\nMagecraft — Whenever you cast or copy an instant "
        + "or sorcery spell, reveal the top card of your library. If it's a nonland card, you may cast it by "
        + "paying {1} rather than paying its mana cost. If it's a land card, put it onto the battlefield.\nYou "
        + "may put any number of land cards from your hand onto the battlefield. Then if you control eight or "
        + "more lands, you may discard a card. If you do, return Journey to the Oracle to its owner's hand.")]
    [InlineData("Eye of Duskmantle", "Creature — Eye",
        "Flying, lifelink\nYou may play lands and cast spells from among cards in your graveyard you've "
        + "surveilled this turn. If you cast a spell this way, you pay life equal to its mana value rather than "
        + "paying its mana cost.")]
    // Overturned the same day ('2. NO', 'Fuori'): a card off the top put onto the
    // battlefield, looked at and chosen (Aetherworks Marvel cast free, Kinnan,
    // Bramble Familiar's mill, Djeru and Hazoret and The Dragon-Kami Reborn exiled
    // and cast later), revealed this way (Doors of Durin, Over the Top), revealed
    // until a creature (Aspiring Champion, Atla Palani), and suspend granted to a
    // card off the top (Sibylline Soothsayer).
    [InlineData("Aetherworks Marvel", "Legendary Artifact",
        "Whenever a permanent you control is put into a graveyard, you get {E} (an energy counter).\n{T}, Pay "
        + "six {E}: Look at the top six cards of your library. You may cast a spell from among them without "
        + "paying its mana cost. Put the rest on the bottom of your library in a random order.")]
    [InlineData("Kinnan, Bonder Prodigy", "Legendary Creature — Human Druid",
        "Whenever you tap a nonland permanent for mana, add one mana of any type that permanent "
        + "produced.\n{5}{G}{U}: Look at the top five cards of your library. You may put a non-Human creature "
        + "card from among them onto the battlefield. Put the rest on the bottom of your library in a random "
        + "order.")]
    [InlineData("Bramble Familiar // Fetch Quest", "Creature — Elemental Raccoon // Sorcery — Adventure",
        "{T}: Add {G}.\n{1}{G}, {T}, Discard a card: Return this creature to its owner's hand.\nMill seven "
        + "cards. Then put a creature, enchantment, or land card from among the milled cards onto the "
        + "battlefield.")]
    [InlineData("Djeru and Hazoret", "Legendary Creature — Human God",
        "As long as you have one or fewer cards in hand, Djeru and Hazoret has vigilance and haste.\nWhenever "
        + "Djeru and Hazoret attacks, look at the top six cards of your library. You may exile a legendary "
        + "creature card from among them. Put the rest on the bottom of your library in a random order. Until "
        + "end of turn, you may cast the exiled card without paying its mana cost.")]
    [InlineData("The Dragon-Kami Reborn // Dragon-Kami's Egg", "Enchantment — Saga // Enchantment Creature — Egg",
        "(As this Saga enters and after your draw step, add a lore counter.)\nI, II — You gain 2 life. Look at "
        + "the top three cards of your library. Exile one of them face down with a hatching counter on it, then "
        + "put the rest on the bottom of your library in any order.\nIII — Exile this Saga, then return it to "
        + "the battlefield transformed under your control.\nWhenever this creature or a Dragon you control dies, "
        + "you may cast a creature spell from among cards you own in exile with hatching counters on them "
        + "without paying its mana cost.")]
    [InlineData("Doors of Durin", "Legendary Artifact",
        "Whenever you attack, scry 2, then you may reveal the top card of your library. If a creature card is "
        + "revealed this way, put it onto the battlefield tapped and attacking. Until your next turn, it gains "
        + "trample if you control a Dwarf and hexproof if you control an Elf.")]
    [InlineData("Over the Top", "Sorcery",
        "Each player reveals a number of cards from the top of their library equal to the number of nonland "
        + "permanents they control, puts all permanent cards they revealed this way onto the battlefield, and "
        + "puts the rest into their graveyard.")]
    [InlineData("Aspiring Champion", "Creature — Astartes Warrior",
        "Menace\nRuinous Ascension — When this creature deals combat damage to a player, sacrifice it. If you "
        + "do, reveal cards from the top of your library until you reveal a creature card. Put that card onto "
        + "the battlefield, then shuffle the rest into your library. If that creature is a Demon, it deals "
        + "damage equal to its power to each opponent.")]
    [InlineData("Atla Palani, Nest Tender", "Legendary Creature — Human Shaman",
        "{2}, {T}: Create a 0/1 green Egg creature token with defender.\nWhenever an Egg you control dies, "
        + "reveal cards from the top of your library until you reveal a creature card. Put that card onto the "
        + "battlefield and the rest on the bottom of your library in a random order.")]
    [InlineData("Sibylline Soothsayer", "Creature — Human Warlock",
        "Temporal Foresight — When this creature enters, reveal cards from the top of your library until you "
        + "reveal a nonland card with mana value 3 or greater. Exile that card with three time counters on it. "
        + "If it doesn't have suspend, it gains suspend. Put the rest of the revealed cards on the bottom of "
        + "your library in a random order.")]
    public void Classify_TheNovember3Redefinition_ACardOffTheTop_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-11-03 ('7. OK'): a real FIXED alternative cost for spells the
    // player chooses is no cap, so it is Cheat: the five colours of Leyline of
    // Mutation, Fist of Suns and Jodah, Nissa, Worldsoul Speaker's eight energy,
    // Conspiracy Unraveler's collect evidence 10, all untagged by hand before
    // 10-25. Read by the same ruling and handed back: Darksteel Monolith's
    // colorless spell from the hand, Demon of Fate's Design's enchantment spell
    // paid with life; Marshland Bloodcaster's next spell, unreviewed. Tooth and
    // Nail, 'il cheat per eccellenza', stays Tutor and Cheat.
    [InlineData("Leyline of Mutation", "Enchantment",
        "If this card is in your opening hand, you may begin the game with it on the battlefield.\nYou may pay "
        + "{W}{U}{B}{R}{G} rather than pay the mana cost for spells you cast.")]
    [InlineData("Fist of Suns", "Artifact",
        "You may pay {W}{U}{B}{R}{G} rather than pay the mana cost for spells you cast.")]
    [InlineData("Jodah, Archmage Eternal", "Legendary Creature — Human Wizard",
        "Flying\nYou may pay {W}{U}{B}{R}{G} rather than pay the mana cost for spells you cast.")]
    [InlineData("Nissa, Worldsoul Speaker", "Legendary Creature — Elf Druid",
        "Landfall — Whenever a land you control enters, you get {E}{E} (two energy counters).\nYou may pay "
        + "eight {E} rather than pay the mana cost for permanent spells you cast.")]
    [InlineData("Conspiracy Unraveler", "Creature — Sphinx Detective",
        "Flying\nYou may collect evidence 10 rather than pay the mana cost for spells you cast. (To collect "
        + "evidence 10, exile cards with total mana value 10 or greater from your graveyard.)")]
    [InlineData("Darksteel Monolith", "Artifact",
        "Indestructible\nOnce each turn, you may pay {0} rather than pay the mana cost for a colorless spell "
        + "you cast from your hand.")]
    [InlineData("Demon of Fate's Design", "Enchantment Creature — Demon",
        "Flying, trample\nOnce during each of your turns, you may cast an enchantment spell by paying life "
        + "equal to its mana value rather than paying its mana cost.\n{2}{B}, Sacrifice another enchantment: "
        + "This creature gets +X/+0 until end of turn, where X is the sacrificed enchantment's mana value.")]
    [InlineData("Marshland Bloodcaster", "Creature — Vampire Warlock",
        "Flying\n{1}{B}, {T}: Rather than pay the mana cost of the next spell you cast this turn, you may pay "
        + "life equal to that spell's mana value.")]
    [InlineData("Tooth and Nail", "Sorcery",
        "Choose one —\n• Search your library for up to two creature cards, reveal them, put them into your "
        + "hand, then shuffle.\n• Put up to two creature cards from your hand onto the battlefield.\nEntwine {2} "
        + "(Choose both if you pay the entwine cost.)")]
    public void Classify_TheNovember3Redefinition_ARealPriceForEverySpell_IsCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-11-03: a FIXED cap keeps a card out ('3. Fuori'; Aluren's mana
    // value 3 or less, Marvo's 8 from the hand, '6. OK'), and so does an X the
    // player pays: a capped tutor is Tutor alone ('1. OK'; Green Sun's Zenith,
    // Chord of Calling, Fiend Artisan, Nature's Rhythm, Rocco), as is a fixed or a
    // lesser one (Woodland Bellower, Protean Hulk's total, Rushed Rebirth). A
    // permission over spells cast from EXILE is out ('8. OK': Charred Foyer keeps
    // CardAdvantage, Tlincalli Hunter), and so is suspend granted to a card off
    // the top (The Parting of the Ways). Winter, Cynical Opportunist's pile out of
    // the graveyard is Reanimate by hand ('5. OK'), a family of one the code does
    // not read.
    [InlineData("Aluren", "Enchantment",
        "Any player may cast creature spells with mana value 3 or less without paying their mana costs and as "
        + "though they had flash.")]
    [InlineData("Marvo, Deep Operative", "Legendary Creature — Octopus Rogue",
        "Whenever Marvo attacks, clash with defending player. (Each clashing player reveals the top card of "
        + "their library, then puts that card on their choice of the top or bottom. A player wins if their card "
        + "had a greater mana value.)\nWhenever you win a clash, draw a card. Then you may cast a spell from "
        + "your hand with mana value 8 or less without paying its mana cost.")]
    [InlineData("Green Sun's Zenith", "Sorcery",
        "Search your library for a green creature card with mana value X or less, put it onto the "
        + "battlefield, then shuffle. Shuffle Green Sun's Zenith into its owner's library.")]
    [InlineData("Chord of Calling", "Instant",
        "Convoke (Your creatures can help cast this spell. Each creature you tap while casting this spell "
        + "pays for {1} or one mana of that creature's color.)\nSearch your library for a creature card with "
        + "mana value X or less, put it onto the battlefield, then shuffle.")]
    [InlineData("Fiend Artisan", "Creature — Nightmare",
        "This creature gets +1/+1 for each creature card in your graveyard.\n{X}{B/G}, {T}, Sacrifice another "
        + "creature: Search your library for a creature card with mana value X or less, put it onto the "
        + "battlefield, then shuffle. Activate only as a sorcery.")]
    [InlineData("Nature's Rhythm", "Sorcery",
        "Search your library for a creature card with mana value X or less, put it onto the battlefield, then "
        + "shuffle.\nHarmonize {X}{G}{G}{G}{G} (You may cast this card from your graveyard for its harmonize "
        + "cost. You may tap a creature you control to reduce that cost by an amount of generic mana equal to "
        + "its power. Then exile this spell.)")]
    [InlineData("Rocco, Cabaretti Caterer", "Legendary Creature — Elf Druid",
        "When Rocco enters, if you cast it, you may search your library for a creature card with mana value X "
        + "or less, put it onto the battlefield, then shuffle.")]
    [InlineData("Woodland Bellower", "Creature — Beast",
        "When this creature enters, you may search your library for a nonlegendary green creature card with "
        + "mana value 3 or less, put it onto the battlefield, then shuffle.")]
    [InlineData("Protean Hulk", "Creature — Beast",
        "When this creature dies, search your library for any number of creature cards with total mana value "
        + "6 or less, put them onto the battlefield, then shuffle.")]
    [InlineData("Rushed Rebirth", "Instant",
        "Choose target creature. When that creature dies this turn, search your library for a creature card "
        + "with lesser mana value, put it onto the battlefield tapped, then shuffle.")]
    [InlineData("Charred Foyer // Warped Space", "Enchantment — Room // Enchantment — Room",
        "At the beginning of your upkeep, exile the top card of your library. You may play it this turn.\n(You "
        + "may cast either half. That door unlocks on the battlefield. As a sorcery, you may pay the mana cost "
        + "of a locked door to unlock it.)\nOnce each turn, you may pay {0} rather than pay the mana cost for a "
        + "spell you cast from exile.\n(You may cast either half. That door unlocks on the battlefield. As a "
        + "sorcery, you may pay the mana cost of a locked door to unlock it.)")]
    [InlineData("Tlincalli Hunter // Retrieve Prey", "Creature — Scorpion Scout // Sorcery — Adventure",
        "Trample\nOnce each turn, you may pay {0} rather than pay the mana cost for a creature spell you cast "
        + "from exile.\nExile target creature card from your graveyard. Until the end of your next turn, you may "
        + "cast that card. (Then exile this card. You may cast the creature later from exile.)")]
    [InlineData("The Parting of the Ways", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Exile "
        + "the top five cards of your library. For each nonland card exiled this way, put a number of time "
        + "counters on that card equal to its mana value. If it doesn't have suspend, it gains suspend.\nII — "
        + "Time travel, then time travel.\nIII — For each opponent, destroy up to one target artifact that "
        + "player controls.")]
    [InlineData("Winter, Cynical Opportunist", "Legendary Creature — Human Warlock",
        "Deathtouch\nWhenever Winter attacks, mill three cards.\nDelirium — At the beginning of your end step, "
        + "you may exile any number of cards from your graveyard with four or more card types among them. If "
        + "you do, put a permanent card from among them onto the battlefield with a finality counter on it.")]
    public void Classify_TheNovember3Redefinition_ACapOrAnExiledCard_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-11-03 ('Ogni tetto fuori', overturning the 2026-10-25 Covert
    // Technician ruling): every mana-value cap on a card put from the hand keeps
    // it out, the board's as well as a fixed one. The damage dealt (Covert
    // Technician, Broodcaller Scourge, named), the lands you control (Anticausal
    // Vestige, untagged by hand on 10-26), a creature's power (Paladin Elizabeth
    // Taggerdy, reached; Minn), the counters (Jhoira, reached; Aether Vial's
    // 'equal to') and a fixed 7 (Emergency Powers).
    [InlineData("Covert Technician", "Artifact Creature — Human Ninja",
        "Ninjutsu {1}{U} ({1}{U}, Return an unblocked attacker you control to hand: Put this card onto the "
        + "battlefield from your hand tapped and attacking.)\nWhenever this creature deals combat damage to a "
        + "player, you may put an artifact card with mana value less than or equal to that damage from your "
        + "hand onto the battlefield.")]
    [InlineData("Broodcaller Scourge", "Creature — Dragon",
        "Flying\nWhenever one or more Dragons you control deal combat damage to a player, you may put a "
        + "permanent card with mana value less than or equal to that damage from your hand onto the "
        + "battlefield.")]
    [InlineData("Anticausal Vestige", "Creature — Eldrazi",
        "When this creature leaves the battlefield, draw a card, then you may put a permanent card with mana "
        + "value less than or equal to the number of lands you control from your hand onto the battlefield "
        + "tapped.\nWarp {4} (You may cast this card from your hand for its warp cost. Exile this creature at "
        + "the beginning of the next end step, then you may cast it from exile on a later turn.)")]
    [InlineData("Paladin Elizabeth Taggerdy", "Legendary Creature — Human Knight",
        "Battalion — Whenever Paladin Elizabeth Taggerdy and at least two other creatures attack, draw a "
        + "card, then you may put a creature card with mana value X or less from your hand onto the battlefield "
        + "tapped and attacking, where X is Paladin Elizabeth Taggerdy's power.")]
    [InlineData("Minn, Wily Illusionist", "Legendary Creature — Gnome Wizard",
        "Whenever you draw your second card each turn, create a 1/1 blue Illusion creature token with \"This "
        + "token gets +1/+0 for each other Illusion you control.\"\nWhenever an Illusion you control dies, you "
        + "may put a permanent card with mana value less than or equal to that creature's power from your hand "
        + "onto the battlefield.")]
    [InlineData("Jhoira, Ageless Innovator", "Legendary Creature — Human Artificer",
        "{T}: Put two ingenuity counters on Jhoira, then you may put an artifact card with mana value X or "
        + "less from your hand onto the battlefield, where X is the number of ingenuity counters on Jhoira.")]
    [InlineData("Aether Vial", "Artifact",
        "At the beginning of your upkeep, you may put a charge counter on this artifact.\n{T}: You may put a "
        + "creature card with mana value equal to the number of charge counters on this artifact from your hand "
        + "onto the battlefield.")]
    [InlineData("Emergency Powers", "Instant",
        "Each player shuffles their hand and graveyard into their library, then draws seven cards. Exile "
        + "Emergency Powers.\nAddendum — If you cast this spell during your main phase, you may put a permanent "
        + "card with mana value 7 or less from your hand onto the battlefield.")]
    public void Classify_TheNovember3Redefinition_AnyCapOnACardFromTheHand_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Read 2026-11-03 by rulings already made, both untagged by hand on 10-26:
    // suspend granted to the spell just cast and paid for from the hand is
    // Taigam's (Gandalf of the Secret Fire), and a LIST of types is a tribe (Dr.
    // Eggman's 'a Construct, Robot, or Vehicle card', which the opponent may
    // refuse as well).
    [InlineData("Gandalf of the Secret Fire", "Legendary Creature — Avatar Wizard",
        "Whenever you cast an instant or sorcery spell from your hand during an opponent's turn, exile that "
        + "card with three time counters on it instead of putting it into your graveyard as it resolves. Then "
        + "if the exiled card doesn't have suspend, it gains suspend. (At the beginning of your upkeep, remove "
        + "a time counter. When the last is removed, you may play it without paying its mana cost.)")]
    [InlineData("Dr. Eggman", "Legendary Creature — Human Scientist",
        "Flying\nAt the beginning of your end step, draw a card. Then each opponent faces a villainous choice "
        + "— That player discards a card, or you may put a Construct, Robot, or Vehicle card from your hand "
        + "onto the battlefield.")]
    public void Classify_TheNovember3Readings_ThePaidSpellAndATribeList_AreNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Pinned on 2026-11-03 as what the again-and-again reading left out: a
    // ONE-SHOT (Hidetsugu and Kairi's death trigger), a CAP (Rashmi's lesser
    // mana value, Powerbalance's same mana value, Keldon Flamesage's mana value X
    // or less) and a TRIBAL one (Descendants' Path, Leaf-Crowned Elder). The
    // redefinition the same day keeps them out for the wider reason: nothing off
    // the top is Cheat.
    [InlineData("Hidetsugu and Kairi", "Legendary Creature — Ogre Demon Dragon",
        "Flying\nWhen Hidetsugu and Kairi enters, draw three cards, then put two cards from your hand on top "
        + "of your library in any order.\nWhen Hidetsugu and Kairi dies, exile the top card of your library. "
        + "Target opponent loses life equal to its mana value. If it's an instant or sorcery card, you may cast "
        + "it without paying its mana cost.")]
    [InlineData("Rashmi, Eternities Crafter", "Legendary Creature — Elf Druid",
        "Whenever you cast your first spell each turn, reveal the top card of your library. You may cast it "
        + "without paying its mana cost if it's a spell with lesser mana value. If you don't cast it, put it "
        + "into your hand.")]
    [InlineData("Powerbalance", "Enchantment",
        "Whenever an opponent casts a spell, you may reveal the top card of your library. If you do, you may "
        + "cast that card without paying its mana cost if the two spells have the same mana value.")]
    [InlineData("Keldon Flamesage", "Creature — Human Shaman",
        "Enlist\nWhenever this creature attacks, look at the top X cards of your library, where X is this "
        + "creature's power. You may exile an instant or sorcery card with mana value X or less from among "
        + "them. Put the rest on the bottom of your library in a random order. You may cast the exiled card "
        + "without paying its mana cost.")]
    [InlineData("Descendants' Path", "Enchantment",
        "At the beginning of your upkeep, reveal the top card of your library. If it's a creature card that "
        + "shares a creature type with a creature you control, you may cast it without paying its mana cost. If "
        + "you don't cast it, put it on the bottom of your library.")]
    [InlineData("Leaf-Crowned Elder", "Creature — Treefolk Shaman",
        "Kinship — At the beginning of your upkeep, you may look at the top card of your library. If it "
        + "shares a creature type with this creature, you may reveal it. If you do, you may play that card "
        + "without paying its mana cost.")]
    public void Classify_TheNovember3CheatPass_OneOffOrCappedOrTribalFreeCard_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Out off an opponent's library too: the player who exiled it casts it
    // (Possibility Storm), and a one-shot (Stolen Goods), both off the top.
    [InlineData("Possibility Storm", "Enchantment",
        "Whenever a player casts a spell from their hand, that player exiles it, then exiles cards from the "
        + "top of their library until they exile a card that shares a card type with it. That player may cast "
        + "that card without paying its mana cost. Then they put all cards exiled with this enchantment on the "
        + "bottom of their library in a random order.")]
    [InlineData("Stolen Goods", "Sorcery",
        "Target opponent exiles cards from the top of their library until they exile a nonland card. Until "
        + "end of turn, you may cast that card without paying its mana cost.")]
    public void Classify_TheNovember3CheatPass_AFreeCardTheOpponentCastsOrOneOff_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // ONE card off the top of a library cast by an ability that runs once is no
    // Cheat, free or paid otherwise, ruled 2026-11-03 ('Mai Cheat'): Amped
    // Raptor's energy (named), Vault 112's chapter III (reached; Filter stays).
    // The redefinition the same day took every card off the top out.
    [InlineData("Amped Raptor", "Creature — Dinosaur",
        "First strike\nWhen this creature enters, you get {E}{E} (two energy counters). Then if you cast it "
        + "from your hand, exile cards from the top of your library until you exile a nonland card. You may "
        + "cast that card by paying an amount of {E} equal to its mana value rather than paying its mana cost.")]
    [InlineData("Vault 112: Sadistic Simulation", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — Tap "
        + "up to one target creature and put a stun counter on it. You get {E}{E} (two energy counters).\nIII — "
        + "Pay any amount of {E}. If you paid one or more {E} this way, shuffle your library, then exile that "
        + "many cards from the top. You may play one of those cards without paying its mana cost.")]
    public void Classify_TheNovember3CheatPass_OneCardOffTheTopOnce_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Invasion of Alara keeps Cheat through its back face, an artifact put from
    // the hand. Its front, one of two cards off the top cast free, and Xander's
    // Pact were pinned here as a pile ('Pile restano Cheat', 2026-11-03); the
    // redefinition the same day took every pile off the top out.
    [InlineData("Invasion of Alara // Awaken the Maelstrom", "Battle — Siege // Sorcery",
        "When this Siege enters, exile cards from the top of your library until you exile two nonland cards "
        + "with mana value 4 or less. You may cast one of those two cards without paying its mana cost. Put one "
        + "of them into your hand. Then put the other cards exiled this way on the bottom of your library in a "
        + "random order.\nAwaken the Maelstrom is all colors.\nTarget player draws two cards. You may put an "
        + "artifact card from your hand onto the battlefield. Create a token that's a copy of a permanent you "
        + "control. Distribute three +1/+1 counters among one, two, or three creatures you control. Destroy "
        + "target permanent an opponent controls.")]
    public void Classify_InvasionOfAlara_KeepsCheatThroughItsBackFace(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // Ruled 2026-11-05 under the 2026-11-03 Wumpus Aberration ruling: an opponent
    // putting a card from their own hand is not Cheat, also when the card says
    // that player after an exile (Divine Gambit).
    [InlineData("Divine Gambit", "Sorcery",
        "Exile target artifact, creature, or enchantment an opponent controls. That player may put a "
        + "permanent card from their hand onto the battlefield.")]
    [InlineData("Wumpus Aberration", "Creature — Eldrazi Beast",
        "Devoid (This card has no color.)\nWhen you cast this spell, if {C} wasn't spent to cast it, target "
        + "opponent may put a creature card from their hand onto the battlefield.\nTrample")]
    public void Classify_TheNovember5Ruling_ThatPlayerPutsFromTheirHandAfterAnExile_IsNotCheat(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }

    [Theory]
    // ...but a clause that carries you too stays Cheat: Braids, Conjurer Adept at
    // each player's upkeep, Hunted Wumpus.
    [InlineData("Braids, Conjurer Adept", "Legendary Creature — Human Wizard",
        "At the beginning of each player's upkeep, that player may put an artifact, creature, or land card "
        + "from their hand onto the battlefield.")]
    [InlineData("Hunted Wumpus", "Creature — Beast",
        "When this creature enters, each other player may put a creature card from their hand onto the "
        + "battlefield.")]
    public void Classify_TheNovember5Ruling_EachPlayersUpkeepStaysCheat(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Cheat));
    }
}
