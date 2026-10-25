using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct RailKeyState(bool Clicked, bool Held);

internal static class RailKey
{
    public static RailKeyState Update(Rect slot, RailSide side, PhoneTheme theme, HardwareKey key, string hint)
    {
        var hit = HardwareButton.HitRect(slot, side);
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        HardwareButton.Draw(ImGui.GetWindowDrawList(), slot, theme, side, key, hovered, held ? 1f : 0f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(hit, hint, LabelSide(side));
        return new RailKeyState(hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left), held);
    }

    private static HoverLabelSide LabelSide(RailSide side) => side switch
    {
        RailSide.Left => HoverLabelSide.Left,
        RailSide.Right => HoverLabelSide.Right,
        RailSide.Top => HoverLabelSide.Above,
        _ => HoverLabelSide.Below,
    };
}
