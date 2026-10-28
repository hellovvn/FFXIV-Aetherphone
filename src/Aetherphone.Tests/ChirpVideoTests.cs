using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Reels;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChirpVideoTests
{
    [Fact]
    public void TheCreateRequestCarriesBothUploadKeys()
    {
        var json = JsonSerializer.Serialize(new CreatePostRequest("watch this", "quoted-1", Sensitive: true,
            ReplyPolicy: 2, VideoKey: "reels/me/clip.mp4", PosterKey: "reels/me/cover.jpg"),
            AethernetJsonContext.Default.CreatePostRequest);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("reels/me/clip.mp4", root.GetProperty("videoKey").GetString());
        Assert.Equal("reels/me/cover.jpg", root.GetProperty("posterKey").GetString());
        Assert.Equal("quoted-1", root.GetProperty("quotedPostId").GetString());
        Assert.True(root.GetProperty("sensitive").GetBoolean());
    }

    [Fact]
    public void AChirpWithAVideoReadsBackFromTheServerShape()
    {
        const string json = """{"id":"c1","authorId":"a1","authorName":"","authorWorld":"","authorDisplayName":"Ann","authorHandle":"ann","text":"look","createdAtUnix":1,"reactionCounts":[],"totalReactions":0,"myReaction":-1,"kind":0,"mediaUrl":null,"mediaWidth":1280,"mediaHeight":720,"authorAvatarUrl":null,"commentCount":0,"isFollowing":false,"scanStatus":"clean","mediaUrls":[],"video":{"url":"https://media/reels/a1/v.mp4","posterUrl":"https://media/reels/a1/p.jpg","durationMs":42000,"width":1280,"height":720}}""";
        var post = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.PostDto)!;

        Assert.Equal(0, post.Kind);
        Assert.NotNull(post.Video);
        Assert.Equal("https://media/reels/a1/p.jpg", post.Video!.PosterUrl);
        Assert.Equal(42_000, post.Video.DurationMs);
        Assert.Empty(post.MediaUrls!);
    }

    [Fact]
    public async Task TheReelPublisherHandsTheCreateToTheAppThatShares()
    {
        CreateReelRequest? handed = null;
        var created = new PostDto("c1", "a1", string.Empty, string.Empty, "Ann", "ann", "look", 1, Array.Empty<int>(), 0,
            -1, 0, null, 0, 0, null, 0, false);
        var publisher = new AethernetReelPublisher(null!, (request, _, _) =>
        {
            handed = request;
            return Task.FromResult<PostDto?>(created);
        });

        var result = await publisher.CreateAsync(new CreateReelRequest("look", "reels/me/v.mp4", "reels/me/p.jpg",
            Sensitive: true), CancellationToken.None, _ => { });

        Assert.Same(created, result);
        Assert.Equal("reels/me/v.mp4", handed!.VideoKey);
        Assert.Equal("reels/me/p.jpg", handed.PosterKey);
        Assert.True(handed.Sensitive);
    }

    [Fact]
    public void ThePillNamesEachAppsOwnPostingStep()
    {
        Assert.Equal(Loc.T(L.Chirper.VideoPosting),
            ReelShareText.Status(ReelShareStage.Posting, 1f, L.Chirper.VideoPosting));
        Assert.Equal(Loc.T(L.Aethergram.ReelPosting),
            ReelShareText.Status(ReelShareStage.Posting, 1f, L.Aethergram.ReelPosting));
        Assert.Equal(Loc.T(L.Aethergram.ReelConverting, 40),
            ReelShareText.Status(ReelShareStage.Converting, 0.4f, L.Chirper.VideoPosting));
    }
}
