using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperPolls
{
    public const int MinOptions = 2;
    public const int MaxOptions = 4;
    public const int MaxOptionLength = 40;
    public const int DefaultDurationIndex = 2;

    private const long RefreshFirstBackoffMilliseconds = 5_000L;
    private const long RefreshMaxBackoffMilliseconds = 300_000L;
    private const long RefreshBackoffFactor = 3L;

    public static readonly int[] DurationHours = { 1, 6, 24, 72, 168 };

    public static readonly LocString[] DurationLabels =
    {
        L.Chirper.PollOneHour,
        L.Chirper.PollSixHours,
        L.Chirper.PollOneDay,
        L.Chirper.PollThreeDays,
        L.Chirper.PollSevenDays,
    };

    public static bool HasEnded(PostPollDto poll, long nowUnix) =>
        poll.Ended || (poll.EndsAtUnix > 0 && nowUnix >= poll.EndsAtUnix);

    public static bool ResultsVisible(PostPollDto poll) => poll.Counts is not null;

    public static bool CanVote(PostPollDto poll, bool isAuthor, long nowUnix) =>
        !isAuthor && poll.MyVote < 0 && !HasEnded(poll, nowUnix);

    public static int CountAt(PostPollDto poll, int optionIndex)
    {
        var counts = poll.Counts;
        return counts is not null && optionIndex >= 0 && optionIndex < counts.Length ? counts[optionIndex] : 0;
    }

    public static int ShownVote(PostPollDto poll, bool pending, int pendingOption) =>
        poll.MyVote >= 0 ? poll.MyVote : pending ? pendingOption : -1;

    public static PostDto WithPoll(PostDto post, PostPollDto poll) =>
        post.Poll is not null && SameState(post.Poll, poll) ? post : post with { Poll = poll };

    public static bool SameState(PostPollDto left, PostPollDto right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left.MyVote == right.MyVote
            && left.TotalVotes == right.TotalVotes
            && left.Ended == right.Ended
            && left.EndsAtUnix == right.EndsAtUnix
            && SameItems(left.Options, right.Options)
            && SameCounts(left.Counts, right.Counts);
    }

    public static long RefreshBackoffMilliseconds(int attempts)
    {
        var delay = RefreshFirstBackoffMilliseconds;
        for (var attempt = 1; attempt < attempts && delay < RefreshMaxBackoffMilliseconds; attempt++)
        {
            delay *= RefreshBackoffFactor;
        }

        return Math.Min(delay, RefreshMaxBackoffMilliseconds);
    }

    private static bool SameItems(string[] left, string[] right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameCounts(int[]? left, int[]? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.AsSpan().SequenceEqual(right);
    }
}
