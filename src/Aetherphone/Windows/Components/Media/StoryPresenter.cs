using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Windows.Components;

internal readonly record struct StoryConfirmLabels(LocString Confirm, LocString Cancel, LocString Busy);

internal readonly record struct StoryReplyHooks(
    LocString ReplyHint,
    Action<string, string, string> Send,
    Action<string> OpenThread);

internal sealed class StoryPresenter : IDisposable
{
    private static readonly Action<StoryDto> IgnoreSeen = static _ => { };

    private readonly StoryStore stories;
    private readonly StoryTrayRow tray;
    private readonly StoryViewerOverlay viewer;
    private readonly StoryRingPainter painter;
    private readonly AppPalette palette;
    private readonly StoryConfirmLabels labels;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly Action onCompose;
    private readonly Func<StoryDto, StoryViewers> viewersSource;
    private readonly Func<bool> nextGroup;
    private readonly Func<bool> previousGroup;
    private readonly StoryReplyHooks? replyHooks;
    private readonly StoryReplyPrompt? replyPrompt;
    private readonly Action<string>? openProfile;
    private readonly Action<StoryDto>? highlightStory;
    private bool detached;
    private readonly Action<StorySharedPostDto>? openPost;
    private readonly Action<StoryDto> toggleLike;
    private readonly StoryStickerHooks stickerHooks;
    private readonly Action<string, Action<bool>>? blockUser;
    private StoryRingDto? pendingRing;
    private string? pendingStoryId;
    private bool pendingOpenAtEnd;
    private bool pendingFromViewer;
    private StoryDto[]? viewerItems;

    public StoryPresenter(AethernetSession session, GramClient client, MediaClient media, RemoteImageCache images,
        LodestoneService lodestone, StoryRingPainter painter, AppPalette palette, StoryConfirmLabels labels,
        ConfirmService confirm, TranslationService translation, RealtimeSignalBus signals, string logTag, Action onCompose,
        StoryReplyHooks? reply = null, Action<string>? openProfile = null, Action<StoryDto>? highlight = null,
        Action<StorySharedPostDto>? openPost = null, Action<string, Action<bool>>? blockUser = null)
    {
        highlightStory = highlight;
        stories = new StoryStore(session, client, media, signals, logTag);
        tray = new StoryTrayRow(images, lodestone);
        this.openProfile = openProfile;
        this.openPost = openPost;
        this.blockUser = blockUser;
        toggleLike = stories.ToggleLike;
        stickerHooks = new StoryStickerHooks(stories.Vote, AnswerSticker, ResponsesFor, stories.LoadMoreResponses,
            AskDeleteResponse, AskBlockResponder);
        viewer = new StoryViewerOverlay(images, lodestone, translation, confirm, palette.Accent,
            openProfile is null ? null : OpenAuthorProfile, openPost is null ? null : OpenSharedPost);
        this.painter = painter;
        this.palette = palette;
        this.labels = labels;
        this.confirm = confirm;
        this.translation = translation;
        this.onCompose = onCompose;
        viewersSource = ViewersFor;
        nextGroup = TryAdvanceGroup;
        previousGroup = TryRetreatGroup;
        replyHooks = reply;
        if (reply is { } hooks)
        {
            replyPrompt = new StoryReplyPrompt(hooks.ReplyHint, SendReply);
        }
    }

    public bool Active => viewer.Active;

    public bool Posting => stories.Posting;

    public void RefreshTray() => stories.RefreshTray();

    public bool TryRing(string authorId, out StoryRingDto ring) => stories.TryRing(authorId, out ring);

    public bool TrayLoading => stories.TrayLoading;

    public void CreateStory(string sourcePath, WallpaperCrop crop, Aetherphone.Core.Media.PhotoEdit edit,
        string caption, Action<bool> onComplete, int audience = StoryAudiences.Everyone,
        StoryStickerInput[]? stickers = null) =>
        stories.CreateStory(sourcePath, crop, edit, caption, onComplete, audience, stickers);

    public void ShareGram(string postId, string caption, Action<bool, AepFailure> onComplete) =>
        stories.ShareGram(postId, caption, onComplete);

    public void DrawTray(PhoneTheme theme, string? ownAvatarUrl = null, string ownName = "",
        string? ownFrameId = null) =>
        tray.Draw(theme, palette, stories.Rings, stories.HasOwnRing, painter, onCompose, OpenRing, ownAvatarUrl,
            ownName, ownFrameId);

    public void DrawViewer(Rect screen, PhoneTheme theme, bool suspended = false) =>
        viewer.Draw(screen, theme, suspended || confirm.Active is not null);

    public void OpenDetached(StoryDto[] items, string label, string? avatarUrl, bool mine, string ownerName)
    {
        Close();
        detached = true;
        viewerItems = items;
        viewer.Open(items, label, avatarUrl, mine ? IgnoreSeen : stories.MarkSeen, reply: mine ? null : replyPrompt,
            replyTo: ownerName, stickers: stickerHooks, author: mine);
    }

    public void OpenArchived(StoryDto[] items, int startIndex, string label, string? avatarUrl,
        Action<StoryDto> delete)
    {
        Close();
        detached = true;
        viewerItems = items;
        viewer.Open(items, label, avatarUrl, IgnoreSeen, true, delete, highlight: highlightStory,
            startIndex: startIndex, stickers: stickerHooks);
    }

    public void DropDetached(string storyId)
    {
        if (!detached || viewerItems is not { } items)
        {
            return;
        }

        var kept = StoryArchiveStore.Without(items, storyId);
        if (ReferenceEquals(kept, items))
        {
            return;
        }

        viewerItems = kept;
        viewer.Replace(kept);
    }

    public void OpenRing(StoryRingDto ring)
    {
        pendingRing = ring;
        pendingStoryId = null;
        pendingOpenAtEnd = false;
        pendingFromViewer = false;
        stories.OpenAuthor(ring.AuthorId);
    }

    public void OpenRingAt(StoryRingDto ring, string storyId)
    {
        OpenRing(ring);
        pendingStoryId = storyId;
    }

    public void Close()
    {
        viewer.Reset();
        stories.CloseAuthor();
        stories.ClearViewers();
        stories.ClearResponses();
        pendingRing = null;
        pendingStoryId = null;
        pendingOpenAtEnd = false;
        pendingFromViewer = false;
        viewerItems = null;
        detached = false;
    }

    public void Advance()
    {
        DrainStickerUpdates();
        if (detached && !viewer.Active)
        {
            detached = false;
            viewerItems = null;
        }

        if (!viewer.Active && stories.OpenAuthorId is not null && (pendingRing is null || pendingFromViewer))
        {
            stories.CloseAuthor();
            viewerItems = null;
            pendingRing = null;
            pendingOpenAtEnd = false;
            pendingFromViewer = false;
        }

        if (pendingRing is not { } ring)
        {
            SyncViewerItems();
            return;
        }

        var items = stories.OpenStories;
        if (items.Length == 0)
        {
            if (!stories.GroupLoading)
            {
                pendingRing = null;
                pendingOpenAtEnd = false;
                pendingFromViewer = false;
                viewer.CancelGroupWait();
            }

            return;
        }

        pendingRing = null;
        var openAtEnd = pendingOpenAtEnd;
        var startIndex = pendingStoryId is { } wanted ? IndexOf(items, wanted) : -1;
        pendingStoryId = null;
        pendingOpenAtEnd = false;
        pendingFromViewer = false;
        viewerItems = items;
        stories.ClearViewers();
        stories.ClearResponses();
        var label = ring.IsMe
            ? Loc.T(L.Story.YourStory)
            : SocialIdentity.Name(ring.AuthorDisplayName, ring.AuthorHandle);
        viewer.Open(items, label, ring.AuthorAvatarUrl, stories.MarkSeen, ring.IsMe, AskDelete, viewersSource,
            null, nextGroup, previousGroup, openAtEnd, ring.IsMe ? null : replyPrompt, stories.LoadMoreViewers,
            ring.IsMe ? highlightStory : null, ring.IsMe || replyPrompt is null ? null : toggleLike,
            startIndex: startIndex, stickers: stickerHooks);
    }

    private static int IndexOf(StoryDto[] items, string storyId)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id == storyId)
            {
                return index;
            }
        }

        return -1;
    }

    private void DrainStickerUpdates()
    {
        while (stories.TryTakeStickerUpdate(out var update))
        {
            if (!detached || viewerItems is not { } items)
            {
                continue;
            }

            var patched = StoryStickerList.Patch(items, update.StoryId, update.Sticker);
            if (ReferenceEquals(patched, items))
            {
                continue;
            }

            viewerItems = patched;
            viewer.Replace(patched);
        }
    }

    private void AnswerSticker(StoryDto story, StoryStickerDto sticker, string text, Action<bool> done)
    {
        stories.Answer(story, sticker, text, (sent, _) => done(sent));
    }

    private StoryResponses ResponsesFor(StoryDto story)
    {
        stories.LoadResponses(story.Id);
        return new StoryResponses(stories.Responses, stories.ResponseVoteCount, stories.ResponseAnswerCount,
            stories.ResponsesLoading, stories.HasMoreResponses, stories.ResponsesLoadingMore);
    }

    private void AskDeleteResponse(StoryDto story, StoryResponseDto response)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Story.DeleteResponseMessage),
            ConfirmLabel = Loc.T(labels.Confirm),
            CancelLabel = Loc.T(labels.Cancel),
            Sheet = true,
            BusyLabel = Loc.T(labels.Busy),
            FailedMessage = Loc.T(L.Story.DeleteResponseFailed),
            ConfirmAsync = done => stories.DeleteResponse(story, response.Id, done),
        });
    }

    private void AskBlockResponder(StoryResponseDto response)
    {
        if (blockUser is not { } block)
        {
            return;
        }

        var userId = response.UserId;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Social.BlockConfirmTitle, SocialIdentity.Name(response.DisplayName, response.Handle)),
            Message = Loc.T(L.Social.BlockConfirm),
            ConfirmLabel = Loc.T(L.Social.BlockAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done => block(userId, blocked =>
            {
                if (blocked)
                {
                    stories.ForgetResponder(userId);
                }

                done(blocked);
            }),
        });
    }

    private bool TryAdvanceGroup() => TryOpenAdjacentRing(1);

    private bool TryRetreatGroup() => TryOpenAdjacentRing(-1);

    private bool TryOpenAdjacentRing(int direction)
    {
        var authorId = stories.OpenAuthorId;
        if (authorId is null)
        {
            return false;
        }

        var snapshot = stories.Rings;
        for (var ringIndex = 0; ringIndex < snapshot.Length; ringIndex++)
        {
            if (snapshot[ringIndex].AuthorId != authorId)
            {
                continue;
            }

            var target = ringIndex + direction;
            if (target < 0 || target >= snapshot.Length)
            {
                return false;
            }

            pendingRing = snapshot[target];
            pendingOpenAtEnd = direction < 0;
            pendingFromViewer = true;
            stories.OpenAuthor(snapshot[target].AuthorId);
            return true;
        }

        return false;
    }

    private void OpenAuthorProfile(string authorId)
    {
        Close();
        openProfile?.Invoke(authorId);
    }

    private void OpenSharedPost(StorySharedPostDto shared)
    {
        Close();
        openPost?.Invoke(shared);
    }

    private void SendReply(StoryDto story, string text)
    {
        if (replyHooks is not { } hooks)
        {
            return;
        }

        var authorId = story.AuthorId;
        hooks.Send(authorId, story.Id, text);
        Close();
        hooks.OpenThread(authorId);
    }

    private StoryViewers ViewersFor(StoryDto story)
    {
        stories.LoadViewers(story.Id);
        return new StoryViewers(stories.Viewers, stories.ViewersTotal, stories.ViewersLoading,
            stories.HasMoreViewers, stories.ViewersLoadingMore);
    }

    private void SyncViewerItems()
    {
        if (!viewer.Active || detached)
        {
            return;
        }

        var items = stories.OpenStories;
        if (ReferenceEquals(items, viewerItems))
        {
            return;
        }

        viewerItems = items;
        viewer.Replace(items);
    }

    private void AskDelete(StoryDto story)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Story.DeleteMessage),
            ConfirmLabel = Loc.T(labels.Confirm),
            CancelLabel = Loc.T(labels.Cancel),
            Sheet = true,
            BusyLabel = Loc.T(labels.Busy),
            FailedMessage = Loc.T(L.Story.DeleteFailed),
            ConfirmAsync = done => stories.DeleteStory(story.Id, done),
        });
    }

    public void Dispose()
    {
        stories.Dispose();
    }
}
