using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float ComposeVideoTileHeight = 140f;
    private const float ComposeVideoGlyph = 19f;
    private const float ComposeVideoBadge = 22f;
    private const float VideoPillMargin = 12f;
    private const float VideoToolGap = 8f;

    private static readonly IReelMedia ChirpVideoMedia = new WindowsReelMedia();

    private readonly ReelTrimEditor videoEditor = new(ChirpVideoMedia);
    private readonly ReelSharePill videoSharePill = new();
    private volatile string? pickedVideoPath;
    private volatile ChirpVideoProbe? videoProbeArrived;
    private int videoProbeTicket;
    private bool videoProbing;
    private bool videoKept;
    private Action? closeVideoEditor;

    private bool ComposeHasVideo => videoEditor.IsOpen;

    private bool ComposeVideoPending => videoProbing || videoEditor.IsOpen;

    private bool ComposeVideoBlocked => composeAttachments.Count > 0 || composePoll.Active;

    private bool ThreadCanGrow => threadDraft.CanAdd && !ComposeVideoPending;

    // A fresh pick that is backed out of leaves nothing behind; one already kept stays with its edits.
    private Action CloseVideoEditor => closeVideoEditor ??= () =>
    {
        composePanel = ComposePanel.None;
        if (!videoKept)
        {
            CloseComposeVideo();
        }
    };

    private void CloseComposeVideo()
    {
        videoEditor.Close();
        videoKept = false;
        videoProbing = false;
        videoProbeTicket++;
        pickedVideoPath = null;
        videoProbeArrived = null;
    }

    private float DrawComposeVideoButton(ImDrawListPtr drawList, float left, float centerY, float iconRadius)
    {
        if (ComposingThread)
        {
            return left;
        }

        var scale = UiScale.Current;
        var center = new Vector2(left + VideoToolGap * scale + iconRadius, centerY);
        var extent = new Vector2(iconRadius, iconRadius);
        var rect = new Rect(center - extent, center + extent);
        UiAnchors.Report("chirper.compose.video", rect);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            drawList.AddCircleFilled(center, iconRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var blocked = !ComposeVideoPending && ComposeVideoBlocked;
        var ink = ComposeVideoPending ? ChirperInk.Accent
            : blocked ? Palette.WithAlpha(ChirperInk.MutedInk, 0.4f)
            : ChirperInk.AccentLink;
        PhoneIcon.Draw(drawList, center, IconGlyph.Of(FontAwesomeIcon.Video), ink, ComposeVideoGlyph * scale);
        var tooltip = ComposeHasVideo ? L.Social.ComposeTapToEdit
            : blocked ? L.Chirper.VideoRidesAlone
            : L.Chirper.AddVideo;
        HoverTooltip.Show(rect, Loc.T(tooltip), HoverLabelSide.Above);
        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return center.X + iconRadius;
        }

        if (blocked)
        {
            toast.Show(Loc.T(L.Chirper.VideoRidesAlone));
        }
        else if (ComposeHasVideo)
        {
            OpenComposeVideoEditor();
        }
        else if (!videoProbing)
        {
            FilePicker.PickVideo(Loc.T(L.Chirper.VideoChooseDialog), picked => pickedVideoPath = picked);
        }

        return center.X + iconRadius;
    }

    private void OpenComposeVideoEditor()
    {
        composeEmoji.Close();
        composePanel = ComposePanel.Video;
    }

    private void AcceptComposeVideo()
    {
        if (Interlocked.Exchange(ref pickedVideoPath, null) is { } picked)
        {
            CloseComposeVideo();
            composeStatus = string.Empty;
            videoProbing = true;
            var ticket = videoProbeTicket;
            _ = Task.Run(() => ProbeComposeVideoAsync(picked, ticket));
        }

        if (Interlocked.Exchange(ref videoProbeArrived, null) is not { } arrived || arrived.Ticket != videoProbeTicket)
        {
            return;
        }

        videoProbing = false;
        var verdict = arrived.Result.Verdict;
        if (verdict != ReelTranscodeFailure.None)
        {
            composeStatus = Loc.T(ReelShareText.ForTranscode(verdict));
            return;
        }

        videoEditor.Open(arrived.Path, arrived.Result.Info);
        videoKept = false;
        OpenComposeVideoEditor();
    }

    private async Task ProbeComposeVideoAsync(string path, int ticket)
    {
        var result = await ChirpVideoMedia.ProbeAsync(path, CancellationToken.None).ConfigureAwait(false);
        videoProbeArrived = new ChirpVideoProbe(path, ticket, result);
    }

    private void DrawComposeVideoEditor(Rect area)
    {
        var scale = UiScale.Current;
        var done = Loc.T(L.Chirper.Done);
        var action = HeaderActionRect(area, done);
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Chirper.VideoEditTitle), ChirperInk.Shared, CloseVideoEditor,
            ScreenTitleStyle, action.Width / scale + HeaderActionGap);
        if (Button.Draw(action, done, ui.Ink, ButtonStyle.Prominent, id: "chirper.video.done"))
        {
            videoEditor.EndDrag();
            videoKept = true;
            composePanel = ComposePanel.None;
            composeFocus = true;
            return;
        }

        videoEditor.Draw(area, CellPadX * scale, theme, ChirperInk.Shared, ui.Palette, false);
    }

    private float ComposeVideoStripHeight() =>
        ComposeVideoPending ? (ComposeVideoTileHeight + 10f) * UiScale.Current : 0f;

    private void DrawComposeVideoTile(ImDrawListPtr drawList, float x, float y, float maxWidth, float scale)
    {
        var height = ComposeVideoTileHeight * scale;
        var rounding = ComposeTileRounding * scale;
        var min = new Vector2(x, y);
        if (!videoEditor.IsOpen)
        {
            var pending = min + new Vector2(height, height);
            Squircle.Fill(drawList, min, pending, rounding, ImGui.GetColorU32(ChirperInk.ChipFill));
            LoadingPulse.Spinner(new Rect(min, pending).Center, ReelTrimEditor.SpinnerRadius * scale, ChirperInk.Accent,
                1f, drawList);
            return;
        }

        var edit = videoEditor.Edit;
        var aspect = edit.DisplayHeight > 0 ? (float)edit.DisplayWidth / edit.DisplayHeight : 1f;
        var max = new Vector2(x + MathF.Min(maxWidth, height * aspect), y + height);
        var tile = new Rect(min, max);
        videoEditor.Frames.Show(edit.CoverSeconds, true);
        if (videoEditor.Frames.Preview is { } texture)
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, tile.Width, tile.Height);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipFill));
            LoadingPulse.Spinner(tile.Center, ReelTrimEditor.SpinnerRadius * scale, ChirperInk.Accent, 1f, drawList);
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipStroke), 1f);
        DrawVideoPlayBadge(drawList, tile.Center, scale);
        DrawVideoDuration(drawList, tile, (int)Math.Round(edit.LengthSeconds * 1000d), scale);
        var tileHovered = UiInteract.Hover(min, max);
        if (tileHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(tile, Loc.T(L.Social.ComposeTapToEdit), HoverLabelSide.Below);
        var editTapped = UiInteract.Click(min, max, tileHovered);
        var badgeRadius = ComposeVideoBadge * scale * 0.5f;
        var badgeCenter = new Vector2(max.X - badgeRadius - 6f * scale, min.Y + badgeRadius + 6f * scale);
        var badgeMin = badgeCenter - new Vector2(badgeRadius, badgeRadius);
        var badgeMax = badgeCenter + new Vector2(badgeRadius, badgeRadius);
        var badgeHovered = UiInteract.Hover(badgeMin, badgeMax);
        drawList.AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(badgeHovered ? Palette.WithAlpha(RemoveChipFill, 0.85f) : RemoveChipFill), 24);
        PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.X, ChirperInk.White, 11f * scale);
        if (UiInteract.Click(badgeMin, badgeMax, badgeHovered))
        {
            CloseComposeVideo();
            return;
        }

        if (editTapped)
        {
            OpenComposeVideoEditor();
        }
    }

    private void SubmitComposeVideo()
    {
        var edit = videoEditor.Edit;
        var request = new ReelShareRequest(videoEditor.Path, edit.StartSeconds, edit.EndSeconds, edit.CoverSeconds, null,
            draft, null, null, null, composeSensitive);
        if (!store.StartVideoShare(ChirpVideoMedia, request, quoteTargetId, composeReplyPolicy))
        {
            composeStatus = Loc.T(L.Chirper.VideoBusy);
            return;
        }

        FinishComposeExtras();
        ClearComposeForm();
        feedScrollTopPending = true;
        router.Pop();
    }

    private void DrainVideoShare()
    {
        if (ComposeVideoPending && !router.IsTransitioning && router.Current.Screen != ChirperScreen.Compose)
        {
            CloseComposeVideo();
        }

        if (store.VideoShare is not { } job)
        {
            return;
        }

        var state = job.State;
        if (state.Stage == ReelShareStage.Done)
        {
            store.AcceptCreatedVideo(state.Created);
            store.DismissVideoShare();
            toast.Show(Loc.T(L.Chirper.VideoPosted));
            return;
        }

        if (state.Stage == ReelShareStage.Cancelled)
        {
            store.DismissVideoShare();
        }
    }

    private void DrawVideoSharePill(Rect screen)
    {
        if (store.VideoShare is not { } job || router.Current.Screen == ChirperScreen.Compose)
        {
            return;
        }

        var scale = UiScale.Current;
        var margin = VideoPillMargin * scale;
        var bottom = router.Depth == 1
            ? TabBar.ContentArea(screen, scale).Max.Y - margin - ComposeFab.Clearance(FabRadius, true)
            : screen.Max.Y - margin;
        if (videoSharePill.Draw(screen, bottom, job, ui, ChirperInk.Shared, L.Chirper.VideoPosting))
        {
            store.DismissVideoShare();
        }
    }

    private sealed record ChirpVideoProbe(string Path, int Ticket, ReelProbeResult Result);
}
