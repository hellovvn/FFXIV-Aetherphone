using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Social;

internal sealed partial class StoryStore
{
    private readonly object stickerUpdateLock = new();
    private readonly List<StoryStickerUpdate> stickerUpdates = new();
    private volatile StoryResponseDto[] responses = Array.Empty<StoryResponseDto>();
    private volatile string? responsesStoryId;
    private volatile string? responsesCursor;
    private volatile int responseVoteCount;
    private volatile int responseAnswerCount;
    private volatile bool responsesLoading;
    private volatile bool responsesLoadingMore;

    public StoryResponseDto[] Responses => responses;
    public int ResponseVoteCount => responseVoteCount;
    public int ResponseAnswerCount => responseAnswerCount;
    public bool ResponsesLoading => responsesLoading;
    public bool ResponsesLoadingMore => responsesLoadingMore;
    public bool HasMoreResponses => responsesCursor is not null;

    public void Vote(StoryDto story, StoryStickerDto sticker, int option)
    {
        if (sticker.MyVote >= 0)
        {
            return;
        }

        PublishSticker(story, sticker with { MyVote = option });
        work.Run("story poll vote", async token =>
        {
            var voted = await client.VoteStoryPollAsync(story.Id, sticker.Id, option, token).ConfigureAwait(false);
            PublishSticker(story, voted ?? sticker);
        });
    }

    public void Answer(StoryDto story, StoryStickerDto sticker, string text, Action<bool, AepFailure> onComplete)
    {
        var failure = AepFailure.None;
        work.Run("story question answer", async token =>
        {
            var answered = await client.AnswerStoryQuestionAsync(story.Id, sticker.Id, text, token,
                reported => failure = reported).ConfigureAwait(false);
            if (answered is null)
            {
                return false;
            }

            PublishSticker(story, answered);
            return true;
        }, sent => onComplete(sent, failure));
    }

    public bool TryTakeStickerUpdate(out StoryStickerUpdate update)
    {
        lock (stickerUpdateLock)
        {
            if (stickerUpdates.Count == 0)
            {
                update = default;
                return false;
            }

            update = stickerUpdates[0];
            stickerUpdates.RemoveAt(0);
            return true;
        }
    }

    public void LoadResponses(string storyId)
    {
        if (responsesStoryId == storyId)
        {
            return;
        }

        responsesStoryId = storyId;
        responses = Array.Empty<StoryResponseDto>();
        responseVoteCount = 0;
        responseAnswerCount = 0;
        responsesCursor = null;
        responsesLoading = true;
        work.Run("story responses", async token =>
        {
            var page = await client.StoryResponsesAsync(storyId, null, token).ConfigureAwait(false);
            if (page is not null && responsesStoryId == storyId)
            {
                ApplyResponses(page, page.Items);
            }
        }, () => responsesLoading = false);
    }

    public void LoadMoreResponses()
    {
        var storyId = responsesStoryId;
        var cursor = responsesCursor;
        if (storyId is null || cursor is null || responsesLoading || responsesLoadingMore)
        {
            return;
        }

        responsesLoadingMore = true;
        work.Run("story responses more", async token =>
        {
            var page = await client.StoryResponsesAsync(storyId, cursor, token).ConfigureAwait(false);
            if (page is not null && responsesStoryId == storyId)
            {
                ApplyResponses(page, StoryResponseList.Append(responses, page.Items));
            }
        }, () => responsesLoadingMore = false);
    }

    public void ClearResponses()
    {
        responsesStoryId = null;
        responses = Array.Empty<StoryResponseDto>();
        responseVoteCount = 0;
        responseAnswerCount = 0;
        responsesCursor = null;
    }

    public void DeleteResponse(StoryDto story, string responseId, Action<bool> onComplete)
    {
        var storyId = story.Id;
        work.Run("delete story response", async token =>
        {
            if (!await client.DeleteStoryResponseAsync(storyId, responseId, token).ConfigureAwait(false))
            {
                return false;
            }

            if (StoryStickerList.Question(story) is { } question)
            {
                PublishSticker(story, question with { AnswerCount = Math.Max(0, question.AnswerCount - 1) });
            }

            if (responsesStoryId == storyId)
            {
                var kept = StoryResponseList.Without(responses, responseId);
                if (!ReferenceEquals(kept, responses))
                {
                    responses = kept;
                    responseAnswerCount = Math.Max(0, responseAnswerCount - 1);
                }
            }

            return true;
        }, onComplete);
    }

    public void ForgetResponder(string userId)
    {
        responses = StoryResponseList.WithoutUser(responses, userId);
    }

    private void ApplyResponses(StoryResponsesPage page, StoryResponseDto[] items)
    {
        responses = items;
        responseVoteCount = page.VoteCount;
        responseAnswerCount = page.AnswerCount;
        responsesCursor = page.NextCursor;
    }

    private void PublishSticker(StoryDto story, StoryStickerDto sticker)
    {
        openStories = StoryStickerList.Patch(openStories, story.Id, sticker);
        lock (stickerUpdateLock)
        {
            stickerUpdates.Add(new StoryStickerUpdate(story.Id, sticker));
        }
    }
}
