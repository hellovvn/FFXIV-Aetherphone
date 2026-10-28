using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal abstract partial class SocialFeedStore
{
    private readonly SocialMuteLedger mutes;
    private volatile UserDto[] mutedAccounts = Array.Empty<UserDto>();
    private volatile string? mutedAccountsCursor;
    private volatile bool mutedAccountsLoading;
    private volatile bool mutedAccountsLoadingMore;
    private volatile bool mutedAccountsLoaded;
    private volatile bool mutedAccountsFailed;

    public UserDto[] MutedAccounts => mutedAccounts;
    public bool MutedAccountsLoading => mutedAccountsLoading;
    public bool MutedAccountsLoadingMore => mutedAccountsLoadingMore;
    public bool MutedAccountsLoaded => mutedAccountsLoaded;
    public bool MutedAccountsFailed => mutedAccountsFailed;
    public bool HasMoreMutedAccounts => mutedAccountsCursor is not null;

    public bool IsMuted(UserDto user) =>
        mutes.TryGet(user.Id, out var muted) ? muted : user.IsMuted;

    public bool IsAuthorMuted(PostDto post) =>
        mutes.TryGet(post.AuthorId, out var muted) ? muted : post.AuthorMuted;

    public bool IsMuted(string userId)
    {
        if (mutes.TryGet(userId, out var muted))
        {
            return muted;
        }

        return profileUser is { } shown && shown.Id == userId && shown.IsMuted;
    }

    public void SetMuted(string userId, bool muted, Action<bool> onComplete)
    {
        mutes.Record(userId, muted);
        work.Run(muted ? "mute" : "unmute", async token => muted
            ? await client.MuteAsync(userId, token).ConfigureAwait(false)
            : await client.UnmuteAsync(userId, token).ConfigureAwait(false), saved =>
        {
            if (!saved)
            {
                AepLog.Warning($"{(muted ? "Mute" : "Unmute")} of {userId} failed; restoring the previous state");
                mutes.Restore(userId, !muted);
            }

            onComplete(saved);
        });
    }

    public void RefreshMutedAccounts()
    {
        if (!session.IsSignedIn || mutedAccountsLoading)
        {
            return;
        }

        mutedAccountsLoading = true;
        work.Run("muted accounts", async token =>
        {
            var page = await client.MutedUsersAsync(null, token).ConfigureAwait(false);
            if (page is null)
            {
                mutedAccountsFailed = true;
                return;
            }

            mutedAccounts = page.Items;
            mutedAccountsCursor = page.NextCursor;
            mutedAccountsFailed = false;
            mutedAccountsLoaded = true;
        }, () => mutedAccountsLoading = false);
    }

    public void LoadMoreMutedAccounts()
    {
        var cursor = mutedAccountsCursor;
        if (!session.IsSignedIn || cursor is null || mutedAccountsLoadingMore || mutedAccountsLoading)
        {
            return;
        }

        mutedAccountsLoadingMore = true;
        work.Run("muted accounts more", async token =>
        {
            var page = await client.MutedUsersAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            mutedAccounts = CopyOnWrite.AppendPageById(mutedAccounts, page.Items);
            mutedAccountsCursor = page.NextCursor;
        }, () => mutedAccountsLoadingMore = false);
    }

    private void OnMuteChanged(MuteChange change)
    {
        if (change.Muted)
        {
            HideMutedAuthor(change.UserId);
            return;
        }

        if (change.Restored)
        {
            RefreshLoadedFeeds();
        }
    }

    // The feeds and the replies under someone else's post lose the author, as the server filters them;
    // their profile, the post being read, saved and liked posts stay, because a mute narrows what is
    // pushed at the viewer and nothing the viewer opens on purpose.
    private void HideMutedAuthor(string userId)
    {
        HideMutedAuthor(forYouLane, userId);
        HideMutedAuthor(latestLane, userId);
        HideMutedAuthor(followingLane, userId);
        HideMutedAuthor(hashtagLane, userId);
        HideMutedAuthor(exploreLane, userId);
        pendingForYouPage = null;
        DismissSuggestion(userId);
        HideMutedReplies(userId);
    }

    private void HideMutedReplies(string userId)
    {
        if (detailPost is { } post && post.AuthorId == userId)
        {
            return;
        }

        detailComments = CopyOnWrite.RemoveWhere(detailComments, comment => comment.AuthorId == userId);
    }

    private static void HideMutedAuthor(FeedLane<PostDto> lane, string userId)
    {
        lane.Items = CopyOnWrite.RemoveWhere(lane.Items, post => BlockedContent.Hides(post, userId));
        lane.DiscardPending();
    }

    private void RefreshLoadedFeeds()
    {
        RefreshLoadedFeed(SocialFeedScope.ForYou);
        RefreshLoadedFeed(SocialFeedScope.Latest);
        RefreshLoadedFeed(SocialFeedScope.Following);
    }

    private void RefreshLoadedFeed(SocialFeedScope scope)
    {
        if (!Lane(scope).NeverLoaded)
        {
            RefreshFeed(scope);
        }
    }

    private void ResetMutes()
    {
        mutes.Reset();
        mutes.Words.Reset();
        mutedAccounts = Array.Empty<UserDto>();
        mutedAccountsCursor = null;
        mutedAccountsLoaded = false;
        mutedAccountsFailed = false;
    }
}
