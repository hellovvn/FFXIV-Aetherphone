using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal enum StickerCardOutcome : byte
{
    None,
    Confirmed,
    Cancelled,
}

internal sealed class StickerTextCard
{
    public const int MaxFields = 3;

    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float MaxDim = 0.55f;
    private const float CardRounding = 24f;
    private const float CardPadding = 20f;
    private const float CardMaxWidth = 340f;
    private const float CardSideMargin = 24f;
    private const float SectionGap = 12f;
    private const float FieldGap = 8f;
    private const float ButtonsTopGap = 16f;
    private const float ButtonGap = 10f;

    private static readonly TextStyle TitleStyle = TextStyles.Title3;
    private static readonly TextStyle NoteStyle = TextStyles.Footnote;
    private static readonly string[] FieldIds = { "##stickerField0", "##stickerField1", "##stickerField2" };

    private readonly string overlayId;
    private readonly string[] drafts = { string.Empty, string.Empty, string.Empty };
    private readonly LocString[] hints = new LocString[MaxFields];
    private readonly int[] hintArguments = new int[MaxFields];
    private readonly int[] maxLengths = new int[MaxFields];
    private readonly string[] resolvedHints = { string.Empty, string.Empty, string.Empty };
    private string hintLanguage = string.Empty;
    private int fieldCount;
    private int requiredFrom;
    private LocString titleKey;
    private string titleText = string.Empty;
    private LocString confirmLabel;
    private string note = string.Empty;
    private Spring reveal;
    private bool open;
    private int focusField = -1;
    private volatile bool busy;
    private volatile string error = string.Empty;

    public StickerTextCard(string overlayId)
    {
        this.overlayId = overlayId;
    }

    public bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    public bool IsOpen => open;

    public bool Busy
    {
        get => busy;
        set => busy = value;
    }

    public void ShowError(string message)
    {
        error = message;
    }

    public string Field(int index) => drafts[index];

    public void Open(LocString title, string customTitle, LocString confirm, string footnote, int fields,
        int firstRequiredField = 0)
    {
        if (!open)
        {
            UiFeedback.Play(UiSound.SheetPresent);
        }

        titleKey = title;
        titleText = customTitle;
        confirmLabel = confirm;
        note = footnote;
        fieldCount = Math.Clamp(fields, 1, MaxFields);
        requiredFrom = Math.Clamp(firstRequiredField, 0, fieldCount - 1);
        busy = false;
        error = string.Empty;
        hintLanguage = string.Empty;
        open = true;
        focusField = 0;
    }

    public void SetField(int index, string text, LocString hint, int maxLength, int hintArgument = 0)
    {
        drafts[index] = text;
        hints[index] = hint;
        hintArguments[index] = hintArgument;
        maxLengths[index] = maxLength;
        hintLanguage = string.Empty;
    }

    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
        busy = false;
        UiFeedback.Play(UiSound.SheetDismiss);
    }

    public StickerCardOutcome Draw(Rect screen, PhoneTheme theme)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, Motion.Sheet, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return StickerCardOutcome.None;
        }

        SyncHints();
        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child(overlayId, screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            return DrawCard(drawList, screen, theme, opacity);
        }
    }

    private StickerCardOutcome DrawCard(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float opacity)
    {
        var scale = UiScale.Current;
        var pad = CardPadding * scale;
        var cardWidth = MathF.Min(CardMaxWidth * scale, screen.Width - CardSideMargin * 2f * scale);
        var innerWidth = cardWidth - pad * 2f;
        var title = titleText.Length > 0 ? titleText : Loc.T(titleKey);
        var titleHeight = Typography.MeasureWrappedBlock(title, TitleStyle, innerWidth).Y;
        var fieldHeight = ImGui.GetFrameHeight();
        var shown = error.Length > 0 ? error : note;
        var noteHeight = shown.Length > 0
            ? Typography.MeasureWrappedBlock(shown, NoteStyle, innerWidth).Y + SectionGap * scale
            : 0f;
        var buttonHeight = Button.LargeHeight * scale;
        var cardHeight = pad + titleHeight + SectionGap * scale + fieldCount * fieldHeight
            + (fieldCount - 1) * FieldGap * scale + SectionGap * scale + noteHeight + ButtonsTopGap * scale
            + buttonHeight + pad;
        var cardMin = new Vector2(screen.Center.X - cardWidth * 0.5f, screen.Center.Y - cardHeight * 0.5f);
        var cardMax = cardMin + new Vector2(cardWidth, cardHeight);
        Squircle.Fill(drawList, cardMin, cardMax, CardRounding * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, opacity)));
        Squircle.Stroke(drawList, cardMin, cardMax, CardRounding * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity)), 1f);

        var left = cardMin.X + pad;
        var right = cardMax.X - pad;
        var y = cardMin.Y + pad;
        Typography.DrawWrappedCentered(new Vector2(screen.Center.X, y), title,
            Palette.WithAlpha(theme.TextStrong, opacity), TitleStyle, innerWidth);
        y += titleHeight + SectionGap * scale;
        var submitted = false;
        for (var index = 0; index < fieldCount; index++)
        {
            var origin = new Vector2(left, y);
            if (open)
            {
                submitted |= DrawField(index, origin, innerWidth, theme);
            }
            else
            {
                DrawResting(drawList, index, origin, innerWidth, fieldHeight, theme, opacity);
            }

            y += fieldHeight + FieldGap * scale;
        }

        y += SectionGap * scale - FieldGap * scale;
        if (shown.Length > 0)
        {
            Typography.DrawWrappedCentered(new Vector2(screen.Center.X, y), shown,
                Palette.WithAlpha(error.Length > 0 ? theme.Danger : theme.TextMuted, opacity), NoteStyle, innerWidth);
            y += noteHeight;
        }

        y += ButtonsTopGap * scale;
        var buttonWidth = (innerWidth - ButtonGap * scale) * 0.5f;
        var cancelRect = new Rect(new Vector2(left, y), new Vector2(left + buttonWidth, y + buttonHeight));
        var confirmRect = new Rect(new Vector2(cancelRect.Max.X + ButtonGap * scale, y), new Vector2(right, y + buttonHeight));
        if (ConfirmDialog.DrawPillButton(cancelRect, Loc.T(L.Common.Cancel), open && !busy, theme, 1f, opacity,
                ConfirmButtonTone.Neutral, "stickercard.cancel") || (open && !busy && ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            Close();
            return StickerCardOutcome.Cancelled;
        }

        var ready = open && !busy && RequiredFilled();
        var confirmed = ConfirmDialog.DrawPillButton(confirmRect, Loc.T(confirmLabel), ready, theme, 1f, opacity,
            ConfirmButtonTone.Primary, "stickercard.confirm");
        return (confirmed || (submitted && ready)) ? StickerCardOutcome.Confirmed : StickerCardOutcome.None;
    }

    private bool RequiredFilled()
    {
        for (var index = requiredFrom; index < fieldCount; index++)
        {
            if (string.IsNullOrWhiteSpace(drafts[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void DrawResting(ImDrawListPtr drawList, int index, Vector2 origin, float width, float height,
        PhoneTheme theme, float opacity)
    {
        drawList.AddRectFilled(origin, origin + new Vector2(width, height),
            ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, theme.SurfaceMuted.W * opacity)));
        var text = drafts[index].Length > 0 ? drafts[index] : resolvedHints[index];
        var padding = ImGui.GetStyle().FramePadding;
        Typography.Draw(drawList, origin + padding, Typography.FitText(text, width - padding.X * 2f, TextStyles.Body),
            Palette.WithAlpha(drafts[index].Length > 0 ? theme.TextStrong : theme.TextMuted, opacity), TextStyles.Body);
    }

    private bool DrawField(int index, Vector2 origin, float width, PhoneTheme theme)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.SetNextItemWidth(width);
        if (focusField == index)
        {
            ImGui.SetKeyboardFocusHere();
            focusField = -1;
        }

        bool submitted;
        using (UiScope.PushColor(ImGuiCol.FrameBg, theme.SurfaceMuted))
        using (UiScope.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Plugin.Fonts.NoticeText(drafts[index]);
            submitted = ImGui.InputTextWithHint(FieldIds[index], resolvedHints[index], ref drafts[index],
                maxLengths[index] * 4, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        if (drafts[index].Length > maxLengths[index])
        {
            drafts[index] = drafts[index][..maxLengths[index]];
        }

        return submitted;
    }

    private void SyncHints()
    {
        var code = Loc.Current.Code;
        if (string.Equals(hintLanguage, code, StringComparison.Ordinal))
        {
            return;
        }

        hintLanguage = code;
        for (var index = 0; index < fieldCount; index++)
        {
            resolvedHints[index] = hintArguments[index] > 0
                ? Loc.T(hints[index], hintArguments[index])
                : Loc.T(hints[index]);
        }
    }
}
