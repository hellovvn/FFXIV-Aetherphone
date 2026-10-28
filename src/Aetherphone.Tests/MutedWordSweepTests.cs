using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MutedWordSweepTests
{
    private const string Me = "me";
    private const string Friend = "friend";
    private const string Stranger = "stranger";

    private static readonly MutedWordMatcher Raid = Matcher("raid");

    [Fact]
    public void APostARechirpOrAQuoteHoldingTheWordLeavesTheFeedButYourOwnStay()
    {
        var raid = Post("p1", Stranger, "Raid tonight");

        Assert.True(SocialFeedStore.HidesPost(Raid, raid, Me));
        Assert.True(SocialFeedStore.HidesPost(Raid, Rechirp("r1", Friend, raid), Me));
        Assert.False(SocialFeedStore.HidesPost(Raid, Rechirp("r2", Me, raid), Me));
        Assert.False(SocialFeedStore.HidesPost(Raid, Post("p2", Me, "my raid notes"), Me));
        Assert.False(SocialFeedStore.HidesPost(Raid, Rechirp("r3", Friend, Post("p3", Me, "my raid")), Me));
        Assert.False(SocialFeedStore.HidesPost(Raid, Post("p4", Stranger, "raiders welcome"), Me));
        Assert.False(SocialFeedStore.HidesPost(Raid, Quote("q1", Friend, "look", raid), Me));
    }

    [Fact]
    public void AnEntryForPeopleYouDoNotFollowSparesFollowedAuthors()
    {
        var matcher = MutedWordMatcher.Compile(new[] { new MutedWordRule("raid", true, true, true, null) }, 0);

        Assert.False(SocialFeedStore.HidesPost(matcher, Post("p1", Friend, "raid") with { IsFollowing = true }, Me));
        Assert.True(SocialFeedStore.HidesPost(matcher, Post("p2", Stranger, "raid"), Me));
    }

    [Fact]
    public void AQuoteOfAMatchingChirpFoldsOnceAndNeverInsideYourOwnChirp()
    {
        var raid = Post("p1", Stranger, "#raid at reset");

        Assert.True(SocialFeedStore.FoldsQuote(Raid, Quote("q1", Friend, "look", raid), Me));
        Assert.False(SocialFeedStore.FoldsQuote(Raid, Quote("q2", Me, "look", raid), Me));
        Assert.False(SocialFeedStore.FoldsQuote(Raid, Quote("q3", Friend, "look", Post("p2", Me, "raid")), Me));
        Assert.False(SocialFeedStore.FoldsQuote(Raid,
            Quote("q4", Friend, "look", raid with { WordMuted = true }), Me));
        Assert.False(SocialFeedStore.FoldsQuote(Raid, Rechirp("r1", Friend, raid), Me));
    }

    [Fact]
    public void RepliesSweptAtOnceAreOnlyTheOnesAnEntryForAnyoneCovers()
    {
        var narrow = MutedWordMatcher.Compile(new[] { new MutedWordRule("raid", true, true, true, null) }, 0);

        Assert.True(SocialFeedStore.HidesReply(Raid, Reply("c1", Stranger, "RAID!"), Me));
        Assert.False(SocialFeedStore.HidesReply(Raid, Reply("c2", Me, "raid"), Me));
        Assert.False(SocialFeedStore.HidesReply(Raid, Reply("c3", Stranger, "glam"), Me));
        Assert.False(SocialFeedStore.HidesReply(narrow, Reply("c4", Stranger, "raid"), Me));
        Assert.False(SocialFeedStore.HidesReply(Matcher("raid", inFeeds: false), Reply("c5", Stranger, "raid"), Me));
    }

    [Fact]
    public void ApplyingAWordAnnouncesItsMatcherToEveryStoreAndAnInvalidOneStaysQuiet()
    {
        var words = new SocialMutedWords();
        var heard = new List<MutedWordMatcher>();
        words.Applied += heard.Add;
        words.Applied += heard.Add;

        words.Apply(new SaveMutedWordRequest("Raid", true, true, MutedWordAudiences.Anyone, null));
        words.Apply(new SaveMutedWordRequest("# raid", true, true, MutedWordAudiences.Anyone, null));

        Assert.Equal(2, heard.Count);
        Assert.Same(heard[0], heard[1]);
        Assert.True(heard[0].Hides("raid tonight", MutedWordScope.Feeds, true));
    }

    [Fact]
    public void TheListReplacesAnEditedEntryAddsANewOneFirstAndForgetsOnReset()
    {
        var words = new SocialMutedWords();
        Assert.True(words.TryBeginLoad());
        Assert.False(words.TryBeginLoad());
        words.Load(new[] { Entry("w1", "raid") });
        words.EndLoad();
        var loaded = words.Generation;

        words.Saved(Entry("w1", "raid", inNotifications: false));
        words.Saved(Entry("w2", "glam"));

        Assert.True(words.Loaded);
        Assert.Equal(new[] { "w2", "w1" }, words.Entries.Select(entry => entry.Id));
        Assert.False(words.Entries[1].InNotifications);
        Assert.True(words.Generation > loaded);

        words.Removed("w2");
        Assert.Equal(new[] { "w1" }, words.Entries.Select(entry => entry.Id));

        words.Reset();
        Assert.Empty(words.Entries);
        Assert.False(words.Loaded);
    }

    [Fact]
    public void AFailedLoadKeepsTheLastListAndCanBeRetried()
    {
        var words = new SocialMutedWords();
        Assert.True(words.TryBeginLoad());

        words.Load(null);
        words.EndLoad();

        Assert.True(words.Failed);
        Assert.False(words.Loaded);
        Assert.True(words.TryBeginLoad());
    }

    [Fact]
    public void TheWireShapesMatchTheServer()
    {
        const string list = "{\"items\":[{\"id\":\"w1\",\"phrase\":\"#raid\",\"inFeeds\":true,\"inNotifications\":false,"
            + "\"audience\":1,\"expiresAtUnix\":1900000000,\"createdAtUnix\":1800000000}]}";
        var parsed = JsonSerializer.Deserialize(list, AethernetJsonContext.Default.MutedWordList)!;
        var entry = Assert.Single(parsed.Items);
        Assert.Equal("#raid", entry.Phrase);
        Assert.False(entry.InNotifications);
        Assert.Equal(MutedWordAudiences.NotFollowing, entry.Audience);
        Assert.Equal(1900000000, entry.ExpiresAtUnix);

        var request = JsonSerializer.Serialize(new SaveMutedWordRequest("raid", true, false, 0, MutedWordDurations.Week),
            AethernetJsonContext.Default.SaveMutedWordRequest);
        Assert.Contains("\"phrase\":\"raid\"", request);
        Assert.Contains("\"inNotifications\":false", request);
        Assert.Contains("\"duration\":2", request);

        var post = JsonSerializer.Deserialize("{\"id\":\"p\",\"authorId\":\"a\",\"text\":\"\",\"wordMuted\":true}",
            AethernetJsonContext.Default.PostDto)!;
        Assert.True(post.WordMuted);
    }

    private static MutedWordMatcher Matcher(string phrase, bool inFeeds = true) =>
        MutedWordMatcher.Compile(new[] { new MutedWordRule(phrase, inFeeds, true, false, null) }, 0);

    private static MutedWordDto Entry(string id, string phrase, bool inNotifications = true) =>
        new(id, phrase, true, inNotifications, MutedWordAudiences.Anyone, null, 0);

    private static PostDto Post(string id, string authorId, string text) =>
        new(id, authorId, string.Empty, string.Empty, string.Empty, authorId, text, 0, Array.Empty<int>(), 0, -1, 0,
            null, 0, 0, null, 0, false);

    private static PostDto Rechirp(string id, string authorId, PostDto original) =>
        Post(id, authorId, string.Empty) with { RepostOfId = original.Id, ReferencedPost = original };

    private static PostDto Quote(string id, string authorId, string text, PostDto quoted) =>
        Post(id, authorId, text) with { QuotedPostId = quoted.Id, ReferencedPost = quoted };

    private static CommentDto Reply(string id, string authorId, string text) =>
        new(id, "post", authorId, string.Empty, string.Empty, authorId, null, text, 0, 0, false);
}
