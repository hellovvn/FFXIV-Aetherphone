using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class StoryViewerOverlay
{
    private const int SheetTabViewers = 0;
    private const int SheetTabResponses = 1;
    private const float ResponseActionSide = 28f;
    private const float ResponseAnswerMaxLines = 4f;
    private const long StampRefreshSeconds = 60L;

    private static readonly TextStyle ResponseBodyStyle = TextStyles.Subheadline;

    private readonly string[] sheetTabLabels = new string[2];
    private readonly StoryResponseRowLabels responseLabels = new();
    private int sheetTab;

    private bool ShowsResponses(StoryDto story) =>
        ownsStory && stickerHooks is not null && StoryStickerList.HasResults(story);

    private int SheetTabFor(StoryDto story)
    {
        if (viewersSource is null)
        {
            return SheetTabResponses;
        }

        return ShowsResponses(story) ? sheetTab : SheetTabViewers;
    }

    private float DrawSheetHeader(Rect panel, PhoneTheme theme, StoryDto story, float scale)
    {
        var headerY = panel.Min.Y + 26f * scale;
        if (viewersSource is not null && ShowsResponses(story))
        {
            sheetTabLabels[0] = Loc.T(L.Story.ViewersTab);
            sheetTabLabels[1] = Loc.T(L.Story.ResponsesTab);
            var row = new Rect(new Vector2(panel.Min.X + 24f * scale, headerY - 14f * scale),
                new Vector2(panel.Max.X - 24f * scale, headerY + 14f * scale));
            sheetTab = SegmentStrip.Draw("storyviewer.sheet.tabs", row, sheetTabLabels, sheetTab, theme);
            return headerY + 22f * scale;
        }

        var title = viewersSource is null ? Loc.T(L.Story.ResponsesTab) : SeenLabel(story);
        Typography.DrawCentered(new Vector2(panel.Center.X, headerY), title, theme.TextStrong, TextStyles.Headline);
        return headerY + 18f * scale;
    }

    private void DrawResponsesList(Rect listRect, PhoneTheme theme, StoryDto story, float scale)
    {
        if (stickerHooks is not { } hooks)
        {
            return;
        }

        var responses = hooks.Responses(story);
        if (responses.Items.Length == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 40f * scale),
                Loc.T(responses.Loading ? L.Common.Loading : L.Story.NoResponses), theme.TextMuted,
                TextStyles.Subheadline);
            return;
        }

        responseLabels.Sync(responses.Items);
        using (AppSurface.Begin(listRect))
        {
            for (var index = 0; index < responses.Items.Length; index++)
            {
                DrawResponseRow(story, responses.Items[index], index, theme, scale, hooks);
            }

            if (responses.LoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, theme.TextMuted);
            }
            else if (responses.HasMore && InfiniteScroll.ReachedBottom())
            {
                hooks.LoadMoreResponses();
            }
        }
    }

    private void DrawResponseRow(StoryDto story, StoryResponseDto response, int index, PhoneTheme theme, float scale,
        in StoryStickerHooks hooks)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var isAnswer = response.Kind == StoryStickerKinds.Question;
        var actionsWidth = isAnswer ? ResponseActionSide * 2f * scale : 0f;
        var left = origin.X + radius * 2f + 14f * scale;
        var textWidth = MathF.Max(1f, origin.X + width - actionsWidth - 8f * scale - left);
        var name = responseLabels.Name(index);
        var nameSize = Typography.Measure(name, TextStyles.Subheadline);
        var body = responseLabels.Body(index);
        var bodyHeight = MathF.Min(Typography.MeasureWrappedBlock(body, ResponseBodyStyle, textWidth).Y,
            Typography.LineHeight(ResponseBodyStyle) * ResponseAnswerMaxLines);
        var height = MathF.Max(SheetRowHeight * scale, nameSize.Y + bodyHeight + 14f * scale);
        var center = new Vector2(origin.X + radius + 4f * scale, origin.Y + 8f * scale + radius);
        AvatarView.DrawRemote(drawList, center, radius, theme, name, string.Empty, response.AvatarUrl, images,
            lodestone, 0.8f, 28, 1f, Frames.Of(response.FrameId));
        var top = origin.Y + 6f * scale;
        var rowHovering = UiInteract.Hover(origin, new Vector2(origin.X + width, origin.Y + height));
        var stamp = responseLabels.Stamp(index);
        var stampWidth = Typography.Measure(stamp, TextStyles.Caption1).X;
        UserName.Draw(responseLabels.NameId(index), name, response.Badges, response.BadgeIds, left, top,
            MathF.Max(1f, textWidth - stampWidth - 8f * scale), TextStyles.Subheadline, theme.TextStrong, rowHovering,
            theme);
        Typography.Draw(drawList, new Vector2(left + textWidth - stampWidth, top + 2f * scale), stamp, theme.TextMuted,
            TextStyles.Caption1);
        var bodyTop = top + nameSize.Y + 2f * scale;
        drawList.PushClipRect(new Vector2(left, bodyTop), new Vector2(left + textWidth, bodyTop + bodyHeight), true);
        Typography.DrawWrappedLeft(new Vector2(left, bodyTop), body, isAnswer ? theme.TextStrong : theme.TextMuted,
            ResponseBodyStyle, textWidth);
        drawList.PopClipRect();
        if (isAnswer)
        {
            var actionY = origin.Y + height * 0.5f;
            var blockCenter = new Vector2(origin.X + width - ResponseActionSide * 0.5f * scale, actionY);
            var deleteCenter = new Vector2(blockCenter.X - ResponseActionSide * scale, actionY);
            if (ResponseAction(drawList, deleteCenter, PhoneIcons.Trash, Loc.T(L.Common.Delete), theme, scale))
            {
                hooks.DeleteResponse(story, response);
            }

            if (ResponseAction(drawList, blockCenter, PhoneIcons.Ban, Loc.T(L.Social.BlockAction), theme, scale))
            {
                hooks.BlockResponder(response);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static bool ResponseAction(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        PhoneTheme theme, float scale)
    {
        var half = new Vector2(ResponseActionSide * 0.5f * scale, ResponseActionSide * 0.5f * scale);
        var hovered = UiInteract.Hover(center - half, center + half);
        if (hovered)
        {
            Squircle.Fill(drawList, center - half, center + half, half.X,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, center, glyph, hovered ? theme.TextStrong : theme.TextMuted, 16f * scale);
        HoverTooltip.Show(new Rect(center - half, center + half), tooltip);
        return UiInteract.Click(center - half, center + half, hovered);
    }

    private sealed class StoryResponseRowLabels
    {
        private StoryResponseDto[] source = Array.Empty<StoryResponseDto>();
        private string language = string.Empty;
        private long stampBucket = -1;
        private string[] names = Array.Empty<string>();
        private string[] nameIds = Array.Empty<string>();
        private string[] bodies = Array.Empty<string>();
        private string[] stamps = Array.Empty<string>();

        public string Name(int index) => names[index];

        public string NameId(int index) => nameIds[index];

        public string Body(int index) => bodies[index];

        public string Stamp(int index) => stamps[index];

        public void Sync(StoryResponseDto[] items)
        {
            var code = Loc.Current.Code;
            var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StampRefreshSeconds;
            var contentChanged = !ReferenceEquals(source, items) || !string.Equals(language, code, StringComparison.Ordinal);
            if (!contentChanged && bucket == stampBucket)
            {
                return;
            }

            if (contentChanged)
            {
                source = items;
                language = code;
                names = new string[items.Length];
                nameIds = new string[items.Length];
                bodies = new string[items.Length];
                for (var index = 0; index < items.Length; index++)
                {
                    var item = items[index];
                    names[index] = SocialIdentity.Name(item.DisplayName, item.Handle);
                    nameIds[index] = "storyresponse.name." + item.Kind + "." + item.Id;
                    bodies[index] = item.Kind == StoryStickerKinds.Question
                        ? item.Text
                        : Loc.T(L.Story.VotedFor, item.Text);
                }
            }

            stampBucket = bucket;
            stamps = new string[items.Length];
            for (var index = 0; index < items.Length; index++)
            {
                stamps[index] = TimeText.Short(items[index].CreatedAtUnix);
            }
        }
    }
}
