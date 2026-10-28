using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum SuggestedPeopleAction : byte
{
    None,
    Follow,
    Dismiss,
    OpenProfile,
}

internal readonly record struct SuggestedPeopleResult(SuggestedPeopleAction Action, UserDto? User);

internal readonly record struct SuggestedPeopleLabels(string Follow, string Following);

internal sealed class SuggestedPeopleRail
{
    private const float CardWidth = 132f;
    private const float CardHeight = 178f;
    private const float CardGap = 10f;
    private const float Inset = 14f;
    private const float CardRounding = 14f;
    private const float CardPadX = 10f;
    private const float AvatarRadius = 30f;
    private const float AvatarTop = 16f;
    private const float NameGap = 10f;
    private const float LineGap = 2f;
    private const float ButtonPadBottom = 12f;
    private const float DismissSize = 22f;
    private const float DismissInset = 4f;
    private const float DismissGlyph = 11f;
    private const float BottomGap = 10f;
    private const int DismissedCap = 500;
    private const string FollowButtonId = "follow";
    private static readonly TextStyle NameStyle = TextStyles.Headline;
    private static readonly TextStyle ReasonStyle = TextStyles.Footnote;

    private readonly PanRail rail = new();

    public static bool HasAny(SuggestedUserDto[] people, HashSet<string> dismissed)
    {
        for (var index = 0; index < people.Length; index++)
        {
            if (!dismissed.Contains(people[index].User.Id))
            {
                return true;
            }
        }

        return false;
    }

    public static bool OpensProfile(in SuggestedPeopleResult result, SocialFeedStore store, Configuration configuration)
    {
        if (result.User is not { } user)
        {
            return false;
        }

        switch (result.Action)
        {
            case SuggestedPeopleAction.Follow:
                store.ToggleFollow(user);
                return false;
            case SuggestedPeopleAction.Dismiss:
                var dismissed = configuration.DismissedFollowSuggestions;
                if (dismissed.Count >= DismissedCap)
                {
                    dismissed.Clear();
                }

                dismissed.Add(user.Id);
                configuration.Save();
                store.DismissSuggestion(user.Id);
                return false;
            default:
                return result.Action == SuggestedPeopleAction.OpenProfile;
        }
    }

    public static string ReasonLine(SuggestedUserDto suggestion) => suggestion.Reason switch
    {
        FollowSuggestionReasons.FollowsYou => Loc.T(L.Social.FollowsYou),
        FollowSuggestionReasons.Interacts => Loc.T(L.Social.SuggestedInteracts),
        FollowSuggestionReasons.FollowedByFollows when SocialProfilePages.FollowedByLine(suggestion.User) is { Length: > 0 } line =>
            line,
        FollowSuggestionReasons.Popular => Loc.T(L.Social.SuggestedPopular),
        _ => Loc.T(L.Social.FeedSuggested),
    };

    public SuggestedPeopleResult Draw(SuggestedUserDto[] people, HashSet<string> dismissed, SocialInk ink,
        in TextStyle sectionStyle, in SuggestedPeopleLabels labels, string anchorKey, PhoneTheme theme,
        RemoteImageCache images, LodestoneService lodestone)
    {
        SocialChrome.DrawSectionLabel(Loc.T(L.Social.PeopleToFollow), ink, sectionStyle);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var cardWidth = CardWidth * scale;
        var cardHeight = CardHeight * scale;
        var gap = CardGap * scale;
        var inset = Inset * scale;
        var count = 0;
        for (var index = 0; index < people.Length; index++)
        {
            if (!dismissed.Contains(people[index].User.Id))
            {
                count++;
            }
        }

        var railRect = new Rect(origin, origin + new Vector2(width, cardHeight));
        rail.Begin(railRect, inset * 2f + count * cardWidth + Math.Max(0, count - 1) * gap);
        var result = default(SuggestedPeopleResult);
        var slot = 0;
        for (var index = 0; index < people.Length; index++)
        {
            var person = people[index];
            if (dismissed.Contains(person.User.Id))
            {
                continue;
            }

            var left = origin.X + inset + slot * (cardWidth + gap) - rail.Offset;
            slot++;
            if (left + cardWidth < origin.X || left > origin.X + width)
            {
                continue;
            }

            var card = new Rect(new Vector2(left, origin.Y), new Vector2(left + cardWidth, origin.Y + cardHeight));
            var action = DrawCard(drawList, person, card, ink, labels, theme, images, lodestone);
            if (action != SuggestedPeopleAction.None)
            {
                result = new SuggestedPeopleResult(action, person.User);
            }
        }

        rail.End();
        if (UiAnchors.Recording && ImGui.IsRectVisible(railRect.Min, railRect.Max))
        {
            UiAnchors.Report(anchorKey, railRect);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + BottomGap * scale));
        return result;
    }

    private SuggestedPeopleAction DrawCard(ImDrawListPtr drawList, SuggestedUserDto person, Rect card,
        SocialInk ink, in SuggestedPeopleLabels labels, PhoneTheme theme, RemoteImageCache images,
        LodestoneService lodestone)
    {
        var scale = UiScale.Current;
        var user = person.User;
        var rounding = CardRounding * scale;
        var buttonHeight = Button.SmallHeight * scale;
        var buttonBottom = card.Max.Y - ButtonPadBottom * scale;
        var button = new Rect(new Vector2(card.Min.X + CardPadX * scale, buttonBottom - buttonHeight),
            new Vector2(card.Max.X - CardPadX * scale, buttonBottom));
        var dismissSize = DismissSize * scale;
        var dismissMin = new Vector2(card.Max.X - dismissSize - DismissInset * scale, card.Min.Y + DismissInset * scale);
        var dismissMax = dismissMin + new Vector2(dismissSize, dismissSize);
        var dismissHovered = rail.Hover(dismissMin, dismissMax);
        var bodyMax = new Vector2(card.Max.X, button.Min.Y);
        var bodyHovered = !dismissHovered && rail.Hover(card.Min, bodyMax);
        drawList.AddRectFilled(card.Min, card.Max, ImGui.GetColorU32(bodyHovered ? ink.ChipHover : ink.ChipFill), rounding);
        drawList.AddRect(card.Min, card.Max, ImGui.GetColorU32(ink.ChipStroke), rounding);
        if (bodyHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(card.Center.X, card.Min.Y + AvatarTop * scale + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, displayName, string.Empty, user.AvatarUrl, images,
            lodestone, 0.95f, 32, 1f, Frames.Of(user.FrameId));
        var textWidth = MathF.Max(1f, card.Width - CardPadX * 2f * scale);
        var nameHeight = Typography.LineHeight(NameStyle);
        var nameCenterY = avatarCenter.Y + avatarRadius + NameGap * scale + nameHeight * 0.5f;
        Typography.DrawCenteredLine(drawList, new Vector2(card.Center.X, nameCenterY),
            Typography.FitText(displayName, textWidth, NameStyle), ink.TitleInk, NameStyle);
        var reasonCenterY = nameCenterY + nameHeight * 0.5f + LineGap * scale + Typography.LineHeight(ReasonStyle) * 0.5f;
        Typography.DrawCenteredLine(drawList, new Vector2(card.Center.X, reasonCenterY),
            Typography.FitText(ReasonLine(person), textWidth, ReasonStyle), ink.MutedInk, ReasonStyle);
        PhoneIcon.Draw(drawList, (dismissMin + dismissMax) * 0.5f, PhoneIcons.X, dismissHovered ? ink.TitleInk : ink.MutedInk,
            DismissGlyph * scale);

        var state = SocialFeedStore.FollowStateOf(user);
        var label = state switch
        {
            FollowState.Following => labels.Following,
            FollowState.Requested => Loc.T(L.Social.Requested),
            _ => labels.Follow,
        };
        bool followClicked;
        using (UiScope.PushId(user.Id))
        {
            followClicked = Button.Draw(drawList, button, label, ink.Control,
                state == FollowState.None ? ButtonStyle.Prominent : ButtonStyle.Gray, id: FollowButtonId);
        }
        if (rail.Dragging)
        {
            return SuggestedPeopleAction.None;
        }

        if (followClicked)
        {
            return SuggestedPeopleAction.Follow;
        }

        if (rail.Tapped(dismissMin, dismissMax, dismissHovered))
        {
            return SuggestedPeopleAction.Dismiss;
        }

        return rail.Tapped(card.Min, bodyMax, bodyHovered) ? SuggestedPeopleAction.OpenProfile : SuggestedPeopleAction.None;
    }
}
