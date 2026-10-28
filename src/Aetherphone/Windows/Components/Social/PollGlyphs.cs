using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PollGlyphs
{
    public const float RadioRadius = 10f;

    private const float RingThickness = 1.6f;
    private const float CheckThickness = 2f;

    private static readonly float[] BarLengths = { 0.84f, 0.5f, 0.68f };

    public static void Bars(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = size * 0.17f;
        var left = center.X - size * 0.42f;
        var spacing = size * 0.3f;
        for (var index = 0; index < BarLengths.Length; index++)
        {
            var y = center.Y + (index - 1) * spacing;
            drawList.AddRectFilled(new Vector2(left, y - thickness * 0.5f),
                new Vector2(left + size * BarLengths[index], y + thickness * 0.5f), color, thickness * 0.5f);
        }
    }

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
