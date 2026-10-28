using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramStore
{
    private enum OwnReels : byte
    {
        Unknown,
        None,
        Posted,
    }

    private static readonly TimeSpan OwnReelsRetry = TimeSpan.FromMinutes(1);

    private readonly RetryGate ownReelsGate = new(OwnReelsRetry);
    private volatile ReelShareJob? reelShare;
    private volatile OwnReels ownReels;
    private volatile bool ownReelsChecking;

    public ReelShareJob? ReelShare => reelShare;

    public bool ReelSharing => reelShare is { State.Busy: true };

    // The first reel bonus pays once ever, so the hint shows only while the account has no reel up. The account's
    // own reels page is one small request; the coin ledger has no rule filter and would need paging through all of it.
    public bool FirstReelBonusAvailable
    {
        get
        {
            EnsureOwnReelsKnown();
            return ownReels == OwnReels.None;
        }
    }

    public bool StartReelShare(IReelMedia reelMedia, ReelShareRequest request)
    {
        if (ReelSharing || !IsSignedIn)
        {
            return false;
        }

        DismissReelShare();
        ReelWorkFiles.SweepStaleOnce();
        var job = new ReelShareJob(reelMedia, new AethernetReelPublisher(media, grams.CreateReelAsync),
            ReelWorkFiles.Folder, request, true);
        reelShare = job;
        job.Start();
        return true;
    }

    public void DismissReelShare()
    {
        var job = reelShare;
        reelShare = null;
        job?.Dispose();
    }

    public void AcceptCreatedReel(PostDto? created)
    {
        ownReels = OwnReels.Posted;
        if (created is null)
        {
            RefreshFeed(SocialFeedScope.Latest);
            return;
        }

        AcceptCreatedPost(created);
        PrependOwnReel(created);
    }

    private void ResetReelPosting()
    {
        DismissReelShare();
        ownReels = OwnReels.Unknown;
        ownReelsGate.Reset();
    }

    private void EnsureOwnReelsKnown()
    {
        if (ownReels != OwnReels.Unknown || ownReelsChecking || !IsSignedIn || AccountId is not { } accountId
            || !ownReelsGate.TryPass())
        {
            return;
        }

        ownReelsChecking = true;
        work.Run("own reels", async token =>
        {
            var page = await grams.OwnReelsAsync(accountId, token).ConfigureAwait(false);
            if (page is null || !string.Equals(AccountId, accountId, StringComparison.Ordinal))
            {
                return;
            }

            ownReels = page.Items.Length > 0 ? OwnReels.Posted : OwnReels.None;
        }, () => ownReelsChecking = false);
    }
}
