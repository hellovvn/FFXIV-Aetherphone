using Aetherphone.Core.Gifs;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LogUrlTests
{
    private static readonly GifSettings Settings = new("secret-app-key", "c0ffee", "high");

    [Fact]
    public void AKlipySearchLogsTheHostAndEndpointButNeverTheKeyOrTheQuery()
    {
        var url = KlipyUrls.Search(Settings, "my private words", 3, "us");

        var logged = LogUrl.Of(url);

        Assert.Equal("https://api.klipy.com/api/v1/***/gifs/search", logged);
        Assert.DoesNotContain("secret-app-key", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("private", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("c0ffee", logged, StringComparison.Ordinal);
        Assert.Equal(logged, LogUrl.Of(new Uri(url)));
    }

    [Fact]
    public void TrendingAndShareAreRedactedTheSameWay()
    {
        Assert.Equal("https://api.klipy.com/api/v1/***/gifs/trending",
            LogUrl.Of(KlipyUrls.Trending(Settings, 1, "us")));
        Assert.Equal("https://api.klipy.com/api/v1/***/gifs/share/hello-hi-662",
            LogUrl.Of(new Uri(KlipyUrls.Share(Settings, "hello-hi-662"))));
    }

    [Fact]
    public void KlipyMediaLosesItsQueryAndOtherHostsAreLoggedAsBefore()
    {
        Assert.Equal("https://static.klipy.com/ii/a/b.gif", LogUrl.Of("https://static.klipy.com/ii/a/b.gif?t=1"));
        Assert.Equal("https://api.aetherphone.net/posts?cursor=abc",
            LogUrl.Of("https://api.aetherphone.net/posts?cursor=abc"));
        Assert.Equal("https://universalis.app/api/v2/Phoenix/5?entries=5",
            LogUrl.Of(new Uri("https://universalis.app/api/v2/Phoenix/5?entries=5")));
        Assert.Equal("/relative/path", LogUrl.Of("/relative/path"));
        Assert.Equal(string.Empty, LogUrl.Of((string?)null));
        Assert.Equal(string.Empty, LogUrl.Of((Uri?)null));
    }
}
