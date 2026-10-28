using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Reels;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

// The floating card a video post shows while it converts, uploads and posts in the background: progress, a cancel
// while it can still stop, and Retry or dismiss once it failed. Shared by Aethergram reels and Chirper videos.
internal sealed class ReelSharePill
{
    private const float MinHeight = 56f;
    private const float Pad = 12f;
    private const float IconSize = 20f;
    private const float Gap = 10f;
    private const float BarHeight = 3f;
    private const float ButtonSize = 16f;
    private const float SpinnerRadius = 9f;
    private const float Margin = 12f;
    private const int PercentKeyShift = 8;

    private CachedText status;
    private ReelShareState? failedState;
    private CultureInfo? failedCulture;
    private string failure = string.Empty;

    // Returns true when the player dismissed a failed share, so the app can let the job go.
    public bool Draw(Rect screen, float bottom, ReelShareJob job, AppSkin ui, SocialInk ink, LocString posting)
    {
        var state = job.State;
        var failed = state.Stage == ReelShareStage.Failed;
        if (!failed && !state.Busy)
        {
            return false;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pad = Pad * scale;
        var gap = Gap * scale;
        var icon = IconSize * scale;
        var button = ButtonSize * scale;
        var retryLabel = Loc.T(L.Common.Retry);
        var retryWidth = failed && state.Failure.Retryable
            ? Typography.Measure(retryLabel, TextStyles.SubheadlineEmphasized).X + gap
            : 0f;
        var showsClose = failed || state.Cancellable;
        var trailing = retryWidth + (showsClose ? button + gap : 0f) + (state.Stage == ReelShareStage.Posting
            ? SpinnerRadius * 2f * scale + gap
            : 0f);
        var accent = ink.Accent;
        var left = screen.Min.X + Margin * scale;
        var right = screen.Max.X - Margin * scale;
        var textLeft = left + pad + icon + gap;
        var textWidth = MathF.Max(1f, right - pad - trailing - textLeft);
        var text = failed ? FailureText(state) : StatusText(state.Stage, job.Progress, posting);
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, textWidth).Y;
        var height = MathF.Max(MinHeight * scale, textHeight + pad * 2f);
        var pill = new Rect(new Vector2(left, bottom - height), new Vector2(right, bottom));
        var overPill = UiInteract.HoverOverlay(pill);
        var style = ScreenToastStyle.From(ui);
        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, pill.Min, pill.Max, rounding, ImGui.GetColorU32(style.Panel));
        drawList.AddRect(pill.Min, pill.Max, ImGui.GetColorU32(style.Stroke), rounding, ImDrawFlags.RoundCornersAll,
            1f);
        ProgressRing.CenterIcon(drawList, new Vector2(left + pad + icon * 0.5f, pill.Center.Y),
            failed ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.Film, failed ? ink.Danger : accent, icon);
        Typography.DrawWrappedLeft(new Vector2(textLeft, pill.Center.Y - textHeight * 0.5f), text, style.Ink,
            TextStyles.Subheadline, textWidth);
        if (state.Stage is ReelShareStage.Converting or ReelShareStage.Uploading)
        {
            var barTop = pill.Max.Y - BarHeight * scale - pad * 0.5f;
            var barMin = new Vector2(textLeft, barTop);
            var barMax = new Vector2(textLeft + textWidth, barTop + BarHeight * scale);
            drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Palette.WithAlpha(style.Ink, 0.15f)),
                BarHeight * scale);
            drawList.AddRectFilled(barMin, new Vector2(barMin.X + textWidth * job.Progress, barMax.Y),
                ImGui.GetColorU32(accent), BarHeight * scale);
        }

        var dismissed = false;
        var cursor = right - pad;
        if (showsClose)
        {
            var center = new Vector2(cursor - button * 0.5f, pill.Center.Y);
            if (CloseButton(drawList, center, button, style.Ink, overPill))
            {
                if (failed)
                {
                    dismissed = true;
                }
                else
                {
                    job.Cancel();
                }
            }

            cursor -= button + gap;
        }

        if (state.Stage == ReelShareStage.Posting)
        {
            LoadingPulse.Spinner(new Vector2(cursor - SpinnerRadius * scale, pill.Center.Y), SpinnerRadius * scale,
                accent);
        }

        if (retryWidth <= 0f)
        {
            return dismissed;
        }

        var retrySize = Typography.Measure(retryLabel, TextStyles.SubheadlineEmphasized);
        var retryMin = new Vector2(cursor - retrySize.X, pill.Center.Y - retrySize.Y * 0.5f);
        var retryMax = retryMin + retrySize;
        var retryHovered = overPill && UiInteract.HoverWindowOnly(retryMin, retryMax);
        Typography.Draw(drawList, retryMin, retryLabel, retryHovered ? ink.TitleInk : ink.AccentLink,
            TextStyles.SubheadlineEmphasized);
        if (retryHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(retryMin, retryMax, retryHovered))
        {
            job.Retry();
        }

        return dismissed;
    }

    private static bool CloseButton(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, bool overPill)
    {
        var half = new Vector2(size, size) * 0.5f;
        var hovered = overPill && UiInteract.HoverWindowOnly(center - half, center + half);
        if (hovered)
        {
            drawList.AddCircleFilled(center, size * 0.75f, ImGui.GetColorU32(Palette.WithAlpha(ink, 0.12f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, center, PhoneIcons.X, ink, size);
        return UiInteract.Click(center - half, center + half, hovered);
    }

    private string StatusText(ReelShareStage stage, float progress, LocString posting)
    {
        var percent = (int)MathF.Round(Math.Clamp(progress, 0f, 1f) * 100f);
        var key = ((long)stage << PercentKeyShift) | (uint)percent;
        return status.IsCurrent(key)
            ? status.Value
            : status.Store(key, ReelShareText.Status(stage, progress, posting));
    }

    private string FailureText(ReelShareState state)
    {
        if (ReferenceEquals(state, failedState) && ReferenceEquals(failedCulture, Loc.Culture))
        {
            return failure;
        }

        failedState = state;
        failedCulture = Loc.Culture;
        failure = ReelShareText.Failure(state.Failure);
        return failure;
    }
}
