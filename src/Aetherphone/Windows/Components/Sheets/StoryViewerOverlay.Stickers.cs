using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct StoryResponses(StoryResponseDto[] Items, int VoteCount, int AnswerCount, bool Loading,
    bool HasMore, bool LoadingMore);

internal readonly record struct StoryStickerHooks(
    Action<StoryDto, StoryStickerDto, int> Vote,
    Action<StoryDto, StoryStickerDto, string, Action<bool>> Answer,
    Func<StoryDto, StoryResponses> Responses,
    Action LoadMoreResponses,
    Action<StoryDto, StoryResponseDto> DeleteResponse,
    Action<StoryResponseDto> BlockResponder);

internal sealed partial class StoryViewerOverlay
{
    private const int MaxStickerBounds = 16;
    private const int AnswerIdle = 0;
    private const int AnswerSent = 1;
    private const int AnswerFailed = 2;
    private const float StoryPhotoAspect = 1080f / 1920f;

    private readonly Rect[] stickerBounds = new Rect[MaxStickerBounds];
    private readonly StoryStickerLabels stickerLabels = new();
    private readonly StickerTextCard answerCard = new("##storyAnswerCard");
    private readonly Action<bool> answerDone;
    private readonly Vector4 stickerAccent;
    private int stickerBoundCount;
    private StoryStickerHooks? stickerHooks;
    private StoryDto? answerStory;
    private StoryStickerDto? answerSticker;
    private volatile int answerOutcome;
    private int hoveredSticker = -1;
    private int hoveredOption = -1;

    private bool AnswerCaptures => answerCard.CapturesPointer;

    private bool PressOnSticker()
    {
        for (var index = 0; index < stickerBoundCount; index++)
        {
            if (UiInteract.Hover(stickerBounds[index].Min, stickerBounds[index].Max))
            {
                return true;
            }
        }

        return false;
    }

    private void OnAnswerDone(bool sent)
    {
        answerOutcome = sent ? AnswerSent : AnswerFailed;
    }

    private void DrawStickers(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale, bool interactive)
    {
        stickerBoundCount = 0;
        if (story.Stickers is not { Length: > 0 } stickers || story.SharedPost is not null
            || images.Get(story.MediaUrl) is null)
        {
            return;
        }

        var aspect = story.MediaWidth > 0 && story.MediaHeight > 0
            ? (float)story.MediaWidth / story.MediaHeight
            : StoryPhotoAspect;
        var photo = StoryStickerLayout.CoverRect(stage, aspect);
        stickerLabels.Sync(story, ownsStory);
        var nextHoveredSticker = -1;
        var nextHoveredOption = -1;
        drawList.PushClipRect(stage.Min, stage.Max, true);
        for (var index = 0; index < stickers.Length && index < MaxStickerBounds; index++)
        {
            var sticker = stickers[index];
            var center = PhotoTagGeometry.ToScreen(photo, sticker.X, sticker.Y);
            var zoom = StoryStickerLayout.Zoom(photo, scale, sticker.Scale);
            var hovered = hoveredSticker == index;
            Rect bounds;
            switch (sticker.Kind)
            {
                case StoryStickerKinds.Poll:
                {
                    var face = PollFace(sticker, index, hovered ? hoveredOption : -1);
                    bounds = StoryStickerArt.DrawPoll(drawList, center, zoom, scale, face, out var first, out var second);
                    if (interactive && UiInteract.Hover(first.Min, first.Max))
                    {
                        nextHoveredSticker = index;
                        nextHoveredOption = 0;
                    }
                    else if (interactive && UiInteract.Hover(second.Min, second.Max))
                    {
                        nextHoveredSticker = index;
                        nextHoveredOption = 1;
                    }

                    if (interactive)
                    {
                        HandlePollInput(story, sticker, bounds, first, second);
                    }

                    break;
                }

                case StoryStickerKinds.Question:
                {
                    var face = new StickerQuestionFace(sticker.Text, stickerLabels.Label(index),
                        ownsStory || sticker.Answered, stickerAccent);
                    bounds = StoryStickerArt.DrawQuestion(drawList, center, zoom, scale, face, hovered);
                    if (interactive && UiInteract.Hover(bounds.Min, bounds.Max))
                    {
                        nextHoveredSticker = index;
                    }

                    if (interactive && UiInteract.Click(bounds.Min, bounds.Max, nextHoveredSticker == index))
                    {
                        TapQuestion(story, sticker);
                    }

                    break;
                }

                default:
                {
                    bounds = StoryStickerArt.DrawMention(drawList, center, zoom, scale, stickerLabels.Label(index),
                        stickerAccent, hovered);
                    if (interactive && UiInteract.Hover(bounds.Min, bounds.Max))
                    {
                        nextHoveredSticker = index;
                    }

                    if (interactive && openProfile is not null && sticker.UserId is { Length: > 0 } userId
                        && UiInteract.Click(bounds.Min, bounds.Max, nextHoveredSticker == index))
                    {
                        openProfile(userId);
                    }

                    break;
                }
            }

            stickerBounds[stickerBoundCount++] = bounds;
        }

        drawList.PopClipRect();
        hoveredSticker = nextHoveredSticker;
        hoveredOption = nextHoveredOption;
        if (nextHoveredSticker >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private StickerPollFace PollFace(StoryStickerDto sticker, int index, int hovered) =>
        new(sticker.Text,
            sticker.Options is { Length: > 0 } options ? options[0] : string.Empty,
            sticker.Options is { Length: > 1 } more ? more[1] : string.Empty,
            stickerLabels.FirstPercent(index),
            stickerLabels.SecondPercent(index),
            stickerLabels.FirstFraction(index),
            stickerLabels.SecondFraction(index),
            StoryPollTally.ShowsResults(sticker),
            sticker.MyVote,
            StoryPollTally.CanVote(sticker, ownsStory) ? hovered : -1,
            stickerAccent);

    private void HandlePollInput(StoryDto story, StoryStickerDto sticker, Rect card, Rect first, Rect second)
    {
        if (stickerHooks is not { } hooks)
        {
            return;
        }

        if (ownsStory)
        {
            if (UiInteract.Click(card.Min, card.Max, UiInteract.Hover(card.Min, card.Max)))
            {
                OpenResponses();
            }

            return;
        }

        if (!StoryPollTally.CanVote(sticker, ownsStory))
        {
            return;
        }

        if (UiInteract.Click(first.Min, first.Max, UiInteract.Hover(first.Min, first.Max)))
        {
            hooks.Vote(story, sticker, 0);
        }
        else if (UiInteract.Click(second.Min, second.Max, UiInteract.Hover(second.Min, second.Max)))
        {
            hooks.Vote(story, sticker, 1);
        }
    }

    private void TapQuestion(StoryDto story, StoryStickerDto sticker)
    {
        if (stickerHooks is null)
        {
            return;
        }

        if (ownsStory)
        {
            OpenResponses();
            return;
        }

        answerStory = story;
        answerSticker = sticker;
        answerOutcome = AnswerIdle;
        answerCard.SetField(0, string.Empty, L.Story.AnswerHint, StoryStickerDrafts.AnswerMaxLength);
        answerCard.Open(default, sticker.Text, L.Aethergram.Send,
            Loc.T(L.Story.AnswerPrivacy, replyName ?? authorLabel), 1);
    }

    private void OpenResponses()
    {
        sheetTab = SheetTabResponses;
        sheetOpen = true;
    }

    private void DrawAnswerCard(Rect area, PhoneTheme theme)
    {
        var outcome = answerOutcome;
        if (outcome == AnswerSent)
        {
            answerOutcome = AnswerIdle;
            answerCard.Close();
        }
        else if (outcome == AnswerFailed)
        {
            answerOutcome = AnswerIdle;
            answerCard.Busy = false;
            answerCard.ShowError(Loc.T(L.Story.AnswerFailed));
        }

        if (answerCard.Draw(area, theme) != StickerCardOutcome.Confirmed)
        {
            return;
        }

        if (stickerHooks is not { } hooks || answerStory is not { } story || answerSticker is not { } sticker)
        {
            answerCard.Close();
            return;
        }

        var text = answerCard.Field(0).Trim();
        if (text.Length == 0)
        {
            return;
        }

        answerCard.Busy = true;
        answerCard.ShowError(string.Empty);
        hooks.Answer(story, sticker, text, answerDone);
    }
}
