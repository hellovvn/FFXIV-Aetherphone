using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Reels;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private enum ComposeMode : byte
    {
        Post,
        Story,
        Reel,
    }

    private enum ReelComposeStage : byte
    {
        Pick,
        Edit,
        Details,
    }

    private const int ComposeModeCount = 3;
    private const float ComposeModeStripHeight = 52f;
    private const float ComposeModeStripWidth = 280f;
    private const float ComposeModeTrackHeight = 32f;
    private const float ReelProblemInset = 28f;
    private const int MaxReelSeconds = (int)ReelFormat.MaxDurationSeconds;

    private static readonly IReelMedia ReelMedia = new WindowsReelMedia();

    private readonly string[] composeModeLabels = new string[ComposeModeCount];
    private readonly ReelTrimEditor reelEditor = new(ReelMedia);
    private bool composeReelMode;
    private ReelComposeStage reelStage;
    private ReelTranscodeFailure reelProbeFailure;
    private int reelProbeTicket;
    private volatile ReelProbed? reelProbeArrived;
    private volatile string? reelPickedPath;
    private bool reelProbing;
    private CachedText reelChooseHint;

    private ComposeMode ActiveComposeMode =>
        composeReelMode ? ComposeMode.Reel : composeStoryMode ? ComposeMode.Story : ComposeMode.Post;

    private void StartReelCompose()
    {
        PrepareReelCompose();
        router.Push(AethergramRoute.Compose);
    }

    private void PrepareReelCompose()
    {
        PrepareCompose(false, false);
        composeReelMode = true;
    }

    private void ResetReelCompose()
    {
        composeReelMode = false;
        reelStage = ReelComposeStage.Pick;
        reelProbeFailure = ReelTranscodeFailure.None;
        reelProbing = false;
        reelPickedPath = null;
        reelProbeArrived = null;
        reelProbeTicket++;
        reelEditor.Close();
    }

    private float DrawComposeModeStrip(Rect area)
    {
        if (composeAvatarMode)
        {
            return area.Max.Y;
        }

        var scale = UiScale.Current;
        var top = area.Max.Y - ComposeModeStripHeight * scale;
        var width = MathF.Min(area.Width - CellPadX * 2f * scale, ComposeModeStripWidth * scale);
        var row = new Rect(new Vector2(area.Center.X - width * 0.5f, top),
            new Vector2(area.Center.X + width * 0.5f, area.Max.Y));
        composeModeLabels[(int)ComposeMode.Post] = Loc.T(L.Aethergram.ComposeModePost);
        composeModeLabels[(int)ComposeMode.Story] = Loc.T(L.Aethergram.ComposeModeStory);
        composeModeLabels[(int)ComposeMode.Reel] = Loc.T(L.Aethergram.ComposeModeReel);
        var current = (int)ActiveComposeMode;
        var picked = SegmentStrip.Draw("aethergram.compose.mode", row, composeModeLabels, current, ui.Palette,
            ComposeModeTrackHeight);
        if (picked != current && !store.Posting && !stories.Posting)
        {
            SwitchComposeMode((ComposeMode)picked);
        }

        return top;
    }

    private void SwitchComposeMode(ComposeMode mode)
    {
        switch (mode)
        {
            case ComposeMode.Reel:
                PrepareReelCompose();
                break;
            case ComposeMode.Story:
                PrepareCompose(false, true);
                break;
            default:
                PrepareCompose(false, false);
                break;
        }
    }

    private void DrawReelCompose(Rect area)
    {
        AcceptReelPick();
        switch (reelStage)
        {
            case ReelComposeStage.Edit:
                DrawReelEdit(area);
                break;
            case ReelComposeStage.Details:
                DrawReelDetails(area);
                break;
            default:
                DrawReelPick(area);
                break;
        }
    }

    private void DrawReelPick(Rect area)
    {
        var scale = UiScale.Current;
        DrawComposeHeader(area, Loc.T(L.Aethergram.NewReel), true, CloseCompose, string.Empty, false);
        var stripTop = DrawComposeModeStrip(area);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale),
            new Vector2(area.Max.X, stripTop));
        if (reelProbing)
        {
            LoadingPulse.Draw(body.Center, ReelTrimEditor.SpinnerRadius * scale, Accent, Ink.MutedInk,
                Loc.T(L.Aethergram.ReelChecking));
            return;
        }

        var hint = reelChooseHint.IsCurrent(MaxReelSeconds)
            ? reelChooseHint.Value
            : reelChooseHint.Store(MaxReelSeconds, Loc.Plural(L.Aethergram.ReelChooseHint, MaxReelSeconds));
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.Film, Loc.T(L.Aethergram.ReelChooseTitle), hint,
                Loc.T(L.Aethergram.ImportFromPc)))
        {
            FilePicker.PickVideo(Loc.T(L.Aethergram.ReelChooseDialog), picked => reelPickedPath = picked);
        }

        if (reelProbeFailure == ReelTranscodeFailure.None)
        {
            return;
        }

        var problem = Loc.T(ReelShareText.ForTranscode(reelProbeFailure));
        var inset = ReelProblemInset * scale;
        var problemHeight = Typography.MeasureWrappedBlock(problem, TextStyles.Footnote, body.Width - inset * 2f).Y;
        Typography.DrawWrappedCentered(new Vector2(body.Center.X, body.Max.Y - problemHeight - inset), problem,
            Ink.Danger, TextStyles.Footnote, body.Width - inset * 2f);
    }

    private void AcceptReelPick()
    {
        if (Interlocked.Exchange(ref reelPickedPath, null) is { } picked)
        {
            reelProbeFailure = ReelTranscodeFailure.None;
            reelProbing = true;
            var ticket = ++reelProbeTicket;
            _ = Task.Run(() => ProbeReelAsync(picked, ticket));
        }

        if (Interlocked.Exchange(ref reelProbeArrived, null) is not { } arrived || arrived.Ticket != reelProbeTicket)
        {
            return;
        }

        reelProbing = false;
        var verdict = arrived.Result.Verdict;
        if (verdict != ReelTranscodeFailure.None)
        {
            reelProbeFailure = verdict;
            return;
        }

        reelEditor.Open(arrived.Path, arrived.Result.Info);
        reelStage = ReelComposeStage.Edit;
    }

    private async Task ProbeReelAsync(string path, int ticket)
    {
        var result = await ReelMedia.ProbeAsync(path, CancellationToken.None).ConfigureAwait(false);
        reelProbeArrived = new ReelProbed(path, ticket, result);
    }

    private sealed record ReelProbed(string Path, int Ticket, ReelProbeResult Result);
}
