using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Team;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const string TeamSelfId = "team.self";
    private const string TeamAutomatedId = "team.automated";
    private const float TeamMinTranscriptHeight = 90f;
    private const int TeamResumeFrameGap = 2;
    private const float TeamVerifiedGlyph = 15f;
    private const float TeamVerifiedGap = 4f;
    private const float TeamBannerPadY = 8f;
    private const float TeamBannerPadX = 14f;
    private const float TeamBannerAlpha = 0.14f;

    private static readonly TextStyle TeamBannerStyle = TextStyles.Footnote;

    private readonly TeamInboxStore team;
    private readonly ChatTranscript teamTranscript = new();
    private readonly ChatComposer teamComposer = new();
    private readonly TeamPaging teamPaging;
    private readonly FailureSlot teamSendFailure = new();
    private readonly Action<string, string, string?> sendTeamText;
    private readonly Action<string, string, string> ignoreTeamEdit = static (_, _, _) => { };
    private TranscriptMessage[] teamTranscriptCache = Array.Empty<TranscriptMessage>();
    private int teamTranscriptVersion = -1;
    private LanguageInfo? teamTranscriptLanguage;
    private string teamPreview = string.Empty;
    private int teamPreviewVersion = -1;
    private LanguageInfo? teamPreviewLanguage;
    private string? teamRestoreDraft;
    private int lastTeamDrawFrame = -1;
    private int teamComposerGeneration = -1;

    private sealed class TeamPaging : IChatTranscriptPaging
    {
        private readonly TeamInboxStore store;

        public TeamPaging(TeamInboxStore store)
        {
            this.store = store;
        }

        public bool HasMoreOlder => store.HasMoreOlder;

        public bool LoadingOlder => store.LoadingOlder;

        public void LoadOlder() => store.LoadOlder();
    }

    private bool TeamRowVisible(string query)
    {
        if (!team.HasThread)
        {
            return false;
        }

        if (query.Length > 0 && !Loc.T(L.TeamInbox.Name).Contains(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        return chatFilter switch
        {
            ChatFilterUnread => team.Unread > 0,
            ChatFilterAll => true,
            _ => false,
        };
    }

    private void DrawTeamRow(ImDrawListPtr drawList)
    {
        var scale = UiScale.Current;
        var row = BeginPersonRow(drawList, ChatRowHeight, ChatRowAvatarRadius, 0f, true, out var avatarCenter);
        BrandMark.DrawAvatar(drawList, avatarCenter, ChatRowAvatarRadius * scale);
        var newest = team.Newest;
        var unreadCount = team.Unread;
        var lineTop = row.Bounds.Min.Y + ChatRowTitleTop * scale;
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var timeLabel = newest is null ? string.Empty : ChatTime(newest.CreatedAtUnix);
        var timeSize = Typography.Measure(timeLabel, RowMetaStyle);
        var timeLeft = row.TextRight - timeSize.X;
        Typography.Draw(drawList, new Vector2(timeLeft, lineTop + (titleHeight - timeSize.Y) * 0.5f), timeLabel,
            unreadCount > 0 ? ink.AccentLink : ink.MutedInk, RowMetaStyle);
        var glyph = TeamVerifiedGlyph * scale;
        var glyphGap = TeamVerifiedGap * scale;
        var titleRight = timeLabel.Length > 0 ? timeLeft - TimeGap * scale : row.TextRight;
        var title = Typography.FitText(Loc.T(L.TeamInbox.Name),
            MathF.Max(1f, titleRight - row.TextLeft - glyph - glyphGap), RowTitleStyle);
        Typography.Draw(drawList, new Vector2(row.TextLeft, lineTop), title, ink.TitleInk, RowTitleStyle);
        var titleWidth = Typography.Measure(title, RowTitleStyle).X;
        PhoneIcon.Draw(drawList,
            new Vector2(row.TextLeft + titleWidth + glyphGap + glyph * 0.5f, lineTop + titleHeight * 0.5f),
            PhoneIcons.CircleCheckFilled, RoleInk.For(RoleKind.Verified, theme), glyph);
        var subHeight = Typography.LineHeight(RowSubStyle);
        var subTop = lineTop + titleHeight + RowLineGap * scale;
        var subCenterY = subTop + subHeight * 0.5f;
        var right = row.TextRight;
        if (unreadCount > 0)
        {
            right = DrawUnreadBadge(drawList, right, subCenterY, unreadCount, false);
        }

        PhoneIcon.Draw(drawList, new Vector2(right - RowStatusGlyph * 0.5f * scale, subCenterY), PhoneIcons.PinFilled,
            ink.MutedInk, RowStatusGlyph * scale);
        right -= RowStatusPitch * scale;
        var previewLeft = row.TextLeft;
        if (newest is { FromTeam: false })
        {
            PhoneIcon.Draw(drawList, new Vector2(previewLeft + PreviewGlyph * 0.5f * scale, subCenterY),
                newest.Read ? PhoneIcons.Checks : PhoneIcons.Check,
                newest.Read ? ChatInk.SeenTick : ink.MutedInk, PreviewGlyph * scale);
            previewLeft += PreviewGlyph * scale + PreviewGlyphGap * scale;
        }

        var previewRight = right - RowTrailingGap * scale;
        Typography.Draw(drawList, new Vector2(previewLeft, subTop),
            Typography.FitText(TeamPreview(), MathF.Max(1f, previewRight - previewLeft), RowSubStyle),
            unreadCount > 0 ? ink.BodyInk : ink.MutedInk, RowSubStyle);
        if (row.Tapped)
        {
            OpenTeamThread();
        }

        EndPersonRow(drawList, row);
    }

    private string TeamPreview()
    {
        var version = team.Version;
        if (version == teamPreviewVersion && ReferenceEquals(teamPreviewLanguage, Loc.Current))
        {
            return teamPreview;
        }

        teamPreview = team.Newest is { } newest ? ChatText.ListPreview(TeamMessageText.Render(newest)) : string.Empty;
        teamPreviewVersion = version;
        teamPreviewLanguage = Loc.Current;
        return teamPreview;
    }

    private void SendTeamText(string text)
    {
        if (!team.Send(text))
        {
            teamRestoreDraft = text;
        }
    }

    private void OpenTeamThread()
    {
        activeTab = MessageTab.Chats;
        if (router.Current.Screen == MessageScreen.Team)
        {
            return;
        }

        router.Push(MessageRoute.Team);
    }

    private void DrawTeamThread(Rect area, bool activeLayer)
    {
        var frame = ImGui.GetFrameCount();
        var resumed = frame - lastTeamDrawFrame > TeamResumeFrameGap;
        lastTeamDrawFrame = frame;
        if (activeLayer)
        {
            if (resumed)
            {
                team.OnThreadOpened();
            }

            team.NoteViewed();
        }

        ForgetTeamDraftOnAccountChange();
        RestoreTeamDraft();
        DrawTeamHeader(area);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var accessoryHeight = teamComposer.AccessoryHeight;
        var composerHeight = teamComposer.Measure(ChatComposerStyle.Plus,
            area.Max.Y - top - accessoryHeight - TeamMinTranscriptHeight * scale);
        var listRect = new Rect(new Vector2(area.Min.X, top),
            new Vector2(area.Max.X, area.Max.Y - composerHeight - accessoryHeight));
        DrawTeamBanner(ref listRect, Loc.T(L.TeamInbox.Disclosure), ui.Accent, false);
        if (team.SendFailure is { } failure)
        {
            teamSendFailure.Set(failure.Failure);
            if (DrawTeamBanner(ref listRect, teamSendFailure.Text(), theme.Danger, true))
            {
                team.ClearSendFailure();
            }
        }

        ChatWallpapers.Paint(ImGui.GetWindowDrawList(), listRect,
            ChatWallpapers.Effective(configuration.MessageChatWallpapers, configuration.MessageWallpaper,
                TeamInboxStore.ThreadKey), configuration.MessageWallpaperPattern, wallpaperImages, 1f);
        var model = new ChatTranscriptModel
        {
            ThreadId = TeamInboxStore.ThreadKey,
            Messages = TeamTranscript(),
            MyUserId = TeamSelfId,
            Accent = ui.Accent,
            Theme = theme,
            MutedInk = ui.MutedInk,
            BodyInk = ui.BodyInk,
            EmptyText = Loc.T(L.TeamInbox.Empty),
            LoadingText = Loc.T(L.Common.Loading),
            Loading = team.Loading,
            IsGroup = true,
            FullSenderNames = true,
            Bubbles = new ChatBubbleStyle(activeTheme.OutgoingBubble, ChatThemes.OutgoingInk,
                ChatThemes.IncomingBubble, ChatThemes.IncomingInk, MessageThreadViewBase.BubbleRounding, true),
            SidePadding = MessageThreadViewBase.ThreadSidePadding,
            Paging = teamPaging,
        };
        teamTranscript.Draw(listRect, model);
        var composerRect = new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max);
        teamComposer.Draw(composerRect, new ChatComposerModel
        {
            Ui = ui,
            Style = ChatComposerStyle.Plus,
            Screen = area,
            Hint = Loc.T(L.TeamInbox.ComposerHint),
            ConversationId = TeamInboxStore.ThreadKey,
            MaxLength = TeamInboxStore.MessageMax,
            Sending = team.Sending,
            CanHandleEscape = true,
            Blocked = !team.CanReply,
            BlockedNotice = Loc.T(L.TeamInbox.RepliesClosed),
            OnSendText = sendTeamText,
            OnEditText = ignoreTeamEdit,
        });
    }

    private void ForgetTeamDraftOnAccountChange()
    {
        var generation = team.AccountGeneration;
        if (generation == teamComposerGeneration)
        {
            return;
        }

        teamComposerGeneration = generation;
        teamRestoreDraft = null;
        teamComposer.Clear();
    }

    private void RestoreTeamDraft()
    {
        var restore = teamRestoreDraft ?? team.TakeReturnedDraft();
        teamRestoreDraft = null;
        if (restore is null || teamComposer.Draft.Trim().Length > 0)
        {
            return;
        }

        teamComposer.Draft = restore;
    }

    private void DrawTeamHeader(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var header = PaintHeaderBand(area);
        var rowCenterY = header.Center.Y;
        var chipRadius = SocialChrome.BackChipRadius * scale;
        var chipCenter = new Vector2(area.Min.X + 12f * scale + chipRadius, rowCenterY);
        if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, ink))
        {
            back();
        }

        var avatarRadius = ThreadHeaderAvatarRadius * scale;
        var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadHeaderAvatarGap * scale + avatarRadius,
            rowCenterY);
        BrandMark.DrawAvatar(drawList, avatarCenter, avatarRadius);
        var nameLeft = avatarCenter.X + avatarRadius + ThreadHeaderNameGap * scale;
        var nameRight = area.Max.X - CellPadX * scale;
        var name = Loc.T(L.TeamInbox.Name);
        var subtitle = Loc.T(L.TeamInbox.Official);
        var nameHeight = Typography.LineHeight(ThreadNameStyle);
        var subHeight = Typography.LineHeight(ThreadSubStyle);
        var nameTop = rowCenterY - (nameHeight + subHeight) * 0.5f;
        var glyph = TeamVerifiedGlyph * scale;
        var glyphGap = TeamVerifiedGap * scale;
        var nameWidth = MathF.Max(1f, nameRight - nameLeft - glyph - glyphGap);
        var fitted = Typography.FitText(name, nameWidth, ThreadNameStyle);
        Typography.Draw(drawList, new Vector2(nameLeft, nameTop), fitted, ink.TitleInk, ThreadNameStyle);
        var fittedWidth = Typography.Measure(fitted, ThreadNameStyle).X;
        PhoneIcon.Draw(drawList, new Vector2(nameLeft + fittedWidth + glyphGap + glyph * 0.5f, nameTop + nameHeight * 0.5f),
            PhoneIcons.CircleCheckFilled, RoleInk.For(RoleKind.Verified, theme), glyph);
        Typography.Draw(drawList, new Vector2(nameLeft, nameTop + nameHeight),
            Typography.FitText(subtitle, MathF.Max(1f, nameRight - nameLeft), ThreadSubStyle), ink.MutedInk,
            ThreadSubStyle);
    }

    private bool DrawTeamBanner(ref Rect listRect, string text, Vector4 tint, bool dismissible)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var padX = TeamBannerPadX * scale;
        var padY = TeamBannerPadY * scale;
        var textWidth = MathF.Max(1f, listRect.Width - padX * 2f);
        var textHeight = Typography.MeasureWrappedBlock(text, TeamBannerStyle, textWidth).Y;
        var min = listRect.Min;
        var max = new Vector2(listRect.Max.X, listRect.Min.Y + textHeight + padY * 2f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(tint, TeamBannerAlpha)));
        Typography.DrawWrappedCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), text,
            ui.MutedInk, TeamBannerStyle, textWidth);
        listRect = new Rect(new Vector2(listRect.Min.X, max.Y), listRect.Max);
        return dismissible && UiInteract.HoverClick(min, max);
    }

    private ReadOnlySpan<TranscriptMessage> TeamTranscript()
    {
        var version = team.Version;
        if (version == teamTranscriptVersion && ReferenceEquals(teamTranscriptLanguage, Loc.Current))
        {
            return teamTranscriptCache;
        }

        var source = team.Messages;
        var built = new TranscriptMessage[source.Length];
        var staffTint = RoleInk.For(RoleKind.Verified, theme);
        for (var index = 0; index < source.Length; index++)
        {
            built[index] = ToTranscript(source[index], staffTint);
        }

        teamTranscriptCache = built;
        teamTranscriptVersion = version;
        teamTranscriptLanguage = Loc.Current;
        return built;
    }

    private static TranscriptMessage ToTranscript(TeamMessageDto message, Vector4 staffTint)
    {
        var createdAt = message.CreatedAtUnix;
        var id = message.Id.ToString(CultureInfo.InvariantCulture);
        var body = TeamMessageText.Render(message);
        if (!message.FromTeam)
        {
            return new TranscriptMessage(id, TeamSelfId, body, 0, createdAt, 0, 0,
                message.Read ? createdAt : null, string.Empty, staffTint);
        }

        var automated = TeamMessageText.IsAutomated(message);
        var senderId = automated || message.StaffName.Length == 0 ? TeamAutomatedId : message.StaffName;
        var senderName = automated ? string.Empty : message.StaffName;
        return new TranscriptMessage(id, senderId, body, 0, createdAt, 0, 0, createdAt, senderName, staffTint);
    }
}
