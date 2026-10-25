using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Cluster;

internal static class ClusterRenderer
{
    public const float GapFraction = 0.08f;
    public const float BarHeight = 10f;
    public const float ButtonHeight = 40f;
    public const float ButtonMaxWidth = 188f;
    private const float PieceRounding = 0.42f;
    private const float GlyphSize = 0.46f;
    private const int StarPoints = 5;
    private const float StarInner = 0.45f;
    private const float RingRadius = 13f;
    private const float RingThickness = 3f;
    private const float ButtonPadding = 14f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 GlyphShadow = new(0f, 0f, 0f, 0.28f);
    private static readonly Vector4 Track = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 Danger = new(0.97f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 Warning = new(1f, 0.70f, 0.26f, 1f);
    private static readonly Vector4 Leftover = new(0.42f, 0.44f, 0.50f, 1f);
    private static readonly Vector4[] Colors =
    {
        new(0.95f, 0.34f, 0.40f, 1f), new(0.28f, 0.56f, 0.98f, 1f), new(0.99f, 0.78f, 0.24f, 1f),
        new(0.27f, 0.80f, 0.48f, 1f), new(0.71f, 0.46f, 0.97f, 1f), new(0.22f, 0.82f, 0.88f, 1f),
    };

    public static ReadOnlySpan<Vector4> Palette => Colors;

    public static Vector4 ColorOf(int color) => Colors[Math.Clamp(color - 1, 0, Colors.Length - 1)];

    public static Vector4 Faded(int color, float amount) => Vector4.Lerp(ColorOf(color), Leftover, amount);

    public static void DrawPiece(ImDrawListPtr drawList, Vector2 center, float half, int color, float alpha,
        float glow, float scale)
    {
        DrawPiece(drawList, center, half, ColorOf(color), color, alpha, glow, scale);
    }

    public static void DrawPiece(ImDrawListPtr drawList, Vector2 center, float half, Vector4 fill, int shape,
        float alpha, float glow, float scale)
    {
        if (half <= 0.5f || alpha <= 0.01f)
        {
            return;
        }

        var lit = glow > 0f ? GamePalette.Lighten(fill, 0.18f * glow) : fill;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var rounding = half * PieceRounding * 2f;
        var shadowOffset = new Vector2(0f, MathF.Max(1f, half * 0.12f));
        Squircle.Fill(drawList, min + shadowOffset, max + shadowOffset, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f * alpha)));
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(lit, 0.16f) with { W = alpha }),
            ImGui.GetColorU32(GamePalette.Darken(lit, 0.18f) with { W = alpha }));
        Squircle.Fill(drawList, min + new Vector2(half * 0.16f, half * 0.12f),
            new Vector2(max.X - half * 0.16f, center.Y - half * 0.1f), rounding * 0.7f,
            ImGui.GetColorU32(White with { W = 0.16f * alpha }));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Darken(fill, 0.45f) with { W = 0.45f * alpha }), MathF.Max(1f, scale));
        var glyphRadius = half * GlyphSize;
        var drop = new Vector2(0f, MathF.Max(1f, half * 0.07f));
        DrawGlyph(drawList, center + drop, glyphRadius, shape,
            ImGui.GetColorU32(GlyphShadow with { W = 0.28f * alpha }));
        DrawGlyph(drawList, center, glyphRadius, shape, ImGui.GetColorU32(White with { W = 0.94f * alpha }));
    }

    public static void DrawGlyph(ImDrawListPtr drawList, Vector2 center, float radius, int shape, uint color)
    {
        switch (shape)
        {
            case 1:
                drawList.AddCircleFilled(center, radius * 0.86f, color, 24);
                return;
            case 2:
                drawList.AddQuadFilled(center + new Vector2(0f, -radius * 1.05f),
                    center + new Vector2(radius * 0.86f, 0f), center + new Vector2(0f, radius * 1.05f),
                    center + new Vector2(-radius * 0.86f, 0f), color);
                return;
            case 3:
                DrawStar(drawList, center + new Vector2(0f, radius * 0.06f), radius * 1.08f, color);
                return;
            case 4:
                drawList.AddTriangleFilled(center + new Vector2(0f, -radius * 0.98f),
                    center + new Vector2(radius * 0.98f, radius * 0.74f),
                    center + new Vector2(-radius * 0.98f, radius * 0.74f), color);
                return;
            case 5:
                drawList.AddRectFilled(center - new Vector2(radius * 0.74f, radius * 0.74f),
                    center + new Vector2(radius * 0.74f, radius * 0.74f), color, radius * 0.18f);
                return;
            default:
                var arm = radius * 0.3f;
                var reach = radius * 0.92f;
                drawList.AddRectFilled(center - new Vector2(arm, reach), center + new Vector2(arm, reach), color,
                    arm * 0.4f);
                drawList.AddRectFilled(center - new Vector2(reach, arm), center + new Vector2(reach, arm), color,
                    arm * 0.4f);
                return;
        }
    }

    private static void DrawStar(ImDrawListPtr drawList, Vector2 center, float radius, uint color)
    {
        const int vertices = StarPoints * 2;
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        var previous = StarVertex(center, radius, vertices - 1);
        for (var vertex = 0; vertex < vertices; vertex++)
        {
            var next = StarVertex(center, radius, vertex);
            drawList.AddTriangleFilled(center, previous, next, color);
            previous = next;
        }

        drawList.Flags = flags;
        for (var vertex = 0; vertex < vertices; vertex++)
        {
            drawList.PathLineTo(StarVertex(center, radius, vertex));
        }

        drawList.PathStroke(color, ImDrawFlags.Closed, 1f);
    }

    private static Vector2 StarVertex(Vector2 center, float radius, int vertex)
    {
        var angle = -MathF.PI * 0.5f + vertex * MathF.PI / StarPoints;
        var reach = vertex % 2 == 0 ? radius : radius * StarInner;
        return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
    }

    public static Vector4 BarColor(float fraction, Vector4 accent)
    {
        if (fraction >= 0.5f)
        {
            return GamePalette.Lighten(accent, 0.12f);
        }

        if (fraction >= 0.25f)
        {
            return Vector4.Lerp(Warning, GamePalette.Lighten(accent, 0.12f), (fraction - 0.25f) / 0.25f);
        }

        return Vector4.Lerp(Danger, Warning, fraction / 0.25f);
    }

    public static void DrawBar(ImDrawListPtr drawList, Rect rect, float fraction, float gainFrom, float gainAlpha,
        bool frozen, Vector4 accent, float scale)
    {
        var height = rect.Height;
        var radius = height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Track));
        var width = rect.Width;
        var fill = Math.Clamp(fraction, 0f, 1f);
        var color = BarColor(fill, accent);
        if (fill <= 0.25f && !frozen)
        {
            color = Vector4.Lerp(color, White, 0.25f * Pulse.Wave(Pulse.Fast));
        }

        if (fill > 0.001f)
        {
            var end = new Vector2(rect.Min.X + MathF.Max(height, width * fill), rect.Max.Y);
            ProgressRing.Glow(new Vector2(end.X - radius, rect.Center.Y), height * 1.1f, color, 0.35f);
            Squircle.FillVerticalGradient(drawList, rect.Min, end, radius,
                ImGui.GetColorU32(GamePalette.Lighten(color, 0.2f)),
                ImGui.GetColorU32(GamePalette.Darken(color, 0.12f)));
            Squircle.Fill(drawList, rect.Min + new Vector2(radius * 0.6f, height * 0.18f),
                new Vector2(end.X - radius * 0.6f, rect.Min.Y + height * 0.42f), height * 0.12f,
                ImGui.GetColorU32(White with { W = 0.28f }));
        }

        if (gainAlpha > 0.01f && gainFrom < fill)
        {
            var from = rect.Min.X + width * Math.Clamp(gainFrom, 0f, 1f);
            var to = rect.Min.X + MathF.Max(height, width * fill);
            Squircle.Fill(drawList, new Vector2(MathF.Max(rect.Min.X, from - radius), rect.Min.Y),
                new Vector2(to, rect.Max.Y), radius, ImGui.GetColorU32(White with { W = 0.75f * gainAlpha }));
        }

        if (frozen)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(White with { W = 0.35f }),
                MathF.Max(1f, scale));
        }
    }

    public static Rect ButtonRect(Rect dock, float scale)
    {
        var width = MathF.Min(dock.Width, ButtonMaxWidth * scale);
        var height = MathF.Min(dock.Height, ButtonHeight * scale);
        var center = dock.Center;
        return new Rect(center - new Vector2(width, height) * 0.5f, center + new Vector2(width, height) * 0.5f);
    }

    public static void DrawButton(ImDrawListPtr drawList, Rect button, string label, float charge, bool ready,
        bool hovered, float flash, Vector4 accent, float scale)
    {
        var radius = button.Height * 0.5f;
        var pulse = ready ? Pulse.Wave(Pulse.Fast) : 0f;
        if (ready)
        {
            ProgressRing.Glow(button.Center, button.Width * 0.42f, accent, 0.55f + 0.35f * pulse + flash);
        }

        StageHud.Capsule(drawList, button, scale, ready ? 0.95f : 0.8f);
        if (ready)
        {
            Squircle.Fill(drawList, button.Min, button.Max, radius,
                ImGui.GetColorU32(accent with { W = 0.55f + 0.2f * pulse + (hovered ? 0.15f : 0f) }));
        }

        Squircle.Stroke(drawList, button.Min, button.Max, radius,
            ImGui.GetColorU32((ready ? GamePalette.Lighten(accent, 0.4f) : accent) with { W = ready ? 0.9f : 0.45f }),
            1.5f * scale);
        var ringCenter = new Vector2(button.Min.X + ButtonPadding * scale + RingRadius * scale, button.Center.Y);
        var ringRadius = RingRadius * scale;
        var thickness = RingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, ringRadius, thickness, StageInks.Track);
        if (charge > 0f)
        {
            ProgressRing.Fill(drawList, ringCenter, ringRadius, thickness, Math.Clamp(charge, 0f, 1f),
                ready ? StageInks.Strong : GamePalette.Lighten(accent, 0.25f));
        }

        ProgressRing.CenterIcon(drawList, ringCenter, FontAwesomeIcon.Random,
            ready ? StageInks.Strong : StageInks.Muted, ringRadius * 0.95f);
        var textLeft = ringCenter.X + ringRadius + ButtonPadding * 0.6f * scale;
        var textCenter = new Vector2((textLeft + button.Max.X - ButtonPadding * scale) * 0.5f, button.Center.Y);
        Typography.DrawCentered(drawList, textCenter, label, ready ? StageInks.Strong : StageInks.Muted,
            TextStyles.SubheadlineEmphasized);
    }

    public static void DrawCursor(ImDrawListPtr drawList, Vector2 center, float half, Vector4 accent, float scale)
    {
        var pad = half * 0.18f;
        var min = center - new Vector2(half + pad, half + pad);
        var max = center + new Vector2(half + pad, half + pad);
        var alpha = 0.65f + 0.35f * Pulse.Wave(Pulse.Fast);
        Squircle.Stroke(drawList, min, max, (half + pad) * PieceRounding * 2f,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.45f) with { W = alpha }), 2.5f * scale);
    }
}
