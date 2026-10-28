using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float ScheduleToolbarGap = 8f;
    private const float ScheduleGlyphSize = 19f;
    private const float ScheduleFieldHeight = 44f;
    private const long ScheduleTextRefreshSeconds = 30;

    private static readonly TextStyle ScheduleLabelStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle ScheduleHintStyle = TextStyles.Footnote;

    private long composeScheduleAtUnix;
    private int scheduleDayOffset;
    private int scheduleMinuteOfDay;
    private Action? scheduleEarlierDay;
    private Action? scheduleLaterDay;
    private ScheduleText scheduleSubtitle;
    private ScheduleText scheduleDayLabel;
    private ScheduleText scheduleHint;

    private Action ScheduleEarlierDay => scheduleEarlierDay ??= () => scheduleDayOffset = Math.Max(0, scheduleDayOffset - 1);

    private Action ScheduleLaterDay => scheduleLaterDay ??= () =>
        scheduleDayOffset = Math.Min(ScheduleRules.MaximumLeadDays, scheduleDayOffset + 1);

    private LocString ComposeActionLabel => composeScheduleAtUnix <= 0 ? L.Chirper.ChirpAction
        : EditingScheduled ? L.Chirper.Save
        : L.Social.ScheduleAction;

    private bool ComposeScheduleBlocked => composeScheduleAtUnix > 0 && (ComposeHasPhotos() || ComposeVideoPending);

    private bool ComposeHasPhotos() => composeAttachments.Count > 0 && AttachedComposeGif() is null;

    private string ComposeScheduleSubtitle()
    {
        if (composeScheduleAtUnix <= 0)
        {
            return string.Empty;
        }

        if (!scheduleSubtitle.Fresh(composeScheduleAtUnix))
        {
            scheduleSubtitle.Store(composeScheduleAtUnix,
                Loc.T(L.Social.ScheduleWillPost, TimeText.FutureMoment(composeScheduleAtUnix)));
        }

        return scheduleSubtitle.Value;
    }

    private float DrawComposeScheduleButton(ImDrawListPtr drawList, float left, float centerY, float iconRadius)
    {
        var scale = UiScale.Current;
        var center = new Vector2(left + ScheduleToolbarGap * scale + iconRadius, centerY);
        var extent = new Vector2(iconRadius, iconRadius);
        var rect = new Rect(center - extent, center + extent);
        UiAnchors.Report("chirper.compose.schedule", rect);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            drawList.AddCircleFilled(center, iconRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var refusal = ComposeVideoPending ? L.Chirper.ScheduleNoVideo : L.Social.ScheduleNoPhotos;
        var blocked = ComposeHasPhotos() || ComposeVideoPending;
        var ink = composeScheduleAtUnix > 0 ? ChirperInk.Accent
            : blocked ? Palette.WithAlpha(ChirperInk.MutedInk, 0.4f)
            : ChirperInk.AccentLink;
        PhoneIcon.Draw(drawList, center, PhoneIcons.Clock, ink, ScheduleGlyphSize * scale);
        HoverTooltip.Show(rect, Loc.T(blocked ? refusal : L.Social.ScheduleLater), HoverLabelSide.Above);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            if (blocked)
            {
                toast.Show(Loc.T(refusal));
            }
            else
            {
                OpenSchedulePanel();
            }
        }

        return center.X + iconRadius;
    }

    private void OpenSchedulePanel()
    {
        composeEmoji.Close();
        var start = composeScheduleAtUnix > 0 ? composeScheduleAtUnix : ScheduleRules.Suggested(DateTime.Now);
        (scheduleDayOffset, scheduleMinuteOfDay) = ScheduleRules.FromUnix(start, DateTime.Now.Date);
        composePanel = ComposePanel.Schedule;
    }

    private void DrawSchedulePanel(Rect area)
    {
        var scale = UiScale.Current;
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Social.ScheduleTitle), ChirperInk.Shared, CloseComposePanel,
            ScreenTitleStyle);
        var drawList = ImGui.GetWindowDrawList();
        var left = area.Min.X + CellPadX * scale;
        var right = area.Max.X - CellPadX * scale;
        var width = right - left;
        var fieldHeight = ScheduleFieldHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var y = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Lg * scale;
        y = DrawScheduleLabel(drawList, left, y, width, Loc.T(L.Social.ScheduleDate)) + gap;
        StepperField.Draw(ui, new Rect(new Vector2(left, y), new Vector2(right, y + fieldHeight)), ScheduleDayText(),
            scale, ScheduleEarlierDay, ScheduleLaterDay);
        y += fieldHeight + Metrics.Space.Lg * scale;
        y = DrawScheduleLabel(drawList, left, y, width, Loc.T(L.Social.ScheduleTime)) + gap;
        scheduleMinuteOfDay = TimeOfDayField.Draw(ui, new Rect(new Vector2(left, y), new Vector2(right, y + fieldHeight)),
            scheduleMinuteOfDay, scale);
        y += fieldHeight + Metrics.Space.Lg * scale;

        var candidate = ScheduleRules.ToUnix(scheduleDayOffset, scheduleMinuteOfDay, DateTime.Now.Date);
        var issue = ScheduleRules.Check(candidate, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        y += Typography.DrawWrappedLeft(new Vector2(left, y), ScheduleHintText(candidate, issue),
            issue == ScheduleIssue.None ? ChirperInk.MutedInk : ChirperInk.Danger, ScheduleHintStyle, width);
        y += Metrics.Space.Lg * scale;

        var buttonHeight = Button.LargeHeight * scale;
        var confirmRect = new Rect(new Vector2(left, y), new Vector2(right, y + buttonHeight));
        UiAnchors.Report("chirper.schedule.confirm", confirmRect);
        if (Button.Draw(confirmRect, Loc.T(L.Social.Confirm), ui.Ink, ButtonStyle.Prominent,
                enabled: issue == ScheduleIssue.None, id: "chirper.schedule.confirm"))
        {
            composeScheduleAtUnix = candidate;
            composeStatus = string.Empty;
            composePanel = ComposePanel.None;
        }

        if (composeScheduleAtUnix <= 0)
        {
            return;
        }

        y += buttonHeight + gap;
        if (Button.Draw(new Rect(new Vector2(left, y), new Vector2(right, y + buttonHeight)),
                Loc.T(L.Social.ScheduleClear), ui.Ink, ButtonStyle.Plain, id: "chirper.schedule.clear"))
        {
            composeScheduleAtUnix = 0;
            composePanel = ComposePanel.None;
        }
    }

    private static float DrawScheduleLabel(ImDrawListPtr drawList, float left, float top, float width, string label)
    {
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(label, width, ScheduleLabelStyle),
            ChirperInk.MutedInk, ScheduleLabelStyle);
        return top + Typography.LineHeight(ScheduleLabelStyle);
    }

    private string ScheduleDayText()
    {
        var dayStart = ScheduleRules.ToUnix(scheduleDayOffset, 12 * 60, DateTime.Now.Date);
        if (!scheduleDayLabel.Fresh(dayStart))
        {
            scheduleDayLabel.Store(dayStart, TimeText.FutureDayLabel(dayStart));
        }

        return scheduleDayLabel.Value;
    }

    private string ScheduleHintText(long candidate, ScheduleIssue issue)
    {
        var key = candidate * 4 + (long)issue;
        if (scheduleHint.Fresh(key))
        {
            return scheduleHint.Value;
        }

        var text = issue switch
        {
            ScheduleIssue.TooSoon => Loc.T(L.Social.ScheduleTooSoon, ScheduleRules.MinimumLeadMinutes),
            ScheduleIssue.TooLate => Loc.T(L.Social.ScheduleTooLate, ScheduleRules.MaximumLeadDays),
            _ => Loc.T(L.Social.ScheduleWillPost, TimeText.FutureMoment(candidate)),
        };
        scheduleHint.Store(key, text);
        return text;
    }

    private bool TrySubmitScheduled()
    {
        if (composeScheduleAtUnix <= 0)
        {
            return false;
        }

        if (store.Posting)
        {
            return true;
        }

        composeStatus = string.Empty;
        composeFailure.Clear();
        if (ComposeHasPhotos())
        {
            composeStatus = Loc.T(L.Social.ScheduleNoPhotos);
            return true;
        }

        var issue = ScheduleRules.Check(composeScheduleAtUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (issue != ScheduleIssue.None)
        {
            composeStatus = ScheduleHintText(composeScheduleAtUnix, issue);
            return true;
        }

        if (ReportComposePollIssue())
        {
            return true;
        }

        var gif = AttachedComposeGif();
        var request = new CreatePostRequest(draft.Trim(), quoteTargetId,
            MediaWidth: gif?.Item.Width ?? 0,
            MediaHeight: gif?.Item.Height ?? 0,
            Sensitive: composeSensitive && gif is not null,
            Poll: composePoll.Active ? composePoll.ToInput() : null,
            GifUrl: gif?.Item.Url,
            GifSlug: gif?.Item.Slug,
            ReplyPolicy: composeReplyPolicy);
        SendScheduled(request, ok =>
        {
            if (ok && gif is not null && !ReferenceEquals(gif, restoredScheduledGif))
            {
                gifSearch.RegisterShare(gif);
            }

            composeOutcome = ok ? 1 : 2;
        });
        return true;
    }

    private struct ScheduleText
    {
        private long key;
        private int format;
        private LanguageInfo? language;
        private long builtAt;
        private string? text;

        public readonly string Value => text ?? string.Empty;

        public readonly bool Fresh(long candidateKey) =>
            text is not null && key == candidateKey && format == TimeText.FormatVersion
            && ReferenceEquals(language, Loc.Current)
            && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - builtAt < ScheduleTextRefreshSeconds;

        public void Store(long candidateKey, string value)
        {
            key = candidateKey;
            format = TimeText.FormatVersion;
            language = Loc.Current;
            builtAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            text = value;
        }
    }
}
