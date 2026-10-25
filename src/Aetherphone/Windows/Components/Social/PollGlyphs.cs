using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PollGlyphs
{
    public const float RadioRadius = 10f;

    private const float RingThickness = 1.6f;
    private const float CheckThickness = 2f;

    public static void RadioRing(ImDrawListPtr drawList, Vector2 center, Vector4 ink, float scale)
    {
        drawList.AddCircle(center, RadioRadius * scale, ImGui.GetColorU32(ink), 32, RingThickness * scale);
    }

    public static void CheckBadge(ImDrawListPtr drawList, Vector2 center, float grow, Vector4 fill, Vector4 mark,
        float scale)
    {
        if (grow <= 0.01f)
        {
            return;
        }

        var radius = RadioRadius * scale * grow;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
        var color = ImGui.GetColorU32(mark);
        var thickness = CheckThickness * scale;
        var elbow = center + new Vector2(-0.12f * radius, 0.36f * radius);
        drawList.AddLine(center + new Vector2(-0.46f * radius, 0.02f * radius), elbow, color, thickness);
        drawList.AddLine(elbow, center + new Vector2(0.48f * radius, -0.34f * radius), color, thickness);
    }
}
