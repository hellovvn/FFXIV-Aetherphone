using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Media;
using Xunit;

namespace Aetherphone.Tests;

public sealed class KlipyRequestTests
{
    private static readonly GifSettings Settings = new("app/key", "c0ffee", "high");

    [Fact]
    public void TrendingCarriesThePageTheCustomerTheLocaleAndTheStrictFilter()
    {
        Assert.Equal("https://api.klipy.com/api/v1/app%2Fkey/gifs/trending?page=2&per_page=24&customer_id=c0ffee"
                     + "&locale=br&content_filter=high&format_filter=gif,webp",
            KlipyUrls.Trending(Settings, 2, "br"));
    }

    [Fact]
    public void SearchEscapesTheQueryAndNeverAsksForPageZero()
    {
        Assert.Equal("https://api.klipy.com/api/v1/app%2Fkey/gifs/search?q=happy%20cat%20%26%20dog&page=1"
                     + "&per_page=24&customer_id=c0ffee&locale=us&content_filter=high&format_filter=gif,webp",
            KlipyUrls.Search(Settings, "happy cat & dog", 0, "us"));
    }

    [Fact]
    public void ShareTargetsTheSlug()
    {
        Assert.Equal("https://api.klipy.com/api/v1/app%2Fkey/gifs/share/hello%20hi", KlipyUrls.Share(Settings, "hello hi"));
    }

    [Fact]
    public void TheShareBodyUsesKlipysFieldNames()
    {
        var json = JsonSerializer.Serialize(new KlipyShareRequest("c0ffee", "cats"),
            KlipyJsonContext.Default.KlipyShareRequest);

        Assert.Equal("{\"customer_id\":\"c0ffee\",\"q\":\"cats\"}", json);
    }

    [Theory]
    [InlineData("en-US", "us")]
    [InlineData("pt-BR", "br")]
    [InlineData("ja-JP", "jp")]
    [InlineData("zh-CN", "cn")]
    [InlineData("de-DE", "de")]
    [InlineData("en", "us")]
    [InlineData("", "us")]
    [InlineData("sr-Latn", "us")]
    public void TheLocaleIsTheCulturesCountry(string cultureName, string expected)
    {
        Assert.Equal(expected, KlipyUrls.LocaleOf(cultureName));
    }

    [Fact]
    public void SettingsComeOnlyFromACompleteEnabledConfig()
    {
        Assert.Equal(new GifSettings("k", "c", "high"), GifSettings.From(new GifConfigDto(true, "k", "c", "high")));
        Assert.Null(GifSettings.From(null));
        Assert.Null(GifSettings.From(new GifConfigDto(false, "k", "c", "high")));
        Assert.Null(GifSettings.From(new GifConfigDto(true, null, "c", "high")));
        Assert.Null(GifSettings.From(new GifConfigDto(true, "k", " ", "high")));
        Assert.Null(GifSettings.From(new GifConfigDto(true, "k", "c", null)));
    }

    [Fact]
    public void TheServerConfigParsesWhetherTheFeatureIsOnOrOff()
    {
        var on = JsonSerializer.Deserialize("{\"enabled\":true,\"appKey\":\"k\",\"customerId\":\"c\",\"contentFilter\":\"high\"}",
            AethernetJsonContext.Default.GifConfigDto);
        var off = JsonSerializer.Deserialize("{\"enabled\":false}", AethernetJsonContext.Default.GifConfigDto);

        Assert.Equal(new GifConfigDto(true, "k", "c", "high"), on);
        Assert.Equal(new GifConfigDto(false), off);
    }

    [Fact]
    public void AGifChirpFromTheServerRendersThroughThePhotoPathFromGifUrl()
    {
        const string gif = "https://static.klipy.com/ii/a/b.gif";
        var post = JsonSerializer.Deserialize("{\"id\":\"p1\",\"authorId\":\"u1\",\"authorName\":\"\",\"authorWorld\":\"\","
            + "\"authorDisplayName\":\"A\",\"authorHandle\":\"a\",\"text\":\"\",\"createdAtUnix\":1,\"reactionCounts\":[],"
            + "\"totalReactions\":0,\"myReaction\":-1,\"kind\":0,\"mediaUrl\":null,\"mediaWidth\":498,\"mediaHeight\":280,"
            + "\"authorAvatarUrl\":null,\"commentCount\":0,\"isFollowing\":false,\"scanStatus\":\"clean\",\"mediaUrls\":[],"
            + "\"gifUrl\":\"" + gif + "\"}", AethernetJsonContext.Default.PostDto);
        var photo = JsonSerializer.Deserialize("{\"id\":\"p2\",\"authorId\":\"u1\",\"authorName\":\"\",\"authorWorld\":\"\","
            + "\"authorDisplayName\":\"A\",\"authorHandle\":\"a\",\"text\":\"\",\"createdAtUnix\":1,\"reactionCounts\":[],"
            + "\"totalReactions\":0,\"myReaction\":-1,\"kind\":0,\"mediaUrl\":\"https://media.test/a.png\",\"mediaWidth\":1,"
            + "\"mediaHeight\":1,\"authorAvatarUrl\":null,\"commentCount\":0,\"isFollowing\":false,\"scanStatus\":\"clean\","
            + "\"mediaUrls\":[\"https://media.test/a.png\"]}", AethernetJsonContext.Default.PostDto);

        Assert.NotNull(post);
        Assert.Null(post.MediaUrl);
        var photos = PostMedia.Photos(post);
        Assert.Equal(new[] { gif }, photos);
        Assert.Same(photos, PostMedia.Photos(post));
        Assert.True(GifMedia.IsGif(photos[0]));
        Assert.Equal(new[] { "https://media.test/a.png" }, PostMedia.Photos(photo!));
        Assert.Null(photo!.GifMedia);
        Assert.DoesNotContain("gifMedia", JsonSerializer.Serialize(post, AethernetJsonContext.Default.PostDto),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AGifReplyFromTheServerExposesItsMediaFromGifUrl()
    {
        var reply = JsonSerializer.Deserialize("{\"id\":\"c1\",\"postId\":\"p1\",\"authorId\":\"u1\",\"authorName\":\"\","
            + "\"authorDisplayName\":\"A\",\"authorHandle\":\"a\",\"authorAvatarUrl\":null,\"text\":\"\","
            + "\"createdAtUnix\":1,\"likeCount\":0,\"liked\":false,\"mediaUrl\":null,\"mediaWidth\":220,"
            + "\"mediaHeight\":124,\"gifUrl\":\"https://static.klipy.com/ii/a/c.gif\"}",
            AethernetJsonContext.Default.CommentDto);

        Assert.Equal("https://static.klipy.com/ii/a/c.gif", reply!.Media);
        Assert.Equal("https://media.test/x.png", (reply with { MediaUrl = "https://media.test/x.png", GifUrl = null }).Media);
    }

    [Fact]
    public void GifPostsAndRepliesSendTheUrlInGifUrl()
    {
        var post = JsonSerializer.Serialize(
            new CreatePostRequest(string.Empty, "quoted", MediaWidth: 498, MediaHeight: 280, GifUrl: "https://static.klipy.com/a.gif"),
            AethernetJsonContext.Default.CreatePostRequest);
        var reply = JsonSerializer.Serialize(
            new CreateCommentRequest("hi", MediaWidth: 220, MediaHeight: 124, GifUrl: "https://static.klipy.com/b.gif"),
            AethernetJsonContext.Default.CreateCommentRequest);

        using var postDocument = JsonDocument.Parse(post);
        using var replyDocument = JsonDocument.Parse(reply);
        Assert.Equal("https://static.klipy.com/a.gif", postDocument.RootElement.GetProperty("gifUrl").GetString());
        Assert.Equal("quoted", postDocument.RootElement.GetProperty("quotedPostId").GetString());
        Assert.Equal(498, postDocument.RootElement.GetProperty("mediaWidth").GetInt32());
        Assert.Equal("https://static.klipy.com/b.gif", replyDocument.RootElement.GetProperty("gifUrl").GetString());
        Assert.Equal(JsonValueKind.Null, replyDocument.RootElement.GetProperty("mediaKey").ValueKind);
    }
}
