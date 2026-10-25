using System.Text.Json;
using Aetherphone.Apps.Chirper;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChirperPollTests
{
    private const long Now = 1_800_000_000L;
    private const long Hour = 3600L;

    private const string PostWithHiddenPoll =
        "{\"id\":\"c1\",\"authorId\":\"a\",\"authorName\":\"\",\"authorWorld\":\"\",\"authorDisplayName\":\"A\","
        + "\"authorHandle\":\"a\",\"text\":\"Best primal?\",\"createdAtUnix\":100,\"reactionCounts\":[],"
        + "\"totalReactions\":0,\"myReaction\":-1,\"kind\":0,\"mediaUrl\":null,\"mediaWidth\":0,\"mediaHeight\":0,"
        + "\"authorAvatarUrl\":null,\"commentCount\":0,\"isFollowing\":false,\"scanStatus\":\"clean\","
        + "\"poll\":{\"options\":[\"Ifrit\",\"Shiva\"],\"counts\":null,\"totalVotes\":7,\"myVote\":-1,"
        + "\"endsAtUnix\":1800003600,\"ended\":false}}";

    private const string PostWithoutPoll =
        "{\"id\":\"c2\",\"authorId\":\"a\",\"authorName\":\"\",\"authorWorld\":\"\",\"authorDisplayName\":\"A\","
        + "\"authorHandle\":\"a\",\"text\":\"Hello\",\"createdAtUnix\":100,\"reactionCounts\":[],"
        + "\"totalReactions\":0,\"myReaction\":-1,\"kind\":0,\"mediaUrl\":null,\"mediaWidth\":0,\"mediaHeight\":0,"
        + "\"authorAvatarUrl\":null,\"commentCount\":0,\"isFollowing\":false}";

    private const string VoteResponse =
        "{\"options\":[\"Ifrit\",\"Shiva\",\"Titan\"],\"counts\":[2,5,1],\"totalVotes\":8,\"myVote\":1,"
        + "\"endsAtUnix\":1800003600,\"ended\":false}";

    private static PostPollDto Poll(int[]? counts = null, int myVote = -1, long endsAt = Now + Hour, bool ended = false,
        int optionCount = 2)
    {
        var options = new string[optionCount];
        for (var index = 0; index < optionCount; index++)
        {
            options[index] = "option" + index;
        }

        var total = 0;
        if (counts is not null)
        {
            for (var index = 0; index < counts.Length; index++)
            {
                total += counts[index];
            }
        }

        return new PostPollDto(options, counts, total, myVote, endsAt, ended);
    }

    [Fact]
    public void APostCarriesItsPollWithHiddenCounts()
    {
        var post = JsonSerializer.Deserialize(PostWithHiddenPoll, AethernetJsonContext.Default.PostDto)!;

        Assert.NotNull(post.Poll);
        Assert.Equal(new[] { "Ifrit", "Shiva" }, post.Poll!.Options);
        Assert.Null(post.Poll.Counts);
        Assert.Equal(7, post.Poll.TotalVotes);
        Assert.Equal(-1, post.Poll.MyVote);
        Assert.Equal(1_800_003_600L, post.Poll.EndsAtUnix);
        Assert.False(ChirperPolls.ResultsVisible(post.Poll));
    }

    [Fact]
    public void APostFromAnOlderServerHasNoPoll()
    {
        var post = JsonSerializer.Deserialize(PostWithoutPoll, AethernetJsonContext.Default.PostDto)!;

        Assert.Null(post.Poll);
    }

    [Fact]
    public void TheVoteResponseParsesWithVisibleCounts()
    {
        var poll = JsonSerializer.Deserialize(VoteResponse, AethernetJsonContext.Default.PostPollDto)!;

        Assert.Equal(new[] { 2, 5, 1 }, poll.Counts);
        Assert.Equal(1, poll.MyVote);
        Assert.True(ChirperPolls.ResultsVisible(poll));
    }

    [Fact]
    public void ACreateRequestSendsThePollInCamelCase()
    {
        var request = new CreatePostRequest("Best primal?", Poll: new PostPollInput(new[] { "Ifrit", "Shiva" }, 24));

        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CreatePostRequest);

        Assert.Contains("\"poll\":{\"options\":[\"Ifrit\",\"Shiva\"],\"durationHours\":24}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void APendingVoteShowsOnEveryUnvotedCopyAndDisappearsWhenDropped()
    {
        var forYouCopy = Poll();
        var latestCopy = Poll();

        Assert.Equal(1, ChirperPolls.ShownVote(forYouCopy, true, 1));
        Assert.Equal(1, ChirperPolls.ShownVote(latestCopy, true, 1));
        Assert.Equal(-1, ChirperPolls.ShownVote(latestCopy, false, 1));
        Assert.Equal(0, ChirperPolls.ShownVote(Poll(new[] { 1, 0 }, myVote: 0), true, 1));
    }

    [Fact]
    public void TheServerAnswerLandsOnEveryCopyOfTheChirp()
    {
        var counted = Poll(new[] { 2, 5 }, myVote: 1);
        var feedCopy = Post("c1", Poll());
        var repostedCopy = Post("c1", Poll());

        var updatedFeed = ChirperPolls.WithPoll(feedCopy, counted);
        var updatedRepost = ChirperPolls.WithPoll(repostedCopy, counted);

        Assert.Same(counted, updatedFeed.Poll);
        Assert.Same(counted, updatedRepost.Poll);
        Assert.Same(updatedFeed, ChirperPolls.WithPoll(updatedFeed, Poll(new[] { 2, 5 }, myVote: 1)));
    }

    [Fact]
    public void TwoCopiesWithTheSameNumbersAreTheSameState()
    {
        var first = Poll(new[] { 3, 1 }, myVote: 0);
        var second = Poll(new[] { 3, 1 }, myVote: 0);

        Assert.NotSame(first, second);
        Assert.True(ChirperPolls.SameState(first, second));
        Assert.False(ChirperPolls.SameState(first, Poll(new[] { 3, 2 }, myVote: 0)));
        Assert.False(ChirperPolls.SameState(first, Poll(new[] { 3, 1 })));
        Assert.False(ChirperPolls.SameState(Poll(), Poll(new[] { 0, 0 })));
        Assert.False(ChirperPolls.SameState(Poll(), Poll(ended: true)));
    }

    [Fact]
    public void AnEndedPollAsksAgainOnAGrowingBackoff()
    {
        Assert.Equal(5_000L, ChirperPolls.RefreshBackoffMilliseconds(1));
        Assert.Equal(15_000L, ChirperPolls.RefreshBackoffMilliseconds(2));
        Assert.Equal(45_000L, ChirperPolls.RefreshBackoffMilliseconds(3));
        Assert.Equal(300_000L, ChirperPolls.RefreshBackoffMilliseconds(10));
        Assert.Equal(300_000L, ChirperPolls.RefreshBackoffMilliseconds(int.MaxValue));
    }

    [Fact]
    public void ACopyWithTheSameNumbersKeepsTheCachedLabels()
    {
        var view = new ChirperPollView();
        view.Sync(Poll(new[] { 3, 1 }, myVote: 0, endsAt: Now + 3 * Hour), Now);
        var percents = view.Percents;
        var first = view.Percents[0];
        var footer = view.Footer;

        view.Sync(Poll(new[] { 3, 1 }, myVote: 0, endsAt: Now + 3 * Hour), Now);

        Assert.Same(percents, view.Percents);
        Assert.Same(first, view.Percents[0]);
        Assert.Same(footer, view.Footer);

        view.Sync(Poll(new[] { 3, 2 }, myVote: 0, endsAt: Now + 3 * Hour), Now);
        Assert.Equal("60%", view.Percents[0]);
    }

    private static PostDto Post(string id, PostPollDto poll) =>
        new(id, "author", string.Empty, string.Empty, "Author", "author", "question", 100, Array.Empty<int>(), 0, -1, 0,
            null, 0, 0, null, 0, false, Poll: poll);

    [Fact]
    public void OnlyANonAuthorWhoHasNotVotedOnAnOpenPollCanVote()
    {
        Assert.True(ChirperPolls.CanVote(Poll(), false, Now));
        Assert.False(ChirperPolls.CanVote(Poll(), true, Now));
        Assert.False(ChirperPolls.CanVote(Poll(myVote: 1), false, Now));
        Assert.False(ChirperPolls.CanVote(Poll(endsAt: Now - 1), false, Now));
        Assert.False(ChirperPolls.CanVote(Poll(ended: true), false, Now));
    }

    [Fact]
    public void ThePollEndsWhenTheClockPassesTheEndTime()
    {
        var poll = Poll(endsAt: Now + 10);

        Assert.False(ChirperPolls.HasEnded(poll, Now));
        Assert.True(ChirperPolls.HasEnded(poll, Now + 10));
    }

    [Fact]
    public void AFreshDraftNeedsBothOptionsFilled()
    {
        var draft = new ChirperPollDraft();
        draft.Open();

        Assert.True(draft.Active);
        Assert.Equal(2, draft.OptionCount);
        Assert.Equal(24, draft.DurationHours);
        Assert.Equal(PollDraftIssue.EmptyOption, draft.Validate());

        draft.Option(0) = "Ifrit";
        draft.Option(1) = "   ";
        Assert.Equal(PollDraftIssue.EmptyOption, draft.Validate());

        draft.Option(1) = "Shiva";
        Assert.Equal(PollDraftIssue.None, draft.Validate());
    }

    [Fact]
    public void ADraftRefusesDuplicatesAndLongOptions()
    {
        var draft = new ChirperPollDraft();
        draft.Open();
        draft.Option(0) = "Yes";
        draft.Option(1) = " yes ";
        Assert.Equal(PollDraftIssue.DuplicateOption, draft.Validate());

        draft.Option(1) = new string('a', ChirperPolls.MaxOptionLength + 1);
        Assert.Equal(PollDraftIssue.OptionTooLong, draft.Validate());

        draft.Option(1) = new string('a', ChirperPolls.MaxOptionLength);
        Assert.Equal(PollDraftIssue.None, draft.Validate());
    }

    [Fact]
    public void ADraftKeepsTwoToFourOptionsAndShiftsOnRemove()
    {
        var draft = new ChirperPollDraft();
        draft.Open();
        draft.RemoveOption(0);
        Assert.Equal(2, draft.OptionCount);

        draft.AddOption();
        draft.AddOption();
        draft.AddOption();
        Assert.Equal(4, draft.OptionCount);
        Assert.False(draft.CanAddOption);

        draft.Option(0) = "A";
        draft.Option(1) = "B";
        draft.Option(2) = "C";
        draft.Option(3) = "D";
        draft.RemoveOption(1);
        Assert.Equal(3, draft.OptionCount);
        Assert.Equal("C", draft.Option(1));
        Assert.Equal("D", draft.Option(2));
    }

    [Fact]
    public void ADraftSendsTrimmedOneLineOptionsAndTheChosenLength()
    {
        var draft = new ChirperPollDraft();
        draft.Open();
        draft.Option(0) = "  Ifrit ";
        draft.Option(1) = "Shi\tva";
        draft.SelectDuration(4);

        var input = draft.ToInput();

        Assert.Equal(new[] { "Ifrit", "Shi va" }, input.Options);
        Assert.Equal(168, input.DurationHours);
    }

    [Fact]
    public void ADraftClampsTheDurationAndResets()
    {
        var draft = new ChirperPollDraft();
        draft.Open();
        draft.SelectDuration(99);
        Assert.Equal(168, draft.DurationHours);

        draft.SelectDuration(-3);
        Assert.Equal(1, draft.DurationHours);

        draft.Option(0) = "kept?";
        draft.Reset();
        Assert.False(draft.Active);
        Assert.Equal(string.Empty, draft.Option(0));
        Assert.Equal(24, draft.DurationHours);
    }

    [Fact]
    public void SharesAlwaysAddUpToAHundred()
    {
        Span<int> percents = stackalloc int[3];

        VoteShares.Percentages(new[] { 1, 1, 1 }, percents);

        Assert.Equal(100, percents[0] + percents[1] + percents[2]);
        Assert.Equal(34, percents[0]);
    }

    [Fact]
    public void NoVotesMeansNoShareAndNoLeader()
    {
        Span<int> percents = stackalloc int[2];
        Span<bool> leaders = stackalloc bool[2];

        VoteShares.Percentages(new[] { 0, 0 }, percents);
        var leaderCount = VoteShares.Leaders(new[] { 0, 0 }, leaders);

        Assert.Equal(0, percents[0] + percents[1]);
        Assert.Equal(0, leaderCount);
    }

    [Fact]
    public void TheViewCachesPercentagesAndRebuildsOnlyOnANewPoll()
    {
        var view = new ChirperPollView();
        var poll = Poll(new[] { 3, 1 }, myVote: 0, endsAt: Now + 3 * Hour);

        view.Sync(poll, Now);
        var percents = view.Percents;
        var first = view.Percents[0];
        view.Sync(poll, Now + 1);

        Assert.Equal("75%", first);
        Assert.Equal("25%", view.Percents[1]);
        Assert.Same(first, view.Percents[0]);
        Assert.Same(percents, view.Percents);
        Assert.True(view.Leaders[0]);
        Assert.False(view.Ended);
        Assert.StartsWith("4 votes", view.Footer, StringComparison.Ordinal);
    }

    [Fact]
    public void TheViewShowsFinalResultsOnceThePollEnds()
    {
        var view = new ChirperPollView();
        var poll = Poll(new[] { 1, 0 }, endsAt: Now - 1);

        view.Sync(poll, Now);

        Assert.True(view.Ended);
        Assert.EndsWith("Final results", view.Footer, StringComparison.Ordinal);
    }
}
