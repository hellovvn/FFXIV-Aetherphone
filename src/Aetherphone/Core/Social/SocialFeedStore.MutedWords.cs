using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Social;

internal enum MutedWordSaveResult : byte
{
    Saved,
    Failed,
    Full,
}

internal abstract partial class SocialFeedStore
{
    private const string MutedWordLimitCode = "muted_word_limit";

    public MutedWordDto[] MutedWords => mutes.Words.Entries;

    public bool MutedWordsLoaded => mutes.Words.Loaded;

    public bool MutedWordsFailed => mutes.Words.Failed;

    public int MutedWordsGeneration => mutes.Words.Generation;

    public void RefreshMutedWords()
    {
        var words = mutes.Words;
        if (!session.IsSignedIn || !words.TryBeginLoad())
        {
            return;
        }

        work.Run("muted words", async token =>
        {
            var list = await client.MutedWordsAsync(token).ConfigureAwait(false);
            words.Load(list?.Items);
        }, words.EndLoad);
    }

    // The sweep runs before the server answers, as an account mute does; a refused save brings the feeds back.
    public void SaveMutedWord(string? wordId, SaveMutedWordRequest request, Action<MutedWordSaveResult> onComplete)
    {
        var words = mutes.Words;
        words.Apply(request);
        MutedWordDto? saved = null;
        var reported = AepFailure.None;
        work.Run(wordId is null ? "mute word" : "edit muted word", async token =>
        {
            saved = wordId is null
                ? await client.AddMutedWordAsync(request, token, failure => reported = failure).ConfigureAwait(false)
                : await client.UpdateMutedWordAsync(wordId, request, token, failure => reported = failure)
                    .ConfigureAwait(false);
        }, () =>
        {
            if (saved is not null)
            {
                words.Saved(saved);
                onComplete(MutedWordSaveResult.Saved);
                return;
            }

            AepLog.Warning($"Muted word save failed ({reported.Describe()}); restoring the feeds");
            words.Revert();
            onComplete(string.Equals(reported.Code, MutedWordLimitCode, StringComparison.Ordinal)
                ? MutedWordSaveResult.Full
                : MutedWordSaveResult.Failed);
        });
    }

    public void DeleteMutedWord(string wordId, Action<bool> onComplete)
    {
        var words = mutes.Words;
        work.Run("unmute word", async token => await client.DeleteMutedWordAsync(wordId, token).ConfigureAwait(false),
            deleted =>
            {
                if (deleted)
                {
                    words.Removed(wordId);
                }

                onComplete(deleted);
            });
    }

    // Like a muted account, the word leaves the feeds and the replies under the post being read; the post itself,
    // profiles, search, saved and liked posts stay, because muted words only narrow what is pushed at the viewer.
    private void OnMutedWordApplied(MutedWordMatcher matcher)
    {
        var meId = me?.Id ?? session.CurrentUser?.Id;
        HideMutedWords(forYouLane, matcher, meId);
        HideMutedWords(latestLane, matcher, meId);
        HideMutedWords(followingLane, matcher, meId);
        HideMutedWords(hashtagLane, matcher, meId);
        HideMutedWords(exploreLane, matcher, meId);
        pendingForYouPage = null;
        detailComments = CopyOnWrite.RemoveWhere(detailComments, comment => HidesReply(matcher, comment, meId));
    }

    private static void HideMutedWords(FeedLane<PostDto> lane, MutedWordMatcher matcher, string? meId)
    {
        var kept = CopyOnWrite.RemoveWhere(lane.Items, post => HidesPost(matcher, post, meId));
        lane.Items = CopyOnWrite.Map(kept, post => FoldsQuote(matcher, post, meId),
            post => post with { ReferencedPost = post.ReferencedPost! with { WordMuted = true } });
        lane.DiscardPending();
    }

    public static bool HidesPost(MutedWordMatcher matcher, PostDto post, string? meId)
    {
        if (string.Equals(post.AuthorId, meId, StringComparison.Ordinal))
        {
            return false;
        }

        var shown = post.RepostOfId is not null && post.ReferencedPost is { } original ? original : post;
        return !string.Equals(shown.AuthorId, meId, StringComparison.Ordinal)
            && matcher.Hides(shown.Text, MutedWordScope.Feeds, shown.IsFollowing);
    }

    public static bool FoldsQuote(MutedWordMatcher matcher, PostDto post, string? meId)
    {
        return post.QuotedPostId is not null
            && post.ReferencedPost is { WordMuted: false } quoted
            && !string.Equals(post.AuthorId, meId, StringComparison.Ordinal)
            && !string.Equals(quoted.AuthorId, meId, StringComparison.Ordinal)
            && matcher.Hides(quoted.Text, MutedWordScope.Feeds, quoted.IsFollowing);
    }

    // A reply carries no follow state, so only entries that apply to anyone sweep the open thread; the server
    // applies the follow-limited ones on the next load.
    public static bool HidesReply(MutedWordMatcher matcher, CommentDto comment, string? meId)
    {
        return !string.Equals(comment.AuthorId, meId, StringComparison.Ordinal)
            && matcher.Hides(comment.Text, MutedWordScope.Feeds, true);
    }
}
