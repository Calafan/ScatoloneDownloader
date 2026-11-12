using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Mtg;

namespace ScatoloneDownloader.Tests.Mtg;

public sealed partial class EffectClassifierTests
{
    [Theory]
    // A +1/+1 counter is Buff in the other vocabulary, ruled 2026-09-15, and so
    // are support and distribute. "One counter on one creature counts" was
    // OVERTURNED for one-shots on 2026-09-25 — Cloudbound Moogle moved to the
    // size-and-purpose tests below — so a counter every combat on a
    // noncreature stands here instead.
    [InlineData("Innkeeper's Talent", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nAt the beginning of combat on your turn, put a "
        + "+1/+1 counter on target creature you control.\n{G}: Level 2\nPermanents you control with counters on "
        + "them have ward {1}.\n{3}{G}: Level 3\nIf you would put one or more counters on a permanent or player, "
        + "put twice that many of each of those kinds of counters on that permanent or player instead.")]
    [InlineData("Blitzball Stadium", "Artifact",
        "When this artifact enters, support X. (Put a +1/+1 counter on each of up to X target creatures.)")]
    [InlineData("Cloudspire Skycycle", "Artifact — Vehicle",
        "Flying\nWhen this Vehicle enters, distribute two +1/+1 counters among one or two other target Vehicles and/or creatures you control.")]
    public void Classify_ACounterOnSomebodyElse_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump restricted to one creature TYPE is not Buff, ruled 2026-09-15: it
    // promises a payoff a cube with no Minotaur deck cannot collect.
    [InlineData("Anaba Spirit Crafter", "Creature — Minotaur Shaman", "Minotaur creatures get +1/+0.")]
    [InlineData("Muscle Sliver", "Creature — Sliver", "All Sliver creatures get +1/+1.")]
    [InlineData("Zuberi, Golden Feather", "Legendary Creature — Griffin",
        "Flying\nOther Griffin creatures get +1/+1.")]
    [InlineData("Heart Wolf", "Legendary Creature — Wolf",
        "First strike\n{T}: Target Dwarf creature gets +2/+0 and gains first strike until end of turn.")]
    [InlineData("Doctor Octopus, Master Planner", "Legendary Creature — Human Villain",
        "Other Villains you control get +2/+2.\nYour maximum hand size is eight.")]
    public void Classify_APumpForOneTribe_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump for the creatures that share the card's NAME goes to its own copies,
    // which is the card pumping itself: the human took Buff off Gruff Triplets on
    // 2026-09-29.
    [InlineData("Gruff Triplets", "Creature — Satyr Warrior",
        "Trample\nWhen this creature enters, if it isn't a token, create two tokens that are copies of "
        + "it.\nWhen this creature dies, put a number of +1/+1 counters equal to its power on each creature you "
        + "control named Gruff Triplets.")]
    [InlineData("Charmed Stray", "Creature — Cat",
        "Lifelink\nWhen this creature enters, put a +1/+1 counter on each other creature you control named "
        + "Charmed Stray.")]
    public void Classify_APumpForItsOwnCopies_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump sized by X or by a count is still a pump; the rules only read
    // digits. Added 2026-09-18 INSIDE the beneficiary guard, which is the whole
    // point — see the next test.
    [InlineData("Berserk", "Instant",
        "Cast this spell only before the combat damage step.\nTarget creature gains trample and gets +X/+0 until end of turn, where X is its power.")]
    [InlineData("Soulshriek", "Instant",
        "Target creature you control gets +X/+0 until end of turn, where X is the number of creature cards in your graveyard.")]
    [InlineData("Frontline Rush", "Sorcery",
        "Choose one —\n• Create two 1/1 red Goblin creature tokens.\n• Target creature gets +X/+X until end of turn, where X is the number of creatures you control.")]
    public void Classify_APumpSizedByACount_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Buff acts on power and/or toughness, ruled 2026-09-18 — and DOUBLE STRIKE
    // is the one keyword that does, because doubling the damage is doubling the
    // power by another name.
    [InlineData("Dual-Sun Technique", "Instant",
        "Target creature you control gains double strike until end of turn. If it has a +1/+1 counter on it, draw a card.")]
    [InlineData("Genji Glove", "Artifact — Equipment",
        "Equipped creature has double strike.\nEquip {3}")]
    public void Classify_GrantingDoubleStrike_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Every other keyword is not. None of them changes what the numbers are, and
    // the hand-tagging says so 49 cards to 5 — Jump and Cloak of Feathers are the
    // same sentence, and only one of them used to be tagged.
    [InlineData("Jump", "Instant", "Target creature gains flying until end of turn.")]
    [InlineData("Toxin Analysis", "Instant",
        "Target creature gains deathtouch and lifelink until end of turn. Investigate.")]
    [InlineData("Flying Carpet", "Artifact",
        "{2}, {T}: Target creature gains flying until end of turn.")]
    // First strike hits first; it does not hit harder.
    [InlineData("Fyndhorn Bow", "Artifact",
        "{3}, {T}: Target creature gains first strike until end of turn.")]
    public void Classify_GrantingAnyOtherKeyword_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Half the cards that scaling reaches pump only THEMSELVES, and the same
    // guard that handles "+2/+2" has to handle these.
    [InlineData("Rabid Wombat", "Creature — Beast",
        "Vigilance\nThis creature gets +2/+2 for each Aura attached to it.")]
    [InlineData("Guidelight Synergist", "Creature — Bird Artificer",
        "Flying\nThis creature gets +1/+0 for each artifact you control.")]
    public void Classify_AScalingSelfPump_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The ACTIVATION COST was being read as the beneficiary: the creature you tap
    // to pay is not the creature being pumped. Found 2026-09-17.
    [InlineData("Llanowar Behemoth", "Creature — Elemental",
        "Tap an untapped creature you control: This creature gets +1/+1 until end of turn.")]
    [InlineData("Karplusan Giant", "Creature — Giant",
        "Tap an untapped snow land you control: This creature gets +1/+1 until end of turn.")]
    // And a condition in the same sentence does the same thing.
    [InlineData("Comet Crawler", "Creature — Insect",
        "Lifelink\nWhenever this creature attacks, you may sacrifice another creature or artifact. If you do, this creature gets +2/+2 until end of turn.")]
    // A pump the card hands to a token it creates belongs to the token.
    [InlineData("Chocobo Racetrack", "Land",
        "Landfall — Whenever a land you control enters, create a 2/2 green Bird creature token with \"Whenever a land you control enters, this creature gets +1/+1 until end of turn.\"")]
    // Lords the type rule could not see: no "creatures" word, or behind a cost.
    [InlineData("Lord of Atlantis", "Creature — Merfolk",
        "Other Merfolk get +1/+1 and have islandwalk.")]
    [InlineData("Adeliz, the Cinder Wind", "Legendary Creature — Efreet Wizard",
        "Flying, haste\nWhenever you cast an instant or sorcery spell, Wizards you control get +1/+1 until end of turn.")]
    [InlineData("Faerie Noble", "Creature — Faerie Lord",
        "Flying\nOther Faerie creatures you control get +0/+1.\n{T}: Other Faerie creatures get +2/+0 until end of turn.")]
    public void Classify_APumpThatLandsSomewhereElse_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    public void Classify_ALandHandedAQuotedPump_IsBuff()
    {
        // Overturned twice. This was the exception to the quotes rule, then on
        // 2026-09-21 it pinned the opposite: what a granted ability does belongs
        // to whatever received it. Ruled back on 2026-10-16 for an ENCHANTED
        // LAND only ("Buff a entrambe", with Mystic Might): a land is no creature
        // to own the pump, so the Aura is what hands it out.
        CardEffect result = EffectClassifier.Classify(MakeCard("Forbidden Lore", "Enchantment — Aura",
            "Enchant land\nEnchanted land has \"{T}: Target creature gets +2/+1 until end of turn.\""));

        Assert.True(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Ruled 2026-09-21, four families at once. A pump inside PARENTHESES is a
    // keyword's reminder text and does nothing; an Aura that pumps the creature
    // it just took is paying for it; a pump bundled with a shrink or a fight is
    // a rider on the answer; and a lord is not this tag however the tribe is
    // named — listed, or named as a card. (A CHOSEN type was one of them until
    // 2026-10-30: Patchwork Banner moved to the theory after this one.)
    [InlineData("Gabriel Angelfire", "Legendary Creature — Angel",
        "At the beginning of your upkeep, choose flying, first strike, trample, or rampage 3. Gabriel "
        + "Angelfire gains that ability until your next upkeep. (Whenever a creature with rampage 3 "
        + "becomes blocked, it gets +3/+3 until end of turn for each creature blocking it beyond the first.)")]
    [InlineData("Binding Grasp", "Enchantment — Aura",
        "Enchant creature\nAt the beginning of your upkeep, sacrifice this Aura unless you pay {1}{U}.\n"
        + "You control enchanted creature.\nEnchanted creature gets +0/+1.")]
    [InlineData("Gurmag Rakshasa", "Creature — Cat Demon",
        "Menace\nWhen this creature enters, target creature an opponent controls gets -2/-2 until end of "
        + "turn and target creature you control gets +2/+2 until end of turn.")]
    [InlineData("The Swarmweaver", "Legendary Creature — Spider",
        "When The Swarmweaver enters, create two 1/1 black and green Insect creature tokens with flying.\n"
        + "Delirium — As long as there are four or more card types among cards in your graveyard, Insects "
        + "and Spiders you control get +1/+1 and have deathtouch.")]
    public void Classify_APumpThatBelongsToSomethingElse_IsNotBuff(
        string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A type CHOSEN as the card enters is no tribe in any tag, ruled 2026-10-30
    // ("Se scegli tu il tipo quando gioco la carta è diverso da un tribale
    // fisso"): its anthem is Buff like any other. Patchwork Banner pinned the
    // opposite from 2026-09-21.
    [InlineData("Patchwork Banner", "Artifact",
        "As this artifact enters, choose a creature type.\nCreatures you control of the chosen type get "
        + "+1/+1.\n{T}: Add one mana of any color.")]
    [InlineData("Banner of Kinship", "Artifact",
        "As this artifact enters, choose a creature type. This artifact enters with a fellowship counter on "
        + "it for each creature you control of the chosen type.\nCreatures you control of the chosen type get "
        + "+1/+1 for each fellowship counter on this artifact.")]
    public void Classify_AnAnthemOfAChosenType_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // And the two vocabularies the tag was blind to, both ruled the same day.
    // Setting a base power and toughness raises it just as an addition does, and
    // DOUBLING is multiplication landing in the same place.
    [InlineData("Wrecking Ball Arm", "Artifact — Equipment",
        "Equipped creature has base power and toughness 7/7 and can't be blocked by creatures with power "
        + "2 or less.\nEquip legendary creature {3}\nEquip {7}")]
    [InlineData("Unruly Krasis", "Creature — Fish Beast",
        "Trample\nWhenever this creature attacks, you may have the base power and toughness of another "
        + "target creature you control become X/X until end of turn, where X is the number of creatures "
        + "you control.")]
    [InlineData("Bulk Up", "Instant", "Double target creature's power until end of turn.")]
    [InlineData("Double Trouble", "Sorcery", "Double the power of each creature you control until end of turn.")]
    public void Classify_ABaseOrADoubling_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    public void Classify_ATriggerThatPumpsTheTeam_StaysBuff()
    {
        // The cost-cutting must not swallow this: "this creature" is only the
        // trigger's subject, and the pump lands on the whole team.
        CardEffect result = EffectClassifier.Classify(MakeCard("Dauntless Veteran", "Creature — Human Soldier",
            "Whenever this creature attacks, creatures you control get +1/+1 until end of turn."));

        Assert.True(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // What "tribe" does NOT mean. A colour is not a tribe (Crusade, Bad Moon), a
    // STATE is not a tribe (Castle, Weakstone), and an unrestricted anthem is the
    // thing the tag is for.
    [InlineData("Crusade", "Enchantment", "White creatures get +1/+1.")]
    [InlineData("Bad Moon", "Enchantment", "Black creatures get +1/+1.")]
    [InlineData("Castle", "Enchantment", "Untapped creatures you control get +0/+2.")]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.")]
    public void Classify_AColourOrAStateIsNotATribe(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    public void Classify_ALordThatAlsoPumpsSomethingUnrestricted_StaysBuff()
    {
        // The exclusion asks whether EVERY pump is tribal, so a card that both
        // lords and pumps freely keeps the tag.
        CardEffect result = EffectClassifier.Classify(MakeCard("Field Marshal", "Creature — Human Soldier",
            "First strike\nOther Soldier creatures get +1/+1 and have first strike.\n{2}{W}: Target creature gets +3/+3 until end of turn."));

        Assert.True(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The two it still has to keep out: a counter the card puts on ITSELF is a
    // stat line, and a counter on THEIR creatures helps them.
    [InlineData("Aku Djinn", "Creature — Djinn",
        "Trample\nAt the beginning of your upkeep, put a +1/+1 counter on each creature each opponent controls.")]
    [InlineData("Aerith Gainsborough", "Legendary Creature — Human",
        "Lifelink\nWhenever you gain life, put a +1/+1 counter on Aerith Gainsborough.")]
    public void Classify_ACounterOnItselfOrOnThem_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A SMALL pump that is not what the card is for, ruled 2026-09-25.
    // B1 — one-shot at sorcery speed: "1 segnalino o un +1/+1 a sorcery non fa
    // niente". This was Buff under the 2026-09-15 counter ruling.
    [InlineData("Cloudbound Moogle", "Creature — Moogle",
        "Flying\nWhen this creature enters, put a +1/+1 counter on target creature.")]
    [InlineData("Keen-Eyed Raven", "Creature — Bird",
        "Flying (This creature can't be blocked except by creatures with flying or reach.)\nWhen this creature "
        + "enters, put a +1/+1 counter on another target creature you control.")]
    // Honor stood here until the same day's re-review overturned it: a spell
    // that is ONLY the pump is Buff. See Classify_APumpRuledIn_IsBuff.
    // B1 now covers a sorcery that does something else as well.
    [InlineData("Aggressive Negotiations", "Sorcery",
        "Target opponent reveals their hand. You choose a nonland card from it and exile that card. Put a "
        + "+1/+1 counter on up to one target creature you control.")]
    // …and from the graveyard, which runs once because the card exiles itself.
    [InlineData("Wither and Bloom", "Instant",
        "Target creature gets -3/-3 until end of turn.\n{1}{B}, Exile this card from your graveyard: Put a +1/+1 "
        + "counter on target creature you control. Activate only as a sorcery.")]
    // B2 — at instant speed, beside something the card is really for:
    // Protection with a rider.
    [InlineData("Magic Damper", "Instant",
        "Target creature you control gets +1/+1 and gains hexproof until end of turn. Untap it.")]
    [InlineData("Lightfoot Technique", "Instant",
        "Put a +1/+1 counter on target creature. It gains flying and indestructible until end of turn. (Damage "
        + "and effects that say \"destroy\" don't destroy it.)")]
    // B3 — repeated, but a small extra on a CREATURE judged as a whole, and
    // since 2026-10-14 only when it wears off: a counter stays Buff.
    [InlineData("Infantry Veteran", "Creature — Human Soldier",
        "{T}: Target attacking creature gets +1/+1 until end of turn.")]
    // A BITE is Removal alone, whatever the size: the pump aims it.
    [InlineData("Felling Blow", "Sorcery",
        "Put a +1/+1 counter on target creature you control. Then that creature deals damage equal to its power "
        + "to target creature an opponent controls.")]
    [InlineData("Bite Down on Crime", "Sorcery",
        "As an additional cost to cast this spell, you may collect evidence 6. This spell costs {2} less to cast "
        + "if evidence was collected. (To collect evidence 6, exile cards with total mana value 6 or greater from "
        + "your graveyard.)\nTarget creature you control gets +2/+0 until end of turn. It deals damage equal to its "
        + "power to target creature you don't control.")]
    public void Classify_ASmallPumpBesideThePoint_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A pump on ITSELF, refused since 2026-09-18, in the three wordings the
    // earlier gate could not see — found in the 2026-09-25 pass.
    // "It gets", when the card is what the sentence is about.
    [InlineData("Slimy Piper", "Creature — Fungus Bard",
        "Whenever this creature attacks, it gets +1/+1 until end of turn. If you control four or more creatures, "
        + "it gets +2/+2 and gains indestructible until end of turn instead. (Damage and effects that say "
        + "\"destroy\" don't destroy it.)")]
    // Counters doubled on "this creature".
    [InlineData("Mossborn Hydra", "Creature — Elemental Hydra",
        "Trample (This creature can deal excess combat damage to the player or planeswalker it's attacking.)\n"
        + "This creature enters with a +1/+1 counter on it.\nLandfall — Whenever a land you control enters, double "
        + "the number of +1/+1 counters on this creature.")]
    // The card's own first name.
    [InlineData("Rashka the Slayer", "Legendary Creature — Human Archer",
        "Reach (This creature can block creatures with flying.)\nWhenever Rashka blocks one or more black "
        + "creatures, Rashka gets +1/+2 until end of turn.")]
    // "Equipped" is the CONDITION, and the card is still the one that gets it.
    [InlineData("Leonin Den-Guard", "Creature — Cat Soldier",
        "As long as this creature is equipped, it gets +1/+1 and has vigilance.")]
    // A condition that names creatures you control is not who gets the pump.
    [InlineData("Hundred-Battle Veteran", "Creature — Zombie Warrior",
        "As long as there are three or more different kinds of counters among creatures you control, this "
        + "creature gets +2/+4.\nYou may cast this card from your graveyard. If you do, it enters with a finality "
        + "counter on it. (If a creature with a finality counter on it would die, exile it instead.)")]
    public void Classify_APumpOnItselfInAnotherWording_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // …and the same pronoun about SOMEBODY ELSE keeps the tag.
    [InlineData("Team Avatar", "Enchantment",
        "Whenever a creature you control attacks alone, it gets +X/+X until end of turn, where X is the number of "
        + "creatures you control.\n{2}{W}, Discard this card: It deals damage equal to the number of creatures you "
        + "control to target creature.")]
    [InlineData("Bestial Fury", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, draw a card at the beginning of the next turn's upkeep.\nWhenever "
        + "enchanted creature becomes blocked, it gets +4/+0 and gains trample until end of turn.")]
    // "It" is the TARGET, even though the card names itself in the sentence.
    [InlineData("Growth Cycle", "Instant",
        "Target creature gets +3/+3 until end of turn. It gets an additional +2/+2 until end of turn for each card "
        + "named Growth Cycle in your graveyard.")]
    // One counter on entry is B1, but DOUBLING them on every attack is a
    // repeated, big pump, and keeps it.
    [InlineData("Seismic Tutelage", "Enchantment — Aura",
        "Enchant creature\nWhen this Aura enters, put a +1/+1 counter on enchanted creature.\nWhenever enchanted "
        + "creature attacks, double the number of +1/+1 counters on it.")]
    public void Classify_ItGetsAboutSomebodyElse_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // …and the pumps that keep it. B2's other half: the pump IS the card.
    [InlineData("Gift of the Viper", "Instant",
        "Put a +1/+1 counter, a reach counter, and a deathtouch counter on target creature. Untap it.")]
    [InlineData("Guided Strike", "Instant",
        "Target creature gets +1/+0 and gains first strike until end of turn.\nDraw a card.")]
    // A scry beside it is a rider, not a second purpose.
    [InlineData("Storm Strike", "Instant",
        "Target creature gets +1/+0 and gains first strike until end of turn. Scry 1.")]
    // Repeated on a NONCREATURE: the pump is the card.
    [InlineData("Firebreathing", "Enchantment — Aura", "Enchant creature\n{R}: Enchanted creature gets +1/+0 until end of turn.")]
    // A modal bullet with its own trigger repeats, whatever its head says.
    [InlineData("Hollowmurk Siege", "Enchantment",
        "As this enchantment enters, choose Sultai or Abzan.\n• Sultai — Whenever a counter is put on a creature "
        + "you control, draw a card. This ability triggers only once each turn.\n• Abzan — Whenever you attack, "
        + "put a +1/+1 counter on target attacking creature. It gains menace until end of turn.")]
    // Bigger than +1/+1, once, on a creature entering.
    [InlineData("Friendly Ghost", "Creature — Spirit",
        "Flying\nWhen this creature enters, target creature gets +2/+4 until end of turn.")]
    // Repeated AND big on a creature.
    [InlineData("Ashroot Animist", "Creature — Lizard Druid",
        "Trample\nWhenever this creature attacks, another target creature you control gains trample and gets "
        + "+X/+X until end of turn, where X is this creature's power.")]
    // A double strike grant is not judged by size at all (ruled 2026-09-18).
    [InlineData("Origin of Spider-Man", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Create a 2/1 "
        + "green Spider creature token with reach.\nII — Put a +1/+1 counter on target creature you control. It "
        + "becomes a legendary Spider Hero in addition to its other types.\nIII — Target creature you control gains "
        + "double strike until end of turn.")]
    public void Classify_APumpThatIsThePoint_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Three cards the human ruled by name on 2026-09-25, each for a reason the
    // size rule does not cover. A STATE in front of a tribe is still a tribe.
    [InlineData("Gornog, the Red Reaper", "Legendary Creature — Minotaur Warrior",
        "Haste\nCowards can't block Warriors.\nWhenever one or more Warriors you control attack a player, target "
        + "creature that player controls becomes a Coward.\nAttacking Warriors you control get +X/+0, where X is the "
        + "number of Cowards your opponents control.")]
    // Counters that ARE the body: on the creatures the sentence just made…
    [InlineData("Valgavoth's Onslaught", "Sorcery",
        "Manifest dread X times, then put X +1/+1 counters on each of those creatures. (To manifest dread, look at "
        + "the top two cards of your library, then put one onto the battlefield face down as a 2/2 creature and the "
        + "other into your graveyard. Turn it face up any time for its mana cost if it's a creature card.)")]
    // …and on a noncreature that becomes a 0/0.
    [InlineData("Case of the Filched Falcon", "Enchantment — Case",
        "When this Case enters, investigate. (Create a Clue token. It's an artifact with \"{2}, Sacrifice this "
        + "token: Draw a card.\")\nTo solve — You control three or more artifacts. (If unsolved, solve at the "
        + "beginning of your end step.)\nSolved — {2}{U}, Sacrifice this Case: Put four +1/+1 counters on target "
        + "noncreature artifact. It becomes a 0/0 Bird creature with flying in addition to its other types.")]
    // …and on a land the next sentence turns into a creature (2026-09-25).
    [InlineData("Rootwise Survivor", "Creature — Human Survivor",
        "Haste\nSurvival — At the beginning of your second main phase, if this creature is tapped, put three "
        + "+1/+1 counters on up to one target land you control. That land becomes a 0/0 Elemental creature in "
        + "addition to its other types. It gains haste until your next turn.")]
    // A tribe in the other vocabularies, ruled on the 2024 re-review of
    // 2026-09-25: counters on each of a tribe…
    [InlineData("Camellia, the Seedmiser", "Legendary Creature — Squirrel Warlock",
        "Menace\nOther Squirrels you control have menace.\nWhenever you sacrifice one or more Foods, create a "
        + "1/1 green Squirrel creature token.\n{2}, Forage: Put a +1/+1 counter on each other Squirrel you "
        + "control. (To forage, exile three cards from your graveyard or sacrifice a Food.)")]
    [InlineData("Kastral, the Windcrested", "Legendary Creature — Bird Scout",
        "Flying\nWhenever one or more Birds you control deal combat damage to a player, choose one —\n• You may "
        + "put a Bird creature card from your hand or graveyard onto the battlefield with a finality counter on "
        + "it.\n• Put a +1/+1 counter on each Bird you control.\n• Draw a card.")]
    // …a counter the tribe enters with…
    [InlineData("Slinza, the Spiked Stampede", "Legendary Creature — Beast",
        "Beast spells you cast cost {2} less to cast.\nEach other Beast creature you control enters with an "
        + "additional +1/+1 counter on it.\nWhenever Slinza or another creature with power 4 or greater enters, "
        + "you may pay {1}{R/G}. When you do, Slinza fights target creature you don't control.")]
    // …a target that has to belong to the tribe, one type or a list…
    [InlineData("Inside Source", "Creature — Human Citizen",
        "When this creature enters, create a 2/2 white and blue Detective creature token.\n{3}, {T}: Target "
        + "Detective you control gets +2/+0 and gains vigilance until end of turn. Activate only as a sorcery.")]
    [InlineData("Rockface Village", "Land",
        "{T}: Add {C}.\n{T}: Add {R}. Spend this mana only to cast a creature spell.\n{R}, {T}: Target Lizard, "
        + "Mouse, Otter, or Raccoon you control gets +1/+0 and gains haste until end of turn. Activate only as "
        + "a sorcery.")]
    // …and TOKENS, a tribe by another name, in both vocabularies.
    [InlineData("Hildibrand Manderville // Gentleman's Rise", "Legendary Creature — Human Detective // Instant — Adventure",
        "Creature tokens you control get +1/+1.\nWhen Hildibrand Manderville dies, you may cast it from your "
        + "graveyard as an Adventure until the end of your next turn.\nCreate a 2/2 black Zombie creature token. "
        + "(Then exile this card. You may cast the creature later from exile.)")]
    [InlineData("Sandstorm Salvager", "Creature — Human Artificer",
        "When this creature enters, create a 3/3 colorless Golem artifact creature token.\n{2}, {T}: Put a "
        + "+1/+1 counter on each creature token you control. They gain trample until end of turn.")]
    public void Classify_ATribeOrABodyBeingMade_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Ruled Buff by name on 2026-09-25: a big pump that repeats…
    [InlineData("Hurska Sweet-Tooth", "Legendary Creature — Bear",
        "Whenever Hurska attacks, create a Food token. (It's an artifact with \"{2}, {T}, Sacrifice this token: "
        + "You gain 3 life.\")\nWhenever you gain life, you may pay {G/W}. When you do, target creature gets +X/+X "
        + "until end of turn, where X is the amount of life you gained.")]
    // …and a big one at instant speed, whatever it costs afterwards.
    [InlineData("Soulshriek", "Instant",
        "Target creature you control gets +X/+0 until end of turn, where X is the number of creature cards in your "
        + "graveyard. Sacrifice that creature at the beginning of the next end step.")]
    // "Those creatures" that were TARGETED, not made, are still pumped.
    [InlineData("Biogenic Upgrade", "Sorcery",
        "Distribute three +1/+1 counters among one, two, or three target creatures, then double the number of +1/+1 "
        + "counters on each of those creatures.")]
    public void Classify_ABigPumpTheHumanNamed_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Fact]
    // Several targets and a shield: Buff and Protection, ruled 2026-09-25. Moved
    // 2026-11-10 (Protection.md, Q1): the shield's recipient is chosen at sorcery
    // speed, so it answers nothing and the card is Buff alone.
    public void Classify_EnduranceBobblehead_IsBuffNotProtection()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Endurance Bobblehead", "Artifact — Bobblehead",
            "{T}: Add one mana of any color.\n{3}, {T}: Up to X target creatures you control get +1/+0 and gain "
            + "indestructible until end of turn, where X is the number of Bobbleheads you control as you activate "
            + "this ability. Activate only as a sorcery."));

        Assert.True(result.HasFlag(CardEffect.Buff));
        Assert.False(result.HasFlag(CardEffect.Protection));
    }

    [Fact]
    // +5/+5 AND a shield: both tags, named by the human on 2026-09-25.
    public void Classify_StonewoodInvocation_IsBuffAndProtection()
    {
        CardEffect result = EffectClassifier.Classify(MakeCard("Stonewood Invocation", "Instant",
            "Split second (As long as this spell is on the stack, players can't cast spells or activate abilities "
            + "that aren't mana abilities.)\nTarget creature gets +5/+5 and gains shroud until end of turn. (It can't "
            + "be the target of spells or abilities.)"));

        Assert.True(result.HasFlag(CardEffect.Buff));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Buff the human tagged on the 2024 re-review of 2026-09-25 and the
    // classifier could not read. Double strike outside the quotes that pump a
    // token:
    [InlineData("Blacksmith's Talent", "Enchantment — Class",
        "(Gain the next level as a sorcery to add its ability.)\nWhen this Class enters, create a colorless "
        + "Equipment artifact token named Sword with \"Equipped creature gets +1/+1\" and equip {2}.\n{2}{R}: "
        + "Level 2\nAt the beginning of combat on your turn, attach target Equipment you control to up to one "
        + "target creature you control.\n{3}{R}: Level 3\nDuring your turn, equipped creatures you control have "
        + "double strike and haste.")]
    // The creature just CAST entering with counters, which is not the card:
    [InlineData("Communal Brewing", "Enchantment",
        "When this enchantment enters, any number of target opponents each draw a card. Put an ingredient "
        + "counter on this enchantment, then put an ingredient counter on it for each card drawn this "
        + "way.\nWhenever you cast a creature spell, that creature enters with X additional +1/+1 counters on "
        + "it, where X is the number of ingredient counters on this enchantment.")]
    // Scavenge handed to a whole graveyard:
    [InlineData("Young Deathclaws", "Creature — Lizard Mutant",
        "Menace (This creature can't be blocked except by two or more creatures.)\nEach creature card in your "
        + "graveyard has scavenge. The scavenge cost is equal to its mana cost. (Exile a creature card from "
        + "your graveyard and pay its mana cost: Put a number of +1/+1 counters equal to that card's power on "
        + "target creature. Scavenge only as a sorcery.)")]
    // A counter "on a creature you control", in a mode bought up to five times:
    [InlineData("Season of Gathering", "Sorcery",
        "Choose up to five {P} worth of modes. You may choose the same mode more than once.\n{P} — Put a +1/+1 "
        + "counter on a creature you control. It gains vigilance and trample until end of turn.\n{P}{P} — Choose "
        + "artifact or enchantment. Destroy all permanents of the chosen type.\n{P}{P}{P} — Draw cards equal to "
        + "the greatest power among creatures you control.")]
    // A creature that grows another with a counter, again and again — the B3
    // exception, widened 2026-10-14 from connecting to any repeated counter:
    [InlineData("Loxodon Battle Priest", "Creature — Elephant Cleric",
        "At the beginning of combat on your turn, put a +1/+1 counter on another target creature you control.")]
    [InlineData("Prowler, Misguided Mentor", "Legendary Creature — Human Rogue Villain",
        "Prowler can't be blocked by creatures with power 2 or less.\nWhenever Prowler deals combat damage to "
        + "a player, put a +1/+1 counter on another target creature you control.")]
    [InlineData("Scurry of Squirrels", "Creature — Squirrel Scout",
        "Myriad, myriad (Whenever this creature attacks, for each opponent other than defending player, you "
        + "may create a token that's a copy of this creature that's tapped and attacking that player or a "
        + "planeswalker they control. Then do it again. Exile the tokens at end of combat.)\nWhenever this "
        + "creature deals combat damage to a player, put a +1/+1 counter on target creature you control.")]
    // And two sizes the reading got wrong: a count behind "until end of turn",
    // and counters spread over a third target.
    [InlineData("Hunger of the Nim", "Sorcery",
        "Target creature gets +1/+0 until end of turn for each artifact you control.")]
    [InlineData("Incremental Growth", "Sorcery",
        "Put a +1/+1 counter on target creature, two +1/+1 counters on another target creature, and three "
        + "+1/+1 counters on a third target creature.")]
    public void Classify_APumpTheReadingMissed_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A new base on the creature an Aura stole is the contour of the theft.
    [InlineData("Coerced to Kill", "Enchantment — Aura",
        "Enchant creature\nYou control enchanted creature.\nEnchanted creature has base power and toughness "
        + "1/1, has deathtouch, and is an Assassin in addition to its other types.")]
    // "That creature" is the one reanimated here, not one being cast.
    [InlineData("Necromantic Summons", "Sorcery",
        "Put target creature card from a graveyard onto the battlefield under your control.\nSpell mastery — "
        + "If there are two or more instant and/or sorcery cards in your graveyard, that creature enters with "
        + "two additional +1/+1 counters on it.")]
    public void Classify_APumpThatIsNotThePoint_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Four rulings of 2026-09-25, Buff side. A spell that is ONLY the pump is
    // Buff however small, at sorcery speed too — "anche se fa schifo".
    [InlineData("Honor", "Sorcery",
        "Put a +1/+1 counter on target creature.\nDraw a card.")]
    // The raised bar is for COUNTERS a creature hands out as it enters: a
    // temporary pump on entry is a trick and still counts…
    [InlineData("Toucan-Puffin", "Creature — Bird",
        "Flying\nWhen this creature enters, target creature you control gets +2/+0 until end of turn.")]
    // …and so do counters that come from the graveyard rather than the entry.
    [InlineData("Agent of Kotis", "Creature — Human Rogue",
        "Renew — {3}{U}, Exile this card from your graveyard: Put two +1/+1 counters on target creature. "
        + "Activate only as a sorcery.")]
    // A sorcery-speed COUNTER is not the irrelevant kind: it stays.
    [InlineData("Perilous Snare", "Artifact",
        "Start your engines! (If you have no speed, it starts at 1. It increases once on each of your turns "
        + "when an opponent loses life. Max speed is 4.)\nWhen this artifact enters, exile target nonland "
        + "permanent an opponent controls until this artifact leaves the battlefield.\nMax speed — {T}: Put a "
        + "+1/+1 counter on target creature or Vehicle you control. Activate only as a sorcery.")]
    public void Classify_APumpRuledIn_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Apothecary Stomper's two counters on entry ("come se fosse un 6/6") stood
    // here until 2026-11-13, when "Basta 2 per entrambi" overturned the bar of
    // three. See Classify_TheNovember13BuffPass_IsBuff.
    // "+1/+1 a velocità sorcery è irrilevante come effetto. La carta fa altro."
    [InlineData("Starnheim Memento", "Artifact",
        "{T}: Add {W}.\n{1}{W}, {T}: Target creature gets +1/+1 and gains flying until end of turn. Activate "
        + "only as a sorcery.")]
    // "Colorless" counts as a tribe, as a lord and as counters.
    [InlineData("Kozilek, the Broken Reality", "Legendary Creature — Eldrazi",
        "When you cast this spell, up to two target players each manifest two cards from their hands. For "
        + "each card manifested this way, you draw a card. (To manifest a card, put it onto the battlefield "
        + "face down as a 2/2 creature. Turn it face up any time for its mana cost if it's a creature "
        + "card.)\nOther colorless creatures you control get +3/+2.")]
    [InlineData("It That Heralds the End", "Creature — Eldrazi Drone",
        "Colorless spells you cast with mana value 7 or greater cost {1} less to cast.\nOther colorless "
        + "creatures you control get +1/+1.")]
    [InlineData("Titans' Vanguard", "Creature — Eldrazi",
        "Devoid (This card has no color.)\nWhen you cast this spell and whenever this creature attacks, put a "
        + "+1/+1 counter on each colorless creature you control.\nTrample")]
    public void Classify_APumpRuledOut_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Buff's misses of October 2026, every one hand-tagged. A ROLE is a pump
    // with no numbers in it.
    [InlineData("Become Brutes", "Sorcery",
        "One or two target creatures each gain haste until end of turn. For each of those creatures, create a "
        + "Monster Role token attached to it. (If you control another Role on it, put that one into the "
        + "graveyard. Enchanted creature gets +1/+1 and has trample.)")]
    // B3 narrowed: a creature handing ANOTHER creature a Role or a counter
    // every turn.
    [InlineData("Gylwain, Casting Director", "Legendary Creature — Human Bard",
        "Whenever Gylwain or another nontoken creature you control enters, choose one —\n• Create a Royal Role "
        + "token attached to that creature.\n• Create a Sorcerer Role token attached to that creature.\n• Create "
        + "a Monster Role token attached to that creature.")]
    [InlineData("Haazda Vigilante", "Creature — Giant Soldier",
        "Whenever this creature enters or attacks, put a +1/+1 counter on target creature you control with "
        + "power 2 or less.")]
    // Mentor's counter is only ever written in its reminder.
    [InlineData("Ordruun Mentor", "Creature — Minotaur Soldier",
        "Mentor (Whenever this creature attacks, put a +1/+1 counter on target attacking creature with lesser "
        + "power.)\nWhenever you attack a player, target creature that's attacking that player gains first "
        + "strike until end of turn.")]
    // Five more ways to write the counter on somebody else.
    [InlineData("Arwen, Weaver of Hope", "Legendary Creature — Elf Noble",
        "Each other creature you control enters with a number of additional +1/+1 counters on it equal to "
        + "Arwen's toughness.")]
    [InlineData("Gluntch, the Bestower", "Legendary Creature — Jellyfish",
        "Flying\nAt the beginning of your end step, choose a player. They put two +1/+1 counters on a creature "
        + "they control. Choose a second player to draw a card. Then choose a third player to create two "
        + "Treasure tokens.")]
    [InlineData("Tribute to the World Tree", "Enchantment",
        "Whenever a creature you control enters, draw a card if its power is 3 or greater. Otherwise, put two "
        + "+1/+1 counters on it.")]
    [InlineData("Jared Carthalion", "Legendary Planeswalker — Jared",
        "+1: Create a 3/3 Kavu creature token with trample that's all colors.\n−3: Choose up to two target "
        + "creatures. For each of them, put a number of +1/+1 counters on it equal to the number of colors it "
        + "is.\n−6: Return target multicolored card from your graveyard to your hand. If that card was all "
        + "colors, draw a card and create two Treasure tokens.\nJared Carthalion can be your commander.")]
    [InlineData("Sandsteppe War Riders", "Creature — Human Warrior",
        "Trample\nAt the beginning of combat on your turn, bolster X, where X is the number of differently "
        + "named artifact tokens you control. (Choose a creature with the least toughness among creatures you "
        + "control and put X +1/+1 counters on it.)")]
    [InlineData("Uncivil Unrest", "Enchantment",
        "Nontoken creatures you control have riot. (They enter with your choice of a +1/+1 counter or haste.)\n"
        + "If a creature you control with a +1/+1 counter on it would deal damage to a permanent or player, it "
        + "deals double that damage instead.")]
    // A counter MOVED off the card onto a creature.
    [InlineData("Costume Closet", "Artifact",
        "This artifact enters with two +1/+1 counters on it.\n{T}: Move a +1/+1 counter from this artifact onto "
        + "target creature you control. Activate only as a sorcery.\nWhenever a modified creature you control "
        + "leaves the battlefield, put a +1/+1 counter on this artifact. (Equipment, Auras you control, and "
        + "counters are modifications.)")]
    // Six chapters are six pumps.
    [InlineData("Long List of the Ents", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after VI.)\nI, II, III, "
        + "IV, V, VI — Note a creature type that hasn't been noted for this Saga. When you next cast a creature "
        + "spell of that type this turn, that creature enters with an additional +1/+1 counter on it.")]
    // The team, then the tribe a little more — in numbers or in counters.
    [InlineData("Take to the Streets", "Sorcery",
        "Creatures you control get +2/+2 until end of turn. Citizens you control get an additional +1/+1 and "
        + "gain vigilance until end of turn.")]
    [InlineData("River Heralds' Boon", "Instant",
        "Put a +1/+1 counter on target creature and a +1/+1 counter on up to one target Merfolk.")]
    // A quoted pump handed to the creature the card is attached to.
    [InlineData("Idolized", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has \"Whenever this creature attacks alone, it gets +X/+X until "
        + "end of turn, where X is the number of nonland permanents you control.\"")]
    [InlineData("Astrologian's Planisphere", "Artifact — Equipment",
        "Job select (When this Equipment enters, create a 1/1 colorless Hero creature token, then attach this "
        + "to it.)\nEquipped creature is a Wizard in addition to its other types and has \"Whenever you cast a "
        + "noncreature spell and whenever you draw your third card each turn, put a +1/+1 counter on this "
        + "creature.\"\nDiana — Equip {2}")]
    [InlineData("Tale of Katara and Toph", "Enchantment",
        "Creatures you control have \"Whenever this creature becomes tapped for the first time during each of "
        + "your turns, put a +1/+1 counter on it.\"")]
    public void Classify_TheOctoberBuffGaps_AreBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A Role made once, as a creature enters, is B1's small one-shot.
    [InlineData("Charmed Clothier", "Creature — Faerie Advisor",
        "Flying\nWhen this creature enters, create a Royal Role token attached to another target creature you "
        + "control. (If you control another Role on it, put that one into the graveyard. Enchanted creature "
        + "gets +1/+1 and has ward {1}.)")]
    // Sandsteppe Scavenger's bolster 2 on entry stood here until 2026-11-13: two
    // counters on entry are enough now. See Classify_TheNovember13BuffPass_IsBuff.
    // "With a +1/+1 counter on it" says who dies, not what is put. The real card
    // also has warp, which makes its counter two (2026-11-13); this text without
    // it pins the condition alone.
    [InlineData("Rayblade Trooper", "Creature — Human Soldier",
        "When this creature enters, put a +1/+1 counter on target creature you control.\nWhenever a nontoken "
        + "creature you control with a +1/+1 counter on it dies, create a 1/1 white Human Soldier creature "
        + "token.")]
    // Six more ways to name a tribe.
    [InlineData("Aunt May", "Legendary Creature — Human Citizen",
        "Whenever another creature you control enters, you gain 1 life. If it's a Spider, put a +1/+1 counter "
        + "on it.")]
    [InlineData("Torgal, A Fine Hound", "Legendary Creature — Wolf",
        "Whenever you cast your first Human creature spell each turn, that creature enters with an additional "
        + "+1/+1 counter on it for each Dog and/or Wolf you control.\n{T}: Add one mana of any color.")]
    [InlineData("Silver-Fur Master", "Creature — Rat Ninja",
        "Ninjutsu {U}{B} ({U}{B}, Return an unblocked attacker you control to hand: Put this card onto the "
        + "battlefield from your hand tapped and attacking.)\nNinjutsu abilities you activate cost {1} less to "
        + "activate.\nOther Ninja and Rogue creatures you control get +1/+1.")]
    [InlineData("Ogre Chitterlord", "Creature — Ogre Warrior",
        "Menace\nWhenever this creature enters or attacks, create two 1/1 black Rat creature tokens with \"This "
        + "token can't block.\" Then if you control five or more Rats, each Rat you control gets +2/+0 until "
        + "end of turn.")]
    [InlineData("Treebeard, Gracious Host", "Legendary Creature — Treefolk",
        "Trample, ward {2}\nWhen Treebeard enters, create two Food tokens.\nWhenever you gain life, put that "
        + "many +1/+1 counters on target Halfling or Treefolk.")]
    [InlineData("The War Games", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after IV.)\nI — Each "
        + "player creates three tapped 1/1 white Warrior creature tokens. The tokens are goaded for as long as "
        + "this Saga remains on the battlefield.\nII, III — Put a +1/+1 counter on each Warrior creature.\nIV — "
        + "You may exile a nontoken creature you control. When you do, exile all Warriors.")]
    // A base with defender is the creature locked away.
    [InlineData("Stasis Field", "Enchantment — Aura",
        "Enchant creature\nEnchanted creature has base power and toughness 0/2, has defender, and loses all "
        + "other abilities.")]
    // A creature's one-shot "for each" is judged by the number it prints.
    [InlineData("Eastfarthing Farmer", "Creature — Halfling Peasant",
        "When this creature enters, create a Food token. When you do, target creature you control gets +1/+1 "
        + "until end of turn for each Food you control. (A Food token is an artifact with \"{2}, {T}, Sacrifice "
        + "this token: You gain 3 life.\")")]
    // The printed Role is the reference card for a token, never cast.
    [InlineData("Monster // Sorcerer", "Token Enchantment — Aura Role // Token Enchantment — Aura Role",
        "Enchant creature\nEnchanted creature gets +1/+1 and has trample.\nEnchant creature\nEnchanted creature "
        + "gets +1/+1 and has \"Whenever this creature attacks, scry 1.\"")]
    public void Classify_TheOctoberBuffMisreadings_AreNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Double strike handed to another creature: named in the sentence before, a
    // mode of the equipped creature's attack, a list of keywords for the team.
    [InlineData("Kayla's Command", "Sorcery",
        "Choose two —\n• Create a 2/2 colorless Construct artifact creature token.\n• Put a +1/+1 counter on a "
        + "creature you control. It gains double strike until end of turn.\n• Search your library for a basic "
        + "Plains card, reveal it, put it into your hand, then shuffle.\n• You gain 2 life and scry 2.")]
    [InlineData("The Spear of Leonidas", "Legendary Artifact — Equipment",
        "Whenever equipped creature attacks, choose one —\n• Bull Rush — It gains double strike until end of "
        + "turn.\n• Summon — Create Phobos, a legendary 3/2 red Horse creature token.\n• Revelation — Discard two "
        + "cards, then draw two cards.\nEquip {2}")]
    [InlineData("Bleeding Effect", "Enchantment",
        "At the beginning of combat on your turn, creatures you control gain flying until end of turn if a "
        + "creature card in your graveyard has flying. The same is true for first strike, double strike, "
        + "deathtouch, hexproof, indestructible, lifelink, menace, reach, trample, and vigilance.")]
    // A counter on the creature a trigger names, or on the one the sentence before
    // targeted.
    [InlineData("Sphere Grid", "Enchantment",
        "Whenever a creature you control deals combat damage to a player, put a +1/+1 counter on that "
        + "creature.\nUnlock Ability — Creatures you control with +1/+1 counters on them have reach and trample.")]
    [InlineData("Experimental Lab // Staff Room", "Enchantment — Room // Enchantment — Room",
        "When you unlock this door, manifest dread, then put two +1/+1 counters and a trample counter on that "
        + "creature.\n(You may cast either half. That door unlocks on the battlefield. As a sorcery, you may pay "
        + "the mana cost of a locked door to unlock it.)\nWhenever a creature you control deals combat damage to "
        + "a player, turn that creature face up or put a +1/+1 counter on it.\n(You may cast either half. That "
        + "door unlocks on the battlefield. As a sorcery, you may pay the mana cost of a locked door to unlock "
        + "it.)")]
    [InlineData("The Aetherspark", "Legendary Artifact Planeswalker — Equipment",
        "As long as The Aetherspark is attached to a creature, The Aetherspark can't be attacked and has "
        + "\"Whenever equipped creature deals combat damage during your turn, put that many loyalty counters on "
        + "The Aetherspark.\"\n+1: Attach The Aetherspark to up to one target creature you control. Put a +1/+1 "
        + "counter on that creature.\n−5: Draw two cards.\n−10: Add ten mana of any one color.")]
    // A repeated counter that comes with a keyword counter is still an engine.
    [InlineData("Guide of Souls", "Creature — Human Cleric",
        "Whenever another creature you control enters, you gain 1 life and get {E} (an energy "
        + "counter).\nWhenever you attack, you may pay {E}{E}{E}. When you do, put two +1/+1 counters and a "
        + "flying counter on target attacking creature. It becomes an Angel in addition to its other types.")]
    // A permanent pump beside a damage shield is the card's pump.
    [InlineData("Caduceus, Staff of Hermes", "Legendary Artifact — Equipment",
        "Equipped creature has lifelink.\nAs long as you have 30 or more life, equipped creature gets +5/+5 "
        + "and has indestructible and \"Prevent all damage that would be dealt to this creature.\"\nEquip {W}{W}")]
    // A +N/-M on your own creature, ruled 2026-10-16.
    [InlineData("Ashnod's Battle Gear", "Artifact",
        "You may choose not to untap this artifact during your untap step.\n{2}, {T}: Target creature you "
        + "control gets +2/-2 for as long as this artifact remains tapped.")]
    // Reinforce, whose counters are written only in its reminder.
    [InlineData("Fowl Strike", "Instant",
        "Destroy target creature with flying.\nReinforce 2—{2}{G} ({2}{G}, Discard this card: Put two +1/+1 "
        + "counters on target creature.)")]
    // A pump alone on its line, beside a shield only a Human gets.
    [InlineData("Flare of Faith", "Instant",
        "Target creature gets +2/+2 until end of turn. If it's a Human, instead it gets +3/+3 and gains "
        + "indestructible until end of turn.")]
    public void Classify_TheMidOctoberBuffGaps_AreBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A one-shot pump said with a shield, on a card that is Protection, ruled
    // 2026-10-16.
    [InlineData("Dark Endurance", "Instant",
        "This spell costs {1} less to cast if it targets a blocking creature.\nTarget creature gets +2/+0 and "
        + "gains indestructible until end of turn. (Damage and effects that say \"destroy\" don't destroy it.)")]
    [InlineData("Fake Your Own Death", "Instant",
        "Until end of turn, target creature gets +2/+0 and gains \"When this creature dies, return it to the "
        + "battlefield tapped under its owner's control and you create a Treasure token.\" (It's an artifact "
        + "with \"{T}, Sacrifice this token: Add one mana of any color.\")")]
    [InlineData("Presumed Dead", "Instant",
        "Until end of turn, target creature gets +2/+0 and gains \"When this creature dies, return it to the "
        + "battlefield under its owner's control and suspect it.\" (A suspected creature has menace and can't "
        + "block.)")]
    // Sacrificed by name, it pumps the team once beside the shield.
    [InlineData("Hajar, Loyal Bodyguard", "Legendary Creature — Human Soldier",
        "Sacrifice Hajar: Legendary creatures you control get +1/+0 and gain indestructible until end of "
        + "turn.")]
    // Otherwise is not other: the counters it doubles are its own.
    [InlineData("Lily Bowen, Raging Grandma", "Legendary Creature — Mutant Warrior",
        "Vigilance\nLily Bowen enters with two +1/+1 counters on it.\nAt the beginning of your upkeep, double "
        + "the number of +1/+1 counters on Lily Bowen if its power is 16 or less. Otherwise, remove all but one "
        + "+1/+1 counter from it, then you gain 1 life for each +1/+1 counter removed this way.")]
    // The tribe as a clause after the noun.
    [InlineData("Skeleton Crew", "Creature — Skeleton Pirate",
        "Each other creature you control that's a Skeleton or Pirate gets +1/+1.\nWhenever one or more "
        + "creature cards leave your graveyard, create a 2/2 black Skeleton Pirate creature token. (This "
        + "ability triggers only from the battlefield.)\n{5}{B}: Return this card from your graveyard to the "
        + "battlefield tapped.")]
    // A one-shot counter that comes with a keyword counter is the keyword handed
    // out.
    [InlineData("Sagu Pummeler", "Creature — Beast",
        "Reach\nRenew — {4}{G}, Exile this card from your graveyard: Put two +1/+1 counters and a reach "
        + "counter on target creature. Activate only as a sorcery.")]
    // The creature an opponent has just been handed.
    [InlineData("Jon Irenicus, Shattered One", "Legendary Creature — Elf Wizard",
        "At the beginning of your end step, target opponent gains control of up to one target creature you "
        + "control. Put two +1/+1 counters on it and tap it. It's goaded for the rest of the game and it gains "
        + "\"This creature can't be sacrificed.\" (It attacks each combat if able and attacks a player other than "
        + "you if able.)\nWhenever a creature you own but don't control attacks, you draw a card.")]
    // The +N/-M an Aura gives is not Buff, ruled 2026-10-16.
    [InlineData("Grave Servitude", "Enchantment — Aura",
        "You may cast this spell as though it had flash. If you cast it any time a sorcery couldn't have been "
        + "cast, the controller of the permanent it becomes sacrifices it at the beginning of the next cleanup "
        + "step.\nEnchant creature\nEnchanted creature gets +3/-1 and is black.")]
    // Exalted and rampage are keywords, never Buff, ruled 2026-10-16.
    [InlineData("Emissary of Soulfire", "Creature — Djinn Monk",
        "When this creature enters, you get {E}{E}{E} (three energy counters).\nPay {E}{E}: Put an exalted "
        + "counter on target creature you control. Activate only as a sorcery. (Whenever a creature you control "
        + "attacks alone, it gets +1/+1 until end of turn for each instance of exalted among permanents you "
        + "control.)")]
    [InlineData("Rapid Fire", "Instant",
        "Cast this spell only before blockers are declared.\nTarget creature gains first strike until end of "
        + "turn. If it doesn't have rampage, that creature gains rampage 2 until end of turn. (Whenever the "
        + "creature becomes blocked, it gets +2/+2 until end of turn for each creature blocking it beyond the "
        + "first.)")]
    // Melee is a keyword, never Buff, ruled 2026-11-05 like exalted and rampage:
    // the team that is handed it (Skyhunter Strike Force, Drogskol Reinforcements
    // for Spirits, Depthshaker Titan beside its Tokens) is untagged.
    [InlineData("Skyhunter Strike Force", "Creature — Cat Knight",
        "Flying\nMelee (Whenever this creature attacks, it gets +1/+1 until end of turn for each opponent you "
        + "attacked this combat.)\nLieutenant — As long as you control your commander, other creatures you "
        + "control have melee.")]
    [InlineData("Drogskol Reinforcements", "Creature — Spirit Soldier",
        "Melee (Whenever this creature attacks, it gets +1/+1 until end of turn for each opponent you "
        + "attacked this combat.)\nOther Spirits you control have melee.\nPrevent all noncombat damage that would "
        + "be dealt to Spirits you control.")]
    [InlineData("Depthshaker Titan", "Artifact Creature — Robot",
        "When this creature enters, any number of target noncreature artifacts you control become 3/3 "
        + "artifact creatures. Sacrifice them at the beginning of the next end step.\nEach artifact creature you "
        + "control has melee, trample, and haste. (Whenever a creature with melee attacks, it gets +1/+1 until "
        + "end of turn for each opponent you attacked this combat.)")]
    public void Classify_TheMidOctoberBuffMisreadings_AreNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Denry Klin puts the same number of each kind of counter on the creature that
    // enters, Buff by hand (2026-10-27, read 10-30).
    [InlineData("Denry Klin, Editor in Chief", "Legendary Creature — Cat Advisor",
        "Denry Klin enters with your choice of a +1/+1, first strike, or vigilance counter on it.\nWhenever a "
        + "nontoken creature you control enters, if Denry Klin has counters on it, put the same number of each "
        + "kind of counter on that creature.")]
    public void Classify_CountersCopiedOntoTheCreatureThatEnters_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Omarthis's "whenever you put one or more +1/+1 counters on another colorless
    // creature" pays Omarthis (0 of 4 reviewed), and Go-Shintai of Boundless
    // Vigor's counter goes on "target Shrine for each Shrine you control", a tribe
    // (0 of 11). Both untagged 2026-10-29.
    [InlineData("Omarthis, Ghostfire Initiate", "Legendary Creature — Spirit Snake",
        "Omarthis enters with X +1/+1 counters on it.\nWhenever you put one or more +1/+1 counters on another "
        + "colorless creature, you may put a +1/+1 counter on Omarthis.\nWhen Omarthis dies, manifest a number "
        + "of cards from the top of your library equal to the number of counters on it.")]
    [InlineData("Go-Shintai of Boundless Vigor", "Legendary Enchantment Creature — Shrine",
        "Trample\nAt the beginning of your end step, you may pay {1}. When you do, put a +1/+1 counter on "
        + "target Shrine for each Shrine you control.")]
    public void Classify_ACounterTheTriggerWaitsForOrATribesCounter_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Return to Action's +1/+0 and lifelink beside a granted return from death is
    // Buff and Protection by hand (2026-10-27), as Scales of Shale's is; the same
    // shield beside a bare pump is Protection alone (Presumed Dead).
    [InlineData("Return to Action", "Instant",
        "Until end of turn, target creature gets +1/+0 and gains lifelink and \"When this creature dies, "
        + "return it to the battlefield tapped under its owner's control.\"")]
    public void Classify_APumpWithLifelinkBesideAShield_IsBuffAndProtection(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.Buff));
        Assert.True(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // Buff pass of 2026-10-27, wordings the rules could not read: a counter on any
    // attacking creature paid for (Anointer of Valor), on the creature chosen the
    // sentence before (Dig Deep, Elspeth Resplendent's +1), on the equipped
    // creature by its own trigger, a mode or anybody's death (Greatsword of Tyr,
    // Lost Jitte; Tarrian's Soulcleaver ruled Buff by name), a new base for one
    // creature of yours that is the whole card (Scale Up), and an instant that is
    // only a +1/+1 and keyword counters (Gift of the Viper, B2's own example).
    [InlineData("Anointer of Valor", "Creature — Angel",
        "Flying\nWhenever a creature attacks, you may pay {3}. When you do, put a +1/+1 counter on that "
        + "creature.")]
    [InlineData("Colossal Badger // Dig Deep", "Creature — Badger // Sorcery — Adventure",
        "When this creature enters, you gain 3 life.\nChoose target creature. Mill four cards, then put a "
        + "+1/+1 counter on that creature for each creature card milled this way.")]
    [InlineData("Elspeth Resplendent", "Legendary Planeswalker — Elspeth",
        "+1: Choose up to one target creature. Put a +1/+1 counter and a counter from among flying, first "
        + "strike, lifelink, or vigilance on it.\n−3: Look at the top seven cards of your library. You may put a "
        + "permanent card with mana value 3 or less from among them onto the battlefield with a shield counter "
        + "on it. Put the rest on the bottom of your library in a random order.\n−7: Create five 3/3 white Angel "
        + "creature tokens with flying.")]
    [InlineData("Greatsword of Tyr", "Artifact — Equipment",
        "Whenever equipped creature attacks, put a +1/+1 counter on it and tap up to one target creature "
        + "defending player controls.\nEquip {W} ({W}: Attach to target creature you control. Equip only as a "
        + "sorcery.)")]
    [InlineData("Lost Jitte", "Legendary Artifact — Equipment",
        "Whenever equipped creature deals combat damage, put a charge counter on Lost Jitte.\nRemove a charge "
        + "counter from Lost Jitte: Choose one —\n• Untap target land.\n• Target creature can't block this "
        + "turn.\n• Put a +1/+1 counter on equipped creature.\nEquip {1}")]
    [InlineData("Tarrian's Soulcleaver", "Legendary Artifact — Equipment",
        "Equipped creature has vigilance.\nWhenever another artifact or creature is put into a graveyard from "
        + "the battlefield, put a +1/+1 counter on equipped creature.\nEquip {2}")]
    [InlineData("Scale Up", "Sorcery",
        "Until end of turn, target creature you control becomes a green Wurm with base power and toughness "
        + "6/4.\nOverload {4}{G}{G} (You may cast this spell for its overload cost. If you do, change \"target\" "
        + "in its text to \"each.\")")]
    public void Classify_TheOctober27BuffPass_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Buff pass of 2026-10-27: a pump beside a return from death OR exile is the
    // shield's rider (Ashnod's Intervention); a tribe with a hyphen, as a
    // condition, as a clause after each creature you control, or as the plural
    // target of distributed counters (Autonomous Assembler, Goatnap, Kibo, Numa),
    // including the tribe the card makes itself (Vault 12); a creature's one-shot
    // counters beside keyword counters (Scavenged Brawler); a team base X/X (The
    // Meep, ruled a slip by name); a token made a creature (Halsin, Tokens by
    // name); and a base that is one mode of a Protection spell (Secret Identity).
    [InlineData("Ashnod's Intervention", "Instant",
        "Until end of turn, target creature gets +2/+0 and gains \"When this creature dies or is put into "
        + "exile from the battlefield, return it to its owner's hand.\"")]
    [InlineData("Autonomous Assembler", "Artifact Creature — Assembly-Worker",
        "Prototype {1}{W} — 2/2 (You may cast this spell with different mana cost, color, and size. It keeps "
        + "its abilities and types.)\nVigilance\n{1}, {T}: Put a +1/+1 counter on target Assembly-Worker you "
        + "control.")]
    [InlineData("Goatnap", "Sorcery",
        "Gain control of target creature until end of turn. Untap that creature. It gains haste until end of "
        + "turn. If that creature is a Goat, it also gets +3/+0 until end of turn.")]
    [InlineData("Kibo, Uktabi Prince", "Legendary Creature — Monkey Noble",
        "{T}: Each player creates a colorless artifact token named Banana with \"{T}, Sacrifice this token: "
        + "Add {R} or {G}. You gain 2 life.\"\nWhenever an artifact an opponent controls is put into a graveyard "
        + "from the battlefield, put a +1/+1 counter on each creature you control that's an Ape or a "
        + "Monkey.\nWhenever Kibo attacks, defending player sacrifices an artifact of their choice.")]
    [InlineData("Numa, Joraga Chieftain", "Legendary Creature — Elf Warrior",
        "At the beginning of combat on your turn, you may pay {X}{X}. When you do, distribute X +1/+1 "
        + "counters among any number of target Elves.\nPartner (You can have two commanders if both have "
        + "partner.)")]
    [InlineData("Vault 12: The Necropolis", "Enchantment — Saga",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Each "
        + "player gets three rad counters.\nII — Create X 2/2 black Zombie Mutant creature tokens, where X is "
        + "the total number of rad counters among players.\nIII — Put two +1/+1 counters on each creature you "
        + "control that's a Zombie or Mutant.")]
    [InlineData("Scavenged Brawler", "Artifact Creature — Construct",
        "Flying, vigilance, trample, lifelink\n{5}, Exile this card from your graveyard: Choose target "
        + "creature. Put four +1/+1 counters, a flying counter, a vigilance counter, a trample counter, and a "
        + "lifelink counter on that creature. Activate only as a sorcery.")]
    [InlineData("The Meep", "Legendary Creature — Alien",
        "Ward—Pay 3 life.\nWhenever The Meep attacks, you may sacrifice another creature. If you do, creatures "
        + "you control have base power and toughness X/X until end of turn, where X is the sacrificed "
        + "creature's mana value.")]
    [InlineData("Halsin, Emerald Archdruid", "Legendary Creature — Elf Druid",
        "{1}: Until end of turn, target token you control becomes a green Bear creature with base power and "
        + "toughness 4/4 in addition to its other colors and types.\nChoose a Background (You can have a "
        + "Background as a second commander.)")]
    [InlineData("Secret Identity", "Instant",
        "Choose one —\n• Conceal — Until end of turn, target creature you control becomes a Citizen with base "
        + "power and toughness 1/1 and gains hexproof.\n• Reveal — Until end of turn, target creature you "
        + "control becomes a Hero with base power and toughness 3/4 and gains flying and vigilance.")]
    public void Classify_TheOctober27BuffPass_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Ruled 2026-11-05: unearth and encore SUM the card's entries for Buff as they
    // do for Tokens (2026-10-27). Simian Simulacrum puts two counters on entry,
    // four with its unearth; Kinsbaile Courier puts one, three with its encore
    // (the cube is played 2vs2, so encore makes two copies). Since 2026-11-13 the
    // bar is two, so Simian's own entry would do; Kinsbaile's one still needs it.
    [InlineData("Simian Simulacrum", "Artifact Creature — Ape",
        "When this creature enters, put two +1/+1 counters on target creature you control.\nUnearth {2}{G}{G} "
        + "({2}{G}{G}: Return this card from your graveyard to the battlefield. It gains haste. Exile it at the "
        + "beginning of the next end step or if it would leave the battlefield. Unearth only as a sorcery.)")]
    [InlineData("Kinsbaile Courier", "Creature — Kithkin Soldier",
        "When this creature enters, put a +1/+1 counter on target creature.\nEncore {2}{W} ({2}{W}, Exile this "
        + "card from your graveyard: For each opponent, create a token copy that attacks that opponent this "
        + "turn if able. They gain haste. Sacrifice them at the beginning of the next end step. Activate only "
        + "as a sorcery.)")]
    public void Classify_TheNovember5UnearthAndEncoreSumTheEntries_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-05 under the quoted-pump ruling of 2026-10-14: a Background's
    // static quoted pump for the commander (Criminal Past) and an emblem that
    // pumps every creature you control (Ellywick Tumblestrum's minus seven) are
    // the team's pump handed through one step.
    [InlineData("Criminal Past", "Legendary Enchantment — Background",
        "Commander creatures you own have menace and \"This creature gets +X/+0, where X is the number of "
        + "creature cards in your graveyard.\" (A creature with menace can't be blocked except by two or more "
        + "creatures.)")]
    [InlineData("Ellywick Tumblestrum", "Legendary Planeswalker — Ellywick",
        "+1: Venture into the dungeon. (Enter the first room or advance to the next room.)\n−2: Look at the "
        + "top six cards of your library. You may reveal a creature card from among them and put it into your "
        + "hand. If it's legendary, you gain 3 life. Put the rest on the bottom of your library in a random "
        + "order.\n−7: You get an emblem with \"Creatures you control have trample and haste and get +2/+2 for "
        + "each differently named dungeon you've completed.\"")]
    public void Classify_TheNovember5ABackgroundOrAnEmblemHandingTheTeamAPump_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The Background reading stops at the static pump: a counter the commander
    // puts on itself (Agent of the Shadow Thieves, Clan Crafter) and a pump behind
    // a trigger (Tavern Brawler's upkeep, Haunted One's tap) are the commander's
    // own business, untagged by hand.
    [InlineData("Agent of the Shadow Thieves", "Legendary Enchantment — Background",
        "Commander creatures you own have \"Whenever this creature attacks a player, if no opponent has more "
        + "life than that player, put a +1/+1 counter on this creature. It gains deathtouch and indestructible "
        + "until end of turn.\"")]
    [InlineData("Clan Crafter", "Legendary Enchantment — Background",
        "Commander creatures you own have \"{2}, Sacrifice an artifact: Put a +1/+1 counter on this creature "
        + "and draw a card.\"")]
    [InlineData("Tavern Brawler", "Legendary Enchantment — Background",
        "Commander creatures you own have \"At the beginning of your upkeep, exile the top card of your "
        + "library. This creature gets +X/+0 until end of turn, where X is that card's mana value. You may play "
        + "that card this turn.\"")]
    [InlineData("Haunted One", "Legendary Enchantment — Background",
        "Commander creatures you own have \"Whenever this creature becomes tapped, it and other creatures you "
        + "control that share a creature type with it each get +2/+0 and gain undying until end of turn.\" (When "
        + "a creature with undying dies, if it had no +1/+1 counters on it, return it to the battlefield under "
        + "its owner's control with a +1/+1 counter on it.)")]
    public void Classify_TheNovember5ABackgroundHandingTheCommanderItsOwnBusiness_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-05: doubling the power of one creature type is a pump
    // restricted to that type (Buff.md, 2026-09-15). Thrakkus the Butcher is
    // untagged by hand; Double Trouble's each creature you control stays Buff (see
    // the base-or-doubling theory).
    [InlineData("Thrakkus the Butcher", "Legendary Creature — Dragon Peasant",
        "Trample\nWhenever Thrakkus attacks, double the power of each Dragon you control until end of turn.")]
    public void Classify_TheNovember5DoublingATribe_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-05: Hungry Ridgewolf's as-long-as another condition names the
    // creature that must be there, not the one pumped; the pump is this creature's
    // own and the card is untagged by hand.
    [InlineData("Hungry Ridgewolf", "Creature — Wolf",
        "As long as you control another Wolf or Werewolf, this creature gets +1/+0 and has trample.")]
    public void Classify_TheNovember5ASelfPumpNamingAnotherInItsCondition_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-05: the NAME of a mode is no fight. You're Ambushed on the
    // Road's Stand and Fight bullet is a +1/+3 pump (Buff and Protection by hand).
    [InlineData("You're Ambushed on the Road", "Instant",
        "Choose one —\n• Make a Retreat — Return target creature you control to its owner's hand.\n• Stand and "
        + "Fight — Target creature gets +1/+3 until end of turn.")]
    // Summon: Magus Sisters keeps its Buff: its Fight! bullet is a real fight, the
    // pump comes from the Combine Powers bullet.
    [InlineData("Summon: Magus Sisters", "Enchantment Creature — Saga Faerie",
        "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II, III "
        + "— Choose one at random —\n• Combine Powers! — Put three +1/+1 counters on target creature.\n• Defense! "
        + "— Put a shield counter on target creature. You gain 3 life.\n• Fight! — This creature fights up to "
        + "one target creature an opponent controls.\nHaste")]
    public void Classify_TheNovember5AModeNamedFight_IsNotABite_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // The emblem reading is for creatures you control; an emblem that pumps one
    // tribe is a tribe's pump (Kaito, Saheeli) and stays untagged.
    [InlineData("Kaito, Bane of Nightmares", "Legendary Planeswalker — Kaito",
        "Ninjutsu {1}{U}{B} ({1}{U}{B}, Return an unblocked attacker you control to hand: Put this card onto "
        + "the battlefield from your hand tapped and attacking.)\nDuring your turn, as long as Kaito has one or "
        + "more loyalty counters on him, he's a 3/4 Ninja creature and has hexproof.\n+1: You get an emblem with "
        + "\"Ninjas you control get +1/+1.\"\n0: Surveil 2. Then draw a card for each opponent who lost life this "
        + "turn.\n−2: Tap target creature. Put two stun counters on it.")]
    [InlineData("Saheeli, Sublime Artificer", "Legendary Planeswalker — Saheeli",
        "Whenever you cast a noncreature spell, create a 1/1 colorless Servo artifact creature token.\n−2: "
        + "Target artifact you control becomes a copy of another target artifact or creature you control until "
        + "end of turn, except it's an artifact in addition to its other types.")]
    public void Classify_TheNovember5AnEmblemForOneTribe_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Ruled 2026-11-05 twice: the melee Vivien's Stampede hands the team is a
    // keyword, never Buff (Skyhunter Strike Force, Drogskol Reinforcements and
    // Depthshaker Titan are untagged), and its draw a card for each player who was
    // dealt combat damage is CardAdvantage like the family (7 tagged to 1).
    [InlineData("Vivien's Stampede", "Sorcery",
        "Each creature you control gains vigilance, trample, and melee until end of turn. (Whenever a "
        + "creature with melee attacks, it gets +1/+1 until end of turn for each opponent you attacked this "
        + "combat.)\nAt the beginning of the next main phase this turn, draw a card for each player who was "
        + "dealt combat damage this turn.")]
    public void Classify_TheNovember5VivienStampede_IsCardAdvantageNotBuff(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.True(result.HasFlag(CardEffect.CardAdvantage));
        Assert.False(result.HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Ruled 2026-11-05 (Buff se forza >= 3): a new BASE power and toughness for
    // ANY target creature is Buff when it is the whole card, a cantrip no other
    // effect, and the base power is three or more. Serpentine Ambush is tagged by
    // hand; Suit Up is reached and handed back; Startling Development,
    // Mordenkainen's Polymorph and Omnibian are unreviewed.
    [InlineData("Serpentine Ambush", "Instant",
        "Until end of turn, target creature becomes a blue Serpent with base power and toughness 5/5.")]
    [InlineData("Suit Up", "Instant",
        "Until end of turn, target creature or Vehicle becomes an artifact creature with base power and "
        + "toughness 4/5.\nDraw a card.")]
    [InlineData("Startling Development", "Instant",
        "Until end of turn, target creature becomes a blue Serpent with base power and toughness 4/4.\nCycling "
        + "{1} ({1}, Discard this card: Draw a card.)")]
    [InlineData("Mordenkainen's Polymorph", "Instant",
        "Until end of turn, target creature becomes a Dragon with base power and toughness 4/4 and gains "
        + "flying. (It loses all other creature types.)")]
    [InlineData("Omnibian", "Creature — Frog",
        "{T}: Target creature becomes a Frog with base power and toughness 3/3 until end of turn.")]
    public void Classify_TheNovember5ABigBaseForAnyCreature_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // A base under three power is the creature taken down: Metamorphic Blast's 0/1
    // mode (CardAdvantage and Pacify by hand) and Eccentric Apprentice's 1/1 Bird
    // (unreviewed) stay out of the big-base reading.
    [InlineData("Metamorphic Blast", "Instant",
        "Spree (Choose one or more additional costs.)\n+ {1} — Until end of turn, target creature becomes a "
        + "white Rabbit with base power and toughness 0/1.\n+ {3} — Target player draws two cards.")]
    [InlineData("Eccentric Apprentice", "Creature — Tiefling Wizard",
        "Flying\nWhen this creature enters, venture into the dungeon. (Enter the first room or advance to the "
        + "next room.)\nAt the beginning of combat on your turn, if you've completed a dungeon, up to one target "
        + "creature becomes a Bird with base power and toughness 1/1 and flying until end of turn.")]
    public void Classify_TheNovember5AShrinkingBase_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-12: a big base HAD by a target creature (the 2026-11-05 'Buff
    // se forza >= 3' ruling, Square Up), and an activation that names its target
    // and puts the counter on it (Tyrite Sanctum).
    [InlineData("Square Up", "Instant",
        "Target creature has base power and toughness 4/4 until end of turn.")]
    [InlineData("Tyrite Sanctum", "Land",
        "{T}: Add {C}.\n{2}, {T}: Target legendary creature becomes a God in addition to its other types. Put "
        + "a +1/+1 counter on it.\n{4}, {T}, Sacrifice this land: Put an indestructible counter on target God.")]
    public void Classify_TheNovember12Readings_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Read 2026-11-12: the counters that size what the card animated (Rise and
    // Shine) and a tribe's anthem behind an ability word's dash (Scuttletide's
    // Crabs).
    [InlineData("Rise and Shine", "Sorcery",
        "Target noncreature artifact you control becomes a 0/0 artifact creature. Put four +1/+1 counters on "
        + "each artifact that became a creature this way.\nOverload {4}{U}{U} (You may cast this spell for its "
        + "overload cost. If you do, change \"target\" in its text to \"each.\")")]
    [InlineData("Scuttletide", "Enchantment",
        "{1}, Discard a card: Create a 0/3 blue Crab creature token.\nDelirium — Crabs you control get +1/+1 "
        + "as long as there are four or more card types among cards in your graveyard.")]
    public void Classify_TheNovember12Readings_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Buff pass of 2026-11-13 under the definition (Glorious Anthem). Counters a
    // creature hands out as it enters meet the bar of two like a temporary pump on
    // entry ('Basta 2 per entrambi', overturning the bar of three of 2026-09-25):
    // Apothecary Stomper, Caldaia Strongarm, bolster 2. An Equipment goes only on
    // your creatures, so its +N/-M is Buff (Barbed Batterfist; Skullclamp beside
    // its CardAdvantage, 'Buff e CardAdvantage'). Warp sums the entries like
    // unearth ('Warp somma le entrate'): one counter is two (Drix Fatemaker,
    // Rayblade Trooper).
    [InlineData("Apothecary Stomper", "Creature — Elephant",
        "Vigilance (Attacking doesn't cause this creature to tap.)\nWhen this creature enters, choose one —\n• "
        + "Put two +1/+1 counters on target creature you control.\n• You gain 4 life.")]
    [InlineData("Caldaia Strongarm", "Creature — Human Warrior",
        "When this creature enters, put two +1/+1 counters on target creature.\nBlitz {3}{G} (If you cast this "
        + "spell for its blitz cost, it gains haste and \"When this creature dies, draw a card.\" Sacrifice it at "
        + "the beginning of the next end step.)")]
    [InlineData("Sandsteppe Scavenger", "Creature — Dog Scout",
        "When this creature enters, bolster 2. (Choose a creature with the least toughness among creatures "
        + "you control and put two +1/+1 counters on it.)")]
    [InlineData("Barbed Batterfist", "Artifact — Equipment",
        "For Mirrodin! (When this Equipment enters, create a 2/2 red Rebel creature token, then attach this "
        + "to it.)\nEquipped creature gets +1/-1.\nEquip {1} ({1}: Attach to target creature you control. Equip "
        + "only as a sorcery.)")]
    [InlineData("Skullclamp", "Artifact — Equipment",
        "Equipped creature gets +1/-1.\nWhenever equipped creature dies, draw two cards.\nEquip {1}")]
    [InlineData("Drix Fatemaker", "Creature — Drix Wizard",
        "When this creature enters, put a +1/+1 counter on target creature.\nEach creature you control with a "
        + "+1/+1 counter on it has trample.\nWarp {1}{G} (You may cast this card from your hand for its warp "
        + "cost. Exile this creature at the beginning of the next end step, then you may cast it from exile on "
        + "a later turn.)")]
    [InlineData("Rayblade Trooper", "Creature — Human Soldier",
        "When this creature enters, put a +1/+1 counter on target creature you control.\nWhenever a nontoken "
        + "creature you control with a +1/+1 counter on it dies, create a 1/1 white Human Soldier creature "
        + "token.\nWarp {1}{W} (You may cast this card from your hand for its warp cost. Exile this creature at "
        + "the beginning of the next end step, then you may cast it from exile on a later turn.)")]
    public void Classify_TheNovember13BuffPass_IsBuff(string name, string typeLine, string oracle)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }

    [Theory]
    // Buff pass of 2026-11-13, the other side. A counter WITH which a creature is
    // named is a condition, not a pump over an area (Duskshell Crawler, untagged
    // 2026-11-11). The bigger count only a tribe gets INSTEAD is the tribe's: the
    // one counter for everybody is judged (Wakandan Royal Guard, Elder Cathar). A
    // team base SETS rather than adds, raising the small and lowering the big, and
    // stays out (0 of 9 reviewed; Candlekeep Inspiration).
    [InlineData("Duskshell Crawler", "Creature — Insect",
        "When this creature enters, put a +1/+1 counter on target creature.\nEach creature you control with a "
        + "+1/+1 counter on it has trample. (It can deal excess combat damage to the player or planeswalker "
        + "it's attacking.)")]
    [InlineData("Wakandan Royal Guard", "Creature — Human Soldier Hero",
        "Vigilance\nWhen this creature enters, put a +1/+1 counter on target creature. If that creature is "
        + "another Hero, put two +1/+1 counters on it instead.")]
    [InlineData("Elder Cathar", "Creature — Human Soldier",
        "When this creature dies, put a +1/+1 counter on target creature you control. If that creature is a "
        + "Human, put two +1/+1 counters on it instead.")]
    [InlineData("Candlekeep Inspiration", "Sorcery",
        "Until end of turn, creatures you control have base power and toughness X/X, where X is the number of "
        + "cards you own in exile and in your graveyard that are instant cards, are sorcery cards, and/or have "
        + "an Adventure.")]
    public void Classify_TheNovember13BuffPass_IsNotBuff(string name, string typeLine, string oracle)
    {
        Assert.False(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(CardEffect.Buff));
    }
}
