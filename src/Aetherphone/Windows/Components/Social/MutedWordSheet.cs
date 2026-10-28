using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal enum MutedWordSheetAction : byte
{
    None,
    Save,
    Delete,
}

internal sealed class MutedWordSheet
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const string OverlayId = "##mutedWordSheet";
    private const string FieldId = "##mutedWordField";
    private const string FeedsToggleId = "##mutedWord.feeds";
    private const string NotificationsToggleId = "##mutedWord.notifications";
    private const string SaveKey = "##mutedWord.save";
    private const string CancelKey = "##mutedWord.cancel";
    private const string DeleteKey = "##mutedWord.delete";
    private const float MaxDim = 0.45f;
    private const float Rounding = 24f;
    private const float PadX = 18f;
    private const float GrabberWidth = 38f;
    private const float GrabberHeight = 4.5f;
    private const float FieldGap = 6f;
    private const float SectionGap = 14f;
    private const float OverlineGap = 6f;
    private const float ToggleRowHeight = 46f;
    private const float ToggleWidth = 46f;
    private const float ToggleHeight = 28f;
    private const float ChoiceRowHeight = 42f;
    private const float CheckSize = 16f;
    private const float ChipHeight = 34f;
    private const float ChipGap = 8f;
    private const float ChipRounding = 12f;
    private const float ButtonGap = 10f;
    private const float BottomPad = 26f;
    private const float PanelLift = 0.08f;
    private const float PanelAlpha = 0.97f;
    private const float ChipOnAlpha = 0.18f;
    private const float ChipOnStrokeAlpha = 0.5f;
    private const float ChipOnInkLift = 0.38f;
    private const float ChipLabelInset = 6f;

    private static readonly TextStyle TitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle RowStyle = TextStyles.Body;
    private static readonly TextStyle ChipStyle = new(0.86f, FontWeight.Bold);
    private static readonly TextStyle NoteStyle = TextStyles.Footnote;
    private static readonly Vector4 ChipOffFill = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 ChipOffStroke = new(1f, 1f, 1f, 0.09f);
    private static readonly Vector4 GrabberFill = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 RowHairline = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 PanelStroke = new(1f, 1f, 1f, 0.10f);
    private static readonly int[] DurationChoices =
    {
        MutedWordDurations.Forever,
        MutedWordDurations.Day,
        MutedWordDurations.Week,
        MutedWordDurations.Month,
    };

    private Spring reveal;
    private bool open;
    private bool focusPending;
    private int openedFrame;
    private string draft = string.Empty;
    private string checkedDraft = "\0";
    private string normalized = string.Empty;
    private bool phraseValid;
    private bool badHashtag;
    private bool inFeeds;
    private bool inNotifications;
    private bool notFollowing;
    private int duration;
    private bool keepsExpiry;
    private string endsNote = string.Empty;

    public bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    public string? EditingId { get; private set; }

    public string Phrase => normalized;

    public void OpenNew()
    {
        Begin(null);
        draft = string.Empty;
        inFeeds = true;
        inNotifications = true;
        notFollowing = false;
        duration = MutedWordDurations.Forever;
        keepsExpiry = false;
        endsNote = string.Empty;
    }

    public void OpenEdit(MutedWordDto entry)
    {
        Begin(entry.Id);
        draft = entry.Phrase;
        inFeeds = entry.InFeeds;
        inNotifications = entry.InNotifications;
        notFollowing = entry.Audience == MutedWordAudiences.NotFollowing;
        keepsExpiry = entry.ExpiresAtUnix is not null;
        duration = keepsExpiry ? -1 : MutedWordDurations.Forever;
        endsNote = entry.ExpiresAtUnix is { } expires
            ? Loc.T(L.Social.MutedWordEnds, TimeText.Until(expires))
            : string.Empty;
    }

    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
        UiFeedback.Play(UiSound.SheetDismiss);
    }

    public SaveMutedWordRequest Request() =>
        new(normalized, inFeeds, inNotifications,
            notFollowing ? MutedWordAudiences.NotFollowing : MutedWordAudiences.Anyone,
            keepsExpiry && duration < 0 ? null : duration);

    public MutedWordSheetAction Draw(Rect screen, PhoneTheme theme, SocialInk ink)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, Motion.Sheet, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return MutedWordSheetAction.None;
        }

        Validate();
        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child(OverlayId, screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            return DrawPanel(drawList, screen, theme, ink, opacity);
        }
    }

    private void Begin(string? editingId)
    {
        if (!open)
        {
            UiFeedback.Play(UiSound.SheetPresent);
        }

        EditingId = editingId;
        open = true;
        focusPending = editingId is null;
        openedFrame = ImGui.GetFrameCount();
        checkedDraft = "\0";
    }

    private void Validate()
    {
        if (string.Equals(draft, checkedDraft, StringComparison.Ordinal))
        {
            return;
        }

        checkedDraft = draft;
        Plugin.Fonts.NoticeText(draft);
        normalized = MutedWordMatcher.Normalize(draft);
        phraseValid = MutedWordMatcher.IsValid(normalized);
        badHashtag = !phraseValid && normalized.Length > 1 && normalized[0] == '#';
    }

    private MutedWordSheetAction DrawPanel(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, SocialInk ink,
        float opacity)
    {
        var scale = UiScale.Current;
        var padX = PadX * scale;
        var editing = EditingId is not null;
        var noteHeight = Typography.LineHeight(NoteStyle);
        var buttonHeight = Button.LargeHeight * scale;
        var overlineBlock = ListSection.OverlineHeight + OverlineGap * scale;
        var panelHeight = (8f + GrabberHeight + 12f) * scale + Typography.LineHeight(TitleStyle) + 12f * scale
            + SearchBar.HeightUnits * scale + FieldGap * scale + noteHeight + SectionGap * scale
            + overlineBlock + ToggleRowHeight * 2f * scale + SectionGap * scale
            + overlineBlock + ChoiceRowHeight * 2f * scale + SectionGap * scale
            + overlineBlock + ChipHeight * scale + FieldGap * scale + noteHeight + SectionGap * scale
            + buttonHeight + (editing ? ButtonGap * scale + buttonHeight : 0f) + BottomPad * scale;
        var panelTop = screen.Max.Y - panelHeight + panelHeight * (1f - opacity);
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, screen.Max.Y + Rounding * scale);
        var rounding = Rounding * scale;
        var panelFill = Palette.WithAlpha(Palette.Lighten(ink.BackdropTop, PanelLift), PanelAlpha * opacity);
        Squircle.Fill(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(panelFill));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(PanelStroke, PanelStroke.W * opacity)), 1f);
        var interactive = open && reveal.Arrive(1f);
        var left = panelMin.X + padX;
        var right = panelMax.X - padX;
        var width = right - left;

        var grabberMin = new Vector2(screen.Center.X - GrabberWidth * scale * 0.5f, panelTop + 8f * scale);
        drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(GrabberWidth, GrabberHeight) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(GrabberFill, GrabberFill.W * opacity)), GrabberHeight * scale * 0.5f);
        var cursorY = grabberMin.Y + (GrabberHeight + 12f) * scale;
        var title = Loc.T(editing ? L.Social.MutedWordEdit : L.Social.MutedWordAdd);
        Typography.Draw(drawList, new Vector2(left, cursorY), Typography.FitText(title, width, TitleStyle),
            Palette.WithAlpha(ink.TitleInk, opacity), TitleStyle);
        cursorY += Typography.LineHeight(TitleStyle) + 12f * scale;

        var field = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + SearchBar.HeightUnits * scale));
        DrawField(drawList, field, ink, opacity, interactive);
        cursorY += SearchBar.HeightUnits * scale + FieldGap * scale;
        if (badHashtag)
        {
            Typography.Draw(drawList, new Vector2(left, cursorY),
                Typography.FitText(Loc.T(L.Social.MutedWordBadHashtag), width, NoteStyle),
                Palette.WithAlpha(ink.Danger, opacity), NoteStyle);
        }

        cursorY += noteHeight + SectionGap * scale;
        cursorY = DrawOverline(drawList, left, cursorY, width, Loc.T(L.Social.MutedWordMuteIn), ink, opacity);
        inFeeds = DrawToggleRow(drawList, left, right, cursorY, Loc.T(L.Social.MutedWordFeeds), inFeeds,
            FeedsToggleId, theme, ink, opacity, interactive);
        cursorY += ToggleRowHeight * scale;
        inNotifications = DrawToggleRow(drawList, left, right, cursorY, Loc.T(L.Social.MutedWordNotifications),
            inNotifications, NotificationsToggleId, theme, ink, opacity, interactive);
        cursorY += ToggleRowHeight * scale + SectionGap * scale;

        cursorY = DrawOverline(drawList, left, cursorY, width, Loc.T(L.Social.MutedWordFrom), ink, opacity);
        if (DrawChoiceRow(drawList, left, right, cursorY, Loc.T(L.Social.MutedWordAnyone), !notFollowing, ink,
                opacity, interactive))
        {
            notFollowing = false;
        }

        cursorY += ChoiceRowHeight * scale;
        if (DrawChoiceRow(drawList, left, right, cursorY, Loc.T(L.Social.MutedWordNotFollowing), notFollowing, ink,
                opacity, interactive))
        {
            notFollowing = true;
        }

        cursorY += ChoiceRowHeight * scale + SectionGap * scale;
        cursorY = DrawOverline(drawList, left, cursorY, width, Loc.T(L.Social.MutedWordDuration), ink, opacity);
        DrawDurationChips(drawList, left, width, cursorY, ink, opacity, interactive);
        cursorY += ChipHeight * scale + FieldGap * scale;
        if (duration < 0 && endsNote.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(left, cursorY), Typography.FitText(endsNote, width, NoteStyle),
                Palette.WithAlpha(ink.MutedInk, opacity), NoteStyle);
        }

        cursorY += noteHeight + SectionGap * scale;
        var action = DrawButtons(drawList, left, right, cursorY, buttonHeight, ink, opacity, interactive, editing);
        if (action == MutedWordSheetAction.None && interactive && ImGui.GetFrameCount() != openedFrame
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !UiInteract.HoverWindowOnly(panelMin, panelMax, false))
        {
            Close();
        }

        return action;
    }

    private void DrawField(ImDrawListPtr drawList, Rect field, SocialInk ink, float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        SearchBar.Surface(drawList, field, ink.Control, opacity);
        if (!interactive)
        {
            var text = draft.Length > 0 ? draft : Loc.T(L.Social.MutedWordPlaceholder);
            var size = Typography.Measure(text, RowStyle);
            Typography.Draw(drawList, new Vector2(field.Min.X + 16f * scale, field.Center.Y - size.Y * 0.5f),
                Typography.FitText(text, field.Width - 32f * scale, RowStyle),
                Palette.WithAlpha(draft.Length > 0 ? ink.TitleInk : ink.MutedInk, opacity), RowStyle);
            return;
        }

        if (focusPending)
        {
            ImGui.SetCursorScreenPos(field.Min);
            ImGui.SetKeyboardFocusHere();
            focusPending = false;
        }

        GlassField.Text(field, FieldId, Loc.T(L.Social.MutedWordPlaceholder), ref draft, ink.TitleInk, ink.MutedInk,
            16f * scale, MutedWordMatcher.MaxPhraseLength);
    }

    private static float DrawOverline(ImDrawListPtr drawList, float left, float top, float width, string label,
        SocialInk ink, float opacity)
    {
        ListSection.PaintOverline(drawList, new Vector2(left, top), label,
            Palette.WithAlpha(ink.MutedInk, ink.MutedInk.W * opacity), width);
        return top + ListSection.OverlineHeight + OverlineGap * UiScale.Current;
    }

    private static bool DrawToggleRow(ImDrawListPtr drawList, float left, float right, float top, string label,
        bool value, string toggleId, PhoneTheme theme, SocialInk ink, float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        var rowHeight = ToggleRowHeight * scale;
        var centerY = top + rowHeight * 0.5f;
        var track = new Rect(new Vector2(right - ToggleWidth * scale, centerY - ToggleHeight * scale * 0.5f),
            new Vector2(right, centerY + ToggleHeight * scale * 0.5f));
        var fitted = Typography.FitText(label, MathF.Max(1f, track.Min.X - left - 12f * scale), RowStyle);
        var size = Typography.Measure(fitted, RowStyle);
        Typography.Draw(drawList, new Vector2(left, centerY - size.Y * 0.5f), fitted,
            Palette.WithAlpha(ink.BodyInk, opacity), RowStyle);
        drawList.AddLine(new Vector2(left, top + rowHeight - 0.5f), new Vector2(right, top + rowHeight - 0.5f),
            ImGui.GetColorU32(Palette.WithAlpha(RowHairline, RowHairline.W * opacity)), 1f);
        return Toggle.Draw(toggleId, track, value, theme, opacity, interactive);
    }

    private static bool DrawChoiceRow(ImDrawListPtr drawList, float left, float right, float top, string label,
        bool selected, SocialInk ink, float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        var rowMin = new Vector2(left, top);
        var rowMax = new Vector2(right, top + ChoiceRowHeight * scale);
        var hovered = interactive && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(Palette.WithAlpha(ink.HoverTint, ink.HoverTint.W * opacity)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var centerY = top + ChoiceRowHeight * scale * 0.5f;
        var checkCenter = new Vector2(right - CheckSize * 0.5f * scale, centerY);
        var fitted = Typography.FitText(label, MathF.Max(1f, checkCenter.X - left - CheckSize * scale), RowStyle);
        var size = Typography.Measure(fitted, RowStyle);
        Typography.Draw(drawList, new Vector2(left, centerY - size.Y * 0.5f), fitted,
            Palette.WithAlpha(selected ? ink.TitleInk : ink.BodyInk, opacity), RowStyle);
        if (selected)
        {
            PhoneIcon.Draw(drawList, checkCenter, PhoneIcons.Check, Palette.WithAlpha(ink.AccentLink, opacity),
                CheckSize * scale);
        }

        drawList.AddLine(new Vector2(left, rowMax.Y - 0.5f), new Vector2(right, rowMax.Y - 0.5f),
            ImGui.GetColorU32(Palette.WithAlpha(RowHairline, RowHairline.W * opacity)), 1f);
        return UiInteract.Click(rowMin, rowMax, hovered);
    }

    private void DrawDurationChips(ImDrawListPtr drawList, float left, float width, float top, SocialInk ink,
        float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        var chipGap = ChipGap * scale;
        var chipWidth = (width - chipGap * (DurationChoices.Length - 1)) / DurationChoices.Length;
        var chipOnFill = Palette.WithAlpha(ink.Accent, ChipOnAlpha * opacity);
        var chipOnStroke = Palette.WithAlpha(ink.Accent, ChipOnStrokeAlpha * opacity);
        var chipOnInk = Palette.WithAlpha(Palette.Lighten(ink.Accent, ChipOnInkLift), opacity);
        var chipOffInk = Palette.WithAlpha(ink.BodyInk, 0.85f * opacity);
        for (var chipIndex = 0; chipIndex < DurationChoices.Length; chipIndex++)
        {
            var chipMin = new Vector2(left + chipIndex * (chipWidth + chipGap), top);
            var chipMax = new Vector2(chipMin.X + chipWidth, top + ChipHeight * scale);
            var on = duration == DurationChoices[chipIndex];
            var hovered = interactive && UiInteract.Hover(chipMin, chipMax);
            var fill = on ? chipOnFill : hovered ? Palette.WithAlpha(ink.ChipHover, ink.ChipHover.W * opacity)
                : Palette.WithAlpha(ChipOffFill, ChipOffFill.W * opacity);
            var stroke = on ? chipOnStroke : Palette.WithAlpha(ChipOffStroke, ChipOffStroke.W * opacity);
            Squircle.Fill(drawList, chipMin, chipMax, ChipRounding * scale, ImGui.GetColorU32(fill));
            Squircle.Stroke(drawList, chipMin, chipMax, ChipRounding * scale, ImGui.GetColorU32(stroke), 1f);
            Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f,
                Typography.FitText(DurationLabel(chipIndex), chipWidth - ChipLabelInset * 2f * scale, ChipStyle),
                on ? chipOnInk : chipOffInk, ChipStyle);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(chipMin, chipMax, hovered))
            {
                duration = DurationChoices[chipIndex];
            }
        }
    }

    private MutedWordSheetAction DrawButtons(ImDrawListPtr drawList, float left, float right, float top,
        float buttonHeight, SocialInk ink, float opacity, bool interactive, bool editing)
    {
        var scale = UiScale.Current;
        var control = ink.Control;
        var half = (right - left - ButtonGap * scale) * 0.5f;
        var cancel = new Rect(new Vector2(left, top), new Vector2(left + half, top + buttonHeight));
        var save = new Rect(new Vector2(cancel.Max.X + ButtonGap * scale, top), new Vector2(right, top + buttonHeight));
        if (Button.Draw(drawList, cancel, Loc.T(L.Common.Cancel), control, ButtonStyle.Gray, ButtonRole.Normal,
                interactive, false, CancelKey, opacity))
        {
            Close();
            return MutedWordSheetAction.None;
        }

        var canSave = interactive && phraseValid && (inFeeds || inNotifications);
        if (Button.Draw(drawList, save, Loc.T(L.Social.MutedWordSave), control, ButtonStyle.Prominent,
                ButtonRole.Normal, canSave, false, SaveKey, opacity))
        {
            Close();
            return MutedWordSheetAction.Save;
        }

        if (!editing)
        {
            return MutedWordSheetAction.None;
        }

        var deleteTop = top + buttonHeight + ButtonGap * scale;
        var delete = new Rect(new Vector2(left, deleteTop), new Vector2(right, deleteTop + buttonHeight));
        if (!Button.Draw(drawList, delete, Loc.T(L.Common.Delete), control, ButtonStyle.Gray, ButtonRole.Destructive,
                interactive, false, DeleteKey, opacity))
        {
            return MutedWordSheetAction.None;
        }

        Close();
        return MutedWordSheetAction.Delete;
    }

    private static string DurationLabel(int chipIndex) => chipIndex switch
    {
        1 => Loc.T(L.Social.MutedWordDay),
        2 => Loc.T(L.Social.MutedWordWeek),
        3 => Loc.T(L.Social.MutedWordMonth),
        _ => Loc.T(L.Social.MutedWordForever),
    };
}
