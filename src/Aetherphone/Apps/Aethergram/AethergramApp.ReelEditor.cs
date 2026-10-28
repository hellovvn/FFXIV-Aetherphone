using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private Action? reelEditBack;
    private Action? reelDetailsBack;

    private Action ReelEditBack => reelEditBack ??= () =>
    {
        reelEditor.Close();
        reelStage = ReelComposeStage.Pick;
    };

    private Action ReelDetailsBack => reelDetailsBack ??= () =>
    {
        composeTagMode = false;
        reelStage = ReelComposeStage.Edit;
    };

    private void DrawReelEdit(Rect area)
    {
        if (DrawComposeHeader(area, Loc.T(L.Aethergram.ReelEditTitle), false, ReelEditBack, Loc.T(L.Aethergram.Next),
                true))
        {
            reelEditor.EndDrag();
            reelStage = ReelComposeStage.Details;
            captionFocus = true;
            return;
        }

        reelEditor.Draw(area, CellPadX * UiScale.Current, theme, Ink, ui.Palette, true);
    }
}
