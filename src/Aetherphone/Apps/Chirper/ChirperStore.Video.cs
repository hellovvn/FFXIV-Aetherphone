using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperStore
{
    private volatile ReelShareJob? videoShare;

    public ReelShareJob? VideoShare => videoShare;

    public bool VideoSharing => videoShare is { State.Busy: true };

    public bool StartVideoShare(IReelMedia reelMedia, ReelShareRequest request, string? quotedPostId, int replyPolicy)
    {
        if (VideoSharing || !IsSignedIn)
        {
            return false;
        }

        DismissVideoShare();
        ReelWorkFiles.SweepStaleOnce();
        var publisher = new AethernetReelPublisher(media, (reel, token, onFailure) =>
            CreateVideoChirpAsync(reel, quotedPostId, replyPolicy, token, onFailure));
        var job = new ReelShareJob(reelMedia, publisher, ReelWorkFiles.Folder, request, true);
        videoShare = job;
        job.Start();
        return true;
    }

    public void DismissVideoShare()
    {
        var job = videoShare;
        videoShare = null;
        job?.Dispose();
    }

    public void AcceptCreatedVideo(PostDto? created)
    {
        if (created is null)
        {
            RefreshFeed(SocialFeedScope.Latest);
            return;
        }

        AcceptCreatedPost(created);
    }

    // The share job hands over a reel request; for a chirp only its text, the two upload keys and the sensitive flag
    // carry over, and the quote and reply setting come from the composer.
    private Task<PostDto?> CreateVideoChirpAsync(CreateReelRequest reel, string? quotedPostId, int replyPolicy,
        CancellationToken token, Action<AepFailure> onFailure) =>
        client.CreateVideoPostAsync(reel.Caption, quotedPostId, reel.VideoKey, reel.PosterKey, reel.Sensitive,
            replyPolicy, token, onFailure);
}
