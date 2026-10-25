using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class HarvestIcon
{
    private const float RimY = 0.08f;
    private const float FloorY = 0.86f;
    private const float RimHalf = 0.92f;
    private const float FloorHalf = 0.62f;
    private const float GlyphScale = 1.7f;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float tileExtent, uint ink, uint hole)
    {
        var extent = tileExtent * GlyphScale;
        center += new Vector2(0f, extent * 0.06f);
        var stroke = MathF.Max(1f, extent * 0.1f);
        drawList.AddQuadFilled(At(center, extent, -RimHalf, RimY), At(center, extent, RimHalf, RimY),
            At(center, extent, FloorHalf, FloorY), At(center, extent, -FloorHalf, FloorY), ink);
        var weave = MathF.Max(1f, extent * 0.07f);
        for (var row = 1; row <= 2; row++)
        {
            var share = row / 3f;
            var y = RimY + (FloorY - RimY) * share;
            var half = RimHalf + (FloorHalf - RimHalf) * share - 0.08f;
            drawList.AddLine(At(center, extent, -half, y), At(center, extent, half, y), hole, weave);
        }

        for (var slat = -1; slat <= 1; slat++)
        {
            drawList.AddLine(At(center, extent, slat * 0.42f, RimY + 0.1f), At(center, extent, slat * 0.3f, FloorY - 0.08f),
                hole, weave);
        }

        drawList.AddLine(At(center, extent, -RimHalf - 0.06f, RimY), At(center, extent, RimHalf + 0.06f, RimY), ink,
            stroke * 1.4f);
        var gem = At(center, extent, 0.18f, -0.5f);
        var halfWidth = extent * 0.3f;
        var halfHeight = extent * 0.44f;
        drawList.AddQuadFilled(gem - new Vector2(0f, halfHeight), gem + new Vector2(halfWidth, 0f),
            gem + new Vector2(0f, halfHeight), gem - new Vector2(halfWidth, 0f), ink);
        drawList.AddLine(gem - new Vector2(0f, halfHeight * 0.75f), gem + new Vector2(halfWidth * 0.35f, 0f), hole,
            weave);
        drawList.AddLine(At(center, extent, -0.42f, -0.96f), At(center, extent, -0.42f, -0.62f), ink, stroke * 0.8f);
        drawList.AddLine(At(center, extent, -0.18f, -1f), At(center, extent, -0.18f, -0.78f), ink, stroke * 0.8f);
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY) =>
        center + new Vector2(unitX, unitY) * extent;
}
