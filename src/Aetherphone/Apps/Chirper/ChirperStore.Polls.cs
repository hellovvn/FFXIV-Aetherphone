using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperStore
{
    private const int PollRefreshCap = 256;

    private readonly object pollGate = new();
    private readonly Dictionary<string, int> pendingPollVotes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PollRefreshState> pollRefreshes = new(StringComparer.Ordinal);
    private AepFailureBox? pollVoteFailure;

    public void ComposePoll(string text, PostPollInput poll, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        work.Run("compose poll", async token =>
        {
            var created = await client.CreatePollPostAsync(trimmed, poll, token, onFailure).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            AcceptCreatedPost(created);
            return true;
        }, onComplete, () => posting = false);
    }

    public bool TryGetPendingPollVote(string postId, out int optionIndex)
    {
        lock (pollGate)
        {
            return pendingPollVotes.TryGetValue(postId, out optionIndex);
        }
    }

    public bool TakePollVoteFailure(out AepFailure failure)
    {
        var box = Interlocked.Exchange(ref pollVoteFailure, null);
        failure = box?.Failure ?? AepFailure.None;
        return box is not null;
    }

    // The tapped choice shows at once as a pending vote on every copy of the chirp, because the
    // overlay is keyed by post id rather than held on one card. The server's answer then lands on
    // every copy by id, and a failure simply drops the overlay, so nothing is left to roll back.
    public void VotePoll(PostDto post, int optionIndex)
    {
        if (post.Poll is not { } poll || optionIndex < 0 || optionIndex >= poll.Options.Length)
        {
            return;
        }

        var isAuthor = Me is { } me && me.Id == post.AuthorId;
        if (!ChirperPolls.CanVote(poll, isAuthor, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            return;
        }

        var postId = post.Id;
        lock (pollGate)
        {
            if (!pendingPollVotes.TryAdd(postId, optionIndex))
            {
                return;
            }
        }

        work.Run("poll vote", async token =>
        {
            var reported = AepFailure.None;
            var counted = await client.VotePostPollAsync(postId, optionIndex, token, failure => reported = failure)
                .ConfigureAwait(false);
            if (counted is not null)
            {
                ApplyPollEverywhere(postId, counted);
                return;
            }

            Interlocked.Exchange(ref pollVoteFailure, new AepFailureBox(reported.Failed
                ? reported
                : AepFailure.Transport(AepFailureKind.Offline)));
            AepLog.Warning($"Chirper poll vote failed: {reported.Describe()}");
            if (reported.Code is FailureCodes.PollAlreadyVoted or FailureCodes.PollClosed)
            {
                await RefetchPollPostAsync(postId, token).ConfigureAwait(false);
            }
        }, () =>
        {
            lock (pollGate)
            {
                pendingPollVotes.Remove(postId);
            }
        });
    }

    // An ended poll whose tallies the server still hid asks again, but never more often than the
    // backoff allows, so a card drawn every frame cannot turn into a request every frame.
    public void RequestPollRefresh(string postId)
    {
        var nowTick = Environment.TickCount64;
        lock (pollGate)
        {
            var known = pollRefreshes.TryGetValue(postId, out var state);
            if (known && (state.InFlight || nowTick < state.DueTick))
            {
                return;
            }

            if (!known && pollRefreshes.Count >= PollRefreshCap)
            {
                pollRefreshes.Clear();
            }

            var attempts = state.Attempts + 1;
            pollRefreshes[postId] = new PollRefreshState(attempts,
                nowTick + ChirperPolls.RefreshBackoffMilliseconds(attempts), true);
        }

        work.Run("poll refresh", token => RefetchPollPostAsync(postId, token), () =>
        {
            lock (pollGate)
            {
                if (pollRefreshes.TryGetValue(postId, out var settled))
                {
                    pollRefreshes[postId] = settled with { InFlight = false };
                }
            }
        });
    }

    private async Task RefetchPollPostAsync(string postId, CancellationToken token)
    {
        var fresh = await client.PostAsync(postId, token).ConfigureAwait(false);
        if (fresh?.Poll is not { } poll)
        {
            return;
        }

        ReplacePost(fresh);
        ApplyPollEverywhere(postId, poll);
    }

    private void ApplyPollEverywhere(string postId, PostPollDto poll)
    {
        PostDto Swap(PostDto host) => ChirperPolls.WithPoll(host, poll);
        MapPostEverywhere(postId, Swap);
        MapReferencedEverywhere(postId, Swap);
    }

    private readonly record struct PollRefreshState(int Attempts, long DueTick, bool InFlight);
}
