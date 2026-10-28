using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

// The trim and cover editor a picked video goes through before it is posted, shared by Aethergram reels and Chirper
// videos. It owns the clip being edited and its frames; the app draws its own header above it.
internal sealed class ReelTrimEditor : IDisposable
{
    private enum TrimDrag : byte
    {
        None,
        Start,
        End,
        Slide,
    }

    public const float SpinnerRadius = 18f;

    private const float EditGap = 12f;
    private const float PreviewRounding = 14f;
    private const float FramingHeight = 34f;
    private const float LabelRowHeight = 22f;
    private const float StripHeight = 52f;
    private const float StripRounding = 8f;
    private const float HandleWidth = 14f;
    private const float HandleHit = 26f;
    private const float HandleGrip = 16f;
    private const float WindowStroke = 3f;
    private const float CropStroke = 2f;
    private const float HintRowHeight = 20f;
    private const float ScrubberHeight = 6f;
    private const float ScrubberRowHeight = 28f;
    private const float OverlayPad = 8f;
    private const float OverlayInset = 10f;
    private const int SecondsKeyShift = 20;
    private const int MaxSeconds = (int)ReelFormat.MaxDurationSeconds;

    private readonly ReelEditorFrames frames;
    private readonly string[] framingLabels = new string[2];
    private ReelEdit edit;
    private string path = string.Empty;
    private TrimDrag drag;
    private bool scrubbing;
    private double slideOffset;
    private CachedText lengthText;
    private CachedText rangeText;
    private CachedText coverTime;
    private CachedText trimHint;

    public ReelTrimEditor(IReelMedia media)
    {
        frames = new ReelEditorFrames(media);
    }

    public bool IsOpen => path.Length > 0;

    public string Path => path;

    public ReelEdit Edit => edit;

    public ReelEditorFrames Frames => frames;

    private double PreviewSeconds => drag switch
    {
        TrimDrag.Start => edit.StartSeconds,
        TrimDrag.End => edit.EndSeconds,
        _ => edit.CoverSeconds,
    };

    public void Open(string source, in ReelMediaInfo info)
    {
        path = source;
        edit = ReelEdit.Begin(info.DurationSeconds, info.DisplayWidth, info.DisplayHeight);
        drag = TrimDrag.None;
        scrubbing = false;
        frames.Open(source, info.DurationSeconds);
    }

    public void Close()
    {
        path = string.Empty;
        edit = default;
        drag = TrimDrag.None;
        scrubbing = false;
        frames.Close();
    }

    public void EndDrag()
    {
        drag = TrimDrag.None;
    }

    // The editor fills the area under an app header; the framing row only shows where the app offers a 9:16 fill.
    public void Draw(Rect area, float margin, PhoneTheme theme, SocialInk ink, in AppPalette palette, bool offersFill)
    {
        var scale = UiScale.Current;
        var gap = EditGap * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var cursor = area.Max.Y - margin;
        var scrubberRow = new Rect(new Vector2(left, cursor - ScrubberRowHeight * scale), new Vector2(right, cursor));
        cursor = scrubberRow.Min.Y;
        var coverLabel = new Rect(new Vector2(left, cursor - LabelRowHeight * scale), new Vector2(right, cursor));
        cursor = coverLabel.Min.Y - gap;
        var hintRow = new Rect(new Vector2(left, cursor - HintRowHeight * scale), new Vector2(right, cursor));
        cursor = hintRow.Min.Y;
        var strip = new Rect(new Vector2(left, cursor - StripHeight * scale), new Vector2(right, cursor));
        cursor = strip.Min.Y - Metrics.Space.Xs * scale;
        var trimLabel = new Rect(new Vector2(left, cursor - LabelRowHeight * scale), new Vector2(right, cursor));
        cursor = trimLabel.Min.Y - gap;
        if (offersFill && edit.CanFill)
        {
            var framingRow = new Rect(new Vector2(left, cursor - FramingHeight * scale), new Vector2(right, cursor));
            DrawFraming(framingRow, palette);
            cursor = framingRow.Min.Y - gap;
        }

        var previewRegion = new Rect(new Vector2(left, area.Min.Y + AppHeader.Height * scale + gap),
            new Vector2(right, cursor));
        DrawTrim(trimLabel, strip, hintRow, scale, theme, ink);
        DrawCover(coverLabel, scrubberRow, scale, ink);
        frames.Show(PreviewSeconds, drag == TrimDrag.None && !scrubbing);
        DrawPreview(previewRegion, scale, theme, ink);
    }

    public void Dispose()
    {
        frames.Dispose();
    }

    private void DrawPreview(Rect region, float scale, PhoneTheme theme, SocialInk ink)
    {
        if (region.Width <= 0f || region.Height <= 0f || edit.DisplayHeight <= 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var preview = ImageFit.CenteredRect(region, (float)edit.DisplayWidth / edit.DisplayHeight);
        var rounding = PreviewRounding * scale;
        if (frames.Preview is { } texture)
        {
            drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(ink.White), rounding, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            LoadingPulse.Spinner(preview.Center, SpinnerRadius * scale, ink.Accent);
        }

        if (edit.Crop is not { } crop)
        {
            return;
        }

        var window = new Rect(preview.Min + new Vector2(crop.X * preview.Width, crop.Y * preview.Height),
            preview.Min + new Vector2((crop.X + crop.Width) * preview.Width, (crop.Y + crop.Height) * preview.Height));
        var scrim = ImGui.GetColorU32(ink.Scrim);
        drawList.AddRectFilled(preview.Min, new Vector2(preview.Max.X, window.Min.Y), scrim);
        drawList.AddRectFilled(new Vector2(preview.Min.X, window.Max.Y), preview.Max, scrim);
        drawList.AddRectFilled(new Vector2(preview.Min.X, window.Min.Y), new Vector2(window.Min.X, window.Max.Y), scrim);
        drawList.AddRectFilled(new Vector2(window.Max.X, window.Min.Y), new Vector2(preview.Max.X, window.Max.Y), scrim);
        drawList.AddRect(window.Min, window.Max, ImGui.GetColorU32(ink.White), 0f, ImDrawFlags.None,
            CropStroke * scale);
        ImGui.SetCursorScreenPos(window.Min);
        ImGui.InvisibleButton("##reel.editor.crop", Vector2.Max(window.Size, Vector2.One));
        var active = ImGui.IsItemActive();
        if (active || ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(edit.FillsHorizontally ? ImGuiMouseCursor.ResizeEw : ImGuiMouseCursor.ResizeNs);
        }

        if (active)
        {
            var delta = ImGui.GetIO().MouseDelta;
            edit.DragFill(edit.FillsHorizontally ? delta.X : delta.Y,
                edit.FillsHorizontally ? preview.Width : preview.Height);
        }

        DrawOverlayCaption(drawList, new Vector2(window.Center.X, window.Max.Y - OverlayInset * scale),
            Loc.T(L.Aethergram.ReelCropHint), window.Width, scale, ink);
    }

    private static void DrawOverlayCaption(ImDrawListPtr drawList, Vector2 bottomCenter, string text, float maxWidth,
        float scale, SocialInk ink)
    {
        var pad = OverlayPad * scale;
        var fitted = Typography.FitText(text, MathF.Max(1f, maxWidth - pad * 4f), TextStyles.Caption1);
        var size = Typography.Measure(fitted, TextStyles.Caption1);
        var min = new Vector2(bottomCenter.X - size.X * 0.5f - pad, bottomCenter.Y - size.Y - pad);
        var max = new Vector2(bottomCenter.X + size.X * 0.5f + pad, bottomCenter.Y);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(ink.Scrim));
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad * 0.5f), fitted, ink.White, TextStyles.Caption1);
    }

    private void DrawFraming(Rect row, in AppPalette palette)
    {
        framingLabels[(int)ReelFraming.Original] = Loc.T(L.Aethergram.ReelFramingOriginal);
        framingLabels[(int)ReelFraming.Fill] = Loc.T(L.Aethergram.ReelFramingFill);
        var current = (int)edit.Framing;
        var picked = SegmentStrip.Draw("reel.editor.framing", row, framingLabels, current, palette);
        if (picked != current)
        {
            edit.SetFraming((ReelFraming)picked);
        }
    }

    private void DrawTrim(Rect label, Rect strip, Rect hintRow, float scale, PhoneTheme theme, SocialInk ink)
    {
        var drawList = ImGui.GetWindowDrawList();
        var lengthKey = (long)Math.Round(edit.LengthSeconds);
        var length = lengthText.IsCurrent(lengthKey)
            ? lengthText.Value
            : lengthText.Store(lengthKey, Loc.T(L.Aethergram.ReelLength, TimeText.MinutesSeconds((int)lengthKey)));
        var startWhole = (int)edit.StartSeconds;
        var endWhole = (int)Math.Ceiling(edit.EndSeconds);
        var rangeKey = ((long)startWhole << SecondsKeyShift) | (uint)endWhole;
        var range = rangeText.IsCurrent(rangeKey)
            ? rangeText.Value
            : rangeText.Store(rangeKey, Loc.T(L.Aethergram.ReelRange, TimeText.MinutesSeconds(startWhole),
                TimeText.MinutesSeconds(endWhole)));
        var lineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(label.Min.X, label.Center.Y - lineHeight * 0.5f), length, ink.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var rangeSize = Typography.Measure(range, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(label.Max.X - rangeSize.X, label.Center.Y - rangeSize.Y * 0.5f), range,
            ink.MutedInk, TextStyles.Footnote);

        DrawStrip(drawList, strip, scale, theme, ink);
        var hint = trimHint.IsCurrent(MaxSeconds)
            ? trimHint.Value
            : trimHint.Store(MaxSeconds, Loc.Plural(L.Aethergram.ReelTrimHint, MaxSeconds));
        Typography.DrawCentered(drawList, hintRow.Center,
            Typography.FitText(hint, hintRow.Width, TextStyles.Caption1), ink.MutedInk, TextStyles.Caption1);
    }

    private void DrawStrip(ImDrawListPtr drawList, Rect strip, float scale, PhoneTheme theme, SocialInk ink)
    {
        var rounding = StripRounding * scale;
        Squircle.Fill(drawList, strip.Min, strip.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
        var slotWidth = strip.Width / ReelEditorFrames.StripCount;
        for (var index = 0; index < ReelEditorFrames.StripCount; index++)
        {
            if (frames.StripAt(index) is not { } thumbnail)
            {
                continue;
            }

            var slotMin = new Vector2(strip.Min.X + slotWidth * index, strip.Min.Y);
            var slotMax = new Vector2(slotMin.X + slotWidth, strip.Max.Y);
            var (uv0, uv1) = ImageFit.Cover(thumbnail.Width, thumbnail.Height, slotWidth, strip.Height);
            drawList.AddImage(thumbnail.Handle, slotMin, slotMax, uv0, uv1);
        }

        var duration = Math.Max(edit.DurationSeconds, ReelEdit.MinimumLengthSeconds);
        var startX = strip.Min.X + (float)(edit.StartSeconds / duration) * strip.Width;
        var endX = strip.Min.X + (float)(edit.EndSeconds / duration) * strip.Width;
        var scrim = ImGui.GetColorU32(ink.Scrim);
        drawList.AddRectFilled(strip.Min, new Vector2(startX, strip.Max.Y), scrim);
        drawList.AddRectFilled(new Vector2(endX, strip.Min.Y), strip.Max, scrim);
        var accent = ImGui.GetColorU32(ink.Accent);
        drawList.AddRect(new Vector2(startX, strip.Min.Y), new Vector2(endX, strip.Max.Y), accent, rounding,
            ImDrawFlags.RoundCornersAll, WindowStroke * scale);
        DrawHandle(drawList, startX, strip, scale, accent, ink);
        DrawHandle(drawList, endX, strip, scale, accent, ink);

        var hit = HandleHit * scale;
        var middleMin = new Vector2(startX + hit * 0.5f, strip.Min.Y);
        var middleMax = new Vector2(MathF.Max(middleMin.X + 1f, endX - hit * 0.5f), strip.Max.Y);
        var mouseSeconds = SecondsAt(strip, ImGui.GetMousePos().X, duration);
        var dragging = TrimDrag.None;
        if (DragArea("##reel.editor.slide", middleMin, middleMax, ImGuiMouseCursor.Hand, out var slideStarted))
        {
            if (slideStarted)
            {
                slideOffset = mouseSeconds - edit.StartSeconds;
            }

            edit.Slide(mouseSeconds - slideOffset);
            dragging = TrimDrag.Slide;
        }

        if (DragArea("##reel.editor.start", new Vector2(startX - hit * 0.5f, strip.Min.Y),
                new Vector2(startX + hit * 0.5f, strip.Max.Y), ImGuiMouseCursor.ResizeEw, out _))
        {
            edit.MoveStart(mouseSeconds);
            dragging = TrimDrag.Start;
        }

        if (DragArea("##reel.editor.end", new Vector2(endX - hit * 0.5f, strip.Min.Y),
                new Vector2(endX + hit * 0.5f, strip.Max.Y), ImGuiMouseCursor.ResizeEw, out _))
        {
            edit.MoveEnd(mouseSeconds);
            dragging = TrimDrag.End;
        }

        drag = dragging;
    }

    private static void DrawHandle(ImDrawListPtr drawList, float x, Rect strip, float scale, uint accent, SocialInk ink)
    {
        var half = HandleWidth * scale * 0.5f;
        var min = new Vector2(x - half, strip.Min.Y);
        var max = new Vector2(x + half, strip.Max.Y);
        drawList.AddRectFilled(min, max, accent, StripRounding * scale * 0.5f, ImDrawFlags.RoundCornersAll);
        var grip = HandleGrip * scale * 0.5f;
        drawList.AddLine(new Vector2(x, strip.Center.Y - grip), new Vector2(x, strip.Center.Y + grip),
            ImGui.GetColorU32(ink.White), CropStroke * scale);
    }

    private static bool DragArea(string id, Vector2 min, Vector2 max, ImGuiMouseCursor cursor, out bool started)
    {
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        started = ImGui.IsItemActivated();
        var active = ImGui.IsItemActive();
        if (active || ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(cursor);
        }

        return active;
    }

    private static double SecondsAt(Rect strip, float x, double duration) =>
        Math.Clamp((x - strip.Min.X) / MathF.Max(1f, strip.Width), 0f, 1f) * duration;

    private void DrawCover(Rect label, Rect scrubberRow, float scale, SocialInk ink)
    {
        var drawList = ImGui.GetWindowDrawList();
        var lineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var title = Loc.T(L.Aethergram.ReelCover);
        Typography.Draw(drawList, new Vector2(label.Min.X, label.Center.Y - lineHeight * 0.5f), title, ink.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var coverKey = (long)edit.CoverSeconds;
        var time = coverTime.IsCurrent(coverKey)
            ? coverTime.Value
            : coverTime.Store(coverKey, TimeText.MinutesSeconds((int)coverKey));
        var timeSize = Typography.Measure(time, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(label.Max.X - timeSize.X, label.Center.Y - timeSize.Y * 0.5f), time,
            ink.MutedInk, TextStyles.Footnote);
        var labelWidth = Typography.Measure(title, TextStyles.SubheadlineEmphasized).X;
        var hintLeft = label.Min.X + labelWidth + Metrics.Space.Sm * scale;
        var hintWidth = label.Max.X - timeSize.X - Metrics.Space.Sm * scale - hintLeft;
        if (hintWidth > 0f)
        {
            var hint = Typography.FitText(Loc.T(L.Aethergram.ReelCoverHint), hintWidth, TextStyles.Caption1);
            var hintHeight = Typography.LineHeight(TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(hintLeft, label.Center.Y - hintHeight * 0.5f), hint, ink.MutedInk,
                TextStyles.Caption1);
        }

        var length = edit.LengthSeconds;
        var value = length > 0d ? (float)((edit.CoverSeconds - edit.StartSeconds) / length) : 0f;
        var track = new Rect(new Vector2(scrubberRow.Min.X, scrubberRow.Center.Y - ScrubberHeight * scale * 0.5f),
            new Vector2(scrubberRow.Max.X, scrubberRow.Center.Y + ScrubberHeight * scale * 0.5f));
        scrubbing = ImGui.IsMouseDown(ImGuiMouseButton.Left) && Scrubber.IsHovered(track);
        var picked = Scrubber.Draw(track, value, ink.Accent, ink.MutedInk, 1f);
        if (MathF.Abs(picked - value) > float.Epsilon)
        {
            edit.SetCover(edit.StartSeconds + picked * length);
        }
    }
}
