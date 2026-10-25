using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Coil;

internal static class CoilGuide
{
    private const float Diameter = CoilBoard.Diameter;
    private const float Radius = CoilBoard.MarbleRadius;
    private const byte Red = 0;
    private const byte Yellow = 1;
    private const byte Green = 2;
    private const byte Blue = 3;
    private const int MaxChain = 12;
    private const int PathCapacity = 64;
    private const int TrackSamples = 96;
    private const float CornerRadius = 0.09f;
    private const int CornerSteps = 10;
    private const float FadeIn = 0.06f;
    private const float FadeOut = 0.9f;
    private const float SettleSpan = 0.08f;
    private const float RecoilSpan = 0.08f;
    private const float ShrinkSpan = 0.05f;
    private const float PopSpan = 0.16f;
    private const int PopSparks = 6;
    private const float IdleHeat = 0.1f;
    private const float MaxHeat = 0.55f;
    private const float FingerBeyond = 0.05f;
    private const float HeartSize = 0.036f;
    private const float HeartGap = 0.06f;
    private const float CrossSize = 0.07f;
    private const float ChipWidth = 50f;
    private const float ChipHeight = 22f;
    private const float ChipInset = 8f;

    private const float AimPeriod = 3.6f;
    private const float AimBack = 0.34f;
    private const int AimHit = 3;
    private const float AimFire = 0.3f;
    private const float AimLand = 0.42f;

    private const float MatchPeriod = 5.4f;
    private const float MatchBack = 0.3f;
    private const int MatchHit = 4;
    private const float MatchFire = 0.2f;
    private const float MatchLand = 0.28f;
    private const float MatchFirstPop = 0.36f;
    private const float MatchFirstPull = 0.46f;
    private const float MatchSecondPop = 0.57f;
    private const float MatchSecondPull = 0.68f;
    private const float PullSpan = 0.1f;

    private const float PowerPeriod = 5.6f;
    private const float PowerBack = 0.3f;
    private const int PowerHit = 3;
    private const float PowerCrawl = 0.6f;
    private const float PowerSwap = 0.1f;
    private const float PowerFire = 0.3f;
    private const float PowerLand = 0.37f;
    private const float PowerPop = 0.42f;
    private const float PowerChipEnd = 0.95f;

    private const float VortexPeriod = 5f;
    private const float VortexApproach = 5f;
    private const float VortexDrain = 0.42f;
    private const float VortexDrained = 0.8f;
    private const float VortexLifeLost = 0.46f;
    private const float VortexCross = 0.5f;

    private static readonly Rect World = new(new Vector2(0.04f, 0.01f), new Vector2(0.7f, 0.47f));
    private static readonly Vector2 LauncherAt = new(0.32f, 0.3f);
    private static readonly Vector2 FingerRest = new(0.24f, 0.2f);
    private static readonly Vector2 SpaceAt = new(0.32f, 0.425f);
    private static readonly Vector2 HeartsAt = new(0.32f, 0.165f);
    private static readonly Vector2 CrossOffset = new(-0.085f, -0.1f);
    private static readonly Vector2 LabelOffset = new(0f, 0.075f);
    private static readonly Vector4 ChainGold = new(1f, 0.86f, 0.42f, 1f);
    private static readonly byte[] AimColours = { Green, Blue, Red, Yellow, Green, Blue, Yellow };
    private static readonly byte[] MatchColours = { Green, Blue, Yellow, Red, Red, Yellow, Yellow, Blue, Green };
    private static readonly byte[] PowerColours = { Yellow, Blue, Green, Green, Red, Blue, Yellow };
    private static readonly byte[] VortexColours = { Red, Yellow, Green, Blue, Red, Yellow, Blue, Green, Yellow, Red };
    private static readonly string ChainTwo = string.Concat("x", GameNumber.Label(2));
    private static readonly CoilTrack Track = BuildTrack();

    public static readonly GameGuide Guide = new(
        new GuidePage(L.Coil.Guide1Title, L.Coil.Guide1Body, DrawAimPage),
        new GuidePage(L.Coil.Guide2Title, L.Coil.Guide2Body, DrawMatchPage),
        new GuidePage(L.Coil.Guide3Title, L.Coil.Guide3Body, DrawPowerPage),
        new GuidePage(L.Coil.Guide4Title, L.Coil.Guide4Body, DrawVortexPage));

    private struct ChainMarble
    {
        public float Arc;
        public float Size;
        public byte Colour;
        public CoilPower Power;
    }

    private static void DrawAimPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, AimPeriod);
        var camera = Frame(art);
        var impactArc = AimBack + (AimHit + 0.5f) * Diameter;
        var impact = Track.PositionAt(impactArc);
        var settle = Easing.SmoothStep(GuideKit.Window(phase, AimLand, AimLand + SettleSpan));
        DrawStage(drawList, in camera, accent, IdleHeat, seconds, scale);
        Span<ChainMarble> chain = stackalloc ChainMarble[MaxChain];
        var count = 0;
        for (var index = 0; index < AimColours.Length; index++)
        {
            var shift = index > AimHit ? settle * Diameter : 0f;
            count = Add(chain, count, AimBack + index * Diameter + shift, AimColours[index], 1f);
            if (index == AimHit && phase >= AimLand)
            {
                count = Add(chain, count, impactArc + settle * Diameter * 0.5f, Blue, 1f);
            }
        }

        DrawChain(drawList, in camera, chain[..count], SceneAlpha(phase), seconds);
        DrawShot(drawList, in camera, impact, GuideKit.Window(phase, AimFire, AimLand), Blue, seconds);
        var finger = Finger(impact, phase, 0.06f, 0.22f, 0.7f, 0.86f);
        var fired = phase >= AimFire;
        DrawLauncher(drawList, in camera, finger, accent, Recoil(phase, AimFire), 0f, fired ? Yellow : Blue,
            fired ? Green : Yellow, seconds);
        GuideKit.Tap(drawList, camera.ToScreen(finger), GuideKit.Window(phase, 0.27f, 0.63f), scale);
    }

    private static void DrawMatchPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, MatchPeriod);
        var camera = Frame(art);
        var impactArc = MatchBack + (MatchHit + 0.5f) * Diameter;
        var impact = Track.PositionAt(impactArc);
        var settle = Easing.SmoothStep(GuideKit.Window(phase, MatchLand, MatchLand + SettleSpan));
        var firstPull = Pull(phase, MatchFirstPull);
        var secondPull = Pull(phase, MatchSecondPull);
        var redSize = Shrink(phase, MatchFirstPop);
        var yellowSize = Shrink(phase, MatchSecondPop);
        DrawStage(drawList, in camera, accent, IdleHeat, seconds, scale);
        Span<ChainMarble> chain = stackalloc ChainMarble[MaxChain];
        var count = 0;
        for (var index = 0; index < MatchColours.Length; index++)
        {
            var arc = MatchBack + index * Diameter;
            if (index > MatchHit)
            {
                arc += settle * Diameter - firstPull;
            }

            if (index > MatchHit + 2)
            {
                arc -= secondPull;
            }

            var size = index is 3 or 4 ? redSize : index is 2 or 5 or 6 ? yellowSize : 1f;
            count = Add(chain, count, arc, MatchColours[index], size);
            if (index == MatchHit && phase >= MatchLand)
            {
                count = Add(chain, count, impactArc + settle * Diameter * 0.5f, Red, redSize);
            }
        }

        DrawChain(drawList, in camera, chain[..count], SceneAlpha(phase), seconds);
        DrawPop(drawList, in camera, Track.PositionAt(MatchBack + 4f * Diameter), Red,
            GuideKit.Window(phase, MatchFirstPop, MatchFirstPop + PopSpan));
        var yellowCenter = Track.PositionAt(MatchBack + 3f * Diameter);
        DrawPop(drawList, in camera, yellowCenter, Yellow,
            GuideKit.Window(phase, MatchSecondPop, MatchSecondPop + PopSpan));
        DrawChainLabel(drawList, in camera, yellowCenter + LabelOffset,
            GuideKit.Window(phase, MatchSecondPop, MatchSecondPop + 0.24f));
        DrawShot(drawList, in camera, impact, GuideKit.Window(phase, MatchFire, MatchLand), Red, seconds);
        var finger = Finger(impact, phase, 0.04f, 0.16f, 0.5f, 0.66f);
        var fired = phase >= MatchFire;
        DrawLauncher(drawList, in camera, finger, accent, Recoil(phase, MatchFire), 0f, fired ? Yellow : Red,
            fired ? Blue : Yellow, seconds);
        GuideKit.Tap(drawList, camera.ToScreen(finger), GuideKit.Window(phase, 0.17f, 0.53f), scale);
    }

    private static void DrawPowerPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, PowerPeriod);
        var camera = Frame(art);
        var back = PowerBack + Crawl(phase);
        var impactArc = PowerBack + Crawl(PowerLand) + (PowerHit + 0.5f) * Diameter;
        var impact = Track.PositionAt(impactArc);
        var settle = Easing.SmoothStep(GuideKit.Window(phase, PowerLand, PowerLand + SettleSpan));
        var greenSize = Shrink(phase, PowerPop);
        DrawStage(drawList, in camera, accent, IdleHeat, seconds, scale);
        Span<ChainMarble> chain = stackalloc ChainMarble[MaxChain];
        var count = 0;
        for (var index = 0; index < PowerColours.Length; index++)
        {
            var shift = index > PowerHit ? settle * Diameter : 0f;
            var size = index is 2 or 3 ? greenSize : 1f;
            count = Add(chain, count, back + index * Diameter + shift, PowerColours[index], size,
                index == PowerHit ? CoilPower.Freeze : CoilPower.None);
            if (index == PowerHit && phase >= PowerLand)
            {
                count = Add(chain, count, back + (PowerHit + 0.5f) * Diameter + settle * Diameter * 0.5f, Green,
                    greenSize, CoilPower.None);
            }
        }

        DrawChain(drawList, in camera, chain[..count], SceneAlpha(phase), seconds);
        var popCenter = Track.PositionAt(back + PowerHit * Diameter);
        DrawPop(drawList, in camera, popCenter, Green, GuideKit.Window(phase, PowerPop, PowerPop + PopSpan));
        DrawFreeze(drawList, in camera, popCenter, GuideKit.Window(phase, PowerPop, PowerPop + 0.3f), scale);
        if (phase >= PowerPop && phase < PowerChipEnd)
        {
            var chipMin = camera.ToScreen(World.Min) + new Vector2(ChipInset, ChipInset) * scale;
            var remaining = 1f - 0.5f * GuideKit.Window(phase, PowerPop, 1f);
            CoilApp.DrawChip(drawList, chipMin, new Vector2(ChipWidth, ChipHeight) * scale, CoilPower.Freeze,
                remaining, false, scale);
        }

        DrawShot(drawList, in camera, impact, GuideKit.Window(phase, PowerFire, PowerLand), Green, seconds);
        var finger = Finger(impact, phase, 0.14f, 0.26f, 0.66f, 0.82f);
        var swapped = phase >= PowerSwap;
        var fired = phase >= PowerFire;
        var loaded = fired ? Red : swapped ? Green : Red;
        var next = fired ? Blue : swapped ? Red : Green;
        var swap = swapped ? 1f - GuideKit.Window(phase, PowerSwap, PowerSwap + 0.1f) : 0f;
        DrawLauncher(drawList, in camera, finger, accent, Recoil(phase, PowerFire), swap, loaded, next, seconds);
        GuideKit.SpaceKey(drawList, camera.ToScreen(SpaceAt), phase >= 0.08f && phase < 0.14f, accent, scale);
        GuideKit.Tap(drawList, camera.ToScreen(finger), GuideKit.Window(phase, 0.28f, 0.64f), scale);
    }

    private static void DrawVortexPage(ImDrawListPtr drawList, Rect art, float seconds, Vector4 accent, float scale)
    {
        var phase = GuideKit.Loop(seconds, VortexPeriod);
        var camera = Frame(art);
        var approach = GuideKit.Window(phase, 0f, VortexDrain) * VortexApproach * Diameter;
        var drain = Easing.EaseInCubic(GuideKit.Window(phase, VortexDrain, VortexDrained)) *
                    (VortexColours.Length + 1) * Diameter;
        var lead = Track.Length - VortexApproach * Diameter + approach + drain;
        var progress = Math.Clamp(lead / Track.Length, 0f, 1f);
        var heat = phase >= VortexDrain ? MaxHeat : MathF.Min(MaxHeat, progress * progress);
        DrawStage(drawList, in camera, accent, heat, seconds, scale);
        Span<ChainMarble> chain = stackalloc ChainMarble[MaxChain];
        var count = 0;
        for (var index = 0; index < VortexColours.Length; index++)
        {
            count = Add(chain, count, lead - (VortexColours.Length - 1 - index) * Diameter, VortexColours[index], 1f);
        }

        DrawChain(drawList, in camera, chain[..count], SceneAlpha(phase), seconds);
        DrawLauncher(drawList, in camera, Track.PositionAt(MathF.Min(lead, Track.Length)), accent, 0f, 0f, Red,
            Yellow, seconds);
        var lost = Easing.SmoothStep(GuideKit.Window(phase, VortexLifeLost, VortexLifeLost + 0.06f)) *
                   (1f - GuideKit.Window(phase, FadeOut, 1f));
        DrawHearts(drawList, in camera, accent, lost);
        var badge = Easing.EaseOutCubic(GuideKit.Window(phase, VortexCross, VortexCross + 0.08f)) *
                    (1f - GuideKit.Window(phase, FadeOut - 0.04f, FadeOut + 0.02f));
        if (badge > 0.01f)
        {
            GuideKit.Cross(drawList, camera.ToScreen(Track.End + CrossOffset), camera.Px(CrossSize) * badge, scale);
        }
    }

    private static CoilTrack BuildTrack()
    {
        Span<Vector2> points = stackalloc Vector2[PathCapacity];
        var count = Line(points, 0, new Vector2(0.11f, 0.42f), new Vector2(0.11f, 0.175f), 8);
        count = Corner(points, count, new Vector2(0.2f, 0.175f), MathF.PI);
        count = Line(points, count, new Vector2(0.2f, 0.085f), new Vector2(0.44f, 0.085f), 8);
        count = Corner(points, count, new Vector2(0.44f, 0.175f), MathF.PI * 1.5f);
        count = Line(points, count, new Vector2(0.53f, 0.175f), new Vector2(0.53f, 0.3f), 4);
        var track = new CoilTrack(count, TrackSamples);
        track.Build(points[..count], LauncherAt, CoilBoard.SampleSpacing);
        return track;
    }

    private static int Line(Span<Vector2> points, int count, Vector2 from, Vector2 to, int steps)
    {
        if (count == 0)
        {
            points[count++] = from;
        }

        for (var step = 1; step <= steps; step++)
        {
            points[count++] = Vector2.Lerp(from, to, step / (float)steps);
        }

        return count;
    }

    private static int Corner(Span<Vector2> points, int count, Vector2 center, float startAngle)
    {
        for (var step = 1; step <= CornerSteps; step++)
        {
            var angle = startAngle + MathF.PI * 0.5f * step / CornerSteps;
            points[count++] = center + CoilShapes.Polar(angle) * CornerRadius;
        }

        return count;
    }

    private static Camera2D Frame(Rect art)
    {
        var camera = default(Camera2D);
        camera.Fit(art, World, FitMode.Contain);
        return camera;
    }

    private static float SceneAlpha(float phase) =>
        GuideKit.Window(phase, 0f, FadeIn) * (1f - GuideKit.Window(phase, FadeOut, 1f));

    private static float Recoil(float phase, float fire) =>
        phase < fire ? 0f : 1f - GuideKit.Window(phase, fire, fire + RecoilSpan);

    private static float Shrink(float phase, float start) =>
        1f - Easing.SmoothStep(GuideKit.Window(phase, start, start + ShrinkSpan));

    private static float Pull(float phase, float start) =>
        Easing.EaseInCubic(GuideKit.Window(phase, start, start + PullSpan)) * 3f * Diameter;

    private static float Crawl(float phase) =>
        MathF.Min(phase, PowerPop) / PowerPop * PowerCrawl * Diameter;

    private static Vector2 Finger(Vector2 target, float phase, float reachStart, float reachEnd, float leaveStart,
        float leaveEnd)
    {
        var reach = Easing.SmoothStep(GuideKit.Window(phase, reachStart, reachEnd)) *
                    (1f - Easing.SmoothStep(GuideKit.Window(phase, leaveStart, leaveEnd)));
        return Vector2.Lerp(FingerRest, target + Toward(target) * FingerBeyond, reach);
    }

    private static Vector2 Toward(Vector2 target)
    {
        var direction = target - LauncherAt;
        return direction.LengthSquared() < 0.000001f ? new Vector2(0f, -1f) : Vector2.Normalize(direction);
    }

    private static int Add(Span<ChainMarble> chain, int count, float arc, byte colour, float size,
        CoilPower power = CoilPower.None)
    {
        chain[count] = new ChainMarble { Arc = arc, Size = size, Colour = colour, Power = power };
        return count + 1;
    }

    private static void DrawStage(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent, float heat,
        float seconds, float scale)
    {
        CoilRenderer.DrawTrack(drawList, Track, in camera, accent, scale, seconds);
        CoilRenderer.DrawVortex(drawList, camera.ToScreen(Track.End), camera.Px(Radius), accent, heat, seconds);
    }

    private static void DrawChain(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<ChainMarble> chain,
        float alpha, float seconds)
    {
        var radius = camera.Px(Radius);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var index = 0; index < chain.Length; index++)
            {
                ref readonly var marble = ref chain[index];
                var size = marble.Size * CoilRenderer.SizeFactor(Track, marble.Arc);
                if (size <= 0.01f)
                {
                    continue;
                }

                var center = camera.ToScreen(Track.PositionAt(marble.Arc));
                if (pass == 0)
                {
                    CoilArt.Shadow(drawList, center, radius * size, alpha);
                    continue;
                }

                CoilArt.Marble(drawList, center, radius * size, marble.Colour, marble.Power, marble.Arc / Radius,
                    Track.TangentAt(marble.Arc), alpha, seconds);
            }
        }
    }

    private static void DrawShot(ImDrawListPtr drawList, in Camera2D camera, Vector2 target, float flight,
        byte colour, float seconds)
    {
        if (flight <= 0f || flight >= 1f)
        {
            return;
        }

        var direction = Toward(target);
        var mouth = LauncherAt + direction * CoilBoard.MouthDistance;
        CoilRenderer.DrawShot(drawList, camera.ToScreen(Vector2.Lerp(mouth, target, flight)), direction,
            camera.Px(Radius), colour, CoilPower.None, seconds);
    }

    private static void DrawLauncher(ImDrawListPtr drawList, in Camera2D camera, Vector2 aim, Vector4 accent,
        float recoil, float swap, byte loaded, byte next, float seconds)
    {
        var radius = camera.Px(Radius);
        var direction = Toward(aim);
        CoilRenderer.DrawAimDots(drawList, camera.ToScreen(LauncherAt + direction * CoilBoard.MouthDistance),
            direction, radius, CoilArt.ColourOf(loaded));
        CoilRenderer.DrawLauncher(drawList, camera.ToScreen(LauncherAt), radius, direction, accent, recoil, swap,
            loaded, next, CoilPower.None, seconds);
    }

    private static void DrawPop(ImDrawListPtr drawList, in Camera2D camera, Vector2 world, byte colour,
        float progress)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return;
        }

        var radius = camera.Px(Radius);
        var center = camera.ToScreen(world);
        var tint = GamePalette.Lighten(CoilArt.ColourOf(colour), 0.3f);
        var spread = Easing.EaseOutCubic(progress);
        var fade = 1f - progress;
        drawList.AddCircle(center, radius * (1.2f + 2.6f * spread), ImGui.GetColorU32(tint with { W = 0.85f * fade }),
            32, MathF.Max(1f, radius * 0.22f * fade));
        for (var spark = 0; spark < PopSparks; spark++)
        {
            var direction = CoilShapes.Polar(spark * MathF.Tau / PopSparks + colour * 0.4f);
            drawList.AddCircleFilled(center + direction * radius * (0.6f + 2.8f * spread),
                MathF.Max(1f, radius * 0.22f * fade), ImGui.GetColorU32(tint with { W = fade }), 8);
        }
    }

    private static void DrawFreeze(ImDrawListPtr drawList, in Camera2D camera, Vector2 world, float progress,
        float scale)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return;
        }

        var radius = camera.Px(Radius);
        var center = camera.ToScreen(world);
        var color = CoilArt.PowerColour(CoilPower.Freeze);
        var spread = Easing.EaseOutCubic(progress);
        var fade = 1f - progress;
        drawList.AddCircle(center, radius * (1.5f + 5.5f * spread), ImGui.GetColorU32(color with { W = 0.6f * fade }),
            48, MathF.Max(1f, radius * 0.18f));
        CoilArt.PowerGlyph(drawList, center, radius * (0.6f + 0.5f * spread), CoilPower.Freeze,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.3f) with { W = fade }), radius * 0.16f);
        Typography.DrawCentered(drawList, center + new Vector2(0f, radius * (2.4f + 1.2f * spread)),
            Loc.T(L.Coil.PowerFreeze), color with { W = MathF.Min(1f, fade * 1.6f) }, TextStyles.Headline);
    }

    private static void DrawChainLabel(ImDrawListPtr drawList, in Camera2D camera, Vector2 world, float progress)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return;
        }

        var rise = Easing.EaseOutCubic(progress) * camera.Px(Radius);
        var alpha = MathF.Min(1f, (1f - progress) * 2.5f);
        Typography.DrawCentered(drawList, camera.ToScreen(world) - new Vector2(0f, rise), ChainTwo,
            ChainGold with { W = alpha }, TextStyles.Title1);
    }

    private static void DrawHearts(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent, float lost)
    {
        var size = camera.Px(HeartSize);
        var dim = StageInks.Muted with { W = 0.35f };
        for (var heart = 0; heart < CoilBoard.StartLives; heart++)
        {
            var offset = (heart - (CoilBoard.StartLives - 1) * 0.5f) * HeartGap;
            var center = camera.ToScreen(HeartsAt + new Vector2(offset, 0f));
            var last = heart == CoilBoard.StartLives - 1;
            var color = last ? Vector4.Lerp(accent, dim, lost) : accent;
            var bump = last ? 1f + 0.35f * MathF.Sin(MathF.PI * lost) : 1f;
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Heart, color, size * bump);
        }
    }
}
