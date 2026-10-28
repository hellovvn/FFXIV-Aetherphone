using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Components;

internal sealed partial class StoryViewerOverlay
{
    private const long StampBucketSeconds = 60L;

    private string headerNameId = string.Empty;
    private string stampStoryId = string.Empty;
    private string stampLanguage = string.Empty;
    private long stampBucket = -1;
    private string stampText = string.Empty;
    private string replyHintLanguage = string.Empty;
    private string replyHintName = string.Empty;
    private string replyHintText = string.Empty;
    private string seenLanguage = string.Empty;
    private int seenCount = -1;
    private string seenText = string.Empty;

    private void NameHeader(string label)
    {
        headerNameId = "storyviewer.header.author." + label;
    }

    private string HeaderStamp(StoryDto story)
    {
        var code = Loc.Current.Code;
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StampBucketSeconds;
        if (bucket == stampBucket && string.Equals(stampStoryId, story.Id, StringComparison.Ordinal)
            && string.Equals(stampLanguage, code, StringComparison.Ordinal))
        {
            return stampText;
        }

        stampBucket = bucket;
        stampStoryId = story.Id;
        stampLanguage = code;
        stampText = TimeText.Short(story.CreatedAtUnix);
        return stampText;
    }

    private string ReplyHint(in StoryReplyPrompt prompt)
    {
        var code = Loc.Current.Code;
        var name = replyName ?? authorLabel;
        if (string.Equals(replyHintLanguage, code, StringComparison.Ordinal)
            && string.Equals(replyHintName, name, StringComparison.Ordinal))
        {
            return replyHintText;
        }

        replyHintLanguage = code;
        replyHintName = name;
        replyHintText = Loc.T(prompt.Hint, name);
        return replyHintText;
    }

    private string SeenLabel(StoryDto story)
    {
        var code = Loc.Current.Code;
        if (seenCount == story.ViewCount && string.Equals(seenLanguage, code, StringComparison.Ordinal))
        {
            return seenText;
        }

        seenCount = story.ViewCount;
        seenLanguage = code;
        seenText = Loc.Plural(L.Story.SeenBy, story.ViewCount);
        return seenText;
    }
}
