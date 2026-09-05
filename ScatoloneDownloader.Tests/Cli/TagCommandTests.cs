using ScatoloneDownloader.Cli.Cube;
using ScatoloneDownloader.Mtg;

using Xunit;

namespace ScatoloneDownloader.Tests.Cli;

/// <summary>
/// Guards the tagger page moved to an embedded resource (#21): the resource
/// loads and its <c>__EFFECT_KEYS__</c> placeholder is substituted, and the
/// hotkey string covers every <see cref="CardEffect"/> so a newly added effect
/// can never silently lose its keyboard shortcut.
/// </summary>
public sealed class TagCommandTests
{
    [Fact]
    public void EffectHotkeys_HasAKeyForEveryEffect()
    {
        int effectCount = EffectResolver.ToNames((CardEffect)~0).Count;

        Assert.True(
            TagCommand.EffectHotkeys.Length >= effectCount,
            $"EffectHotkeys must have at least {effectCount} keys (one per CardEffect); has {TagCommand.EffectHotkeys.Length}.");
    }

    [Fact]
    public void EffectHotkeys_AvoidReservedKeys()
    {
        // Reserved: 0-5 (rating), n/b/t/j (status), c (confirm), "/" (card list).
        // Deliberately SHORT: every filter is mouse-only, which is what freed "f"
        // and the punctuation for the effects. "-", "8" and "9" ARE effect keys.
        const string reserved = "012345nbtjc/";
        foreach (char key in TagCommand.EffectHotkeys)
        {
            Assert.DoesNotContain(key, reserved);
        }
    }

    [Fact]
    public void GetPageHtml_LoadsEmbeddedResource_AndSubstitutesHotkeys()
    {
        string html = TagCommand.GetPageHtml();

        Assert.Contains("<!doctype html>", html);
        Assert.DoesNotContain("__EFFECT_KEYS__", html);                 // placeholder replaced
        Assert.Contains($"\"{TagCommand.EffectHotkeys}\".split", html); // keys injected
    }

    [Theory]
    // The library is laid out as <year>\<set>\<card>.png, which is what the
    // page's two folder pickers split on.
    [InlineData(@"C:\Master", @"C:\Master\2000\Invasion\Rogue Kavu.png", "2000/Invasion")]
    [InlineData(@"C:\Master\", @"C:\Master\1993\Limited Edition Alpha\Fastbond.png", "1993/Limited Edition Alpha")]
    // A file sitting straight in the root has no folder to filter by.
    [InlineData(@"C:\Master", @"C:\Master\Loose.png", "")]
    // Deeper or shallower layouts still yield whatever levels exist, so a
    // library organised differently keeps working.
    [InlineData(@"C:\Master", @"C:\Master\2020\Zendikar Rising\Promos\Card.png", "2020/Zendikar Rising/Promos")]
    [InlineData(@"C:\Master", @"C:\Master\Unsorted\Card.png", "Unsorted")]
    public void RelativeFolder_ReturnsForwardSlashedPathBelowTheMaster(string master, string file, string expected)
    {
        Assert.Equal(expected, TagCommand.RelativeFolder(master, file));
    }

    [Fact]
    public void RelativeFolder_IsEmpty_WhenTheMasterIsUnknown()
    {
        // The DTO is built before the master directory is known only if something
        // went wrong; return "" rather than a path relative to nothing.
        Assert.Equal(string.Empty, TagCommand.RelativeFolder(string.Empty, @"C:\Master\2000\Invasion\Card.png"));
    }
}
