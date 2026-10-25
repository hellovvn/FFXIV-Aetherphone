using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Social;

internal abstract partial class SocialFeedStore
{
    public void ComposeGif(string text, string? quotedPostId, GifItem gif, bool sensitive, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        if (posting)
        {
            return;
        }

        posting = true;
        var trimmed = text.Trim();
        work.Run("compose gif", async token =>
        {
            var created = await client.CreateGifPostAsync(trimmed, quotedPostId, gif.Url, gif.Slug, gif.Width,
                gif.Height, sensitive, token, onFailure).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            AcceptCreatedPost(created);
            return true;
        }, onComplete, () => posting = false);
    }

    public void AddGifComment(string postId, string text, GifItem gif, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        if (commenting)
        {
            return;
        }

        commenting = true;
        var trimmed = text.Trim();
        work.Run("comment gif", async token =>
        {
            var created = await client.AddGifCommentAsync(postId, trimmed, gif.Url, gif.Width, gif.Height, token,
                onFailure).ConfigureAwait(false);
            if (created is null)
            {
                AepLog.Warning($"GIF reply on {postId} was not accepted");
                return false;
            }

            if (detailPostId == postId)
            {
                detailComments = CopyOnWrite.Append(detailComments, created);
            }

            BumpCommentCount(postId, 1);
            return true;
        }, onComplete, () => commenting = false);
    }
}
