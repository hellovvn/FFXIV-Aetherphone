using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float PollTopGap = 9f;
    private const float PollRowHeight = 34f;
    private const float PollHeadRowHeight = 40f;
    private const float PollRowGap = 6f;
    private const float PollRowInsetX = 10f;
    private const float PollGlyphGap = 8f;
    private const float PollPercentGap = 8f;
    private const float PollFooterGap = 7f;
    private const float PollOutlineStroke = 1.2f;
    private const float PollIdleWashAlpha = 0.08f;
    private const float PollHoverWashAlpha = 0.18f;
    private const float PollOutlineAlpha = 0.55f;
    private const float PollFillAlphaMine = 0.85f;
    private const float PollFillAlphaWinner = 0.6f;
    private const float PollRevealEpsilon = 0.01f;
    private const int PollViewCap = 256;
    private const float PollEditorTopGap = 10f;
    private const float PollEditorPad = 10f;
    private const float PollEditorRounding = 14f;
    private const float PollFieldHeight = 36f;
    private const float PollFieldGap = 6f;
    private const float PollFieldInset = 12f;
    private const float PollRemoveRadius = 11f;
    private const float PollRemoveGap = 6f;
    private const float PollAddRowHeight = 32f;
    private const float PollSectionGap = 10f;
    private const float PollLabelGap = 6f;
    private const float PollGlyphSize = 19f;
    private const float PollToolbarGap = 8f;

    private static readonly TextStyle PollLabelStyle = TextStyles.Body;
    private static readonly TextStyle PollLabelEmphasisStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle PollPercentStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle PollFooterStyle = TextStyles.Footnote;
    private static readonly TextStyle PollEditorLabelStyle = TextStyles.Footnote;
    private static readonly TextStyle PollEditorActionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle PollAddStyle = TextStyles.SubheadlineEmphasized;

    private static readonly float[] PollGlyphBars = { 0.84f, 0.5f, 0.68f };

    private static readonly string[] PollOptionIds =
    {
        "##chirpPollOption0",
        "##chirpPollOption1",
        "##chirpPollOption2",
        "##chirpPollOption3",
    };

    private readonly ChirperPollDraft composePoll = new();
    private readonly Dictionary<string, ChirperPollView> pollViews = new(StringComparer.Ordinal);
    private readonly ChipRail pollDurationRail = new();
    private readonly string[] pollDurationLabels = new string[ChirperPolls.DurationHours.Length];
    private readonly bool[] pollDurationActive = new bool[ChirperPolls.DurationHours.Length];
    private readonly string[] pollOptionHints = new string[ChirperPolls.MaxOptions];
    private string pollTextsLanguage = string.Empty;
    private int pollFocusIndex = -1;

    private static float PollRowHeightFor(bool isThreadHead) => isThreadHead ? PollHeadRowHeight : PollRowHeight;

    private float PollBlockHeight(PostDto post, bool isThreadHead)
    {
        if (post.Poll is not { } poll || poll.Options.Length == 0)
        {
            return 0f;
        }

        var scale = UiScale.Current;
        var rows = poll.Options.Length;
        return (PollTopGap + PollFooterGap) * scale + rows * PollRowHeightFor(isThreadHead) * scale
            + (rows - 1) * PollRowGap * scale + Typography.LineHeight(PollFooterStyle);
    }

    private float DrawThreadPoll(ImDrawListPtr drawList, PostDto post, float left, float top, float width)
    {
        var height = PollBlockHeight(post, true);
        if (height <= 0f)
        {
            return top;
        }

        DrawPollBlock(drawList, post, left, top, width, true);
        return top + height;
    }

    private void DrawPollBlock(ImDrawListPtr drawList, PostDto post, float left, float top, float width,
        bool isThreadHead)
    {
        if (post.Poll is not { } poll || poll.Options.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var view = PollViewFor(post.Id);
        view.Sync(poll, nowUnix);
        ShowPollVoteFailure();
        var resultsVisible = ChirperPolls.ResultsVisible(poll);
        if (view.Ended && !resultsVisible)
        {
            store.RequestPollRefresh(post.Id);
        }

        var isAuthor = store.Me is { } me && me.Id == post.AuthorId;
        var pending = store.TryGetPendingPollVote(post.Id, out var pendingOption);
        var shownVote = ChirperPolls.ShownVote(poll, pending, pendingOption);
        var canVote = store.IsSignedIn && !pending && ChirperPolls.CanVote(poll, isAuthor, nowUnix);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        PrimePollView(view, resultsVisible);
        var reveal = view.Reveal.Step(resultsVisible ? 1f : 0f, Motion.Appear, deltaSeconds);
        var rowHeight = PollRowHeightFor(isThreadHead) * scale;
        var gap = PollRowGap * scale;
        var rowTop = top + PollTopGap * scale;
        for (var index = 0; index < poll.Options.Length; index++)
        {
            var row = new Rect(new Vector2(left, rowTop), new Vector2(left + width, rowTop + rowHeight));
            if (DrawPollRow(drawList, poll, view, index, shownVote, row, canVote, reveal, deltaSeconds, scale))
            {
                store.VotePoll(post, index);
            }

            rowTop += rowHeight + gap;
        }

        var footer = Typography.FitText(view.Footer, MathF.Max(1f, width), PollFooterStyle);
        Typography.Draw(drawList, new Vector2(left, rowTop - gap + PollFooterGap * scale), footer, ChirperInk.MutedInk,
            PollFooterStyle);
    }

    private static void PrimePollView(ChirperPollView view, bool resultsVisible)
    {
        if (view.Primed)
        {
            return;
        }

        view.Primed = true;
        view.Reveal.SnapTo(resultsVisible ? 1f : 0f);
        for (var index = 0; index < view.Fills.Length; index++)
        {
            view.Fills[index].SnapTo(resultsVisible ? view.Fractions[index] : 0f);
        }
    }

    private static bool DrawPollRow(ImDrawListPtr drawList, PostPollDto poll, ChirperPollView view, int optionIndex,
        int shownVote, Rect row, bool canVote, float reveal, float deltaSeconds, float scale)
    {
        var radius = row.Height * 0.5f;
        var mine = shownVote == optionIndex;
        var showResults = reveal > PollRevealEpsilon;
        var winner = view.Ended && showResults && view.Leaders[optionIndex];
        var hovered = canVote && UiInteract.Hover(row.Min, row.Max);
        var track = canVote
            ? Palette.WithAlpha(ChirperInk.Accent, hovered ? PollHoverWashAlpha : PollIdleWashAlpha)
            : Surfaces.Fill(ChirperInk.TitleInk, FillLevel.Quaternary);
        Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(track));
        if (canVote || (mine && !showResults))
        {
            Squircle.Stroke(drawList, row.Min, row.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ChirperInk.Accent, PollOutlineAlpha)), PollOutlineStroke * scale);
        }

        var fill = view.Fills[optionIndex].Step(showResults ? view.Fractions[optionIndex] : 0f, Motion.Sheet,
            deltaSeconds);
        if (fill > 0.001f)
        {
            var color = mine ? Palette.WithAlpha(ChirperInk.Accent, PollFillAlphaMine)
                : winner ? Palette.WithAlpha(ChirperInk.Accent, PollFillAlphaWinner)
                : Surfaces.Fill(ChirperInk.TitleInk, view.Leaders[optionIndex] ? FillLevel.Secondary : FillLevel.Tertiary);
            var fillRight = row.Min.X + row.Width * MathF.Min(1f, fill);
            drawList.PushClipRect(row.Min, new Vector2(fillRight, row.Max.Y), true);
            Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(color with { W = color.W * reveal }));
            drawList.PopClipRect();
        }

        var glyphCenter = new Vector2(row.Min.X + (PollRowInsetX + PollGlyphs.RadioRadius) * scale, row.Center.Y);
        if (canVote)
        {
            PollGlyphs.RadioRing(drawList, glyphCenter, Palette.WithAlpha(ChirperInk.Accent, PollOutlineAlpha + 0.25f),
                scale);
        }

        if (mine)
        {
            PollGlyphs.CheckBadge(drawList, glyphCenter, 1f, ChirperInk.White, ChirperInk.Accent, scale);
        }

        var labelLeft = glyphCenter.X + (PollGlyphs.RadioRadius + PollGlyphGap) * scale;
        var labelRight = row.Max.X - PollRowInsetX * scale;
        if (showResults)
        {
            var percent = view.Percents[optionIndex];
            var percentSize = Typography.Measure(percent, PollPercentStyle);
            labelRight -= percentSize.X + PollPercentGap * scale;
            var percentInk = mine || winner ? ChirperInk.TitleInk : ChirperInk.BodyInk;
            Typography.Draw(drawList,
                new Vector2(row.Max.X - PollRowInsetX * scale - percentSize.X, row.Center.Y - percentSize.Y * 0.5f),
                percent, percentInk with { W = percentInk.W * reveal }, PollPercentStyle);
        }

        var style = mine || winner ? PollLabelEmphasisStyle : PollLabelStyle;
        var label = Typography.FitText(poll.Options[optionIndex], MathF.Max(1f, labelRight - labelLeft), style);
        var labelSize = Typography.Measure(label, style);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - labelSize.Y * 0.5f), label, ChirperInk.TitleInk,
            style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return canVote && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private ChirperPollView PollViewFor(string postId)
    {
        if (pollViews.TryGetValue(postId, out var view))
        {
            return view;
        }

        if (pollViews.Count >= PollViewCap)
        {
            pollViews.Clear();
        }

        view = new ChirperPollView();
        pollViews[postId] = view;
        return view;
    }

    private void ShowPollVoteFailure()
    {
        if (!store.TakePollVoteFailure(out var failure))
        {
            return;
        }

        toast.Show(failure.Code is FailureCodes.PollAlreadyVoted or FailureCodes.PollClosed
            ? FailureText.Resolve(failure)
            : Loc.T(L.Polls.VoteFailed));
    }

    private float ComposePollHeight()
    {
        if (!composePoll.Active)
        {
            return 0f;
        }

        var scale = UiScale.Current;
        var rows = composePoll.OptionCount;
        var inner = rows * PollFieldHeight + (rows - 1) * PollFieldGap;
        if (composePoll.CanAddOption)
        {
            inner += PollFieldGap + PollAddRowHeight;
        }

        inner += PollSectionGap * 2f + PollLabelGap + ChipRail.RowHeight;
        return (PollEditorTopGap + PollEditorPad * 2f + inner) * scale + Typography.LineHeight(PollEditorLabelStyle);
    }

    private void DrawComposePoll(ImDrawListPtr drawList, Vector2 origin, float width)
    {
        var scale = UiScale.Current;
        RefreshPollEditorTexts();
        var top = origin.Y + PollEditorTopGap * scale;
        var max = new Vector2(origin.X + width, origin.Y + ComposePollHeight());
        Squircle.Stroke(drawList, new Vector2(origin.X, top), max, PollEditorRounding * scale,
            ImGui.GetColorU32(ChirperInk.ChipStroke), 1f);
        var left = origin.X + PollEditorPad * scale;
        var right = max.X - PollEditorPad * scale;
        var cursorY = top + PollEditorPad * scale;
        var removable = composePoll.CanRemoveOption;
        var removeReserve = removable ? (PollRemoveRadius * 2f + PollRemoveGap) * scale : 0f;
        var removeIndex = -1;
        for (var index = 0; index < composePoll.OptionCount; index++)
        {
            var field = new Rect(new Vector2(left, cursorY),
                new Vector2(right - removeReserve, cursorY + PollFieldHeight * scale));
            Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f, ImGui.GetColorU32(ChirperInk.FieldFill));
            if (pollFocusIndex == index)
            {
                ImGui.SetKeyboardFocusHere();
                pollFocusIndex = -1;
            }

            GlassField.Text(field, PollOptionIds[index], pollOptionHints[index], ref composePoll.Option(index),
                ChirperInk.TitleInk, ChirperInk.MutedInk, PollFieldInset * scale, ChirperPolls.MaxOptionLength);
            if (removable && DrawPollRemoveButton(drawList,
                    new Vector2(right - PollRemoveRadius * scale, field.Center.Y), scale))
            {
                removeIndex = index;
            }

            cursorY = field.Max.Y + PollFieldGap * scale;
        }

        if (removeIndex >= 0)
        {
            composePoll.RemoveOption(removeIndex);
            composeStatus = string.Empty;
        }

        if (composePoll.CanAddOption)
        {
            var addRow = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + PollAddRowHeight * scale));
            if (DrawPollAddRow(drawList, addRow, scale))
            {
                composePoll.AddOption();
                pollFocusIndex = composePoll.OptionCount - 1;
                composeStatus = string.Empty;
            }

            cursorY = addRow.Max.Y + PollFieldGap * scale;
        }

        cursorY += PollSectionGap * scale - PollFieldGap * scale;
        drawList.AddLine(new Vector2(left, cursorY), new Vector2(right, cursorY), ImGui.GetColorU32(ChirperInk.Hairline),
            1f);
        cursorY += PollSectionGap * scale;
        if (DrawPollLengthHeader(drawList, left, right, cursorY))
        {
            composePoll.Reset();
            composeStatus = string.Empty;
            return;
        }

        cursorY += Typography.LineHeight(PollEditorLabelStyle) + PollLabelGap * scale;
        for (var index = 0; index < pollDurationActive.Length; index++)
        {
            pollDurationActive[index] = index == composePoll.DurationIndex;
        }

        var rail = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + ChipRail.RowHeight * scale));
        var picked = pollDurationRail.Draw(rail, ui, pollDurationLabels, pollDurationActive);
        if (picked >= 0)
        {
            composePoll.SelectDuration(picked);
        }
    }

    private bool DrawPollLengthHeader(ImDrawListPtr drawList, float left, float right, float top)
    {
        var removeLabel = Loc.T(L.Chirper.RemovePoll);
        var removeSize = Typography.Measure(removeLabel, PollEditorActionStyle);
        var removeMin = new Vector2(right - removeSize.X, top);
        var removeMax = new Vector2(right, top + removeSize.Y);
        var lengthLabel = Typography.FitText(Loc.T(L.Chirper.PollLength),
            MathF.Max(1f, removeMin.X - left - PollLabelGap * UiScale.Current), PollEditorLabelStyle);
        Typography.Draw(drawList, new Vector2(left, top), lengthLabel, ChirperInk.MutedInk, PollEditorLabelStyle);
        var hovered = UiInteract.Hover(removeMin, removeMax);
        var ink = hovered ? Palette.Lighten(ChirperInk.Danger, 0.12f) : ChirperInk.Danger;
        Typography.Draw(drawList, removeMin, removeLabel, ink, PollEditorActionStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(removeMin, removeMax, hovered);
    }

    private static bool DrawPollRemoveButton(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = PollRemoveRadius * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? ChirperInk.AccentWash : ChirperInk.ChipFill), 24);
        PhoneIcon.Draw(drawList, center, PhoneIcons.X, hovered ? ChirperInk.TitleInk : ChirperInk.MutedInk,
            radius * 1.1f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Chirper.PollRemoveOption), HoverLabelSide.Above);
        return UiInteract.Click(min, max, hovered);
    }

    private static bool DrawPollAddRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, row.Height * 0.5f, ImGui.GetColorU32(ChirperInk.AccentWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + PollFieldInset * scale + 7f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Plus, ChirperInk.AccentLink, 15f * scale);
        var label = Loc.T(L.Chirper.PollAddOption);
        var labelLeft = iconCenter.X + 15f * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, row.Max.X - labelLeft), PollAddStyle);
        var size = Typography.Measure(fitted, PollAddStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - size.Y * 0.5f), fitted, ChirperInk.AccentLink,
            PollAddStyle);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void RefreshPollEditorTexts()
    {
        var code = Loc.Current.Code;
        if (string.Equals(pollTextsLanguage, code, StringComparison.Ordinal))
        {
            return;
        }

        pollTextsLanguage = code;
        for (var index = 0; index < pollOptionHints.Length; index++)
        {
            pollOptionHints[index] = Loc.T(L.Chirper.PollOptionHint, index + 1);
        }

        for (var index = 0; index < pollDurationLabels.Length; index++)
        {
            pollDurationLabels[index] = Loc.T(ChirperPolls.DurationLabels[index]);
        }
    }

    private float DrawComposePollButton(ImDrawListPtr drawList, float left, float centerY, float iconRadius)
    {
        var scale = UiScale.Current;
        var center = new Vector2(left + PollToolbarGap * scale + iconRadius, centerY);
        var extent = new Vector2(iconRadius, iconRadius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        if (hovered)
        {
            drawList.AddCircleFilled(center, iconRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var blocked = !composePoll.Active && (composeAttachments.Count > 0 || quoteTarget is not null);
        var ink = composePoll.Active ? ChirperInk.Accent
            : blocked ? Palette.WithAlpha(ChirperInk.MutedInk, 0.4f)
            : ChirperInk.AccentLink;
        PaintPollGlyph(drawList, center, PollGlyphSize * scale, ink);
        HoverTooltip.Show(new Rect(center - extent, center + extent),
            Loc.T(composePoll.Active ? L.Chirper.RemovePoll : L.Chirper.AddPoll), HoverLabelSide.Above);
        if (UiInteract.Click(center - extent, center + extent, hovered))
        {
            ToggleComposePoll();
        }

        return center.X + iconRadius;
    }

    private static void PaintPollGlyph(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = size * 0.17f;
        var left = center.X - size * 0.42f;
        var spacing = size * 0.3f;
        for (var index = 0; index < PollGlyphBars.Length; index++)
        {
            var y = center.Y + (index - 1) * spacing;
            drawList.AddRectFilled(new Vector2(left, y - thickness * 0.5f),
                new Vector2(left + size * PollGlyphBars[index], y + thickness * 0.5f), color, thickness * 0.5f);
        }
    }

    private void ToggleComposePoll()
    {
        if (composePoll.Active)
        {
            composePoll.Reset();
            composeStatus = string.Empty;
            return;
        }

        if (quoteTarget is not null)
        {
            toast.Show(Loc.T(L.Chirper.PollNotOnQuote));
            return;
        }

        if (composeAttachments.Count > 0)
        {
            toast.Show(Loc.T(ComposeHasGif() ? L.Gifs.PollBlockedByGif : L.Chirper.PollRidesAlone));
            return;
        }

        composePoll.Open();
        pollDurationRail.Reset();
        composeStatus = string.Empty;
    }

    private string ComposeMediaRefusal()
    {
        if (composePoll.Active)
        {
            return Loc.T(L.Chirper.PollBlocksMedia);
        }

        return ComposeHasGif() ? Loc.T(L.Common.GifRidesAlone) : Loc.T(L.Chirper.MaxPhotos, ChirperStore.MaxImages);
    }

    private string ComposeGifRefusal() =>
        composePoll.Active ? Loc.T(L.Chirper.PollBlocksMedia) : Loc.T(L.Common.GifRidesAlone);

    private bool SubmitComposePoll()
    {
        if (!composePoll.Active)
        {
            return false;
        }

        var issue = composePoll.Validate();
        if (issue != PollDraftIssue.None)
        {
            composeStatus = issue switch
            {
                PollDraftIssue.OptionTooLong => Loc.T(L.Chirper.PollOptionTooLong, ChirperPolls.MaxOptionLength),
                PollDraftIssue.DuplicateOption => Loc.T(L.Chirper.PollOptionDuplicate),
                _ => Loc.T(L.Chirper.PollOptionEmpty),
            };
            return true;
        }

        store.ComposePoll(draft, composePoll.ToInput(), ok => composeOutcome = ok ? 1 : 2, composeFailure.Set);
        return true;
    }
}
