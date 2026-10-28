using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float StickerTrayHeight = 36f;
    private const float StickerTrayGap = 8f;
    private const float StickerChipGlyph = 16f;
    private const float StickerCloseRadius = 10f;
    private const float StickerDragThreshold = 4f;
    private const int StickerSlots = StoryStickerDrafts.MaxPolls + StoryStickerDrafts.MaxQuestions
                                     + StoryStickerDrafts.MaxMentions;

    private static readonly int[] StickerTrayKinds =
    {
        StoryStickerKinds.Poll, StoryStickerKinds.Question, StoryStickerKinds.Mention,
    };

    private readonly StoryStickerDrafts storyStickers = new();
    private readonly StickerTextCard stickerEditor = new("##storyStickerEditor");
    private readonly Rect[] stickerDraftBounds = new Rect[StickerSlots];
    private readonly Rect[] stickerCloseBounds = new Rect[StickerSlots];
    private readonly PersonPicker mentionPicker;
    private readonly Action<string> storyUnavailable;
    private int stickerEditIndex = -1;
    private int stickerEditKind;
    private int stickerDragIndex = -1;
    private Vector2 stickerDragOrigin;
    private Vector2 stickerDragStart;
    private bool stickerDragMoved;

    private bool StickerEditorCaptures => stickerEditor.CapturesPointer;

    private void OpenMentionedStory(string authorId, string storyId)
    {
        if (stories.TryRing(authorId, out var ring))
        {
            stories.OpenRingAt(ring, storyId);
            return;
        }

        stories.OpenStoryItem(authorId, storyId, storyUnavailable);
    }

    private void ShowStoryUnavailable(string authorId)
    {
        toast.Show(Loc.T(L.Story.Unavailable));
        OpenProfile(authorId);
    }

    private void BlockFromStory(string userId, Action<bool> done)
    {
        store.Block(userId, done, confirm.ReportFailure);
    }

    private void ResetStoryStickers()
    {
        storyStickers.Clear();
        stickerEditor.Close();
        mentionPicker.Close();
        stickerEditIndex = -1;
        stickerDragIndex = -1;
    }

    private float StickerTrayBlock(float scale) =>
        composeStoryMode ? (StickerTrayHeight + StickerTrayGap) * scale : 0f;

    private void DrawStoryStickerTray(Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var gap = StickerTrayGap * scale;
        var width = (row.Width - gap * (StickerTrayKinds.Length - 1)) / StickerTrayKinds.Length;
        for (var index = 0; index < StickerTrayKinds.Length; index++)
        {
            var left = row.Min.X + (width + gap) * index;
            var chip = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
            var kind = StickerTrayKinds[index];
            UiAnchors.Report(StickerAnchor(kind), chip);
            if (DrawStickerChip(drawList, chip, kind, scale))
            {
                TapStickerTray(kind);
            }
        }
    }

    private static string StickerAnchor(int kind) => kind switch
    {
        StoryStickerKinds.Poll => "aethergram.compose.sticker.poll",
        StoryStickerKinds.Question => "aethergram.compose.sticker.question",
        _ => "aethergram.compose.sticker.mention",
    };

    private bool DrawStickerChip(ImDrawListPtr drawList, Rect chip, int kind, float scale)
    {
        var hovered = UiInteract.Hover(chip.Min, chip.Max);
        var full = !storyStickers.CanAdd(kind) && kind == StoryStickerKinds.Mention;
        Squircle.Fill(drawList, chip.Min, chip.Max, chip.Height * 0.5f,
            ImGui.GetColorU32(hovered && !full ? Ink.HoverTint : theme.SurfaceMuted));
        var label = Loc.T(kind switch
        {
            StoryStickerKinds.Poll => L.Story.StickerPoll,
            StoryStickerKinds.Question => L.Story.StickerQuestion,
            _ => L.Story.StickerMention,
        });
        var style = TextStyles.SubheadlineEmphasized;
        var glyph = StickerChipGlyph * scale;
        var gap = 6f * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, chip.Width - glyph - gap - 16f * scale), style);
        var size = Typography.Measure(fitted, style);
        var left = chip.Center.X - (glyph + gap + size.X) * 0.5f;
        var glyphCenter = new Vector2(left + glyph * 0.5f, chip.Center.Y);
        var ink = full ? Ink.FaintInk : Ink.TitleInk;
        switch (kind)
        {
            case StoryStickerKinds.Poll:
                PollGlyphs.Bars(drawList, glyphCenter, glyph, Accent);
                break;
            case StoryStickerKinds.Question:
                PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.HelpCircle, Accent, glyph);
                break;
            default:
                AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.At), full ? Ink.FaintInk : Accent,
                    0.8f);
                break;
        }

        Typography.Draw(drawList, new Vector2(left + glyph + gap, chip.Center.Y - size.Y * 0.5f), fitted, ink, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(chip.Min, chip.Max, hovered);
    }

    private void TapStickerTray(int kind)
    {
        composeStatus = string.Empty;
        if (kind == StoryStickerKinds.Mention)
        {
            if (!storyStickers.CanAdd(kind))
            {
                composeStatus = Loc.T(L.Story.StickerLimit, StoryStickerDrafts.MaxMentions);
                return;
            }

            mentionPicker.Open();
            return;
        }

        OpenStickerEditor(kind, storyStickers.IndexOf(kind));
    }

    private void OpenStickerEditor(int kind, int index)
    {
        stickerEditKind = kind;
        stickerEditIndex = index;
        var existing = index >= 0 ? storyStickers[index] : default;
        if (kind == StoryStickerKinds.Poll)
        {
            stickerEditor.SetField(0, index >= 0 ? existing.Text : string.Empty, L.Chirper.PollQuestionHint,
                StoryStickerDrafts.PollQuestionMaxLength);
            stickerEditor.SetField(1, index >= 0 ? existing.FirstOption : Loc.T(L.Story.PollYes),
                L.Chirper.PollOptionHint, StoryStickerDrafts.PollOptionMaxLength, 1);
            stickerEditor.SetField(2, index >= 0 ? existing.SecondOption : Loc.T(L.Story.PollNo),
                L.Chirper.PollOptionHint, StoryStickerDrafts.PollOptionMaxLength, 2);
            stickerEditor.Open(L.Story.StickerPoll, string.Empty, L.Aethergram.Done, string.Empty, 3, 1);
            return;
        }

        stickerEditor.SetField(0, index >= 0 ? existing.Text : Loc.T(L.Story.QuestionDefault), L.Story.QuestionDefault,
            StoryStickerDrafts.QuestionPromptMaxLength);
        stickerEditor.Open(L.Story.StickerQuestion, string.Empty, L.Aethergram.Done, string.Empty, 1);
    }

    private void DrawStoryStickerEditor(Rect screen)
    {
        if (stickerEditor.Draw(screen, theme) != StickerCardOutcome.Confirmed)
        {
            return;
        }

        var position = StoryStickerLayout.Spawn(storyStickers.Count);
        if (stickerEditKind == StoryStickerKinds.Poll)
        {
            var index = stickerEditIndex >= 0
                ? stickerEditIndex
                : storyStickers.Add(StoryStickerDraft.Poll(position, string.Empty, string.Empty, string.Empty));
            storyStickers.SetPoll(index, stickerEditor.Field(0), stickerEditor.Field(1), stickerEditor.Field(2));
        }
        else
        {
            var index = stickerEditIndex >= 0
                ? stickerEditIndex
                : storyStickers.Add(StoryStickerDraft.Question(position, string.Empty));
            storyStickers.SetPrompt(index, stickerEditor.Field(0));
        }

        stickerEditIndex = -1;
        stickerEditor.Close();
    }

    private void DrawMentionPicker(Rect area)
    {
        var picked = mentionPicker.Draw(area, theme, images, lodestone);
        if (picked is null || storyStickers.HasMention(picked.Handle))
        {
            return;
        }

        storyStickers.Add(StoryStickerDraft.Mention(StoryStickerLayout.Spawn(storyStickers.Count), picked.Handle));
    }

    private bool DrawComposeStickers(ImDrawListPtr drawList, Rect preview, float scale)
    {
        var count = Math.Min(storyStickers.Count, StickerSlots);
        if (count == 0)
        {
            stickerDragIndex = -1;
            return false;
        }

        var answerHint = Loc.T(L.Story.AnswerHint);
        drawList.PushClipRect(preview.Min, preview.Max, true);
        for (var index = 0; index < count; index++)
        {
            var draft = storyStickers[index];
            var center = PhotoTagGeometry.ToScreen(preview, draft.Position.X, draft.Position.Y);
            var zoom = StoryStickerLayout.Zoom(preview, scale, draft.Scale);
            var bounds = draft.Kind switch
            {
                StoryStickerKinds.Poll => StoryStickerArt.DrawPoll(drawList, center, zoom, scale,
                    new StickerPollFace(draft.Text, draft.FirstOption, draft.SecondOption, string.Empty, string.Empty,
                        0f, 0f, false, -1, -1, Accent), out _, out _),
                StoryStickerKinds.Question => StoryStickerArt.DrawQuestion(drawList, center, zoom, scale,
                    new StickerQuestionFace(draft.Text, answerHint, false, Accent), false),
                _ => StoryStickerArt.DrawMention(drawList, center, zoom, scale, draft.Label, Accent, false),
            };
            stickerDraftBounds[index] = bounds;
            var closeCenter = new Vector2(bounds.Max.X - 2f * scale, bounds.Min.Y + 2f * scale);
            var closeRadius = StickerCloseRadius * scale;
            stickerCloseBounds[index] = new Rect(closeCenter - new Vector2(closeRadius, closeRadius),
                closeCenter + new Vector2(closeRadius, closeRadius));
            drawList.AddCircleFilled(closeCenter, closeRadius, ImGui.GetColorU32(Ink.Scrim), 20);
            PhoneIcon.Draw(drawList, closeCenter, PhoneIcons.X, Ink.White, closeRadius * 1.1f);
        }

        drawList.PopClipRect();
        return HandleComposeStickerInput(preview, count, scale);
    }

    private bool HandleComposeStickerInput(Rect preview, int count, float scale)
    {
        var hoveredClose = -1;
        var hoveredSticker = -1;
        for (var index = count - 1; index >= 0; index--)
        {
            if (UiInteract.Hover(stickerCloseBounds[index].Min, stickerCloseBounds[index].Max))
            {
                hoveredClose = index;
                break;
            }

            if (UiInteract.Hover(stickerDraftBounds[index].Min, stickerDraftBounds[index].Max))
            {
                hoveredSticker = index;
                break;
            }
        }

        if (hoveredClose >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(stickerCloseBounds[hoveredClose].Min, stickerCloseBounds[hoveredClose].Max, true))
            {
                storyStickers.RemoveAt(hoveredClose);
                stickerDragIndex = -1;
            }

            return true;
        }

        var mouse = ImGui.GetMousePos();
        if (hoveredSticker >= 0 && stickerDragIndex < 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            stickerDragIndex = hoveredSticker;
            stickerDragOrigin = mouse;
            stickerDragStart = storyStickers[hoveredSticker].Position;
            stickerDragMoved = false;
        }

        if (stickerDragIndex >= 0)
        {
            DragComposeSticker(preview, mouse, scale);
        }

        if (hoveredSticker < 0)
        {
            return stickerDragIndex >= 0;
        }

        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel != 0f)
        {
            storyStickers.ScaleTo(hoveredSticker,
                StoryStickerLayout.Wheel(storyStickers[hoveredSticker].Scale, wheel));
        }

        ImGui.SetMouseCursor(stickerDragMoved ? ImGuiMouseCursor.ResizeAll : ImGuiMouseCursor.Hand);
        HoverTooltip.Show(stickerDraftBounds[hoveredSticker], Loc.T(L.Story.StickerHint));
        return true;
    }

    private void DragComposeSticker(Rect preview, Vector2 mouse, float scale)
    {
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var delta = mouse - stickerDragOrigin;
            if (!stickerDragMoved && delta.Length() >= StickerDragThreshold * scale)
            {
                stickerDragMoved = true;
            }

            if (stickerDragMoved)
            {
                storyStickers.MoveTo(stickerDragIndex, StoryStickerLayout.Drag(preview, stickerDragStart, delta));
            }

            return;
        }

        var tapped = stickerDragIndex;
        var moved = stickerDragMoved;
        stickerDragIndex = -1;
        stickerDragMoved = false;
        if (moved || tapped < 0 || tapped >= storyStickers.Count)
        {
            return;
        }

        var kind = storyStickers[tapped].Kind;
        if (kind != StoryStickerKinds.Mention)
        {
            OpenStickerEditor(kind, tapped);
        }
    }
}
