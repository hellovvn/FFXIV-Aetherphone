using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Social;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const int ReelSeedCap = 30;
    private const float ReelSnapSeconds = Motion.PageSettle;
    private const uint ReelBackdropTint = 0xFF525252;
    private const float ReelScrimShare = 0.42f;
    private const float ReelTopScrimHeight = 72f;
    private const float ReelRailWidth = 62f;
    private const float ReelRailIconSize = 27f;
    private const float ReelRailSlotGap = 16f;
    private const float ReelRailLabelGap = 3f;
    private const float ReelInset = 14f;
    private const float ReelBottomGap = 12f;
    private const float ReelAvatarRadius = 15f;
    private const float ReelHeaderGap = 9f;
    private const float ReelFollowHeight = 26f;
    private const float ReelFollowPadX = 11f;
    private const float ReelCaptionGap = 8f;
    private const int ReelCaptionLines = 2;
    private const float ReelExpandedCaptionShare = 0.4f;
    private const float ReelProgressHeight = 2f;
    private const float ReelSpinnerRadius = 15f;
    private const float ReelFlashRadius = 34f;
    private const float ReelFlashGlyph = 30f;
    private const float ReelFlashSeconds = 0.75f;
    private const float ReelChipHeight = 32f;
    private const float ReelChipPadX = 14f;
    private const float ReelShadowDrop = 1.5f;
    private const float ReelBackGlyph = 24f;

    private static readonly Vector4 ReelBlack = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 ReelShadow = new(0f, 0f, 0f, 0.45f);
    private static readonly Vector4 ReelScrim = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 ReelChipFill = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 ReelPillStroke = new(1f, 1f, 1f, 0.75f);
    private static readonly TextStyle ReelHandleStyle = TextStyles.Headline;
    private static readonly TextStyle ReelCountStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle ReelTitleStyle = TextStyles.Title3;
    private static readonly TextStyle ReelChipStyle = TextStyles.SubheadlineEmphasized;

    private readonly ReelPlayback reelPlayback;
    private readonly ReelSource[] reelWindow = new ReelSource[ReelPlayer.Depth];
    private readonly ReelSwipe reelSwipe = new();
    private readonly RichTextCache reelCaptionLayouts = new(scanHashtags: true);
    private ReelDeck? presentedDeck;
    private Spring reelSnap;
    private bool reelMuted;
    private bool reelWasHeld;
    private double reelFlashAt = double.MinValue;
    private int reelPresentedFrame = -1;
    private float reelInfoCaptionHeight;
    private string? expandedReelCaptionId;
    private PostDto? reelTextPost;
    private CultureInfo? reelTextCulture;
    private string reelLikeText = string.Empty;
    private string reelCommentText = string.Empty;
    private string reelHandleText = string.Empty;

    private static string ReelsGlyph => IconGlyph.Of(FontAwesomeIcon.Film);

    private void DrawReelsTab(Rect area)
    {
        if (ReelSetupNeeded)
        {
            DrawReelSetup(TabBar.ContentArea(area, UiScale.Current));
            return;
        }

        DrawReelDeck(area, store.ReelTab, true);
    }

    private void DrawReelsRoute(Rect area)
    {
        DrawReelDeck(area, store.ReelOpened, false);
    }

    // Tapping Reels again starts a fresh session from the top, as on Instagram.
    private void RestartReelsTab()
    {
        RefreshReelDeck(store.ReelTab);
    }

    private void RefreshReelDeck(ReelDeck deck)
    {
        EndReelPlay(deck, false);
        deck.Reset();
        reelSnap.SnapTo(0f);
        reelSwipe.Cancel();
        expandedReelCaptionId = null;
    }

    private void OpenReel(PostDto post)
    {
        OpenReels([post], 0);
    }

    private void OpenReels(PostDto[] posts, int start)
    {
        var count = 0;
        for (var index = start; index < posts.Length && count < ReelSeedCap; index++)
        {
            if (posts[index].Video is not null)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        var seed = new PostDto[count];
        var filled = 0;
        for (var index = start; index < posts.Length && filled < count; index++)
        {
            if (posts[index].Video is not null)
            {
                seed[filled] = posts[index];
                filled++;
            }
        }

        var deck = store.ReelOpened;
        EndReelPlay(deck, false);
        deck.Seed(seed);
        expandedReelCaptionId = null;
        router.Push(AethergramRoute.Reels(seed[0].Id));
    }

    private void OpenGridPost(PostDto[] posts, int index, PostSource source)
    {
        if (posts[index].Video is not null)
        {
            OpenReels(posts, index);
            return;
        }

        OpenPosts(posts[index].Id, source);
    }

    private void DrawReelDeck(Rect area, ReelDeck deck, bool tabMode)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(ReelBlack));
        var overlay = tabMode ? TabBar.ContentArea(area, scale) : area;
        SwitchPresentedDeck(deck);
        DropHiddenReel(deck, tabMode);
        var post = deck.Resolve();
        store.LoadReels(deck);
        if (post is null)
        {
            EndReelPlay(deck, false);
            DrawReelEmpty(overlay, deck);
            DrawReelTopBar(drawList, overlay, tabMode);
            return;
        }

        var items = deck.Items;
        var now = ImGui.GetTime();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var onLast = deck.Position == items.Length - 1;
        if (deck.AtEnd && (!onLast || !deck.Exhausted))
        {
            deck.AtEnd = false;
        }

        var pressAllowed = deck.AtEnd || !ReelControlsHovered(overlay, tabMode, post);
        var step = reelSwipe.Update(area, !ReelCommentsCaptures, pressAllowed,
            !deck.AtEnd && (!onLast || deck.Exhausted), deck.AtEnd || deck.Position > 0, now, delta, out var touch);
        if (reelSwipe.Released)
        {
            if (step != 0)
            {
                StepReelDeck(deck, step);
                post = deck.Resolve()!;
                items = deck.Items;
                expandedReelCaptionId = null;
            }

            reelSnap.SnapTo(reelSnap.Value + reelSwipe.ReleasedDrag + step * area.Height);
        }

        var offset = reelSwipe.Dragging
            ? reelSnap.Value + reelSwipe.Drag
            : reelSnap.Step(0f, ReelSnapSeconds, delta);
        if (deck.AtEnd)
        {
            reelPlayback.Present(ReadOnlySpan<ReelSource>.Empty, false, reelMuted);
            reelPresentedFrame = ImGui.GetFrameCount();
            DrawReelEndCard(drawList, area, overlay, post, deck, offset, tabMode);
            return;
        }

        BeginReelPlay(deck, post);
        var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
        HandleReelTouch(touch, post, veiled);
        var held = reelSwipe.Holding;
        var play = !veiled && ReelPauseRules.ShouldPlay(new ReelPlaybackInputs(reelPlayback.PhoneVisible, true,
            ReelCoverState(), reelPlayback.OtherVideoPlaying, held));
        reelPlayback.Present(reelWindow.AsSpan(0, FillReelWindow(items, deck.Position)), play, reelMuted);
        reelPresentedFrame = ImGui.GetFrameCount();
        var current = reelPlayback.FrameOf(post.Id);
        var progress = reelPlayback.Progress;
        if (current.HasFrame)
        {
            deck.Play.Observe(progress.PositionSeconds, progress.DurationSeconds);
        }

        if (held && !reelWasHeld)
        {
            SuspendReelDeck(deck);
        }

        reelWasHeld = held;
        drawList.PushClipRect(area.Min, area.Max, true);
        DrawReelStrip(drawList, area, items, deck.Position, offset);
        if (!held)
        {
            var shift = new Vector2(0f, offset);
            var shifted = new Rect(overlay.Min + shift, overlay.Max + shift);
            DrawReelChrome(drawList, shifted, post, progress, tabMode);
            DrawReelStatus(drawList, shifted, post, current, veiled);
        }

        doubleTapLike.DrawBurst(drawList, area, post.Id);
        drawList.PopClipRect();
        FlushReelWatches(deck, false);
    }

    // The current reel always leads the window, so a post without a video never lets the next reel's sound play
    // under its cover.
    private int FillReelWindow(PostDto[] items, int position)
    {
        if (items[position].Video is null)
        {
            return 0;
        }

        var count = 0;
        for (var index = position; index < items.Length && count < reelWindow.Length; index++)
        {
            if (items[index].Video is not { } video)
            {
                continue;
            }

            reelWindow[count] = new ReelSource(items[index].Id, video.Url, video.Width, video.Height);
            count++;
        }

        return count;
    }

    private ReelCover ReelCoverState()
    {
        var sheet = postSheet.CapturesPointer || collabAuthorsSheet.CapturesPointer || postViewsSheet.CapturesPointer
            || AltTextCapturesPointer || GifSheetCaptures || StoryShareCaptures || avatarLightbox.Expanded
            || confirm.Active is not null || report.Active is not null;
        if (sheet)
        {
            return ReelCover.Sheet;
        }

        return ReelCommentsCaptures ? ReelCover.Comments : ReelCover.None;
    }

    private void SwitchPresentedDeck(ReelDeck deck)
    {
        if (ReferenceEquals(presentedDeck, deck))
        {
            return;
        }

        SuspendReelDeck(presentedDeck);
        presentedDeck = deck;
        reelSnap.SnapTo(0f);
        reelSwipe.Cancel();
    }

    // Not interested drops a reel anywhere; a mute only drops the author from the Reels feed, since a reel opened
    // from their profile was asked for.
    private void DropHiddenReel(ReelDeck deck, bool feed)
    {
        if (deck.Resolve() is not { } post)
        {
            return;
        }

        var muted = feed && !IsOwnPost(post) && store.IsMuted(post.AuthorId);
        if (!muted && !store.IsNotInterested(post.Id))
        {
            return;
        }

        EndReelPlay(deck, false);
        deck.Remove(post.Id);
    }

    private void HandleReelTouch(ReelTouch touch, PostDto post, bool veiled)
    {
        if (touch == ReelTouch.DoubleTap && !veiled)
        {
            doubleTapLike.Burst(post.Id);
            SocialSeason.Play(UiSound.HalloweenHeartbeat);
            if (post.MyReaction < 0)
            {
                store.ToggleLike(post);
            }

            return;
        }

        if (touch != ReelTouch.Tap)
        {
            return;
        }

        if (veiled)
        {
            SensitiveReveals.Reveal(post.Id);
            return;
        }

        if (expandedReelCaptionId is not null)
        {
            expandedReelCaptionId = null;
            return;
        }

        reelMuted = !reelMuted;
        reelFlashAt = ImGui.GetTime();
    }

    private void BeginReelPlay(ReelDeck deck, PostDto post)
    {
        if (string.Equals(deck.Play.PostId, post.Id, StringComparison.Ordinal))
        {
            return;
        }

        EndReelPlay(deck, false);
        deck.Play.Begin(post.Id, post.Video?.DurationMs ?? 0);
    }

    private void EndReelPlay(ReelDeck deck, bool swiped)
    {
        if (deck.Play.TryEnd(swiped, out var item))
        {
            deck.Watches.Add(item);
        }

        FlushReelWatches(deck, !swiped);
    }

    private void SuspendReelDeck(ReelDeck? deck)
    {
        if (deck is null)
        {
            return;
        }

        if (deck.Play.TryCheckpoint(out var item))
        {
            deck.Watches.Add(item);
        }

        deck.Play.Rebase();
        FlushReelWatches(deck, true);
    }

    private void FlushReelWatches(ReelDeck deck, bool urgent)
    {
        var now = Environment.TickCount64;
        if (!deck.Watches.ShouldFlush(now, urgent))
        {
            return;
        }

        store.ReportReelWatches(deck.Watches.Drain(now), deck.Session);
    }

    private void OnReelsHidden()
    {
        SuspendReelDeck(presentedDeck);
    }

    private void DrawReelStrip(ImDrawListPtr drawList, Rect area, PostDto[] items, int position, float offset)
    {
        var height = area.Height;
        DrawReelFace(drawList, ShiftRect(area, offset), items[position]);
        if (offset > 0.5f && position > 0)
        {
            DrawReelFace(drawList, ShiftRect(area, offset - height), items[position - 1]);
        }

        if (offset < -0.5f && position + 1 < items.Length)
        {
            DrawReelFace(drawList, ShiftRect(area, offset + height), items[position + 1]);
        }
    }

    private static Rect ShiftRect(Rect rect, float offset) =>
        new(new Vector2(rect.Min.X, rect.Min.Y + offset), new Vector2(rect.Max.X, rect.Max.Y + offset));

    private void DrawReelFace(ImDrawListPtr drawList, Rect rect, PostDto post)
    {
        if (SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent))
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, 0f);
            return;
        }

        var posterUrl = post.Video?.PosterUrl ?? post.MediaUrl;
        var poster = images.Sized(posterUrl, MathF.Max(rect.Width, rect.Height));
        var frame = reelPlayback.FrameOf(post.Id);
        var mediaWidth = frame.HasFrame ? frame.Width : post.Video?.Width ?? post.MediaWidth;
        var mediaHeight = frame.HasFrame ? frame.Height : post.Video?.Height ?? post.MediaHeight;
        var fills = ReelChrome.FillsFrame(mediaWidth, mediaHeight, rect);
        if (!fills && poster is not null)
        {
            var (backdropMin, backdropMax) = ImageFit.Cover(poster.Size.X, poster.Size.Y, rect.Width, rect.Height);
            drawList.AddImage(poster.Handle, rect.Min, rect.Max, backdropMin, backdropMax, ReelBackdropTint);
        }

        var target = fills ? rect : ReelChrome.Contain(mediaWidth, mediaHeight, rect);
        if (frame.HasFrame)
        {
            ReelChrome.DrawTexture(drawList, new ImTextureID(frame.TextureHandle), frame.Width, frame.Height, target,
                fills);
        }
        else if (poster is not null)
        {
            ReelChrome.DrawTexture(drawList, poster.Handle, poster.Size.X, poster.Size.Y, target, fills);
        }

        ModerationOverlay.Draw(drawList, rect.Min, rect.Max, 0f, post.ScanStatus);
    }

    private bool ReelControlsHovered(Rect overlay, bool tabMode, PostDto post)
    {
        var scale = UiScale.Current;
        var mouse = ImGui.GetMousePos();
        var band = ReelChipHeight * scale;
        if (reelPlayback.LibraryMissing && MathF.Abs(mouse.Y - ReelChipY(overlay)) <= band)
        {
            return true;
        }

        if (reelPlayback.FailureOf(post.Id) is not null && MathF.Abs(mouse.Y - overlay.Center.Y) <= band * 1.5f)
        {
            return true;
        }

        var railLeft = overlay.Max.X - ReelRailWidth * scale;
        var lowerTop = overlay.Min.Y + overlay.Height * (1f - ReelScrimShare);
        if (mouse.X >= railLeft && mouse.Y >= lowerTop && mouse.Y <= overlay.Max.Y)
        {
            return true;
        }

        if (mouse.Y >= overlay.Max.Y - ReelInfoHeight(scale) && mouse.Y <= overlay.Max.Y && mouse.X <= railLeft)
        {
            return true;
        }

        return !tabMode && mouse.Y <= overlay.Min.Y + Metrics.Size.TapTarget * scale
            && mouse.X <= overlay.Min.X + Metrics.Size.TapTarget * scale * 1.5f;
    }

    private float ReelInfoHeight(float scale) =>
        (ReelBottomGap + ReelProgressHeight + ReelAvatarRadius * 2f + ReelCaptionGap) * scale
        + reelInfoCaptionHeight;

    private void SyncReelText(PostDto post)
    {
        if (ReferenceEquals(post, reelTextPost) && ReferenceEquals(Loc.Culture, reelTextCulture))
        {
            return;
        }

        reelTextPost = post;
        reelTextCulture = Loc.Culture;
        reelLikeText = post.TotalReactions > 0 ? CountText.Compact(post.TotalReactions) : string.Empty;
        reelCommentText = post.CommentCount > 0 ? CountText.Compact(post.CommentCount) : string.Empty;
        reelHandleText = post.AuthorHandle.Length > 0
            ? "@" + post.AuthorHandle
            : SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
    }

    private void DrawReelChrome(ImDrawListPtr drawList, Rect overlay, PostDto post, ReelProgress progress,
        bool tabMode)
    {
        var scale = UiScale.Current;
        SyncReelText(post);
        var scrimTop = overlay.Min.Y + overlay.Height * (1f - ReelScrimShare);
        var scrimColor = ImGui.GetColorU32(ReelScrim);
        drawList.AddRectFilledMultiColor(new Vector2(overlay.Min.X, scrimTop), overlay.Max, 0u, 0u, scrimColor,
            scrimColor);
        var topScrimBottom = overlay.Min.Y + ReelTopScrimHeight * scale;
        drawList.AddRectFilledMultiColor(overlay.Min, new Vector2(overlay.Max.X, topScrimBottom),
            ImGui.GetColorU32(ReelShadow), ImGui.GetColorU32(ReelShadow), 0u, 0u);
        DrawReelProgress(drawList, overlay, post, progress);
        var bottom = overlay.Max.Y - (ReelBottomGap + ReelProgressHeight) * scale;
        DrawReelRail(drawList, overlay, bottom, post);
        DrawReelInfo(drawList, overlay, bottom, post);
        DrawReelTopBar(drawList, overlay, tabMode);
    }

    private static void DrawReelProgress(ImDrawListPtr drawList, Rect overlay, PostDto post, ReelProgress progress)
    {
        var duration = progress.DurationSeconds > 0d ? progress.DurationSeconds : (post.Video?.DurationMs ?? 0) / 1000d;
        ReelChrome.Progress(drawList, overlay, progress.PositionSeconds, duration);
    }

    private void DrawReelRail(ImDrawListPtr drawList, Rect overlay, float bottom, PostDto post)
    {
        var scale = UiScale.Current;
        var iconSize = ReelRailIconSize * scale;
        var labelHeight = Typography.LineHeight(ReelCountStyle);
        var slot = iconSize + ReelRailSlotGap * scale;
        var labelled = iconSize + ReelRailLabelGap * scale + labelHeight + ReelRailSlotGap * scale;
        var centerX = overlay.Max.X - ReelRailWidth * scale * 0.5f;
        var y = bottom - iconSize * 0.5f;
        if (DrawReelRailButton(drawList, new Vector2(centerX, y), PhoneIcons.Dots, Ink.White, string.Empty,
                Loc.T(L.Aethergram.More), out _))
        {
            OpenPostSheet(post, false, !IsOwnPost(post));
        }

        y -= slot;
        if (DrawReelRailButton(drawList, new Vector2(centerX, y),
                post.Saved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark, Ink.White, string.Empty,
                Loc.T(L.Aethergram.Save), out _))
        {
            store.SetSaved(post.Id, !post.Saved);
        }

        y -= slot;
        if (DrawReelRailButton(drawList, new Vector2(centerX, y), PhoneIcons.Send, Ink.White, string.Empty,
                Loc.T(L.Aethergram.SendTo), out _))
        {
            OpenShare(post);
        }

        y -= labelled;
        if (DrawReelRailButton(drawList, new Vector2(centerX, y), PhoneIcons.MessageCircle, Ink.White,
                reelCommentText, Loc.T(L.Aethergram.Comment), out var commentLabel) || commentLabel)
        {
            OpenReelComments(post);
        }

        y -= labelled;
        var liked = post.MyReaction >= 0;
        if (DrawReelRailButton(drawList, new Vector2(centerX, y), liked ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                liked ? Ink.LikeRed : Ink.White, reelLikeText, Loc.T(L.Aethergram.Like), out var likersLabel))
        {
            store.ToggleLike(post);
        }
        else if (likersLabel)
        {
            OpenUserList(post.Id, UserListKind.Likers);
        }
    }

    private static bool DrawReelRailButton(ImDrawListPtr drawList, Vector2 center, string glyph, Vector4 ink,
        string label, string tooltip, out bool labelClicked)
    {
        labelClicked = false;
        var scale = UiScale.Current;
        var iconSize = ReelRailIconSize * scale;
        var extent = new Vector2(Metrics.Size.TapTarget * 0.5f * scale);
        var min = center - extent;
        var max = center + extent;
        var hovered = UiInteract.Hover(min, max);
        PhoneIcon.Draw(drawList, center + new Vector2(0f, ReelShadowDrop * scale), glyph, ReelShadow, iconSize);
        PhoneIcon.Draw(drawList, center, glyph, ink, hovered ? iconSize * 1.06f : iconSize);
        HoverTooltip.Show(new Rect(min, max), tooltip, HoverLabelSide.Left);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var clicked = UiInteract.Click(min, max, hovered);
        if (label.Length == 0)
        {
            return clicked;
        }

        var labelSize = Typography.Measure(label, ReelCountStyle);
        var labelMin = new Vector2(center.X - labelSize.X * 0.5f, center.Y + iconSize * 0.5f + ReelRailLabelGap * scale);
        Typography.Draw(drawList, labelMin + new Vector2(0f, ReelShadowDrop * scale), label, ReelShadow, ReelCountStyle);
        Typography.Draw(drawList, labelMin, label, Ink.White, ReelCountStyle);
        labelClicked = UiInteract.HoverClick(labelMin, labelMin + labelSize);
        return clicked;
    }

    private void DrawReelInfo(ImDrawListPtr drawList, Rect overlay, float bottom, PostDto post)
    {
        var scale = UiScale.Current;
        var left = overlay.Min.X + ReelInset * scale;
        var right = overlay.Max.X - ReelRailWidth * scale;
        var width = MathF.Max(1f, right - left);
        var captionHeight = DrawReelCaption(drawList, overlay, left, width, bottom, post);
        reelInfoCaptionHeight = captionHeight;
        var radius = ReelAvatarRadius * scale;
        var headerBottom = captionHeight > 0f ? bottom - captionHeight - ReelCaptionGap * scale : bottom;
        var avatarCenter = new Vector2(left + radius, headerBottom - radius);
        DrawCardAvatar(post, avatarCenter, radius, SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle));
        var handleLeft = avatarCenter.X + radius + ReelHeaderGap * scale;
        var handleHeight = Typography.LineHeight(ReelHandleStyle);
        var handleTop = avatarCenter.Y - handleHeight * 0.5f;
        var showFollow = !post.IsFollowing && !IsOwnPost(post);
        var followLabel = showFollow ? Loc.T(L.Aethergram.Follow) : string.Empty;
        var followWidth = showFollow
            ? Typography.Measure(followLabel, TextStyles.FootnoteEmphasized).X + ReelFollowPadX * 2f * scale
            : 0f;
        var handleRoom = MathF.Max(1f, right - handleLeft - (showFollow ? followWidth + ReelHeaderGap * scale : 0f));
        var handle = Typography.FitText(reelHandleText, handleRoom, ReelHandleStyle);
        var handleWidth = Typography.Measure(handle, ReelHandleStyle).X;
        Typography.Draw(drawList, new Vector2(handleLeft, handleTop + ReelShadowDrop * scale), handle, ReelShadow,
            ReelHandleStyle);
        Typography.Draw(drawList, new Vector2(handleLeft, handleTop), handle, Ink.White, ReelHandleStyle);
        if (UiInteract.HoverClick(new Vector2(left, avatarCenter.Y - radius),
                new Vector2(handleLeft + handleWidth, avatarCenter.Y + radius)))
        {
            OpenProfileFromPost(post.AuthorId, post.Id);
        }

        var bylineLeft = handleLeft + handleWidth;
        if (showFollow)
        {
            var pillHeight = ReelFollowHeight * scale;
            var pillMin = new Vector2(bylineLeft + ReelHeaderGap * scale, avatarCenter.Y - pillHeight * 0.5f);
            var pillMax = pillMin + new Vector2(followWidth, pillHeight);
            if (DrawReelFollowPill(drawList, pillMin, pillMax, followLabel))
            {
                store.SetFollow(post.AuthorId, true);
            }

            bylineLeft = pillMax.X;
        }

        DrawCollabByline(drawList, post, bylineLeft, handleTop, right, avatarCenter, radius);
    }

    private static bool DrawReelFollowPill(ImDrawListPtr drawList, Vector2 min, Vector2 max, string label)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(min, max);
        var rounding = (max.Y - min.Y) * 0.5f;
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(ReelShadow), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        drawList.AddRect(min, max, ImGui.GetColorU32(ReelPillStroke), rounding, ImDrawFlags.None, 1f * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, Ink.White, TextStyles.FootnoteEmphasized);
        return UiInteract.Click(min, max, hovered);
    }

    private float DrawReelCaption(ImDrawListPtr drawList, Rect overlay, float left, float width, float bottom,
        PostDto post)
    {
        if (post.Text.Length == 0)
        {
            return 0f;
        }

        var translateKey = new TranslationKey(TranslationSurface.Post, post.Id, post.EditedAtUnix ?? 0);
        var captionView = translation.View(translateKey, post.Text, post.Lang);
        var expanded = string.Equals(expandedReelCaptionId, post.Id, StringComparison.Ordinal);
        using (Plugin.Fonts.Push(CardCaptionScale))
        {
            // A caption with no tag, mention, link or emoji has no rich layout; it draws as plain wrapped lines.
            var layout = reelCaptionLayouts.LayoutFor(captionView.LayoutKey, captionView.Text, post.Mentions, width);
            var lineHeight = layout?.LineHeight ?? ImGui.GetTextLineHeight();
            var fullHeight = layout?.Size.Y ?? Typography.WrapCurrent(captionView.Text, width).Length * lineHeight;
            var fold = TextFold.Measure(fullHeight, lineHeight, ReelCaptionLines, expanded);
            var cappedLines = Math.Max(1, (int)(overlay.Height * ReelExpandedCaptionShare / lineHeight));
            var visibleLines = Math.Min(fold.VisibleLines, cappedLines);
            var visibleHeight = MathF.Min(fold.VisibleHeight, visibleLines * lineHeight);
            var height = fold.Folded ? fold.Height : visibleHeight;
            var origin = new Vector2(left, bottom - height);
            if (layout is null)
            {
                Typography.DrawWrappedLines(drawList, origin, captionView.Text, Ink.BodyInk, width, lineHeight,
                    visibleLines);
            }
            else
            {
                DrawRichBody(drawList, layout, origin, visibleHeight);
            }

            if (fold.DrawReadMore(drawList, origin, Ink.MutedInk, Ink.White))
            {
                expandedReelCaptionId = post.Id;
            }

            return height;
        }
    }

    private void DrawReelTopBar(ImDrawListPtr drawList, Rect overlay, bool tabMode)
    {
        var scale = UiScale.Current;
        var inset = ReelInset * scale;
        if (tabMode)
        {
            var titleTop = overlay.Min.Y + inset;
            var title = Loc.T(L.Aethergram.Reels);
            Typography.Draw(drawList, new Vector2(overlay.Min.X + inset, titleTop + ReelShadowDrop * scale), title,
                ReelShadow, ReelTitleStyle);
            Typography.Draw(drawList, new Vector2(overlay.Min.X + inset, titleTop), title, Ink.White, ReelTitleStyle);
            return;
        }

        var extent = Metrics.Size.TapTarget * 0.5f * scale;
        var center = new Vector2(overlay.Min.X + inset + extent * 0.5f, overlay.Min.Y + inset + extent * 0.5f);
        var hovered = UiInteract.Hover(center - new Vector2(extent), center + new Vector2(extent));
        PhoneIcon.Draw(drawList, center + new Vector2(0f, ReelShadowDrop * scale), PhoneIcons.ChevronLeft, ReelShadow,
            ReelBackGlyph * scale);
        PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronLeft, Ink.White, ReelBackGlyph * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - new Vector2(extent), center + new Vector2(extent), hovered))
        {
            back();
        }
    }

    private void DrawReelStatus(ImDrawListPtr drawList, Rect overlay, PostDto post, ReelFrame current, bool veiled)
    {
        var scale = UiScale.Current;
        var center = overlay.Center;
        if (veiled)
        {
            return;
        }

        if (!reelPlayback.CanPlay)
        {
            DrawReelPlayerChip(drawList, overlay);
        }
        else if (reelPlayback.FailureOf(post.Id) is not null)
        {
            DrawReelFailure(drawList, center);
        }
        else if (!current.HasFrame)
        {
            LoadingPulse.Spinner(center, ReelSpinnerRadius * scale, Ink.White, 1f, drawList);
        }
        else if (reelPlayback.OtherVideoPlaying)
        {
            DrawReelChip(drawList, new Vector2(center.X, overlay.Min.Y + overlay.Height * 0.3f),
                Loc.T(L.Aethergram.ReelPausedForMogCast));
        }

        DrawReelMuteFlash(drawList, center);
    }

    private void DrawReelMuteFlash(ImDrawListPtr drawList, Vector2 center)
    {
        var elapsed = (float)(ImGui.GetTime() - reelFlashAt);
        if (elapsed < 0f || elapsed >= ReelFlashSeconds)
        {
            return;
        }

        var scale = UiScale.Current;
        var alpha = 1f - elapsed / ReelFlashSeconds;
        drawList.AddCircleFilled(center, ReelFlashRadius * scale, ImGui.GetColorU32(ReelChipFill with
        {
            W = ReelChipFill.W * alpha,
        }), 32);
        var glyph = IconGlyph.Of(reelMuted ? FontAwesomeIcon.VolumeMute : FontAwesomeIcon.VolumeUp);
        PhoneIcon.Draw(drawList, center, glyph, Ink.White with { W = alpha }, ReelFlashGlyph * scale);
    }

    private void DrawReelFailure(ImDrawListPtr drawList, Vector2 center)
    {
        var scale = UiScale.Current;
        var message = Loc.T(L.Aethergram.ReelPlayFailed);
        DrawReelChip(drawList, new Vector2(center.X, center.Y - ReelChipHeight * scale * 0.75f), message);
        var label = Loc.T(L.Common.Retry);
        var width = Typography.Measure(label, ReelChipStyle).X + ReelChipPadX * 2f * scale;
        var height = ReelChipHeight * scale;
        var min = new Vector2(center.X - width * 0.5f, center.Y + height * 0.25f);
        if (DrawReelFollowPill(drawList, min, min + new Vector2(width, height), label))
        {
            reelPlayback.Restart();
        }
    }

    private static Rect DrawReelChip(ImDrawListPtr drawList, Vector2 center, string text) =>
        ReelChrome.Chip(drawList, center, text);

    // Past the last reel of a feed that ran dry sits its own end card, so a swipe up ends there and a swipe down
    // returns to the last reel.
    private void StepReelDeck(ReelDeck deck, int step)
    {
        if (deck.AtEnd)
        {
            deck.AtEnd = false;
            return;
        }

        EndReelPlay(deck, true);
        if (step > 0 && deck.Position == deck.Items.Length - 1)
        {
            deck.AtEnd = true;
            return;
        }

        deck.MoveTo(deck.Position + step);
    }

    private void DrawReelEndCard(ImDrawListPtr drawList, Rect area, Rect overlay, PostDto lastReel, ReelDeck deck,
        float offset, bool tabMode)
    {
        drawList.PushClipRect(area.Min, area.Max, true);
        if (offset > 0.5f)
        {
            DrawReelFace(drawList, ShiftRect(area, offset - area.Height), lastReel);
        }

        if (EmptyState.Draw(ShiftRect(overlay, offset), ui, FontAwesomeIcon.Film, Loc.T(L.Aethergram.ReelsCaughtUp),
                Loc.T(L.Aethergram.ReelsCaughtUpHint), Loc.T(L.Common.Refresh)))
        {
            RefreshReelDeck(deck);
        }

        drawList.PopClipRect();
        DrawReelTopBar(drawList, overlay, tabMode);
    }

    private void DrawReelEmpty(Rect overlay, ReelDeck deck)
    {
        var scale = UiScale.Current;
        if (deck.Loading)
        {
            LoadingPulse.Spinner(overlay.Center, ReelSpinnerRadius * scale, Ink.White, 1f,
                ImGui.GetWindowDrawList());
            return;
        }

        if (deck.Lane.Failed)
        {
            feedFailure.Set(deck.Lane.Failure);
            if (EmptyState.Draw(overlay, ui, FontAwesomeIcon.Film, Loc.T(L.Aethergram.ReelsLoadFailed),
                    feedFailure.Text(), Loc.T(L.Common.Retry)))
            {
                deck.Reset();
            }

            return;
        }

        if (EmptyState.Draw(overlay, ui, FontAwesomeIcon.Film, Loc.T(L.Aethergram.ReelsEmpty),
                Loc.T(L.Aethergram.ReelsEmptyHint), Loc.T(L.Common.Retry)))
        {
            deck.Reset();
        }
    }
}
