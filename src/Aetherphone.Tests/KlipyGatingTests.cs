using Aetherphone.Core.Gifs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class KlipyGatingTests
{
    [Theory]
    [InlineData("https://static.klipy.com/ii/a/b.gif", true)]
    [InlineData("https://static1.klipy.com/ii/a/b.gif", true)]
    [InlineData("https://STATIC2.KLIPY.COM/ii/a/b.gif", true)]
    [InlineData("https://klipy.com/x.gif", true)]
    [InlineData("https://static.klipy.com:443/x.gif", true)]
    [InlineData("https://static.klipy.com?x", true)]
    [InlineData("https://static.klipy.com", true)]
    [InlineData("http://static.klipy.com/x.gif", false)]
    [InlineData("https://notklipy.com/x.gif", false)]
    [InlineData("https://static.klipy.com.evil.test/x.gif", false)]
    [InlineData("https://media.aetherphone.net/chirps/u/x.gif", false)]
    [InlineData("https://evil.test/static.klipy.com/x.gif", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsHostedRecognisesEveryKlipyHostAndNothingElse(string? url, bool expected)
    {
        Assert.Equal(expected, KlipyMedia.IsHosted(url));
    }

    [Fact]
    public void AKlipyGifWaitsForATapUntilThePlayerAllowsKlipy()
    {
        const string url = "https://static.klipy.com/ii/gating/held.gif";

        Assert.True(KlipyReveals.ShouldHold(url, false));
        Assert.False(KlipyReveals.ShouldHold(url, true));
        Assert.False(KlipyReveals.ShouldHold("https://media.aetherphone.net/chirps/u/own.gif", false));
        Assert.False(KlipyReveals.ShouldHold(null, false));
    }

    [Fact]
    public void ATappedGifStaysShownWithoutRevealingOthers()
    {
        const string tapped = "https://static.klipy.com/ii/gating/tapped.gif";
        const string other = "https://static.klipy.com/ii/gating/other.gif";

        KlipyReveals.Reveal(tapped);

        Assert.False(KlipyReveals.ShouldHold(tapped, false));
        Assert.True(KlipyReveals.ShouldHold(other, false));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("  happy \t\n cat  ", "happy cat")]
    [InlineData("dance\u0000party", "dance party")]
    public void TheQueryIsTrimmedAndItsWhitespaceCollapsed(string? raw, string expected)
    {
        Assert.Equal(expected, GifQuery.Normalize(raw));
    }

    [Fact]
    public void TheQueryIsCappedWithoutSplittingAnEmoji()
    {
        var raw = new string('a', GifQuery.MaxLength - 1) + "\U0001F600tail";

        var normalized = GifQuery.Normalize(raw);

        Assert.Equal(new string('a', GifQuery.MaxLength - 1), normalized);
        Assert.Equal(GifQuery.MaxLength, GifQuery.Normalize(new string('b', GifQuery.MaxLength * 2)).Length);
        Assert.Equal("\U0001F600", GifQuery.Normalize("\U0001F600"));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("ok", true)]
    [InlineData("good morning", true)]
    public void OnlyTwoOrMoreCharactersSearchAndAnythingShorterShowsTrending(string normalized, bool expected)
    {
        Assert.Equal(expected, GifQuery.IsSearch(normalized));
    }
}
