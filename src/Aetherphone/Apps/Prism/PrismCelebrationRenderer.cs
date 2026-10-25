using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismCelebrationRenderer
{
    private const int ParticleCount = 56;
    private const float CycleSeconds = 7f;
    private static readonly Vector4 Gold = new(1f, 0.81f, 0.39f, 1f);
    private static readonly Vector4 Silver = new(0.73f, 0.87f, 1f, 1f);
    private static readonly Vector4 Bronze = new(1f, 0.65f, 0.48f, 1f);

    public static Vector4 Color(int rank) => rank switch
    {
        1 => Gold,
        2 => Silver,
        3 => Bronze,
        _ => PrismLight.Hues[4],
    };

    public static void Draw(ImDrawListPtr drawList, Rect area, float age, int rank, float scale)
    {
        if (Plugin.PluginInterface.UiBuilder.ShouldUseReducedMotion)
        {
            return;
        }

        var tint = Color(rank);
        var enter = Math.Clamp(age * 1.5f, 0f, 1f);
        drawList.PushClipRect(area.Min, area.Max, true);
        for (var index = 0; index < ParticleCount; index++)
        {
            var seed = index * 0.618034f;
            var horizontal = seed - MathF.Floor(seed);
            var cycle = age / (CycleSeconds + index % 5) + index * 0.137f;
            cycle -= MathF.Floor(cycle);
            var drift = MathF.Sin(age * 0.7f + index * 2.4f) * 14f * scale;
            var center = new Vector2(area.Min.X + horizontal * area.Width + drift,
                area.Min.Y + cycle * area.Height);
            var size = (1.5f + index % 3) * scale;
            var turn = age * (0.7f + index % 3 * 0.2f) + index;
            var axis = new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * size;
            var cross = new Vector2(-axis.Y, axis.X) * 0.4f;
            var hue = rank == 0 || index % 5 == 0 ? PrismLight.Hues[index % PrismLight.Hues.Length] : tint;
            var alpha = enter * MathF.Sin(cycle * MathF.PI) * 0.8f;
            drawList.AddQuadFilled(center - axis, center + cross, center + axis, center - cross,
                ImGui.GetColorU32(Palette.WithAlpha(hue, alpha)));
        }

        PrismSparkles.Around(drawList, new Vector2(area.Center.X, area.Min.Y + area.Height * 0.68f),
            area.Width * 0.4f, rank + 71, 18, rank == 1 || rank == 0 ? PrismGlow.Spectrum : PrismGlow.Pearl,
            enter, scale);
        drawList.PopClipRect();
    }
}
