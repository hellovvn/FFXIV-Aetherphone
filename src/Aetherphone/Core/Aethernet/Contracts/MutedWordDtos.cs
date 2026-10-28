namespace Aetherphone.Core.Aethernet.Contracts;

internal static class MutedWordAudiences
{
    public const int Anyone = 0;

    public const int NotFollowing = 1;
}

internal static class MutedWordDurations
{
    public const int Forever = 0;

    public const int Day = 1;

    public const int Week = 2;

    public const int Month = 3;
}

internal sealed record MutedWordDto(
    string Id,
    string Phrase,
    bool InFeeds,
    bool InNotifications,
    int Audience,
    long? ExpiresAtUnix,
    long CreatedAtUnix) : IIdentified;

internal sealed record MutedWordList(MutedWordDto[] Items);

internal sealed record SaveMutedWordRequest(
    string Phrase,
    bool InFeeds,
    bool InNotifications,
    int Audience,
    int? Duration);
