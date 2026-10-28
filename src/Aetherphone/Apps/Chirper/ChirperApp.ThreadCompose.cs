using Aetherphone.Core;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float ThreadBoxMinHeight = 40f;
    private const float ThreadBoxTextPad = 20f;
    private const float ThreadRemoveRadius = 11f;

    private readonly ChirperThreadDraft threadDraft = new();
    private readonly MentionAutocomplete?[] threadMentions = new MentionAutocomplete?[ChirperThreadDraft.MaxFollowUps];
    private readonly MentionPopup threadMentionPopup = new();

    private bool ComposingThread => threadDraft.Count > 0;

    private float OpeningInputHeight(float measuredText, float desired, float available)
    {
        var scale = UiScale.Current;
        var floor = ThreadBoxMinHeight * scale;
        if (ComposingThread)
        {
            return MathF.Max(floor, measuredText + ThreadBoxTextPad * scale);
        }

        var addRow = ThreadCanGrow ? ThreadAddRowHeight() : 0f;
        return MathF.Max(floor, MathF.Min(desired, available - addRow));
    }

    private static float ThreadAddRowHeight()
    {
        return (RoundButton.SmallRadius * 2f + Metrics.Space.Md) * UiScale.Current;
    }

    private bool ThreadDraftReady()
    {
        return threadDraft.AllWritten(MaxPostLength);
    }

    private ref string FocusedComposeText()
    {
        if (!threadDraft.FocusesFollowUp)
        {
            return ref draft;
        }

        return ref threadDraft.Text(threadDraft.FocusedBox - 1);
    }

    private List<string> FocusedAttachments() =>
        threadDraft.FocusesFollowUp ? threadDraft.Attachments(threadDraft.FocusedBox - 1) : composeAttachments;

    private bool FocusedTakesPoll => !threadDraft.FocusesFollowUp;

    private static bool HoldsGif(List<string> attachments) => attachments.Count > 0 && GifMedia.IsGif(attachments[0]);

    private bool IsComposeGif(string path) =>
        (AttachedComposeGif() is { } gif && string.Equals(gif.Item.Url, path, StringComparison.Ordinal))
        || threadDraft.GifFor(path) is not null;

    private MentionAutocomplete ThreadMentions(int index) =>
        threadMentions[index] ??= new MentionAutocomplete(store.NewMentionSuggestions());

    private void PruneComposeAltTexts()
    {
        if (composeAltTexts.Count == 0)
        {
            return;
        }

        foreach (var path in composeAltTexts.Keys)
        {
            if (!composeAttachments.Contains(path) && !threadDraft.Holds(path))
            {
                composeAltTexts.Remove(path);
            }
        }
    }

    private void DrawThreadFollowUps(Rect area, ImDrawListPtr drawList, Vector2 openingAvatar, float inputX,
        float inputWidth, float openingBottom)
    {
        var scale = UiScale.Current;
        var radius = FeedAvatarRadius * scale;
        var gap = Metrics.Space.Md * scale;
        var lineInset = Metrics.Space.Xxs * scale;
        var lineColor = ImGui.GetColorU32(ThreadLine);
        var lineWidth = ThreadLineWidth * scale;
        var removeRadius = ThreadRemoveRadius * scale;
        var framePadding = ImGui.GetStyle().FramePadding.X;
        var fieldWidth = MathF.Max(1f, inputWidth - removeRadius * 2f - Metrics.Space.Xs * scale);
        var wrapWidth = fieldWidth - framePadding * 2f - Metrics.Space.Xxs * scale;
        var stripGap = Metrics.Space.Xs * scale;
        var me = store.Me;
        if (composeFieldActive)
        {
            threadDraft.FocusedBox = ChirperThreadDraft.OpeningBox;
        }

        if (threadDraft.FocusRequest == ChirperThreadDraft.OpeningBox)
        {
            threadDraft.FocusRequest = -1;
            composeFocus = true;
        }

        var previousAvatarBottom = openingAvatar.Y + radius;
        var cursorY = MathF.Max(openingBottom, previousAvatarBottom) + gap;
        var removeIndex = -1;
        for (var index = 0; index < threadDraft.Count; index++)
        {
            var boxTop = cursorY;
            var avatarCenter = new Vector2(openingAvatar.X, boxTop + radius);
            drawList.AddLine(new Vector2(openingAvatar.X, previousAvatarBottom + lineInset),
                new Vector2(openingAvatar.X, avatarCenter.Y - radius - lineInset), lineColor, lineWidth);
            if (me is not null)
            {
                DrawAvatar(drawList, avatarCenter, radius, me.Name, me.World, me.AvatarUrl, 0.95f, 48,
                    Frames.Of(me.FrameId));
            }

            ref var text = ref threadDraft.Text(index);
            var measured = text.Length == 0
                ? Typography.Measure("Ag", ComposeInputStyle).Y
                : Typography.MeasureWrapped(text, wrapWidth, ComposeInputStyle.Scale);
            var fieldHeight = MathF.Max(ThreadBoxMinHeight * scale, measured + ThreadBoxTextPad * scale);
            var fieldId = ChirperThreadDraft.FieldId(index);
            var focusNow = threadDraft.FocusRequest == index + 1;
            ImGui.SetCursorScreenPos(new Vector2(inputX, boxTop));
            ImGui.SetNextItemWidth(fieldWidth);
            if (focusNow)
            {
                threadDraft.FocusRequest = -1;
                ImGui.SetKeyboardFocusHere();
                SoftWrapField.CaretToEnd(fieldId);
            }

            using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (UiScope.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
            using (Plugin.Fonts.Push(ComposeInputStyle.Scale))
            {
                SoftWrapField.Paragraphs(fieldId, ref text, MaxPostLength, new Vector2(fieldWidth, fieldHeight),
                    wrapWidth, ThreadMentions(index));
                if (ImGui.IsItemActive())
                {
                    threadDraft.FocusedBox = index + 1;
                }
            }

            if (focusNow)
            {
                ImGui.SetScrollHereY(1f);
            }

            if (text.Length == 0)
            {
                Typography.Draw(drawList, new Vector2(inputX + lineInset, boxTop + 2f * scale),
                    Typography.FitText(Loc.T(L.Chirper.AddAnotherChirp), fieldWidth, ComposeInputStyle),
                    ChirperInk.MutedInk, ComposeInputStyle);
            }

            var removeCenter = new Vector2(inputX + inputWidth - removeRadius, boxTop + removeRadius);
            using (UiScope.PushId(index))
            {
                if (RoundButton.Icon(drawList, removeCenter, removeRadius, PhoneIcons.X, ui.Ink, ButtonStyle.Gray,
                        Loc.T(L.Chirper.RemoveThreadChirp)))
                {
                    removeIndex = index;
                }
            }

            var boxBottom = boxTop + fieldHeight;
            var attachments = threadDraft.Attachments(index);
            if (attachments.Count > 0)
            {
                var tile = ComposeTileSize(fieldWidth, attachments.Count, stripGap);
                DrawComposeAttachmentStrip(drawList, attachments, inputX, boxBottom + stripGap, tile, stripGap, scale);

                boxBottom += tile + stripGap * 2f;
            }

            previousAvatarBottom = avatarCenter.Y + radius;
            cursorY = MathF.Max(boxBottom, previousAvatarBottom) + gap;
        }

        DrawThreadMentionPopup(area);
        if (removeIndex >= 0)
        {
            threadDraft.RemoveAt(removeIndex);
        }

        if (ThreadCanGrow)
        {
            cursorY = DrawThreadAddRow(drawList, openingAvatar.X, inputX, inputWidth, previousAvatarBottom, cursorY);
        }

        ImGui.SetCursorScreenPos(new Vector2(inputX, cursorY));
        ImGui.Dummy(new Vector2(1f, 1f));
    }

    private void DrawThreadMentionPopup(Rect area)
    {
        if (!threadDraft.FocusesFollowUp)
        {
            return;
        }

        var mentions = ThreadMentions(threadDraft.FocusedBox - 1);
        var picked = threadMentionPopup.Draw(mentions, area, theme, images, lodestone);
        if (picked >= 0)
        {
            mentions.Pick(picked);
        }

        threadMentionPopup.Gate(mentions);
    }

    private static float ComposeTileSize(float width, int count, float gap)
    {
        return MathF.Min(ComposeTileMax * UiScale.Current, (width - gap * (count - 1)) / MathF.Max(2f, count));
    }

    private float DrawThreadAddRow(ImDrawListPtr drawList, float lineX, float inputX, float inputWidth,
        float previousAvatarBottom, float top)
    {
        var scale = UiScale.Current;
        var plusRadius = RoundButton.SmallRadius * scale;
        var plusCenter = new Vector2(lineX, top + plusRadius);
        var lastWritten = ComposingThread
            ? !threadDraft.IsBlank(threadDraft.Count - 1)
            : !string.IsNullOrWhiteSpace(draft) || composeAttachments.Count > 0;
        var enabled = composeScheduleAtUnix == 0 && composeScheduledId is null && lastWritten;
        var label = Loc.T(L.Chirper.AddAnotherChirp);
        if (enabled)
        {
            var lineInset = Metrics.Space.Xxs * scale;
            drawList.AddLine(new Vector2(lineX, previousAvatarBottom + lineInset),
                new Vector2(lineX, plusCenter.Y - plusRadius - lineInset), ImGui.GetColorU32(ThreadLine),
                ThreadLineWidth * scale);
        }

        var clicked = RoundButton.Icon(drawList, plusCenter, plusRadius, PhoneIcons.Plus, ui.Ink,
            ButtonStyle.Tinted, label, enabled: enabled);
        var labelText = Typography.FitText(label, inputWidth, MetaStyle);
        var labelSize = Typography.Measure(labelText, MetaStyle);
        var labelMin = new Vector2(inputX + Metrics.Space.Xxs * scale, plusCenter.Y - labelSize.Y * 0.5f);
        var labelMax = labelMin + labelSize;
        var labelHovered = enabled && UiInteract.Hover(labelMin, labelMax);
        Typography.Draw(drawList, labelMin, labelText,
            labelHovered ? ChirperInk.AccentLink : Palette.WithAlpha(ChirperInk.MutedInk, enabled ? 1f : 0.5f),
            MetaStyle);
        if (labelHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if ((clicked || UiInteract.Click(labelMin, labelMax, labelHovered)) && enabled)
        {
            threadDraft.Add();
        }

        return top + ThreadAddRowHeight();
    }

    private void SubmitThread()
    {
        if (store.Posting || !ThreadDraftReady())
        {
            return;
        }

        composeStatus = string.Empty;
        composeFailure.Clear();
        if (ReportComposePollIssue())
        {
            return;
        }

        var gif = AttachedComposeGif();
        var imagePaths = gif is null ? composeAttachments.ToArray() : Array.Empty<string>();
        var opening = new ThreadOpening(draft, quoteTargetId, gif?.Item, composePoll.Active ? composePoll.ToInput() : null,
            composeSensitive, composeReplyPolicy, ComposeAltTextInputs(imagePaths));
        var followUps = new ThreadFollowUp[threadDraft.Count];
        var gifs = new List<GifPick>(threadDraft.Count + 1);
        if (gif is not null)
        {
            gifs.Add(gif);
        }

        for (var index = 0; index < followUps.Length; index++)
        {
            var boxGif = threadDraft.AttachedGif(index);
            var paths = boxGif is null ? threadDraft.Attachments(index).ToArray() : Array.Empty<string>();
            followUps[index] = new ThreadFollowUp(threadDraft.Text(index), paths, boxGif?.Item,
                ComposeAltTextInputs(paths));
            if (boxGif is not null)
            {
                gifs.Add(boxGif);
            }
        }

        store.ComposeThread(opening, imagePaths, followUps, ok =>
        {
            if (ok)
            {
                for (var index = 0; index < gifs.Count; index++)
                {
                    gifSearch.RegisterShare(gifs[index]);
                }
            }

            composeOutcome = ok ? 1 : 2;
        }, composeFailure.Set);
    }
}
