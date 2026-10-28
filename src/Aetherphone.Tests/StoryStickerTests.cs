using System.Numerics;
using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StoryStickerTests
{
    private const string ServerStory =
        "{\"id\":\"s1\",\"authorId\":\"a1\",\"caption\":\"\",\"mediaUrl\":\"https://cdn/x.jpg\",\"mediaWidth\":1080,"
        + "\"mediaHeight\":1920,\"createdAtUnix\":1,\"expiresAtUnix\":2,\"seen\":false,\"viewCount\":0,"
        + "\"stickers\":[{\"id\":\"p1\",\"kind\":0,\"x\":0.5,\"y\":0.3,\"scale\":1.25,\"text\":\"Tea?\","
        + "\"options\":[\"Yes\",\"No\"],\"counts\":[3,1],\"myVote\":1,\"answered\":false,\"userId\":null,\"answerCount\":0},"
        + "{\"id\":\"q1\",\"kind\":1,\"x\":0.5,\"y\":0.6,\"scale\":1,\"text\":\"Ask me\",\"answered\":true},"
        + "{\"id\":\"m1\",\"kind\":2,\"x\":0.2,\"y\":0.9,\"scale\":0.5,\"text\":\"cora\",\"userId\":\"u3\"}]}";

    [Fact]
    public void StickersSurviveTheWireFromTheServer()
    {
        var story = JsonSerializer.Deserialize(ServerStory, AethernetJsonContext.Default.StoryDto);

        Assert.NotNull(story);
        var stickers = story.Stickers!;
        Assert.Equal(3, stickers.Length);
        Assert.Equal(StoryStickerKinds.Poll, stickers[0].Kind);
        Assert.Equal(new[] { "Yes", "No" }, stickers[0].Options);
        Assert.Equal(new[] { 3, 1 }, stickers[0].Counts);
        Assert.Equal(1, stickers[0].MyVote);
        Assert.Equal(1.25f, stickers[0].Scale);
        Assert.True(stickers[1].Answered);
        Assert.Equal(-1, stickers[1].MyVote);
        Assert.Equal("u3", stickers[2].UserId);
    }

    [Fact]
    public void AStoryFromAnOlderServerHasNoStickers()
    {
        var story = JsonSerializer.Deserialize(
            "{\"id\":\"s1\",\"authorId\":\"a1\",\"caption\":\"\",\"mediaUrl\":\"\",\"mediaWidth\":0,\"mediaHeight\":0,"
            + "\"createdAtUnix\":1,\"expiresAtUnix\":2,\"seen\":false,\"viewCount\":0}",
            AethernetJsonContext.Default.StoryDto);

        Assert.NotNull(story);
        Assert.Null(story.Stickers);
        Assert.False(StoryStickerList.HasResults(story));
    }

    [Fact]
    public void ACreatedStorySendsItsStickersInTheServerShape()
    {
        var request = new CreateStoryRequest("hi", "key", 1080, 1920, Stickers: new[]
        {
            new StoryStickerInput(StoryStickerKinds.Poll, 0.5f, 0.25f, 1.5f, "Tea?", new[] { "Yes", "No" }),
            new StoryStickerInput(StoryStickerKinds.Mention, 0.1f, 0.9f, 1f, "cora"),
        });

        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CreateStoryRequest);

        Assert.Contains("\"stickers\":[{\"kind\":0,\"x\":0.5,\"y\":0.25,\"scale\":1.5,\"text\":\"Tea?\",\"options\":[\"Yes\",\"No\"]}", json);
        Assert.Contains("{\"kind\":2,\"x\":0.1,\"y\":0.9,\"scale\":1,\"text\":\"cora\",\"options\":null}", json);
    }

    [Fact]
    public void VotesAnswersAndResponsesUseTheServerFieldNames()
    {
        Assert.Equal("{\"option\":1}",
            JsonSerializer.Serialize(new StoryPollVoteRequest(1), AethernetJsonContext.Default.StoryPollVoteRequest));
        Assert.Equal("{\"text\":\"hello\"}",
            JsonSerializer.Serialize(new StoryAnswerRequest("hello"), AethernetJsonContext.Default.StoryAnswerRequest));

        var page = JsonSerializer.Deserialize(
            "{\"items\":[{\"id\":\"r1\",\"kind\":1,\"userId\":\"u1\",\"displayName\":\"Bram\",\"handle\":\"bram\","
            + "\"avatarUrl\":null,\"createdAtUnix\":5,\"option\":-1,\"text\":\"sunsets\"},{\"id\":\"u2\",\"kind\":0,"
            + "\"userId\":\"u2\",\"displayName\":\"\",\"handle\":\"cora\",\"avatarUrl\":null,\"createdAtUnix\":4,"
            + "\"option\":1,\"text\":\"No\"}],\"voteCount\":1,\"answerCount\":1,\"nextCursor\":\"c\"}",
            AethernetJsonContext.Default.StoryResponsesPage);

        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Length);
        Assert.Equal("sunsets", page.Items[0].Text);
        Assert.Equal(1, page.Items[1].Option);
        Assert.Equal(1, page.VoteCount);
        Assert.Equal("c", page.NextCursor);
    }

    [Fact]
    public void AMentionedStoryArrivesWithItsAuthorForTheViewerHeader()
    {
        var item = JsonSerializer.Deserialize("{\"story\":" + ServerStory + ",\"authorDisplayName\":\"Ayla\","
            + "\"authorHandle\":\"ayla\",\"authorAvatarUrl\":null}", AethernetJsonContext.Default.StoryItem);

        Assert.NotNull(item);
        Assert.Equal("s1", item.Story.Id);
        Assert.Equal(3, item.Story.Stickers!.Length);
        Assert.Equal("Ayla", item.AuthorDisplayName);
        Assert.Equal("ayla", item.AuthorHandle);
        Assert.Null(item.AuthorAvatarUrl);
    }

    [Fact]
    public void ThePhotoCoversAWideStageByOverflowingTopAndBottom()
    {
        var stage = new Rect(new Vector2(0f, 0f), new Vector2(450f, 600f));

        var photo = StoryStickerLayout.CoverRect(stage, 9f / 16f);

        Assert.Equal(450f, photo.Width, 3);
        Assert.Equal(800f, photo.Height, 3);
        Assert.Equal(stage.Center, photo.Center);
    }

    [Fact]
    public void ThePhotoCoversATallStageByOverflowingTheSides()
    {
        var stage = new Rect(new Vector2(10f, 20f), new Vector2(410f, 920f));

        var photo = StoryStickerLayout.CoverRect(stage, 9f / 16f);

        Assert.Equal(900f, photo.Height, 3);
        Assert.Equal(506.25f, photo.Width, 3);
        Assert.Equal(stage.Center.X, photo.Center.X, 3);
    }

    [Fact]
    public void AStickerKeepsItsShareOfThePhotoAtAnySize()
    {
        var preview = new Rect(Vector2.Zero, new Vector2(150f, 266.67f));
        var viewer = new Rect(Vector2.Zero, new Vector2(450f, 800f));

        var previewZoom = StoryStickerLayout.Zoom(preview, 1.5f, 1f);
        var viewerZoom = StoryStickerLayout.Zoom(viewer, 1.5f, 1f);

        Assert.Equal(3f, viewerZoom / previewZoom, 3);
        Assert.Equal(1f, StoryStickerLayout.Zoom(new Rect(Vector2.Zero, new Vector2(450f, 800f)), 1.5f, 1f), 3);
        Assert.Equal(2f, StoryStickerLayout.Zoom(viewer, 1.5f, 2f), 3);
        Assert.Equal(2f, StoryStickerLayout.Zoom(viewer, 1.5f, 9f), 3);
    }

    [Fact]
    public void TheWheelResizesInTenthsWithinTheAllowedRange()
    {
        Assert.Equal(1.1f, StoryStickerLayout.Wheel(1f, 1f), 3);
        Assert.Equal(0.8f, StoryStickerLayout.Wheel(1f, -2f), 3);
        Assert.Equal(StoryStickerLayout.MaxScale, StoryStickerLayout.Wheel(1.95f, 3f), 3);
        Assert.Equal(StoryStickerLayout.MinScale, StoryStickerLayout.Wheel(0.55f, -1f), 3);
        Assert.Equal(1f, StoryStickerLayout.ClampScale(float.NaN), 3);
    }

    [Fact]
    public void DraggingMovesByThePhotosShareAndStaysOnThePhoto()
    {
        var photo = new Rect(new Vector2(100f, 100f), new Vector2(300f, 500f));

        var moved = StoryStickerLayout.Drag(photo, new Vector2(0.5f, 0.5f), new Vector2(50f, -100f));
        var pinned = StoryStickerLayout.Drag(photo, new Vector2(0.9f, 0.1f), new Vector2(500f, -500f));

        Assert.Equal(0.75f, moved.X, 3);
        Assert.Equal(0.25f, moved.Y, 3);
        Assert.Equal(new Vector2(1f, 0f), pinned);
        Assert.Equal(PhotoTagGeometry.ToScreen(photo, 0.75f, 0.25f), new Vector2(250f, 200f));
    }

    [Fact]
    public void NewStickersCascadeSoTheyDoNotStackExactly()
    {
        var first = StoryStickerLayout.Spawn(0);
        var second = StoryStickerLayout.Spawn(1);

        Assert.Equal(StoryStickerLayout.DefaultX, first.X);
        Assert.True(second.Y > first.Y);
        Assert.Equal(first, StoryStickerLayout.Spawn(5));
    }

    [Fact]
    public void ResultsShowOnlyWithCountsAndOnlyNonAuthorsCanVoteOnce()
    {
        var hidden = new StoryStickerDto("p", StoryStickerKinds.Poll, 0.5f, 0.5f, 1f, Options: new[] { "A", "B" });
        var shown = hidden with { Counts = new[] { 2, 1 }, MyVote = 0 };

        Assert.False(StoryPollTally.ShowsResults(hidden));
        Assert.True(StoryPollTally.ShowsResults(shown));
        Assert.True(StoryPollTally.CanVote(hidden, false));
        Assert.False(StoryPollTally.CanVote(hidden, true));
        Assert.False(StoryPollTally.CanVote(shown, false));
        Assert.Equal(3, StoryPollTally.Total(shown.Counts));
        Assert.Equal(2f / 3f, StoryPollTally.Fraction(shown.Counts!, 0), 3);
        Assert.Equal(0f, StoryPollTally.Fraction(new[] { 0, 0 }, 1));
        Assert.Equal(0f, StoryPollTally.Fraction(shown.Counts!, 5));
    }

    [Fact]
    public void PercentagesOfTwoOptionsAlwaysAddUpToOneHundred()
    {
        Span<int> percents = stackalloc int[2];

        VoteShares.Percentages(new[] { 2, 1 }, percents);

        Assert.Equal(67, percents[0]);
        Assert.Equal(33, percents[1]);
        VoteShares.Percentages(new[] { 0, 0 }, percents);
        Assert.Equal(0, percents[0] + percents[1]);
    }

    [Fact]
    public void DraftsHoldOnePollOneQuestionAndTenMentions()
    {
        var drafts = new StoryStickerDrafts();

        Assert.Equal(0, drafts.Add(StoryStickerDraft.Poll(Vector2.Zero, "q", "Yes", "No")));
        Assert.Equal(-1, drafts.Add(StoryStickerDraft.Poll(Vector2.Zero, "q", "Yes", "No")));
        Assert.Equal(1, drafts.Add(StoryStickerDraft.Question(Vector2.Zero, "Ask")));
        Assert.False(drafts.CanAdd(StoryStickerKinds.Question));
        for (var index = 0; index < StoryStickerDrafts.MaxMentions; index++)
        {
            Assert.True(drafts.Add(StoryStickerDraft.Mention(Vector2.Zero, "player" + index)) >= 0);
        }

        Assert.False(drafts.CanAdd(StoryStickerKinds.Mention));
        Assert.True(drafts.HasMention("PLAYER3"));
        drafts.RemoveAt(drafts.IndexOf(StoryStickerKinds.Poll));
        Assert.True(drafts.CanAdd(StoryStickerKinds.Poll));
    }

    [Fact]
    public void DraftsBecomeServerInputsWithClippedTextAndClampedLayout()
    {
        var drafts = new StoryStickerDrafts();
        var poll = drafts.Add(StoryStickerDraft.Poll(new Vector2(0.5f, 0.5f), string.Empty, "Yes", "No"));
        drafts.SetPoll(poll, "  Tea?  ", new string('a', 40), " No ");
        drafts.MoveTo(poll, new Vector2(1.5f, -1f));
        drafts.ScaleTo(poll, 5f);
        drafts.Add(StoryStickerDraft.Mention(new Vector2(0.2f, 0.8f), "cora"));

        var inputs = drafts.ToInputs()!;

        Assert.Equal(2, inputs.Length);
        Assert.Equal("Tea?", inputs[0].Text);
        Assert.Equal(StoryStickerDrafts.PollOptionMaxLength, inputs[0].Options![0].Length);
        Assert.Equal("No", inputs[0].Options![1]);
        Assert.Equal(1f, inputs[0].X);
        Assert.Equal(0f, inputs[0].Y);
        Assert.Equal(StoryStickerLayout.MaxScale, inputs[0].Scale);
        Assert.Equal("cora", inputs[1].Text);
        Assert.Null(inputs[1].Options);
        Assert.Equal("@cora", drafts[1].Label);
        Assert.Null(new StoryStickerDrafts().ToInputs());
    }

    [Fact]
    public void AVotePatchesOnlyTheStoryAndStickerItBelongsTo()
    {
        var story = JsonSerializer.Deserialize(ServerStory, AethernetJsonContext.Default.StoryDto)!;
        var other = story with { Id = "s2" };
        var stories = new[] { other, story };
        var voted = story.Stickers![0] with { Counts = new[] { 3, 2 } };

        var patched = StoryStickerList.Patch(stories, "s1", voted);

        Assert.NotSame(stories, patched);
        Assert.Same(other, patched[0]);
        Assert.Equal(new[] { 3, 2 }, patched[1].Stickers![0].Counts);
        Assert.Same(story.Stickers[1], patched[1].Stickers![1]);
        Assert.Same(stories, StoryStickerList.Patch(stories, "missing", voted));
        Assert.Same(stories, StoryStickerList.Patch(stories, "s1", voted with { Id = "nope" }));
        Assert.Equal("q1", StoryStickerList.Question(story)!.Id);
    }

    [Fact]
    public void ResponseListsDedupeAndDropOnlyWhatWasAsked()
    {
        var answer = new StoryResponseDto("r1", StoryStickerKinds.Question, "u1", "Bram", "bram", null, 5, Text: "hi");
        var vote = new StoryResponseDto("u1", StoryStickerKinds.Poll, "u1", "Bram", "bram", null, 4, 1, "No");
        var other = new StoryResponseDto("r2", StoryStickerKinds.Question, "u2", "Cora", "cora", null, 3, Text: "yo");
        var first = new[] { answer, vote };

        var appended = StoryResponseList.Append(first, new[] { vote, other });

        Assert.Equal(3, appended.Length);
        Assert.Same(other, appended[2]);
        Assert.Same(first, StoryResponseList.Append(first, new[] { answer }));
        Assert.Same(first, StoryResponseList.Without(first, "u1"));
        Assert.Equal(new[] { vote }, StoryResponseList.Without(first, "r1"));
        Assert.Equal(new[] { other }, StoryResponseList.WithoutUser(appended, "u1"));
        Assert.Same(appended, StoryResponseList.WithoutUser(appended, "nobody"));
    }
}
