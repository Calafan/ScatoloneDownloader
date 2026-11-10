using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // ONE creature each is an edict, ruled Removal on 2026-09-27 ("un singolo
    // sacrifice lo mettiamo removal"); the hand tags had split three to two.
    // OVERTURNS Classify_ASymmetricEdict_IsNotRemoval, which pinned Abyssal
    // Gatekeeper as NOT Removal on the reading that the edict costs you one too;
    // it was removed the same day and Abyssal Gatekeeper is pinned here instead.
    [InlineData("Abyssal Gatekeeper", "Creature — Horror",
        "When this creature dies, each player sacrifices a creature of their choice.")]
    [InlineData("Accursed Marauder", "Creature — Zombie Warrior",
        "When this creature enters, each player sacrifices a nontoken creature of their choice.")]
    [InlineData("Tariff", "Sorcery",
        "Each player sacrifices the creature they control with the greatest mana value unless they pay that "
        + "creature's mana cost. If two or more creatures a player controls are tied for greatest, that player "
        + "chooses one.")]
    public void Classify_EachPlayerSacrificesOne_IsRemovalNotWipe(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Wipe));
    }

    [Fact]
    public void Classify_AnEdictThatExiles_IsRemovalNotWipe()
    {
        // Ruled 2026-09-28: Sothera's edict comes back every time one of yours
        // dies, but it only ever lands on the opponents, so it is Removal.
        CardEffect result = EffectClassifier.Classify(MakeCard("Sothera, the Supervoid", "Legendary Enchantment",
            "Whenever a creature you control dies, each opponent chooses a creature they control and exiles "
            + "it.\nAt the beginning of your end step, if a player controls no creatures, sacrifice Sothera, then "
            + "put a creature card exiled with it onto the battlefield under your control with two additional +1/+1 "
            + "counters on it."));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Wipe));
    }

    [Theory]
    // A shrink is an answer — a creature at zero toughness is as dead as a
    // destroyed one, in both vocabularies. Ruled 2026-09-15.
    [InlineData("Locust Spray", "Instant",
        "Target creature gets -1/-1 until end of turn.\nCycling {B} ({B}, Discard this card: Draw a card.)")]
    [InlineData("Grandmother Sengir", "Legendary Creature — Human Wizard",
        "{1}{B}, {T}: Target creature gets -1/-1 until end of turn.")]
    [InlineData("Fevered Convulsions", "Enchantment",
        "{2}{B}{B}: Put a -1/-1 counter on target creature.")]
    public void Classify_AShrink_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The two edges of that reading. Taking the power off leaves the creature
    // standing, which is Pacify's job; and a shrink aimed at everybody is a Wipe.
    [InlineData("Pradesh Gypsies", "Creature — Human Nomad",
        "{1}{G}, {T}: Target creature gets -2/-0 until end of turn.")]
    [InlineData("Dread of Night", "Enchantment", "White creatures get -1/-1.")]
    public void Classify_AShrinkThatKillsNothingOrKillsEverything_IsNotRemoval(
        string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The Removal rules could not cross a comma, so every "destroy target
    // artifact, creature, or land" went unread; nor could they cross the filler
    // in front of "target", so every "exile up to one target creature" O-ring
    // did too. Between them that was 49 of 189 misses.
    [InlineData("Aftershock", "Sorcery", "Destroy target artifact, creature, or land. Aftershock deals 3 damage to you.")]
    [InlineData("Shattered Wings", "Instant", "Destroy target artifact, enchantment, or creature with flying. Surveil 1.")]
    [InlineData("All-Fates Stalker", "Creature — Assassin",
        "When this creature enters, exile up to one target non-Assassin creature until this creature leaves the battlefield.")]
    // Damage sized by a count kills exactly as damage sized by a digit does.
    [InlineData("Cat-Gator", "Creature — Cat Crocodile",
        "Lifelink\nWhen this creature enters, it deals damage equal to the number of Swamps you control to any target.")]
    public void Classify_KillsTheRuleCouldNotRead_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The damage rules could not read a QUALIFIED target: the adjective sits
    // between "target" and "creature", where nothing was allowed to.
    [InlineData("D'Avenant Archer", "Creature — Human Soldier Archer",
        "{T}: This creature deals 1 damage to target attacking or blocking creature.")]
    [InlineData("Femeref Archers", "Creature — Human Archer",
        "{T}: This creature deals 4 damage to target attacking creature with flying.")]
    [InlineData("Cloud of Darkness", "Creature — Elemental",
        "Flying\nParticle Beam — When Cloud of Darkness enters, target creature an opponent controls gets -X/-X until end of turn.")]
    // And the amount is allowed to come after the target.
    [InlineData("Divine Retribution", "Instant",
        "Divine Retribution deals damage to target attacking creature equal to the number of creatures defending player controls.")]
    // A fireball split between several things still kills one of them.
    [InlineData("Fiery Justice", "Sorcery",
        "Fiery Justice deals 5 damage divided as you choose among any number of targets. Target opponent gains 5 life.")]
    [InlineData("Dwarven Catapult", "Sorcery",
        "Dwarven Catapult deals X damage divided evenly, rounded down, among all creatures your opponents control.")]
    // An edict aimed at them, in a wording the single shipped pattern missed.
    [InlineData("Cornered by Black Mages", "Sorcery",
        "Target opponent sacrifices a creature of their choice.\nCreate a 0/1 black Wizard creature token.")]
    public void Classify_KillsTheDamageRulesCouldNotRead_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // NONCREATURE ends in "creature", so these read as kills. The same bug the
    // tap/untap pair had, found the same way.
    [InlineData("Gorilla Shaman", "Creature — Ape",
        "{X}{X}{1}: Destroy target noncreature artifact with mana value X.")]
    [InlineData("Joven", "Legendary Creature — Human Rogue",
        "{R}{R}{R}, {T}: Destroy target noncreature artifact.")]
    // And an answer that can only ever touch what is already blocking the card
    // is a combat trick, not removal — the reading Wipe already had.
    [InlineData("Knight of Dusk", "Creature — Human Knight",
        "{B}{B}: Destroy target creature blocking this creature.")]
    [InlineData("Flowstone Salamander", "Creature — Elemental Lizard",
        "{R}: This creature deals 1 damage to target creature blocking it.")]
    public void Classify_NoncreatureAndCombatTricks_AreNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Two families the wider reading has to keep out. A blink returns the card,
    // so it answers nothing; a card already in a graveyard is already dead.
    [InlineData("Explosive Getaway", "Instant",
        "Exile up to one target artifact or creature. Return it to the battlefield under its owner's control at the beginning of the next end step.")]
    [InlineData("Nyla, Shirshu Sleuth", "Legendary Creature — Elf Detective",
        "When Nyla enters, exile up to one target creature card from your graveyard.")]
    public void Classify_BlinkAndGraveyardHate_AreNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Targeted damage answers a threat, so it is Removal as well as Burn. The
    // scaling ones matter most: "deals X damage" is how the whole pre-modern
    // library writes a kill spell.
    [InlineData("Blaze", "Sorcery", "Blaze deals X damage to any target.")]
    [InlineData("Broadside Barrage", "Instant", "Broadside Barrage deals 5 damage to target creature or planeswalker. Draw a card, then discard a card.")]
    [InlineData("Char", "Instant", "Char deals 4 damage to any target and 2 damage to you.")]
    // Fight is the same act with the damage delegated; an edict never says "target".
    [InlineData("Prey Upon", "Sorcery", "Target creature you control fights target creature you don't control.")]
    [InlineData("Diabolic Edict", "Instant", "Target player sacrifices a creature.")]
    // An edict aimed at DEFENDING PLAYER on every attack, tagged Removal by the
    // human on 2026-09-29.
    [InlineData("Witch-king, Bringer of Ruin", "Legendary Creature — Wraith Noble",
        "Flying\nWhenever Witch-king attacks, defending player sacrifices a creature with the least power "
        + "among creatures they control.")]
    public void Classify_DamageAndItsCousins_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Damage that can only ever hit a creature answers a threat, and answering a
    // threat is Removal. Reading it as Burn too was worth 129 false positives.
    // A variable amount changes nothing: measured, "deals X damage to target
    // creature" is hand-tagged Burn on none of the six reviewed cards that say it.
    [InlineData("Explosive Shot", "Instant", "Explosive Shot deals 4 damage to target creature.")]
    [InlineData("Thunder Salvo", "Instant",
        "Thunder Salvo deals X damage to target creature, where X is 2 plus the number of other spells you've cast this turn.")]
    // A player named only as the controller of the creature hit is no face: the
    // human took Burn off Lothlórien Blade on 2026-09-29.
    [InlineData("Lothlórien Blade", "Artifact — Equipment",
        "Whenever equipped creature attacks, it deals damage equal to its power to target creature defending "
        + "player controls.\nEquip Elf {2}\nEquip {5}")]
    public void Classify_DamageAtACreature_IsRemovalNotBurn(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.False(result.HasFlag(CardEffect.Burn));
    }

    [Fact]
    // A SPELL that only sets up damage on your next casts removes nothing by
    // itself (Season of the Bold, 2026-10-13: "deve aggiungere un altro effetto").
    public void Classify_ASpellDamagingOnYourNextCasts_IsNotRemoval()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Season of the Bold", "Sorcery",
            "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Create a "
            + "tapped Treasure token.\n{P}{P} — Exile the top two cards of your library. Until the end of your next "
            + "turn, you may play them.\n{P}{P}{P} — Until the end of your next turn, whenever you cast a spell, "
            + "Season of the Bold deals 2 damage to up to one target creature."));

        Assert.False(result.HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Attempted Murder's two -1/-1 counters per even result on the creature chosen
    // first, Removal by hand (2026-10-27).
    [InlineData("Attempted Murder", "Sorcery",
        "Choose target creature. Roll X six-sided dice. For each even result, put two -1/-1 counters on that "
        + "creature. For each odd result, create a 1/2 blue Bird creature token with flying named Storm Crow.")]
    public void Classify_ACreatureChosenThenShrunkByADieRoll_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Removal, widened 2026-09-19. "Equal to" is how the game writes a creature
    // hitting another creature, and the rule read one word order, one length of
    // filler and four destinations.
    [InlineData("Repentance", "Instant", "Target creature deals damage to itself equal to its power.")]
    [InlineData("Allies at Last", "Sorcery",
        "Affinity for Allies (This spell costs {1} less to cast for each Ally you control.)\n"
        + "Up to two target creatures you control each deal damage equal to their power to target "
        + "creature an opponent controls.")]
    [InlineData("Betrayal at the Vault", "Sorcery",
        "Target creature you control deals damage equal to its power to each of two other target creatures.")]
    [InlineData("Slash of Light", "Instant",
        "Slash of Light deals damage equal to the number of creatures you control plus the number of "
        + "Equipment you control to target creature.")]
    // The amount is written as freely as the target.
    [InlineData("Banshee", "Creature — Spirit",
        "{X}, {T}: This creature deals half X damage, rounded down, to any target, and half X damage, "
        + "rounded up, to you.")]
    [InlineData("Firestorm", "Instant",
        "As an additional cost to cast this spell, discard X cards.\n"
        + "Firestorm deals X damage to each of X targets.")]
    // "Destroy UP TO ONE OTHER target creature" is sixteen characters of filler.
    [InlineData("Faller's Faithful", "Creature — Human",
        "When this creature enters, destroy up to one other target creature. If that creature wasn't "
        + "dealt damage this turn, its controller draws two cards.")]
    // An Aura shrinks without ever saying "target", and the counter can land on
    // the victim after one has landed on the card itself.
    [InlineData("Weakness", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets -2/-1.")]
    [InlineData("Immolation", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +2/-2.")]
    [InlineData("Serrated Biskelion", "Artifact Creature — Construct",
        "{T}: Put a -1/-1 counter on this creature and a -1/-1 counter on target creature.")]
    // ANY toughness malus counts, ruled 2026-09-19 — the size is not the question
    // and neither is what happens to the power.
    [InlineData("Funeral Charm", "Instant",
        "Choose one —\n• Target player discards a card.\n• Target creature gets +2/-1 until end of turn.\n"
        + "• Target creature gains swampwalk until end of turn.")]
    [InlineData("Coils of the Medusa", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets +1/-1.\n"
        + "Sacrifice this Aura: Destroy all non-Wall creatures blocking enchanted creature.")]
    [InlineData("Ironclaw Curse", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature gets -0/-1.\n"
        + "Enchanted creature can't block creatures with power equal to or greater than the enchanted "
        + "creature's toughness.")]
    [InlineData("Contagion", "Instant",
        "You may pay 1 life and exile a black card from your hand rather than pay this spell's mana cost.\n"
        + "Distribute two -2/-1 counters among one or two target creatures.")]
    // A creature that ends up on the BOTTOM of a library is as answered as one
    // that is destroyed.
    [InlineData("The Spot's Portal", "Instant",
        "Put target creature on the bottom of its owner's library. You lose 2 life unless you control "
        + "a Villain.")]
    [InlineData("Dramatic Accusation", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, tap enchanted creature.\n"
        + "Enchanted creature doesn't untap during its controller's untap step.\n"
        + "{U}{U}: Shuffle enchanted creature into its owner's library.")]
    public void Classify_AnswersOneCreature_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Your OWN creature is a blink, an outlet or a way to hide it from a Wrath.
    [InlineData("Cold Storage", "Artifact",
        "{3}: Exile target creature you control.\n"
        + "Sacrifice this artifact: Return each creature card exiled with this artifact to the "
        + "battlefield under your control.")]
    // A creature CARD IN A GRAVEYARD is already dead.
    [InlineData("Eater of the Dead", "Creature — Horror",
        "{0}: If this creature is tapped, exile target creature card from a graveyard and untap this creature.")]
    // An answer that can only touch what it is already fighting is a combat
    // trick, in the active voice as much as the passive.
    [InlineData("Wall of Corpses", "Creature — Wall",
        "Defender (This creature can't attack.)\n"
        + "{B}, Sacrifice this creature: Destroy target creature this creature is blocking.")]
    [InlineData("Elite Javelineer", "Creature — Human Soldier",
        "Whenever this creature blocks, it deals 1 damage to target attacking creature.")]
    // A shrink aimed at YOUR OWN creature is a pump with a price.
    [InlineData("Ashnod's Battle Gear", "Artifact",
        "You may choose not to untap this artifact during your untap step.\n"
        + "{2}, {T}: Target creature you control gets +2/-2 for as long as this artifact remains tapped.")]
    // A fight has to name what it fights: "Fight Crime" is the name of a mode.
    [InlineData("School Daze", "Instant",
        "Choose one —\n• Do Homework — Draw three cards.\n• Fight Crime — Counter target spell. Draw a card.")]
    // A COLOUR-restricted permanent kill is RemovePermanent, not this: the
    // breadth is the point, exactly as it is for Vindicate. Ruled 2026-09-20,
    // reversing the reading of the day before.
    [InlineData("Northern Paladin", "Creature — Human Knight",
        "{W}{W}, {T}: Destroy target black permanent.")]
    // "That creature's CONTROLLER" is a face, not the creature.
    [InlineData("Dingus Staff", "Artifact",
        "Whenever a creature dies, this artifact deals 2 damage to that creature's controller.")]
    // And the TOP of a library hands the card straight back.
    [InlineData("Time Ebb", "Sorcery", "Put target creature on top of its owner's library.")]
    // …but UNLESS defending player sacrifices one is the opponent's choice of a
    // price, not an edict.
    [InlineData("Ogre Marauder", "Creature — Ogre Warrior",
        "Whenever this creature attacks, it gains \"this creature can't be blocked\" until end of turn unless "
        + "defending player sacrifices a creature of their choice.")]
    public void Classify_WhatOnlyReadsLikeAnAnswer_IsNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // A creature SHUFFLED into its owner's library, from a spell or an ability.
    [InlineData("This Is How It Ends", "Instant",
        "Target creature's owner shuffles it into their library, then faces a villainous choice — They lose "
        + "5 life, or they shuffle another creature they own into their library.")]
    [InlineData("Zoyowa's Justice", "Instant",
        "The owner of target artifact or creature with mana value 1 or greater shuffles it into their "
        + "library. Then that player discovers X, where X is its mana value. (They exile cards from the top of "
        + "their library until they exile a nonland card with that mana value or less. They cast it without "
        + "paying its mana cost or put it into their hand. They put the rest on the bottom in a random order.)")]
    [InlineData("Blink", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI, III — "
        + "Choose target creature. Its owner shuffles it into their library, then investigates. (They create a "
        + "Clue token.)\nII, IV — Create a 2/2 black Alien Angel artifact creature token with first strike, "
        + "vigilance, and \"Whenever an opponent casts a creature spell, this token isn't a creature until end "
        + "of turn.\"")]
    // The edict that EXILES, the one offered as a villainous choice, and the
    // life-or-creature price every player pays.
    [InlineData("Blot Out", "Instant",
        "Target opponent exiles a creature or planeswalker they control with the greatest mana value among "
        + "creatures and planeswalkers they control.")]
    [InlineData("Midnight Crusader Shuttle", "Artifact — Vehicle",
        "Midnight Entity — Whenever this Vehicle attacks, defending player faces a villainous choice — That "
        + "player sacrifices a creature of their choice, or you gain control of a creature of your choice that "
        + "player controls until end of turn. If you gain control of a creature this way, tap it, and it's "
        + "attacking that player.\nCrew 2")]
    [InlineData("Bellowing Mauler", "Creature — Ogre Warrior",
        "At the beginning of your end step, each player loses 4 life unless they sacrifice a nontoken "
        + "creature of their choice.")]
    // A creature that stops being one: a Treasure, a Clue, a face-down 2/2.
    [InlineData("Kitesail Larcenist", "Creature — Human Pirate",
        "Flying, ward {1}\nWhen this creature enters, for each player, choose up to one other target "
        + "artifact or creature that player controls. For as long as this creature remains on the battlefield, "
        + "the chosen permanents become Treasure artifacts with \"{T}, Sacrifice this artifact: Add one mana "
        + "of any color\" and lose all other abilities.")]
    [InlineData("In Too Deep", "Enchantment — Aura",
        "Split second (As long as this spell is on the stack, players can't cast spells or activate "
        + "abilities that aren't mana abilities.)\nEnchant creature, planeswalker, or Clue\nEnchanted "
        + "permanent is a colorless Clue artifact with \"{2}, Sacrifice this artifact: Draw a card\" and loses "
        + "all other abilities. (It's no longer a creature or planeswalker.)")]
    [InlineData("Cyber Conversion", "Instant",
        "Turn target creature face down. It's a 2/2 Cyberman artifact creature.")]
    // Damage whose amount is written any other way.
    [InlineData("Fires of Victory", "Instant",
        "Kicker {2}{U} (You may pay an additional {2}{U} as you cast this spell.)\nIf this spell was kicked, "
        + "draw a card. Fires of Victory deals damage to target creature or planeswalker equal to the number "
        + "of cards in your hand.")]
    [InlineData("Spinning Wheel Kick", "Sorcery",
        "Target creature you control deals damage equal to its power to each of X target creatures and/or "
        + "planeswalkers.")]
    [InlineData("Borborygmos and Fblthp", "Legendary Creature — Cyclops Homunculus",
        "Whenever Borborygmos and Fblthp enters or attacks, draw a card, then you may discard any number of "
        + "land cards. When you discard one or more cards this way, Borborygmos and Fblthp deals twice that "
        + "much damage to target creature.\n{1}{U}: Put Borborygmos and Fblthp into its owner's library third "
        + "from the top.")]
    [InlineData("The Sea Devils", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — "
        + "Create a 2/2 green Alien Salamander creature token with islandwalk. (It can't be blocked as long as "
        + "defending player controls an Island.)\nIII — Until end of turn, whenever a Salamander deals combat "
        + "damage to a player, it deals that much damage to target creature that player controls.")]
    [InlineData("Invasion of Regatha", "Battle — Siege",
        "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's "
        + "defeated, exile it, then cast it transformed.)\nWhen this Siege enters, it deals 4 damage to "
        + "another target battle or opponent and 1 damage to up to one target creature.\nProwess (Whenever you "
        + "cast a noncreature spell, this creature gets +1/+1 until end of turn.)\nIf a noncreature source you "
        + "control would deal damage to a creature, battle, or opponent, it deals that much damage plus 2 "
        + "instead.")]
    [InlineData("Chandra, Hope's Beacon", "Legendary Planeswalker — Chandra",
        "Whenever you cast an instant or sorcery spell, copy it. You may choose new targets for the copy. "
        + "This ability triggers only once each turn.\n+2: Add two mana in any combination of colors.\n+1: "
        + "Exile the top five cards of your library. Until the end of your next turn, you may cast an instant "
        + "or sorcery spell from among those exiled cards.\n−X: Chandra deals X damage to each of up to two "
        + "targets.")]
    [InlineData("Jaya's Immolating Inferno", "Legendary Sorcery",
        "(You may cast a legendary sorcery only if you control a legendary creature or planeswalker.)\nJaya's "
        + "Immolating Inferno deals X damage to each of up to three targets.")]
    // A creature that stops being one, continued: a Treasure from a −2, a Food
    // from an Aura (Sugar Coat, In Too Deep's twin).
    [InlineData("Vraska, Betrayal's Sting", "Legendary Planeswalker — Vraska",
        "−2: Target creature becomes a Treasure artifact with \"{T}, Sacrifice this artifact: Add one mana "
        + "of any color\" and loses all other card types and abilities.")]
    [InlineData("Sugar Coat", "Enchantment — Aura",
        "Flash\nEnchant creature or Food\nEnchanted permanent is a colorless Food artifact with \"{2}, {T}, "
        + "Sacrifice this artifact: You gain 3 life\" and loses all other card types and abilities.")]
    // An Aura that can exile what it holds; a phasing that lasts as long as
    // the card does. (The Phasing of Zhalfir, a nonland PERMANENT phased out so,
    // moved on 2026-11-08 to RemovePermanent alone, in that tag's file.)
    [InlineData("Cooped Up", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature can't attack or block.\n{2}{W}: Exile enchanted creature.")]
    [InlineData("Oubliette", "Enchantment",
        "When this enchantment enters, target creature phases out until this enchantment leaves the "
        + "battlefield. Tap that creature as it phases in this way. (Auras and Equipment phase out with it. "
        + "While permanents are phased out, they're treated as though they don't exist.)")]
    // Sentinel: a blink beside DAMAGE of its own keeps the damage's tag.
    [InlineData("Immersturm", "Plane — Valla",
        "Whenever a creature enters, that creature's controller may have it deal damage equal to its power "
        + "to any target of their choice.\nWhenever chaos ensues, exile target creature, then return it to the "
        + "battlefield under its owner's control.")]
    // Sentinel: TWO points from a token that leaves still kill something.
    [InlineData("Weapons Manufacturing", "Enchantment",
        "Whenever a nontoken artifact you control enters, create a colorless artifact token named Munitions "
        + "with \"When this token leaves the battlefield, it deals 2 damage to any target.\"")]
    public void Classify_TheOctoberRemovalGaps_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // A CARD exiled from a graveyard, however long the sentence runs.
    [InlineData("Armored Scrapgorger", "Creature — Phyrexian Beast",
        "This creature gets +3/+0 as long as it has three or more oil counters on it.\n{T}: Add one mana of "
        + "any color.\nWhenever this creature becomes tapped, exile target card from a graveyard and put an "
        + "oil counter on this creature.")]
    [InlineData("Lobelia Sackville-Baggins", "Legendary Creature — Halfling Citizen",
        "Flash\nMenace\nWhen Lobelia enters, exile target creature card from an opponent's graveyard that "
        + "was put there from the battlefield this turn, then create X Treasure tokens, where X is the exiled "
        + "card's power.")]
    // A blink of any creature, and of one you own.
    [InlineData("Twining Twins", "Creature — Faerie Wizard",
        "Flying, vigilance, ward {1}\nExile target nontoken creature. Return it to the battlefield under its "
        + "owner's control at the beginning of the next end step.")]
    [InlineData("Slip On the Ring", "Instant",
        "Exile target creature you own, then return it to the battlefield under your control. The Ring "
        + "tempts you.")]
    [InlineData("Northampton Farm", "Land",
        "{T}: Add {C}.\n{1}, {T}: Exile target creature you own.\n{2}, {T}, Sacrifice this land: Return a "
        + "creature card exiled with this land to the battlefield under your control. Return each other card "
        + "exiled with this land to its owner's hand.")]
    // The Devil's one-point ping is the Devil's.
    [InlineData("Pugnacious Pugilist", "Creature — Ogre Warrior",
        "Whenever this creature attacks, create a tapped and attacking 1/1 red Devil creature token with "
        + "\"When this token dies, it deals 1 damage to any target.\"\nBlitz {3}{R} (If you cast this spell "
        + "for its blitz cost, it gains haste and \"When this creature dies, draw a card.\" Sacrifice it at "
        + "the beginning of the next end step.)")]
    // Sentinels: a phasing held by a TAPPED artifact is a tapper's lock; the
    // price of "a creature, artifact, or land" is not an edict; an Aura that
    // exiles its creature AND ITS AURAS is a blink.
    [InlineData("The Pandorica", "Legendary Artifact",
        "You may choose not to untap The Pandorica during your untap step.\n{1}{W}, {T}: Untap another "
        + "target nonland permanent, then it phases out. It can't phase in for as long as The Pandorica "
        + "remains tapped. When The Pandorica becomes untapped or leaves the battlefield, that permanent "
        + "phases in. Activate only as a sorcery.")]
    [InlineData("Tomb of Annihilation", "Dungeon",
        "Trapped Entry — Each player loses 1 life. (Leads to: Veils of Fear, Oubliette)\nVeils of Fear — "
        + "Each player loses 2 life unless they discard a card. (Leads to: Sandfall Cell)\nSandfall Cell — "
        + "Each player loses 2 life unless they sacrifice a creature, artifact, or land of their choice. "
        + "(Leads to: Cradle of the Death God)\nOubliette — Discard a card and sacrifice a creature, an "
        + "artifact, and a land. (Leads to: Cradle of the Death God)\nCradle of the Death God — Create The "
        + "Atropal, a legendary 4/4 black God Horror creature token with deathtouch.")]
    [InlineData("Flickerform", "Enchantment — Aura",
        "Enchant creature\n{2}{W}{W}: Exile enchanted creature and all Auras attached to it. At the "
        + "beginning of the next end step, return that card to the battlefield under its owner's control. If "
        + "you do, return the other cards exiled this way to the battlefield under their owners' control "
        + "attached to that creature.")]
    public void Classify_TheOctoberRemovalGaps_AreNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The Removal pass of 2026-10-16. R1 PUNISHER: an opponent or each player
    // choosing between a creature and something else is an edict (Fandaniel, Fatal
    // Lore, Bellowing Mauler; Mogis, Tomb Blade, Unnatural Hunger, Demanding
    // Dragon; Pillar Tombs of Aku). R3: Chandra's emblem. R4: a removal ability
    // granted for one turn (Dead Before Sunrise, Vorinclex, Gaze of Pain). R2
    // keeps Chaos Charm and Reign of Chaos on their other clause. The majority
    // outliers the code already proposed, and the twenty readings F3-F20.
    [InlineData("Fandaniel, Telophoroi Ascian", "Legendary Creature — Elder Wizard",
        "Whenever you cast an instant or sorcery spell, surveil 1.\nAt the beginning of your end step, each "
        + "opponent may sacrifice a nontoken creature of their choice. Each opponent who doesn't loses 2 life "
        + "for each instant and sorcery card in your graveyard.")]
    [InlineData("Fatal Lore", "Sorcery",
        "An opponent chooses one —\n• You draw three cards.\n• You destroy up to two target creatures that "
        + "player controls. They can't be regenerated. That player draws up to three cards.")]
    [InlineData("Mogis, God of Slaughter", "Legendary Enchantment Creature — God",
        "Indestructible\nAs long as your devotion to black and red is less than seven, Mogis isn't a "
        + "creature.\nAt the beginning of each opponent's upkeep, Mogis deals 2 damage to that player unless "
        + "they sacrifice a creature of their choice.")]
    [InlineData("Tomb Blade", "Artifact Creature — Necron",
        "Flying\nWhenever this creature deals combat damage to a player, that player loses life equal to the "
        + "number of creatures they control unless they sacrifice a creature of their choice.\nUnearth {6}{B}{B}")]
    [InlineData("Unnatural Hunger", "Enchantment — Aura",
        "Enchant creature\nAt the beginning of the upkeep of enchanted creature's controller, this Aura deals "
        + "damage equal to that creature's power to that player unless they sacrifice another creature of their "
        + "choice.")]
    [InlineData("Demanding Dragon", "Creature — Dragon",
        "Flying\nWhen this creature enters, it deals 5 damage to target opponent unless that player sacrifices "
        + "a creature of their choice.")]
    [InlineData("Pillar Tombs of Aku", "World Enchantment",
        "At the beginning of each player's upkeep, that player may sacrifice a creature of their choice. If "
        + "that player doesn't, they lose 5 life and you sacrifice this enchantment.")]
    [InlineData("Chaos Charm", "Instant",
        "Choose one —\n• Destroy target Wall.\n• Chaos Charm deals 1 damage to target creature.\n• Target "
        + "creature gains haste until end of turn.")]
    [InlineData("Reign of Chaos", "Sorcery",
        "Choose one —\n• Destroy target Plains and target white creature.\n• Destroy target Island and target "
        + "blue creature.")]
    [InlineData("Chandra, Spark Hunter", "Legendary Planeswalker — Chandra",
        "At the beginning of combat on your turn, choose up to one target Vehicle you control. Until end of "
        + "turn, it becomes an artifact creature and gains haste.\n+2: You may sacrifice an artifact or discard "
        + "a card. If you do, draw a card.\n0: Create a 3/2 colorless Vehicle artifact token with crew 1.\n−7: "
        + "You get an emblem with \"Whenever an artifact you control enters, this emblem deals 3 damage to any "
        + "target.\"")]
    [InlineData("Dead Before Sunrise", "Instant",
        "Until end of turn, outlaw creatures you control get +1/+0 and gain \"{T}: This creature deals damage "
        + "equal to its power to target creature.\" (Assassins, Mercenaries, Pirates, Rogues, and Warlocks are "
        + "outlaws.)")]
    [InlineData("Vorinclex // The Grand Evolution", "Legendary Creature — Phyrexian Praetor // Enchantment — Saga",
        "Reach, trample\nWhen Vorinclex enters, search your library for up to two Forest cards, reveal them, "
        + "put them into your hand, then shuffle.\n{6}{G}{G}: Exile Vorinclex, then return it to the battlefield "
        + "transformed under its owner's control. Activate only as a sorcery.\n(As this Saga enters and after "
        + "your draw step, add a lore counter.)\nI — Mill ten cards. Put up to two creature cards from among the "
        + "milled cards onto the battlefield.\nII — Distribute seven +1/+1 counters among any number of target "
        + "creatures you control.\nIII — Until end of turn, creatures you control gain \"{1}: This creature "
        + "fights target creature you don't control.\" Exile this Saga, then return it to the battlefield (front "
        + "face up).")]
    [InlineData("Gaze of Pain", "Sorcery",
        "Until end of turn, whenever a creature you control attacks and isn't blocked, you may choose to have "
        + "it deal damage equal to its power to a target creature. If you do, it assigns no combat damage this "
        + "turn.")]
    [InlineData("Spider-Man 2099", "Legendary Creature — Spider Human Hero",
        "From the Future — You can't cast Spider-Man 2099 during your first, second, or third turns of the "
        + "game.\nDouble strike, vigilance\nAt the beginning of your end step, if you've played a land or cast a "
        + "spell this turn from anywhere other than your hand, Spider-Man 2099 deals damage equal to his power "
        + "to any target.")]
    [InlineData("Sawblade Skinripper", "Creature — Human Assassin",
        "Menace\n{2}, Sacrifice another creature or enchantment: Put a +1/+1 counter on this creature.\nAt the "
        + "beginning of your end step, if you sacrificed one or more permanents this turn, this creature deals "
        + "that much damage to any target.")]
    [InlineData("Crimson Manticore", "Creature — Manticore",
        "Flying\n{R}, {T}: This creature deals 1 damage to target attacking or blocking creature.")]
    [InlineData("Marjhan", "Creature — Serpent",
        "This creature doesn't untap during your untap step.\n{U}{U}, Sacrifice a creature: Untap this "
        + "creature. Activate only during your upkeep.\nThis creature can't attack unless defending player "
        + "controls an Island.\n{U}{U}: This creature gets -1/-0 until end of turn and deals 1 damage to target "
        + "attacking creature without flying.\nWhen you control no Islands, sacrifice this creature.")]
    [InlineData("Sawback Manticore", "Creature — Manticore",
        "{4}: This creature gains flying until end of turn.\n{1}: This creature deals 2 damage to target "
        + "attacking or blocking creature. Activate only if this creature is attacking or blocking and only "
        + "once each turn.")]
    [InlineData("Gargantuan Gorilla", "Creature — Ape",
        "At the beginning of your upkeep, you may sacrifice a Forest. If you sacrifice a snow Forest this "
        + "way, this creature gains trample until end of turn. If you don't sacrifice a Forest, sacrifice this "
        + "creature and it deals 7 damage to you.\n{T}: This creature deals damage equal to its power to another "
        + "target creature. That creature deals damage equal to its power to this creature.")]
    [InlineData("Papalymo Totolymo", "Legendary Creature — Dwarf Wizard",
        "Whenever you cast a noncreature spell, Papalymo Totolymo deals 1 damage to each opponent and you "
        + "gain 1 life.\n{4}, {T}, Sacrifice Papalymo Totolymo: Each opponent who lost life this turn sacrifices "
        + "a creature with the greatest power among creatures they control.")]
    [InlineData("Case of the Gateway Express", "Enchantment — Case",
        "When this Case enters, choose target creature you don't control. Each creature you control deals 1 "
        + "damage to that creature.\nTo solve — Three or more creatures attacked this turn. (If unsolved, solve "
        + "at the beginning of your end step.)\nSolved — Creatures you control get +1/+0.")]
    [InlineData("Transforming Flourish", "Instant",
        "Demonstrate (When you cast this spell, you may copy it. If you do, choose an opponent to also copy "
        + "it. Players may choose new targets for their copies.)\nDestroy target artifact or creature you don't "
        + "control. If that permanent is destroyed this way, its controller exiles cards from the top of their "
        + "library until they exile a nonland card, then they may cast that card without paying its mana cost.")]
    [InlineData("Farrel's Mantle", "Enchantment — Aura",
        "Enchant creature\nWhenever enchanted creature attacks and isn't blocked, its controller may have it "
        + "deal damage equal to its power plus 2 to another target creature. If that player does, the attacking "
        + "creature assigns no combat damage this turn.")]
    [InlineData("Hearth Charm", "Instant",
        "Choose one —\n• Destroy target artifact creature.\n• Attacking creatures get +1/+0 until end of "
        + "turn.\n• Target creature with power 2 or less can't be blocked this turn.")]
    [InlineData("Summon: Magus Sisters", "Enchantment Creature — Saga Faerie",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II, III "
        + "— Choose one at random —\n• Combine Powers! — Put three +1/+1 counters on target creature.\n• Defense! "
        + "— Put a shield counter on target creature. You gain 3 life.\n• Fight! — This creature fights up to "
        + "one target creature an opponent controls.\nHaste")]
    [InlineData("Beastie Beatdown", "Sorcery",
        "Choose target creature you control and target creature an opponent controls.\nDelirium — If there are "
        + "four or more card types among cards in your graveyard, put two +1/+1 counters on the creature you "
        + "control.\nThe creature you control deals damage equal to its power to the creature an opponent "
        + "controls.")]
    [InlineData("Blood Frenzy", "Instant",
        "Cast this spell only before the combat damage step.\nTarget attacking or blocking creature gets +4/+0 "
        + "until end of turn. Destroy that creature at the beginning of the next end step.")]
    [InlineData("Depressurize", "Instant",
        "Target creature gets -3/-0 until end of turn. Then if that creature's power is 0 or less, destroy "
        + "it.")]
    [InlineData("Drop of Honey", "Enchantment",
        "At the beginning of your upkeep, destroy the creature with the least power. It can't be regenerated. "
        + "If two or more creatures are tied for least power, you choose one of them.\nWhen there are no "
        + "creatures on the battlefield, sacrifice this enchantment.")]
    [InlineData("Expel the Unworthy", "Sorcery",
        "Kicker {2}{W} (You may pay an additional {2}{W} as you cast this spell.)\nChoose target creature with "
        + "mana value 3 or less. If this spell was kicked, instead choose target creature. Exile the chosen "
        + "creature, then its controller gains life equal to its mana value.")]
    [InlineData("V.A.T.S.", "Instant",
        "Split second (As long as this spell is on the stack, players can't cast spells or activate abilities "
        + "that aren't mana abilities.)\nChoose any number of target creatures with equal toughness. Destroy the "
        + "chosen creatures.")]
    [InlineData("Sadistic Shell Game", "Sorcery",
        "Starting with the next opponent in turn order, each player chooses a creature you don't control. "
        + "Destroy the chosen creatures.")]
    [InlineData("Fear of Burning Alive", "Enchantment Creature — Nightmare",
        "When this creature enters, it deals 4 damage to each opponent.\nDelirium — Whenever a source you "
        + "control deals noncombat damage to an opponent, if there are four or more card types among cards in "
        + "your graveyard, this creature deals that amount of damage to target creature that player controls.")]
    [InlineData("Torch the Witness", "Sorcery",
        "Torch the Witness deals twice X damage to target creature. If excess damage was dealt to that "
        + "creature this way, investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this "
        + "token: Draw a card.\")")]
    [InlineData("Floodpits Drowner", "Creature — Merfolk",
        "Flash\nVigilance\nWhen this creature enters, tap target creature an opponent controls and put a stun "
        + "counter on it.\n{1}{U}, {T}: Shuffle this creature and target creature with a stun counter on it into "
        + "their owners' libraries.")]
    [InlineData("Galvanic Discharge", "Instant",
        "Choose target creature or planeswalker. You get {E}{E}{E} (three energy counters), then you may pay "
        + "any amount of {E}. Galvanic Discharge deals that much damage to that permanent.")]
    [InlineData("Giant Oyster", "Creature — Oyster",
        "You may choose not to untap this creature during your untap step.\n{T}: For as long as this creature "
        + "remains tapped, target tapped creature doesn't untap during its controller's untap step, and at the "
        + "beginning of each of your draw steps, put a -1/-1 counter on that creature. When this creature "
        + "leaves the battlefield or becomes untapped, remove all -1/-1 counters from the creature.")]
    [InlineData("Mana Chains", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has \"Cumulative upkeep {1}.\" (At the beginning of its "
        + "controller's upkeep, that player puts an age counter on it, then sacrifices it unless they pay its "
        + "upkeep cost for each age counter on it.)")]
    [InlineData("Retribution", "Sorcery",
        "Choose two target creatures controlled by the same opponent. That player chooses and sacrifices one "
        + "of those creatures. Put a -1/-1 counter on the other.")]
    // Rush of Dread's half moved to Wipe on 2026-11-08, see
    // Classify_TheNovember8Ruling_SeveralCreaturesFromOneSide_IsWipeNotRemoval.
    [InlineData("Soltari Guerrillas", "Creature — Soltari Soldier",
        "Shadow (This creature can block or be blocked by only creatures with shadow.)\n{0}: The next time "
        + "this creature would deal combat damage to an opponent this turn, it deals that damage to target "
        + "creature instead.")]
    [InlineData("Sonar Strike", "Instant",
        "Sonar Strike deals 4 damage to target attacking, blocking, or tapped creature. You gain 3 life if "
        + "you control a Bat.")]
    [InlineData("Vault 11: Voter's Dilemma", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — For "
        + "each opponent, you create a 1/1 white Human Soldier creature token.\nII, III — Each player secretly "
        + "votes for up to one creature, then those votes are revealed. If no creature got votes, each player "
        + "draws a card. Otherwise, destroy each creature with the most votes or tied for most votes.")]
    [InlineData("Vile Mutilator", "Creature — Demon",
        "As an additional cost to cast this spell, sacrifice a creature or enchantment.\nFlying, trample\nWhen "
        + "this creature enters, each opponent sacrifices a nontoken enchantment of their choice, then "
        + "sacrifices a nontoken creature of their choice.")]
    [InlineData("Winter Blast", "Sorcery",
        "Tap X target creatures. Winter Blast deals 2 damage to each of those creatures with flying.")]
    // Paid in LIFE it is the same Aura, ruled 2026-10-16 'Removal, no Pacify'
    // (Decomposition).
    [InlineData("Decomposition", "Enchantment — Aura",
        "Enchant black creature\nEnchanted creature has \"Cumulative upkeep—Pay 1 life.\" (At the beginning of "
        + "its controller's upkeep, that player puts an age counter on it, then sacrifices it unless they pay "
        + "its upkeep cost for each age counter on it.)\nWhen enchanted creature dies, its controller loses 2 "
        + "life.")]
    public void Classify_TheOctober16RemovalPassGains_AreRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // The Removal pass of 2026-10-16, what stays out. R2: a target limited to a
    // creature type is never Removal (Rashida Scalebane, Phoenix Down, Tunnel,
    // Word of Blasting, King Suleiman, Human Frailty, Thraben Exorcism). F1:
    // Goblin Vandal's own creature is not a second victim (Floral Spuzzem as it
    // was). Tomb of Annihilation asks for a card or a creature, artifact or land.
    // F4/F7/F18 neighbours: Mogg Cannon and Stone Giant sentence a creature YOU
    // control, Plasma Caster chooses only the creature blocking it, Sail into the
    // West votes on a mode. Misfortune and Withering Wisps are the pure Wipe pins.
    [InlineData("Rashida Scalebane", "Legendary Creature — Human Soldier",
        "{T}: Destroy target attacking or blocking Dragon. It can't be regenerated. You gain life equal to "
        + "its power.")]
    [InlineData("Phoenix Down", "Artifact",
        "{1}{W}, {T}, Exile this artifact: Choose one —\n• Return target creature card with mana value 4 or "
        + "less from your graveyard to the battlefield tapped.\n• Exile target Skeleton, Spirit, or Zombie.")]
    [InlineData("Tunnel", "Instant",
        "Destroy target Wall. It can't be regenerated.")]
    [InlineData("Word of Blasting", "Instant",
        "Destroy target Wall. It can't be regenerated. Word of Blasting deals damage equal to that Wall's "
        + "mana value to the Wall's controller.")]
    [InlineData("King Suleiman", "Creature — Human Noble",
        "{T}: Destroy target Djinn or Efreet.")]
    [InlineData("Human Frailty", "Instant",
        "Destroy target Human creature.")]
    [InlineData("Thraben Exorcism", "Instant",
        "Exile target Spirit, creature with disturb, or enchantment.")]
    [InlineData("Goblin Vandal", "Creature — Goblin Rogue",
        "Whenever this creature attacks and isn't blocked, you may pay {R}. If you do, destroy target "
        + "artifact defending player controls and this creature assigns no combat damage this turn.")]
    [InlineData("Floral Spuzzem", "Creature — Elemental",
        "Whenever this creature attacks and isn't blocked, you may destroy target artifact defending player "
        + "controls. If you do, this creature assigns no combat damage this turn.")]
    [InlineData("Mogg Cannon", "Artifact",
        "{T}: Target creature you control gets +1/+0 and gains flying until end of turn. Destroy that "
        + "creature at the beginning of the next end step.")]
    [InlineData("Stone Giant", "Creature — Giant",
        "{T}: Target creature you control with toughness less than this creature's power gains flying until "
        + "end of turn. Destroy that creature at the beginning of the next end step.")]
    [InlineData("Plasma Caster", "Artifact — Equipment",
        "Equipped creature gets +1/+1.\nWhenever equipped creature attacks, you get {E}{E} (two energy "
        + "counters).\nPay {E}{E}: Choose target creature that's blocking equipped creature. Flip a coin. If you "
        + "win the flip, exile the chosen creature. Otherwise, this Equipment deals 1 damage to it.\nEquip {2}")]
    [InlineData("Sail into the West", "Instant",
        "Will of the council — Starting with you, each player votes for return or embark. If return gets more "
        + "votes, each player returns up to two cards from their graveyard to their hand, then you exile Sail "
        + "into the West. If embark gets more votes or the vote is tied, each player may discard their hand and "
        + "draw seven cards.")]
    [InlineData("Misfortune", "Sorcery",
        "An opponent chooses one —\n• You put a +1/+1 counter on each creature you control and gain 4 life.\n• "
        + "You put a -1/-1 counter on each creature that player controls and Misfortune deals 4 damage to that "
        + "player.")]
    [InlineData("Withering Wisps", "Enchantment",
        "At the beginning of the end step, if no creatures are on the battlefield, sacrifice this "
        + "enchantment.\n{B}: This enchantment deals 1 damage to each creature and each player. Activate no more "
        + "times each turn than the number of snow Swamps you control.")]
    public void Classify_TheOctober16RemovalPassRemovals_AreNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Read 2026-10-27 from the sitting of 10-20..10-26, every card tagged Removal
    // by hand: damage EQUAL TO something divided among targets (Captain America,
    // Orca); a target chosen first and hit after, 'damage to it' (Grisly Sigil),
    // 'the chosen permanent' (Quag Feast), 'each of the chosen creatures' (Mega
    // Flare); the edict between two targets of the same PLAYER (Incriminate), the
    // secret choice among theirs (Malik), one creature each player chooses (The
    // Horus Heresy) and one per opponent by its power (Highcliff Felidar); and the
    // Aura shrink of an 'artifact or creature' (Clawing Torment).
    [InlineData("Captain America, First Avenger", "Legendary Creature — Human Soldier Hero",
        "Throw ... — {3}, Unattach an Equipment from Captain America: He deals damage equal to that "
        + "Equipment's mana value divided as you choose among one, two, or three targets.\n... Catch — At the "
        + "beginning of combat on your turn, attach up to one target Equipment you control to Captain America.")]
    [InlineData("Orca, Siege Demon", "Legendary Creature — Demon",
        "Trample\nWhenever another creature dies, put a +1/+1 counter on Orca.\nWhen Orca dies, it deals damage "
        + "equal to its power divided as you choose among any number of targets.")]
    [InlineData("Grisly Sigil", "Sorcery",
        "Casualty 1 (As you cast this spell, you may sacrifice a creature with power 1 or greater. When you "
        + "do, copy this spell and you may choose a new target for the copy.)\nChoose target creature or "
        + "planeswalker. If it was dealt noncombat damage this turn, Grisly Sigil deals 3 damage to it and you "
        + "gain 3 life. Otherwise, Grisly Sigil deals 1 damage to it and you gain 1 life.")]
    [InlineData("Quag Feast", "Sorcery",
        "Choose target creature, planeswalker, or Vehicle. Mill two cards, then destroy the chosen permanent "
        + "if its mana value is less than or equal to the number of cards in your graveyard.")]
    [InlineData("Mega Flare", "Sorcery",
        "Kicker {3}{R}{R}\nIf this spell was kicked, create a 6/6 red Dragon creature token with flying.\nFor "
        + "each opponent, choose up to one target creature that player controls. Mega Flare deals damage equal "
        + "to the greatest power among creatures you control to each of the chosen creatures.")]
    [InlineData("Incriminate", "Sorcery",
        "Choose two target creatures controlled by the same player. That player sacrifices one of them of "
        + "their choice.")]
    [InlineData("Malik, Grim Manipulator", "Legendary Creature — Human Rogue",
        "When Malik, Grim Manipulator enters, you and target opponent each secretly choose a creature that "
        + "player controls. Then those choices are revealed, and that player sacrifices those "
        + "creatures.\nWhenever an opponent sacrifices a creature, you create a Treasure token.")]
    [InlineData("The Horus Heresy", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — For "
        + "each opponent, gain control of up to one target nonlegendary creature that player controls for as "
        + "long as this Saga remains on the battlefield.\nII — Draw a card for each creature you control but "
        + "don't own.\nIII — Starting with you, each player chooses a creature. Destroy each creature chosen "
        + "this way.")]
    [InlineData("Highcliff Felidar", "Creature — Cat Beast",
        "Vigilance\nWhen this creature enters, for each opponent, choose a creature with the greatest power "
        + "among creatures that player controls. Destroy those creatures.")]
    [InlineData("Clawing Torment", "Enchantment — Aura",
        "Enchant artifact or creature\nAs long as enchanted permanent is a creature, it gets -1/-1 and can't "
        + "block.\nEnchanted permanent has \"At the beginning of your upkeep, you lose 1 life.\"")]
    public void Classify_TheOctober27RemovalPass_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Read 2026-10-27: a blink whose return follows 'If you do,' (Astral Drift,
    // Protection alone by hand); an exile its owner may play 'for as long as that
    // card remains exiled' (Prowl, Bounce by hand); a FIGHTER limited to a
    // creature type, with the tribal rulings (Ayula, untagged); and ANNIHILATOR,
    // ruled not Removal on 2026-10-27 (Nazgul Battle-Mace corrected by name, 6 of
    // the 7 reviewed untagged).
    [InlineData("Astral Drift", "Enchantment",
        "Whenever you cycle this card or cycle another card while this enchantment is on the battlefield, you "
        + "may exile target creature. If you do, return that card to the battlefield under its owner's control "
        + "at the beginning of the next end step.\nCycling {2}{W} ({2}{W}, Discard this card: Draw a card.)")]
    [InlineData("Prowl, Stoic Strategist // Prowl, Pursuit Vehicle", "Legendary Artifact Creature — Robot // Legendary Artifact — Vehicle",
        "More Than Meets the Eye {2}{W} (You may cast this card converted for {2}{W}.)\nWhenever Prowl "
        + "attacks, exile up to one other target tapped creature or Vehicle. For as long as that card remains "
        + "exiled, its owner may play it.\nWhenever a player plays a card exiled with Prowl, you draw a card and "
        + "convert Prowl.\nLiving metal (During your turn, this Vehicle is also a creature.)\nWhenever another "
        + "creature or Vehicle you control enters, put a +1/+1 counter on Prowl. If this is the second time "
        + "this ability has resolved this turn, convert Prowl.")]
    [InlineData("Ayula, Queen Among Bears", "Legendary Creature — Bear",
        "Whenever another Bear you control enters, choose one —\n• Put two +1/+1 counters on target Bear.\n• "
        + "Target Bear you control fights target creature you don't control.")]
    [InlineData("Nazgûl Battle-Mace", "Artifact — Equipment",
        "Equipped creature has menace, deathtouch, annihilator 1, and \"Whenever an opponent sacrifices a "
        + "nontoken permanent, put that card onto the battlefield under your control unless that player pays 3 "
        + "life.\" (Whenever a creature with annihilator 1 attacks, defending player sacrifices a permanent of "
        + "their choice.)\nEquip {3}")]
    public void Classify_TheOctober27RemovalPass_IsNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // A target creature CARD is in a graveyard, not on the battlefield.
    [InlineData("Lazotep Quarry", "Land — Desert",
        "{T}: Add {C}.\n{T}, Sacrifice a creature: Add one mana of any color.\n{X}{2}, {T}, Sacrifice a Desert: "
        + "Exile target creature card with mana value X from your graveyard. Create a token that's a copy of "
        + "it, except it's a 4/4 black Zombie. Activate only as a sorcery.")]
    public void Classify_TheOctober16Pass_ATargetCreatureCard_IsNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // Read 2026-11-05: the statistic edict (Fall of the Impostor: the creature
    // with the greatest power among those a target opponent controls), loyalty
    // counters taken off a planeswalker an opponent controls (Confront the Past),
    // and a target that may be a creature on the battlefield OR a card in a
    // graveyard (Savior of Ollenbock, so the graveyard half does not make the
    // whole an answer to nobody). The edict by piles read the same day (Make an
    // Example) moved to Wipe on 2026-11-08.
    [InlineData("Fall of the Impostor", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — Put "
        + "a +1/+1 counter on up to one target creature.\nIII — Exile a creature with the greatest power among "
        + "creatures target opponent controls.")]
    [InlineData("Confront the Past", "Sorcery — Lesson",
        "Choose one —\n• Return target planeswalker card with mana value X or less from your graveyard to the "
        + "battlefield.\n• Remove twice X loyalty counters from target planeswalker an opponent controls.")]
    [InlineData("Savior of Ollenbock", "Creature — Human Soldier",
        "Training (Whenever this creature attacks with another creature with greater power, put a +1/+1 "
        + "counter on this creature.)\nWhenever this creature trains, exile up to one other target creature from "
        + "the battlefield or creature card from a graveyard.\nWhen this creature leaves the battlefield, put "
        + "the exiled cards onto the battlefield under their owners' control.")]
    public void Classify_TheNovember5RemovalPass_IsRemoval(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }

    [Theory]
    // RULED 2026-11-05, corrected 2026-11-06: an Aura that makes any nonland
    // permanent a Treasure that loses all other abilities is RemovePermanent
    // alone, and neither Removal nor Pacify (Minimus Containment): "è sia
    // removal che remove permanent ma basta solo questo secondo me per
    // l'ontologia attuale".
    [InlineData("Minimus Containment", "Enchantment — Aura",
        "Enchant nonland permanent\nEnchanted permanent is a Treasure artifact with \"{T}, Sacrifice this "
        + "artifact: Add one mana of any color,\" and it loses all other abilities. (If it was a creature, it's "
        + "no longer a creature.)")]
    public void Classify_TheNovember5Ruling_MinimusContainment_IsRemovePermanentAloneNotRemovalNorPacify(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Removal));
        Assert.True(result.HasFlag(CardEffect.RemovePermanent));
        Assert.False(result.HasFlag(CardEffect.Pacify));
    }

    [Theory]
    // Ruled 2026-11-06 with the Burn definition (Steal + Removal): a creature
    // stolen and sacrificed at the next end step is gone from its owner for good,
    // a kill beside the theft; the attack itself is Act of Treason's and no Burn.
    // In Thrall to the Pit sacrifices only when kicked and was handed back with
    // the tag.
    [InlineData("Slave of Bolas", "Sorcery",
        "Gain control of target creature. Untap that creature. It gains haste until end of turn. Sacrifice it "
        + "at the beginning of the next end step.")]
    [InlineData("In Thrall to the Pit", "Sorcery",
        "Kicker {2}{B} (You may pay an additional {2}{B} as you cast this spell.)\nGain control of target "
        + "creature until end of turn. Untap that creature. It gains haste until end of turn. If this spell was "
        + "kicked, sacrifice that creature at the beginning of the next end step.")]
    public void Classify_TheNovember6Ruling_AStolenCreatureSacrificed_IsRemoval(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Removal));
        Assert.True(result.HasFlag(CardEffect.Steal));
    }

    [Theory]
    // Read 2026-11-10: 'exile target creature or Equipment card ... from your
    // graveyard' is a card already dead, as 'target creature card' (Nahiri, the
    // Unforgiving, Removal never tagged by hand; AnswersNobody).
    [InlineData("Nahiri, the Unforgiving", "Legendary Planeswalker — Nahiri",
        "Compleated ({R/W/P} can be paid with {R}, {W}, or 2 life. If life was paid, this planeswalker enters "
        + "with two fewer loyalty counters.)\n+1: Until your next turn, up to one target creature attacks a "
        + "player each combat if able.\n+1: Discard a card, then draw a card.\n0: Exile target creature or "
        + "Equipment card with mana value less than Nahiri's loyalty from your graveyard. Create a token that's "
        + "a copy of it. That token gains haste. Exile it at the beginning of the next end step.")]
    public void Classify_TheNovember10Reading_ACreatureOrOtherCardInAGraveyard_IsNotRemoval(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Removal));
    }
}
