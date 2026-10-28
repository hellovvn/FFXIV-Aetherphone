using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private readonly ReelEngineSetup reelSetup = new("Aethergram reels setup");
    private bool reelSetupDismissed;

    private bool ReelSetupNeeded => reelPlayback.LibraryMissing && !reelSetupDismissed;

    private void DrawReelSetup(Rect area)
    {
        if (reelSetup.Draw(area, ui, theme, reelPlayback.Dependencies, L.Aethergram.ReelSetupTitle,
                L.Aethergram.ReelSetupBody))
        {
            reelSetupDismissed = true;
        }
    }

    private void InstallReelPlayer()
    {
        reelSetup.Install(reelPlayback.Dependencies);
    }

    private static float ReelChipY(Rect overlay) => overlay.Min.Y + overlay.Height * 0.3f;

    private void DrawReelPlayerChip(ImDrawListPtr drawList, Rect overlay)
    {
        var center = new Vector2(overlay.Center.X, ReelChipY(overlay));
        if (!reelPlayback.LibraryMissing)
        {
            DrawReelChip(drawList, center, Loc.T(L.Aethergram.ReelVideoUnavailable));
            return;
        }

        var installing = DependencySetup.IsBusy(reelPlayback.Dependencies.VideoLibrary.Snapshot());
        var chip = DrawReelChip(drawList, center,
            Loc.T(installing ? L.Aethergram.ReelInstallingPlayer : L.Aethergram.ReelInstallPlayer));
        if (installing)
        {
            return;
        }

        var hovered = UiInteract.Hover(chip.Min, chip.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(chip.Min, chip.Max, hovered))
        {
            InstallReelPlayer();
        }
    }
}
