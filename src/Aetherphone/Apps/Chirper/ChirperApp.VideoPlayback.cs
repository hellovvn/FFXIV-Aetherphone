using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float VideoCardMaxHeight = 320f;
    private const float VideoPlayRadius = 22f;
    private const float VideoPlayGlyph = 18f;
    private const float VideoChipInset = 10f;
    private const float VideoChipHeight = 22f;
    private const float VideoChipPadX = 8f;
    private const float VideoMuteRadius = 13f;
    private const float VideoMuteGlyph = 13f;
    private const float QuoteVideoGlyph = 14f;
    private const float ViewerCloseRadius = 18f;
    private const float ViewerFlashRadius = 34f;
    private const float ViewerFlashGlyph = 30f;
    private const float ViewerFlashSeconds = 0.75f;
    private const float ViewerSpinnerRadius = 15f;
    private const float ViewerPosterDim = 0.35f;

    private static readonly Vector4 VideoBlack = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 VideoWhite = new(1f, 1f, 1f, 1f);
    private static readonly TextStyle VideoChipStyle = TextStyles.FootnoteEmphasized;

    private readonly ReelPlayback reels;
    private readonly ReelSource[] videoWindow = new ReelSource[1];
    private readonly ReelEngineSetup videoSetup = new("Chirper video setup");
    private FeedReelFocus videoFocus;
    private PostDto? videoCandidate;
    private string? videoPlayingId;
    private PostDto? videoViewerPost;
    private bool videoViewerPaused;
    private double videoViewerFlashAt = double.MinValue;

    // A chirp with a video shows the video alone; an older server shape also carries its cover as the photo.
    private static string[] ChirpPhotos(PostDto post) =>
        post.Video is null ? PostMedia.Photos(post) : Array.Empty<string>();

    private static Vector2 VideoBlockSize(VideoDto video, float width, bool isThreadHead)
    {
        var scale = UiScale.Current;
        var aspect = video.Width > 0 && video.Height > 0
            ? Math.Clamp((float)video.Width / video.Height, ReelFormat.TallestAspect, ReelFormat.WidestAspect)
            : 1f;
        var height = MathF.Min(width / aspect, (isThreadHead ? HeadMediaMaxHeight : VideoCardMaxHeight) * scale);
        return new Vector2(MathF.Min(width, height * aspect), height);
    }

    private void DrawChirpVideo(PostDto post, VideoDto video, Rect rect)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = MediaRounding * scale;
        var stroke = ImGui.GetColorU32(ChirperInk.ChipStroke);
        if (SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent))
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, rounding);
            drawList.AddRect(rect.Min, rect.Max, stroke, rounding, ImDrawFlags.RoundCornersAll, 1f);
            if (UiInteract.HoverClick(rect.Min, rect.Max))
            {
                SensitiveReveals.Reveal(post.Id);
            }

            return;
        }

        OfferVideoFocus(post, rect);
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(VideoBlack), rounding);
        var frame = string.Equals(videoPlayingId, post.Id, StringComparison.Ordinal) ? reels.FrameOf(post.Id) : default;
        if (frame.HasFrame)
        {
            DrawVideoPicture(drawList, new ImTextureID(frame.TextureHandle), frame.Width, frame.Height, rect, rounding);
        }
        else if (MediaTexture(video.PosterUrl) is { } poster)
        {
            DrawVideoPicture(drawList, poster.Handle, poster.Size.X, poster.Size.Y, rect, rounding);
        }

        ModerationOverlay.Draw(drawList, rect.Min, rect.Max, rounding, post.ScanStatus);
        if (frame.HasFrame)
        {
            DrawVideoMuteBadge(drawList, rect, scale);
        }
        else
        {
            DrawVideoPlayBadge(drawList, rect.Center, scale);
        }

        DrawVideoDuration(drawList, rect, video.DurationMs, scale);
        drawList.AddRect(rect.Min, rect.Max, stroke, rounding, ImDrawFlags.RoundCornersAll, 1f);
        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            OpenChirpVideo(post);
        }
    }

    private static void DrawVideoPicture(ImDrawListPtr drawList, ImTextureID texture, float width, float height, Rect rect,
        float rounding)
    {
        var (uv0, uv1) = ImageFit.Cover(width, height, rect.Width, rect.Height);
        drawList.AddImageRounded(texture, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }

    private static void DrawVideoPlayBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        drawList.AddCircleFilled(center, VideoPlayRadius * scale, ImGui.GetColorU32(ReelChrome.ChipFill), 32);
        PhoneIcon.Draw(drawList, center, IconGlyph.Of(FontAwesomeIcon.Play), VideoWhite, VideoPlayGlyph * scale);
    }

    private static void DrawVideoMuteBadge(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var radius = VideoMuteRadius * scale;
        var inset = VideoChipInset * scale;
        var center = new Vector2(rect.Max.X - inset - radius, rect.Max.Y - inset - radius);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ReelChrome.ChipFill), 24);
        PhoneIcon.Draw(drawList, center, IconGlyph.Of(FontAwesomeIcon.VolumeMute), VideoWhite, VideoMuteGlyph * scale);
    }

    private static void DrawVideoDuration(ImDrawListPtr drawList, Rect rect, int durationMs, float scale)
    {
        var text = TimeText.MinutesSeconds((durationMs + 500) / 1000);
        var size = Typography.Measure(text, VideoChipStyle);
        var height = VideoChipHeight * scale;
        var inset = VideoChipInset * scale;
        var min = new Vector2(rect.Min.X + inset, rect.Max.Y - inset - height);
        var max = new Vector2(min.X + size.X + VideoChipPadX * 2f * scale, min.Y + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(ReelChrome.ChipFill), height * 0.5f);
        Typography.DrawCentered(drawList, new Rect(min, max).Center, text, VideoWhite, VideoChipStyle);
    }

    private void DrawVideoThumb(ImDrawListPtr drawList, PostDto post, VideoDto video, Rect thumb, float rounding)
    {
        if (SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent))
        {
            SensitiveVeil.Draw(drawList, thumb.Min, thumb.Max, rounding);
            return;
        }

        if (MediaTexture(video.PosterUrl) is { } poster)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(poster.Size);
            drawList.AddImageRounded(poster.Handle, thumb.Min, thumb.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            Squircle.Fill(drawList, thumb.Min, thumb.Max, rounding, ImGui.GetColorU32(ChirperInk.ChipFill));
        }

        var scale = UiScale.Current;
        drawList.AddCircleFilled(thumb.Center, QuoteVideoGlyph * scale, ImGui.GetColorU32(ReelChrome.ChipFill), 24);
        PhoneIcon.Draw(drawList, thumb.Center, IconGlyph.Of(FontAwesomeIcon.Play), VideoWhite, QuoteVideoGlyph * scale);
    }

    private void OfferVideoFocus(PostDto post, Rect rect)
    {
        var viewportTop = ImGui.GetWindowPos().Y;
        var viewportBottom = viewportTop + ImGui.GetWindowSize().Y;
        var share = FeedReelFocus.VisibleShare(rect.Min.Y, rect.Max.Y, viewportTop, viewportBottom);
        if (videoFocus.Offer(post.Id, share, ImGui.GetFrameCount()))
        {
            videoCandidate = post;
        }
    }

    // Runs once a frame after the lists drew, so it knows which card won, as Aethergram's home feed does.
    private void PresentChirpVideo()
    {
        var frame = ImGui.GetFrameCount();
        var winner = videoFocus.Winner(frame);
        if (winner is null || videoCandidate is not { Video: { } video } post
            || !string.Equals(post.Id, winner, StringComparison.Ordinal))
        {
            videoPlayingId = null;
            return;
        }

        videoWindow[0] = new ReelSource(post.Id, video.Url, video.Width, video.Height);
        var play = ReelPauseRules.ShouldPlay(new ReelPlaybackInputs(reels.PhoneVisible, true, VideoCoverState(),
            reels.OtherVideoPlaying, false));
        reels.Present(videoWindow, play, true);
        videoPlayingId = post.Id;
    }

    private ReelCover VideoCoverState()
    {
        var covered = sheet.IsOpen || filterSheet.IsOpen || avatarLightbox.Expanded || confirm.Active is not null;
        return covered ? ReelCover.Sheet : ReelCover.None;
    }

    private void OpenChirpVideo(PostDto post)
    {
        videoViewerPost = post;
        videoViewerPaused = false;
        videoViewerFlashAt = double.MinValue;
    }

    private void CloseChirpVideo()
    {
        videoViewerPost = null;
    }

    private void DrawChirpVideoViewer(Rect screen, Rect content, PostDto post, VideoDto video)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(VideoBlack));
        if (reels.LibraryMissing)
        {
            DrawViewerPoster(drawList, content, video, ViewerPosterDim);
            if (videoSetup.Draw(content, ui, theme, reels.Dependencies, L.Chirper.VideoSetupTitle,
                    L.Chirper.VideoSetupBody))
            {
                CloseChirpVideo();
                return;
            }

            DrawViewerClose(drawList, content, scale);
            return;
        }

        videoWindow[0] = new ReelSource(post.Id, video.Url, video.Width, video.Height);
        var play = !videoViewerPaused && ReelPauseRules.ShouldPlay(new ReelPlaybackInputs(reels.PhoneVisible, true,
            ReelCover.None, reels.OtherVideoPlaying, false));
        reels.Present(videoWindow, play, false);
        var frame = reels.FrameOf(post.Id);
        if (frame.HasFrame)
        {
            var target = ReelChrome.Contain(frame.Width, frame.Height, content);
            ReelChrome.DrawTexture(drawList, new ImTextureID(frame.TextureHandle), frame.Width, frame.Height, target,
                false);
        }
        else
        {
            DrawViewerPoster(drawList, content, video, 1f);
        }

        if (UiInteract.HoverClick(content.Min, content.Max))
        {
            videoViewerPaused = !videoViewerPaused;
            videoViewerFlashAt = ImGui.GetTime();
        }

        var progress = reels.Progress;
        var duration = progress.DurationSeconds > 0d ? progress.DurationSeconds : video.DurationMs / 1000d;
        ReelChrome.Progress(drawList, content, frame.HasFrame ? progress.PositionSeconds : 0d, duration);
        DrawViewerStatus(drawList, content, post, frame, scale);
        DrawViewerClose(drawList, content, scale);
    }

    private void DrawViewerPoster(ImDrawListPtr drawList, Rect content, VideoDto video, float alpha)
    {
        if (images.Sized(video.PosterUrl, MathF.Max(content.Width, content.Height)) is not { } poster)
        {
            return;
        }

        var target = ReelChrome.Contain(poster.Size.X, poster.Size.Y, content);
        drawList.AddImage(poster.Handle, target.Min, target.Max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(VideoWhite with { W = alpha }));
    }

    private void DrawViewerStatus(ImDrawListPtr drawList, Rect content, PostDto post, ReelFrame frame, float scale)
    {
        var center = content.Center;
        var chipCenter = new Vector2(center.X, content.Min.Y + content.Height * 0.3f);
        if (!reels.CanPlay)
        {
            ReelChrome.Chip(drawList, chipCenter, Loc.T(L.Aethergram.ReelVideoUnavailable));
        }
        else if (reels.FailureOf(post.Id) is not null)
        {
            ReelChrome.Chip(drawList, new Vector2(center.X, center.Y - ReelChrome.ChipHeight * scale * 0.75f),
                Loc.T(L.Chirper.VideoPlayFailed));
            var retry = ReelChrome.Chip(drawList, new Vector2(center.X, center.Y + ReelChrome.ChipHeight * scale * 0.75f),
                Loc.T(L.Common.Retry));
            if (UiInteract.HoverClick(retry.Min, retry.Max))
            {
                reels.Restart();
            }
        }
        else if (!frame.HasFrame)
        {
            LoadingPulse.Spinner(center, ViewerSpinnerRadius * scale, VideoWhite, 1f, drawList);
        }
        else if (reels.OtherVideoPlaying)
        {
            ReelChrome.Chip(drawList, chipCenter, Loc.T(L.Aethergram.ReelPausedForMogCast));
        }
        else if (videoViewerPaused)
        {
            DrawVideoPlayBadge(drawList, center, scale);
        }

        DrawViewerFlash(drawList, center, scale);
    }

    private void DrawViewerFlash(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var elapsed = (float)(ImGui.GetTime() - videoViewerFlashAt);
        if (elapsed < 0f || elapsed >= ViewerFlashSeconds || videoViewerPaused)
        {
            return;
        }

        var alpha = 1f - elapsed / ViewerFlashSeconds;
        drawList.AddCircleFilled(center, ViewerFlashRadius * scale,
            ImGui.GetColorU32(ReelChrome.ChipFill with { W = ReelChrome.ChipFill.W * alpha }), 32);
        PhoneIcon.Draw(drawList, center, IconGlyph.Of(FontAwesomeIcon.Play), VideoWhite with { W = alpha },
            ViewerFlashGlyph * scale);
    }

    private void DrawViewerClose(ImDrawListPtr drawList, Rect content, float scale)
    {
        var radius = ViewerCloseRadius * scale;
        var inset = Metrics.Space.Md * scale;
        var center = new Vector2(content.Min.X + inset + radius, content.Min.Y + inset + radius);
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? ReelChrome.ChipFill with { W = 0.8f } : ReelChrome.ChipFill), 24);
        PhoneIcon.Draw(drawList, center, PhoneIcons.X, VideoWhite, radius);
        HoverTooltip.Show(new Rect(center - extent, center + extent), Loc.T(L.Common.Close), HoverLabelSide.Below);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - extent, center + extent, hovered))
        {
            CloseChirpVideo();
        }
    }
}
