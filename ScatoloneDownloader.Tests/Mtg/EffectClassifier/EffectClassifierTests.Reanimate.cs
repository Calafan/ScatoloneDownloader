using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // The same effect written PUT instead of RETURN, with the graveyard named
    // any of the ways the game names it. All six reviewed cards written this
    // way are tagged, and none of them read before 2026-09-19.
    [InlineData("Ashen Powder", "Sorcery",
        "Put target creature card from an opponent's graveyard onto the battlefield under your control.")]
    [InlineData("Chorale of the Void", "Enchantment — Aura",
        "Enchant creature you control\n"
        + "Whenever enchanted creature attacks, put target creature card from defending player's graveyard "
        + "onto the battlefield under your control tapped and attacking.\n"
        + "Void — At the beginning of your end step, sacrifice this Aura unless a nonland permanent left the "
        + "battlefield this turn or a spell was warped this turn.")]
    // A return whose card is qualified at length (mana value less than or equal to
    // its power) is read up to 90 characters, as Regrowth's is: Carmen, tagged by
    // the human on 2026-09-29, and Sandbender Scavengers.
    [InlineData("Carmen, Cruel Skymarcher", "Legendary Creature — Vampire Soldier",
        "Flying\nWhenever a player sacrifices a permanent, put a +1/+1 counter on Carmen and you gain 1 "
        + "life.\nWhenever Carmen attacks, return up to one target permanent card with mana value less than or "
        + "equal to Carmen's power from your graveyard to the battlefield.")]
    [InlineData("Sandbender Scavengers", "Creature — Human Rogue",
        "Whenever you sacrifice another permanent, put a +1/+1 counter on this creature.\nWhen this creature "
        + "dies, you may exile it. When you do, return target creature card with mana value less than or equal "
        + "to this creature's power from your graveyard to the battlefield.")]
    public void Classify_PuttingACreatureCardOntoTheBattlefield_IsReanimate(
        string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // A LAND out of the graveyard rebuilds a mana base; it reanimates nothing.
    // Follows the 2026-09-18 Ramp ruling and was read here 2026-09-19: 7
    // reviewed cards put one back and only 1 carries Reanimate.
    [InlineData("Summon: Titan", "Enchantment Creature — Saga Giant",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\n"
        + "I — Mill five cards.\n"
        + "II — Return all land cards from your graveyard to the battlefield tapped.\n"
        + "III — Until end of turn, another target creature you control gains trample and gets +X/+X, where X "
        + "is the number of lands you control.\nReach, trample")]
    [InlineData("Floral Evoker", "Creature — Snake Druid",
        "Landfall — Whenever a land you control enters, put a +1/+1 counter on this creature.\n"
        + "{G}, Discard a creature card: Return target land card from your graveyard to the battlefield tapped.")]
    public void Classify_ALandOutOfTheGraveyard_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // A card bringing ITSELF back reanimates nothing, ruled 2026-10-13 ("mai
    // Reanimate"), overturning the 09-19 rejection of this veto.
    [InlineData("Haunt of the Dead Marshes", "Creature — Nightmare Elf",
        "When this creature enters, scry 1.\n{2}{B}: Return this card from your graveyard to the battlefield "
        + "tapped. Activate only if you control a legendary creature.")]
    // An old card names itself by the name cut at the comma (ShortName), not
    // by its full front name: Akuta was Reanimate until 2026-11-01.
    [InlineData("Akuta, Born of Ash", "Legendary Creature — Spirit",
        "Haste\nAt the beginning of your upkeep, if you have more cards in hand than each opponent, you may "
        + "sacrifice a Swamp. If you do, return Akuta from your graveyard to the battlefield.")]
    public void Classify_ACardBringingItselfBack_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // The wordings of the 10-06..10-11 sitting and the older ones it brought
    // up, read 2026-10-13, every one tagged Reanimate by hand: a long list
    // before "from", "OF your graveyard", cards "that weren't put there this
    // way", the card described before its zone or a permanent card, a token
    // copy of a permanent card, the Aura on a card in a graveyard, the verb
    // first, hand-or-battlefield, the mass swap, cards chosen in a graveyard,
    // a library-and/or-graveyard search, every creature of yours that dies.
    [InlineData("Relive the Past", "Sorcery",
        "Return up to one target artifact card, up to one target land card, and up to one target non-Aura "
        + "enchantment card from your graveyard to the battlefield. They are 5/5 Elemental creatures in "
        + "addition to their other types.")]
    [InlineData("Shallow Grave", "Instant",
        "Return the top creature card of your graveyard to the battlefield. That creature gains haste until "
        + "end of turn. Exile it at the beginning of the next end step.")]
    [InlineData("Bringer of the Last Gift", "Creature — Vampire Demon",
        "Flying\nWhen this creature enters, if you cast it, each player sacrifices all other creatures they "
        + "control. Then each player returns all creature cards from their graveyard that weren't put there "
        + "this way to the battlefield.")]
    [InlineData("Coalstoke Gearhulk", "Artifact Creature — Construct",
        "Menace, deathtouch\nWhen this creature enters, put target creature card with mana value 4 or less "
        + "from a graveyard onto the battlefield under your control with a finality counter on it. That "
        + "creature gains menace, deathtouch, and haste. At the beginning of your next end step, exile that "
        + "creature.")]
    [InlineData("Staff of Eden, Vault's Key", "Legendary Artifact",
        "When Staff of Eden enters, put target legendary permanent card not named Staff of Eden, Vault's Key "
        + "from a graveyard onto the battlefield under your control.\n{T}: Draw a card for each permanent you "
        + "control but don't own.")]
    [InlineData("Bone Dancer", "Creature — Zombie",
        "Whenever this creature attacks and isn't blocked, you may put the top creature card of defending "
        + "player's graveyard onto the battlefield under your control. If you do, this creature assigns no "
        + "combat damage this turn.")]
    [InlineData("Fire Nation Salvagers", "Creature — Human Soldier",
        "Menace\nWhen this creature enters, put a +1/+1 counter on target creature or Vehicle you "
        + "control.\nWhenever one or more creatures you control with counters on them deal combat damage to a "
        + "player, put target creature or Vehicle card from that player's graveyard onto the battlefield under "
        + "your control.")]
    [InlineData("Sin, Spira's Punishment", "Legendary Creature — Leviathan Avatar",
        "Flying\nWhenever Sin enters or attacks, exile a permanent card from your graveyard at random, then "
        + "create a tapped token that's a copy of that card. If the exiled card is a land card, repeat this "
        + "process.")]
    [InlineData("Lazotep Quarry", "Land — Desert",
        "{T}: Add {C}.\n{T}, Sacrifice a creature: Add one mana of any color.\n{X}{2}, {T}, Sacrifice a "
        + "Desert: Exile target creature card with mana value X from your graveyard. Create a token that's a "
        + "copy of it, except it's a 4/4 black Zombie. Activate only as a sorcery.")]
    [InlineData("Espers to Magicite", "Instant",
        "Exile each opponent's graveyard. When you do, choose up to one target creature card exiled this "
        + "way. Create a token that's a copy of that card, except it's an artifact and it loses all other card "
        + "types.")]
    [InlineData("Animate Dead", "Enchantment — Aura",
        "Enchant creature card in a graveyard\nWhen this Aura enters, if it's on the battlefield, it loses "
        + "\"enchant creature card in a graveyard\" and gains \"enchant creature put onto the battlefield with "
        + "this Aura.\" Return enchanted creature card to the battlefield under your control and attach this "
        + "Aura to it. When this Aura leaves the battlefield, that creature's controller sacrifices "
        + "it.\nEnchanted creature gets -1/-0.")]
    [InlineData("Squirming Emergence", "Sorcery",
        "Fathomless descent — Return to the battlefield target nonland permanent card in your graveyard with "
        + "mana value less than or equal to the number of permanent cards in your graveyard.")]
    [InlineData("Myojin of Grim Betrayal", "Legendary Creature — Spirit",
        "Myojin of Grim Betrayal enters with an indestructible counter on it if you cast it from your "
        + "hand.\nRemove an indestructible counter from Myojin of Grim Betrayal: Put onto the battlefield "
        + "under your control all creature cards in all graveyards that were put there from anywhere this turn.")]
    [InlineData("Zuko's Conviction", "Instant",
        "Kicker {4} (You may pay an additional {4} as you cast this spell.)\nReturn target creature card "
        + "from your graveyard to your hand. If this spell was kicked, instead put that card onto the "
        + "battlefield tapped.")]
    [InlineData("Living Death", "Sorcery",
        "Each player exiles all creature cards from their graveyard, then sacrifices all creatures they "
        + "control, then puts all cards they exiled this way onto the battlefield.")]
    [InlineData("Afterlife from the Loam", "Sorcery",
        "Delve (Each card you exile from your graveyard while casting this spell pays for {1}.)\nFor each "
        + "player, choose up to one target creature card in that player's graveyard. Put those cards onto the "
        + "battlefield under your control. They're Zombies in addition to their other types.")]
    [InlineData("Rejoin the Fight", "Sorcery",
        "Mill three cards. Then starting with the next opponent in turn order, each opponent chooses a "
        + "creature card in your graveyard that hasn't been chosen. Return each card chosen this way to the "
        + "battlefield under your control.")]
    [InlineData("Invasion of Ikoria", "Battle — Siege",
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's "
        + "defeated, exile it, then cast it transformed.)\nWhen this Siege enters, search your library and/or "
        + "graveyard for a non-Human creature card with mana value X or less and put it onto the battlefield. "
        + "If you search your library this way, shuffle.\nReach\nFor each non-Human creature you control, you "
        + "may have that creature assign its combat damage as though it weren't blocked.")]
    [InlineData("Valkyrie's Call", "Enchantment",
        "Whenever a nontoken, non-Angel creature you control dies, return that card to the battlefield under "
        + "its owner's control with a +1/+1 counter on it. It has flying and is an Angel in addition to its "
        + "other types.")]
    public void Classify_ACreatureCardPutBackFromAGraveyard_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Not for you: a creature put back under its owner's or an opponent's
    // control (2026-10-13).
    [InlineData("Immortal Obligation", "Instant",
        "Return target creature card from an opponent's graveyard to the battlefield under their control "
        + "with a duty counter on it. For as long as that creature has a duty counter on it, it is goaded, "
        + "can't attack you or a permanent you control, and can't block creatures you control.")]
    [InlineData("Mysterious Limousine", "Artifact — Vehicle",
        "Whenever this Vehicle enters or attacks, exile up to one other target creature until this Vehicle "
        + "leaves the battlefield. If a creature is put into exile this way, return each other card exiled "
        + "with this Vehicle to the battlefield under its owner's control.\nCrew 2")]
    public void Classify_ACreaturePutBackForSomebodyElse_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // The graveyard is not the only place a creature comes back from. A card
    // THIS CARD exiled, put onto the battlefield under your control, is a
    // reanimation by another route. Ruled 2026-09-19.
    [InlineData("Ghost Vacuum", "Artifact",
        "{T}: Exile target card from a graveyard.\n"
        + "{6}, {T}, Sacrifice this artifact: Put each creature card exiled with this artifact onto the "
        + "battlefield under your control with a flying counter on it. Each of them is a 1/1 Spirit in "
        + "addition to its other types. Activate only as a sorcery.")]
    [InlineData("Purgatory", "Enchantment",
        "Whenever a nontoken creature is put into your graveyard from the battlefield, exile that card.\n"
        + "At the beginning of your upkeep, you may pay {4} and 2 life. If you do, return a card exiled with "
        + "this enchantment to the battlefield.")]
    // …and a token COPY of a creature card in a graveyard, which is not the
    // card itself but puts the same thing on the table.
    [InlineData("Cursecloth Wrappings", "Artifact",
        "Zombies you control get +1/+1.\n"
        + "{T}: Target creature card in your graveyard gains embalm until end of turn. The embalm cost is "
        + "equal to its mana cost. (Exile that card and pay its embalm cost: Create a token that's a copy of "
        + "it, except it's a white Zombie in addition to its other types and has no mana cost. Embalm only as "
        + "a sorcery.)")]
    public void Classify_ComingBackFromExileOrAsACopy_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Exiling a creature YOU CONTROL and handing it back is a blink, and a
    // strange kind of protection rather than a reanimation. Ruled 2026-09-19
    // and deliberately left untagged — it is the guard that separates these
    // two from Ghost Vacuum and Purgatory above.
    [InlineData("Cold Storage", "Artifact",
        "{3}: Exile target creature you control.\n"
        + "Sacrifice this artifact: Return each creature card exiled with this artifact to the battlefield "
        + "under your control.")]
    [InlineData("Safe Haven", "Land",
        "{2}, {T}: Exile target creature you control.\n"
        + "At the beginning of your upkeep, you may sacrifice this land. If you do, return each card exiled "
        + "with this land to the battlefield under its owner's control.")]
    public void Classify_BlinkingYourOwnCreature_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Readings of 2026-10-16: an enchanted creature that dies returned under your
    // control, a creature put into your graveyard returned, and a creature or
    // permanent cast free from your graveyard, the copy included.
    [InlineData("Necrogen Communion", "Enchantment — Aura",
        "Enchant creature you control\nEnchanted creature has toxic 2. (Players dealt combat damage by it also "
        + "get two poison counters.)\nWhen enchanted creature dies, return that card to the battlefield under "
        + "your control.")]
    [InlineData("False Demise", "Enchantment — Aura",
        "Enchant creature\nWhen enchanted creature dies, return that card to the battlefield under your "
        + "control.")]
    [InlineData("Angelic Renewal", "Enchantment",
        "Whenever a creature is put into your graveyard from the battlefield, you may sacrifice this "
        + "enchantment. If you do, return that card to the battlefield.")]
    [InlineData("Counterpoint", "Instant",
        "Counter target spell. You may cast a creature, instant, sorcery, or planeswalker spell from your "
        + "graveyard with mana value less than or equal to that spell's mana value without paying its mana "
        + "cost.")]
    [InlineData("Shiko, Paragon of the Way", "Legendary Creature — Spirit Dragon",
        "Flying, vigilance\nWhen Shiko enters, exile target nonland card with mana value 3 or less from your "
        + "graveyard. Copy it, then you may cast the copy without paying its mana cost. (A copy of a permanent "
        + "spell becomes a token.)")]
    public void Classify_TheOctober16ReanimatePass_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // A creature that returns under its OWNER's control was never yours to bring
    // back.
    [InlineData("Abduction", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, untap enchanted creature.\nYou control enchanted "
        + "creature.\nWhen enchanted creature dies, return that card to the battlefield under its owner's "
        + "control.")]
    public void Classify_TheOctober16ReanimatePass_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Swift Warkite loses Cheat but keeps Reanimate through its graveyard half.
    [InlineData("Swift Warkite", "Creature — Dragon",
        "Flying\nWhen this creature enters, you may put a creature card with mana value 3 or less from your "
        + "hand or graveyard onto the battlefield. That creature gains haste. Return it to your hand at the "
        + "beginning of the next end step.")]
    public void Classify_TheNovember3CheatPass_SwiftWarkite_KeepsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Read 2026-11-05: Savior of Ollenbock exiles a creature from the battlefield
    // or a creature card from a graveyard and puts the exiled cards onto the
    // battlefield under their owners' control when it leaves (Removal and
    // Reanimate by hand). The graveyard half is a return from the dead even though
    // it is handed to the owner (Reanimate.md, 2026-10-13, is about a card taken
    // off the battlefield).
    [InlineData("Savior of Ollenbock", "Creature — Human Soldier",
        "Training (Whenever this creature attacks with another creature with greater power, put a +1/+1 "
        + "counter on this creature.)\nWhenever this creature trains, exile up to one other target creature from "
        + "the battlefield or creature card from a graveyard.\nWhen this creature leaves the battlefield, put "
        + "the exiled cards onto the battlefield under their owners' control.")]
    public void Classify_TheNovember5Readings_AGraveyardCardExiledThenReturned_IsReanimate(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Reanimate));
        Assert.True(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Read 2026-11-06, each Reanimate by hand: several creature cards chosen in a
    // graveyard and returned 'each of them' or 'two of them at random' on the next
    // sentence (Sinister Waltz, The War in Heaven), ALL the cards of a kind
    // returned with the verb first (Gerrard, Weatherlight Hero), and a permanent
    // card put from among the cards exiled from the graveyard (Winter, Cynical
    // Opportunist).
    [InlineData("Sinister Waltz", "Sorcery",
        "Choose three target creature cards in your graveyard. Return two of them at random to the "
        + "battlefield and put the other on the bottom of your library.")]
    [InlineData("The War in Heaven", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — You "
        + "draw three cards and you lose 3 life.\nII — Mill three cards.\nIII — Choose up to three target "
        + "creature cards with total mana value 8 or less in your graveyard. Return each of them to the "
        + "battlefield with a necrodermis counter on it. They're artifacts in addition to their other types.")]
    [InlineData("Gerrard, Weatherlight Hero", "Legendary Creature — Human Soldier",
        "First strike\nWhen Gerrard dies, exile it and return to the battlefield all artifact and creature "
        + "cards in your graveyard that were put there from the battlefield this turn.")]
    [InlineData("Winter, Cynical Opportunist", "Legendary Creature — Human Warlock",
        "Deathtouch\nWhenever Winter attacks, mill three cards.\nDelirium — At the beginning of your end step, "
        + "you may exile any number of cards from your graveyard with four or more card types among them. If "
        + "you do, put a permanent card from among them onto the battlefield with a finality counter on it.")]
    public void Classify_TheNovember6Readings_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Read 2026-11-06, each untagged for Reanimate by hand: a token that is a copy
    // of THIS creature (Uchuulon) or of a creature token you control (Selesnya
    // Eulogist's populate reminder) brings nobody back, and cards exiled with a
    // Vehicle returned 'tapped under their owners' control' are handed back to
    // their owners (Mechtitan Core, as Mysterious Limousine).
    [InlineData("Uchuulon", "Creature — Crab Ooze Horror",
        "Uchuulon's power is equal to the number of Crabs, Oozes, and/or Horrors you control.\nHorrific "
        + "Symbiosis — At the beginning of your end step, exile up to one target creature card from an "
        + "opponent's graveyard. If you do, create a token that's a copy of this creature.")]
    [InlineData("Selesnya Eulogist", "Creature — Centaur Druid",
        "{2}{G}: Exile target creature card from a graveyard, then populate. (Create a token that's a copy of "
        + "a creature token you control.)")]
    [InlineData("Mechtitan Core", "Artifact — Vehicle",
        "{5}, Exile this Vehicle and four other artifact creatures and/or Vehicles you control: Create "
        + "Mechtitan, a legendary 10/10 Construct artifact creature token with flying, vigilance, trample, "
        + "lifelink, and haste that's all colors. When that token leaves the battlefield, return all cards "
        + "exiled with this Vehicle except this card to the battlefield tapped under their owners' "
        + "control.\nCrew 2")]
    public void Classify_TheNovember6Readings_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // RULED 2026-11-06 (No: dal grimorio non è Reanimate), refining 09-19: the
    // permanent cards exiled with the card are put onto the battlefield, but the
    // card only exiled from the top of a library, so nothing came back from the
    // dead (Journey to the Lost City).
    [InlineData("Journey to the Lost City", "Enchantment",
        "At the beginning of your upkeep, exile the top four cards of your library, then roll a d20.\n1—9 | "
        + "You may put a land card from among those cards onto the battlefield.\n10—19 | Create a 2/2 green Wolf "
        + "creature token, then put a +1/+1 counter on it for each creature card among those cards.\n20 | Put "
        + "all permanent cards exiled with this enchantment onto the battlefield, then sacrifice it.")]
    public void Classify_TheNovember6Ruling_ExilingOnlyFromTheTopOfALibrary_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // A card that exiles from a graveyard as well as from the top of a library
    // keeps the 09-19 route (Yggdrasil, Rebirth Engine).
    [InlineData("Yggdrasil, Rebirth Engine", "Legendary Artifact",
        "When Yggdrasil enters, exile all creature cards from your graveyard.\n{T}: Exile the top three cards "
        + "of your library.\n{4}, {T}: Put a creature card exiled with Yggdrasil onto the battlefield under your "
        + "control. It gains haste until end of turn. Activate only as a sorcery.")]
    public void Classify_TheNovember6Ruling_ExilingFromAGraveyardAsWell_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // The definition of 2026-11-06: another NONLAND PERMANENT card back from the
    // graveyard onto the battlefield under your control. RULED that day: an Aura
    // or Equipment card from your hand or graveyard (Danitha, Vault 101: Birthday
    // Party, Liberated Livestock) and a Saga card, no tribe (Rydia). Read under
    // it: an artifact card from an opponent's graveyard (Grasping Tentacles), and
    // a token copy of a creature-or-Equipment card (Nahiri, the Unforgiving) or of
    // an artifact card (Leonardo da Vinci, Osgir, the Reconstructor).
    [InlineData("Danitha, Benalia's Hope", "Legendary Creature — Human Knight",
        "First strike, vigilance, lifelink\nWhen Danitha enters, you may put an Aura or Equipment card from "
        + "your hand or graveyard onto the battlefield attached to Danitha.")]
    [InlineData("Vault 101: Birthday Party", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Create "
        + "a 1/1 white Human Soldier creature token and a Food token. (A Food token is an artifact with \"{2}, "
        + "{T}, Sacrifice this token: You gain 3 life.\")\nII, III — You may put an Aura or Equipment card from "
        + "your hand or graveyard onto the battlefield. If an Equipment is put onto the battlefield this way, "
        + "you may attach it to a creature you control.")]
    [InlineData("Liberated Livestock", "Creature — Cat Bird Ox",
        "When this creature dies, create a 1/1 white Cat creature token with lifelink, a 1/1 white Bird "
        + "creature token with flying, and a 2/4 white Ox creature token. For each of those tokens, you may put "
        + "an Aura card from your hand and/or graveyard onto the battlefield attached to it.")]
    [InlineData("Rydia, Summoner of Mist", "Legendary Creature — Human Shaman",
        "Landfall — Whenever a land you control enters, you may discard a card. If you do, draw a "
        + "card.\nSummon — {X}, {T}: Return target Saga card with mana value X from your graveyard to the "
        + "battlefield with a finality counter on it. It gains haste until end of turn. Activate only as a "
        + "sorcery.")]
    [InlineData("Grasping Tentacles", "Sorcery",
        "Target opponent mills eight cards. You may put an artifact card from that player's graveyard onto "
        + "the battlefield under your control.")]
    [InlineData("Nahiri, the Unforgiving", "Legendary Planeswalker — Nahiri",
        "Compleated ({R/W/P} can be paid with {R}, {W}, or 2 life. If life was paid, this planeswalker enters "
        + "with two fewer loyalty counters.)\n+1: Until your next turn, up to one target creature attacks a "
        + "player each combat if able.\n+1: Discard a card, then draw a card.\n0: Exile target creature or "
        + "Equipment card with mana value less than Nahiri's loyalty from your graveyard. Create a token that's "
        + "a copy of it. That token gains haste. Exile it at the beginning of the next end step.")]
    [InlineData("Leonardo da Vinci", "Legendary Creature — Human Artificer",
        "{3}{U}{U}: Until end of turn, Thopters you control have base power and toughness X/X, where X is the "
        + "number of cards in your hand.\n{2}{U}, {T}: Draw a card, then discard a card. If the discarded card "
        + "was an artifact card, exile it from your graveyard. If you do, create a token that's a copy of it, "
        + "except it's a 0/2 Thopter artifact creature with flying in addition to its other types.")]
    [InlineData("Osgir, the Reconstructor", "Legendary Creature — Giant Artificer",
        "Vigilance\n{1}, Sacrifice an artifact: Target creature you control gets +2/+0 until end of turn.\n{X}, "
        + "{T}, Exile an artifact card with mana value X from your graveyard: Create two tokens that are copies "
        + "of the exiled card. Activate only as a sorcery.")]
    public void Classify_TheNovember6Definition_ANonlandPermanentCard_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // RULED 2026-11-06 (Reanimate al posto di Regrowth), overturning 10-30 for
    // every permanent card: a token copy of an enchantment card exiled from your
    // graveyard is a nonland permanent back from the dead, as Tamiyo, Compleated
    // Sage's nonland permanent card copy is.
    [InlineData("Anikthea, Hand of Erebos", "Legendary Enchantment Creature — Demigod",
        "Menace\nOther enchantment creatures you control have menace.\nWhenever Anikthea enters or attacks, "
        + "exile up to one target non-Aura enchantment card from your graveyard. Create a token that's a copy "
        + "of that card, except it's a 3/3 black Zombie creature in addition to its other types.")]
    public void Classify_TheNovember6Ruling_AnEnchantmentCardCopied_IsReanimateNotRegrowth(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Reanimate));
        Assert.False(result.HasFlag(CardEffect.Regrowth));
    }

    [Theory]
    // Read 2026-11-06 under the definition, each Reanimate by hand: a real
    // alternative cost is a cost skipped (Anrakyr the Traveller pays life rather
    // than its mana cost), and a spell cast free from among the cards exiled from
    // an opponent's graveyard (Crabomination).
    [InlineData("Anrakyr the Traveller", "Legendary Artifact Creature — Necron",
        "Lord of the Pyrrhian Legions — Whenever Anrakyr the Traveller attacks, you may cast an artifact "
        + "spell from your hand or graveyard by paying life equal to its mana value rather than paying its mana "
        + "cost.")]
    [InlineData("Crabomination", "Creature — Crab Demon",
        "Emerge from artifact {5}{B}{B} (You may cast this spell by sacrificing an artifact and paying the "
        + "emerge cost reduced by that artifact's mana value.)\nWhen this creature enters, target opponent "
        + "exiles the top card of their library, a card at random from their graveyard, and a card at random "
        + "from their hand. You may cast a spell from among cards exiled this way without paying its mana cost.")]
    public void Classify_TheNovember6Definition_ACostSkipped_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // The 09-19 route under the definition of 2026-11-06: the card exiled the
    // dead, a creature exiled instead of dying (The Darkness Crystal) or as it is
    // sacrificed (Prowling Geistcatcher).
    [InlineData("The Darkness Crystal", "Legendary Artifact",
        "Black spells you cast cost {1} less to cast.\nIf a nontoken creature an opponent controls would die, "
        + "instead exile it and you gain 2 life.\n{4}{B}{B}, {T}: Put target creature card exiled with The "
        + "Darkness Crystal onto the battlefield tapped under your control with two additional +1/+1 counters "
        + "on it.")]
    [InlineData("Prowling Geistcatcher", "Creature — Human Rogue",
        "Whenever you sacrifice another creature, exile it. If that creature was a token, put a +1/+1 counter "
        + "on this creature.\nWhen this creature leaves the battlefield, return each card exiled with it to the "
        + "battlefield under your control.")]
    // …and a whole graveyard exiled, then a creature card exiled with the card put
    // onto the battlefield (Death in Heaven, Nautiloid Ship).
    [InlineData("Death in Heaven", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Target player mills two cards, then exiles their graveyard.\nIII — Put all creature cards exiled with "
        + "this enchantment onto the battlefield face down under your control. They're 2/2 Cyberman artifact "
        + "creatures.")]
    [InlineData("Nautiloid Ship", "Artifact — Vehicle",
        "Flying\nWhen this Vehicle enters, exile target player's graveyard.\nWhenever this Vehicle deals combat "
        + "damage to a player, you may put a creature card exiled with this Vehicle onto the battlefield under "
        + "your control.\nCrew 3")]
    public void Classify_TheNovember6Definition_AnExileOfTheDead_IsReanimate(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // RULED 2026-11-06 on Sothera, the Supervoid (-Reanimate, +Steal): a creature
    // exiled off an opponent's battlefield and put onto the battlefield under your
    // control came back from nowhere dead, and it is a theft. Profane Procession
    // and Sisters of Stone Death read the same.
    [InlineData("Sothera, the Supervoid", "Legendary Enchantment",
        "Whenever a creature you control dies, each opponent chooses a creature they control and exiles "
        + "it.\nAt the beginning of your end step, if a player controls no creatures, sacrifice Sothera, then "
        + "put a creature card exiled with it onto the battlefield under your control with two additional +1/+1 "
        + "counters on it.")]
    [InlineData("Profane Procession // Tomb of the Dusk Rose", "Legendary Enchantment // Legendary Land",
        "{3}{W}{B}: Exile target creature. Then if there are three or more cards exiled with Profane "
        + "Procession, transform it.\n(Transforms from Profane Procession.)\n{T}: Add one mana of any "
        + "color.\n{2}{W}{B}, {T}: Put a creature card exiled with this permanent onto the battlefield under "
        + "your control.")]
    [InlineData("Sisters of Stone Death", "Legendary Creature — Gorgon",
        "{G}: Target creature blocks Sisters of Stone Death this turn if able.\n{B}{G}: Exile target creature "
        + "blocking or blocked by Sisters of Stone Death.\n{2}{B}: Put a creature card exiled with Sisters of "
        + "Stone Death onto the battlefield under your control.")]
    public void Classify_TheNovember6Ruling_ACreatureExiledOffTheirBattlefield_IsStealNotReanimate(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Steal));
        Assert.False(result.HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // Not the definition of 2026-11-06: your own permanent exiled and returned
    // (Synod Sanctum, Northampton Farm), a card casting ITSELF for an alternative
    // cost (Scourge of Nel Toth), and a creature that BECOMES a copy of a card
    // exiled from a graveyard, since nothing enters (Mimeoplasm, Revered One,
    // RULED that day, as Lazav, Wearer of Faces was untagged).
    [InlineData("Synod Sanctum", "Artifact",
        "{2}, {T}: Exile target permanent you control.\n{2}, Sacrifice this artifact: Return all cards exiled "
        + "with this artifact to the battlefield under your control.")]
    [InlineData("Northampton Farm", "Land",
        "{T}: Add {C}.\n{1}, {T}: Exile target creature you own.\n{2}, {T}, Sacrifice this land: Return a "
        + "creature card exiled with this land to the battlefield under your control. Return each other card "
        + "exiled with this land to its owner's hand.")]
    [InlineData("Scourge of Nel Toth", "Creature — Zombie Dragon",
        "Flying\nYou may cast this creature from your graveyard by paying {B}{B} and sacrificing two creatures "
        + "rather than paying its mana cost.")]
    [InlineData("Mimeoplasm, Revered One", "Legendary Creature — Ooze",
        "As Mimeoplasm enters, exile up to X creature cards from your graveyard. It enters with three +1/+1 "
        + "counters on it for each creature card exiled this way.\n{2}: Mimeoplasm becomes a copy of target "
        + "creature card exiled with it, except it's 0/0 and has this ability.")]
    public void Classify_TheNovember6Definition_NothingDeadComesBack_IsNotReanimate(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Reanimate));
    }

    [Theory]
    // The Sothera ruling of 2026-11-06 takes a creature off somebody else's
    // battlefield; exiling your own permanent and putting it back under your
    // control steals nothing (Cold Storage, Synod Sanctum).
    [InlineData("Cold Storage", "Artifact",
        "{3}: Exile target creature you control.\nSacrifice this artifact: Return each creature card exiled "
        + "with this artifact to the battlefield under your control.")]
    [InlineData("Synod Sanctum", "Artifact",
        "{2}, {T}: Exile target permanent you control.\n{2}, Sacrifice this artifact: Return all cards exiled "
        + "with this artifact to the battlefield under your control.")]
    public void Classify_TheNovember6Ruling_YourOwnPermanentExiledAndReturned_IsNotSteal(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Steal));
    }
}
