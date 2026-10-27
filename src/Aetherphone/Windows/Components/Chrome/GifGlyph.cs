using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class GifGlyph
{
    private const string Label = "GIF";
    private const float HeightShare = 0.72f;
    private const float RoundingShare = 0.16f;
    private const float Stroke = 1.6f;

    private static readonly TextStyle LabelStyle = new(0.58f, FontWeight.Bold);

    public static void Draw(ImDrawListPtr drawList, Vector2 center, Vector4 ink, float size)
    {
        var half = new Vector2(size * 0.5f, size * HeightShare * 0.5f);
        drawList.AddRect(center - half, center + half, ImGui.GetColorU32(ink), size * RoundingShare, ImDrawFlags.None,
            MathF.Max(1f, Stroke * UiScale.Current));
        Typography.DrawCentered(drawList, center, Label, ink, LabelStyle);
    }
}
