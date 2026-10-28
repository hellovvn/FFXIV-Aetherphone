using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Reels;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float ReelPillMargin = 12f;

    private readonly ReelSharePill reelSharePill = new();

    private void DrainReelShare()
    {
        if (reelEditor.IsOpen && !router.IsTransitioning && router.Current.Screen != AethergramScreen.Compose)
        {
            ResetReelCompose();
        }

        if (store.ReelShare is not { } job)
        {
            return;
        }

        var state = job.State;
        if (state.Stage == ReelShareStage.Done)
        {
            store.AcceptCreatedReel(state.Created);
            store.DismissReelShare();
            toast.Show(Loc.T(L.Aethergram.ReelShared));
            return;
        }

        if (state.Stage == ReelShareStage.Cancelled)
        {
            store.DismissReelShare();
        }
    }

    private void DrawReelSharePill(Rect screen)
    {
        if (store.ReelShare is not { } job || router.Current.Screen == AethergramScreen.Compose)
        {
            return;
        }

        var scale = UiScale.Current;
        var margin = ReelPillMargin * scale;
        var bottom = router.Depth == 1 ? TabBar.ContentArea(screen, scale).Max.Y - margin : screen.Max.Y - margin;
        if (reelSharePill.Draw(screen, bottom, job, ui, Ink, L.Aethergram.ReelPosting))
        {
            store.DismissReelShare();
        }
    }
}
