using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Gifs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private enum ComposePanel : byte
    {
        None,
        Unsent,
        Schedule,
        Video,
    }

    private const int DraftsTab = 0;
    private const int ScheduledTab = 1;
    private const float DraftsIconSize = 18f;
    private const long ScheduledRowsRefreshSeconds = 30;

    private readonly DraftExitSheet draftExitSheet = new();
    private readonly DraftRowCache draftRows = new();
    private readonly FailureSlot scheduledFailureSlot = new();
    private DraftListRow[] scheduledRows = Array.Empty<DraftListRow>();
    private int scheduledRowsVersion = -1;
    private int scheduledRowsFormat = -1;
    private LanguageInfo? scheduledRowsLanguage;
    private long scheduledRowsBuiltAt;
    private ComposePanel composePanel;
    private int unsentTab;
    private Spring unsentTabSlide;
    private string? composeDraftId;
    private PostDto? pendingDraftQuote;
    private Action? closeCompose;
    private Action? closeComposePanel;

    private Action CloseCompose => closeCompose ??= RequestComposeClose;

    private Action CloseComposePanel => closeComposePanel ??= () => composePanel = ComposePanel.None;

    private List<ComposerDraft>? ComposeDrafts => ComposerDrafts.For(configuration.ChirperDrafts, store.AccountId);

    private bool DrawComposeExtras(Rect area)
    {
        if (Interlocked.Exchange(ref pendingDraftQuote, null) is { } quoted && quoted.Id == quoteTargetId)
        {
            quoteTarget = quoted;
        }

        switch (composePanel)
        {
            case ComposePanel.Unsent:
                DrawUnsentPanel(area);
                return true;
            case ComposePanel.Schedule:
                DrawSchedulePanel(area);
                return true;
            case ComposePanel.Video:
                DrawComposeVideoEditor(area);
                return true;
            default:
                return false;
        }
    }

    private float DrawComposeDraftsEntry(Rect action)
    {
        var scale = UiScale.Current;
        var radius = SocialChrome.HeaderIconRadius * scale;
        var center = new Vector2(action.Min.X - HeaderActionGap * scale - radius, action.Center.Y);
        UiAnchors.Report("chirper.compose.drafts", new Rect(center - new Vector2(radius, radius),
            center + new Vector2(radius, radius)));
        if (SocialChrome.DrawHeaderIcon(ImGui.GetWindowDrawList(), center, radius, PhoneIcons.FileText,
                DraftsIconSize, Loc.T(L.Social.Drafts), ChirperInk.Shared, ChirperInk.BodyInk))
        {
            OpenUnsentPanel(DraftsTab);
        }

        return radius * 2f + HeaderActionGap * scale;
    }

    private void OpenUnsentPanel(int tab)
    {
        composeEmoji.Close();
        unsentTab = tab;
        composePanel = ComposePanel.Unsent;
        if (tab == ScheduledTab)
        {
            store.LoadScheduled();
        }
    }

    private bool ComposeHasContent() =>
        !string.IsNullOrWhiteSpace(draft) || composeAttachments.Count > 0 || composePoll.Active || ComposeHasVideo;

    private void RequestComposeClose()
    {
        if (store.Posting || !ComposeHasContent())
        {
            CloseComposeNow();
            return;
        }

        draftExitSheet.Open();
    }

    private void DrawDraftExitSheet(Rect screen)
    {
        switch (draftExitSheet.Draw(screen, SheetStyle))
        {
            case DraftExitChoice.Save:
                SaveComposeDraft();
                CloseComposeNow();
                break;
            case DraftExitChoice.Discard:
                CloseComposeNow();
                break;
        }
    }

    private void CloseComposeNow()
    {
        ClearComposeForm();
        router.Pop();
    }

    private void ClearComposeForm()
    {
        draft = string.Empty;
        composeStatus = string.Empty;
        composeFailure.Clear();
        composeEmoji.Close();
        quoteTarget = null;
        quoteTargetId = null;
        composeAttachments.Clear();
        composeGif = null;
        composeIntake.Discard();
        composePicking = false;
        composeSensitive = false;
        composePoll.Reset();
        composeReplyPolicy = ChirperReplyPolicy.Everyone;
        threadDraft.Clear();
        CloseComposeVideo();
        ResetComposeExtras();
    }

    private void ResetComposeExtras()
    {
        composeDraftId = null;
        composeScheduledId = null;
        composeScheduleAtUnix = 0;
        composePanel = ComposePanel.None;
        draftExitSheet.Close();
        Interlocked.Exchange(ref pendingDraftQuote, null);
    }

    private void FinishComposeExtras()
    {
        if (composeScheduleAtUnix > 0)
        {
            toast.Show(Loc.T(L.Social.ScheduledToast, TimeText.FutureMoment(composeScheduleAtUnix)));
        }
        else if (composeScheduledId is { } postedNow)
        {
            store.DeleteScheduled(postedNow);
        }

        if (ComposerDrafts.Remove(configuration.ChirperDrafts, store.AccountId, composeDraftId))
        {
            configuration.Save();
        }

        ResetComposeExtras();
    }

    private void SaveComposeDraft()
    {
        if (store.AccountId is not { } accountId)
        {
            return;
        }

        var saved = new ComposerDraft
        {
            Id = composeDraftId ?? ComposerDrafts.NewId(),
            Text = draft,
            QuotedPostId = quoteTargetId,
            Sensitive = composeSensitive,
            ReplyPolicy = composeReplyPolicy,
        };
        if (AttachedComposeGif() is { } gif)
        {
            saved.Gif = new ComposerDraftGif
            {
                Slug = gif.Item.Slug,
                PreviewUrl = gif.Item.PreviewUrl,
                PreviewWidth = gif.Item.PreviewWidth,
                PreviewHeight = gif.Item.PreviewHeight,
                Url = gif.Item.Url,
                Width = gif.Item.Width,
                Height = gif.Item.Height,
                Query = gif.Query,
            };
        }
        else
        {
            saved.PhotoPaths.AddRange(composeAttachments);
            if (ComposeAltTextInputs(composeAttachments.ToArray()) is { } altTexts)
            {
                saved.AltTexts.AddRange(altTexts);
            }
        }

        if (composePoll.Active)
        {
            for (var index = 0; index < composePoll.OptionCount; index++)
            {
                saved.PollOptions.Add(composePoll.Option(index));
            }

            saved.PollDurationIndex = composePoll.DurationIndex;
        }

        ComposerDrafts.Put(configuration.ChirperDrafts, accountId, saved);
        configuration.Save();
        toast.Show(Loc.T(ComposeHasVideo ? L.Chirper.DraftSavedNoVideo : L.Social.DraftSaved));
    }

    private void RestoreComposeDraft(ComposerDraft saved)
    {
        ClearComposeForm();
        draft = saved.Text;
        composeDraftId = saved.Id;
        var photos = ComposerDrafts.ExistingPaths(saved.PhotoPaths, out var missing);
        for (var index = 0; index < photos.Count; index++)
        {
            AddComposeAttachment(photos[index]);
        }

        RestoreDraftAltTexts(saved);
        composeReplyPolicy = ChirperReplyPolicy.Clamp(saved.ReplyPolicy);

        if (saved.Gif is { } gif && composeAttachments.Count == 0)
        {
            composeGif = new GifPick(new GifItem(gif.Slug, gif.PreviewUrl, gif.PreviewWidth, gif.PreviewHeight,
                gif.Url, gif.Width, gif.Height), gif.Query);
            composeAttachments.Add(gif.Url);
        }

        if (saved.PollOptions.Count > 0 && composeAttachments.Count == 0 && saved.QuotedPostId is null)
        {
            RestoreComposePoll(saved.PollOptions, saved.PollDurationIndex);
        }

        if (saved.QuotedPostId is { } quotedId)
        {
            LoadComposeQuote(quotedId);
        }

        composeSensitive = saved.Sensitive && composeAttachments.Count > 0;
        composeStatus = missing > 0 ? Loc.Plural(L.Social.DraftPhotosMissing, missing) : string.Empty;
        composeFocus = true;
    }

    private void RestoreDraftAltTexts(ComposerDraft saved)
    {
        var count = Math.Min(saved.PhotoPaths.Count, saved.AltTexts.Count);
        for (var index = 0; index < count; index++)
        {
            if (composeAttachments.Contains(saved.PhotoPaths[index]))
            {
                PostAltTexts.Set(composeAltTexts, saved.PhotoPaths[index], saved.AltTexts[index]);
            }
        }
    }

    // The preview is fetched again so a quote restored from a draft or a scheduled chirp shows the
    // quoted chirp as it is now, or nothing if it is gone.
    private void LoadComposeQuote(string quotedId)
    {
        quoteTargetId = quotedId;
        store.LoadPost(quotedId, post => Interlocked.Exchange(ref pendingDraftQuote, post));
    }

    private void RestoreComposePoll(IReadOnlyList<string> options, int durationIndex)
    {
        composePoll.Open();
        pollDurationRail.Reset();
        for (var index = 0; index < options.Count; index++)
        {
            if (index >= composePoll.OptionCount)
            {
                if (!composePoll.CanAddOption)
                {
                    break;
                }

                composePoll.AddOption();
            }

            composePoll.Option(index) = options[index];
        }

        composePoll.SelectDuration(durationIndex);
    }

    private void DrawUnsentPanel(Rect area)
    {
        var scale = UiScale.Current;
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Social.UnsentChirps), ChirperInk.Shared, CloseComposePanel,
            ScreenTitleStyle);
        var top = area.Min.Y + AppHeader.Height * scale;
        var tabRow = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + FeedTabRowHeight * scale));
        var picked = UnderlineTabs.Draw(tabRow, Loc.T(L.Social.Drafts), Loc.T(L.Social.Scheduled),
            unsentTab == ScheduledTab, ref unsentTabSlide, ChirperInk.Shared, FeedTabsStyle);
        if (picked >= 0 && picked != unsentTab)
        {
            OpenUnsentPanel(picked);
        }

        var body = new Rect(new Vector2(area.Min.X, tabRow.Max.Y), area.Max);
        if (unsentTab == ScheduledTab)
        {
            DrawScheduledList(body);
            return;
        }

        DrawDraftsList(body);
    }

    private void DrawDraftsList(Rect body)
    {
        var drafts = ComposeDrafts;
        var rows = draftRows.Rows(drafts);
        if (rows.Length == 0)
        {
            EmptyState.Draw(body, ui, PhoneIcons.FileText, Loc.T(L.Social.DraftsEmpty), Loc.T(L.Social.DraftsEmptyHint));
            return;
        }

        var tapped = ComposerDraftList.Draw(body, ui, ChirperInk.Shared, rows, Loc.T(L.Social.DraftDelete), true,
            out var deleted);
        if (deleted >= 0)
        {
            AskDeleteDraft(drafts![deleted].Id);
            return;
        }

        if (tapped >= 0)
        {
            RestoreComposeDraft(drafts![tapped]);
        }
    }

    private void AskDeleteDraft(string draftId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Social.DraftDeleteConfirm),
            ConfirmLabel = Loc.T(L.Common.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => DeleteDraft(draftId),
        });
    }

    private void DeleteDraft(string draftId)
    {
        if (!ComposerDrafts.Remove(configuration.ChirperDrafts, store.AccountId, draftId))
        {
            return;
        }

        if (composeDraftId == draftId)
        {
            composeDraftId = null;
        }

        configuration.Save();
    }

    private void DrawScheduledList(Rect body)
    {
        var items = store.Scheduled;
        if (items.Length == 0)
        {
            DrawScheduledEmpty(body);
            return;
        }

        var rows = ScheduledRows(items);
        var tapped = ComposerDraftList.Draw(body, ui, ChirperInk.Shared, rows, Loc.T(L.Social.ScheduledDelete), true,
            out var deleted);
        if (deleted >= 0)
        {
            AskDeleteScheduled(items[deleted].Id);
            return;
        }

        if (tapped >= 0)
        {
            OpenScheduledEdit(items[tapped]);
        }
    }

    private void DrawScheduledEmpty(Rect body)
    {
        if (store.ScheduledLoading)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * UiScale.Current),
                Loc.T(L.Common.Loading), ChirperInk.MutedInk);
            return;
        }

        var failure = store.ScheduledFailure;
        if (failure.Failed && !store.ScheduledLoaded)
        {
            scheduledFailureSlot.Set(failure);
            if (EmptyState.Draw(body, ui, PhoneIcons.Clock, Loc.T(L.Social.ScheduledLoadFailed),
                    scheduledFailureSlot.Text(), Loc.T(L.Common.Retry)))
            {
                store.LoadScheduled();
            }

            return;
        }

        EmptyState.Draw(body, ui, PhoneIcons.Clock, Loc.T(L.Social.ScheduledEmpty),
            Loc.T(L.Social.ScheduledEmptyHint));
    }

    private void AskDeleteScheduled(string scheduledId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Social.ScheduledDeleteConfirm),
            ConfirmLabel = Loc.T(L.Common.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => store.DeleteScheduled(scheduledId),
        });
    }

    private ReadOnlySpan<DraftListRow> ScheduledRows(ScheduledPostDto[] items)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (scheduledRowsVersion == store.ScheduledVersion && scheduledRowsFormat == TimeText.FormatVersion
            && ReferenceEquals(scheduledRowsLanguage, Loc.Current) && scheduledRows.Length == items.Length
            && now - scheduledRowsBuiltAt < ScheduledRowsRefreshSeconds)
        {
            return scheduledRows;
        }

        scheduledRowsVersion = store.ScheduledVersion;
        scheduledRowsFormat = TimeText.FormatVersion;
        scheduledRowsLanguage = Loc.Current;
        scheduledRowsBuiltAt = now;
        if (scheduledRows.Length != items.Length)
        {
            scheduledRows = new DraftListRow[items.Length];
        }

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var meta = item.Failed
                ? Loc.T(L.Social.ScheduledFailed, FailureText.Resolve(
                    new AepFailure(AepFailureKind.Server, 0, item.LastError, null, null, null)))
                : Loc.T(L.Social.ScheduleWillPost, TimeText.FutureMoment(item.PublishAtUnix));
            scheduledRows[index] = new DraftListRow(ComposerDrafts.PreviewText(item.Post.Text), meta, item.Failed);
        }

        return scheduledRows;
    }
}
