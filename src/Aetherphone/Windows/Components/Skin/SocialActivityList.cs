using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class SocialActivityList
{
    private static readonly TextStyle BodyStyle = new(0.88f, FontWeight.Regular);

    public static void Draw(Rect area, AppSkin ui, AppPalette palette, PhoneTheme theme, NotificationDto[] items,
        string app, RemoteImageCache images, LodestoneService lodestone, Action<NotificationDto> openActor,
        Action<NotificationDto> openPost, Action? loadOlder = null)
    {
        var scale = UiScale.Current;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (Shows(items[index], app))
            {
                count++;
            }
        }

        using (AppSurface.Begin(area))
        {
            if (count == 0)
            {
                Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 90f * scale),
                    Loc.T(L.Social.ActivityEmpty), palette.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 6f * scale));
            for (var index = 0; index < items.Length; index++)
            {
                if (Shows(items[index], app))
                {
                    DrawRow(items[index], ui, palette, theme, images, lodestone, openActor, openPost);
                }
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            if (loadOlder is not null && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 300f * scale)
            {
                loadOlder();
            }
        }
    }

    private static bool Shows(NotificationDto item, string app)
    {
        return item.App == app && !SocialActivity.IsModerationNotice(item.Type);
    }

    private static void DrawRow(NotificationDto item, AppSkin ui, AppPalette palette, PhoneTheme theme,
        RemoteImageCache images, LodestoneService lodestone, Action<NotificationDto> openActor,
        Action<NotificationDto> openPost)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 12f * scale;
        var radius = 20f * scale;
        var textLeft = origin.X + pad + radius * 2f + 12f * scale;
        var timeText = TimeText.Short(item.CreatedAtUnix);
        var timeSize = Typography.Measure(timeText, 0.78f);
        var textRight = origin.X + width - pad - timeSize.X - 8f * scale;
        var textWidth = textRight - textLeft;
        var actorLabel = SocialActivity.ActorLabel(item);
        var body = SocialActivity.Body(item);
        var actorSize = Typography.Measure(actorLabel, 0.95f, FontWeight.SemiBold);
        var bodyHeight = body.Length > 0 ? EmojiText.BlockHeight(body, BodyStyle, textWidth) : 0f;
        var contentHeight = actorSize.Y + 4f * scale + bodyHeight;
        var rowHeight = MathF.Max(radius * 2f + pad * 2f, contentHeight + pad * 2f);
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        var rowRounding = 16f * scale;
        ui.Card(drawList, origin, rowMax, rowRounding);
        UiInteract.HoverHighlight(drawList, origin, rowMax, rowRounding);
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, item.ActorName, string.Empty,
            item.ActorAvatarUrl, images, lodestone, 0.95f, 32, 1f, Frames.Of(item.ActorFrameId));
        DrawTypeBadge(drawList, avatarCenter + new Vector2(radius - 4f * scale, radius - 4f * scale), item.Type,
            theme, scale);
        var textTop = origin.Y + (rowHeight - contentHeight) * 0.5f;
        var rowHovering = UiInteract.Hover(origin, rowMax);
        UserName.Draw(drawList, "socialactivity.actor." + item.Id, actorLabel, item.ActorBadges, item.ActorBadgeIds, textLeft, textTop,
            textWidth, new TextStyle(0.95f, FontWeight.SemiBold), theme.TextStrong, rowHovering, theme);
        Typography.Draw(new Vector2(origin.X + width - pad - timeSize.X, textTop + 2f * scale), timeText,
            palette.MutedInk, 0.78f);
        if (body.Length > 0)
        {
            EmojiText.DrawBlock(new Vector2(textLeft, textTop + actorSize.Y + 4f * scale), body, palette.BodyInk,
                BodyStyle, textWidth);
        }

        var avatarMin = avatarCenter - new Vector2(radius, radius);
        var avatarMax = avatarCenter + new Vector2(radius, radius);
        var overAvatar = UiInteract.Hover(avatarMin, avatarMax);
        if (overAvatar)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(avatarMin, avatarMax, true))
            {
                openActor(item);
            }
        }
        else if (UiInteract.HoverClick(origin, rowMax))
        {
            if (SocialActivity.OpensPost(item))
            {
                openPost(item);
            }
            else
            {
                openActor(item);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private static void DrawTypeBadge(ImDrawListPtr drawList, Vector2 center, int type, PhoneTheme theme, float scale)
    {
        var (glyph, color) = type switch
        {
            SocialActivity.TypeLike => (IconGlyph.Of(FontAwesomeIcon.Heart), theme.Danger),
            SocialActivity.TypeComment => (IconGlyph.Of(FontAwesomeIcon.Comment), theme.Accent),
            SocialActivity.TypeFollow => (IconGlyph.Of(FontAwesomeIcon.UserPlus), theme.Accent),
            SocialActivity.TypeConnectRequest => (IconGlyph.Of(FontAwesomeIcon.UserPlus), theme.Accent),
            SocialActivity.TypeConnectAccept => (IconGlyph.Of(FontAwesomeIcon.UserCheck), theme.Accent),
            SocialActivity.TypeCommentLike => (IconGlyph.Of(FontAwesomeIcon.Heart), theme.Danger),
            SocialActivity.TypeMention => (IconGlyph.Of(FontAwesomeIcon.At), theme.Accent),
            SocialActivity.TypeCommentMention => (IconGlyph.Of(FontAwesomeIcon.At), theme.Accent),
            SocialActivity.TypeStoryMention => (IconGlyph.Of(FontAwesomeIcon.At), theme.Accent),
            SocialActivity.TypePhotoTag => (IconGlyph.Of(FontAwesomeIcon.UserTag), theme.Accent),
            SocialActivity.TypeRepost => (IconGlyph.Of(FontAwesomeIcon.Retweet), theme.Accent),
            SocialActivity.TypeQuote => (IconGlyph.Of(FontAwesomeIcon.QuoteRight), theme.Accent),
            SocialActivity.TypeFollowRequest => (IconGlyph.Of(FontAwesomeIcon.UserClock), theme.Accent),
            SocialActivity.TypeFollowAccept => (IconGlyph.Of(FontAwesomeIcon.UserCheck), theme.Accent),
            SocialActivity.TypeRadioLive => (IconGlyph.Of(FontAwesomeIcon.BroadcastTower), theme.Accent),
            _ => (IconGlyph.Of(FontAwesomeIcon.Bell), theme.Accent),
        };
        var badgeRadius = 8f * scale;
        drawList.AddCircleFilled(center, badgeRadius + 2f * scale, ImGui.GetColorU32(theme.AppBackground), 20);
        drawList.AddCircleFilled(center, badgeRadius, ImGui.GetColorU32(color), 20);
        AppSkin.Icon(drawList, center, glyph, new Vector4(1f, 1f, 1f, 1f), 0.5f);
    }
}
