using ScatoloneDownloader.Cli;
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
        // Reserved: 0-5 (rating), n/b/t/j (status), c (confirm), f (filter).
        const string reserved = "012345nbtjcf";
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
}
