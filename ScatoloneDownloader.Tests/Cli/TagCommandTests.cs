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

    [Fact]
    public void BuildPrefixes_WithNoExtraHosts_IsLocalhostOnly()
    {
        Assert.Equal(["http://localhost:8765/"], TagCommand.BuildPrefixes(8765, null));
        Assert.Equal(["http://localhost:8765/"], TagCommand.BuildPrefixes(8765, []));
    }

    [Fact]
    public void BuildPrefixes_KeepsLocalhostFirst_SoTheLocalTaggerSurvivesABadHost()
    {
        // localhost is the only prefix Windows registers without a reservation.
        string[] prefixes = TagCommand.BuildPrefixes(8765, ["cala.tail6de9de.ts.net"]);

        Assert.Equal("http://localhost:8765/", prefixes[0]);
        Assert.Equal("http://cala.tail6de9de.ts.net:8765/", prefixes[1]);
    }

    [Fact]
    public void BuildPrefixes_UsesTheListenerPort_NotTheProxysPort()
    {
        // `tailscale serve --http=8080 http://localhost:8765` forwards to 8765 while
        // preserving "Host: cala...:8080". http.sys ignores that port and matches on
        // the name plus the port the connection arrived on, so 8765 is what belongs
        // in the prefix — publishing on another tailnet port needs nothing here.
        Assert.Equal(
            ["http://localhost:8765/", "http://cala.tail6de9de.ts.net:8765/"],
            TagCommand.BuildPrefixes(8765, ["cala.tail6de9de.ts.net"]));
    }

    [Theory]
    [InlineData("cala")]
    [InlineData("CALA")]
    [InlineData("  cala  ")] // shells and copy-paste leave whitespace
    public void BuildPrefixes_DoesNotRepeatAHost(string second)
    {
        // HttpListener throws on a duplicate prefix, so a repeated (or differently
        // cased, or padded) --host must collapse rather than crash the tagger.
        Assert.Equal(
            ["http://localhost:8765/", "http://cala:8765/"],
            TagCommand.BuildPrefixes(8765, ["cala", second]));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LocalHost")]
    public void BuildPrefixes_IgnoresLocalhostAsAnExtraHost(string host)
    {
        Assert.Equal(["http://localhost:8765/"], TagCommand.BuildPrefixes(8765, [host]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildPrefixes_SkipsBlankHosts(string host)
    {
        Assert.Equal(["http://localhost:8765/"], TagCommand.BuildPrefixes(8765, [host]));
    }

    [Theory]
    // Each of these parses into a prefix that never matches anything, which would
    // start the tagger clean and still answer the phone 400 — so fail at startup.
    [InlineData("http://cala.tail6de9de.ts.net")] // scheme
    [InlineData("cala.tail6de9de.ts.net:8080")]   // port
    [InlineData("cala.tail6de9de.ts.net/tagger")] // path
    [InlineData("cala tail6de9de")]               // unquoted, split by the shell
    [InlineData(@"CALA\Cala")]                    // a user, not a host
    public void BuildPrefixes_RejectsAnythingThatIsNotABareHostname(string host)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => TagCommand.BuildPrefixes(8765, [host]));

        Assert.Contains("bare hostname", ex.Message);
    }
}
