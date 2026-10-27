using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ClusterIcon
{
    private const float Offset = 0.6f;
    private const float Block = 0.5f;
    private const float Rounding = 0.2f;
    private const float Dot = 0.17f;
    private const float Spark = 0.2f;
    private const float RayInner = 0.32f;
    private const float RayOuter = 0.56f;
    private const float RayWidth = 0.11f;
    private const int Rays = 6;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var block = Block * extent;
        var rounding = Rounding * extent;
        var topLeft = At(center, extent, -Offset, -Offset);
        var bottomLeft = At(center, extent, -Offset, Offset);
        var bottomRight = At(center, extent, Offset, Offset);
        Piece(drawList, topLeft, block, rounding, ink);
        Piece(drawList, bottomLeft, block, rounding, ink);
        Piece(drawList, bottomRight, block, rounding, ink);
        drawList.AddRectFilled(new Vector2(topLeft.X - block, topLeft.Y),
            new Vector2(bottomLeft.X + block, bottomLeft.Y), ink);
        drawList.AddRectFilled(new Vector2(bottomLeft.X, bottomLeft.Y - block),
            new Vector2(bottomRight.X, bottomRight.Y + block), ink);
        drawList.AddCircleFilled(topLeft, Dot * extent, hole, 16);
        drawList.AddCircleFilled(bottomLeft, Dot * extent, hole, 16);
        drawList.AddCircleFilled(bottomRight, Dot * extent, hole, 16);
        var spark = At(center, extent, Offset, -Offset);
        drawList.AddCircleFilled(spark, Spark * extent, ink, 16);
        for (var ray = 0; ray < Rays; ray++)
        {
            var angle = ray * MathF.PI * 2f / Rays - MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(spark + direction * (RayInner * extent), spark + direction * (RayOuter * extent), ink,
                MathF.Max(1f, RayWidth * extent));
        }
    }

    private static void Piece(ImDrawListPtr drawList, Vector2 center, float block, float rounding, uint ink)
    {
        drawList.AddRectFilled(center - new Vector2(block, block), center + new Vector2(block, block), ink, rounding);
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY) =>
        center + new Vector2(unitX, unitY) * extent;
}
