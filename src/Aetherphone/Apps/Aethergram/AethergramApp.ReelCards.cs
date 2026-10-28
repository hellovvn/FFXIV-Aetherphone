using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float FeedReelBadgeRadius = 13f;
    private const float FeedReelBadgeInset = 12f;
    private const float FeedReelBadgeGlyph = 13f;
    private const float ReelTileGlyphScale = 0.8f;

    private static readonly PostGridStyle ReelGrid = new(1.7778f, false, true);

    private readonly ReelSource[] feedReelWindow = new ReelSource[1];
    private readonly DeferredTap pendingReelTap = new();
    private readonly Dictionary<string, (int Views, string Text)> reelViewTexts = new(StringComparer.Ordinal);
    private CultureInfo? reelViewTextsCulture;
    private FeedReelFocus feedReelFocus;
    private PostDto? feedReelCandidate;
    private PostDto? pendingReelTapPost;
    private string? feedReelPlayingId;

    private int DrawFeedReel(Rect imageRect, PostDto post)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent))
        {
            SensitiveVeil.Draw(drawList, imageRect.Min, imageRect.Max, 0f);
            if (UiInteract.HoverClick(imageRect.Min, imageRect.Max))
            {
                SensitiveReveals.Reveal(post.Id);
            }

            return 0;
        }

        var viewportTop = ImGui.GetWindowPos().Y;
        var viewportBottom = viewportTop + ImGui.GetWindowSize().Y;
        var share = FeedReelFocus.VisibleShare(imageRect.Min.Y, imageRect.Max.Y, viewportTop, viewportBottom);
        if (feedReelFocus.Offer(post.Id, share, ImGui.GetFrameCount()))
        {
            feedReelCandidate = post;
        }

        DrawGramImage(drawList, imageRect, post.Video?.PosterUrl ?? post.MediaUrl, 0f, post.ScanStatus);
        var frame = string.Equals(feedReelPlayingId, post.Id, StringComparison.Ordinal)
            ? reelPlayback.FrameOf(post.Id)
            : default;
        if (frame.HasFrame)
        {
            ReelChrome.DrawTexture(drawList, new ImTextureID(frame.TextureHandle), frame.Width, frame.Height, imageRect,
                true);
        }

        DrawFeedReelBadge(drawList, imageRect);
        HandleFeedReelGesture(imageRect, post);
        doubleTapLike.DrawBurst(drawList, imageRect, post.Id);
        return 0;
    }

    private static void DrawFeedReelBadge(ImDrawListPtr drawList, Rect imageRect)
    {
        var scale = UiScale.Current;
        var radius = FeedReelBadgeRadius * scale;
        var inset = FeedReelBadgeInset * scale;
        var center = new Vector2(imageRect.Max.X - inset - radius, imageRect.Max.Y - inset - radius);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ReelChipFill), 24);
        PhoneIcon.Draw(drawList, center, IconGlyph.Of(FontAwesomeIcon.VolumeMute), Ink.White,
            FeedReelBadgeGlyph * scale);
    }

    private void HandleFeedReelGesture(Rect imageRect, PostDto post)
    {
        if (!UiInteract.Hover(imageRect.Min, imageRect.Max))
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (doubleTapLike.Tapped(imageRect, post.Id))
        {
            pendingReelTap.Cancel();
            SocialSeason.Play(UiSound.HalloweenHeartbeat);
            TapGlow.Bloom(TapGlow.Key(post.Id, TapGlow.Like));
            if (post.MyReaction < 0)
            {
                store.ToggleLike(post);
            }

            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pendingReelTap.Arm(post.Id);
            pendingReelTapPost = post;
        }
    }

    private void AdvancePendingReelTap()
    {
        if (!pendingReelTap.Ready(out var postId) || pendingReelTapPost is not { } post
            || !string.Equals(post.Id, postId, StringComparison.Ordinal))
        {
            return;
        }

        pendingReelTapPost = null;
        OpenReel(post);
    }

    // Runs once a frame after the lists drew, so it knows which card won. The Reels viewer owns the player on any
    // frame it drew, even mid-transition.
    private void PresentFeedReel()
    {
        var frame = ImGui.GetFrameCount();
        if (reelPresentedFrame == frame)
        {
            feedReelPlayingId = null;
            return;
        }

        var winner = feedReelFocus.Winner(frame);
        if (winner is null || feedReelCandidate is not { Video: { } video } post
            || !string.Equals(post.Id, winner, StringComparison.Ordinal))
        {
            feedReelPlayingId = null;
            return;
        }

        feedReelWindow[0] = new ReelSource(post.Id, video.Url, video.Width, video.Height);
        var play = ReelPauseRules.ShouldPlay(new ReelPlaybackInputs(reelPlayback.PhoneVisible, true,
            ReelCoverState(), reelPlayback.OtherVideoPlaying, false));
        reelPlayback.Present(feedReelWindow, play, true);
        feedReelPlayingId = post.Id;
    }

    private void DrawReelTileBadges(ImDrawListPtr drawList, Vector2 min, Vector2 max, PostDto post, bool showViews,
        bool pinned)
    {
        var scale = UiScale.Current;
        var badgeCenter = new Vector2(max.X - GridBadgeInset * scale, min.Y + GridBadgeInset * scale);
        if (pinned)
        {
            badgeCenter.X -= GridBadgeGap * scale;
        }

        var glyph = IconGlyph.Of(FontAwesomeIcon.Play);
        var size = GridBadgeSize * scale * ReelTileGlyphScale;
        PhoneIcon.Draw(drawList, badgeCenter + new Vector2(0f, ReelShadowDrop * scale), glyph, ReelShadow, size);
        PhoneIcon.Draw(drawList, badgeCenter, glyph, Ink.White, size);
        if (!showViews || post.Views is not { } views)
        {
            return;
        }

        var scrimTop = max.Y - 34f * scale;
        drawList.AddRectFilledMultiColor(new Vector2(min.X, scrimTop), max, 0u, 0u, ImGui.GetColorU32(Ink.Scrim),
            ImGui.GetColorU32(Ink.Scrim));
        var iconCenter = new Vector2(min.X + 14f * scale, max.Y - 12f * scale);
        PhoneIcon.Draw(drawList, iconCenter, glyph, Ink.White, 11f * scale);
        var label = ReelViewText(post.Id, views);
        var labelSize = Typography.Measure(label, GridOverlayStyle);
        Typography.Draw(drawList, new Vector2(iconCenter.X + 10f * scale, iconCenter.Y - labelSize.Y * 0.5f), label,
            Ink.White, GridOverlayStyle);
    }

    private string ReelViewText(string postId, int views)
    {
        if (!ReferenceEquals(reelViewTextsCulture, Loc.Culture))
        {
            reelViewTexts.Clear();
            reelViewTextsCulture = Loc.Culture;
        }

        if (reelViewTexts.TryGetValue(postId, out var cached) && cached.Views == views)
        {
            return cached.Text;
        }

        var text = CountText.Compact(views);
        reelViewTexts[postId] = (views, text);
        return text;
    }

    private void DrawProfileReels(UserDto user)
    {
        store.EnsureUserReels(user.Id);
        var reels = store.UserReels;
        if (reels.Length == 0 && store.UserReelsLoading)
        {
            InfiniteScroll.DrawLoadingRow(ImGui.GetCursorScreenPos().X + ScrollLayout.StableContentWidth() * 0.5f,
                Ink.MutedInk);
            return;
        }

        DrawPostGrid(reels, L.Aethergram.ReelsEmpty, store.HasMoreUserReels, store.UserReelsLoadingMore, ReelGrid,
            PostSource.Reels);
    }
}
