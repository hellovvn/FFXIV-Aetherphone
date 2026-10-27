using Aetherphone.Core.Gifs;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GifShareTests
{
    private const string Url = "https://static.klipy.com/ii/925f17378dd1893b674a723c07535afe/e7/3c/c8SEQWwx.gif";

    private static readonly GifItem Item = new("happy-caturday-happy-saturday-5", "https://static.klipy.com/ii/p.gif",
        220, 124, Url, 498, 280);

    [Fact]
    public void ComposeRoundTripsThroughTryParse()
    {
        var token = GifShare.Compose(Item);

        Assert.True(GifShare.TryParse(token, out var gif));
        Assert.Equal(new SharedGif(Item.Slug, 498, 280, Url), gif);
        Assert.Equal(ChatText.GifKind, ChatText.EffectiveKind(token, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[aep.gif.v1:]")]
    [InlineData("[aep.gif.v1:slug;498;280]")]
    [InlineData("[aep.gif.v1:slug;498;280;" + Url)]
    [InlineData("look [aep.gif.v1:slug;498;280;" + Url + "]")]
    [InlineData("[aep.gif.v1:slug;0;280;" + Url + "]")]
    [InlineData("[aep.gif.v1:slug;498;99999;" + Url + "]")]
    [InlineData("[aep.gif.v1:slug;-4;280;" + Url + "]")]
    [InlineData("[aep.gif.v1:../report;498;280;" + Url + "]")]
    [InlineData("[aep.gif.v1:slug;498;280;https://evil.example/ii/a.gif]")]
    [InlineData("[aep.gif.v1:slug;498;280;https://static.klipy.com.evil.test/ii/a.gif]")]
    [InlineData("[aep.gif.v1:slug;498;280;http://static.klipy.com/ii/a.gif]")]
    [InlineData("[aep.gif.v1:slug;498;280;https://static.klipy.com/ii/a.gif?track=1]")]
    [InlineData("[aep.gif.v1:slug;498;280;https://static.klipy.com/ii//a.gif]")]
    [InlineData("[aep.gif.v1:slug;498;280;https://static.klipy.com/ii/a.png]")]
    [InlineData("[aep.gif.v1:slug;498;280;https://api.klipy.com/ii/a.gif]")]
    public void AnythingButAPlainKlipyGifStaysText(string? body)
    {
        Assert.False(GifShare.TryParse(body, out _));
    }

    [Theory]
    [InlineData(Url, true)]
    [InlineData("https://static1.klipy.com/a/b.gif", true)]
    [InlineData("https://static2.klipy.com/a_b-c.gif", true)]
    [InlineData("https://klipy.com/a.gif", false)]
    [InlineData("https://static.klipy.com/.gif", false)]
    [InlineData("https://static.klipy.com/a/.gif", false)]
    [InlineData("https://STATIC.klipy.com/a.gif", false)]
    public void OnlyKlipyMediaHostsServeAGif(string url, bool expected)
    {
        Assert.Equal(expected, KlipyMedia.IsGifUrl(url));
    }

    [Fact]
    public void PreviewsNameAGifInsteadOfShowingTheToken()
    {
        var token = GifShare.Compose(Item);

        Assert.Equal("GIF", ChatText.ListPreview(token));
        Assert.Equal("GIF", ChatText.QuotePreview(token, 0));
    }
}
