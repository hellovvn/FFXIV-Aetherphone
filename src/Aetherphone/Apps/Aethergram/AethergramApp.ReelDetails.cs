using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Reels;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float ReelBadgeSize = 18f;
    private const float ReelBadgeInset = 12f;
    // The amount of the server's reel.first coin rule.
    private const int FirstReelBonusCoins = 500;

    private CachedText reelBonusText;

    private float ReelOutputAspect
    {
        get
        {
            var edit = reelEditor.Edit;
            var layout = ReelLayout.Plan(Math.Max(2, edit.DisplayWidth), Math.Max(2, edit.DisplayHeight), edit.Crop);
            return (float)layout.Width / layout.Height;
        }
    }

    private void DrawReelDetails(Rect area)
    {
        personPicker.Gate();
        GateComposeCollabPicker();
        var scale = UiScale.Current;
        reelEditor.Frames.Show(reelEditor.Edit.CoverSeconds, true);
        if (composeTagMode)
        {
            DrawReelTagging(area, scale);
            return;
        }

        DrawComposeHeader(area, Loc.T(L.Aethergram.NewReel), false, ReelDetailsBack, string.Empty, false);
        var busy = store.ReelSharing;
        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var shareRect = new Rect(new Vector2(left, area.Max.Y - margin - ComposeShareHeight * scale),
            new Vector2(right, area.Max.Y - margin));
        var notice = busy ? Loc.T(L.Aethergram.ReelBusy) : composeStatus;
        var noticeHeight = notice.Length > 0
            ? Typography.MeasureWrappedBlock(notice, TextStyles.Footnote, right - left).Y + ComposeStatusGap * scale
            : 0f;
        var bonus = store.FirstReelBonusAvailable ? FirstReelBonusText() : string.Empty;
        var bonusHeight = bonus.Length > 0
            ? Typography.LineHeight(TextStyles.FootnoteEmphasized) + ComposeStatusGap * scale
            : 0f;
        var cardsBottom = shareRect.Min.Y - ComposeCardGap * scale - noticeHeight - bonusHeight;
        var optionsHeight = ComposeRowHeight * (2f + CollabOptionRows(composeCollaborators.Count)) * scale;
        var optionsCard = new Rect(new Vector2(left, cardsBottom - optionsHeight), new Vector2(right, cardsBottom));
        var captionBottom = optionsCard.Min.Y - ComposeCardGap * scale;
        var captionHeight = (ComposeCardPad * 2f + ComposeCaptionFieldHeight + ComposeMetaGap + ComposeMetaRowHeight)
                            * scale;
        var captionCard = new Rect(new Vector2(left, captionBottom - captionHeight), new Vector2(right, captionBottom));
        var previewHalfWidth = area.Width * ComposePreviewWidthFraction * 0.5f;
        var previewRegion = new Rect(new Vector2(area.Center.X - previewHalfWidth, top + ComposePreviewGap * scale),
            new Vector2(area.Center.X + previewHalfWidth, captionCard.Min.Y - ComposePreviewGap * scale));
        var preview = ImageFit.CenteredRect(previewRegion, ReelOutputAspect);
        if (DrawReelPoster(preview, scale, Loc.T(L.Social.ComposeTapToEdit)))
        {
            reelStage = ReelComposeStage.Edit;
            return;
        }

        DrawReelAltChip(preview);
        DrawCaptionCard(captionCard, area, scale, "##reelCaption", true, ref caption, composeMentions);
        if (DrawComposeOptionsCard(optionsCard, scale, ref composeSensitive, CollabRows.Compose))
        {
            composeTagMode = true;
        }

        var lineTop = shareRect.Min.Y - noticeHeight - bonusHeight;
        if (bonus.Length > 0)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(),
                new Vector2(area.Center.X, lineTop + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
                Typography.FitText(bonus, right - left, TextStyles.FootnoteEmphasized), Ink.AccentLink,
                TextStyles.FootnoteEmphasized);
            lineTop += bonusHeight;
        }

        if (noticeHeight > 0f)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, lineTop), notice,
                busy ? Ink.MutedInk : Ink.Danger, TextStyles.Footnote, right - left);
        }

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            var panelBottom = shareRect.Min.Y - ComposeCardGap * scale;
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, panelBottom - panelHeight),
                new Vector2(area.Max.X, panelBottom)), ui, ref caption, MaxCaptionLength);
        }

        var pickedPerson = personPicker.Draw(area, theme, images, lodestone);
        if (pickedPerson is not null)
        {
            PlaceComposeTag(pickedPerson);
        }

        DrawComposeCollabPicker(area);
        if (DrawAccentPill(shareRect, Loc.T(L.Aethergram.Share), !busy))
        {
            ShareReel();
        }
    }

    private string FirstReelBonusText() =>
        reelBonusText.IsCurrent(FirstReelBonusCoins)
            ? reelBonusText.Value
            : reelBonusText.Store(FirstReelBonusCoins, Loc.Plural(L.Aethergram.ReelBonusHint, FirstReelBonusCoins));

    private bool DrawReelPoster(Rect preview, float scale, string tooltip)
    {
        if (preview.Width <= 0f || preview.Height <= 0f)
        {
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rounding = ComposePreviewRounding * scale;
        Elevation.Card(drawList, preview.Min, preview.Max, rounding, scale);
        if (reelEditor.Frames.Preview is not { } texture)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            LoadingPulse.Spinner(preview.Center, ReelTrimEditor.SpinnerRadius * scale, Accent);
            return false;
        }

        var crop = reelEditor.Edit.Crop ?? ReelCrop.Whole;
        ImageFit.DrawLetterboxed(drawList, texture, preview, new Vector2(crop.X, crop.Y),
            new Vector2(crop.X + crop.Width, crop.Y + crop.Height), rounding);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        var badge = ReelBadgeSize * scale;
        var inset = ReelBadgeInset * scale;
        ProgressRing.CenterIcon(drawList,
            new Vector2(preview.Max.X - inset - badge * 0.5f, preview.Min.Y + inset + badge * 0.5f),
            FontAwesomeIcon.Film, Ink.White, badge);
        if (tooltip.Length == 0)
        {
            return false;
        }

        var hovered = UiInteract.Hover(preview.Min, preview.Max);
        HoverTooltip.Show(preview, tooltip, HoverLabelSide.Below);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(preview.Min, preview.Max, hovered);
    }

    private void DrawReelAltChip(Rect preview)
    {
        if (!reelEditor.IsOpen || preview.Width <= 0f || preview.Height <= 0f)
        {
            return;
        }

        var reelPath = reelEditor.Path;
        var text = PostAltTexts.For(composeAltTexts, reelPath);
        if (DrawAltChip(preview, text))
        {
            altTextEditsPost = false;
            altTextPhotoIndex = 0;
            altTextEditor.Open(reelPath, text);
        }
    }

    private void DrawReelTagging(Rect area, float scale)
    {
        if (DrawComposeHeader(area, Loc.T(L.PhotoTag.TagPeople), false, composeExitTagMode, Loc.T(L.Aethergram.Done),
                true))
        {
            composeTagMode = false;
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var hintHeight = Typography.LineHeight(TextStyles.Footnote) + ComposeTagHintGap * scale;
        var previewRegion = new Rect(new Vector2(area.Min.X + margin, top + ComposePreviewGap * scale),
            new Vector2(area.Max.X - margin, area.Max.Y - margin - hintHeight));
        var preview = ImageFit.CenteredRect(previewRegion, ReelOutputAspect);
        DrawReelPoster(preview, scale, string.Empty);
        var drawList = ImGui.GetWindowDrawList();
        var removed = DrawComposeTags(drawList, preview, 0, scale, out var overClose);
        var hovered = !removed && !overClose && UiInteract.Hover(preview.Min, preview.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(preview.Min, preview.Max, hovered))
        {
            PlaceTagAt(preview, 0);
        }

        DrawTaggingFooter(area, preview.Max.Y, margin, hintHeight, scale);
    }

    private void ShareReel()
    {
        if (!reelEditor.IsOpen)
        {
            return;
        }

        var reelPath = reelEditor.Path;
        var reelEdit = reelEditor.Edit;
        var alt = PostAltTexts.For(composeAltTexts, reelPath);
        var request = new ReelShareRequest(reelPath, reelEdit.StartSeconds, reelEdit.EndSeconds,
            reelEdit.CoverSeconds, reelEdit.Crop, caption, ComposeTagInputs(), ComposeCollaboratorIds(),
            alt.Length > 0 ? alt : null, composeSensitive);
        if (!store.StartReelShare(ReelMedia, request))
        {
            composeStatus = Loc.T(L.Aethergram.ReelBusy);
            return;
        }

        CloseComposeNow();
    }
}
