using System.Collections.Generic;

using ScatoloneDownloader.Cube;
using ScatoloneDownloader.Json.Cards;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Mtg;

/// <summary>
/// Validates the rule-based <see cref="EffectClassifier"/> against real card
/// oracle text. The classifier only PROPOSES (a human confirms), so these pin
/// the high-confidence cases, the deliberate narrow edges (mana denial that
/// destroys nothing, a land sacrifice paid as a drawback) and the wordings
/// that fooled an earlier draft, rather than every card.
/// </summary>
public sealed class EffectClassifierTests
{
    [Theory]
    // name, typeLine, oracleText, expected flags
    [InlineData("Lightning Bolt", "Instant", "Lightning Bolt deals 3 damage to any target.", CardEffect.Burn)]
    [InlineData("Wrath of God", "Sorcery", "Destroy all creatures. They can't be regenerated.", CardEffect.Wipe)]
    [InlineData("Counterspell", "Instant", "Counter target spell.", CardEffect.Counter)]
    [InlineData("Swords to Plowshares", "Instant", "Exile target creature. Its controller gains life equal to its power.", CardEffect.Removal)]
    [InlineData("Vindicate", "Sorcery", "Destroy target permanent.", CardEffect.RemovePermanent)]
    [InlineData("Demonic Tutor", "Sorcery", "Search your library for a card, then shuffle and put that card into your hand.", CardEffect.Tutor)]
    [InlineData("Worldly Tutor", "Instant", "Search your library for a creature card, reveal that card, then shuffle and put it on top.", CardEffect.Tutor)]
    [InlineData("Sol Ring", "Artifact", "{T}: Add {C}{C}.", CardEffect.Ramp)]
    [InlineData("Pacifism", "Enchantment — Aura", "Enchant creature. Enchanted creature can't attack or block.", CardEffect.Pacify)]
    [InlineData("Icy Manipulator", "Artifact", "{1}, {T}: Tap target artifact, creature, or land.", CardEffect.Pacify)]
    [InlineData("Reanimate", "Sorcery", "Return target creature card from your graveyard to the battlefield. You lose life equal to its mana value.", CardEffect.Reanimate)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Mind Rot", "Sorcery", "Target player discards two cards.", CardEffect.Discard)]
    [InlineData("Divination", "Sorcery", "Draw two cards.", CardEffect.CardAdvantage)]
    [InlineData("Control Magic", "Enchantment — Aura", "Enchant creature. You control enchanted creature.", CardEffect.Steal)]
    [InlineData("Stone Rain", "Sorcery", "Destroy target land.", CardEffect.LandDestruction)]
    [InlineData("Wasteland", "Land", "{T}, Sacrifice Wasteland: Destroy target nonbasic land.", CardEffect.LandDestruction)]
    // "Destroy all lands" is land destruction, not a creature/permanent wipe:
    // Wipe's patterns deliberately list creatures, permanents and nonland only.
    [InlineData("Armageddon", "Sorcery", "Destroy all lands.", CardEffect.LandDestruction)]
    [InlineData("Rain of Salt", "Sorcery", "Destroy two target lands.", CardEffect.LandDestruction)]
    [InlineData("Sinkhole", "Sorcery", "Destroy target land.", CardEffect.LandDestruction)]
    [InlineData("Glimpse the Unthinkable", "Sorcery", "Target player mills ten cards.", CardEffect.Mill)]
    [InlineData("Misdirection", "Instant", "Change the target of target spell with a single target.", CardEffect.Redirect)]
    [InlineData("Fork", "Instant", "Copy target instant or sorcery spell. You may choose new targets for the copy.", CardEffect.Redirect)]
    // A token entering as a copy OF something is Tokens, not stack interaction:
    // the rules require "copy target", never the bare word "copy".
    [InlineData("Clone", "Creature — Shapeshifter", "You may have this creature enter as a copy of any creature on the battlefield.", CardEffect.None)]
    // "Mill" is only keyword wording from 2021 on; the pre-2021 library spells
    // the action out, so the old phrasing has to match too.
    [InlineData("Millstone", "Artifact", "{2}, {T}: Target player puts the top two cards of their library into their graveyard.", CardEffect.Mill)]
    // Self-mill is the same effect aimed the other way — a graveyard deck's fuel.
    // CardAdvantage rides along because dredge's reminder text says "if you would
    // draw a card": reminder text is part of oracle_text, and the classifier only
    // proposes, so the extra flag is noise a reviewer drops rather than a bug to
    // chase with a negative lookahead.
    [InlineData("Stinkweed Imp", "Creature — Imp", "Flying\nWhenever this creature deals combat damage to a creature, destroy that creature.\nDredge 5 (If you would draw a card, you may mill five cards instead.)", CardEffect.CardAdvantage | CardEffect.Mill)]
    public void Classify_ExactMatch_ForHighConfidenceCards(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

    [Theory]
    // "island" and "islandwalk" contain the letters of "land", so the patterns
    // require a word boundary before it. Without that, every islandwalk creature
    // would be proposed as land destruction.
    [InlineData("Deep Spawn", "Creature — Kraken", "Islandwalk")]
    [InlineData("Sea Serpent", "Creature — Serpent", "Sea Serpent can't attack unless defending player controls an Island.")]
    // Both of these were caught by auditing already-reviewed cards against a
    // looser first draft of the rules. Pyramids PROTECTS lands, and the thing it
    // destroys is an Aura; Serendib Djinn's land sacrifice is a drawback its own
    // controller pays, not an effect aimed at an opponent.
    [InlineData("Pyramids", "Artifact", "{2}: Destroy target Aura attached to a land.\n{2}: The next time target land would be destroyed this turn, remove all damage marked on it instead.")]
    [InlineData("Serendib Djinn", "Creature — Djinn", "Flying\nAt the beginning of your upkeep, sacrifice a land. If you sacrifice an Island this way, this creature deals 3 damage to you.")]
    public void Classify_LandWordings_ThatAreNotLandDestruction(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.LandDestruction));
    }

    [Theory]
    // Milling as a PRICE is not a mill card. Measured against the whole Scryfall
    // corpus, this guard withdraws Mill from exactly five cards — the three
    // wordings below plus Charmed Pendant and The Warring Triad — so it pins the
    // ruling rather than re-cutting the tag.
    [InlineData("Deep Spawn", "Creature — Kraken",
        "Trample\nAt the beginning of your upkeep, sacrifice this creature unless you mill two cards.\n{U}: This creature gains shroud until end of turn.")]
    [InlineData("Millikin", "Artifact Creature — Construct", "{T}, Mill a card: Add {C}.")]
    [InlineData("Rot Farm Skeleton", "Creature — Plant Skeleton",
        "This creature can't block.\n{2}{B}{G}, Mill four cards: Return this card from your graveyard to the battlefield. Activate only as a sorcery.")]
    public void Classify_MillPaidAsACost_IsNotMill(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.Mill));
    }

    [Theory]
    // The guard strikes out cost clauses and asks what is LEFT, so a card that
    // both pays and mills keeps the tag. Altar of Dementia's sacrifice is the
    // cost and the mill is what it buys — the opposite arrangement to Millikin.
    [InlineData("Altar of Dementia", "Artifact", "Sacrifice a creature: Target player mills cards equal to the sacrificed creature's power.")]
    [InlineData("Glimpse the Unthinkable", "Sorcery", "Target player mills ten cards.")]
    // A payoff whose only mill wording is reminder text still counts: the same
    // reminder-text reading that gives Stinkweed Imp its Mill.
    [InlineData("Bruvac the Grandiloquent", "Legendary Creature — Human Advisor",
        "If an opponent would mill one or more cards, they mill twice that many cards instead. (To mill a card, a player puts the top card of their library into their graveyard.)")]
    public void Classify_MillAsAnEffect_SurvivesTheCostGuard(string name, string typeLine, string oracle)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.True(EffectClassifier.Classify(card).HasFlag(CardEffect.Mill));
    }

    [Fact]
    public void Classify_ManaDenial_WithoutDestroyingALand_IsNotProposed()
    {
        // The tag is deliberately narrow: Winter Orb attacks the mana base but
        // destroys nothing, so it stays for a human to decide.
        Card card = MakeCard("Winter Orb", "Artifact", "Players can't untap more than one land during their untap steps.");

        Assert.False(EffectClassifier.Classify(card).HasFlag(CardEffect.LandDestruction));
    }

    [Fact]
    public void Classify_BirdsOfParadise_RampAndManaFixing()
    {
        Card card = MakeCard("Birds of Paradise", "Creature — Bird", "Flying\n{T}: Add one mana of any color.", keywords: ["Flying"]);

        Assert.Equal(CardEffect.Ramp | CardEffect.ManaFixing, EffectClassifier.Classify(card));
    }

    [Fact]
    public void Classify_DualLand_IsManaFixing_NotRamp()
    {
        // A land tapping for its own (fixing) mana must be ManaFixing but NOT Ramp.
        Card card = MakeCard("Tundra", "Land", "{T}: Add {W} or {U}.");

        CardEffect result = EffectClassifier.Classify(card);

        Assert.True(result.HasFlag(CardEffect.ManaFixing));
        Assert.False(result.HasFlag(CardEffect.Ramp));
    }

    [Theory]
    // Protection counts only when it can be deployed in response. Mother of Runes
    // answers a removal spell on the stack; a bogle with hexproof printed on it
    // protects nobody but itself, and holds nothing up.
    [InlineData("Mother of Runes", "Creature — Human Cleric", "{T}: Target creature you control gains protection from the color of your choice until end of turn.", true)]
    [InlineData("Giant Growth", "Instant", "Target creature gets +3/+3 until end of turn. It gains hexproof until end of turn.", true)]
    // NB: a Circle of Protection would pass this gate (its "{1}:" is an activated
    // ability) but never reaches it — the rules' vocabulary has no word for
    // PREVENTION yet, only protection/hexproof/shroud/indestructible. Whether
    // prevention joins them is still open, and deciding it here by side effect
    // would also silently rule on every fog.
    [InlineData("Slippery Bogle", "Creature — Beast", "", false)]
    [InlineData("Darksteel Colossus", "Artifact Creature — Golem", "Trample\nThis creature is indestructible.", false)]
    [InlineData("Asceticism-style aura", "Enchantment — Aura", "Enchanted creature has hexproof.", false)]
    public void Classify_Protection_RequiresInstantSpeed(string name, string typeLine, string oracle, bool expected)
    {
        List<string>? keywords = name == "Slippery Bogle" ? ["Hexproof"] : null;
        Card card = MakeCard(name, typeLine, oracle, keywords);

        Assert.Equal(expected, EffectClassifier.Classify(card).HasFlag(CardEffect.Protection));
    }

    [Theory]
    // A creature that pumps or shields only ITSELF has a stat line, not an
    // effect — it answers nothing for the rest of the board. Decided 2026-09-05.
    [InlineData("Adanto Vanguard", "Creature — Vampire Soldier",
        "As long as this creature is attacking, it gets +2/+0.\nPay 4 life: This creature gains indestructible until end of turn.")]
    [InlineData("Advanced Hoverguard", "Creature — Drone",
        "Flying\n{U}: This creature gains shroud until end of turn.")]
    // Its own name is the same self-reference the older printings write.
    [InlineData("Akroma, Angel of Fury", "Legendary Creature — Angel",
        "This spell can't be countered.\nFlying, trample, protection from white and from blue\n{R}: Akroma gets +1/+0 until end of turn.")]
    [InlineData("Armored Armadillo", "Creature — Armadillo",
        "Ward {1} (Whenever this creature becomes the target of a spell or ability an opponent controls, counter it unless that player pays {1}.)")]
    // A condition ON what you control is not a beneficiary: the pump still lands
    // on the creature itself. This is the wording the stop-word list exists for.
    [InlineData("Aerial Engineer", "Artifact Creature — Construct",
        "As long as you control an artifact, this creature gets +2/+0 and has flying.")]
    public void Classify_SelfBuffAndSelfProtection_AreNotEffects(string name, string typeLine, string oracle)
    {
        CardEffect result = EffectClassifier.Classify(MakeCard(name, typeLine, oracle));

        Assert.False(result.HasFlag(CardEffect.Buff));
        Assert.False(result.HasFlag(CardEffect.Protection));
    }

    [Theory]
    // The same creatures, aimed outward, keep the tag. A "target" two lines up
    // must not vouch for a self-buff, which is why the window is one line: the
    // Armored Guardian entry below grants on one line and shields itself on the
    // next, and only the first is why it counts.
    [InlineData("Aerie Mystics", "Creature — Bird Wizard",
        "Flying\n{1}{G}{U}: Creatures you control gain shroud until end of turn.", CardEffect.Protection)]
    [InlineData("Armored Guardian", "Creature — Giant",
        "{1}{W}{W}: Target creature you control gains protection from the color of your choice until end of turn.\n{1}{U}{U}: This creature gains shroud until end of turn.", CardEffect.Protection)]
    [InlineData("Glorious Anthem", "Enchantment", "Creatures you control get +1/+1.", CardEffect.Buff)]
    [InlineData("Bonesplitter", "Artifact — Equipment", "Equipped creature gets +2/+0.\nEquip {1}", CardEffect.Buff)]
    // The beneficiary is whatever type line the card names, not just "creatures",
    // and it can be a battlefield state rather than a controller.
    [InlineData("Adeliz, the Cinder Wind", "Legendary Creature — Efreet Wizard",
        "Flying, haste\nWhenever you cast an instant or sorcery spell, Wizards you control get +1/+1 until end of turn.", CardEffect.Buff)]
    [InlineData("Ainok Strike Leader", "Creature — Hound Soldier",
        "Sacrifice this creature: Creature tokens you control gain indestructible until end of turn.", CardEffect.Protection)]
    [InlineData("Agrus Kos, Wojek Veteran", "Legendary Creature — Human Soldier",
        "Whenever Agrus Kos attacks, attacking red creatures get +2/+0 and attacking white creatures get +0/+2 until end of turn.", CardEffect.Buff)]
    public void Classify_EffectsAimedAtSomethingElse_Survive(string name, string typeLine, string oracle, CardEffect expected)
    {
        Assert.True(EffectClassifier.Classify(MakeCard(name, typeLine, oracle)).HasFlag(expected));
    }

    [Fact]
    public void Classify_VanillaCreature_IsNone()
    {
        Card card = MakeCard("Grizzly Bears", "Creature — Bear", "");

        Assert.Equal(CardEffect.None, EffectClassifier.Classify(card));
    }

    [Theory]
    // Graveyard to HAND is Regrowth; graveyard to the BATTLEFIELD stays Reanimate.
    // This used to be a documented gap: the Reanimate rules never matched a return
    // to hand, so Regrowth itself came back untagged.
    [InlineData("Regrowth", "Sorcery", "Return target card from your graveyard to your hand.", CardEffect.Regrowth)]
    [InlineData("Raise Dead", "Sorcery", "Return target creature card from your graveyard to your hand.", CardEffect.Regrowth)]
    [InlineData("Animate Dead", "Enchantment — Aura", "Return target creature card from your graveyard to the battlefield under your control.", CardEffect.Reanimate)]
    public void Classify_GraveyardRecursion_SplitsByDestination(string name, string typeLine, string oracle, CardEffect expected)
    {
        Card card = MakeCard(name, typeLine, oracle);

        Assert.Equal(expected, EffectClassifier.Classify(card));
    }

    private static Card MakeCard(string name, string typeLine, string oracleText, List<string>? keywords = null)
    {
        JsonCard json = new()
        {
            Name = name,
            Language = "en",
            ReleasedAt = "1997-04-25",
            Layout = "normal",
            TypeLine = typeLine,
            OracleText = oracleText,
            Keywords = keywords,
            Games = ["paper"],
            FrameEffects = [],
            Set = "TMP",
            SetName = "Tempest",
            SetType = "expansion",
            BorderColor = "black",
            Cmc = 1,
            Colors = [],
            ColorIdentity = [],
            ManaCost = "",
            ImageUris = new JsonImageUris { Png = "https://test/x.png" },
        };

        return Card.CreateCard(json);
    }
}
