using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // Sacrifice means an OUTLET for your own creatures — the half of the combo
    // that makes a stolen creature worth taking. An activation cost in front of a
    // colon is the canonical shape; a trigger that OFFERS the sacrifice counts too.
    [InlineData("Ashnod's Altar", "Artifact", "Sacrifice a creature: Add {C}{C}.")]
    [InlineData("Goblin Bombardment", "Enchantment", "Sacrifice a creature: This enchantment deals 1 damage to any target.")]
    [InlineData("Comet Crawler", "Creature — Beast",
        "Lifelink\nWhenever this creature attacks, you may sacrifice another creature or artifact. If you do, this creature gets +2/+0 until end of turn.")]
    // An artifact outlet, or one that names the fuel as a permanent, is the same
    // engine with something else going in.
    [InlineData("Atog", "Creature — Atog", "Sacrifice an artifact: This creature gets +2/+2 until end of turn.")]
    [InlineData("Infernal Tribute", "Enchantment", "{2}, Sacrifice a nontoken permanent: Draw a card.")]
    [InlineData("Dwarven Weaponsmith", "Creature — Dwarf",
        "{T}, Sacrifice an artifact: Put a +1/+1 counter on target creature. Activate only during your upkeep.")]
    // Ruled 2026-09-25: only a REPEATABLE outlet, fed creatures or artifacts.
    // An artifact outlet the human had left untagged…
    [InlineData("Orcish Mechanics", "Creature — Orc",
        "{T}, Sacrifice an artifact: This creature deals 2 damage to any target.")]
    // …a sacrifice every upkeep, forced or usable only then…
    [InlineData("Lord of the Pit", "Creature — Demon",
        "Flying, trample\nAt the beginning of your upkeep, sacrifice a creature other than this creature. If "
        + "you can't, this creature deals 7 damage to you.")]
    [InlineData("Marjhan", "Creature — Serpent",
        "This creature doesn't untap during your untap step.\n{U}{U}, Sacrifice a creature: Untap this "
        + "creature. Activate only during your upkeep.\nThis creature can't attack unless defending player "
        + "controls an Island.\n{U}{U}: This creature gets -1/-0 until end of turn and deals 1 damage to target "
        + "attacking creature without flying.\nWhen you control no Islands, sacrifice this creature.")]
    // …a card cast again for it, by buyback or from the graveyard…
    [InlineData("Worthy Cause", "Instant",
        "Buyback {2} (You may pay an additional {2} as you cast this spell. If you do, put this card into "
        + "your hand as it resolves.)\nAs an additional cost to cast this spell, sacrifice a creature.\nYou gain "
        + "life equal to the sacrificed creature's toughness.")]
    [InlineData("Wickerfolk Indomitable", "Artifact Creature — Scarecrow",
        "You may cast this card from your graveyard by paying 2 life and sacrificing an artifact or creature "
        + "in addition to paying its other costs.")]
    // …an equip cost, "any number" on every attack, a keyword handed on, a
    // loyalty ability, and an activated ability whose EFFECT sacrifices…
    [InlineData("Dissection Tools", "Artifact — Equipment",
        "When this Equipment enters, manifest dread, then attach this Equipment to that creature.\nEquipped "
        + "creature gets +2/+2 and has deathtouch and lifelink.\nEquip—Sacrifice a creature.")]
    [InlineData("Kylox, Visionary Inventor", "Legendary Creature — Lizard Artificer",
        "Menace, ward {2}, haste\nWhenever Kylox attacks, sacrifice any number of other creatures, then exile "
        + "the top X cards of your library, where X is their total power. You may cast any number of instant "
        + "and/or sorcery spells from among the exiled cards without paying their mana costs.")]
    [InlineData("Colonel Autumn", "Legendary Creature — Human Soldier",
        "Lifelink\nExploit (When this creature enters, you may sacrifice a creature.)\nOther legendary "
        + "creatures you control have exploit.\nWhenever a creature you control exploits a creature, put a +1/+1 "
        + "counter on each creature you control.")]
    [InlineData("Chandra, Spark Hunter", "Legendary Planeswalker — Chandra",
        "At the beginning of combat on your turn, choose up to one target Vehicle you control. Until end of "
        + "turn, it becomes an artifact creature and gains haste.\n+2: You may sacrifice an artifact or discard "
        + "a card. If you do, draw a card.\n0: Create a 3/2 colorless Vehicle artifact token with crew 1.\n−7: "
        + "You get an emblem with \"Whenever an artifact you control enters, this emblem deals 3 damage to any "
        + "target.\"")]
    [InlineData("Joo Dee, One of Many", "Creature — Human Advisor",
        "{B}, {T}: Surveil 1. Create a token that's a copy of this creature, then sacrifice an artifact or "
        + "creature. Activate only as a sorcery. (To surveil 1, look at the top card of your library. You may "
        + "put it into your graveyard.)")]
    // "One or more" is a count like "two" — the base pattern once missed it.
    [InlineData("Radiant Lotus", "Artifact",
        "{T}, Sacrifice one or more artifacts: Choose a color. Target player adds three mana of the chosen "
        + "color for each artifact sacrificed this way. (Activate only as an instant.)")]
    // …and destroying or exiling your own at will, an outlet by another verb.
    [InlineData("Despotic Scepter", "Artifact",
        "{T}: Destroy target permanent you own. It can't be regenerated.")]
    [InlineData("Rats of Rath", "Creature — Rat",
        "{B}: Destroy target artifact, creature, or land you control.")]
    [InlineData("City of Shadows", "Land",
        "{T}, Exile a creature you control: Put a storage counter on this land.\n{T}: Add {C} for each storage "
        + "counter on this land.")]
    public void Classify_SacrificeOutlet_IsSacrifice(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
    }

    [Theory]
    // Read 2026-11-06, both Sacrifice by hand: 'Sacrifice UP TO THREE permanents:'
    // is a cost in front of a colon whatever the number (Baba Lysaga, Night
    // Witch), and an opponent's optional reply to YOUR sacrifice, 'if you do, each
    // opponent may sacrifice a permanent', is no edict (Braids, Arisen Nightmare).
    [InlineData("Baba Lysaga, Night Witch", "Legendary Creature — Human Warlock",
        "{T}, Sacrifice up to three permanents: If there were three or more card types among the sacrificed "
        + "permanents, each opponent loses 3 life, you gain 3 life, and you draw three cards.")]
    [InlineData("Braids, Arisen Nightmare", "Legendary Creature — Nightmare",
        "At the beginning of your end step, you may sacrifice an artifact, creature, enchantment, land, or "
        + "planeswalker. If you do, each opponent may sacrifice a permanent of their choice that shares a card "
        + "type with it. For each opponent who doesn't, that player loses 2 life and you draw a card.")]
    public void Classify_TheNovember6Readings_IsSacrifice(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
    }

    [Theory]
    // Three things that are not an outlet: an edict empties somebody ELSE's
    // board, an additional cost to cast is paid once on the way to a different
    // effect, and a land is not a creature.
    [InlineData("Diabolic Edict", "Instant", "Target player sacrifices a creature of their choice.")]
    [InlineData("Natural Order", "Sorcery",
        "As an additional cost to cast this spell, sacrifice a green creature.\nSearch your library for a green creature card, put it onto the battlefield, then shuffle.")]
    [InlineData("Harrow", "Instant",
        "As an additional cost to cast this spell, sacrifice a land.\nSearch your library for up to two basic land cards, put them onto the battlefield, then shuffle.")]
    // Ruled 2026-09-25: a sacrifice made ONCE is not an outlet, however it is
    // worded — entering, "rather than pay", kicker, exploit, a spell, "any
    // number" at a time.
    [InlineData("Boilerbilges Ripper", "Creature — Human Assassin",
        "When this creature enters, you may sacrifice another creature or enchantment. When you do, this "
        + "creature deals 2 damage to any target.")]
    [InlineData("Flare of Denial", "Instant",
        "You may sacrifice a nontoken blue creature rather than pay this spell's mana cost.\nCounter target "
        + "spell.")]
    [InlineData("Vayne's Treachery", "Instant",
        "Kicker—Sacrifice an artifact or creature. (You may sacrifice an artifact or creature in addition to "
        + "any other costs as you cast this spell.)\nTarget creature gets -2/-2 until end of turn. If this spell "
        + "was kicked, that creature gets -6/-6 until end of turn instead.")]
    [InlineData("Infernal Captor", "Creature — Devil Rogue",
        "Exploit (When this creature enters, you may sacrifice a creature.)\nWhen this creature exploits a "
        + "creature, gain control of target artifact or creature until end of turn. Untap that permanent. It "
        + "gains haste until end of turn.")]
    [InlineData("Tip the Scales", "Sorcery",
        "Sacrifice a creature. When you do, all creatures get -X/-X until end of turn, where X is the "
        + "sacrificed creature's toughness.")]
    [InlineData("Angelic Aberration", "Creature — Eldrazi Angel",
        "Devoid (This card has no color.)\nFlying, vigilance\nWhen this creature enters, sacrifice any number "
        + "of creatures each with base power or toughness 1 or less. Create that many 4/4 colorless Eldrazi "
        + "Angel creature tokens with flying and vigilance.")]
    // Emerge's reminder reads "cast this spell by sacrificing a creature", the
    // shape Wickerfolk Indomitable repeats — but it is paid once, as it is cast.
    [InlineData("Wretched Gryff", "Creature — Eldrazi Hippogriff",
        "Emerge {5}{U} (You may cast this spell by sacrificing a creature and paying the emerge cost reduced "
        + "by that creature's mana value.)\nWhen you cast this spell, draw a card.\nFlying")]
    // Not a LAND (Zuran Orb, corrected by name), not an artifact TOKEN, which
    // cashes in the card's own Clues, and not an edict inside a trigger.
    [InlineData("Zuran Orb", "Artifact",
        "Sacrifice a land: You gain 2 life.")]
    [InlineData("Sophia, Dogged Detective", "Legendary Creature — Human Detective",
        "When Sophia enters, create Tiny, a legendary 2/2 green Dog Detective creature token with "
        + "trample.\n{1}, Sacrifice an artifact token: Put a +1/+1 counter on each Dog you control.\nWhenever a "
        + "Dog you control deals combat damage to a player, create a Food token, then investigate.")]
    [InlineData("Grave Pact", "Enchantment",
        "Whenever a creature you control dies, each other player sacrifices a creature of their choice.")]
    // A trigger the OPPONENT fires is a price, not an outlet: ruled 2026-09-25,
    // "un effetto extra non è a comando".
    [InlineData("Oath of Lim-Dûl", "Enchantment",
        "Whenever you lose life, for each 1 life you lost, sacrifice a permanent other than this enchantment "
        + "unless you discard a card. (Damage dealt to you causes you to lose life.)\n{B}{B}: Draw a card.")]
    // Damage is the same price as lost life (Phyrexian Negator, named by the
    // human the same day). Both are held out today because "sacrifice THAT
    // MANY" is not read at all; teach the base pattern that count and the
    // trigger guard in SacrificeOutlet has to learn "is dealt damage" too.
    [InlineData("Phyrexian Negator", "Creature — Phyrexian Horror",
        "Trample\nWhenever this creature is dealt damage, sacrifice that many permanents.")]
    [InlineData("Lich", "Enchantment",
        "As this enchantment enters, you lose life equal to your life total.\nYou don't lose the game for "
        + "having 0 or less life.\nIf you would gain life, draw that many cards instead.\nWhenever you're dealt "
        + "damage, sacrifice that many nontoken permanents. If you can't, you lose the game.\nWhen this "
        + "enchantment is put into a graveyard from the battlefield, you lose the game.")]
    // Destroying an Aura on your creature saves it, and a harness is paid once.
    [InlineData("Miracle Worker", "Creature — Human Cleric",
        "{T}: Destroy target Aura attached to a creature you control.")]
    [InlineData("The Soul Stone", "Legendary Artifact — Infinity Stone",
        "Indestructible\n{T}: Add {B}.\n{6}{B}, {T}, Exile a creature you control: Harness The Soul Stone. "
        + "(Once harnessed, its ∞ ability is active.)\n∞ — At the beginning of your upkeep, return target "
        + "creature card from your graveyard to the battlefield.")]
    public void Classify_SacrificeThatIsNotAnOutlet_IsNotSacrifice(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Sacrifice));
    }
}
