namespace Aetherphone.Core.Aethernet.Contracts;

internal static class StoryStickerKinds
{
    public const int Poll = 0;
    public const int Question = 1;
    public const int Mention = 2;
}

internal sealed record StoryStickerInput(int Kind, float X, float Y, float Scale = 1f, string? Text = null,
    string[]? Options = null);

internal sealed record StoryStickerDto(
    string Id,
    int Kind,
    float X,
    float Y,
    float Scale,
    string Text = "",
    string[]? Options = null,
    int[]? Counts = null,
    int MyVote = -1,
    bool Answered = false,
    string? UserId = null,
    int AnswerCount = 0);

internal sealed record StoryPollVoteRequest(int Option);

internal sealed record StoryAnswerRequest(string Text);

internal sealed record StoryResponseDto(
    string Id,
    int Kind,
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    long CreatedAtUnix,
    int Option = -1,
    string Text = "",
    int Badges = 0,
    string[]? BadgeIds = null,
    string FrameId = "");

internal sealed record StoryResponsesPage(StoryResponseDto[] Items, int VoteCount, int AnswerCount,
    string? NextCursor = null);

internal sealed record StoryItem(StoryDto Story, string AuthorDisplayName, string AuthorHandle, string? AuthorAvatarUrl);
