using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class GramClient
{
    public Task<StoryStickerDto?> VoteStoryPollAsync(string storyId, string stickerId, int option,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(StickerPath(storyId, stickerId) + "/vote", new StoryPollVoteRequest(option),
            AethernetJsonContext.Default.StoryPollVoteRequest, AethernetJsonContext.Default.StoryStickerDto, token,
            null, onFailure);
    }

    public Task<StoryStickerDto?> AnswerStoryQuestionAsync(string storyId, string stickerId, string text,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(StickerPath(storyId, stickerId) + "/answers", new StoryAnswerRequest(text),
            AethernetJsonContext.Default.StoryAnswerRequest, AethernetJsonContext.Default.StoryStickerDto, token,
            null, onFailure);
    }

    public Task<StoryResponsesPage?> StoryResponsesAsync(string storyId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/stories/{Uri.EscapeDataString(storyId)}/responses";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.StoryResponsesPage, token, null, onFailure);
    }

    public Task<bool> DeleteStoryResponseAsync(string storyId, string responseId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete,
            $"/stories/{Uri.EscapeDataString(storyId)}/responses/{Uri.EscapeDataString(responseId)}", token, null,
            onFailure);
    }

    public Task<StoryItem?> StoryItemAsync(string storyId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/stories/item/{Uri.EscapeDataString(storyId)}", AethernetJsonContext.Default.StoryItem,
            token, null, onFailure);
    }

    private static string StickerPath(string storyId, string stickerId) =>
        $"/stories/{Uri.EscapeDataString(storyId)}/stickers/{Uri.EscapeDataString(stickerId)}";
}
