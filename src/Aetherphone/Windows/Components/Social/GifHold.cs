using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class GifHold
{
    private const float SidePadding = 12f;
    private const float PillPadX = 10f;
    private const float PillPadY = 5f;

    private static readonly TextStyle LabelStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 PillFill = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 PillInk = new(1f, 1f, 1f, 0.95f);

    public static void Draw(ImDrawListPtr drawList, Rect rect, float rounding, ImDrawFlags corners, Vector4 fill)
    {
        var scale = UiScale.Current;
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(fill), rounding, corners);
        var padding = new Vector2(PillPadX * scale, PillPadY * scale);
        var label = Typography.FitText(Loc.T(L.Gifs.TapToShow),
            MathF.Max(1f, rect.Width - (SidePadding * 2f + PillPadX * 2f) * scale), LabelStyle);
        var size = Typography.Measure(label, LabelStyle);
        var pillMin = rect.Center - size * 0.5f - padding;
        var pillMax = rect.Center + size * 0.5f + padding;
        drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(PillFill), (pillMax.Y - pillMin.Y) * 0.5f);
        Typography.Draw(drawList, pillMin + padding, label, PillInk, LabelStyle);
        if (UiInteract.Hover(rect.Min, rect.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
