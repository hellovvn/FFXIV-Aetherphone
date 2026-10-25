using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal static class GuideKit
{
    private const float FingerRadius = 11f;
    private const float RippleSpread = 2.6f;
    private const float PressDepth = 0.18f;
    private const float KeySize = 34f;
    private const float KeyPadX = 10f;
    private const float KeyRadius = 8f;
    private const float KeyDrop = 3f;
    private const int TrailDots = 6;
    private const float TrailSpacing = 0.06f;
    private const float ArrowHead = 9f;
    private const float ArrowWidth = 2.5f;
    private const float SpaceWidth = 3.2f;
    private const float GlyphShare = 0.42f;
    private const float GlyphStroke = 2f;
    private const float MarkStroke = 3f;
    private static readonly Vector4 Good = new(0.30f, 0.78f, 0.45f, 1f);
    private static readonly Vector4 Bad = new(0.92f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 Finger = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 FingerShadow = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 KeyFace = new(0.26f, 0.26f, 0.32f, 0.95f);
    private static readonly Vector4 KeyEdge = new(0.12f, 0.12f, 0.16f, 0.95f);

    public static float Loop(float seconds, float period) => period <= 0f ? 0f : seconds % period / period;

    public static float Window(float phase, float start, float end) =>
        end <= start ? (phase >= end ? 1f : 0f) : Math.Clamp((phase - start) / (end - start), 0f, 1f);

    public static Vector2 Point(Rect art, float x, float y) =>
        new(art.Min.X + art.Width * x, art.Min.Y + art.Height * y);

    public static Rect Fit(Rect art, float aspect)
    {
        var width = MathF.Min(art.Width, art.Height * aspect);
        var half = new Vector2(width, width / aspect) * 0.5f;
        return new Rect(art.Center - half, art.Center + half);
    }

    public static void Tap(ImDrawListPtr drawList, Vector2 center, float phase, float scale)
    {
        var radius = FingerRadius * scale;
        var press = Window(phase, 0f, 0.2f) - Window(phase, 0.3f, 0.45f);
        var ripple = Window(phase, 0.2f, 0.75f);
        if (ripple > 0f && ripple < 1f)
        {
            var ring = radius * (1f + (RippleSpread - 1f) * Easing.EaseOutCubic(ripple));
            drawList.AddCircle(center, ring, ImGui.GetColorU32(Finger with { W = 0.6f * (1f - ripple) }), 32,
                2f * scale);
        }

        var pressed = radius * (1f - PressDepth * press);
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), pressed, ImGui.GetColorU32(FingerShadow), 24);
        drawList.AddCircleFilled(center, pressed, ImGui.GetColorU32(Finger), 24);
    }

    public static void Swipe(ImDrawListPtr drawList, Vector2 from, Vector2 to, float phase, float scale)
    {
        var travel = Easing.SmoothStep(Window(phase, 0.1f, 0.7f));
        var fade = 1f - Window(phase, 0.8f, 1f);
        for (var dot = TrailDots; dot >= 1; dot--)
        {
            var back = Math.Clamp(travel - dot * TrailSpacing, 0f, 1f);
            if (back <= 0f || travel <= 0f)
            {
                continue;
            }

            var weight = 1f - dot / (float)(TrailDots + 1);
            drawList.AddCircleFilled(Vector2.Lerp(from, to, back), FingerRadius * scale * weight * 0.7f,
                ImGui.GetColorU32(Finger with { W = 0.35f * weight * fade }), 16);
        }

        Tap(drawList, Vector2.Lerp(from, to, travel), travel > 0f && travel < 1f ? 0.25f : 0f, scale);
    }

    public static Rect Key(ImDrawListPtr drawList, Vector2 center, string label, bool down, Vector4 accent,
        float scale)
    {
        var width = MathF.Max(KeySize * scale, Typography.Measure(label, TextStyles.SubheadlineEmphasized).X +
                                                KeyPadX * 2f * scale);
        var face = Cap(drawList, center, width, down, accent, scale);
        Typography.DrawCentered(drawList, face.Center, label, StageInks.Strong, TextStyles.SubheadlineEmphasized);
        return face;
    }

    public static Rect ArrowKey(ImDrawListPtr drawList, Vector2 center, Vector2 direction, bool down,
        Vector4 accent, float scale)
    {
        var face = Cap(drawList, center, KeySize * scale, down, accent, scale);
        var length = direction.Length();
        if (length <= 0.001f)
        {
            return face;
        }

        var forward = direction / length;
        var side = new Vector2(-forward.Y, forward.X);
        var size = KeySize * scale * GlyphShare;
        var tip = face.Center + forward * size * 0.5f;
        var back = face.Center - forward * size * 0.4f;
        drawList.AddTriangleFilled(tip, back + side * size * 0.45f, back - side * size * 0.45f,
            ImGui.GetColorU32(StageInks.Strong));
        return face;
    }

    public static Rect SpaceKey(ImDrawListPtr drawList, Vector2 center, bool down, Vector4 accent, float scale)
    {
        var face = Cap(drawList, center, KeySize * SpaceWidth * scale, down, accent, scale);
        var half = KeySize * scale * GlyphShare;
        var baseline = face.Center.Y + half * 0.25f;
        var rise = half * 0.35f;
        var color = ImGui.GetColorU32(StageInks.Strong);
        var thickness = GlyphStroke * scale;
        drawList.AddLine(new Vector2(face.Center.X - half, baseline - rise), new Vector2(face.Center.X - half, baseline),
            color, thickness);
        drawList.AddLine(new Vector2(face.Center.X - half, baseline), new Vector2(face.Center.X + half, baseline), color,
            thickness);
        drawList.AddLine(new Vector2(face.Center.X + half, baseline), new Vector2(face.Center.X + half, baseline - rise),
            color, thickness);
        return face;
    }

    public static void Check(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        Badge(drawList, center, size, Good);
        var half = size * 0.5f;
        var color = ImGui.GetColorU32(StageInks.Strong);
        var thickness = MarkStroke * scale;
        var corner = center + new Vector2(-half * 0.12f, half * 0.38f);
        drawList.AddLine(center + new Vector2(-half * 0.5f, 0f), corner, color, thickness);
        drawList.AddLine(corner, center + new Vector2(half * 0.52f, -half * 0.42f), color, thickness);
    }

    public static void Cross(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        Badge(drawList, center, size, Bad);
        var reach = size * 0.22f;
        var color = ImGui.GetColorU32(StageInks.Strong);
        var thickness = MarkStroke * scale;
        drawList.AddLine(center - new Vector2(reach, reach), center + new Vector2(reach, reach), color, thickness);
        drawList.AddLine(center + new Vector2(-reach, reach), center + new Vector2(reach, -reach), color, thickness);
    }

    private static void Badge(ImDrawListPtr drawList, Vector2 center, float size, Vector4 fill)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, size * 0.06f), size * 0.5f, ImGui.GetColorU32(FingerShadow),
            32);
        drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(fill), 32);
    }

    private static Rect Cap(ImDrawListPtr drawList, Vector2 center, float width, bool down, Vector4 accent,
        float scale)
    {
        var half = new Vector2(width, KeySize * scale) * 0.5f;
        var drop = KeyDrop * scale;
        var lift = down ? drop : 0f;
        var radius = KeyRadius * scale;
        Squircle.Fill(drawList, center - half + new Vector2(0f, drop), center + half + new Vector2(0f, drop), radius,
            ImGui.GetColorU32(KeyEdge));
        var faceMin = center - half + new Vector2(0f, lift);
        var faceMax = center + half + new Vector2(0f, lift);
        Squircle.Fill(drawList, faceMin, faceMax, radius,
            ImGui.GetColorU32(down ? Palette.Mix(KeyFace, accent, 0.45f) : KeyFace));
        return new Rect(faceMin, faceMax);
    }

    public static void Arrow(ImDrawListPtr drawList, Vector2 from, Vector2 to, Vector4 color, float scale)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 0.001f)
        {
            return;
        }

        direction /= length;
        var head = ArrowHead * scale;
        var normal = new Vector2(-direction.Y, direction.X);
        var color32 = ImGui.GetColorU32(color);
        var shaftEnd = to - direction * head * 0.8f;
        drawList.AddLine(from, shaftEnd, color32, ArrowWidth * scale);
        drawList.AddTriangleFilled(to, to - direction * head + normal * head * 0.6f,
            to - direction * head - normal * head * 0.6f, color32);
    }
}
