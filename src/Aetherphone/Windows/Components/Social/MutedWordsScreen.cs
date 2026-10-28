using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

// The Muted words list and its add and edit sheet, shared by Chirper and Aethergram so both apps show the same
// account-wide list the same way.
internal sealed class MutedWordsScreen
{
    private const float HeaderIconSize = 20f;
    private const float RowPadY = 11f;
    private const float RowLineGap = 3f;
    private const float HintPadY = 10f;
    private const float LoadingTop = 60f;
    private const float ChevronSize = 14f;
    private const float ChevronGap = 10f;
    private const string Separator = " · ";

    private static readonly TextStyle PhraseStyle = TextStyles.Headline;
    private static readonly TextStyle DetailStyle = TextStyles.Subheadline;
    private static readonly TextStyle HintStyle = TextStyles.Footnote;

    private readonly MutedWordSheet sheet = new();
    private string[] details = Array.Empty<string>();
    private MutedWordDto[]? detailsFor;
    private LanguageInfo? detailsLanguage;
    private long detailsMinute = -1;

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open(SocialFeedStore store)
    {
        sheet.Close();
        store.RefreshMutedWords();
    }

    public void Draw(Rect area, SocialFeedStore store, AppSkin ui, SocialInk ink, Action back, in TextStyle titleStyle,
        bool centered)
    {
        var scale = UiScale.Current;
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Social.MutedWords), ink, back, titleStyle,
            SocialChrome.HeaderReserve(1), string.Empty, true, centered);
        if (SocialChrome.DrawHeaderIcon(ImGui.GetWindowDrawList(), SocialChrome.HeaderSlot(area, 0),
                SocialChrome.HeaderIconRadius * scale, PhoneIcons.Plus, HeaderIconSize, Loc.T(L.Social.MutedWordAdd),
                ink, ink.TitleInk))
        {
            sheet.OpenNew();
        }

        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var entries = store.MutedWords;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (CountLive(entries, nowUnix) == 0)
        {
            DrawEmpty(listRect, store, ui, ink);
            return;
        }

        SyncDetails(entries, nowUnix);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            DrawHint(ink);
            for (var index = 0; index < entries.Length; index++)
            {
                if (entries[index].ExpiresAtUnix is { } expires && expires <= nowUnix)
                {
                    continue;
                }

                using (UiScope.PushId(entries[index].Id))
                {
                    if (DrawRow(entries[index].Phrase, details[index], ink))
                    {
                        sheet.OpenEdit(entries[index]);
                    }
                }
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    public void DrawSheet(Rect screen, PhoneTheme theme, SocialInk ink, SocialFeedStore store, ScreenToast toast)
    {
        var action = sheet.Draw(screen, theme, ink);
        if (action == MutedWordSheetAction.Save)
        {
            Save(store, toast, sheet.EditingId, sheet.Request());
        }
        else if (action == MutedWordSheetAction.Delete && sheet.EditingId is { } wordId)
        {
            Delete(store, toast, wordId, sheet.Phrase);
        }
    }

    public static void MuteHashtag(SocialFeedStore store, ScreenToast toast, string tag)
    {
        Save(store, toast, null, new SaveMutedWordRequest("#" + tag, true, true, MutedWordAudiences.Anyone,
            MutedWordDurations.Forever));
    }

    private static void Save(SocialFeedStore store, ScreenToast toast, string? wordId, SaveMutedWordRequest request)
    {
        toast.Show(Loc.T(L.Social.MutedToast, request.Phrase));
        store.SaveMutedWord(wordId, request, result =>
        {
            if (result == MutedWordSaveResult.Full)
            {
                toast.Show(Loc.T(L.Social.MutedWordFull, MutedWordMatcher.MaxEntries));
            }
            else if (result == MutedWordSaveResult.Failed)
            {
                toast.Show(Loc.T(L.Social.MuteFailed));
            }
        });
    }

    private static void Delete(SocialFeedStore store, ScreenToast toast, string wordId, string phrase)
    {
        toast.Show(Loc.T(L.Social.UnmutedToast, phrase));
        store.DeleteMutedWord(wordId, deleted =>
        {
            if (!deleted)
            {
                toast.Show(Loc.T(L.Social.MuteFailed));
            }
        });
    }

    private void DrawEmpty(Rect body, SocialFeedStore store, AppSkin ui, SocialInk ink)
    {
        if (store.MutedWordsFailed)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.VolumeMute, Loc.T(L.Common.LoadFailed),
                    Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry)))
            {
                store.RefreshMutedWords();
            }

            return;
        }

        if (!store.MutedWordsLoaded)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + LoadingTop * UiScale.Current),
                Loc.T(L.Common.Loading), ink.MutedInk);
            return;
        }

        if (EmptyState.Draw(body, ui, FontAwesomeIcon.VolumeMute, Loc.T(L.Social.MutedWordsEmpty),
                Loc.T(L.Social.MutedWordsHint), Loc.T(L.Social.MutedWordAdd)))
        {
            sheet.OpenNew();
        }
    }

    private static void DrawHint(SocialInk ink)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var padX = SocialChrome.CellPadX * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + padX, origin.Y + HintPadY * scale),
            Loc.T(L.Social.MutedWordsHint), ink.MutedInk, HintStyle, width - padX * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + HintPadY * 2f * scale));
    }

    private static bool DrawRow(string phrase, string detail, SocialInk ink)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var padX = SocialChrome.CellPadX * scale;
        var textWidth = MathF.Max(1f, width - padX * 2f - (ChevronSize + ChevronGap) * scale);
        var phraseHeight = Typography.LineHeight(PhraseStyle);
        var detailHeight = Typography.MeasureWrappedBlock(detail, DetailStyle, textWidth).Y;
        var height = RowPadY * 2f * scale + phraseHeight + RowLineGap * scale + detailHeight;
        var cell = FeedCell.Begin(drawList, height, ink.HoverTint);
        var left = cell.Bounds.Min.X + padX;
        var top = cell.Bounds.Min.Y + RowPadY * scale;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(phrase, textWidth, PhraseStyle),
            ink.TitleInk, PhraseStyle);
        Typography.DrawWrappedLeft(new Vector2(left, top + phraseHeight + RowLineGap * scale), detail, ink.MutedInk,
            DetailStyle, textWidth);
        PhoneIcon.Draw(drawList, new Vector2(cell.Bounds.Max.X - padX - ChevronSize * 0.5f * scale, cell.Bounds.Center.Y),
            PhoneIcons.ChevronRight, ink.FaintInk, ChevronSize * scale);
        FeedCell.End(drawList, cell, ink.Hairline);
        return cell.Tapped;
    }

    // Details carry the time left, so they are rebuilt once a minute and whenever the list or the language changes,
    // never per frame.
    private void SyncDetails(MutedWordDto[] entries, long nowUnix)
    {
        var minute = nowUnix / 60;
        if (ReferenceEquals(entries, detailsFor) && ReferenceEquals(Loc.Current, detailsLanguage)
            && minute == detailsMinute)
        {
            return;
        }

        detailsFor = entries;
        detailsLanguage = Loc.Current;
        detailsMinute = minute;
        details = new string[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            details[index] = Describe(entries[index]);
            Plugin.Fonts.NoticeText(entries[index].Phrase);
        }
    }

    private static string Describe(MutedWordDto entry)
    {
        var scope = entry.InFeeds && entry.InNotifications
            ? string.Concat(Loc.T(L.Social.MutedWordFeeds), Separator, Loc.T(L.Social.MutedWordNotifications))
            : Loc.T(entry.InFeeds ? L.Social.MutedWordFeeds : L.Social.MutedWordNotifications);
        var audience = Loc.T(entry.Audience == MutedWordAudiences.NotFollowing
            ? L.Social.MutedWordNotFollowing
            : L.Social.MutedWordAnyone);
        var lifetime = entry.ExpiresAtUnix is { } expires
            ? Loc.T(L.Social.MutedWordEnds, TimeText.Until(expires))
            : Loc.T(L.Social.MutedWordForever);
        return string.Concat(scope, Separator, audience, Separator, lifetime);
    }

    private static int CountLive(MutedWordDto[] entries, long nowUnix)
    {
        var live = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].ExpiresAtUnix is not { } expires || expires > nowUnix)
            {
                live++;
            }
        }

        return live;
    }
}
